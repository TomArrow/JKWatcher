
#define LOGDECISIONS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JKClient;

namespace JKWatcher.CameraOperators
{
    // TODO General idea:
    // If a flag is not visible, check if the other connection sees it. And maybe that can help find a better match.
    // If the other connection sees it, we may not have to give chances to recently died players
    // Also maybe de-prioritize players who are already being specced by the other connection, to avoid duplicating of info?
    class FFACameraOperator : CameraOperator
    {

        CancellationTokenSource cts = null;

        private Task backgroundTask = null;


        public override void Initialize()
        {
            base.Initialize();
            startBackground();
        }

        private void startBackground()
        {
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                HasErrored = true;
                OnErrored(new ErroredEventArgs(t.Exception));
                TaskManager.TaskRun(() => {
                    Thread.Sleep(5000);
                    startBackground(); // Try to recover.
                }, $"FFACameraOperator Background Restarter ({serverWindow.ServerName},{serverWindow.netAddress})");
            }, TaskContinuationOptions.OnlyOnFaulted);
            TaskManager.RegisterTask(backgroundTask, $"FFACameraOperator Loop ({serverWindow.ServerName},{serverWindow.netAddress})");
        }

        ~FFACameraOperator()
        {
            Destroy();
        }

        public override void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;
                cts.Cancel();
                if (backgroundTask != null)
                {
                    try
                    {
                        backgroundTask.Wait();
                    }
                    catch (Exception e)
                    {
                        // Task cancelled most likely.
                    }
                }
                cts.Dispose();
                foreach(Connection conn in connections)
                {
                    conn.AlwaysFollowSomeone = true;
                    conn.AllowBotFight = false;
                    conn.HandlesFightBotChatCommands = false;
                    conn.HandleAutoCommands = true;
                }
                base.Destroy();
                isDestroyed = true;
            }

        }

        // first connection [0] follows red flag
        // second connection [1] follows blue flag
        private void Run(CancellationToken ct)
        {

            while (true)
            {
                System.Threading.Thread.Sleep(100);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                if (!infoPool.isIntermission)
                { // No use during intermission, and avoid server errors popping up from trying to follow during intermission
                    lock (connections) // Not 100% sure this is needed but let's be safe.
                    {
                        MainLoopFunction();
                    }
                }

            }
        }


        const int maxAllowedServerConnectionsUpperLimit = 3;
        public int MaxAllowedServerConnections { get; set; } = maxAllowedServerConnectionsUpperLimit;
        private DateTime lastMaxAllowedServerConnectionsChange = DateTime.Now;

        private double destructionDelayMs = 1000.0 * 60.0 * 10.0; // 10 minutes
        private double retryMoreConnectionsDelay = 1000.0 * 60.0 * 60.0; // 60 minutes
        private DateTime destructionDelayStartTime = DateTime.Now;

        private bool lastFrameNeededFightBot = false;

        private void MainLoopFunction()
        {
            int neededConnectionsCount = 1;

            bool needFightBot = false;

            FightBotTargetingMode mode = FightBotTargetingMode.OPTIN;

            int freeSlotsOnServer = infoPool.MaxServerClients;
            if (!infoPool.connectionOptions.silentMode)
            {
                foreach (PlayerInfo pi in infoPool.playerInfo)
                {
                    if (pi.infoValid)
                    {
                        freeSlotsOnServer--;
                        if (pi.chatCommandTrackingStuff.wantsBotFight && pi.team != Team.Spectator)
                        {
                            if(!pi.chatCommandTrackingStuff.fightBotBlacklist || pi.chatCommandTrackingStuff.fightBotBlacklistAllowBrave)
                            {
                                needFightBot = true;
                            }
                        }
                    }
                }
                if ((DateTime.Now - infoPool.lastBerserkerStarted).TotalMinutes < 10.0)
                {
                    needFightBot = true;
                    mode = FightBotTargetingMode.BERSERK;
                }

            }

            infoPool.fightBotTargetingMode = mode;

            if (needFightBot) neededConnectionsCount++;

            // TODO what about that reserved slots thingie? how to check for that?
            // Don't ever fill up the server with the fight bot.
            if (freeSlotsOnServer == 0 && connections.Count > 1 || freeSlotsOnServer == 1 && connections.Count == 1) 
            {
                neededConnectionsCount = 1;
            }


            if (neededConnectionsCount > connections.Count && connections.Count < MaxAllowedServerConnections)
            {
                if(!lastFrameNeededFightBot && needFightBot)
                {
                    // We are likely spawning a fightbot.
                    // Reset its botmode
                    infoPool.sillyMode = SillyMode.GRIPKICKDBS;
                    infoPool.gripDbsMode = GripKickDBSMode.VANILLA;
                }
                getMoreConnections(Math.Min(neededConnectionsCount - connections.Count, MaxAllowedServerConnections - connections.Count));
            }
            //if(activePlayers.Count() <= 1 && connections.Count() > 1) // If only 1 player here, get rid of extra connections.
            if (neededConnectionsCount < connections.Count && connections.Count > 1) // Get rid of extra connections if too many
            {
                if ((DateTime.Now - destructionDelayStartTime).TotalMilliseconds > destructionDelayMs || freeSlotsOnServer == 0) // Don't destroy immediately. Wait 10 minutes. Maybe a player went spec and will come back to play. Or maybe another player connects. Too many connects/disconnects are annoying and result in a lot of tiny demo files.
                {
                    int connectionsToDestroy = Math.Min(connections.Count - 1, connections.Count - neededConnectionsCount);
                    for (int i = connections.Count - 1; i > 0; i--)
                    {
                        if (connectionsToDestroy == 0) break;

                        //if (neededConnectionsCount == 0)
                        //{
                            destroyConnection(connections[i]);
                            connectionsToDestroy--;
                        //}
                    }
                }
            }
            else
            {
                // Connection count is fine. Reset destruction delay start time.
                destructionDelayStartTime = DateTime.Now;
            }


            // Check if there are any connections that are too much. ("Connection limit reached")
            for (int i = connections.Count - 1; i > 0; i--)
            {
                if (connections[i].ConnectionLimitReached && connections.Count > 1 && MaxAllowedServerConnections > 1) // Can't destroy the last connection
                {
                    //MaxAllowedServerConnections = connections.Count - 1; // Think about this some more
                    destroyConnection(connections[i]);
                    MaxAllowedServerConnections--; // Lower our number here.
                    lastMaxAllowedServerConnectionsChange = DateTime.Now;
                }
            }

            if ((DateTime.Now - lastMaxAllowedServerConnectionsChange).TotalMilliseconds > retryMoreConnectionsDelay && MaxAllowedServerConnections < maxAllowedServerConnectionsUpperLimit)
            {
                // After a certain time, let's try more again.
                MaxAllowedServerConnections++;
                lastMaxAllowedServerConnectionsChange = DateTime.Now;
            }

            if(connections.Count >= 2)
            {
                Connection extraConnection = connections[1];
                if (!infoPool.connectionOptions.silentMode)
                {
                    extraConnection.NameOverride = "!bot for cmds";
                } else
                {
                    extraConnection.NameOverride = null;
                }
                int clientNum = extraConnection.ClientNum.GetValueOrDefault(-1);
                if(clientNum >=0 && clientNum < 32)
                {
                    if (needFightBot && infoPool.playerInfo[clientNum].team == Team.Spectator)
                    {
                        extraConnection.AlwaysFollowSomeone = false;
                        extraConnection.AllowBotFight = true;
                        extraConnection.HandlesFightBotChatCommands = true;
                        extraConnection.HandleAutoCommands = false;
                        extraConnection.leakyBucketRequester.requestExecution("team f",RequestCategory.FIGHTBOTSPAWNRELATED,5,2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    } else if (!needFightBot && infoPool.playerInfo[clientNum].team != Team.Spectator)
                    {
                        // Go back to spec
                        extraConnection.AlwaysFollowSomeone = true;
                        extraConnection.AllowBotFight = false;
                        extraConnection.HandlesFightBotChatCommands = false;
                        extraConnection.HandleAutoCommands = true;
                        extraConnection.leakyBucketRequester.requestExecution("team s",RequestCategory.FIGHTBOTSPAWNRELATED,5,2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }

            }

            lastFrameNeededFightBot = needFightBot;

        }



        public override int getRequiredConnectionCount()
        {
            return 1;
        }

        public override string getTypeDisplayName()
        {
            return "FFA";
        }
    }


}
