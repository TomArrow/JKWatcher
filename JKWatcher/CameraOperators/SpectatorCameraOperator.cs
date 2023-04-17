// Follows a spectator by ping (experimental)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JKClient;
using JKWatcher.GenericDialogBoxes;

namespace JKWatcher.CameraOperators
{
    class SpectatorCameraOperator : CameraOperator
    {
        CancellationTokenSource cts = null;

        private Task backgroundTask = null;

        private int spectatorToFollow = -1;

        public override void Initialize()
        {
            base.Initialize();
            startBackground();
            OpenDialog();
        }

        private void startBackground()
        {
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                HasErrored = true;
                OnErrored(new ErroredEventArgs(t.Exception));
                Task.Run(() => {
                    Thread.Sleep(5000);
                    startBackground(); // Try to recover.
                });
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        bool dialogIsOpen = false;

        public override void OpenDialog()
        {
            if (!dialogIsOpen)
            {
                dialogIsOpen = true;
                TextInput spectatorClientNumInputBox = new TextInput("Which clientNum do you want to follow?", (string clientNum) => {
                    int spectatorToFollowParse = -1;
                    int.TryParse(clientNum, out spectatorToFollowParse);
                    if(spectatorToFollowParse < 32 && spectatorToFollowParse >= 0)
                    {
                        spectatorToFollow = spectatorToFollowParse;
                    }
                    dialogIsOpen = false;
                });
                spectatorClientNumInputBox.Show();
            }
        }
        

        ~SpectatorCameraOperator()
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


        private DateTime? lastTimeKnownSpectated = null;

        private void MainLoopFunction()
        {
            int currentlySpectatedPlayer = connections[0].client.playerStateClientNum;

            if (spectatorToFollow == -1) return;

            if (!infoPool.playerInfo[spectatorToFollow].infoValid) return;

            if (infoPool.playerInfo[spectatorToFollow].team == JKClient.Team.Spectator) // Do the thing
            {

                // Ok try to find out who he is spectating. (who has the same ping)
                int guyToFollow = -1;
                int matches = 0;

                double timeSinceSpectatedPlayerUpdated = infoPool.playerInfo[spectatorToFollow].nwhSpectatedPlayerLastUpdate != null ? (DateTime.Now - infoPool.playerInfo[spectatorToFollow].nwhSpectatedPlayerLastUpdate.Value).TotalMilliseconds : double.PositiveInfinity;
                double timeSincePingUpdated = infoPool.playerInfo[spectatorToFollow].lastScoreUpdated != null ? (DateTime.Now - infoPool.playerInfo[spectatorToFollow].lastScoreUpdated.Value).TotalMilliseconds : double.PositiveInfinity;

                if (timeSinceSpectatedPlayerUpdated < 10000.0) // Nwh specs info gets priority. Because it's more reliable overall.
                {
                    guyToFollow = infoPool.playerInfo[spectatorToFollow].nwhSpectatedPlayer;
                    matches = 1;
                }
                else if (timeSincePingUpdated < 10000.0)
                {
                    for (int i = 0; i < infoPool.playerInfo.Length; i++)
                    {
                        if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].team != Team.Spectator)
                        {
                            if (infoPool.playerInfo[i].score.ping == infoPool.playerInfo[spectatorToFollow].score.ping) // Write it out to have higher chance of temporal coherence? (if one is currently being updated and the other not yet). Not a full solution but worst case is we get the result wrong and it's fixed on the next frame.
                            {
                                matches++;
                                guyToFollow = i;
                            }
                        }
                    }
                } else
                {
                    // Request spectator info from NWH
                    // TODO: Make it detect if this is actually NWH. If not, don't bother.
                    connections[0].leakyBucketRequester.requestExecution("specs", RequestCategory.INFOCOMMANDS, 4, 10000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                }

                if (guyToFollow != -1 && matches == 1) // If there are multiple matches, there is ambiguity which we don't want.
                {
                    lastTimeKnownSpectated = DateTime.Now;
                    if (guyToFollow != currentlySpectatedPlayer) connections[0].leakyBucketRequester.requestExecution("follow " + guyToFollow, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                } else if (matches == 0)
                {
                    double timeSinceKnown = lastTimeKnownSpectated != null ? (DateTime.Now - lastTimeKnownSpectated.Value).TotalMilliseconds : double.PositiveInfinity;

                    if(timeSinceKnown > 10000.0)
                    {
                        // Somehow not working. Maybe giga amount of players on server and actual player ping not available. Or too many matches over and over. Ok. Request specs info from NWH.
                        connections[0].leakyBucketRequester.requestExecution("specs", RequestCategory.INFOCOMMANDS, 4, 10000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }

            } else
            {
                // Just follow this player in actuality.
                if (spectatorToFollow != currentlySpectatedPlayer) connections[0].leakyBucketRequester.requestExecution("follow " + spectatorToFollow, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

            }


        }



        public override int getRequiredConnectionCount()
        {
            return 1;
        }

        public override string getTypeDisplayName()
        {
            return "Spec";
        }
    }
}
