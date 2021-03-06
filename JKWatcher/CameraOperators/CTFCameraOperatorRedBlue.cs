
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
    class CTFCameraOperatorRedBlue : CameraOperator
    {

        CancellationTokenSource cts = new CancellationTokenSource();

        public override void Initialize()
        {
            base.Initialize();
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

                foreach (Team team in teams)
                {
                    switch (infoPool.teamInfo[(int)team].flag)
                    {
                        case FlagStatus.FLAG_ATBASE:
                            handleFlagAtBase(team, infoPool.teamInfo[(int)team].flag == lastFlagStatus[(int)team]);
                            break;
                        case FlagStatus.FLAG_TAKEN:
                            handleFlagTaken(team, infoPool.teamInfo[(int)team].flag == lastFlagStatus[(int)team]);
                            break;
                        case FlagStatus.FLAG_DROPPED:
                            handleFlagDropped(team, infoPool.teamInfo[(int)team].flag == lastFlagStatus[(int)team]);
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
        private void handleFlagAtBase(Team flagTeam, bool flagStatusChanged)
        {
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

            int currentlySpectatedPlayer = connection.client.playerStateClientNum;
            int teamInt = (int)flagTeam;

            if (flagStatusChanged)
            {
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            int flagItemNumber = -1;
            if (infoPool.teamInfo[teamInt].lastFlagBaseItemPositionUpdate != null)
            {
                flagPosition = infoPool.teamInfo[teamInt].flagBaseItemPosition; // Actual flag item
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseItemEntityNumber == 0 ? -1: infoPool.teamInfo[teamInt].flagBaseItemEntityNumber;
            } else if (infoPool.teamInfo[teamInt].lastFlagBasePositionUpdate != null)
            {
                flagPosition = infoPool.teamInfo[teamInt].flagBasePosition; // Flag base. the thing it stands n
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseEntityNumber == 0 ? -1 : infoPool.teamInfo[teamInt].flagBaseEntityNumber;
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
                while (playerFilter >= 0) {

                    // Do this loop with increasingly low demands (playerFilter)
                    for (int i = 0; i < infoPool.playerInfo.Length; i++)
                    {
                        // Is valid player?
                        if (infoPool.playerInfo[i].infoValid && (infoPool.playerInfo[i].team == Team.Blue || infoPool.playerInfo[i].team == Team.Red)) { // Make sure it's a valid active player

                            foundAtLeastOneValidPlayer = true;

                            // Already cycled through? Then ignore.
                            if (!playersCycled[teamInt].Contains(i)) // skip players we already cycled through.
                            {
                                switch (playerFilter)
                                {
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND:
                                        if (infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 1000)
                                        {
                                            nextPlayerTotry = i;
                                        }
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS:
                                        if (infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].lastDeath != null && (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds < 2000)
                                        {
                                            nextPlayerTotry = i;
                                        }
                                        break;
                                    case FlagAtBasePlayerCyclePriority.SAME_TEAM_PLAYER:
                                        if (infoPool.playerInfo[i].team == flagTeam)
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
                    connection.leakyBucketRequester.requestExecution("follow " + nextPlayerTotry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                }

            }
            else
            {
                //
                // Ok we know where the flag is. Now what?
                //
                //

                // First off, is the flag visible RIGHT now?
                bool flagVisible = flagItemNumber==-1 ? false: connection.client.Entities[flagItemNumber].CurrentValid;
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
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() { grade=float.PositiveInfinity}, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++) {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
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
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                        };
                        tmp.gradeForFlagAtBase(!flagVisible);
                        if (lastGradings[teamInt].ContainsKey(i) && playersCycled[teamInt].Contains(i) && tmp.grade * 3f < lastGradings[teamInt][i])
                        {
                            // Grade has significantly improved since last time we graded this player, so let him be "re-cycled" lol
                            // It has to have improved by a factor of 3, which roughly means 1000 units of distance closer for example
                            playersCycled[teamInt].Remove(i);
                        }
                        lastGradings[teamInt][i] = tmp.grade;
                        possibleNextPlayers.Add(tmp);
                        if (i == currentlySpectatedPlayer) stayWithCurrentPlayerDecision = tmp;
                    }
                }
                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue);
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
                        connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }
                else
                {
                    // Flag is NOT visible. What this means is we need to find a player who CAN see the flag asap.
                    // This is more similar to the approach we take when we don't even know where the flag is at all
                    playersCycled[teamInt].Add(currentlySpectatedPlayer);

                    // TODO Add recent death mechanism. If best option isn't good enough, just pick recently deceased persons

                    int nextPlayerToTry = -1;
                    foreach (PossiblePlayerDecision player in possibleNextPlayers)
                    {
                        if (!playersCycled[teamInt].Contains(player.clientNum)) // already cycled throug those
                        {
                            nextPlayerToTry = player.clientNum;
                            break; // Since the array is sorted with best players first, break as soon as we have a candidate
                        }
                    }

                    if (nextPlayerToTry == -1)
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

        private void handleFlagTaken(Team flagTeam, bool flagStatusChanged)
        {
            Connection connection;
            Team opposingTeam = Team.Free;
            switch (flagTeam)
            {
                case Team.Red:
                    connection = connections[0];
                    opposingTeam = Team.Blue;
                    break;
                case Team.Blue:
                    connection = connections[1];
                    opposingTeam = Team.Red;
                    break;
                default:
                    return; // Only red blue team support atm
            }
            int opposingTeamInt = (int)opposingTeam;

            int currentlySpectatedPlayer = connection.client.playerStateClientNum;
            int teamInt = (int)flagTeam;


            if (flagStatusChanged)
            {
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Find out who has it
            if (infoPool.teamInfo[(int)flagTeam].lastFlagCarrierUpdate == null)
            {
                // Uhm idk, that's weird. We know the flag's taken but we don't know who has it
                // Maybe if the tool didn't witness the taking of the flag and didn't have time to receive the info from somewhere.

                // We could probably just sit this one out but let's cycle through players anyway?
                // Might be faster in some cases but might also be slower in others due to more commands having been sent?
                playersCycled[teamInt].Add(currentlySpectatedPlayer);

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                        };
                        tmp.gradeForFlagTakenUnknownPlayer();
                        if (lastGradings[teamInt].ContainsKey(i) && playersCycled[teamInt].Contains(i) && tmp.grade * 3f < lastGradings[teamInt][i])
                        {
                            // Grade has significantly improved since last time we graded this player, so let him be "re-cycled" lol
                            // It has to have improved by a factor of 3, which roughly means 1000 units of distance closer for example
                            playersCycled[teamInt].Remove(i);
                        }
                        lastGradings[teamInt][i] = tmp.grade;
                        possibleNextPlayers.Add(tmp);
                    }
                }

                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue);
                });

                // Flag carrier is not visible otherwise we would likely know who has the flag.
                playersCycled[teamInt].Add(currentlySpectatedPlayer);

                int nextPlayerToTry = -1;
                foreach (PossiblePlayerDecision player in possibleNextPlayers)
                {
                    if (!playersCycled[teamInt].Contains(player.clientNum)) // already cycled throug those
                    {
                        nextPlayerToTry = player.clientNum;
                        break; // Since the array is sorted with best players first, break as soon as we have a candidate
                    }
                }

                if (nextPlayerToTry == -1)
                {
                    // Hmm no players left to cycle through I guess. Not much we can do then.
                    playersCycled[teamInt].Clear();
                    lastGradings[teamInt].Clear();
                }
                else
                {
                    // Small delay. We want to get through this as quickly as possible. Don't discard scoreboard requests, as they have info about flag carrier.
                    // In fact, request one right away.
                    // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                    // Need to give the server a bit of time to react.
                    connection.leakyBucketRequester.requestExecution("score", RequestCategory.SCOREBOARD, 3, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);

                }

                return;
            }

            // Okay we know who the flag carrier is now
            // Do we know WHERE is?
            int flagCarrier = infoPool.teamInfo[(int)flagTeam].lastFlagCarrier;

            bool flagCarrierVisible = connection.client.Entities[flagCarrier].currentValidOrFilledFromPlayerState();
            bool currentlyFollowingFlagCarrier = flagCarrier == currentlySpectatedPlayer;

            if (!flagCarrierVisible)
            {
                // Okay now get this... we know who the flag carrier is...
                // How do we find out where he is?
                // Simple, we follow him.
                // However ... maybe he only just disappeared behind a corner but we actually know someone who's closer to him
                // So we could give that a try.
                // In short: If he disappeared recently (up to half second?) ago, just see if we know of any player who we know is closer
                // to him. If not, just switch to the carrier himself.
                if(infoPool.playerInfo[flagCarrier].lastFullPositionUpdate == null)
                {
                    // We don't even know where the flag carrier is or was. Just follow him.
                    connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                }
                else
                {
                    float lastSeen = (float)(DateTime.Now - infoPool.playerInfo[flagCarrier].lastFullPositionUpdate.Value).TotalMilliseconds / 1000.0f;
                    if(lastSeen < 1f)
                    {
                        // In theory we could go more crazy here with the decision making process to find best match
                        // But highest priority should be to get the flag carrier visible again
                        // We could also cycle here but again, we can simply switch to the carrier himself in worst case
                        Vector3 flagCarrierPredictedPosition = infoPool.playerInfo[flagCarrier].position + lastSeen * infoPool.playerInfo[flagCarrier].velocity;
                        Vector3 flagCarrierPositionInOneSecond = flagCarrierPredictedPosition+ infoPool.playerInfo[flagCarrier].velocity;

                        Vector3 currentSpectatedPlayerPositionInOneSecond = infoPool.playerInfo[currentlySpectatedPlayer].position + infoPool.playerInfo[currentlySpectatedPlayer].velocity;
                        float currentDistance = (currentSpectatedPlayerPositionInOneSecond - flagCarrierPositionInOneSecond).Length();

                        float closestDistance = currentDistance;
                        int closestPlayer = -1;
                        for (int i = 0; i < infoPool.playerInfo.Length; i++)
                        {
                            if (infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].IsAlive && infoPool.playerInfo[i].team != Team.Spectator)
                            {
                                float lastSeenThisPlayer = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds / 1000.0f;
                                if(lastSeenThisPlayer < 1f)
                                {
                                    Vector3 playerPositionInOneSecond = infoPool.playerInfo[flagCarrier].position + (1f+lastSeen) * infoPool.playerInfo[flagCarrier].velocity;
                                    float playerDistance = (playerPositionInOneSecond - flagCarrierPositionInOneSecond).Length();
                                    if(playerDistance < currentDistance)
                                    {
                                        closestDistance = playerDistance;
                                        closestPlayer = i;
                                    }
                                }
                            }
                        }

                        if(closestPlayer == -1)
                        {
                            // Ok we found nobody closer. Follow carrier himself.
                            connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        }
                        else
                        {
                            // give this guy a chance.
                            connection.leakyBucketRequester.requestExecution("follow " + closestPlayer, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        }

                        // We don't do any cycling here because once you switch to the closer player you won't accidentally switch to him AGAIN

                    } else
                    {
                        // Don't really have any up to date information on anyone who's closer so just follow the carrier
                        connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                        // Todo: Maybe also consider that distance may not be a perfect measure, since someone could be through a wall and technically closer but a further player may actually see the flag carrier
                    }
                    

                }
                


            } else
            {
                // Flag carrier IS visible.
                playersCycled[teamInt].Clear(); 
                lastGradings[teamInt].Clear();

                Vector3 flagCarrierPositionInOneSecond = infoPool.playerInfo[flagCarrier].position + infoPool.playerInfo[flagCarrier].velocity;
                
                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() { grade = float.PositiveInfinity }, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds,
                            distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity - flagCarrierPositionInOneSecond).Length(),
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            isVisible = connection.client.Entities[i].currentValidOrFilledFromPlayerState(),
                            isCarryingTheFlag = i == flagCarrier,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                            isCarryingTheOtherTeamsFlag = (infoPool.teamInfo[opposingTeamInt].flag == FlagStatus.FLAG_TAKEN && infoPool.teamInfo[opposingTeamInt].lastFlagCarrierUpdate != null) ? infoPool.teamInfo[opposingTeamInt].lastFlagCarrier == i : false
                        };
                        tmp.gradeForFlagTakenAndVisible(currentlyFollowingFlagCarrier);
                        possibleNextPlayers.Add(tmp);
                        if (i == currentlySpectatedPlayer) stayWithCurrentPlayerDecision = tmp;
                    }
                }
                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue);
                });

                if (currentlySpectatedPlayer == possibleNextPlayers[0].clientNum)
                {
                    return; // Flag is visible and we already have the best player choice
                }

                if (currentlyFollowingFlagCarrier || infoPool.playerInfo[currentlySpectatedPlayer].team != flagTeam) // This is unfortunate and any improvement is good.
                {
                    // Flag is visible but we're following the capper or the capper's team. Boring.

                    // Switch with decent speed but not too hectically (well, up for debate I guess. We'll see)
                    connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 5, 600, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
    
                } else
                {
                    // If the flag is visible and we're not speccing the capper, we need a greater persuasion to switch to a better player
                    // We don't want to too hectically jump around all the time
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
                        connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }
                  
            }

        }

        private void handleFlagDropped(Team flagTeam, bool flagStatusChanged)
        {
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

            int currentlySpectatedPlayer = connection.client.playerStateClientNum;
            int teamInt = (int)flagTeam;

            if (flagStatusChanged)
            {
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            int flagItemNumber = -1;
            if (infoPool.teamInfo[teamInt].lastFlagDroppedPositionUpdate != null)
            {
                flagPosition = infoPool.teamInfo[teamInt].flagDroppedPosition; // Actual flag item

                // TODO Be very careful here. We may know the position (from the drop notification) but not the item number.
                // That actually will need some kind of fix I think, very unelegant
                flagItemNumber = infoPool.teamInfo[teamInt].droppedFlagEntityNumber == 0 ? -1 : infoPool.teamInfo[teamInt].droppedFlagEntityNumber;
            }

            if (flagPosition == null)
            {
                // Don't really know where it is!
                // Was it a long time since it was in base?
                float timeSinceAtBase = float.PositiveInfinity;
                if (infoPool.teamInfo[teamInt].lastTimeFlagWasSeenAtBase != null)
                {
                    // Let the grading take into account how long it was since the map was in base
                    // As then players who recently died are more likely to be near it
                    timeSinceAtBase = (float)(DateTime.Now - infoPool.teamInfo[teamInt].lastTimeFlagWasSeenAtBase.Value).TotalMilliseconds;
                }


                // Just cycle through players I guess!
                // Give preference to players of same team as flag, as it's likely (but not guaranteed) that 
                // the flag was dropped due to a ret by an enemy
                playersCycled[teamInt].Add(currentlySpectatedPlayer);

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount // Will only be filled on nwh but won't hurt anything otherwise. TODO : Do our own counting?
                        };
                        tmp.gradeForFlagDroppedUnknownPosition(timeSinceAtBase);
                        if (lastGradings[teamInt].ContainsKey(i) && playersCycled[teamInt].Contains(i) && tmp.grade * 3f < lastGradings[teamInt][i])
                        {
                            // Grade has significantly improved since last time we graded this player, so let him be "re-cycled" lol
                            // It has to have improved by a factor of 3, which roughly means 1000 units of distance closer for example
                            playersCycled[teamInt].Remove(i);
                        }
                        lastGradings[teamInt][i] = tmp.grade;
                        possibleNextPlayers.Add(tmp);
                    }
                }

                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue);
                });

                // Flag is not visible otherwise we would likely know where it is.
                playersCycled[teamInt].Add(currentlySpectatedPlayer);

                int nextPlayerToTry = -1;
                foreach (PossiblePlayerDecision player in possibleNextPlayers)
                {
                    if (!playersCycled[teamInt].Contains(player.clientNum)) // already cycled throug those
                    {
                        nextPlayerToTry = player.clientNum;
                        break; // Since the array is sorted with best players first, break as soon as we have a candidate
                    }
                }

                if (nextPlayerToTry == -1)
                {
                    // Hmm no players left to cycle through I guess. Not much we can do then.
                    playersCycled[teamInt].Clear();
                    lastGradings[teamInt].Clear();
                }
                else
                {
                    // Small delay. We want to get through this as quickly as possible.
                    // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                    // Need to give the server a bit of time to react.
                    connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                }

                return;
            }
            else
            {
                //
                // Ok we know where the flag is. Now what?
                //
                //

                // First off, is the flag visible RIGHT now?
                bool flagVisible = flagItemNumber == -1 ? false: connection.client.Entities[flagItemNumber].CurrentValid; // If we don't know its item number it can't be visible either duh!
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

                float flagDistanceFromBase = float.PositiveInfinity;
                Vector3 ? flagBasePosition = null;
                if (infoPool.teamInfo[teamInt].lastFlagBaseItemPositionUpdate != null)
                {
                    flagDistanceFromBase = (infoPool.teamInfo[teamInt].flagBaseItemPosition - flagPosition.Value).Length(); // Actual flag item
                }
                else if (infoPool.teamInfo[teamInt].lastFlagBasePositionUpdate != null)
                {
                    flagDistanceFromBase = (infoPool.teamInfo[teamInt].flagBasePosition - flagPosition.Value).Length();// Flag base. the thing it stands n
                }

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() { grade = float.PositiveInfinity }, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
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
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount
                        };
                        tmp.gradeForFlagDroppedWithKnownPosition(flagVisible,flagDistanceFromBase);
                        if (lastGradings[teamInt].ContainsKey(i) && playersCycled[teamInt].Contains(i) && tmp.grade * 3f < lastGradings[teamInt][i])
                        {
                            // Grade has significantly improved since last time we graded this player, so let him be "re-cycled" lol
                            // It has to have improved by a factor of 3, which roughly means 1000 units of distance closer for example
                            playersCycled[teamInt].Remove(i);
                        }
                        lastGradings[teamInt][i] = tmp.grade;
                        possibleNextPlayers.Add(tmp);
                        if (i == currentlySpectatedPlayer) stayWithCurrentPlayerDecision = tmp;
                    }
                }
                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue);
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
                        connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }
                else
                {
                    // Flag is NOT visible. What this means is we need to find a player who CAN see the flag asap.
                    // This is more similar to the approach we take when we don't even know where the flag is at all
                    playersCycled[teamInt].Add(currentlySpectatedPlayer);

                    // TODO Add recent death mechanism. If best option isn't good enough, just pick recently deceased persons

                    int nextPlayerToTry = -1;
                    foreach (PossiblePlayerDecision player in possibleNextPlayers)
                    {
                        if (!playersCycled[teamInt].Contains(player.clientNum)) // already cycled throug those
                        {
                            nextPlayerToTry = player.clientNum;
                            break; // Since the array is sorted with best players first, break as soon as we have a candidate
                        }
                    }

                    if (nextPlayerToTry == -1)
                    {
                        // Hmm no players left to cycle through I guess. Not much we can do then.
                        playersCycled[teamInt].Clear();
                        lastGradings[teamInt].Clear();
                    }
                    else
                    {
                        // Small delay. We want to get through this as quickly as possible. Also discard any scoreboard commands, this is more important.
                        // But don't use zero delay or we will be re-requesting the same player a lot of times, which is of no benefit
                        // Need to give the server a bit of time to react.
                        connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });

                    }

                }


            }
        }


        public struct PossiblePlayerDecision
        {
            // Important to do: Cap out some of these grading parameters
            // For example information age should not really get much worse beyond a certain point.
            // Just because we haven't heard from someone in 20 seconds doesn't make him an exponentially worse choice 
            // than someone we haven't heard from in 10 seconds
            // Todo maybe rething this whole system and instead make the algorithm guess the estimated distance?
            public int clientNum;
            public float informationAge;
            public float distance;
            public bool isAlive;
            public bool isOnSameTeamAsFlag;
            public int lastDeath;
            public int retCount;
            public float lastDeathDistance;
            public bool isCarryingTheOtherTeamsFlag;
            public bool isCarryingTheFlag;
            public bool isVisible;

            public float grade { get; set; }
            public string gradeMethod { get; set; }

            public float gradeForFlagTakenUnknownPlayer()
            {
                gradeMethod = "gradeForFlagTakenUnknownPlayer()";

                grade = 1;

                if (isOnSameTeamAsFlag) { 
                    // Retter likely to be near capper/flag
                    grade /= this.retCount > 0 ? (float)this.retCount : 1f;
                }

                grade *= isOnSameTeamAsFlag ? 4f : 1f; // We want to find the flag carrier (he is unknown). Can only be an opposite team player
                grade *= isAlive ? 4f : 1f; // Dead player can't carry a flag!
                float lastDeath = this.lastDeath;
                if(lastDeath > 10000)
                {
                    lastDeath = 10000;
                }
                // If a player died recently, he's unlikely to have the flag unless he got it from a dead teammate. (since it takes time to get to enemy base)
                // For other team players: We don't know when the flag was taken so players alive for longer are more likely to be near the flag, but not to the same degree as with carrier.
                // Maybe for future include info about when the flag was taken.
                // But if we don't know who has it, it's unlikely we know when it was taken anyway.
                grade *= (float)Math.Pow(isOnSameTeamAsFlag? 1.2f: 1.35f, 10000-lastDeath / 1000);

#if DEBUG && LOGDECISIONS
                DecisionsLogger.logDecisionGrading(this);
#endif
                return grade;
            }
            
            public float gradeForFlagDroppedUnknownPosition(float timeSinceInBase)
            {
                gradeMethod = "gradeForFlagDroppedUnknownPosition(" + timeSinceInBase.ToString() + ")";

                grade = 1;
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than supporters.
                
                // Depending on whether it's been a long time since the flag was in the base or not,
                // it might be better to watch the flag's team or the other team
                if (this.isOnSameTeamAsFlag)
                {
                    // Retter likely to be near capper/flag
                    grade /= this.retCount > 0 ? (float)this.retCount : 1f;

                    //float effectOfDeathTime = (float)Math.Pow(2.0, 4f - timeSinceInBase / 1000.0f);
                    float recentDeathAdvantageFactor = (float)Math.Pow(1.5, Math.Abs(this.lastDeath - 1500) / 1000); // This will count the more recently flag was seen at base
                    float recentDeathDisadvantageFactor = (float)Math.Pow(1.3, 10000 - Math.Min(this.lastDeath, 10000) / 1000); // This will count the longer it's been
                    
                    if(timeSinceInBase <= 3000f)
                    {
                        grade *= recentDeathAdvantageFactor;
                    } else if(timeSinceInBase >3000f && timeSinceInBase <= 6000f) // Blend between recent death advantage and 1
                    {
                        float ratio = (timeSinceInBase - 3000f) / 3000f;
                        grade *= (1 - ratio) * recentDeathAdvantageFactor + ratio*1;
                    } else if(timeSinceInBase >6000f && timeSinceInBase <= 9000f) // blend between 1 and recent death disadvantage
                    {
                        float ratio = (timeSinceInBase - 6000f) / 3000f;
                        grade *= ratio * recentDeathDisadvantageFactor + (1-ratio) * 1;
                    } else
                    {
                        grade *=  recentDeathDisadvantageFactor;
                    }
                } else
                {
                    // Enemy retter unlikely to concern himself with his capper
                    // TODO If enemy flag is not taken, give enemy retter a little rating boost if timesinceinbase is high, since he's likely to be at his own base
                    grade *= this.retCount > 0 ? (float)this.retCount : 1f;

                    // Opposite treatment for other team
                    float recentDeathAdvantageFactor = (float)Math.Pow(1.3, Math.Abs(this.lastDeath - 1500) / 1000); // This will count the more recently flag was seen at base
                    float recentDeathDisadvantageFactor = (float)Math.Pow(1.3, 10000 - Math.Min(this.lastDeath, 10000) / 1000); // This will count the longer it's been


                    if (timeSinceInBase <= 3000f)
                    {
                        grade *= recentDeathDisadvantageFactor;
                    }
                    else if (timeSinceInBase > 3000f && timeSinceInBase <= 6000f) // Blend between recent death advantage and 1
                    {
                        float ratio = (timeSinceInBase - 3000f) / 3000f;
                        grade *= (1 - ratio) * recentDeathDisadvantageFactor + ratio * 1;
                    }
                    else if (timeSinceInBase > 6000f && timeSinceInBase <= 9000f) // blend between 1 and recent death disadvantage
                    {
                        float ratio = (timeSinceInBase - 6000f) / 3000f;
                        grade *= ratio * recentDeathAdvantageFactor + (1 - ratio) * 1;
                    }
                    else
                    {
                        grade *= recentDeathAdvantageFactor;
                    }
                }
#if DEBUG && LOGDECISIONS
                DecisionsLogger.logDecisionGrading(this);
#endif
                return grade;
            }

            // Bigger value: worse decision
            public float gradeForFlagAtBase(bool flagInvisibleDeathBonus = false)
            {
                gradeMethod = "gradeForFlagAtBase(" + flagInvisibleDeathBonus.ToString() +  ")";

                grade = 1;
                if (flagInvisibleDeathBonus && 
                    this.isOnSameTeamAsFlag && // Enemy players dying just puts them in their own base. Information age bc need no guessing if we KNOW
                    (informationAge > 2000 || // Only make guesses if our information is either outdated or from before the player died 
                    informationAge > this.lastDeath) && // Flag may be invisible but it's position IS known
                    this.lastDeath < 4000
                    ) { 
                    
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
#if DEBUG && LOGDECISIONS
                    DecisionsLogger.logDecisionGrading(this);
#endif
                    return grade;
                    
                }

                if (!this.isOnSameTeamAsFlag)
                {
                    // If player is on opposing team and died recently, he's almost certainly a bad choice.
                    // If someone died recently he's almost certainly in his own base and not near the flag of the enemy team.
                    // TODO Adjust this automatically based on map size measured by distance between both flag bases
                    grade *= (float)Math.Pow(1.5, 10000- Math.Min(this.lastDeath,10000) / 1000);
                }

                grade *= (float)Math.Pow(3, Math.Min(this.informationAge, 7000) / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than cappers.
#if DEBUG && LOGDECISIONS
                DecisionsLogger.logDecisionGrading(this);
#endif
                return grade;
            }
            
            public float gradeForFlagDroppedWithKnownPosition(bool flagIsVisible,float flagDistanceFromBase)
            {
                gradeMethod = "gradeForFlagDroppedWithKnownPosition(" + flagIsVisible.ToString()+","+ flagDistanceFromBase.ToString() + ")";

                grade = 1;
                if (!flagIsVisible && this.isOnSameTeamAsFlag && flagDistanceFromBase<2000) { // Flag is still near base, so give recently died players on team a bonus
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
#if DEBUG && LOGDECISIONS
                        DecisionsLogger.logDecisionGrading(this);
#endif
                        return grade;
                    }
                }

                if (!this.isOnSameTeamAsFlag && flagDistanceFromBase < 2000)
                {
                    // If player is on opposing team and died recently, he's almost certainly a bad choice.
                    // If someone died recently he's almost certainly in his own base and not near the flag of the enemy team.
                    // TODO Adjust this automatically based on map size measured by distance between both flag bases
                    grade *= (float)Math.Pow(1.5, 10000- Math.Min(this.lastDeath,10000) / 1000);
                }

                grade *= (float)Math.Pow(3, Math.Min(this.informationAge,7000) / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than cappers.
#if DEBUG && LOGDECISIONS
                DecisionsLogger.logDecisionGrading(this);
#endif
                return grade;
            }

            // Bigger value: worse decision
            public float gradeForFlagTakenAndVisible(bool currentlyFollowingFlagCarrier)
            {
                gradeMethod = "gradeForFlagTakenAndVisible(" + currentlyFollowingFlagCarrier.ToString()+ ")";

                grade = 1;

                if (isOnSameTeamAsFlag)
                {
                    // Good retter more interesting to watch
                    grade /= this.retCount > 0 ? this.retCount : 1f;
                }
                grade *= (float)Math.Pow(3, Math.Min(this.informationAge, 7000) / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than teammates of cappers.

                if (currentlyFollowingFlagCarrier)
                {
                    // This means he might only be visible because we are actually following him. In that case, don't penalize him so hard.
                    // But stil, prefer to watch a retter 2000 units away than the capper himself
                    grade *= this.isCarryingTheFlag ? 9f : 1f;
                    // On the other hand penalize players who aren't visible. If the carrier can't see them, they can't see him (likely)
                    grade *= !this.isVisible ? 81f : 1f; // harsh penalty
                } else { 
                    grade *= this.isCarryingTheFlag ? float.PositiveInfinity : 1f; // Don't want to watch the capper himself. He is visible already, there's no need to compromise.
                }

                grade *= this.isCarryingTheOtherTeamsFlag ? 1.15f : 1f; // Action from flag carrier against flag carrier is cool, give it a slight bonus
#if DEBUG && LOGDECISIONS
                DecisionsLogger.logDecisionGrading(this);
#endif
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

    static class DecisionsLogger
    {
        private static StreamWriter sw = null;

        static DecisionsLogger()
        {
            AppDomain.CurrentDomain.ProcessExit +=
                DecisionsLogger_Dtor;

            sw = new StreamWriter(new FileStream("playerDecisionsDEBUG.csv", FileMode.Append, FileAccess.Write, FileShare.Read));
            if(sw.BaseStream.Position == 0)
            { // Empty file
                sw.WriteLine("clientNum,informationAge,distance,isAlive,isOnSameTeamAsFlag,lastDeath,retCount,lastDeathDistance,isCarryingTheOtherTeamsFlag,isCarryingTheFlag,isVisible,gradegradeMethod");
            }
        }

        static void DecisionsLogger_Dtor(object sender, EventArgs e)
        {
            // clean it up
            sw.Close();
            sw.Dispose();
        }

        public static void logDecisionGrading(CTFCameraOperatorRedBlue.PossiblePlayerDecision possiblePlayerDecision)
        {
            if(sw != null)
            {
                sw.WriteLine($"\"{possiblePlayerDecision.clientNum}\",\"{possiblePlayerDecision.informationAge}\",\"{possiblePlayerDecision.distance}\",\"{possiblePlayerDecision.isAlive}\",\"{possiblePlayerDecision.isOnSameTeamAsFlag}\",\"{possiblePlayerDecision.lastDeath}\",\"{possiblePlayerDecision.retCount}\",\"{possiblePlayerDecision.lastDeathDistance}\",\"{possiblePlayerDecision.isCarryingTheOtherTeamsFlag}\",\"{possiblePlayerDecision.isCarryingTheFlag}\",\"{possiblePlayerDecision.isVisible}\",\"{possiblePlayerDecision.gradeMethod}\"");
            }
        }
    }

}
