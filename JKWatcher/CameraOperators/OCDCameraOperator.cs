
#define LOGDECISIONS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JKClient;

namespace JKWatcher.CameraOperators
{
    // TODO General idea:
    // If a flag is not visible, check if the other connection sees it. And maybe that can help find a better match.
    // If the other connection sees it, we may not have to give chances to recently died players
    // Also maybe de-prioritize players who are already being specced by the other connection, to avoid duplicating of info?
    internal class OCDCameraOperator : CameraOperator
    {

        CancellationTokenSource cts = null;

        private Task backgroundTask = null;


        public override void Initialize()
        {
            base.Initialize();
            startBackground();
            this.connections[0].ServerCommandRan += OCDCameraOperator_ServerCommandRan;
        }

        Regex ocDefrag = new Regex(@"\^2\[\^7OC-System\^2\]: (.*?)\^7 has finished in \[\^2(\d+):(\d+.\d+)\^7\]( which is his personal best time)?.( \^2Top10 time!\^7)? Difference to best: \[((\^200:00.000\^7)|(\^2(\d+):(\d+.\d+)\^7))\]\.",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
        Regex ocDefragTop = new Regex(@"\^7\[\^2(?<num>\d+)\^7\]\s*(?<name>[^\s]+)\s*(?<min>\d+):(?<sec>\d+.\d+)", RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);

        private int OCDTimeStringToMilliseconds(string minutesString, string secondsString)
        {

            //int minutes = match.Groups[2].Value.Atoi();
            int minutes = minutesString.Atoi();
            //std::string secondString = vec_num[matchNum][3];
            //float seconds = match.Groups[3].Value.Atof();
            float seconds = secondsString.Atof();
            int milliSeconds = (int)((1000.0f * seconds) + 0.5f);
            //int pureMilliseconds = milliSeconds % 1000;
            //int pureSeconds = milliSeconds / 1000;

            int milliseconds = milliSeconds + minutes * 60 * 1000;
            return milliseconds;
        }
        private bool TryTopMatch(string cmdArg)
        {
            Match match = ocDefragTop.Match(cmdArg);

            if (!match.Success)
            {
                return false;
            }

            int milliseconds = OCDTimeStringToMilliseconds(match.Groups["min"].Value, match.Groups["sec"].Value);
            int rank = match.Groups["num"].Value.Atoi();
            string username  = match.Groups["name"].Value;
            if (rank != 1) return true;


            string mapname = this.connections[0].GetMapName();


            DefragAverageMapTime maptime = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(mapname);
            if (maptime == null)
            {
                maptime = new DefragAverageMapTime(mapname);
                AsyncPersistentDataManager<DefragAverageMapTime>.addItem(maptime);
            }
            maptime.SetLoggedUserNameRecord(username, milliseconds);

            return true;
        }
        
        private bool TryRunFinishMatch(string cmdArg)
        {
            Match match = ocDefrag.Match(cmdArg);

            if (!match.Success)
            {
                return false;
            }

            int milliseconds = OCDTimeStringToMilliseconds(match.Groups[2].Value, match.Groups[3].Value);
            string playerName = match.Groups[1].Value;
            bool isLogged = match.Groups[5].Value.Length > 0;
            bool isNumber1 = match.Groups[7].Value.Length > 0;
            bool isPersonalBest = match.Groups[4].Value.Length > 0;

            int allMinutes = milliseconds / 1000 / 60 + 1;

            //if((allMinutes * 1000 * 60- milliSeconds) < 10000)
            if ((allMinutes * 1000 * 60 - milliseconds) < 10000)
            {
                allMinutes++;
            }

            string mapname = this.connections[0].GetMapName();
            foreach (PlayerInfo pi in infoPool.playerInfo)
            {
                if (pi.infoValid && pi.name == playerName)
                {
                    foreach (var tracker in pi.GetChatCommandTrackers())
                    {
                        tracker.defragRunsFinished++;
                        tracker.defragMapsRun.AddOrIncrement(mapname, 1);
                        if (isNumber1)
                        {
                            tracker.defragWRCount++;
                            tracker.defragMapsWR.AddOrIncrement(mapname, 1);
                        }
                        else if (isLogged)
                        {
                            tracker.defragTop10RunCount++;
                            tracker.defragMapsTop10.AddOrIncrement(mapname, 1);
                        }
                        tracker.defragTotalRunTime += milliseconds;
                        tracker.defragAverageMapTimes.AddSample(mapname, milliseconds);
                        tracker.defragBestTimes.AddOrPickLower(mapname, milliseconds);
                    }

                    DefragAverageMapTime maptime = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(mapname);
                    if (maptime == null)
                    {
                        maptime = new DefragAverageMapTime(mapname);
                        AsyncPersistentDataManager<DefragAverageMapTime>.addItem(maptime);
                    }
                    maptime.LogTime(playerName, milliseconds, isLogged, isNumber1);
                }
            }

            if (isNumber1)
            {
                this.connections[0].MarkDemoManual(allMinutes, false, -1, playerName, "World Record Run");
            }
            return true;
        }
        
        private void OCDCameraOperator_ServerCommandRan(CommandEventArgs obj)
        {
            if(obj.Command.Argc < 2 || !obj.Command.Argv(0).Equals("print",StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
            string arg = obj.Command.Argv(1);
            if (TryRunFinishMatch(arg))
            {
                //serverWindow.addToLog($"Finished run detected.");
            } else if(TryTopMatch(arg))
            {
                //serverWindow.addToLog($"Top10 entry detected.");
            }
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
                }, $"OCDCameraOperator Background Restarter ({serverWindow.ServerName},{serverWindow.netAddress})");
            }, TaskContinuationOptions.OnlyOnFaulted);
            TaskManager.RegisterTask(backgroundTask, $"OCDCameraOperator Loop ({serverWindow.ServerName},{serverWindow.netAddress})");
        }

        ~OCDCameraOperator()
        {
            Destroy();
        }

        public override void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;
                this.connections[0].ServerCommandRan -= OCDCameraOperator_ServerCommandRan;
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

        List<int> lastActivePlayers = new List<int>();

        const int maxAllowedServerConnectionsUpperLimit = 10;
        public int MaxAllowedServerConnections { get; set; } = maxAllowedServerConnectionsUpperLimit;
        private DateTime lastMaxAllowedServerConnectionsChange = DateTime.Now;

        private Dictionary<int, int> lastFollowedPlayers = new Dictionary<int, int>(); // clientnum (watcher client) -> clientnum (player) dictionary. remember who we were watching last loop. to detect changes.
        private Dictionary<int, DateTime> lastFollowedPlayerChanges = new Dictionary<int, DateTime>(); // clientnum (watcher client) -> DateTime of last change.

        private double destructionDelayMs = 1000.0 * 60.0 * 10.0; // 10 minutes
        private double retryMoreConnectionsDelay = 1000.0 * 60.0 * 60.0; // 60 minutes
        private DateTime destructionDelayStartTime = DateTime.Now;

        private DateTime[] clientNumsWereOurs = new DateTime[64];

        private void MainLoopFunction()
        {
            // First, let's figure out the current count of players.
            List<int> activePlayers = new List<int>();
            List<int> myOwnConnections = new List<int>();
            foreach(Connection conn in connections)
            {
                int thisClientNum = conn.client.clientNum;
                myOwnConnections.Add(thisClientNum);
                if (thisClientNum >= 0 && thisClientNum < 64)
                {
                    clientNumsWereOurs[thisClientNum] = DateTime.Now;
                }
            }
            int maxClients = connections[0].client.ClientHandler != null ? connections[0].client.ClientHandler.MaxClients : 32;
            if (maxClients > 64) maxClients = 64;
            for (int i=0;i< maxClients; i++)
            {
                if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].team != Team.Spectator && !myOwnConnections.Contains(i) && ((DateTime.Now-clientNumsWereOurs[i]).TotalSeconds > 5 || infoPool.GameSeconds > 5)){  // clientNumsWereOurs & GameSeconds: I'm trying this to avoid spawning useless connections after server crashes/restarts
                    
                    activePlayers.Add(i); // Don't count myself. Don't count spectators.
                                                                                                                                                               
                }
            }

            bool activePlayerPoolChanged = !Enumerable.SequenceEqual(activePlayers, lastActivePlayers);

            /*bool newActivePlayerPoolIsSubsetofOld = true;
            // Strike this: Check if new active player pool is subset of old. In which case dont reset disconnect timeout.
            // This is actually nonsense. Don't do this. That's the very reason this exists in the first place.
            foreach(int activePlayer in activePlayers)
            {
                if (!lastActivePlayers.Contains(activePlayer))
                {
                    newActivePlayerPoolIsSubsetofOld = false;
                    break;
                }
            }*/

            if (activePlayers.Count > MaxAllowedServerConnections && activePlayerPoolChanged) // Also do this if MaxAllowedServerConnections changed
            {
                Console.Beep();
            }

            if (activePlayerPoolChanged/* && !newActivePlayerPoolIsSubsetofOld*/)
            {
                // I guess we could track this for individual connections but let's just keep it simple. Destruction either happens or not, no matter how many destructions are needed.
                // We want 10 mminute delay for destruction. During which active player pool should not change. // Strike this: Edit: Or if it changes, it should only change in that individual active players became inactive. Aka: No new players or other changes other than that.
                // Might, in some situations, result in too many connections existing for longer than the desired 10 minutes past their "expiration date" but whatever.
                destructionDelayStartTime = DateTime.Now;
            }

            if (activePlayers.Count > connections.Count && connections.Count < MaxAllowedServerConnections) getMoreConnections(Math.Min(activePlayers.Count- connections.Count, MaxAllowedServerConnections-connections.Count)); // If more than 1 player here, get another connection
            //if(activePlayers.Count() <= 1 && connections.Count() > 1) // If only 1 player here, get rid of extra connections.
            if(activePlayers.Count < connections.Count && connections.Count > 1) // Get rid of extra connections if too many
            {
                if((DateTime.Now- destructionDelayStartTime).TotalMilliseconds > destructionDelayMs) // Don't destroy immediately. Wait 10 minutes. Maybe a player went spec and will come back to play. Or maybe another player connects. Too many connects/disconnects are annoying and result in a lot of tiny demo files.
                {
                    int connectionsToDestroy = Math.Min(connections.Count-1,connections.Count - activePlayers.Count);
                    for (int i = connections.Count-1; i > 0; i--)
                    {
                        if (connectionsToDestroy == 0) break;
                        int currentlySpeccedPlayer = connections[i].client.playerStateClientNum;

                        // Check if the player this connection follows is being followed by another connection (otherwise not really safe to destroy)
                        int othersFollowingSamePlayer = 0;
                        for(int b = 0; b < connections.Count; b++)
                        {
                            if (b != i && connections[b].client.playerStateClientNum == currentlySpeccedPlayer) othersFollowingSamePlayer++;
                        }

                        if(activePlayers.Count() == 0 || connections[i].client.playerStateClientNum == connections[i].ClientNum || (infoPool.playerInfo[currentlySpeccedPlayer].velocity.Length() < 0.0000001f && infoPool.playerInfo[currentlySpeccedPlayer].score.score == 0 && (DateTime.Now-infoPool.playerInfo[currentlySpeccedPlayer].score.lastScoreValueChanged).TotalSeconds > 10 && othersFollowingSamePlayer > 0))
                        {
                            // We destroy the connection if there are no active players OR if it isn't spectating anyone anyway OR:
                            // Explanation for the velocity part:
                            // We have 2+ connections. We have 0 or 1 players. But we never destroy connections[0]. 
                            // It's possible that the active player is being spectated by connections[1].
                            // If we destroy connections[1], it will interrupt the demo until connections[0] spectates that player again.
                            // This could interrupt a good defrag run.
                            // So to be safe, we only destroy a connection if the player it is spectating is not moving.
                            // Additionally, make sure the player's score is 0. The score is the time into the map. On very long maps like mountains, a player might have a velocity of 0 temporarily despite being in the middle of the run.
                            destroyConnection(connections[i]);
                            connectionsToDestroy--;
                        }
                    }
                }
            } else
            {
                // Connection count is fine. Reset destruction delay start time.
                destructionDelayStartTime = DateTime.Now;
            }


            // Check if there are any connections that are too much. ("Connection limit reached")
            for(int i = connections.Count - 1; i > 0; i--)
            {
                if (connections[i].ConnectionLimitReached && connections.Count > 1 && MaxAllowedServerConnections > 1) // Can't destroy the last connection
                {
                    //MaxAllowedServerConnections = connections.Count - 1; // Think about this some more
                    destroyConnection(connections[i]);
                    MaxAllowedServerConnections--; // Lower our number here.
                    lastMaxAllowedServerConnectionsChange = DateTime.Now;
                }
            }

            if((DateTime.Now - lastMaxAllowedServerConnectionsChange).TotalMilliseconds > retryMoreConnectionsDelay && MaxAllowedServerConnections < maxAllowedServerConnectionsUpperLimit)
            {
                // After a certain time, let's try more again.
                MaxAllowedServerConnections++;
                lastMaxAllowedServerConnectionsChange = DateTime.Now;
            }

            List<int> activeButUnfollowedPlayers = new List<int>();
            activeButUnfollowedPlayers.AddRange(activePlayers);

            // Detect changes in who we are following and remember the times a change happened.
            Dictionary<int,int> lastFollowedPlayersCopy = new Dictionary<int, int>(lastFollowedPlayers);
            lastFollowedPlayers.Clear();
            foreach (Connection conn in connections)
            {
                int myClientNum = conn.ClientNum.GetValueOrDefault(-1);
                int currentlySpeccedPlayer = conn.client.playerStateClientNum;

                if (activeButUnfollowedPlayers.Contains(currentlySpeccedPlayer)) activeButUnfollowedPlayers.Remove(currentlySpeccedPlayer);

                if (!lastFollowedPlayersCopy.ContainsKey(myClientNum) || lastFollowedPlayersCopy[myClientNum] != currentlySpeccedPlayer || !lastFollowedPlayerChanges.ContainsKey(myClientNum))
                {
                    lastFollowedPlayerChanges[myClientNum] = DateTime.Now;
                }
                lastFollowedPlayers[myClientNum] = currentlySpeccedPlayer;
            }

            Queue<Connection> connectionsToDestroyList = new Queue<Connection>(); // Secondary place where connections get destroyed, more focused on individual ones instead of as a whole. This makes sure connections don't linger for too long when there's clearly too many of them.

            bool isIndex0 = true;
            foreach (Connection conn in connections)
            {
                int myClientNum = conn.ClientNum.GetValueOrDefault(-1);
                int currentlySpeccedPlayer = conn.client.playerStateClientNum;

                // Check if any other connnection is speccing the same player for longer than this one
                bool anyOtherConnectionBeenSpeccingLonger = false; // This variable indicates there's another watcher client watching this player and been doing so for longer than this one
                bool anyOtherConnectionBeenSpeccingEquallyLongAndSmallerIndex = false; // If been speccing equally long, change connection with higher index.
                bool anyOtherConnectionBeenSpeccingShorterButIsIndex0AndWeAreSpeccingLongerThanRemovalDelay = false; // We never deleted the connection with index 0
                bool subConnIsIndex0 = true;
                foreach (Connection subConn in connections)
                { 
                    if(!Object.ReferenceEquals(conn, subConn))
                    {
                        int subClientNum = subConn.ClientNum.GetValueOrDefault(-1);
                        // I'm doing it in this convoluted way because I want to reuse the data from the above loop that checked for followed player changes.
                        // Due to multithreading, things may have changed in the meantime, at least theoretically, and could result in weirdness. Followed player may have changed but lastFollowedPlayerChanges would not have been updated.
                        // Likewise, I'm checking whether the clientNum exists as a key because the clientnum may have changed (a freshly created connection may not have had a clientnum yet)
                        bool clientNumValuesAreStable = lastFollowedPlayers.ContainsKey(myClientNum) && lastFollowedPlayers.ContainsKey(subClientNum);
                        if (clientNumValuesAreStable)
                        {
                            if (lastFollowedPlayers[subClientNum] == lastFollowedPlayers[myClientNum] && lastFollowedPlayerChanges[subClientNum] < lastFollowedPlayerChanges[myClientNum]) anyOtherConnectionBeenSpeccingLonger = true;
                            if (lastFollowedPlayers[subClientNum] == lastFollowedPlayers[myClientNum] && lastFollowedPlayerChanges[subClientNum] == lastFollowedPlayerChanges[myClientNum] && subClientNum < myClientNum) anyOtherConnectionBeenSpeccingEquallyLongAndSmallerIndex = true;
                            if (lastFollowedPlayers[subClientNum] == lastFollowedPlayers[myClientNum] && subConnIsIndex0 && destructionDelayMs < (DateTime.Now - lastFollowedPlayerChanges[subClientNum]).TotalMilliseconds) anyOtherConnectionBeenSpeccingShorterButIsIndex0AndWeAreSpeccingLongerThanRemovalDelay = true;
                        }
                    }
                    subConnIsIndex0 = false;
                }

                //Followed by others
                if (anyOtherConnectionBeenSpeccingLonger || anyOtherConnectionBeenSpeccingEquallyLongAndSmallerIndex || anyOtherConnectionBeenSpeccingShorterButIsIndex0AndWeAreSpeccingLongerThanRemovalDelay)
                {
                    // Meaning, we don't really need this connection anymore.
                    if (activeButUnfollowedPlayers.Count == 0 && !isIndex0 && destructionDelayMs < (DateTime.Now - lastFollowedPlayerChanges[myClientNum]).TotalMilliseconds)
                    {
                        // Safe to destroy. Don't destroy in middle of run.
                        if (activePlayers.Count() == 0 || conn.client.playerStateClientNum == conn.ClientNum || (infoPool.playerInfo[currentlySpeccedPlayer].velocity.Length() < 0.0000001f && infoPool.playerInfo[currentlySpeccedPlayer].score.score == 0 && (anyOtherConnectionBeenSpeccingLonger || anyOtherConnectionBeenSpeccingEquallyLongAndSmallerIndex || (DateTime.Now-infoPool.playerInfo[currentlySpeccedPlayer].score.lastScoreValueChanged).TotalSeconds > 10))) {
                            
                            connectionsToDestroyList.Enqueue(conn);
                        }
                    }
                }

                // Is following himself
                if (myClientNum == currentlySpeccedPlayer)
                {
                    if (activeButUnfollowedPlayers.Count == 0 && !isIndex0 && destructionDelayMs < (DateTime.Now - lastFollowedPlayerChanges[myClientNum]).TotalMilliseconds)
                    {
                        // Safe to destroy. Don't destroy in middle of run.
                        connectionsToDestroyList.Enqueue(conn);
                    }
                    else if (isIndex0 && activePlayers.Count() > 0 && activeButUnfollowedPlayers.Count == 0)
                    {
                        // Always have the main connection follow someone because we can never despawn it and if it's following nobody, we can not despawn others either.
                        int randomGuyToFollow = activePlayers[0];
                        conn.leakyBucketRequester.requestExecution("follow " + randomGuyToFollow, RequestCategory.FOLLOW, 5, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    }
                }

                //if (currentlySpectatedPlayers.Contains(currentlySpeccedPlayer))
                if (anyOtherConnectionBeenSpeccingLonger || anyOtherConnectionBeenSpeccingEquallyLongAndSmallerIndex)
                {
                    // This guy is already being spectated, and for longer than by this connection. Let's spectate someone else instead.
                    if(activeButUnfollowedPlayers.Count > 0)
                    {
                        int activePlayerToFollow = activeButUnfollowedPlayers[0];
                        activeButUnfollowedPlayers.RemoveAt(0);
                        conn.leakyBucketRequester.requestExecution("follow " + activePlayerToFollow, RequestCategory.FOLLOW, 5, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    }
                }
                isIndex0 = false;
            }

            // As mentioned above, this is kind of a secondary mechanism to destroy connections to avoid having too many lingering connections around
            //foreach(Connection connToDestroy in connectionsToDestroyList)
            //{
            //    destroyConnection(connToDestroy);
            //}
            while(connectionsToDestroyList.Count() > 0)
            {
                Connection connToDestroy = connectionsToDestroyList.Dequeue();
                destroyConnection(connToDestroy);
            }


            lastActivePlayers.Clear();
            lastActivePlayers.AddRange(activePlayers);
        }



        public override int getRequiredConnectionCount()
        {
            return 1;
        }

        public override string getTypeDisplayName()
        {
            return "OCD";
        }
        public override string[] getAvailableOptions()
        {
            return null;
        }
    }


}
