
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
    class OCDCameraOperator : CameraOperator
    {

        CancellationTokenSource cts = new CancellationTokenSource();

        private Task backgroundTask = null;


        public override void Initialize()
        {
            base.Initialize();
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                HasErrored = true;
                OnErrored(new ErroredEventArgs(t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);
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
                ct.ThrowIfCancellationRequested();

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

        private void MainLoopFunction()
        {
            // First, let's figure out the current count of players.
            List<int> activePlayers = new List<int>();
            List<int> myOwnConnections = new List<int>();
            foreach(Connection conn in connections)
            {
                myOwnConnections.Add(conn.client.clientNum);
            }
            int maxClients = connections[0].client.ClientHandler != null ? connections[0].client.ClientHandler.MaxClients : 32;
            for (int i=0;i< maxClients; i++)
            {
                if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].team != Team.Spectator && !myOwnConnections.Contains(i)) activePlayers.Add(i); // Don't count myself. Don't count spectators.
            }

            if(activePlayers.Count() > 2 && !Enumerable.SequenceEqual(activePlayers,lastActivePlayers))
            {
                Console.Beep();
            }

            if (activePlayers.Count() > 1 && connections.Count() < 2) getMoreConnections(1); // If more than 1 player here, get another connection
            if(activePlayers.Count() <= 1 && connections.Count() > 1) // If only 1 player here, get rid of extra connections.
            {
                for(int i = connections.Count-1; i > 0; i--)
                {
                    destroyConnection(connections[i]); 
                }
            }

            List<int> currentlySpectatedPlayers = new List<int>();
            currentlySpectatedPlayers.AddRange(myOwnConnections); // This isn't strictly semantically logical but it will do the job. Avoid speccing itself.
            foreach (Connection conn in connections)
            {
                int currentlySpeccedPlayer = conn.client.playerStateClientNum;
                if (currentlySpectatedPlayers.Contains(currentlySpeccedPlayer))
                {
                    // This guy is already being spectated. Let's spectate someone else instead.
                    int activePlayerToFollow = -1;
                    foreach(int playerNum in activePlayers)
                    {
                        if (!currentlySpectatedPlayers.Contains(playerNum))
                        {
                            activePlayerToFollow = playerNum;
                            break;
                        }
                    }
                    if(activePlayerToFollow != -1)
                    {
                        conn.leakyBucketRequester.requestExecution("follow " + activePlayerToFollow, RequestCategory.FOLLOW, 5, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        currentlySpectatedPlayers.Add(activePlayerToFollow);
                    }
                } else
                {
                    currentlySpectatedPlayers.Add(currentlySpeccedPlayer);
                }
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
    }


}
