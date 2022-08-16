
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

        public int MaxAllowedServerConnections { get; set; } = 4;

        private Dictionary<int, int> lastFollowedPlayers = new Dictionary<int, int>(); // clientnum (watcher client) -> clientnum (player) dictionary. remember who we were watching last loop. to detect changes.
        private Dictionary<int, DateTime> lastFollowedPlayerChanges = new Dictionary<int, DateTime>(); // clientnum (watcher client) -> DateTime of last change.

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

            if(activePlayers.Count > MaxAllowedServerConnections && !Enumerable.SequenceEqual(activePlayers,lastActivePlayers))
            {
                Console.Beep();
            }

            if (activePlayers.Count > connections.Count && connections.Count < MaxAllowedServerConnections) getMoreConnections(Math.Min(activePlayers.Count- connections.Count, MaxAllowedServerConnections-connections.Count)); // If more than 1 player here, get another connection
            //if(activePlayers.Count() <= 1 && connections.Count() > 1) // If only 1 player here, get rid of extra connections.
            if(activePlayers.Count < connections.Count && connections.Count > 1) // If only 1 player here, get rid of extra connections.
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
                    if (othersFollowingSamePlayer == 0) continue; // We can't destroy this one. It's the only one following this player.

                    if(activePlayers.Count() == 0 || connections[i].client.playerStateClientNum == connections[i].ClientNum || (infoPool.playerInfo[currentlySpeccedPlayer].velocity.Length() < 0.0000001f && infoPool.playerInfo[currentlySpeccedPlayer].score.score == 0))
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

            // Detect changes in who we are following and remember the times a change happened.
            foreach (Connection conn in connections)
            {
                int myClientNum = conn.ClientNum.GetValueOrDefault(-1);
                int currentlySpeccedPlayer = conn.client.playerStateClientNum;

                if (lastFollowedPlayers[myClientNum] != currentlySpeccedPlayer || !lastFollowedPlayerChanges.ContainsKey(myClientNum))
                {
                    lastFollowedPlayerChanges[myClientNum] = DateTime.Now;
                }
                lastFollowedPlayers[myClientNum] = currentlySpeccedPlayer;
            }

            List<int> currentlySpectatedPlayers = new List<int>();
            currentlySpectatedPlayers.AddRange(myOwnConnections); // This isn't strictly semantically logical but it will do the job. Avoid speccing itself.
            foreach (Connection conn in connections)
            {
                int myClientNum = conn.ClientNum.GetValueOrDefault(-1);
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
