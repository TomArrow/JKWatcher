using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher.CameraOperators
{
    class CTFCameraOperatorRedBlue : CameraOperator
    {

        CancellationTokenSource cts = new CancellationTokenSource();
        public CTFCameraOperatorRedBlue()
        {
            CancellationToken ct = cts.Token;
            Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        ~CTFCameraOperatorRedBlue()
        {
            cts.Cancel();
            cts.Dispose();
        }

        // first connection [0] follows red flag
        // second connection [1] follows blue flag
        private void Run(CancellationToken ct)
        {
            FlagStatus lastRedFlagStatus = (FlagStatus)(-1);
            FlagStatus lastBlueFlagStatus = (FlagStatus)(-1);
            while (true)
            {
                System.Threading.Thread.Sleep(100);
                ct.ThrowIfCancellationRequested();

                switch (infoPool.redFlag)
                {
                    case FlagStatus.FLAG_ATBASE:
                        handleRedFlagAtBase(infoPool.redFlag == lastRedFlagStatus);
                        break;
                    case FlagStatus.FLAG_TAKEN:
                        break;
                    case FlagStatus.FLAG_DROPPED:
                        break;
                    default:
                        // uh, dunno.
                        break;
                }
                lastRedFlagStatus = infoPool.redFlag;

                switch (infoPool.redFlag)
                {
                    case FlagStatus.FLAG_ATBASE:
                        break;
                    case FlagStatus.FLAG_TAKEN:
                        break;
                    case FlagStatus.FLAG_DROPPED:
                        break;
                    default:
                        // uh, dunno.
                        break;
                }

            }
        }


        List<int> playersCycled = new List<int>();
        enum FlagAtBasePlayerCyclePriority : int
        {
            ANY_VALID_PLAYER,
            SAME_TEAM_PLAYER,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND,
        }
        private void handleRedFlagAtBase(bool flagStatusChanged)
        {
            int currentlySpectatedPlayer = connections[0].client.playerStateClientNum;

            if (flagStatusChanged)
            {
                playersCycled.Clear(); // Reset cycling array if the flag status changed.
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            if(infoPool.lastRedFlagBaseItemPositionUpdate != null)
            {
                flagPosition = infoPool.redFlagBaseItemPosition; // Actual flag item
            } else if (infoPool.lastRedFlagBasePositionUpdate != null)
            {
                flagPosition = infoPool.redFlagBasePosition; // Flag base. the thing it stands n
            }

            if (flagPosition == null)
            {
                // Dunno where flag is I guess. All we can do is cycle through players until flag position is filled.
                // Should only happen at the start of a game I think

                // Prioritize cycling:
                // 1. Players on same team that died in last second (would respawn at base)
                // 2. Players on same team that died in last 2 seconds (would respawn at base)
                // 3. Players on same team
                // 4. Players on enemy team

                // First off, put the player we are currently spectating to the list of already cycled players
                // Clearly he is of no use.
                if (!playersCycled.Contains(currentlySpectatedPlayer)) {
                    playersCycled.Add(currentlySpectatedPlayer);
                }

                int nextPlayerTotry = -1;

                // Find player to try out next
                bool foundAtLeastOneValidPlayer = false;
                FlagAtBasePlayerCyclePriority playerFilter = FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND;
                while(playerFilter >= 0) {

                    // Do this loop with increasingly low demands (playerFilter)
                    for (int i=0;i< infoPool.playerInfo.Length;i++)
                    {
                        // Is valid player?
                        if(infoPool.playerInfo[i].infoValid && (infoPool.playerInfo[i].team == JKClient.Team.Blue || infoPool.playerInfo[i].team == JKClient.Team.Red)) { // Make sure it's a valid active player
                            
                            foundAtLeastOneValidPlayer = true;

                            // Already cycled through? Then ignore.
                            if (!playersCycled.Contains(i)) // skip players we already cycled through.
                            {
                                switch (playerFilter)
                                {
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND:
                                        if(infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 1000)
                                        {
                                            nextPlayerTotry = i;
                                        } 
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS:
                                        if(infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 1000)
                                        {
                                            nextPlayerTotry = i;
                                        } 
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER:
                                        if(infoPool.playerInfo[i].team == JKClient.Team.Red)
                                        {
                                            nextPlayerTotry = i;
                                        }
                                        break;
                                    case FlagAtBasePlayerCyclePriority.ANY_VALID_PLAYER:
                                        nextPlayerTotry = i;
                                        break;
                                }
                            } 
                        }
                    }
                    if (nextPlayerTotry != -1) break;
                    playerFilter--; // Didn't find any? Well let's go again.
                }

                if (nextPlayerTotry == -1)
                {
                    // Guess we're done cycling and found nobody. Let's start from scratch then.
                    playersCycled.Clear();
                } else
                {
                    // Small delay. We want to get through this as quickly as possible. Also discard any scoreboard commands, this is more important.
                    // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                    // Need to give the server a bit of time to react.
                    connections[0].leakyBucketRequester.requestExecution("follow " + nextPlayerTotry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE,new RequestCategory[] { RequestCategory.SCOREBOARD});
                    
                }

            } else
            {
                // Ok we know where the flag is. Now what?

            }
        }

        public override int getRequiredConnectionCount()
        {
            return 2;
        }

        public override string getTypeDisplayName()
        {
            return "CTFRedBlue";
        }
    }

}
