
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

        private Task backgroundTask = null;

        private DecisionsLogger decisionsLogger = null;

        public override void Initialize()
        {
            base.Initialize();
            decisionsLogger = new DecisionsLogger(infoPool);
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { Run(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                HasErrored = true;
                OnErrored(new ErroredEventArgs( t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        ~CTFCameraOperatorRedBlue()
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
            FlagStatus[] lastFlagStatus = new FlagStatus[] { (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1) }; // What it was last time we checked
            FlagStatus[] lastLastFlagStatus = new FlagStatus[] { (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1), (FlagStatus)(-1) }; // What it was before the last change
            Team[] teams = new Team[] { Team.Red, Team.Blue };
            while (true)
            {
                System.Threading.Thread.Sleep(100);
                ct.ThrowIfCancellationRequested();

                if (!infoPool.isIntermission) { // No use during intermission, and avoid server errors popping up from trying to follow during intermission
                    foreach (Team team in teams)
                    {
                        bool statusChanged = infoPool.teamInfo[(int)team].flag != lastFlagStatus[(int)team];
                        if (statusChanged)
                        {
                            lastLastFlagStatus[(int)team] = lastFlagStatus[(int)team];
                        }
                        switch (infoPool.teamInfo[(int)team].flag)
                        {
                            case FlagStatus.FLAG_ATBASE:
                                handleFlagAtBase(team, statusChanged, lastLastFlagStatus[(int)team]);
                                break;
                            case FlagStatus.FLAG_TAKEN:
                                handleFlagTaken(team, statusChanged, lastLastFlagStatus[(int)team]);
                                break;
                            case FlagStatus.FLAG_DROPPED:
                                handleFlagDropped(team, statusChanged, lastLastFlagStatus[(int)team]);
                                break;
                            default:
                                // uh, dunno.
                                break;
                        }
                        lastFlagStatus[(int)team] = infoPool.teamInfo[(int)team].flag;
                    }

                }

            }
        }


        // TODO For stick around, if the player we were following kills himself, and he was NOT the killer, maybe jump out of his body.

        HashSet<int>[] playersCycled = new HashSet<int>[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        enum FlagAtBasePlayerCyclePriority : int
        {
            ANY_VALID_PLAYER,
            SAME_TEAM_PLAYER,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_TWO_SECONDS,
            SAME_TEAM_PLAYER_WHO_DIED_IN_LAST_SECOND,
        }
        Dictionary<int, float>[] lastGradings = new Dictionary<int, float>[] { new Dictionary<int, float>(), new Dictionary<int, float>(), new Dictionary<int, float>(), new Dictionary<int, float>() };
        bool[] stuckAround = new bool[] { false, false,false,false };
        private void handleFlagAtBase(Team flagTeam, bool flagStatusChanged, FlagStatus lastLastFlagStatus)
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


            double timeSinceFlagCarrierFrag = infoPool.teamInfo[teamInt].lastFlagCarrierFragged != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierFragged.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceFlagCarrierWorldDeath = infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceLastFlagUpdate = infoPool.teamInfo[teamInt].lastFlagUpdate != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagUpdate.Value).TotalMilliseconds : double.PositiveInfinity;
            
            bool stickAround = false;
            // Check if we should stick around where we already are for a while. If there was a cool kill, we wanna see a couple seconds of aftermath.
            if (
                timeSinceFlagCarrierFrag < 2500
                || timeSinceFlagCarrierWorldDeath < 1500 
                || timeSinceLastFlagUpdate < 100) // Since maybe message hasnt finished fully processing yet.... might need to solve this in a more clever way somehow, idk.
            {
                // If there was a frag of the flag carrier, stick around for 2,5 seconds
                // If there was a world death (like fall damage) of the flag carrier, stick around for 1,5 seconds
                // But this can be overridden if we KNOW (for whatever reason, for example from other connection) that an enemy is very close to the flag right now
                // Likewise, if we know this NOT to be the case (no enemy nearby right now) we can stick around even longer to have a better rhythm.
                stickAround = true;
            }

            if (flagStatusChanged)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagStatus changed");
                if ( lastLastFlagStatus == FlagStatus.FLAG_TAKEN)
                {
                    stuckAround[teamInt] = stickAround; // When status has freshly changed to "at base", we have no valid reference value for "stuckAround" so we reset it to the current stickAround value.
                }
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            int flagItemNumber = -1;
            double flagLastSeen = double.PositiveInfinity;
            if (infoPool.teamInfo[teamInt].lastFlagBaseItemPositionUpdate != null)
            {
                flagLastSeen = (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagBaseItemPositionUpdate.Value).TotalMilliseconds;
                flagPosition = infoPool.teamInfo[teamInt].flagBaseItemPosition; // Actual flag item
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseItemEntityNumber == 0 ? -1: infoPool.teamInfo[teamInt].flagBaseItemEntityNumber;
            } else if (infoPool.teamInfo[teamInt].lastFlagBasePositionUpdate != null)
            {
                flagLastSeen = (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagBasePositionUpdate.Value).TotalMilliseconds;
                flagPosition = infoPool.teamInfo[teamInt].flagBasePosition; // Flag base. the thing it stands n
                flagItemNumber = infoPool.teamInfo[teamInt].flagBaseEntityNumber == 0 ? -1 : infoPool.teamInfo[teamInt].flagBaseEntityNumber;
            }

            if (flagPosition == null)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is Null");
                // This is extremely unlikely, but we handle it anyway. If there was a kill a short while ago and we don't know where the flag is rn, stick around is used.
                if (stickAround && stuckAround[teamInt])
                {
                    return; // Do nothing basically.
                }

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
                    if(nextPlayerTotry != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + nextPlayerTotry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    stuckAround[teamInt] = false;
                }

            }
            else
            {
                //
                // Ok we know where the flag is. Now what?
                //
                //
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is known");

                // First off, is the flag visible RIGHT now?
                bool flagVisible = flagItemNumber==-1 ? false: (connection.client.Entities?[flagItemNumber].CurrentValid).GetValueOrDefault(false);
                if (flagVisible)
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag is visible");
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


                // Stick around?
                // We assume that one of the clients is/was near the flag in the base if last flag position update is less than 200ms ago, as the client has seen it. This means the client likely would have also seen any players near it.
                // We can use this knowledge to do some more fancy guesses.
                bool flagVisibleSomewhere = flagLastSeen < 200; 
                if (!flagVisibleSomewhere && stickAround && stuckAround[teamInt])
                {
                    return; // Stick around.
                } else if (flagVisibleSomewhere)
                {
                    if (stickAround && stuckAround[teamInt])
                    {
                        bool anyCloseByEnemiesConfirmed = false;
                        // Check if any enemies are close to the flag. If so, we override stick around and change NOW.
                        for (int i = 0; i < infoPool.playerInfo.Length; i++)
                        {
                            if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator && infoPool.playerInfo[i].team != flagTeam && infoPool.playerInfo[i].IsAlive)
                            {
                                float informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds;
                                float distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity*informationAge/1000.0f - flagPosition.Value).Length();
                                if(informationAge < 200 && distance <= 200) 
                                {
                                    // I say 200 units counts as being close enough to force a perspective change right this very moment.
                                    // If the info is more than 100 ms old, this player likely isn't actually visible or something weird is going on, so we let it go.
                                    anyCloseByEnemiesConfirmed = true;
                                }
                               
                            }
                        }
                        if (!anyCloseByEnemiesConfirmed)
                        {
                            return;
                        }
                        else // At least stick around for a second. So it's not a completely abrupt cut. Hell maybe TODO make it even persist into FlagTaken.
                        {
                            if (
                                timeSinceFlagCarrierFrag < 1000
                                || timeSinceFlagCarrierWorldDeath < 1000
                                || timeSinceLastFlagUpdate < 100
                                )
                            {
                                return;
                            }
                        }
                    } else if(stuckAround[teamInt]) 
                    {
                        // Now we check if maybe we stick around for longer than originally planned
                        // This is only meaningful if stuckAround is true, meaning that we haven't flicked to a different player in the meantime already.
                        bool anySomewhatCloseByEnemiesConfirmed = false;
                        // Check if any enemies are close to the flag. If so, we override stick around and change NOW.
                        for (int i = 0; i < infoPool.playerInfo.Length; i++)
                        {
                            if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator && infoPool.playerInfo[i].team != flagTeam && infoPool.playerInfo[i].IsAlive)
                            {
                                float informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds;
                                float distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity * informationAge/1000.0f - flagPosition.Value).Length();
                                if (informationAge < 100 && distance <= 700)
                                {
                                    // I say 700 units counts as close enough to trigger normal behavior. Anything farther away gives bonus time to stick around.
                                    // If the info is more than 100 ms old, this player likely isn't actually visible or something weird is going on, so we let it go.
                                    anySomewhatCloseByEnemiesConfirmed = true;
                                }

                            }
                        }
                        if (!anySomewhatCloseByEnemiesConfirmed)
                        {
                            // To the best of our knowledge, all enemies are pretty far away from the flag, so we aren't forced to switch right now yet.
                            // So instead we reevaluate stick around with more generous times
                            // We do not want to stick around endlessly tho, at some point there's nothing relevant to see anymore really.
                            // So I'd give it 7,5 seconds max for frags and 5 seconds for "natural cause deaths"
                            if (
                                timeSinceFlagCarrierFrag < 7500
                                || timeSinceFlagCarrierWorldDeath < 5000
                                || timeSinceLastFlagUpdate < 100
                                )
                            {
                                return;
                            }
                        }
                    }
                    
                }



                // My approach: Add velocity to position. That way we will know roughly where a player will be 1 second in the future
                // Measure distance from that predicted place to the flag position.

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() {decisionsLogger = decisionsLogger, grade=float.PositiveInfinity}, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++) {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            decisionsLogger = decisionsLogger,
                            informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds,
                            distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity - flagPosition.Value).Length(),
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                        };
                        tmp.gradeForFlagAtBase(flagTeam, !flagVisible);
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
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue - 100); // maxvalue clamping and casting to int doesnt work well due to floating point precision or lack thereof
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
                        if (possibleNextPlayers[0].clientNum != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                        stuckAround[teamInt] = false;
                    }
                }
                else
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag not visible handling");
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
                        if (nextPlayerToTry != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        stuckAround[teamInt] = false;
                    }

                    // Todo: problem with this algorithm is that someone added to the list of "already cycled through" may become a 
                    // good choice again rather quickly and then we might ignore him just because he's on that list.
                    // Maybe we should use some kind of list that expires after a while. Or remove clientnums from it if they die etc.
                    // Or simply maybe: If the person's alive status changed, remove him from the list? idk. Let's leave it like this for now.
                    // Or: If the person's grade has improved by more than a certain factor, remove from list.
                }


            }
        }

        private void handleFlagTaken(Team flagTeam, bool flagStatusChanged, FlagStatus lastLastFlagStatus)
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


            double timeSinceFlagCarrierFrag = infoPool.teamInfo[teamInt].lastFlagCarrierFragged != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierFragged.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceFlagCarrierWorldDeath = infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceLastFlagUpdate = infoPool.teamInfo[teamInt].lastFlagUpdate != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagUpdate.Value).TotalMilliseconds : double.PositiveInfinity;


            bool stickAround = false;
            // Check if we should stick around where we already are for a while. If there was a cool kill, we wanna see a couple seconds of aftermath.
            // But the flag is already stolen AGAIN, so we don't stick around for long. Only 0.5-1 second max just so the cut isn't COMPLETELY abrupt.
            // Even like this we might already miss out on a quick ret so let's check if there's an enemy very close to retting and if so, switch immediately anyway.
            if (
                timeSinceFlagCarrierFrag < 1000
                || timeSinceFlagCarrierWorldDeath < 500
                || timeSinceLastFlagUpdate < 100) // Since maybe message hasnt finished fully processing yet.... might need to solve this in a more clever way somehow, idk.
            {
                stickAround = true;
            }


            if (flagStatusChanged)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag status changed");
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Find out who has it
            if (infoPool.teamInfo[(int)flagTeam].lastFlagCarrierUpdate == null)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Carrier unknown");
                // Uhm idk, that's weird. We know the flag's taken but we don't know who has it
                // Maybe if the tool didn't witness the taking of the flag and didn't have time to receive the info from somewhere.

                // We could probably just sit this one out but let's cycle through players anyway?
                // Might be faster in some cases but might also be slower in others due to more commands having been sent?

                // Let's make a compromise. If stickAround is active, we wait a bit. If not, we start cycling.
                if (stickAround && stuckAround[teamInt])
                {
                    return;
                }


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
                            decisionsLogger = decisionsLogger,
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                        };
                        tmp.gradeForFlagTakenUnknownPlayer(flagTeam);
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
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue - 100); // maxvalue clamping and casting to int doesnt work well due to floating point precision or lack thereof
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
                    if (nextPlayerToTry != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    stuckAround[teamInt] = false;
                }

                return;
            }

            // Okay we know who the flag carrier is now
            // Do we know WHERE is?
            int flagCarrier = infoPool.teamInfo[(int)flagTeam].lastFlagCarrier;

            // TODO Make this more flexible. Check generally if he is visible SOMEWHERE (not just on this associated connection). But make sure it doesnt break any of the existing logic.
            bool flagCarrierVisible = (connection.client.Entities?[flagCarrier].currentValidOrFilledFromPlayerState()).GetValueOrDefault(false);
            bool currentlyFollowingFlagCarrier = flagCarrier == currentlySpectatedPlayer;

            if (!flagCarrierVisible)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition invisible");
                // Okay now get this... we know who the flag carrier is...
                // How do we find out where he is?
                // Simple, we follow him.
                // However ... maybe he only just disappeared behind a corner but we actually know someone who's closer to him
                // So we could give that a try.
                // In short: If he disappeared recently (up to half second?) ago, just see if we know of any player who we know is closer
                // to him. If not, just switch to the carrier himself.
                if (infoPool.playerInfo[flagCarrier].lastFullPositionUpdate == null)
                {
                    // We don't even know where the flag carrier is or was. 
                    // Check if we should stick around. Since his position is not known, we cannot decide whether anyone is near him,
                    // so we err on the side of sticking around. This could miss rets sometimes but oh well.
                    if (stickAround && stuckAround[teamInt])
                    {
                        return;
                    }
                    // Just follow him.
                    if (flagCarrier != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    stuckAround[teamInt] = false;
                }
                else
                {
                    float lastSeen = (float)(DateTime.Now - infoPool.playerInfo[flagCarrier].lastFullPositionUpdate.Value).TotalMilliseconds / 1000.0f;

                    if (lastSeen < 1f)
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
                        bool anyCloseByPossibleReturnersConfirmed = false; // For stickAround
                        for (int i = 0; i < infoPool.playerInfo.Length; i++)
                        {
                            if (infoPool.playerInfo[i].team == flagTeam && infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].IsAlive && infoPool.playerInfo[i].team != Team.Spectator)
                            {


                                float lastSeenThisPlayer = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds / 1000.0f;
                                if(lastSeenThisPlayer < 1f)
                                {

                                    float distanceRightNow = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity * lastSeenThisPlayer - (infoPool.playerInfo[flagCarrier].position + infoPool.playerInfo[flagCarrier].velocity * lastSeen)).Length();
                                    if (distanceRightNow <= 200)
                                    {
                                        // I say 200 units counts as being close enough to force a perspective change right this very moment.
                                        // If the info is more than 200 ms old, this player likely isn't actually visible or something weird is going on, so we let it go.
                                        anyCloseByPossibleReturnersConfirmed = true;
                                    }

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

                        // If no close by potential returners are confirmed, we can probably safely do the stick around. 
                        if (!anyCloseByPossibleReturnersConfirmed && stickAround && stuckAround[teamInt])
                        {
                            return;
                        }

                        if (closestPlayer == -1)
                        {
                            // Ok we found nobody closer. Follow carrier himself.
                            if (flagCarrier != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                            stuckAround[teamInt] = false;
                        }
                        else
                        {
                            // give this guy a chance.
                            if (closestPlayer != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + closestPlayer, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                            stuckAround[teamInt] = false;
                        }

                        // We don't do any cycling here because once you switch to the closer player you won't accidentally switch to him AGAIN

                    } else
                    {
                        if (stickAround && stuckAround[teamInt])
                        {
                            return;
                        }

                        // Don't really have any up to date information on anyone who's closer so just follow the carrier
                        if (flagCarrier != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + flagCarrier, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        stuckAround[teamInt] = false;
                        // Todo: Maybe also consider that distance may not be a perfect measure, since someone could be through a wall and technically closer but a further player may actually see the flag carrier
                    }
                    

                }
                


            } else
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag carrier visible");
                // Flag carrier IS visible.
                playersCycled[teamInt].Clear(); 
                lastGradings[teamInt].Clear();

                float lastSeen = infoPool.playerInfo[flagCarrier].lastFullPositionUpdate.HasValue ? (float)(DateTime.Now - infoPool.playerInfo[flagCarrier].lastFullPositionUpdate.Value).TotalMilliseconds/1000.0f : float.PositiveInfinity; // Need to check. In rare freak cases it can cause null object exception.

                Vector3 flagCarrierPositionInOneSecond = infoPool.playerInfo[flagCarrier].position + infoPool.playerInfo[flagCarrier].velocity;

                bool anyCloseByPossibleReturnersConfirmed = false; // For stickAround

                // Assemble list of players we'd consider watching
                List<PossiblePlayerDecision> possibleNextPlayers = new List<PossiblePlayerDecision>();
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() { decisionsLogger = decisionsLogger, grade = float.PositiveInfinity }, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {

                        float informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds;
                        
                        // For stick around. Just quick check if any possible returners are near.
                        if (infoPool.playerInfo[i].team == flagTeam)
                        {
                            float distanceRightNow = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity * informationAge/1000.0f - (infoPool.playerInfo[flagCarrier].position + infoPool.playerInfo[flagCarrier].velocity * lastSeen)).Length();
                            
                            if (distanceRightNow <= 200 && lastSeen < 0.2f)
                            {

                                // I say 200 units counts as being close enough to force a perspective change right this very moment.
                                // If the info is more than 200 ms old, this player likely isn't actually visible or something weird is going on, so we let it go.
                                anyCloseByPossibleReturnersConfirmed = true;
                            }
                        }

                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            decisionsLogger = decisionsLogger,
                            informationAge = informationAge,
                            distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity - flagCarrierPositionInOneSecond).Length(),
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            isVisible = (connection.client.Entities?[i].currentValidOrFilledFromPlayerState()).GetValueOrDefault(false),
                            isCarryingTheFlag = i == flagCarrier,
                            retCount = infoPool.playerInfo[i].score.impressiveCount,
                            isCarryingTheOtherTeamsFlag = (infoPool.teamInfo[opposingTeamInt].flag == FlagStatus.FLAG_TAKEN && infoPool.teamInfo[opposingTeamInt].lastFlagCarrierUpdate != null) ? infoPool.teamInfo[opposingTeamInt].lastFlagCarrier == i : false
                        };
                        tmp.gradeForFlagTakenAndVisible(flagTeam, currentlyFollowingFlagCarrier);
                        possibleNextPlayers.Add(tmp);
                        if (i == currentlySpectatedPlayer) stayWithCurrentPlayerDecision = tmp;
                    }
                }
                if (possibleNextPlayers.Count == 0)
                {
                    return; // Idk, not really much we can do here lol!
                }

                // If no close by potential returners are confirmed, we can probably safely do the stick around. 
                if (!anyCloseByPossibleReturnersConfirmed && stickAround && stuckAround[teamInt])
                {
                    return;
                }

                // Sort players by how good they are as choices
                possibleNextPlayers.Sort((a, b) => {
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue - 100); // maxvalue clamping and casting to int doesnt work well due to floating point precision or lack thereof
                });

                if (currentlySpectatedPlayer == possibleNextPlayers[0].clientNum)
                {
                    return; // Flag is visible and we already have the best player choice
                }

                if (currentlyFollowingFlagCarrier || infoPool.playerInfo[currentlySpectatedPlayer].team != flagTeam) // This is unfortunate and any improvement is good.
                {
                    // Flag is visible but we're following the capper or the capper's team. Boring.

                    // Switch with decent speed but not too hectically (well, up for debate I guess. We'll see)
                    if (possibleNextPlayers[0].clientNum != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 5, 600, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    stuckAround[teamInt] = false;
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
                        if (possibleNextPlayers[0].clientNum != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                        stuckAround[teamInt] = false;
                    }
                }
                  
            }

        }

        private void handleFlagDropped(Team flagTeam, bool flagStatusChanged, FlagStatus lastLastFlagStatus)
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



            double timeSinceFlagCarrierFrag = infoPool.teamInfo[teamInt].lastFlagCarrierFragged != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierFragged.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceFlagCarrierWorldDeath = infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagCarrierWorldDeath.Value).TotalMilliseconds : double.PositiveInfinity;
            double timeSinceLastFlagUpdate = infoPool.teamInfo[teamInt].lastFlagUpdate != null ? (DateTime.Now - infoPool.teamInfo[teamInt].lastFlagUpdate.Value).TotalMilliseconds : double.PositiveInfinity;
            bool stickAround = false;
            // Check if we should stick around where we already are for a while. If there was a cool kill, we wanna see a couple seconds of aftermath.
            // Technically we should still remain around since we are in flag dropped status now. 
            // However, our flag dropped algorithm is a little bit different so in reality it often leads to a switch and thus switches right after kill
            // which makes it jarring to watch. So let's not do that.
            // Note: Problem here may be that the cs 23 command arrives first, so flag status may have changed without us having information about the flag carrier death yet.
            // These should really come in the same message from the server as they're all reliable commands, so if we don't have that info yet, it's likely because it just 
            // hasn't finished processing yet? So allow for stickAround if timeSinceLastFlagUpdate < 100 regardless to give it a lil bit of time.
            if (
                timeSinceFlagCarrierFrag < 2500
                || timeSinceFlagCarrierWorldDeath < 1500
                || timeSinceLastFlagUpdate < 100)
            {
                // If there was a frag of the flag carrier, stick around for 2,5 seconds
                // If there was a world death (like fall damage) of the flag carrier, stick around for 1,5 seconds
                // But this can be overridden if we KNOW (for whatever reason, for example from other connection) that an enemy is very close to the flag right now
                // Likewise, if we know this NOT to be the case (no enemy nearby right now) we can stick around even longer to have a better rhythm.
                stickAround = true;
            }

            if (flagStatusChanged)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flagstatus changed");
                if (lastLastFlagStatus == FlagStatus.FLAG_TAKEN)
                {
                    stuckAround[teamInt] = stickAround;
                }
                playersCycled[teamInt].Clear(); // Reset cycling array if the flag status changed.
                lastGradings[teamInt].Clear();
            }

            // Try to figure out it's rough position
            Vector3? flagPosition = null;
            int flagItemNumber = -1;
            if (infoPool.teamInfo[teamInt].lastFlagDroppedPositionUpdate != null
                )
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag dropped position is unknown");

                // Is the last time we updated dropped flag position more than 500ms older than last time we updated flag status?
                // If so, we most likely still have the OLD flag dropped position from when it was dropped previously.
                // This could happen due to us not being near where the flag drop happened or the dropped flag item not having spawned yet (most likely rare timing issue)
                bool flagPositionUnknown = infoPool.teamInfo[teamInt].lastFlagUpdate != null && (infoPool.teamInfo[teamInt].lastFlagUpdate.Value - infoPool.teamInfo[teamInt].lastFlagDroppedPositionUpdate.Value).TotalMilliseconds > 500;

                if (!flagPositionUnknown)
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is known");
                    flagPosition = infoPool.teamInfo[teamInt].flagDroppedPosition; // Actual flag item

                    // TODO Be very careful here. We may know the position (from the drop notification) but not the item number.
                    // That actually will need some kind of fix I think, very unelegant
                    flagItemNumber = infoPool.teamInfo[teamInt].droppedFlagEntityNumber == 0 ? -1 : infoPool.teamInfo[teamInt].droppedFlagEntityNumber;
                } else if (infoPool.teamInfo[teamInt].lastFlagUpdate != null && (DateTime.Now-infoPool.teamInfo[teamInt].lastFlagUpdate.Value).TotalMilliseconds < 500)
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is unknown shortly after drop");
                    // The flag position is unknown.
                    // It's only been a very short moment since the flag was dropped. Maybe the dropped flag item hasn't spawned yet or something like that. 
                    // This could be the case because flag status updates are reliable commands, but entities are evaluated afterwards. It's a very short timespan
                    // between the two, but in principle it can happen. Multithreading YAY!
                    // Give it some room to breathe and do nothing for now.
                    return;
                }
            }

            if (flagPosition == null)
            {
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is null");

                // Re. stickAround
                // flagdropped is different from at base handling. We don't know where the flag is, so there isn't much of a point to sticking around because the implication is
                // that we aren't near where whatever happened happened.

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
                            decisionsLogger = decisionsLogger,
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount // Will only be filled on nwh but won't hurt anything otherwise. TODO : Do our own counting?
                        };
                        tmp.gradeForFlagDroppedUnknownPosition(flagTeam, timeSinceAtBase);
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
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue - 100); // maxvalue clamping and casting to int doesnt work well due to floating point precision or lack thereof
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
                    if (nextPlayerToTry != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                    stuckAround[teamInt] = false;
                }

                return;
            }
            else
            {
                //
                // Ok we know where the flag is. Now what?
                //
                //
                decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "FlagPosition is known");

                // First off, is the flag visible RIGHT now?
                bool flagVisible = flagItemNumber == -1 ? false: (connection.client.Entities?[flagItemNumber].CurrentValid).GetValueOrDefault(false); // If we don't know its item number it can't be visible either duh!
                if (flagVisible)
                {
                    // re. stickARound
                    // Flag is visible. We don't really *have* to do anything. Let's respect stickAround
                    // If the flag is not visible, there's not that much reason to stick around really. 
                    if(stickAround && stuckAround[teamInt])
                    {
                        return;
                    } else if (stuckAround[teamInt] && 
                       (timeSinceFlagCarrierFrag < 7500 || timeSinceFlagCarrierWorldDeath < 5000 || timeSinceLastFlagUpdate < 100 ))
                    {
                        // Since the flag is actually visible we can stick around for even longer no problemo.
                        return;
                        // TODO 
                        // Just a thought. We might wanna handle the case where a flag disappears after someone actually touches it to return it to base. 
                        // So it doesn't interrupt stuckAround.
                        // HOWEVER, maybe this is not necessary because flag status changes are reliable commands and evaluated earlier than the snapshot.
                        // so we should, in principle, never run into a situation where the flag has disappeared back to base without the flag status being updated first.
                    }


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
                PossiblePlayerDecision stayWithCurrentPlayerDecision = new PossiblePlayerDecision() { decisionsLogger = decisionsLogger, grade = float.PositiveInfinity }, tmp; // We set grade to positive infinity in case the currently spectated player isn't found. (for example if we're not spectating anyone)
                for (int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    if (infoPool.playerInfo[i].infoValid && infoPool.playerInfo[i].lastFullPositionUpdate != null && infoPool.playerInfo[i].team != Team.Spectator)
                    {
                        // Todo: Allow an option for using lastPositionUpdate instead of  lastFullPositionUpdate to get more up to date info
                        double lastDeath = infoPool.playerInfo[i].lastDeath.HasValue ? (DateTime.Now - infoPool.playerInfo[i].lastDeath.Value).TotalMilliseconds : 60000;
                        tmp = new PossiblePlayerDecision()
                        {
                            decisionsLogger = decisionsLogger,
                            informationAge = (float)(DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMilliseconds,
                            distance = (infoPool.playerInfo[i].position + infoPool.playerInfo[i].velocity - flagPosition.Value).Length(),
                            isAlive = infoPool.playerInfo[i].IsAlive,
                            clientNum = i,
                            isOnSameTeamAsFlag = infoPool.playerInfo[i].team == flagTeam,
                            lastDeath = (int)lastDeath,
                            retCount = infoPool.playerInfo[i].score.impressiveCount
                        };
                        tmp.gradeForFlagDroppedWithKnownPosition(flagTeam,flagVisible,flagDistanceFromBase);
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
                    return (int)Math.Clamp(a.grade - b.grade, Int32.MinValue, Int32.MaxValue-100); // maxvalue clamping and casting to int doesnt work well due to floating point precision or lack thereof
                });

                if (flagVisible)
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "flag is visible");
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
                        if (possibleNextPlayers[0].clientNum != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + possibleNextPlayers[0].clientNum, RequestCategory.FOLLOW, 3, 1000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                        stuckAround[teamInt] = false;
                    }
                }
                else
                {
                    decisionsLogger.logLine(flagTeam, System.Reflection.MethodBase.GetCurrentMethod().Name, "Flag is not visible");
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
                        if (nextPlayerToTry != currentlySpectatedPlayer) connection.leakyBucketRequester.requestExecution("follow " + nextPlayerToTry, RequestCategory.FOLLOW, 5, 366, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE, new RequestCategory[] { RequestCategory.SCOREBOARD });
                        stuckAround[teamInt] = false;
                    }

                }


            }
        }


        public struct PossiblePlayerDecision
        {

            public DecisionsLogger decisionsLogger;

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

            public float gradeForFlagTakenUnknownPlayer(Team team)
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
                decisionsLogger?.logDecisionGrading(team, this);
#endif
                return grade;
            }
            
            public float gradeForFlagDroppedUnknownPosition(Team team,float timeSinceInBase)
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
                    float recentDeathDisadvantageFactor = (float)Math.Pow(1.3, (10000 - Math.Min(this.lastDeath, 10000)) / 1000); // This will count the longer it's been
                    
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
                    float recentDeathDisadvantageFactor = (float)Math.Pow(1.3, (10000 - Math.Min(this.lastDeath, 10000)) / 1000); // This will count the longer it's been


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
                decisionsLogger?.logDecisionGrading(team, this);
#endif
                return grade;
            }

            // Bigger value: worse decision
            public float gradeForFlagAtBase(Team team,bool flagInvisibleDeathBonus = false)
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
                    decisionsLogger?.logDecisionGrading(team, this);
#endif
                    return grade;
                    
                }

                if (!this.isOnSameTeamAsFlag)
                {
                    // If player is on opposing team and died recently, he's almost certainly a bad choice.
                    // If someone died recently he's almost certainly in his own base and not near the flag of the enemy team.
                    // TODO Adjust this automatically based on map size measured by distance between both flag bases
                    grade *= (float)Math.Pow(1.5, (10000- Math.Min(this.lastDeath,10000)) / 1000);
                }

                grade *= (float)Math.Pow(3, Math.Min(this.informationAge, 7000) / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than cappers.
#if DEBUG && LOGDECISIONS
                decisionsLogger?.logDecisionGrading(team, this);
#endif
                return grade;
            }
            
            public float gradeForFlagDroppedWithKnownPosition(Team team,bool flagIsVisible,float flagDistanceFromBase)
            {
                gradeMethod = "gradeForFlagDroppedWithKnownPosition(" + flagIsVisible.ToString()+","+ flagDistanceFromBase.ToString() + ")";

                //throw new NotImplementedException();

                 //First try:
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
                        decisionsLogger?.logDecisionGrading(team, this);
#endif
                        return grade;
                    }
                }

                if (!this.isOnSameTeamAsFlag && flagDistanceFromBase < 2000)
                {
                    // If player is on opposing team and died recently, he's almost certainly a bad choice.
                    // If someone died recently he's almost certainly in his own base and not near the flag of the enemy team.
                    // TODO Adjust this automatically based on map size measured by distance between both flag bases
                    grade *= (float)Math.Pow(1.5, (10000- Math.Min(this.lastDeath,10000)) / 1000);
                }

                grade *= (float)Math.Pow(3, Math.Min(this.informationAge,7000) / 2000); // 2 second old information is 3 times worse. 4 second old information is 9 times  worse
                grade *= (float)Math.Pow(3, this.distance / 1000); // 1000 units away is 3 times worse. 2000 units away is 9 times worse.
                grade *= this.isAlive ? 1f : 9f; // Being dead makes you 9 times worse choice. Aka, a dead person is a better choice if he's more than 2000 units closer or if his info is more than 4 seconds newer.
                grade *= this.isOnSameTeamAsFlag ? 1.0f : 5.2f; // Being on opposing team is 9 times worse. The next best team member would have to be 1500 units away to justify following an opposite team member. (5.2 is roughly 3^1.5). It's cooler to watch retters than cappers.
#if DEBUG && LOGDECISIONS
                decisionsLogger?.logDecisionGrading(team, this);
#endif
                return grade;
            }

            // Bigger value: worse decision
            public float gradeForFlagTakenAndVisible(Team team,bool currentlyFollowingFlagCarrier)
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
                decisionsLogger?.logDecisionGrading(team,this);
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

    class DecisionsLogger
    {
        private StreamWriter sw = null;

        ServerSharedInformationPool infoPool = null;


        public DecisionsLogger(ServerSharedInformationPool _infoPool)
        {
            infoPool = _infoPool;

            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "playerDecisionsDebugLogs"));
            sw = new StreamWriter(new FileStream(Helpers.GetUnusedFilename(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "playerDecisionsDebugLogs\\playerDecisionsDEBUG.csv")), FileMode.Append, FileAccess.Write, FileShare.Read));
            if(sw.BaseStream.Position == 0)
            { // Empty file
                sw.WriteLine("team,time,clientNum,informationAge,distance,isAlive,isOnSameTeamAsFlag,lastDeath,retCount,lastDeathDistance,isCarryingTheOtherTeamsFlag,isCarryingTheFlag,isVisible,grade,gradeMethod");
            }
        }

        ~DecisionsLogger()
        {
            // clean it up
            Destroy();
        }

        private bool isDestroyed = false;
        private Mutex destructionMutex = new Mutex();
        private void Destroy()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    try // Idk why this is needed. Sometimes random clicks in UI make this happen
                    {

                        sw.Close(); // Got cannot access a closed file once, strange.
                        sw.Dispose();
                        isDestroyed = true; 
                    } catch (Exception e)
                    {
                        Helpers.logToFile(new string[] { e.ToString() });
                    }
                }
            }
        }

        public void logLine(Team team,string methodName,string line)
        {
            if(sw != null)
            {
                sw.WriteLine($"\"{team.ToString()}\",\"{infoPool.GameTime}\",\"logLine\",\"{methodName}\",\"{line}\"");
            }
        }
        public void logDecisionGrading(Team team, CTFCameraOperatorRedBlue.PossiblePlayerDecision possiblePlayerDecision)
        {
            if(sw != null)
            {
                sw.WriteLine($"\"{team.ToString()}\",\"{infoPool.GameTime}\",\"{possiblePlayerDecision.clientNum}\",\"{possiblePlayerDecision.informationAge}\",\"{possiblePlayerDecision.distance}\",\"{possiblePlayerDecision.isAlive}\",\"{possiblePlayerDecision.isOnSameTeamAsFlag}\",\"{possiblePlayerDecision.lastDeath}\",\"{possiblePlayerDecision.retCount}\",\"{possiblePlayerDecision.lastDeathDistance}\",\"{possiblePlayerDecision.isCarryingTheOtherTeamsFlag}\",\"{possiblePlayerDecision.isCarryingTheFlag}\",\"{possiblePlayerDecision.isVisible}\",\"{possiblePlayerDecision.grade}\",\"{possiblePlayerDecision.gradeMethod}\"");
            }
        }
    }

}
