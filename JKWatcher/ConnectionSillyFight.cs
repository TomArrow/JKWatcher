using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JKClient;
using Client = JKClient.JKClient;


namespace JKWatcher
{

	public enum FightBotTargetingMode
	{
		NONE,
		OPTIN,
		BERSERK,
		BODYGUARD // Not done yet
	}
	public enum SillyMode
	{
		NONE,
		SILLY,
		DBS,
		GRIPKICKDBS,
		LOVER,
		CUSTOM,
		ABSORBSPEED,
		MINDTRICKSPEED,
		WALKWAYPOINTS
	}
	public enum GripKickDBSMode
	{
		VANILLA,
		SPEED,
		SPEEDRAGE,
		SPEEDRAGEBS
	}

	// Silly fighting when forced out of spec
	public partial class Connection
	{


		static string[] fightBotNameBlacklist = null;
		static DateTime lastBlacklistUpdate = DateTime.Now;
		static DateTime lastBlacklistDateModified = DateTime.Now;
		static readonly string blacklistPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "playerBlackList.txt");
		private bool CheckPlayerBlacklist(string playerName)
		{
			if (fightBotNameBlacklist == null || (DateTime.Now - lastBlacklistUpdate).TotalMinutes > 1.0)
			{
                try { 
					using (new GlobalMutexHelper("JKWatcherSillyFightPlayerNameBlacklist")) // Check if file was changed
					{
						// Read blacklist again.
						if (!File.Exists(blacklistPath))
						{
							File.WriteAllText(blacklistPath, "");
							fightBotNameBlacklist = new string[0];
							serverWindow.addToLog($"Silly fighter player name blacklist loaded with {fightBotNameBlacklist.Length} items.");
						} else
						{
							DateTime lastModified = System.IO.File.GetLastWriteTime(blacklistPath);
                            if (!lastModified.Equals(lastBlacklistDateModified))
                            {
								string[] blacklistRaw = File.ReadAllLines(blacklistPath);
								List<string> blackListNamesTmp = new List<string>();
								foreach(string blackListItemRaw in blacklistRaw)
                                {
									string sanitized = blackListItemRaw.Trim();
									if(sanitized.Length > 0)
                                    {
										blackListNamesTmp.Add(sanitized);
									}
								}
								fightBotNameBlacklist = blackListNamesTmp.ToArray();
								serverWindow.addToLog($"Silly fighter player name blacklist loaded with {fightBotNameBlacklist.Length} items.");
							}
						}
						lastBlacklistUpdate = DateTime.Now;
					}
				} catch(Exception ex)
				{
					serverWindow.addToLog($"Error trying to load player blacklist: {ex.ToString()}",true);
					lastBlacklistUpdate = DateTime.Now;
				}
			}
			if(fightBotNameBlacklist == null)
            {
				return false;
            } else
            {
				foreach(string blockedName in fightBotNameBlacklist)
                {
					if (playerName.Contains(blockedName, StringComparison.OrdinalIgnoreCase) || Q3ColorFormatter.cleanupString(playerName).Contains(blockedName,StringComparison.OrdinalIgnoreCase))
					{
						serverWindow.addToLog($"Silly fighter player name blacklist match: {playerName} matches {blockedName}.",false,5000); // 5 second time out in case of weird userinfo spam
						return true;
					}
				}
				return false;
            }
		}

		enum GenericCommandJK2
		{
			SABERSWITCH = 1,
			ENGAGE_DUEL,
			FORCE_HEAL,
			FORCE_SPEED,
			FORCE_THROW,
			FORCE_PULL,
			FORCE_DISTRACT,
			FORCE_RAGE,
			FORCE_PROTECT,
			FORCE_ABSORB,
			FORCE_HEALOTHER,
			FORCE_FORCEPOWEROTHER,
			FORCE_SEEING,
			USE_SEEKER,
			USE_FIELD,
			USE_BACTA,
			USE_ELECTROBINOCULARS,
			ZOOM,
			USE_SENTRY,
			SABERATTACKCYCLE
		}


		private bool amNotInSpec = false; // If not in spec for whatever reason, do funny things
		private bool isDuelMode = false; // If we are in duel mode, different behavior. Some servers don't like us attacking innocents but for duel we have to, to end it quick. But if someone attacks US, then all bets are off.
		private GameType currentGameType = GameType.FFA;

		private bool oldIsDuelMode = false;

		public bool AllowBotFight = false;
		public bool HandlesFightBotChatCommands = false;
		private void DoSillyThings(ref UserCommand userCmd, in UserCommand previousCommand)
		{
			// Of course normally the priority is to get back in spec
			// But sometimes it might not be possible, OR we might not want it (for silly reasons)
			// So as long as we aren't in spec, let's do silly things.
			bool isFightyCameraOperator = /*this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator*/ this.CameraOperator is CameraOperators.SillyCameraOperator;
			if (isDuelMode || isFightyCameraOperator || AllowBotFight)
			{
				if(isDuelMode != oldIsDuelMode || infoPool.sillyMode == SillyMode.NONE) // If gametype changes or if not set yet.
                {
					infoPool.sillyMode = isDuelMode ? SillyMode.SILLY : SillyMode.GRIPKICKDBS;
					oldIsDuelMode = isDuelMode;
				}
				DoSillyThingsReal(ref userCmd, in previousCommand, infoPool.sillyMode);
			}
		}

		Int64 sillyflip = 0;
		bool sillyAttack = false;

		DateTime lastSaberAttackCycleSent = DateTime.Now;

		DateTime lastNavigationJump = DateTime.Now;

		int sillyMoveLastSnapNum = 0;
		int lastUserCmdTime = 0;
		float dbsLastRotationOffset = 0;
		float dbsLastVertRotationOffset = 0;
		bool amGripping = false;
		int personImTryingToGrip = -1;
		int sillyLastCommandTime = 0;
		Vector3 sillyOurLastPosition = new Vector3();
		Vector2 sillyOurLastPosition2D = new Vector2();
		DateTime lastTimeAnyPlayerSeen = DateTime.Now;
		DateTime lastTimeFastMove = DateTime.Now;
		bool kissOrCustomSent = false;
		DateTime kissOrCustomLastSent = DateTime.Now;
		bool previousSaberHolstered = false;

		List<WayPoint> wayPointsToWalk = new List<WayPoint>();
		DateTime wayPointsToWalkLastUpdate = DateTime.Now;

		DateTime lastSaberSwitchCommand = DateTime.Now;

		static readonly PlayerInfo dummyPlayerInfo = new PlayerInfo() {  position = new Vector3() { X= float.PositiveInfinity ,Y= float.PositiveInfinity ,Z= float.PositiveInfinity } };

		private long pendingCalmSays = 0;

		float moveSpeedMultiplier = 1.0f;
		//bool jumpReleasedThisJump = false;
		bool lastFrameWasAir = false;
		DateTime lastTimeInAir = DateTime.Now;
		Vector3 lastFrameMyVelocity = new Vector3();
		int countFramesJumpReleasedThisJump = 0;
		bool findShortestBotPathWalkDistanceNext = false;
		float approximateStrafeJumpAirtimeInSeconds = 225f * 2f / 800f; // 225 is JUMP_VELOCITY, 800 is g_gravity

		int ourLastAttacker = -1;
		int ourLastHitCount = -1;

		bool lastFrameWasJumpCommand = false;

		#region stuckdetection
		class StuckDetector
        {

			Vector3 stuckDetectReferencePos = new Vector3();
			int stuckDetectCounter = 0;
			//float stuckDetectAntiStuckCooldownTime = 3000; // time before waypoints can be interrupted again after stuck detected
			float stuckDetectTimeIncrement = 500;
			float stuckDetectDistanceThreshold = 100;
			int stuckDetectSmallThreshold = 10;
			int stuckDetectBigThreshold = 10;
			DateTime lastStuckDetectCheck = DateTime.Now;
			DateTime lastStuckDetected = DateTime.Now;
			public StuckDetector(/*float stuckDetectAntiStuckCooldownTimeA = 3000,*/ float stuckDetectTimeIncrementA = 500, float stuckDetectDistanceThresholdA = 100, int stuckDetectSmallThresholdA=10, int stuckDetectBigThresholdA=35)
            {
				//stuckDetectAntiStuckCooldownTime = stuckDetectAntiStuckCooldownTimeA;
				stuckDetectTimeIncrement = stuckDetectTimeIncrementA;
				stuckDetectDistanceThreshold = stuckDetectDistanceThresholdA;
				stuckDetectSmallThreshold = stuckDetectSmallThresholdA;
				stuckDetectBigThreshold = stuckDetectBigThresholdA;
			}
			public void stuckDetectReset(Vector3 currentPos)
			{
				stuckDetectCounter = 0;
				lastStuckDetectCheck = DateTime.Now;
				stuckDetectReferencePos = currentPos;
			}
			/*public bool areWeInAntiStuckCooldown()
			{
				return (DateTime.Now - lastStuckDetected).TotalMilliseconds < stuckDetectAntiStuckCooldownTime;
			}*/
			public bool areWeStuck(Vector3 ourPos, ref bool veryStuck)
			{
				veryStuck = stuckDetectCounter > stuckDetectBigThreshold;
				if ((DateTime.Now - lastStuckDetectCheck).TotalMilliseconds > stuckDetectTimeIncrement)
				{
					if ((ourPos - stuckDetectReferencePos).Length() > stuckDetectDistanceThreshold)
					{
						stuckDetectReset(ourPos);
						return false;
					}
					else
					{
						lastStuckDetectCheck = DateTime.Now;
						stuckDetectCounter++;
						if (stuckDetectCounter > stuckDetectSmallThreshold) // basically 5 seconds
						{
							lastStuckDetected = DateTime.Now;
							return true;
						}
						else
						{
							return false;
						}
					}
				}
				else
				{
					return stuckDetectCounter > stuckDetectSmallThreshold;
				}
			}
		}
		/*Vector3 stuckDetectReferencePos = new Vector3();
		int stuckDetectCounter = 0;
		const float stuckDetectAntiStuckCooldownTime = 3000; // time before waypoints can be interrupted again after stuck detected
		const float stuckDetectTimeIncrement = 500;
		const float stuckDetectDistanceThreshold = 100;
		DateTime lastStuckDetectCheck = DateTime.Now;
		DateTime lastStuckDetected = DateTime.Now;
		private void stuckDetectReset(Vector3 currentPos)
        {
			stuckDetectCounter = 0;
			lastStuckDetectCheck = DateTime.Now;
			stuckDetectReferencePos = currentPos;
		}
		private bool areWeInAntiStuckCooldown()
        {
			return (DateTime.Now - lastStuckDetected).TotalMilliseconds < stuckDetectAntiStuckCooldownTime;
		}
		private bool areWeStuck(Vector3 ourPos, ref bool veryStuck)
        {
			veryStuck = stuckDetectCounter > 35;
			if ((DateTime.Now- lastStuckDetectCheck).TotalMilliseconds > stuckDetectTimeIncrement)
            {
                if ((ourPos - stuckDetectReferencePos).Length() > stuckDetectDistanceThreshold)
                {
					stuckDetectReset(ourPos);
					return false;
				}
                else
                {
					lastStuckDetectCheck = DateTime.Now;
					stuckDetectCounter++;
					if(stuckDetectCounter > 10) // basically 5 seconds
                    {
						lastStuckDetected = DateTime.Now;
						return true;
                    }
                    else
                    {
						return false;
                    }
				}
            } else
            {
				return stuckDetectCounter > 10;
            }
        }*/
		readonly StuckDetector smallRangeStuckDetector = new StuckDetector();
		readonly StuckDetector bigRangeStuckDetector = new StuckDetector(500,500,120,240); // Sometimes we get stuck in weird ways where we walk back and forth longer distances. We can't detect this short term because we would get lots of false positives. So instead we must be staying within same 500 unit radius for more than ~1 minute to trigger this one. Not a nice solution, but it will do.
		DateTime lastStuckDetectReset = DateTime.Now;
		bool stuckDetectRandomDirectionSet = false;
		Vector3 veryStuckRandomDirection = new Vector3();
        #endregion

        private unsafe void DoSillyThingsReal(ref UserCommand userCmd, in UserCommand prevCmd, SillyMode sillyMode)
		{

			int myNum = ClientNum.GetValueOrDefault(-1);
			if (myNum < 0 || myNum > infoPool.playerInfo.Length) return;

			PlayerInfo myself = infoPool.playerInfo[myNum];
			PlayerInfo closestPlayer = null;
			float closestDistance = float.PositiveInfinity;
			PlayerInfo closestPlayerAfk = null;
			float closestDistanceAfk = float.PositiveInfinity;
			WayPoint[] closestWayPointPath = null;
			float closestWayPointBotPathDistance = float.PositiveInfinity;
			WayPoint[] closestWayPointPathAfk = null;
			float closestWayPointBotPathAfkDistance = float.PositiveInfinity;
			List<PlayerInfo> grippingPlayers = new List<PlayerInfo>();

			if (ourLastAttacker != lastPlayerState.Persistant[6] || ourLastHitCount != lastPlayerState.Persistant[1])
			{
				// Disable stuck detection during fights
				//stuckDetectReset(myself.position);
				smallRangeStuckDetector.stuckDetectReset(myself.position);
				bigRangeStuckDetector.stuckDetectReset(myself.position);
			}

			if (calmSayQueue.Count > 0 || Interlocked.Read(ref pendingCalmSays) > 0)
			{
				if (calmSayQueue.Count > 0 && lastPlayerState.GroundEntityNum == Common.MaxGEntities - 2 && myself.velocity.Length() == 0 && lastPlayerState.SaberHolstered && lastPlayerState.Stats[0] > 0)
				{
					string sayCommand = null;
					if(calmSayQueue.TryDequeue(out sayCommand))
                    {
						Task sayRequest = leakyBucketRequester.requestExecution(sayCommand, RequestCategory.BOTSAY, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
						if (sayRequest != null)
						{
							Interlocked.Increment(ref pendingCalmSays);
							sayRequest.ContinueWith((a) => { Interlocked.Decrement(ref pendingCalmSays); });
						}
					}
				}
				if (!lastPlayerState.SaberHolstered && (DateTime.Now - lastSaberSwitchCommand).TotalMilliseconds > (lastSnapshot.ping + 100))
				{
					userCmd.GenericCmd = (byte)GenericCommandJK2.SABERSWITCH; // Switch saber off.
				}
				return;
			}

			bool bsModeActive = sillyMode == SillyMode.GRIPKICKDBS && infoPool.gripDbsMode == GripKickDBSMode.SPEEDRAGEBS; // Use BS instead of dbs
			bool speedRageModeActive = infoPool.gripDbsModeOneOf(GripKickDBSMode.SPEEDRAGE, GripKickDBSMode.SPEEDRAGEBS);

			int timeDelta = lastPlayerState.CommandTime - sillyLastCommandTime;
			//Vector3 ourMoveDelta = myself.position - sillyOurLastPosition;
			Vector2 myPosition2D = new Vector2() { X = myself.position.X, Y = myself.position.Y };
			Vector2 ourMoveDelta2D = myPosition2D - sillyOurLastPosition2D;
			float realDeltaSpeed = timeDelta == 0 ? float.PositiveInfinity : ourMoveDelta2D.Length() / ((float)timeDelta / 1000.0f); // Our speed per second

			bool movingVerySlowly = timeDelta > 0 && realDeltaSpeed < (20.0f*moveSpeedMultiplier);
			bool findShortestBotPathWalkDistance = false;
            if (movingVerySlowly)
            {
				if ((DateTime.Now - lastTimeFastMove).TotalSeconds > 30)
				{
					// KMS
					if(infoPool.sillyMode != SillyMode.WALKWAYPOINTS) { 
						leakyBucketRequester.requestExecution("kill", RequestCategory.FIGHTBOT, 0, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
						lastTimeFastMove = DateTime.Now;
						return;
					}
				} else if ((DateTime.Now - lastTimeFastMove).TotalSeconds > 10 && wayPointsToWalk.Count > 0 && myself.groundEntityNum == Common.MaxGEntities - 2)
				{
					findShortestBotPathWalkDistance = true; // Dont clear here, just recalculate. Reason: It can use the current points as exclusion.
					//wayPointsToWalk.Clear();
				} else if (((DateTime.Now - lastTimeFastMove).TotalSeconds > 2 && wayPointsToWalk.Count == 0)|| (DateTime.Now - lastTimeFastMove).TotalSeconds > 4)
				{
					//if(wayPointsToWalk.Count > 0)
                    //{
					//	wayPointsToWalk.RemoveAt(0); // Remove this point for fun, see if it does us any good. 
                   // }
                    //else
					if(myself.groundEntityNum == Common.MaxGEntities - 2)
                    {
						// Ok. Let's go full crazy and find closest walk distance to player and make that our waypoints.
						// We gotta do this rarely or else it will be computationally expensive.
						findShortestBotPathWalkDistance = true;

					}
				}
			} else if (timeDelta > 0)
            {
				lastTimeFastMove = DateTime.Now;
			}


            if (findShortestBotPathWalkDistanceNext)
            {
				findShortestBotPathWalkDistance = true;
				findShortestBotPathWalkDistanceNext = false;
			}
			

			sillyLastCommandTime = lastPlayerState.CommandTime;
			sillyOurLastPosition = myself.position;
			sillyOurLastPosition2D = myPosition2D;

			bool isLightsideMode = infoPool.sillyModeOneOf(SillyMode.ABSORBSPEED,SillyMode.MINDTRICKSPEED);
			bool amGripped = lastPlayerState.PlayerMoveType == JKClient.PlayerMoveType.Float; // Not 100% reliable ig but good enough
			bool amBeingDrained = myself.lastDrainedEvent.HasValue ? (DateTime.Now - myself.lastDrainedEvent.Value).TotalMilliseconds < 400 : false; // Not 100% reliable ig but good enough
			bool weAreChoking = amGripped && lastPlayerState.ForceHandExtend == 5;
			bool grippingSomebody = lastPlayerState.PowerUps[9] != 0 && 0 < (lastPlayerState.forceData.ForcePowersActive & (1 << 6)); // TODO 1.04 and JKA
			bool amInRage = (lastPlayerState.forceData.ForcePowersActive & (1 << 8)) > 0; // TODO 1.04 and JKA
			bool amInSpeed = (lastPlayerState.forceData.ForcePowersActive & (1 << 2)) > 0; // TODO 1.04 and JKA
			bool amInAbsorb = (lastPlayerState.forceData.ForcePowersActive & (1 << 10)) > 0; // TODO 1.04 and JKA
			bool amInMindTrick = (lastPlayerState.forceData.ForcePowersActive & (1 << 5)) > 0; // TODO 1.04 and JKA
			bool amInHeal = (lastPlayerState.forceData.ForcePowersActive & (1 << 0)) > 0; // TODO 1.04 and JKA
			int rageCoolDownTime = lastPlayerState.forceData.ForceRageRecoveryTime - lastSnapshot.ServerTime;
			bool amInRageCoolDown = lastPlayerState.forceData.ForceRageRecoveryTime > lastSnapshot.ServerTime;
			int fullForceRecoveryTime = 5000; // TODO Measure this on the server in particular
			bool canUseNonRageSpeedPowers = !speedRageModeActive || (amInRageCoolDown && rageCoolDownTime > fullForceRecoveryTime); // We are waiting for rage to cool down anyway, may as well use others.


			//int dbsTriggerDistance = bsModeActive ? 64 : 128; //128 is max possible but that results mostly in just jumps without hits as too far away.
			float dbsTriggerDistance = bsModeActive ? infoPool.bsTriggerDistance : infoPool.dbsTriggerDistance; //128 is max possible but that results mostly in just jumps without hits as too far away.
			int maxGripDistance = 256; // 256 is default in jk2
			int maxDrainDistance = 512; // 256 is default in jk2
			int maxPullDistance = 1024; // 256 is default in jk2

			bool blackListedPlayerIsNearby = false;
			bool strongIgnoreNearby = false;

			bool amInAttack = lastPlayerState.SaberMove > 3;


			bool weAreStuck = false;
			bool weAreVeryStuck = false;
			if (!findShortestBotPathWalkDistance && !amInAttack)
			{
				bool tmp = false;
				weAreStuck = smallRangeStuckDetector.areWeStuck(myself.position, ref weAreVeryStuck) || bigRangeStuckDetector.areWeStuck(myself.position, ref tmp);
				weAreVeryStuck = weAreVeryStuck || tmp;
				if (weAreStuck && (DateTime.Now- lastStuckDetectReset).TotalMilliseconds > 4000) // give it 4 seconds before resetting again or we will end up resetting every frame which makes the waypoint discarding concept useless...
				{
                    if (weAreVeryStuck)
                    {
						veryStuckRandomDirection.X = getNiceRandom(-1000, 1000);
						veryStuckRandomDirection.Y = getNiceRandom(-1000, 1000);
						veryStuckRandomDirection = Vector3.Normalize(veryStuckRandomDirection) * 1000.0f;
						stuckDetectRandomDirectionSet = true;
						findShortestBotPathWalkDistance = false;
						wayPointsToWalk.Clear();
					}
                    else
                    {
						findShortestBotPathWalkDistance = true;
						stuckDetectRandomDirectionSet = false;
					}
					lastStuckDetectReset = DateTime.Now;
				}
			}

			WayPoint myClosestWayPoint = findShortestBotPathWalkDistance ? ( this.pathFinder != null ? this.pathFinder.findClosestWayPoint(myself.position,((movingVerySlowly|| weAreStuck) && wayPointsToWalk.Count > 0) ? wayPointsToWalk.GetRange(0,Math.Min(10, wayPointsToWalk.Count)) : null,myself.groundEntityNum == Common.MaxGEntities-2) : null) : null;



			// Find nearest player
			if(infoPool.sillyMode != SillyMode.WALKWAYPOINTS) { 
				foreach (PlayerInfo pi in infoPool.playerInfo)
				{
					//if (infoPool.fightBotTargetingMode == FightBotTargetingMode.NONE) continue;
					if (pi.infoValid && pi.IsAlive && pi.team != Team.Spectator && pi.clientNum != myNum  && !pi.duelInProgress)
					{
						float curdistance = (pi.position - myself.position).Length();
						if (amGripped && (pi.forcePowersActive & (1 << 6)) > 0 && curdistance <= maxGripDistance) // To find who is gripping us
						{
							grippingPlayers.Add(pi);
						}

						if (infoPool.fightBotTargetingMode == FightBotTargetingMode.OPTIN && !pi.chatCommandTrackingStuff.wantsBotFight) continue;
						if (this.currentGameType >= GameType.Team && pi.team == myself.team) continue;

						if (pi.chatCommandTrackingStuff.fightBotBlacklist)
						{
							bool blacklistOverridden = pi.chatCommandTrackingStuff.fightBotBlacklistAllowBrave && pi.chatCommandTrackingStuff.wantsBotFight;
							if (!blacklistOverridden && !_connectionOptions.noBotIgnore && curdistance < 500)
							{
								blackListedPlayerIsNearby = true;
								continue;
							}
						}
						if (pi.chatCommandTrackingStuff.fightBotStrongIgnore && !_connectionOptions.noBotIgnore && curdistance < 300) {
							strongIgnoreNearby = true;
							continue;
						}
						if (pi.chatCommandTrackingStuff.fightBotStrongIgnore && !_connectionOptions.noBotIgnore && curdistance < 100 && amInAttack) { // If a very scared person less than 100 units away and im attacking ... kill myself with high priority.
							if(infoPool.sillyMode != SillyMode.WALKWAYPOINTS) { 
								leakyBucketRequester.requestExecution("kill", RequestCategory.SELFKILL, 5, 300, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
							}
							return;
						}
						if (pi.chatCommandTrackingStuff.fightBotIgnore && !_connectionOptions.noBotIgnore) continue;
						if (grippingSomebody && personImTryingToGrip != pi.clientNum) continue; // This is shit. There must be better way. Basically, wanna get the player we're actually gripping.
																								// Check if player is in the radius of someone with strong ignore
						bool thisPlayerNearStrongIgnore = false;
						foreach (PlayerInfo piSub in infoPool.playerInfo)
						{
							if (piSub.infoValid && piSub.IsAlive && piSub.team != Team.Spectator && piSub.clientNum != myNum && !piSub.duelInProgress)
							{
								bool blacklistOverridden = piSub.chatCommandTrackingStuff.fightBotBlacklistAllowBrave && piSub.chatCommandTrackingStuff.wantsBotFight;
								if ((piSub.chatCommandTrackingStuff.fightBotStrongIgnore || (piSub.chatCommandTrackingStuff.fightBotBlacklist && !blacklistOverridden)) && (pi.position - piSub.position).Length() < 300)
								{
									thisPlayerNearStrongIgnore = true;
									break;
								}
							}
						}
						if (thisPlayerNearStrongIgnore) continue;


						bool playerIsAfk = /*!pi.lastMovementDirChange.HasValue ||*/ (DateTime.Now - pi.lastMovementDirChange).TotalSeconds > 10;

						if (findShortestBotPathWalkDistance && myClosestWayPoint != null && this.pathFinder != null)
						{
							WayPoint hisClosestWayPoint = this.pathFinder.findClosestWayPoint(pi.position, null,pi.groundEntityNum == Common.MaxGEntities-2);
							if(hisClosestWayPoint != null)
							{
								float walkDistance = 0;
								var path = this.pathFinder.getPath(myClosestWayPoint, hisClosestWayPoint,ref walkDistance);
								if (playerIsAfk)
								{
									if (path != null && walkDistance > 0 && walkDistance < closestWayPointBotPathAfkDistance)
									{
										closestWayPointPathAfk = path;
										closestWayPointBotPathAfkDistance = walkDistance;
									}
								} else
								{
									if (path != null && walkDistance > 0 && walkDistance < closestWayPointBotPathDistance)
									{
										closestWayPointPath = path;
										closestWayPointBotPathDistance = walkDistance;
									}
								}
							}
						}

						// We just deprioritize them
						//if (playerIsAfk) continue; // ignore mildly afk players

						if (playerIsAfk)
						{
							if (curdistance < closestDistanceAfk)
							{
								closestDistanceAfk = curdistance;
								closestPlayerAfk = pi;
							}
						} else
						{
							if (curdistance < closestDistance)
							{
								closestDistance = curdistance;
								closestPlayer = pi;
							}
						}
					}
				}
			}

			if (findShortestBotPathWalkDistance || (infoPool.sillyMode == SillyMode.WALKWAYPOINTS && wayPointsToWalk.Count == 0))
            {
                if (infoPool.sillyMode == SillyMode.WALKWAYPOINTS)
                {
					wayPointsToWalk.Clear();
                    lock (infoPool.wayPoints)
                    {
						wayPointsToWalk.AddRange(infoPool.wayPoints);
					}
				}
				else if(closestWayPointPath != null)
                {
					wayPointsToWalk.Clear();
					wayPointsToWalk.AddRange(closestWayPointPath);
					wayPointsToWalkLastUpdate = DateTime.Now;
					closestWayPointPath = null;
                }
                else if(closestWayPointPathAfk != null) // Afk players are only a secondary option
                {
					wayPointsToWalk.Clear();
					wayPointsToWalk.AddRange(closestWayPointPathAfk);
					wayPointsToWalkLastUpdate = DateTime.Now;
					closestWayPointPathAfk = null;
                }
                else if(myClosestWayPoint != null)
				{
					float totalWalkDistance = 0;
					var path = this.pathFinder?.getLongestPathFrom(myClosestWayPoint, ref totalWalkDistance);
					if (path != null)
					{
						wayPointsToWalk.Clear();
						wayPointsToWalk.AddRange(path);
						wayPointsToWalkLastUpdate = DateTime.Now;
						closestWayPointPath = null;
					}
					/*
					// Pick random point to walk to.
					WayPoint randomWayPoint = this.pathFinder?.getRandomWayPoint();
					if(randomWayPoint != null)
                    {
						float totalWalkDistance = 0;
						var path = this.pathFinder?.getPath(myClosestWayPoint, randomWayPoint, ref totalWalkDistance);
						if(path != null)
                        {
							wayPointsToWalk.Clear();
							wayPointsToWalk.AddRange(path);
							wayPointsToWalkLastUpdate = DateTime.Now;
							closestWayPointPath = null;
						}
                    }*/
				}
            }

			if (closestPlayer != null && (DateTime.Now - wayPointsToWalkLastUpdate).TotalSeconds > 2.5 && wayPointsToWalk.Count > 0 && myself.groundEntityNum == Common.MaxGEntities - 2) // It's a bit time limited to prevent too much computational power being wasted 100s of times per second.
			{
				findShortestBotPathWalkDistanceNext = true;
			}

			if (closestPlayer == null)
			{
				if(closestPlayerAfk != null)
                {
					closestPlayer = closestPlayerAfk;
					closestDistance = closestDistanceAfk;
				} else { 
					if((DateTime.Now - lastTimeAnyPlayerSeen).TotalSeconds > 120)
					{
						// KMS
						if(infoPool.sillyMode != SillyMode.WALKWAYPOINTS) { 
							leakyBucketRequester.requestExecution("kill", RequestCategory.FIGHTBOT, 0, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
						}
						lastTimeAnyPlayerSeen = DateTime.Now;//Reset or we will get thrown out here all the time.
					}
					closestPlayer = dummyPlayerInfo;
					closestDistance = (dummyPlayerInfo.position - myself.position).Length();
				}
			} else if (closestDistance < 2000)
            {
				lastTimeAnyPlayerSeen = DateTime.Now;
			}


			bool enemyLikelyOnSamePlane = closestDistance < 300 && Math.Abs(closestPlayer.position.Z - myself.position.Z) < 10.0f && lastPlayerState.GroundEntityNum == Common.MaxGEntities - 2 && closestPlayer.groundEntityNum == Common.MaxGEntities - 2;


			Vector3 myViewHeightPos = myself.position;
			myViewHeightPos.Z += lastPlayerState.ViewHeight;


			int genCmdSaberAttackCycle = jkaMode ? 26 : 20;
			int bsLSMove = jkaMode ? 12 : 12;
			int dbsLSMove = jkaMode ? 13 : 13;
			int parryLower = jkaMode ? 152 : 108;
			int parryUpper = jkaMode ? 156 : 112;


			bool heInAttack = closestPlayer.saberMove > 3;

			//int grippedEntity = lastPlayerState.forceData.ForceGripEntityNum != Common.MaxGEntities - 1 ? lastPlayerState.forceData.ForceGripEntityNum : -1;
			bool drainPossible = closestDistance < maxDrainDistance;//&& grippedEntity == -1;
			bool pullPossibleDistanceWise = closestDistance < maxPullDistance;//&& grippedEntity == -1;
			bool pullPossible = pullPossibleDistanceWise && (heInAttack || (closestPlayer.groundEntityNum == Common.MaxGEntities-1));
			Vector3 viewHeightMoveVector = closestPlayer.position- myViewHeightPos;
			float viewHeightToEnemyDistance = viewHeightMoveVector.Length();
			bool kissPossibleDistanceWise = viewHeightToEnemyDistance < 40.0f || closestDistance < 40.0f;//&& grippedEntity == -1;
			bool kissPossible = kissPossibleDistanceWise && lastPlayerState.Stats[0] > 0 && myself.groundEntityNum == Common.MaxGEntities-2 && closestPlayer.groundEntityNum == Common.MaxGEntities-2;// && lastPlayerState.Stats[0] > 0; Technically, dont do if we're dead but who knows. doing it always might reveal funny game glitches due to forgotten checks
            if (!kissPossible)
            {
				kissOrCustomSent = false;
			}
			bool gripPossibleDistanceWise = viewHeightToEnemyDistance < maxGripDistance;//&& grippedEntity == -1;
			bool gripPossible = gripPossibleDistanceWise && !grippingSomebody;//&& grippedEntity == -1;

			//float mySpeed = this.baseSpeed == 0 ? (myself.speed == 0 ? 250: myself.speed) : this.baseSpeed; // We only walk so walk speed is our speed.
			float mySpeed = myself.speed == 0 ? (this.baseSpeed == 0 ? 250: this.baseSpeed) : myself.speed; // We only walk so walk speed is our speed.
			float hisSpeed = closestPlayer.velocity.Length();
			Vector3 vecToClosestPlayer = closestPlayer.position - myself.position;
			Vector2 vecToClosestPlayer2D = new Vector2() { X=vecToClosestPlayer.X,Y=vecToClosestPlayer.Y};
			Vector2 enemyPosition2D = new Vector2() { X= closestPlayer.position.X,Y= closestPlayer.position.Y};
			Vector2 enemyVelocity2D = new Vector2() { X= closestPlayer.velocity.X,Y= closestPlayer.velocity.Y};
			// Predict where other player is going and how I can get there.
			// The problem is, I can't just predict 1 second into the future.
			// I need to ideally predict the point of intercept, which might be any arbitrary amount of time into the future.
			//
			// Let's first find out if me intercepting is theoretically possible (if he is not moving away from me faster than my speed)
			Vector2 normalizedVecToPlayer2D = Vector2.Normalize(vecToClosestPlayer2D);
			float dotProduct = Vector2.Dot(enemyVelocity2D, vecToClosestPlayer2D);
			Vector3 moveVector = vecToClosestPlayer;
			float distance2D = (enemyPosition2D - myPosition2D).Length();
			
			int knockDownLower = jkaMode ? -2 : 829; // TODO Adapt to 1.04 too? But why, its so different.
			int knockDownUpper = jkaMode ? -2 : 848;

			int hisLegsAnim = closestPlayer.legsAnim & ~2048;
			int hisTorsoAnim = closestPlayer.torsoAnim & ~2048;
			int myLegsAnim = lastPlayerState.LegsAnimation & ~2048;
			int myTorsoAnim = lastPlayerState.TorsoAnim & ~2048;
			bool enemyIsKnockedDown = (hisLegsAnim >= knockDownLower && hisLegsAnim <= knockDownUpper) || (hisTorsoAnim >= knockDownLower && hisTorsoAnim <= knockDownUpper);
			bool meIsDucked = (myLegsAnim >= 697 && myLegsAnim <= 699) || (lastPlayerState.PlayerMoveFlags & 1) > 0; // PMF_DUCKED
			bool heIsDucked = hisLegsAnim >= 697 && hisLegsAnim <= 699; // Bad-ish guess but hopefully ok
			bool meIsInRoll = myLegsAnim >= 781 && myLegsAnim <= 784 && lastPlayerState.LegsTimer > 0; // TODO Make work with JKA and 1.04
			bool heIsInRoll = hisLegsAnim >= 781 && hisLegsAnim <= 784/* && closestPlayer.legsTimer > 0*/; // TODO Make work with JKA and 1.04. Also bad guess but best I can (want to) do rn.
			

			// Bounding boxes of him and me
			float myMax = meIsDucked || meIsInRoll ? 16 : 40;
			float myMin = 24;
			float hisMax = heIsDucked || heIsInRoll ? 16 : 40;
			float hisMin = 24;


			float pingInSeconds = (float)lastSnapshot.ping / 1000.0f;
			float halfPingInSeconds = pingInSeconds / 2.0f;
			Vector3 myPredictedPosition = myself.position + myself.velocity * halfPingInSeconds;
			Vector2 myPredictedPosition2D = new Vector2() { X= myPredictedPosition.X, Y= myPredictedPosition.Y};
			Vector3 myPositionMaybePredicted = infoPool.selfPredict ? myPredictedPosition : myself.position;
			Vector2 myPosition2DMaybePredicted = infoPool.selfPredict ? myPredictedPosition2D : myPosition2D;

			//float verticalDistance = Math.Abs(closestPlayer.position.Z - myself.position.Z);
			//bool dbsPossiblePositionWise = distance2D < dbsTriggerDistance && verticalDistance < 64; // Backslash distance. The vertical distance we gotta do better, take crouch into account etc.

			bool amCurrentlyDbsing = lastPlayerState.SaberMove == dbsLSMove || lastPlayerState.SaberMove == bsLSMove;
			bool amInParry = lastPlayerState.SaberMove >= parryLower && lastPlayerState.SaberMove <= parryUpper;

			bool gripForcePitchUp = false;
			bool gripForcePitchDown = false;
			bool forceKick = false;
			bool releaseGrip = false;
			bool doPull = false;

			bool heIsStandingOnTopOfMe = closestPlayer.groundEntityNum == myself.clientNum ||( vecToClosestPlayer2D.Length() < 15 && closestPlayer.position.Z <= (myself.position.Z + myMax + hisMin + 10.0f) && closestPlayer.position.Z > (myself.position.Z + myMax + hisMin - 1.0f) && closestPlayer.velocity.Z < 5.0f);
			bool dbsPossiblePositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z > (closestPlayer.position.Z - hisMin) && myself.position.Z < (closestPlayer.position.Z + hisMax);
			bool dbsPossible = dbsPossiblePositionWise && !grippingSomebody && !amGripped; // Don't dbs while gripped. Is it even possible?
			bool dbsPossibleWithJumpPositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z < (closestPlayer.position.Z - hisMin) && (myself.position.Z + 96) > (closestPlayer.position.Z - hisMin); // 96 is force level 1 jump height. adapt to different force jump heights?
			bool dbsPossibleWithJump = dbsPossibleWithJumpPositionWise && !grippingSomebody; // Don't dbs while gripped. Is it even possible?

			bool doingGripDefense = false;

			bool hadWayPoints = false;
			float nextSharpTurnOrEnd = float.PositiveInfinity;
			if (wayPointsToWalk.Count > 0) // Do some potential trimming of waypoints.
            {
				if(wayPointsToWalk[0].command != null)
                {
					if (infoPool.sillyMode == SillyMode.WALKWAYPOINTS && _connectionOptions.allowWayPointBotmodeCommands)
                    {
						leakyBucketRequester.requestExecution(wayPointsToWalk[0].command, RequestCategory.FIGHTBOT_WAYPOINTCOMMAND, 3, 500, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
					}
					wayPointsToWalk.RemoveAt(0);
					return;
				}

				bool inAntiStuckCooldown = (DateTime.Now-lastStuckDetectReset).TotalMilliseconds < 3000 && !stuckDetectRandomDirectionSet;
				if ((enemyLikelyOnSamePlane || dbsPossible) && !inAntiStuckCooldown)
				{
					wayPointsToWalk.Clear();
				}

				hadWayPoints = wayPointsToWalk.Count > 0;
				if (wayPointsToWalk.Count > 0 && (wayPointsToWalk[0].origin - myself.position).LengthXY() < 32 && wayPointsToWalk[0].origin.Z<myself.position.Z-64 && lastPlayerState.GroundEntityNum == Common.MaxGEntities-2)
                {
					// Special case: We have reached the waypoint horizontally but we are way above it and we are standing on ground. We likely got lost/stuck.
					wayPointsToWalk.Clear();
				}

				bool weAreOverjumping = false;
				// Remove points that we have already hit
				while (wayPointsToWalk.Count > 0 && (/*(wayPointsToWalk[0].origin - myself.position).Length() > 500.0f ||*/ ((wayPointsToWalk[0].origin - myself.position).LengthXY() < 32 && (wayPointsToWalk[0].origin - myself.position).Length() < 96 && wayPointsToWalk[0].origin.Z < myself.position.Z + 1.0f)))
				{
					// Check if we are going the right angle or overjumping
					if(lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
                    {
						// We are in air. Will we overjump?
						if(wayPointsToWalk.Count == 1)
                        {
							// Don't care then, whatever.
							wayPointsToWalk.RemoveAt(0);
                        }
                        else
                        {
							Vector3 nextWpToNextAfterWpVector = wayPointsToWalk[1].origin - wayPointsToWalk[0].origin;
							if (Math.Abs(AngleSubtract(vectoyaw(myself.velocity), vectoyaw(nextWpToNextAfterWpVector))) > 25)
							{
								weAreOverjumping = true; // TODO: Blue uppercut? hm
								break; // We will overjump, so don't remove this point.
							} else
                            {
								wayPointsToWalk.RemoveAt(0);
							}
						}
                    }
                    else
                    {
						// We are standing, no worries then
						wayPointsToWalk.RemoveAt(0);
					}
				}
				/*while (wayPointsToWalk.Count > 1 && (wayPointsToWalk[1].origin - myself.position).Length() < (wayPointsToWalk[1].origin - wayPointsToWalk[0].origin).Length() && myself.position.DistanceToLine(wayPointsToWalk[1].origin, wayPointsToWalk[0].origin) < 32)
				{
					// Check if we walked past the next point and are on our way to the following point.
					// To make sure, check that on the line from this to next one, we are no more than 32 units removed from the line
					wayPointsToWalk.RemoveAt(0);
				}*/

				// generalization of the above one, and for the future 5 points.
				if (!weAreOverjumping && wayPointsToWalk.Count > 1)
				{
					// Check if we are on the line between some future points
					int highestMatchIndex = 0;
					int maxCount = Math.Min(5, wayPointsToWalk.Count);
					for (int i = 1; i < maxCount; i++)
					{
						Debug.WriteLine($"{wayPointsToWalk.Count},{i}\n");
						float distanceToLastWayPoint = (wayPointsToWalk[i].origin - wayPointsToWalk[i - 1].origin).Length();
						if (myself.position.DistanceToLineXY(wayPointsToWalk[i].origin, wayPointsToWalk[i - 1].origin) < 32 && // Horizontal distance to line < 32
							myself.position.DistanceToLine(wayPointsToWalk[i].origin, wayPointsToWalk[i - 1].origin) < 96 && // Total distance to line < 96 (max level 1 force jump height)
							(/*wayPointsToWalk[i-1].origin.Z < (myself.position.Z + 1.0f) ||*/ wayPointsToWalk[i].origin.Z < (myself.position.Z + 1.0f)) && // previous or current waypoint is below us.
							(wayPointsToWalk[i].origin-myself.position).Length() < distanceToLastWayPoint &&  // These two conditions combined mean we are somewhere between the two points
							(wayPointsToWalk[i-1].origin-myself.position).Length() < distanceToLastWayPoint)
                        {
							highestMatchIndex = i;

						}
					}
					if (highestMatchIndex > 0)
					{
						wayPointsToWalk.RemoveRange(0, highestMatchIndex);
					}
				}

				if (!weAreOverjumping && wayPointsToWalk.Count > 1 && !movingVerySlowly)
				{
					// If some future point is on the same line and we're heading that direction, remove intermediate points
					// Basically: Optimize straight lines for better strafing
                    if (wayPointsToWalk[0].origin.Z < (myself.position.Z + 30.0f) &&
						wayPointsToWalk[0].origin.DistanceToLineXY(myself.position, myself.position + myself.velocity) < 32 && wayPointsToWalk[0].origin.DistanceToLine(myself.position, myself.position + myself.velocity) < 96.0f && // I'm heading towards the next waypoint
						myself.position.DistanceToLineXY(wayPointsToWalk[0].origin, wayPointsToWalk[1].origin) < 32 && myself.position.DistanceToLine(wayPointsToWalk[0].origin, wayPointsToWalk[1].origin) < 96 // Next path segment is pointing at me
						)
                    {
						int index = 2;
                        while (wayPointsToWalk.Count > index && wayPointsToWalk[index].origin.DistanceToLine(wayPointsToWalk[0].origin, wayPointsToWalk[1].origin) < 32)
                        {
							index++;
                        }
						wayPointsToWalk.RemoveRange(0, index-1);
					}
				}

				// Check if next segment is a sharp turn.
				if (wayPointsToWalk.Count > 1)
				{
					Vector3 meToNextWPVector = wayPointsToWalk[0].origin - myself.position;
					Vector3 nextWpToNextAfterWpVector = wayPointsToWalk[1].origin - wayPointsToWalk[0].origin;
                    if (Math.Abs(AngleSubtract(vectoyaw(meToNextWPVector), vectoyaw(nextWpToNextAfterWpVector))) > 25){
						nextSharpTurnOrEnd = (wayPointsToWalk[0].origin - myself.position).LengthXY();
					}

				} else if (wayPointsToWalk.Count == 1)
                {
					nextSharpTurnOrEnd = (wayPointsToWalk[0].origin - myself.position).LengthXY();
				}

				// This is bad: what if we need to walk around a corner and the points on the other side are closer?
				/*if (wayPointsToWalk.Count > 1)
				{
					// Check if a following waypoint among future 3 is closer than current
					int closestIndex = 0;
					float closestWayPointDistance = (wayPointsToWalk[0].origin - myself.position).Length();
					int maxCount = Math.Min(3, wayPointsToWalk.Count);
					for (int i = 1; i < maxCount; i++)
					{
						float distanceHere = (wayPointsToWalk[i].origin - myself.position).Length();
						if (distanceHere < closestWayPointDistance)
						{
							closestWayPointDistance = distanceHere;
							closestIndex = i;
						}
					}
					if (closestIndex > 0)
					{
						wayPointsToWalk.RemoveRange(0, closestIndex);
					}
				}*/
			}
			if(hadWayPoints && wayPointsToWalk.Count == 0 && closestDistance > 500)
            {
				findShortestBotPathWalkDistanceNext = true; // continue walking around
            }

			bool mustJumpToReachWayPoint = false;

			moveSpeedMultiplier = 1.0f;

			bool strafe = true;
			bool amStrafing = false;
			float strafeAngleYawDelta = 0.0f;

			if (stuckDetectRandomDirectionSet && (DateTime.Now - lastStuckDetectReset).TotalMilliseconds < 4000)
			{
                if (strafe && sillyMode != SillyMode.SILLY)
                {
					strafeAngleYawDelta = setUpStrafeAndGetAngleDelta(veryStuckRandomDirection+myself.position, myself, ref moveVector, ref userCmd, in prevCmd, ref amStrafing);
				}
                else
                {
					moveVector = veryStuckRandomDirection;
				}
			}
			else if (!enemyLikelyOnSamePlane && wayPointsToWalk.Count > 0)
            {
                if (strafe && sillyMode != SillyMode.SILLY)
                {
					strafeAngleYawDelta = setUpStrafeAndGetAngleDelta(wayPointsToWalk[0].origin,myself, ref moveVector,ref userCmd, in prevCmd, ref amStrafing);
					/*Vector3 targetDirection = wayPointsToWalk[0].origin - myself.position;
					float targetAngle = vectoyaw(targetDirection);
					float velocityAngle = vectoyaw(myself.velocity);
					float angleDiff = AngleSubtract(targetAngle, velocityAngle);
					bool rightWards = angleDiff >= 0;
					bool bigAngleChange = Math.Abs(angleDiff) > 5;
					moveVector = myself.velocity;
					// Choose strafe method first. 
					if (lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
                    {
                        // In air
                        // Use WD/A scheme
                        if (!rightWards)
                        {
							userCmd.ForwardMove = 127;
							userCmd.RightMove = 127;
                        }
                        else
                        {
							userCmd.ForwardMove = 0;
							userCmd.RightMove = -128;
						}
						(strafeAngleYawDelta,_) = calculateStrafeAngleDelta(in userCmd);

                    } else
                    {
						// On ground
						// Use WD/WA
						if (!rightWards)
						{
							userCmd.ForwardMove = 127;
							userCmd.RightMove = 127;
						}
						else
						{
							userCmd.ForwardMove = 127;
							userCmd.RightMove = -128;
						}
						(strafeAngleYawDelta, _) = calculateStrafeAngleDelta(in userCmd);
					}*/
				}
                else
                {
					moveVector = wayPointsToWalk[0].origin - myself.position;
				}
				if (lastPlayerState.GroundEntityNum!=Common.MaxGEntities-1 && (wayPointsToWalk[0].origin.Z > (myself.position.Z + myMin + 16)) || movingVerySlowly) // Check if we need a jump to get here. Aka if it is higher up than our lowest possible height. Meaning we couldn't duck under it or such. Else it's likely just a staircase. If we do get stuck, do jump.
				{
					if (CheckWaypointJumpDirection(myself.velocity, /*strafeTarget*/moveVector, mySpeed))
                    {
						mustJumpToReachWayPoint = true;
					}
				} else if (lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1 && myself.velocity.Z > 0 && (wayPointsToWalk[0].origin.Z > (myself.position.Z-20.0f)))
				{
					// Already in air. Jump a bit higher than needed to get over potential obstacles.
					mustJumpToReachWayPoint = true;
				}
			}
            else if (amGripped && grippingPlayers.Count > 0 && lastPlayerState.forceData.ForcePower > 0 && !isLightsideMode)
            {
				// TODO exception when gripping myself?
				PlayerInfo guessedGrippingPlayer = grippingPlayers[(int)(sillyflip % grippingPlayers.Count)];
				moveVector = guessedGrippingPlayer.position - myViewHeightPos;
            }
			else if (amCurrentlyDbsing)
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
				moveVector.Z += hisMax - myMax;
			} else if (infoPool.sillyModeOneOf(SillyMode.DBS, SillyMode.GRIPKICKDBS, SillyMode.ABSORBSPEED, SillyMode.MINDTRICKSPEED) && dbsPossible && ((lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1) || (infoPool.fastDbs && lastFrameWasJumpCommand)) )
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
				moveVector.Z += hisMax - myMax;
			}
			else if (!infoPool.sillyModeOneOf(SillyMode.SILLY, SillyMode.LOVER, SillyMode.CUSTOM) && heIsStandingOnTopOfMe && meIsDucked)
			{
				forceKick = true;
			}
			else if (sillyMode == SillyMode.GRIPKICKDBS && gripPossible)
			{
				moveVector = viewHeightMoveVector;
			}
			else if (sillyMode == SillyMode.GRIPKICKDBS && pullPossible && closestDistance < 400 && !gripPossibleDistanceWise && !amGripping && lastPlayerState.forceData.ForcePower >= 40)
			{
				doPull = true;
				//moveVector = closestPlayer.position - myself.position;
			}
			else if (sillyMode == SillyMode.GRIPKICKDBS && grippingSomebody)
			{
				// This has a few stages. First we need to look up until the person is above us. Then we need to look down until he stands on our head.
				if(vecToClosestPlayer2D.Length() >= 15 || closestPlayer.position.Z < (myself.position.Z+myMax+hisMin-1.0f)) // -1.0f to give some leniency to floating point precision?
                {
					gripForcePitchUp = true;
				//} else if (closestPlayer.groundEntityNum != myself.clientNum)
				} else if (closestPlayer.position.Z > (myself.position.Z + myMax + hisMin + 10.0f))
                {
					gripForcePitchDown = true;
				//} else if (!enemyIsKnockedDown)
                //{
				//	gripForceKick = true;
				} else
                {
					releaseGrip = true;
				}
			}
			else if (sillyMode == SillyMode.LOVER && kissPossible)
            {
				moveVector = viewHeightMoveVector;
			}
			else if (sillyMode == SillyMode.CUSTOM && kissPossible)
            {
				moveVector = viewHeightMoveVector;
            }
			else if (dotProduct > (mySpeed-10)) // -10 so we don't end up with infinite intercept routes and weird potential number issues
            {
				// I can never intercept him. He's moving away from me faster than I can move towards him.
				// Do a simplified thing.
				// Just predict his position in 1 second and move there.
				if(strafe && !dbsPossible)
                {
					strafeAngleYawDelta = setUpStrafeAndGetAngleDelta(closestPlayer.position + closestPlayer.velocity, myself, ref moveVector, ref userCmd, in prevCmd, ref amStrafing);
				} else
                {
					moveVector = (closestPlayer.position + closestPlayer.velocity) - myPositionMaybePredicted;
				}
			} else
            {
				Vector2 moveVector2d = new Vector2();
				// Do some fancy shit.
				// Do a shitty iterative approach for now. Improve in future if possible.
				// Remember that the dotProduct is the minimum speed we must have in his direction.
				bool foundSolution = false;
				Vector2 interceptPos = new Vector2();
				for(float interceptTime=0.1f; interceptTime < 10.0f; interceptTime+= 0.1f)
                {
					// Imagine our 1-second reach like a circle. If that circle intersects with his movement line, we can intercept him quickly)
					// If the intersection does not exist, we expand the circle, by giving ourselves more time to intercept.
					Vector2 hisPosThen = enemyPosition2D + enemyVelocity2D * (interceptTime+ halfPingInSeconds);
					interceptPos = hisPosThen + Vector2.Normalize(enemyVelocity2D) * 32.0f; // Give it 100 units extra in that direction for ideal intercept.
					moveVector2d = (interceptPos - myPosition2DMaybePredicted);
					if (moveVector2d.Length() <= mySpeed * interceptTime)
					{
						foundSolution = true;
						moveSpeedMultiplier = moveVector2d.Length() / (mySpeed * interceptTime);
						break;
					}
				}
                if (!foundSolution)
                {
					Vector3 targetPos = closestPlayer.position + closestPlayer.velocity;
                    // Sad. ok just fall back to the usual.
                    if (strafe && !dbsPossible)
                    {
						strafeAngleYawDelta = setUpStrafeAndGetAngleDelta(targetPos, myself, ref moveVector, ref userCmd, in prevCmd, ref amStrafing);
					} else
                    {
						moveVector = targetPos - myPositionMaybePredicted;
                    }
                }
                else
                {
					if(strafe && !dbsPossible)
					{
						strafeAngleYawDelta = setUpStrafeAndGetAngleDelta(new Vector3() { X = interceptPos.X, Y = interceptPos.Y, Z = closestPlayer.position.Z }, myself, ref moveVector, ref userCmd, in prevCmd, ref amStrafing);
					} else
                    {
						moveVector = new Vector3() { X = moveVector2d.X, Y = moveVector2d.Y, Z = moveVector.Z };
					}
                }
            }

			if((DateTime.Now- lastSaberAttackCycleSent).TotalMilliseconds > 1000 && this.saberDrawAnimLevel != 3 && this.saberDrawAnimLevel != -1 && myself.curWeapon == infoPool.saberWeaponNum)
            {
				userCmd.GenericCmd = (byte)genCmdSaberAttackCycle;
            }

			Vector3 angles = new Vector3();
			vectoangles(moveVector, ref angles);
			float yawAngle = angles.Y - this.delta_angles.Y;
			float pitchAngle = angles.X - this.delta_angles.X;
            if (amStrafing)
			{
				pitchAngle = 0 - this.delta_angles.X;
				yawAngle += strafeAngleYawDelta;
            }

            if (mustJumpToReachWayPoint)
            {
				userCmd.Upmove = 127;
            }
			else if (sillyMode == SillyMode.WALKWAYPOINTS)
			{
				// Nothing to do.
				if(userCmd.ForwardMove == 0)
                {
					userCmd.ForwardMove = 127;
				}
			}
			else if (sillyMode == SillyMode.LOVER)
			{

				amGripping = false;
				if (closestDistance < 100 && userCmd.GenericCmd == 0)
                {
					if(!lastPlayerState.SaberHolstered && (DateTime.Now - lastSaberSwitchCommand).TotalMilliseconds > (lastSnapshot.ping + 100))
					{
						userCmd.GenericCmd =  (byte)GenericCommandJK2.SABERSWITCH; // Switch saber off.
						lastSaberSwitchCommand = DateTime.Now;
					} else
                    {
						userCmd.GenericCmd = (byte)0;
					}
				}
				userCmd.ForwardMove = (sbyte)(127* moveSpeedMultiplier);
				if (kissPossible && !(blackListedPlayerIsNearby || strongIgnoreNearby))
                {
					if ((DateTime.Now - kissOrCustomLastSent).TotalSeconds > 10) kissOrCustomSent = false;
					userCmd.ForwardMove = 0;
					if (!kissOrCustomSent)
					{
						leakyBucketRequester.requestExecution(getNiceRandom(0, 5) == 0 ? "amkiss2" : "amkiss", RequestCategory.FIGHTBOT, 3, 500, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
						kissOrCustomSent = true;
						kissOrCustomLastSent = DateTime.Now;
					}
				}
			}
			else if (sillyMode == SillyMode.CUSTOM)
			{

				amGripping = false;
				if (lastPlayerState.SaberHolstered != previousSaberHolstered)
                {
					kissOrCustomSent = false;
                }
				userCmd.ForwardMove = (sbyte)(127 * moveSpeedMultiplier);
				if (kissPossible && !(blackListedPlayerIsNearby || strongIgnoreNearby))
				{
					if ((DateTime.Now - kissOrCustomLastSent).TotalSeconds > 10) kissOrCustomSent = false; // might not be enough for some moves? but it will only trigger when near players so oh well.
					userCmd.ForwardMove = 0;
                    if (!kissOrCustomSent)
					{
						leakyBucketRequester.requestExecution(infoPool.sillyModeCustomCommand, RequestCategory.FIGHTBOT, 3, 500, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
						kissOrCustomSent = true;
						kissOrCustomLastSent = DateTime.Now;
					}
				}
			}
			else if(sillyMode == SillyMode.SILLY) {
				amGripping = false;
				switch (sillyflip % 4) {
					case 0:
						userCmd.ForwardMove = (sbyte)(127 * moveSpeedMultiplier);
						break;
					case 1:
						yawAngle += 90;
						userCmd.RightMove = (sbyte)(127 * moveSpeedMultiplier);
						break;
					case 2:
						yawAngle += 180;
						userCmd.ForwardMove = (sbyte)(-128 * moveSpeedMultiplier);
						break;
					case 3:
						yawAngle += 270;
						userCmd.RightMove = (sbyte)(-128 * moveSpeedMultiplier);
						break;
				}

				if (sillyAttack)
				{
					userCmd.Buttons |= (int)UserCommand.Button.Attack;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			} else if(infoPool.sillyModeOneOf( SillyMode.DBS,SillyMode.GRIPKICKDBS,SillyMode.ABSORBSPEED, SillyMode.MINDTRICKSPEED))
            {

                if (!amStrafing)
                {
					userCmd.ForwardMove = (sbyte)(127 * moveSpeedMultiplier);
				}
				
				// For lightside, this doesn't need to be part of the general if-else statements because we can still do other stuff while activating absorb
				if((amGripped || amBeingDrained || closestDistance < 128+50) && !amInAbsorb && isLightsideMode && lastPlayerState.forceData.ForcePower > 10) // If gripped, drained or within 128 of pulling range... absorb
				{
                    if (sillyAttack)
					{
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_ABSORB;
					}
				}

				if (amGripped && !isLightsideMode && grippingPlayers.Count > 0 && lastPlayerState.forceData.ForcePower > 20)
                {
					amGripping = false;
                    if (sillyAttack)
                    {
						doingGripDefense = true;
						userCmd.GenericCmd = (weAreChoking || blackListedPlayerIsNearby) ? (byte)GenericCommandJK2.FORCE_THROW : (byte)GenericCommandJK2.FORCE_PULL;
					}
                } 
				else if (!amInAttack /*&& !amCurrentlyDbsing*/)
                {
					//if (lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1 && dbsPossible)
					bool amOnGround = lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1;
					if(infoPool.fastDbs && lastFrameWasJumpCommand && !lastFrameWasAir)
                    {
						amOnGround = false;
					}
					if (amOnGround && (dbsPossible || dbsPossibleWithJump) && !bsModeActive)
					{
						// Check if we are moving in the right direction
						// Because once we are in the air, we can't change our direction anymore.

						Vector2 myVelocity2D = new Vector2() { X=myself.velocity.X, Y=myself.velocity.Y};
						Vector2 moveVector2D = new Vector2() { X = moveVector.X, Y = moveVector.Y };
						Vector2 moveVector2DNormalized = Vector2.Normalize(moveVector2D);
						float dot = Vector2.Dot(myVelocity2D, moveVector2DNormalized);
						float myVelocity2DAbs = myVelocity2D.Length();
						float maxSpeed = mySpeed * moveSpeedMultiplier * 1.1f;
						if (dot < maxSpeed && dot > (150*moveSpeedMultiplier) && ((dot > mySpeed * 0.75f && dot > myVelocity2DAbs * 0.95f) || distance2D <= 32 || (closestPlayer.groundEntityNum == Common.MaxGEntities-2 &&(closestPlayer.position.Z > (myself.position.Z + 10.0f))))) // Make sure we are at least 75% in the right direction, or ignore if we are very close to player or if other player is standing on higher ground than us.
                        {
							// Gotta jump
							userCmd.Upmove = 127;
						} else if ((dot < 150 && !amInRageCoolDown && 150 < maxSpeed) || (dot < 110 && amInRageCoolDown && 110 < maxSpeed))
                        {
							// We're slower than a backflip anyway.
							// Do a backflip :). 150 is backflip speed.
							userCmd.ForwardMove = -128;
							yawAngle += 180;
							userCmd.Upmove = 127;
						}

					}
					else if (dbsPossible)
					{
                        // Do dbs
                        if (!bsModeActive) // In bs mode, don't crouch
                        {
							userCmd.Upmove = -128;
						}
						yawAngle += 180;
						//pitchAngle -= 180; // Eh..
						userCmd.ForwardMove = -128;
						if (sillyAttack)
						{
							userCmd.Buttons |= (int)UserCommand.Button.Attack;
							userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
						}
					} else if (sillyMode == SillyMode.GRIPKICKDBS && gripForcePitchUp) // Look up
					{
						userCmd.ForwardMove = 0;
						amGripping = true;
						userCmd.Upmove = -128; // Crouch
						pitchAngle = -89 - this.delta_angles.X; // Not sure if 90 is safe (might cause weird math issues?)
					} else if (sillyMode == SillyMode.GRIPKICKDBS && gripForcePitchDown) // Look down
					{
						userCmd.ForwardMove = 0;
						amGripping = true;
						userCmd.Upmove = -128; // Crouch
						pitchAngle = 89 - this.delta_angles.X; // Not sure if 90 is safe (might cause weird math issues?)
					} else if (!infoPool.sillyModeOneOf(SillyMode.SILLY, SillyMode.LOVER, SillyMode.CUSTOM) && forceKick) // Kick enemy
					{
						userCmd.ForwardMove = 127;
                        if (sillyAttack) // On off quickly
                        {
							userCmd.Upmove = 127;
						} else
                        {
							userCmd.Upmove = -128; // Crouch
						}
						amGripping = false;
					} else if (sillyMode == SillyMode.GRIPKICKDBS && releaseGrip) // Release him and then we can dbs
					{
						userCmd.ForwardMove = 127;
                        if (sillyAttack) // On off quickly
                        {
							userCmd.Upmove = 127; // Jump diretly? idk
						}
						amGripping = false;
					}
					else if (!infoPool.sillyModeOneOf(SillyMode.SILLY,SillyMode.LOVER,SillyMode.CUSTOM) && heIsStandingOnTopOfMe && !meIsDucked) // Release him and then we can dbs
					{
						userCmd.ForwardMove = 0;
						userCmd.Upmove = -128;
						amGripping = false;
					}
					else if(sillyMode == SillyMode.GRIPKICKDBS && gripPossible)
					{
						personImTryingToGrip = closestPlayer.clientNum;

						Int64 myClientNums = serverWindow.getJKWatcherClientNumsBitMask();
						bool someoneIsInTheWay = false;
						// Check if someone is in the way.
						// Just a safety mechanism to make sure we don't grab innocents
						foreach (PlayerInfo pi in infoPool.playerInfo)
                        {
                            if (pi.infoValid && pi.team != Team.Spectator && pi.clientNum != closestPlayer.clientNum && (0==(myClientNums & (1L<<pi.clientNum)))
								&& (
								(infoPool.fightBotTargetingMode == FightBotTargetingMode.OPTIN && !pi.chatCommandTrackingStuff.wantsBotFight)
								|| pi.chatCommandTrackingStuff.fightBotIgnore // TODO make generalized "mayIAttackPerson" function
								|| (pi.chatCommandTrackingStuff.fightBotBlacklist && !(pi.chatCommandTrackingStuff.fightBotBlacklistAllowBrave && pi.chatCommandTrackingStuff.wantsBotFight)))
								)
                            {
								float myDistanceToTargetedPlayer = (myViewHeightPos - closestPlayer.position).Length();
								if (pi.position.DistanceToLine(myViewHeightPos,closestPlayer.position) < 64
									&& (pi.position- myViewHeightPos).Length() < myDistanceToTargetedPlayer
									&& (pi.position- closestPlayer.position).Length() < myDistanceToTargetedPlayer
									)
                                {
									someoneIsInTheWay = true;
								}
                            }
                        }
                        if (!someoneIsInTheWay) 
                        {
							amGripping = true;
						}

					} else if(sillyMode == SillyMode.GRIPKICKDBS && doPull)
					{
						personImTryingToGrip = closestPlayer.clientNum;
						amGripping = false;
						if(canUseNonRageSpeedPowers) userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_PULL;

					} else
					{
						amGripping = false;
					}
				}
				else if(amCurrentlyDbsing) 
				{
					float serverFrameDuration = 1000.0f/ (float)this.SnapStatus.TotalSnaps;
					float maxRotationPerMillisecond = 160.0f / serverFrameDuration; // -20 to have a slow alternation of the angle to cover all sides. must be under 180 i think to be reasonable. 180 would just have 2 directions forever in theory.
					float maxVertRotationPerMillisecond = 3.5f / serverFrameDuration; // -20 to have a slow alternation of the angle to cover all sides. must be under 180 i think to be reasonable. 180 would just have 2 directions forever in theory.
					float thisCommandDuration = userCmd.ServerTime - lastUserCmdTime;
					float rotationBy = thisCommandDuration * maxRotationPerMillisecond + dbsLastRotationOffset;
					float vertRotationBy = thisCommandDuration * maxVertRotationPerMillisecond + dbsLastVertRotationOffset;

					vertRotationBy = vertRotationBy % 20.0f; // just a +- 10 overall

					yawAngle += rotationBy;
					pitchAngle += vertRotationBy - 10.0f + 50.0f; // +50 to look more down

					//pitchAngle = Math.Clamp(pitchAngle, -89 -this.delta_angles.X, 89 - this.delta_angles.X);

					dbsLastRotationOffset = rotationBy;
					dbsLastVertRotationOffset = vertRotationBy;
					amGripping = false;
				} else
				{
					amGripping = false;
					switch (sillyflip %4)
					{
						case 0:
							userCmd.ForwardMove = (sbyte)(127 * moveSpeedMultiplier);
							break;
						case 1:
							yawAngle += 90;
							userCmd.RightMove = (sbyte)(127 * moveSpeedMultiplier);
							break;
						case 2:
							yawAngle += 180;
							userCmd.ForwardMove = (sbyte)(-128 * moveSpeedMultiplier);
							break;
						case 3:
							yawAngle += 270;
							userCmd.RightMove = (sbyte)(-128 * moveSpeedMultiplier);
							break;
					}
					if (amInParry)
					{
						if (sillyAttack)
						{
							userCmd.Buttons |= (int)UserCommand.Button.Attack;
							userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
						}
					}
				}
				
			}
			
			// Light side stuff
			if (sillyMode == SillyMode.MINDTRICKSPEED)
			{
				if (userCmd.GenericCmd == 0) // Other stuff has priority.
				{
					if (!amInSpeed && lastPlayerState.forceData.ForcePower >= 50 && closestDistance < 700)
					{
						// Go Speed
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;
					}
					else if (amInSpeed && (lastPlayerState.forceData.ForcePower < 25 && !amInMindTrick || closestDistance > 700))
					{
						// Disable speed unless in rage
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;
					} else if (!amInMindTrick && lastPlayerState.forceData.ForcePower > 0 && closestDistance < 700)
                    {
						// Enable mindtrick
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_DISTRACT;
					}  else if (amInMindTrick && !amInSpeed && closestDistance > 700)
                    {
						// Enable mindtrick
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_DISTRACT;
					} 
				}
			}
			// Conservative speed usage.
			else if (sillyMode == SillyMode.ABSORBSPEED)
			{
				if (userCmd.GenericCmd == 0) // Other stuff has priority.
				{
					if (!amInSpeed && lastPlayerState.forceData.ForcePower == 100 && closestDistance < 700)
					{
						// Go Speed
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;
					}
					else if (amInSpeed && (lastPlayerState.forceData.ForcePower < 25 && !amInAbsorb || closestDistance > 700))
					{
						// Disable speed unless in absorb
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;
					} else if (!amInSpeed && closestDistance > 500 && amInAbsorb)
                    {
						// Disable absorb
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_ABSORB;
					} 
				}
			}
			// Conservative speed usage.
			else if ((sillyMode == SillyMode.GRIPKICKDBS && infoPool.gripDbsMode == GripKickDBSMode.SPEED) || sillyMode == SillyMode.LOVER)
            {
				if (userCmd.GenericCmd == 0) // Other stuff has priority.
				{
					if (!amInRage && lastPlayerState.Stats[0] > 1 && lastPlayerState.Stats[0] < 50 && closestDistance < 500)
					{
						// Go rage
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_RAGE;
					}
					else if (amInRage && (closestDistance > 500 || lastPlayerState.Stats[0] > 50))
					{
						// Disable rage if nobody within 500 units
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_RAGE;
					}
					else if (!amInSpeed && lastPlayerState.forceData.ForcePower == 100 && closestDistance < 700)
					{
						// Go Speed
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;
					}
					else if (amInSpeed && (lastPlayerState.forceData.ForcePower < 25 && !amInRage || closestDistance > 700))
					{
						// Disable speed unless in rage
						userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED;

					}
				}
			}
			// Hardcore rage+speed mode.
			else if(sillyMode == SillyMode.GRIPKICKDBS && speedRageModeActive)
            {
				if (userCmd.GenericCmd == 0) // Other stuff has priority.
				{
                    if (amInSpeed != amInRage && lastPlayerState.forceData.ForcePower < 50)
                    {
                        // One of them is active but the other isn't and we can't activate it, so deactivate the first.
                        if (amInRage)
                        {
							userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_RAGE; // Deactivate rage
						}
                        else
                        {
							userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED; // Deactivate speed
						}
                    }
					// General conditions for trying to go back in ragespeed mode
					else if(!amInRageCoolDown && enemyLikelyOnSamePlane)
                    {
						if(!amInSpeed && !amInRage && lastPlayerState.forceData.ForcePower == 100)
                        {
							// Start with speed.
							userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED; // activate speed
						} else if(amInRage != amInSpeed && lastPlayerState.forceData.ForcePower >= 50) // We already checked above but whatever 
                        {
							if (!amInRage)
							{
								userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_RAGE; // activate rage
							}
							else
							{
								userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_SPEED; // activate speed
							}
						}
                    }
				}
			}

			if((DateTime.Now - lastNavigationJump).TotalSeconds > 5 && userCmd.Upmove == 0 && lastPlayerState.GroundEntityNum == Common.MaxGEntities -2 && closestDistance > 400 && !enemyLikelyOnSamePlane && movingVerySlowly)
            {
				// TODO: Also do high jumps. 
				userCmd.Upmove = 127; // Just do a lil jump to get over obstacles
				userCmd.ForwardMove = 0;
				userCmd.RightMove = 0;
				lastNavigationJump = DateTime.Now;
			}



			if (amGripping)
			{
				if (canUseNonRageSpeedPowers)
				{
					userCmd.Buttons |= (int)UserCommand.Button.ForceGripJK2;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			} else if (lastPlayerState.Stats[0] < 100 && drainPossible && lastPlayerState.forceData.ForcePower > 0 && !amInAttack && !dbsPossible && !movingVerySlowly)
			{
                if (canUseNonRageSpeedPowers && !isLightsideMode)
                {
					userCmd.Buttons |= (int)UserCommand.Button.ForceDrainJK2;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				} else if (isLightsideMode && userCmd.GenericCmd == 0)
                {
					userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_HEAL;
                }
			}

			if (lastPlayerState.Stats[0] <= 0) // Respawn no matter what if dead.
            {
				wayPointsToWalk.Clear(); // When dead, always clear waypoints.
				if (sillyAttack)
				{
					userCmd.Buttons |= (int)UserCommand.Button.Attack;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			} else if (blackListedPlayerIsNearby || strongIgnoreNearby) // If near blacklisted player, don't do anything that could cause damage
            {
				userCmd.Buttons = 0;
				if(!lastPlayerState.SaberHolstered && (DateTime.Now - lastSaberSwitchCommand).TotalMilliseconds > (lastSnapshot.ping + 100))
				{
					userCmd.GenericCmd = (byte)GenericCommandJK2.SABERSWITCH; // Switch saber off.
					lastSaberSwitchCommand = DateTime.Now;
				} else
				{
					userCmd.GenericCmd = (byte)0; // Switch saber off.
				}
                if (doingGripDefense) // Allow grip defense.
                {
					userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_THROW;
				}
				userCmd.Upmove = userCmd.Upmove > 0 ? (sbyte)0 : userCmd.Upmove;
			} else if (userCmd.GenericCmd == 0 && lastPlayerState.SaberHolstered && ((sillyMode != SillyMode.CUSTOM && sillyMode != SillyMode.LOVER) || (closestDistance>200 && sillyMode == SillyMode.LOVER)))
            {
				if((DateTime.Now - lastSaberSwitchCommand).TotalMilliseconds > (lastSnapshot.ping + 100))
                {
					userCmd.GenericCmd = (byte)GenericCommandJK2.SABERSWITCH; // switch it back on.
					lastSaberSwitchCommand = DateTime.Now;
				} 

			}

            if (amStrafing)
            {
				bool wouldOverjump = false;
				if(Vector3.Dot(Vector3.Normalize(strafeTarget-myself.position), approximateStrafeJumpAirtimeInSeconds * myself.velocity) > nextSharpTurnOrEnd + 32)
                {
					wouldOverjump = true;
				} 
				//if((approximateStrafeJumpAirtimeInSeconds*myself.velocity).Length() > nextSharpTurnOrEnd + 32)
                //{
					// Would overjump
					//wouldOverjump = true;
				//}
				bool canShouldJump = !wouldOverjump && CheckStrafeJumpDirection(myself.velocity, /*strafeTarget*/moveVector, mySpeed);
				if (lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
				{
					// In air
					if (!lastFrameWasAir) countFramesJumpReleasedThisJump = 0;
					if (myself.velocity.Z > 0 && lastFrameMyVelocity.Z <= 0) countFramesJumpReleasedThisJump = 0;

					if (userCmd.Upmove == 0)
                    {
						if (countFramesJumpReleasedThisJump < 2) // Gotta interrupt the initial sstage of the jump, but then hit jump again so it hits when we land
						{
							userCmd.Upmove = 0;
							countFramesJumpReleasedThisJump++;
						}
						else if(canShouldJump)
						{
							userCmd.Upmove = 127;
						}
					} else if(userCmd.Upmove < 0)
                    {
						//jumpReleasedThisJump = true;
						countFramesJumpReleasedThisJump++;
					}
					lastTimeInAir = DateTime.Now;
				}
				else
				{
					if(canShouldJump && userCmd.Upmove == 0 /*&& (DateTime.Now - lastTimeInAir).TotalMilliseconds > (lastSnapshot.ping+100)*/) // Stuff can fuck up sometimes with detecting that we jumped
                    {
						userCmd.Upmove = 127;
					}
					countFramesJumpReleasedThisJump = 0;
				}
			}

			userCmd.Weapon = (byte)infoPool.saberWeaponNum;

			userCmd.Angles[YAW] = Angle2Short(yawAngle);
			userCmd.Angles[PITCH] = Angle2Short(pitchAngle);

			lastFrameWasJumpCommand = userCmd.Upmove > 0;
			sillyAttack = !sillyAttack;
			//sillyflip++;
			sillyflip+= (lastSnapNum - sillyMoveLastSnapNum);
			//sillyflip = sillyflip % 4;
			sillyMoveLastSnapNum = lastSnapNum;
			lastUserCmdTime = userCmd.ServerTime;
			previousSaberHolstered = lastPlayerState.SaberHolstered;
			lastFrameWasAir = lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1;
			lastFrameMyVelocity = myself.velocity;
			ourLastAttacker = lastPlayerState.Persistant[6];
			ourLastHitCount = lastPlayerState.Persistant[1];
		}


		const int PITCH = 0;
		const int YAW = 1;
		const int ROLL = 2;

		private int Angle2Short(float x)
		{
			return (int)(x * 65536.0f / 360.0f) & 65535;
		}
		private float Short2Angle(int x)
		{
			return (x) * (360.0f / 65536);
		}

		float vectoyaw(float[] vec)
		{
			float yaw;

			if (vec[YAW] == 0 && vec[PITCH] == 0)
			{
				yaw = 0;
			}
			else
			{
				if (vec[PITCH] != 0)
				{
					yaw = (float)(Math.Atan2(vec[YAW], vec[PITCH]) * 180 / Math.PI);
				}
				else if (vec[YAW] > 0)
				{
					yaw = 90;
				}
				else
				{
					yaw = 270;
				}
				if (yaw < 0)
				{
					yaw += 360;
				}
			}

			return yaw;
		}
		float vectoyaw(Vector3 vec)
		{
			float yaw;

			if (vec.Y == 0 && vec.X == 0)
			{
				yaw = 0;
			}
			else
			{
				if (vec.X != 0)
				{
					yaw = (float)(Math.Atan2(vec.Y, vec.X) * 180 / Math.PI);
				}
				else if (vec.Y > 0)
				{
					yaw = 90;
				}
				else
				{
					yaw = 270;
				}
				if (yaw < 0)
				{
					yaw += 360;
				}
			}

			return yaw;
		}

		void vectoangles(Vector3 value1, ref Vector3 angles)
		{
			float forward;
			float yaw, pitch;

			if (value1.Y == 0 && value1.X == 0)
			{
				yaw = 0;
				if (value1.Z > 0)
				{
					pitch = 90;
				}
				else
				{
					pitch = 270;
				}
			}
			else
			{
				if (value1.X != 0)
				{
					yaw = (float)(Math.Atan2(value1.Y, value1.X) * 180 / Math.PI);
				}
				else if (value1.Y > 0)
				{
					yaw = 90;
				}
				else
				{
					yaw = 270;
				}
				if (yaw < 0)
				{
					yaw += 360;
				}

				forward = (float)Math.Sqrt(value1.X * value1.X + value1.Y * value1.Y);
				pitch = (float)(Math.Atan2(value1.Z, forward) * 180 / Math.PI);
				if (pitch < 0)
				{
					pitch += 360;
				}
			}


			angles.X = -pitch;
			angles.Y = yaw;
			angles.Z = 0;
		}

		public static void AngleVectors( Vector3 angles, out Vector3 forward, out Vector3 right, out Vector3 up) {
			float angle;
			float sr, sp, sy, cr, cp, cy;
			// static to help MS compiler fp bugs

			angle = angles.Y * ((float)Math.PI*2f / 360f);
			sy = (float)Math.Sin(angle);
			cy = (float)Math.Cos(angle);
			angle = angles.X * ((float)Math.PI * 2f / 360f);
			sp = (float)Math.Sin(angle);
			cp = (float)Math.Cos(angle);
			angle = angles.Z * ((float)Math.PI * 2f / 360f);
			sr = (float)Math.Sin(angle);
			cr = (float)Math.Cos(angle);

			forward.X = cp* cy;
			forward.Y = cp* sy;
			forward.Z = -sp;
			right.X = (-1* sr* sp* cy+-1* cr*-sy);
			right.Y = (-1* sr* sp* sy+-1* cr* cy);
			right.Z = -1* sr* cp;
			up.X = (cr * sp * cy + -sr * -sy);
			up.Y = (cr * sp * sy + -sr * cy);
			up.Z = cr * cp;
		}


		// UserCommand input is for the movement dirs.
		// 2 float values are angle diff values. for WA/WD both are same. for A/D its backwards or forwards. for W its 2 variations (left/ right?)
		unsafe (float,float) calculateStrafeAngleDelta(in UserCommand cmd,int frametimeMs)
		{ // Handles entitystate and playerstate

			//int movementDir;
			int groundEntityNum;
			float baseSpeed;
			//movementDir = lastPlayerState.MovementDirection;
			groundEntityNum = lastPlayerState.GroundEntityNum;
			baseSpeed = lastPlayerState.Speed == 0 ? (lastPlayerState.Basespeed == 0 ? 250 : lastPlayerState.Basespeed) : lastPlayerState.Speed;

			//Vector3 viewAngles = new Vector3() { X=lastPlayerState.ViewAngles[0] ,Y= lastPlayerState.ViewAngles[1] ,Z= lastPlayerState.ViewAngles[2] };
			Vector3 velocity = new Vector3() { X = lastPlayerState.Velocity[0], Y = lastPlayerState.Velocity[1], Z = lastPlayerState.Velocity[2] };
			//Vector3 viewAngles = velocity;
			/*else if constexpr(std::is_same < T, entityState_t >::value) {
				movementDir = ((entityState_t*)state)->angles2.Y;
				groundEntityNum = ((entityState_t*)state)->groundEntityNum;
				baseSpeed = ((entityState_t*)state)->speed;
				VectorCopy(((entityState_t*)state)->apos.trBase, viewAngles);
				VectorCopy(((entityState_t*)state)->pos.trDelta, velocity);
			}*/

			float pmAccel = 10.0f, pmAirAccel = 1.0f, pmFriction = 6.0f, frametime, optimalDeltaAngle;

			float currentSpeed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);

			/*UserCommand cmd = new UserCommand();

			switch (movementDir)
			{
				case 0: // W
					cmd.ForwardMove = 1; break;
				case 1: // WA
					cmd.ForwardMove = 1; cmd.RightMove = -1; break;
				case 2: // A
					cmd.RightMove = -1; break;
				case 3: // AS
					cmd.RightMove = -1; cmd.ForwardMove = -1; break;
				case 4: // S
					cmd.ForwardMove = -1; break;
				case 5: // SD
					cmd.ForwardMove = -1; cmd.RightMove = 1; break;
				case 6: // D
					cmd.RightMove = 1; break;
				case 7: // DW
					cmd.RightMove = 1; cmd.ForwardMove = 1; break;
				default:
					break;
			}*/

			//bool onGround = groundEntityNum == Common.MaxGEntities-2; //sadly predictedPlayerState makes it jerky so need to use cg.snap groundentityNum, and check for cg.snap earlier
			bool onGround = groundEntityNum != Common.MaxGEntities-1; //sadly predictedPlayerState makes it jerky so need to use cg.snap groundentityNum, and check for cg.snap earlier

			if (currentSpeed < (baseSpeed - 1))
			{ // Dunno why
				return (float.PositiveInfinity, float.PositiveInfinity);
			}

			//frametime = 0.001f;// / 142.0f; // Not sure what else I can do with a demo... TODO? 0.001 prevents invalid nan(ind) coming from acos when onground sometimes?
			//frametime = 0.007f;// / 142.0f; // Not sure what else I can do with a demo... TODO? 0.001 prevents invalid nan(ind) coming from acos when onground sometimes?
			//frametime = 0.07f;// / 142.0f; // Not sure what else I can do with a demo... TODO? 0.001 prevents invalid nan(ind) coming from acos when onground sometimes?
			frametime = (float)frametimeMs/1000.0f;
			/*if (cg_strafeHelper_FPS.value < 1)
				frametime = ((float)cg.frametime * 0.001f);
			else if (cg_strafeHelper_FPS.value > 1000) // invalid
				frametime = 1;
			else frametime = 1 / cg_strafeHelper_FPS.value;*/

			if (onGround)//On ground
				optimalDeltaAngle = (float)Math.Acos(((baseSpeed - (pmAccel * baseSpeed * frametime)) / (currentSpeed * (1 - pmFriction * (frametime))))) * (180.0f / (float)Math.PI) - 45.0f;
			else
				optimalDeltaAngle = (float)Math.Acos(((baseSpeed - (pmAirAccel * baseSpeed * frametime)) / currentSpeed)) * (180.0f / (float) Math.PI) - 45.0f;

			if (float.IsNaN(optimalDeltaAngle))
			{ // It happens sometimes I guess...
				return (float.PositiveInfinity, float.PositiveInfinity);
			}

			if (optimalDeltaAngle < 0 || optimalDeltaAngle > 360)
				optimalDeltaAngle = 0;

			//Vector3 velocityAngle = new Vector3();
			velocity.Z = 0;
			//vectoangles(velocity, ref velocityAngle); //We have the offset from our Velocity angle that we should be aiming at, so now we need to get our velocity angle.

			//float cg_strafeHelperOffset = 75; // dunno why or what huh
			float cg_strafeHelperOffset = 150; // dunno why or what huh

			float diff = float.PositiveInfinity;
			float diff2 = float.PositiveInfinity;
			bool anyActive = false;
			//float smallestAngleDiff = float.PositiveInfinity;
			if (cmd.ForwardMove > 0 && cmd.RightMove < 0)
			{
				diff = diff2 = (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f)); // WA
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
				anyActive = true;
			}
			if (cmd.ForwardMove > 0 && cmd.RightMove > 0)
			{
				diff = diff2 = (-optimalDeltaAngle - (cg_strafeHelperOffset * 0.01f)); // WD
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
				anyActive = true;
			}
			if (cmd.ForwardMove == 0 && cmd.RightMove < 0)
			{
				anyActive = true;
				// Forwards
				diff = -(45.0f - (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // A
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
				// Backwards
				diff2 = (225.0f - (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // A
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
			}
			if (cmd.ForwardMove == 0 && cmd.RightMove > 0)
			{
				anyActive = true;
				// Forwards
				diff = (45.0f - (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // D
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
				// Backwards
				diff2 = (135.0f + (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // D
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
			}

			if (cmd.ForwardMove > 0 && cmd.RightMove == 0)
			{
				anyActive = true;
				// Variation 1
				diff = (45.0f + (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // W
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
				// Variation 2
				diff2 = (-45.0f - (optimalDeltaAngle + (cg_strafeHelperOffset * 0.01f))); // W
				//smallestAngleDiff = Math.Min(smallestAngleDiff, Math.Abs(AngleSubtract(viewAngles.Y, velocityAngle.Y + diff)));
			}


			if (!anyActive)
			{
				return (float.PositiveInfinity, float.PositiveInfinity); ;
			}
			else
			{
				return (diff,diff2);
			}



		}
		float AngleSubtract(float a1, float a2)
		{
			float a;

			a = a1 - a2;

			// Improved variant. Same results for most values but it's more correct for extremely high values (and more performant as well I guess)
			// The reason I do this: Some demos end up having (or being read as having) nonsensically high  float values.
			// This results in the old code entering an endless loop because subtracting 360 no longer does  anything  to float  values that are that  high.
			//a = fmodf(a, 360.0f);
			a = a % 360.0f;
			if (a > 180)
			{
				a -= 360;
			}
			if (a < -180)
			{
				a += 360;
			}
			return a;
		}

		bool lastStrafeAngleWasRightward = false;

		Vector3 strafeTarget = new Vector3();

		float setUpStrafeAndGetAngleDelta(Vector3 targetPoint, PlayerInfo myself, ref Vector3 moveVector, ref UserCommand userCmd, in UserCommand prevCmd, ref bool amStrafing, bool secondaryStrafeOption = false)
		{
			float pingInSeconds = (float)lastSnapshot.ping / 1000.0f;
			float halfPingInSeconds = pingInSeconds/2.0f;
			Vector3 myPredictedPosition = myself.position + myself.velocity * halfPingInSeconds;
			Vector3 myPositionMaybePredicted = infoPool.selfPredict ? myPredictedPosition : myself.position;

			strafeTarget = targetPoint;
			Vector3 targetDirection = targetPoint - myPositionMaybePredicted;
			float targetAngle = vectoyaw(targetDirection);
			float velocityAngle = vectoyaw(myself.velocity);
			float angleDiff = AngleSubtract(targetAngle, velocityAngle);
			bool rightWards = angleDiff > 0;
			float maxAllowedAngleDiff = Math.Min(70f, (float)Math.Pow(targetDirection.Length(),0.6f)*0.5f);
			bool directionChangeThresholdReached = Math.Abs(angleDiff) > Math.Min(maxAllowedAngleDiff*0.5f,5);
			bool bigAngleChange = Math.Abs(angleDiff) > maxAllowedAngleDiff;//> 40;
			if (bigAngleChange || myself.velocity.LengthXY() < 50)
			{
				amStrafing = false;
				moveVector = targetPoint - myPositionMaybePredicted;
				return 0;
			}

			amStrafing = true;

			moveVector = myself.velocity;
			// Choose strafe method first. 
			if (lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
			{
				// In air
				// Use WD/A scheme
				if (!rightWards && directionChangeThresholdReached || lastStrafeAngleWasRightward && !directionChangeThresholdReached)
				{
					userCmd.ForwardMove = 127;
					userCmd.RightMove = 127;
					lastStrafeAngleWasRightward = true;
				}
				else //if(rightWards && directionChangeThresholdReached || !lastStrafeAngleWasRightward)
				{
					userCmd.ForwardMove = 0;
					userCmd.RightMove = -128;
					lastStrafeAngleWasRightward = false;
				}
				(float delta1, float delta2) = calculateStrafeAngleDelta(in userCmd,userCmd.ServerTime-prevCmd.ServerTime);
                if (float.IsInfinity(delta1) || float.IsInfinity(delta2)){
					amStrafing = false;
					return 0;
                }
				return secondaryStrafeOption ? delta2 : delta1;

			}
			else
			{
				// On ground
				// Use WD/WA
				if (!rightWards && directionChangeThresholdReached || lastStrafeAngleWasRightward && !directionChangeThresholdReached)
				{
					userCmd.ForwardMove = 127;
					userCmd.RightMove = 127;
					lastStrafeAngleWasRightward = true;
				}
				else
				{
					userCmd.ForwardMove = 127;
					userCmd.RightMove = -128;
					lastStrafeAngleWasRightward = false;
				}
				(float delta1, float delta2) = calculateStrafeAngleDelta(in userCmd, userCmd.ServerTime - prevCmd.ServerTime);
				if (float.IsInfinity(delta1) || float.IsInfinity(delta2))
				{
					amStrafing = false;
					return 0;
				}
				return secondaryStrafeOption ? delta2 : delta1;
			}
		}

		// MySpeed: means the ps.speed
		// Comments are outdated as this is kinda copypasted from somewhere else
		bool CheckStrafeJumpDirection(Vector3 myVelocity, Vector3 targetVector, float mySpeed/*, bool amInRageCoolDown, ref bool backflip*/)
        {
			Vector2 myVelocity2D = new Vector2() { X = myVelocity.X, Y = myVelocity.Y };
			Vector2 moveVector2D = new Vector2() { X = targetVector.X, Y = targetVector.Y };
			Vector2 moveVector2DNormalized = Vector2.Normalize(moveVector2D);
			float dot = Vector2.Dot(myVelocity2D, moveVector2DNormalized);
			float myVelocity2DAbs = myVelocity2D.Length();
			//float maxSpeed = mySpeed * moveSpeedMultiplier * 1.1f;
			if (/*dot < maxSpeed &&*/ ((dot > mySpeed * 0.95f && dot > myVelocity2DAbs * 0.85f) )) // Make sure we are at least 85% in the right direction, or ignore if we are very close to player or if other player is standing on higher ground than us.
			{
				// Can jump
				//backflip = false;
				return true;
			}
			/*else if ((dot < 150 && !amInRageCoolDown && 150 < maxSpeed) || (dot < 110 && amInRageCoolDown && 110 < maxSpeed))
			{
				// We're slower than a backflip anyway.
				// Do a backflip :). 150 is backflip speed.
				backflip = true;
				return true;
			}*/
			return false;
		}
		
		bool CheckWaypointJumpDirection(Vector3 myVelocity, Vector3 targetVector, float mySpeed/*, bool amInRageCoolDown, ref bool backflip*/)
        {
			Vector2 myVelocity2D = new Vector2() { X = myVelocity.X, Y = myVelocity.Y };
			Vector2 moveVector2D = new Vector2() { X = targetVector.X, Y = targetVector.Y };
			Vector2 moveVector2DNormalized = Vector2.Normalize(moveVector2D);
			float dot = Vector2.Dot(myVelocity2D, moveVector2DNormalized);
			float myVelocity2DAbs = myVelocity2D.Length();
			//float maxSpeed = mySpeed * moveSpeedMultiplier * 1.1f;
			if (/*dot < maxSpeed &&*/ ((dot > mySpeed * 0.95f && dot > myVelocity2DAbs * 0.85f) )) // Make sure we are at least 85% in the right direction, or ignore if we are very close to player or if other player is standing on higher ground than us.
			{
				// Can jump
				//backflip = false;
				return true;
			} else if (myVelocity2D.Length() < 0.001f)
            {
				return true; // We can try a jump standing still. If we're already close to a wall and can't get speed for example
            }
			/*else if ((dot < 150 && !amInRageCoolDown && 150 < maxSpeed) || (dot < 110 && amInRageCoolDown && 110 < maxSpeed))
			{
				// We're slower than a backflip anyway.
				// Do a backflip :). 150 is backflip speed.
				backflip = true;
				return true;
			}*/
			return false;
		}
	}
}
