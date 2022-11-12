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
        CancellationTokenSource cts = new CancellationTokenSource();

        private Task backgroundTask = null;

        private int spectatorToFollow = -1;

        public override void Initialize()
        {
            base.Initialize();
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                HasErrored = true;
                OnErrored(new ErroredEventArgs(t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);

            OpenDialog();
        }

        bool dialogIsOpen = false;

        public override void OpenDialog()
        {
            if (!dialogIsOpen)
            {
                dialogIsOpen = true;
                TextInput spectatorClientNumInputBox = new TextInput("Which clientNum do you want to follow?", (string clientNum) => {
                    int.TryParse(clientNum, out spectatorToFollow);
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

                if (guyToFollow != -1 && matches == 1) // If there are multiple matches, there is ambiguity which we don't want.
                {
                    if (guyToFollow != currentlySpectatedPlayer) connections[0].leakyBucketRequester.requestExecution("follow " + guyToFollow, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

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
