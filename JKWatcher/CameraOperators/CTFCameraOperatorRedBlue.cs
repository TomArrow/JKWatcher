using System;
using System.Collections.Generic;
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
            FlagStatus[] lastFlagStatus = new FlagStatus[] { (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1) };
            Team[] teams = new Team[] { Team.Red, Team.Blue };
            while (true)
            {
                System.Threading.Thread.Sleep(100);
                ct.ThrowIfCancellationRequested();

                foreach(Team team in teams)
                {
                    switch (infoPool.teamInfo[(int)team].flag)
                    {
                        case FlagStatus.FLAG_ATBASE:
                            handleFlagAtBase(team, infoPool.teamInfo[(int)team].flag == lastFlagStatus[(int)team]);
                            break;
                        case FlagStatus.FLAG_TAKEN:
                            break;
                        case FlagStatus.FLAG_DROPPED:
                            break;
                        default:
                            // uh, dunno.
                            break;
                    }
                    lastFlagStatus[(int)team] = infoPool.teamInfo[(int)team].flag;
                }
                


            }
        }


        HashSet<int>[] playersCycled = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        enum FlagAtBasePlayerCyclePriority : int
        {
            ANY_VALID_PLAYER,
            SAME_TEAM_PLAYER,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND,
        }
        Dictionary<int, float>[] lastGradings = new Dictionary<int, float>[] { new Dictionary<int, float>(), new Dictionary<int, float>(), new Dictionary<int, float>(), new Dictionary<int, float>() };
        private void handleFlagAtBase(Team flagTeam,bool flagStatusChanged)
        {
            int currentlySpectatedPlayer = connections[0].client.playerStateClientNum;
            int teamInt = (int)flagTeam;

            Connection connection;
            switch (flagTeam)
            {
                case Team.Red:
                    connection = connections[0];
                    break;
                case Team.Blue:
                    connection = connections[1];
                    break;
                default:
                    return; // Only red blue team support atm
            }

            if (flagStatusChanged)
            {
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            int flagItemNumber = -1;
            if(infoPool.teamInfo[teamInt].lastFlagBaseItemPositionUpdate != null)
            {
                flagPosition = infoPool.teamInfo[teamInt].flagBaseItemPosition; // Actual flag item
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseItemEntityNumber;
            } else if (infoPool.teamInfo[teamInt].lastFlagBasePositionUpdate != null)
            {
                flagPosition = infoPool.teamInfo[teamInt].flagBasePosition; // Flag base. the thing it stands n
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseEntityNumber;
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

                // The reason this code is arguably simpler than if the flag position IS known
                // is that we can't make a good judgment of how good a player spectating choice is
                // if we don't even know where the flag is to begin with.

                // First off, put the player we are currently spectating to the list of already cycled players
                // Clearly he is of no use.
                playersCycled[teamInt].Add(currentlySpectatedPlayer); 

                int nextPlayerTotry = -1;

                // Find player to try out next
                bool foundAtLeastOneValidPlayer = false;
                FlagAtBasePlayerCyclePriority playerFilter = FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND;
                while(playerFilter >= 0) {

                    // Do this loop with increasingly low demands (playerFilter)
                    for (int i=0;i< infoPool.playerInfo.Length;i++)
                    {
                        // Is valid player?
                        if(infoPool.playerInfo[i].infoValid && (infoPool.playerInfo[i].team == Team.Blue || infoPool.playerInfo[i].team == Team.Red)) { // Make sure it's a valid active player
                            
                            foundAtLeastOneValidPlayer = true;

                            // Already cycled through? Then ignore.
                            if (!playersCycled[teamInt].Contains(i)) // skip players we already cycled through.
                            {
                                switch (playerFilter)
                                {
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND:
                                        if(infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 1000)
                                        {
                                            nextPlayerTotry = i;
                                        } 
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS:
                                        if(infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 2000)
                                        {
                                            nextPlayerTotry = i;
                                        } 
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER:
                                        if(infoPool.playerInfo[i].team == flagTeam)
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
                        if (nextPlayerTotry != -1) break;
                    }
                    if (nextPlayerTotry != -1) break;
                    playerFilter--; // Didn't find any? Well let's go again.
                }

                if (nextPlayerTotry == -1)
                {
                    // Guess we're done cycling and found nobody. Let's start from scratch then.
                    playersCycled[teamInt].Clear();
                } else
                {
                    // Small delay. We want to get through this as quickly as possible. Also discard any scoreboard commands, this is more important.
                    // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                    // Need to give the server a bit of time to react.
                    connection.leakyBucketRequester.requestExecution("follow " + nextPlayerTotry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE,new RequestCategory[] { RequestCategory.SCOREBOARD});
                    
                }

            } 
            else
            {
                //
                // Ok we know where the flag is. Now what?
                //
                //

                // First off, is the flag visible RIGHT now?
                bool flagVisible = connection.client.Entities[flagItemNumber].CurrentValid;
                if (flagVisible)
                {
                    // Basically we don't HAVE to do anything in principle.
                    // But:
                    // 1. If the player we are spectating right now is dead, he might respawn in a different place, so kinda unsafe.
                    // 2. Even if the player we are spectating is in visible range of the flag, he might be moving away
                    // 3. Even if the player we are spectating is not moving away, we don't know if he actually sees the flag,
                    //      since we do not have the level geometry.
                    // 4. Some servers might be sending the entity either way even if it's all the way across the map. We want to
                    //      actually see it though.
                    //
                    // So in a nutshell, we just do the same thing, whether the flag is visible or not. But if it's visible, we will be 
                    // a bit more tolerant in switching. We might then not switch unless a player is significantly closer to the flag.
                    //
                    //

                    // But, since we're not currently doing any cycling through players, may as well clear this array.
                    playersCycled[teamInt].Clear();
                    lastGradings[teamInt].Clear();
                }

                // My approach: Add velocity to position. That way we will know roughly where a player will be 1 second in the future
                // Measure distance from that predicted place to the flag position.

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision(), tmp;
                for (int i= 0; i < infoPool.playerInfo.Length; i++){
                    if(infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds,
                            distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity - flagPosition.Value).Length(),
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath
                        };
                        tmp.gradeForFlagAtBase(flagVisible);
                        if(lastGradings[teamInt].ContainsKey(i) && playersCycled[teamInt].Contains(i) && tmp.grade*3f < lastGradings[teamInt][i])
                        {
                            // Grade has significantly improved since last time we graded this player, so let him be "re-cycled" lol
                            // It has to have improved by a factor of 3, which roughly means 1000 units of distance closer for example
                            playersCycled[teamInt].Remove(i);
                        }
                        possibleNextPlayers.Add(tmp);
                        if (i == currentlySpectatedPlayer) stayWithCurrentPlayerDecision = tmp;
                    }
                }
                if(possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a,b)=> {
                    return (int)Math.Clamp(a.grade-b.grade,Int32.MinValue,Int32.MaxValue);
                });

                if (flagVisible)
                {
                    if (currentlySpectatedPlayer == possibleNextPlayers[0].clientNum)
                    {
                        return; // Flag is visible and we already have the best player choice
                    }
                    // As said above, if the flag is visible, we need a greater persuasion to switch to a better player
                    float currentPlayerGrade = stayWithCurrentPlayerDecision.grade;
                    float bestOtherOption = possibleNextPlayers[0].grade;

                    if (currentPlayerGrade / 1.73f > bestOtherOption)
                    {
                        // Ok the alternative is more than twice as good
                        // The number is a bit arbitrary and might need tuning. 
                        // A factor of 3 is roughly 1000 units of distance, 2 seconds of outdated info
                        // 1.73 is roughly 500 units of distance. So only switch to other player if he's 500 units closer.

                        // Also with this type of switch (since it's not so urgent), allow 1 second delay from last switch
                        // so stuff doesn't get too frantic. 
                        connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    }
                }
                else
                {
                    // Flag is NOT visible. What this means is we need to find a player who CAN see the flag asap.
                    // This is more similar to the approach we take when we don't even know where the flag is at all
                    playersCycled[teamInt].Add(currentlySpectatedPlayer);

                    // TODO Add recent death mechanism. If best option isn't good enough, just pick recently deceased persons

                    int nextPlayerToTry = -1;
                    foreach(PossiblePlayerDecision player in possibleNextPlayers)
                    {
                        if (!playersCycled[teamInt].Contains(player.clientNum)) // already cycled throug those
                        {
                            nextPlayerToTry = player.clientNum;
                            break; // Since the array is sorted with best players first, break as soon as we have a candidate
                        }
                    }

                    if(nextPlayerToTry == -1)
                    {
                        // Hmm no players left to cycle through I guess. Not much we can do then.
                        playersCycled[teamInt].Clear();
                        lastGradings[teamInt].Clear();
                    } else
                    {
                        // Small delay. We want to get through this as quickly as possible. Also discard any scoreboard commands, this is more important.
                        // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                        // Need to give the server a bit of time to react.
                        connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                    }

                    // Todo: problem with this algorithm is that someone added to the list of "already cycled through" may become a 
                    // good choice again rather quickly and then we might ignore him just because he's on that list.
                    // Maybe we should use some kind of list that expires after a while. Or remove clientnums from it if they die etc.
                    // Or simply maybe: If the person's alive status changed, remove him from the list? idk. Let's leave it like this for now.
                    // Or: If the person's grade has improved by more than a certain factor, remove from list.
                }


            }
        }

        

        struct PossiblePlayerDecision
        {
            public int clientNum;
            public float informationAge;
            public float distance;
            public bool isAlive;
            public bool isOnSameTeamAsFlag;
            public int lastDeath;
            public float lastDeathDistance;

            public float grade { get; private set; }

            // Bigger value: worse decision
            public float gradeForFlagAtBase(bool flagInvisibleDeathBonus = false)
            {
                float grade = 1;
                if (flagInvisibleDeathBonus) { 
                    if(this.lastDeath < 4000) 
                    {
                        // Flag is not visible so we will take a chance with players who died recently
                        // They might be close or they might not.
                        // I figure the sweet spot is around 1-2 seconds.
                        // Too early and player may not have respawned yet.
                        // Too late and he might already be gone to elsewhere.
                        grade *= (float)Math.Pow(3, Math.Abs(this.lastDeath-1500) / 1000);

                        // We might also take into account where he died, however the flag is now in base,
                        // so even a chase ret will likely return home to the flag.
                        // Camp ret might however go out to camp. 
                        // However, a chase ret is still very likely to go back to the flag,
                        // so treating his distance at time of death as a negative is a bad idea.
                        // We'll take our chances.

                        // We might also try and track who retted how many times to guess roles by numbers.

                        return grade;
                    }
                }

                grade *= (float)Math.Pow(3, this.informationAge / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than cappers.
                this.grade = grade;
                return grade;
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
