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

	public enum SillyMode
	{
		NONE,
		SILLY,
		DBS,
		GRIPKICKDBS, 
		LOVER,
		CUSTOM
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

		private bool oldIsDuelMode = false;

		private void DoSillyThings(ref UserCommand userCmd)
		{
			// Of course normally the priority is to get back in spec
			// But sometimes it might not be possible, OR we might not want it (for silly reasons)
			// So as long as we aren't in spec, let's do silly things.
			bool isSillyCameraOperator = this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator;
			if (isDuelMode || isSillyCameraOperator)
			{
				if(isDuelMode != oldIsDuelMode || infoPool.sillyMode == SillyMode.NONE) // If gametype changes or if not set yet.
                {
					infoPool.sillyMode = isDuelMode ? SillyMode.SILLY : SillyMode.GRIPKICKDBS;
					oldIsDuelMode = isDuelMode;
				}
				DoSillyThingsReal(ref userCmd, infoPool.sillyMode);
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
		DateTime lastTimeAnyPlayerSeen = DateTime.Now;
		DateTime lastTimeFastMove = DateTime.Now;
		bool kissOrCustomSent = false;
		DateTime kissOrCustomLastSent = DateTime.Now;
		bool previousSaberHolstered = false;
		private unsafe void DoSillyThingsReal(ref UserCommand userCmd, SillyMode sillyMode)
		{
			int myNum = ClientNum.GetValueOrDefault(-1);
			if (myNum < 0 || myNum > infoPool.playerInfo.Length) return;

			PlayerInfo myself = infoPool.playerInfo[myNum];
			PlayerInfo closestPlayer = null;
			float closestDistance = float.PositiveInfinity;
			List<PlayerInfo> grippingPlayers = new List<PlayerInfo>();

			bool bsModeActive = sillyMode == SillyMode.GRIPKICKDBS && infoPool.gripDbsMode == GripKickDBSMode.SPEEDRAGEBS; // Use BS instead of dbs
			bool speedRageModeActive = infoPool.gripDbsModeOneOf(GripKickDBSMode.SPEEDRAGE, GripKickDBSMode.SPEEDRAGEBS);

			int timeDelta = lastPlayerState.CommandTime - lastPlayerState.CommandTime;
			Vector3 ourMoveDelta = myself.position - sillyOurLastPosition;
			float realDeltaSpeed = timeDelta == 0 ? float.PositiveInfinity : ourMoveDelta.Length() / ((float)timeDelta / 1000.0f); // Our speed per second
			bool movingVerySlowly = timeDelta > 0 && realDeltaSpeed < 20;
            if (movingVerySlowly)
            {
				if ((DateTime.Now - lastTimeFastMove).TotalSeconds > 30)
				{
					// KMS
					leakyBucketRequester.requestExecution("kill", RequestCategory.FIGHTBOT, 0, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
					return;
				}
			} else
            {
				lastTimeFastMove = DateTime.Now;
			}

			sillyLastCommandTime = lastPlayerState.CommandTime;
			sillyOurLastPosition = myself.position;

			bool amGripped = lastPlayerState.PlayerMoveType == JKClient.PlayerMoveType.Float; // Not 100% reliable ig but good enough
			bool weAreChoking = amGripped && lastPlayerState.ForceHandExtend == 5;
			bool grippingSomebody = lastPlayerState.PowerUps[9] != 0 && 0 < (lastPlayerState.forceData.ForcePowersActive & (1 << 6)); // TODO 1.04 and JKA
			bool amInRage = (lastPlayerState.forceData.ForcePowersActive & (1 << 8)) > 0; // TODO 1.04 and JKA
			bool amInSpeed = (lastPlayerState.forceData.ForcePowersActive & (1 << 2)) > 0; // TODO 1.04 and JKA
			int rageCoolDownTime = lastPlayerState.forceData.ForceRageRecoveryTime - lastSnapshot.ServerTime;
			bool amInRageCoolDown = lastPlayerState.forceData.ForceRageRecoveryTime > lastSnapshot.ServerTime;
			int fullForceRecoveryTime = 5000; // TODO Measure this on the server in particular
			bool canUseNonRageSpeedPowers = !speedRageModeActive || (amInRageCoolDown && rageCoolDownTime > fullForceRecoveryTime); // We are waiting for rage to cool down anyway, may as well use others.


			int dbsTriggerDistance = bsModeActive ? 64 : 128; //128 is max possible but that results mostly in just jumps without hits as too far away.
			int maxGripDistance = 256; // 256 is default in jk2
			int maxDrainDistance = 512; // 256 is default in jk2
			int maxPullDistance = 1024; // 256 is default in jk2

			bool blackListedPlayerIsNearby = false;
			bool strongIgnoreNearby = false;

			bool amInAttack = lastPlayerState.SaberMove > 3;

			// Find nearest player
			foreach (PlayerInfo pi in infoPool.playerInfo)
			{
				if (pi.infoValid && pi.IsAlive && pi.team != Team.Spectator && pi.clientNum != myNum  && !pi.duelInProgress)
				{
					float curdistance = (pi.position - myself.position).Length();
					if (amGripped && (pi.forcePowersActive & (1 << 6)) > 0 && curdistance <= maxGripDistance) // To find who is gripping us
					{
						grippingPlayers.Add(pi);
					}
					if (pi.chatCommandTrackingStuff.fightBotBlacklist && curdistance < 500) {
						blackListedPlayerIsNearby = true;
						continue;
					}
					if (pi.chatCommandTrackingStuff.fightBotStrongIgnore && curdistance < 300) {
						strongIgnoreNearby = true;
						continue;
					}
					if (pi.chatCommandTrackingStuff.fightBotStrongIgnore && curdistance < 100 && amInAttack) { // If a very scared person less than 100 units away and im attacking ... kill myself with high priority.
						leakyBucketRequester.requestExecution("kill", RequestCategory.SELFKILL, 5, 300, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
						return;
					}
					if (pi.chatCommandTrackingStuff.fightBotIgnore) continue;
					if (!pi.lastPositionOrAngleChange.HasValue || (DateTime.Now - pi.lastPositionOrAngleChange.Value).TotalSeconds > 10) continue; // ignore mildly afk players
					if (grippingSomebody && personImTryingToGrip != pi.clientNum) continue; // This is shit. There must be better way. Basically, wanna get the player we're actually gripping.
																							// Check if player is in the radius of someone with strong ignore
					bool thisPlayerNearStrongIgnore = false;
					foreach (PlayerInfo piSub in infoPool.playerInfo)
					{
						if (piSub.infoValid && piSub.IsAlive && piSub.team != Team.Spectator && piSub.clientNum != myNum && !piSub.duelInProgress)
						{
							if(piSub.chatCommandTrackingStuff.fightBotStrongIgnore && (pi.position - piSub.position).Length() < 300)
                            {
								thisPlayerNearStrongIgnore = true;
								break;
							}
						}
					}
					if (thisPlayerNearStrongIgnore) continue;

					if (curdistance < closestDistance)
                    {
						closestDistance = curdistance;
						closestPlayer = pi;
					}
				}
			}

			if (closestPlayer == null)
			{
				if((DateTime.Now - lastTimeAnyPlayerSeen).TotalSeconds > 30)
                {
					// KMS
					leakyBucketRequester.requestExecution("kill", RequestCategory.FIGHTBOT, 0, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                }
				return;
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
			Vector2 myPosition2D = new Vector2() { X= myself.position.X,Y= myself.position.Y};
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

			//float verticalDistance = Math.Abs(closestPlayer.position.Z - myself.position.Z);
			//bool dbsPossiblePositionWise = distance2D < dbsTriggerDistance && verticalDistance < 64; // Backslash distance. The vertical distance we gotta do better, take crouch into account etc.
			
			bool amCurrentlyDbsing = lastPlayerState.SaberMove == dbsLSMove || lastPlayerState.SaberMove == bsLSMove;
			bool amInParry = lastPlayerState.SaberMove >= parryLower && lastPlayerState.SaberMove <= parryUpper;

			bool gripForcePitchUp = false;
			bool gripForcePitchDown = false;
			bool gripForceKick = false;
			bool releaseGrip = false;
			bool doPull = false;

			bool heIsStandingOnTopOfMe = closestPlayer.groundEntityNum == myself.clientNum ||( vecToClosestPlayer2D.Length() < 15 && closestPlayer.position.Z <= (myself.position.Z + myMax + hisMin + 10.0f) && closestPlayer.position.Z > (myself.position.Z + myMax + hisMin - 1.0f) && closestPlayer.velocity.Z < 5.0f);
			bool dbsPossiblePositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z > (closestPlayer.position.Z - hisMin) && myself.position.Z < (closestPlayer.position.Z + hisMax);
			bool dbsPossible = dbsPossiblePositionWise && !grippingSomebody && !amGripped; // Don't dbs while gripped. Is it even possible?
			bool dbsPossibleWithJumpPositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z < (closestPlayer.position.Z - hisMin) && (myself.position.Z + 96) > (closestPlayer.position.Z - hisMin); // 96 is force level 1 jump height. adapt to different force jump heights?
			bool dbsPossibleWithJump = dbsPossibleWithJumpPositionWise && !grippingSomebody; // Don't dbs while gripped. Is it even possible?

			bool doingGripDefense = false;

            if (amGripped && grippingPlayers.Count > 0 && lastPlayerState.forceData.ForcePower > 0)
            {
				// TODO exception when gripping myself?
				PlayerInfo guessedGrippingPlayer = grippingPlayers[(int)(sillyflip % grippingPlayers.Count)];
				moveVector = guessedGrippingPlayer.position - myViewHeightPos;
            }
			else if (amCurrentlyDbsing)
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
				moveVector.Z += hisMax - myMax;
			} else if (infoPool.sillyModeOneOf(SillyMode.DBS, SillyMode.GRIPKICKDBS) && dbsPossible && lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
				moveVector.Z += hisMax - myMax;
			}
			else if (sillyMode == SillyMode.GRIPKICKDBS && heIsStandingOnTopOfMe && meIsDucked)
			{
				gripForceKick = true;
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
				moveVector = (closestPlayer.position + closestPlayer.velocity) - myself.position;
			} else
            {
				Vector2 moveVector2d = new Vector2();
				// Do some fancy shit.
				// Do a shitty iterative approach for now. Improve in future if possible.
				// Remember that the dotProduct is the minimum speed we must have in his direction.
				bool foundSolution = false;
				float pingInSeconds = (float)lastSnapshot.ping / 1000.0f;
				for(float interceptTime=0.1f; interceptTime < 10.0f; interceptTime+= 0.1f)
                {
					// Imagine our 1-second reach like a circle. If that circle intersects with his movement line, we can intercept him quickly)
					// If the intersection does not exist, we expand the circle, by giving ourselves more time to intercept.
					Vector2 hisPosThen = enemyPosition2D + enemyVelocity2D * (interceptTime+ pingInSeconds);
					Vector2 interceptPos = hisPosThen + Vector2.Normalize(enemyVelocity2D) * 100.0f; // Give it 100 units extra in that direction for ideal intercept.
					moveVector2d = (interceptPos - myPosition2D);
					if (moveVector2d.Length() <= mySpeed*interceptTime)
                    {
						foundSolution = true;
						break;
                    }
				}
                if (!foundSolution)
                {
					// Sad. ok just fall back to the usual.
					moveVector = (closestPlayer.position + closestPlayer.velocity) - myself.position;
                }
                else
                {
					moveVector = new Vector3() { X= moveVector2d.X, Y= moveVector2d.Y, Z=moveVector.Z};
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
			if (sillyMode == SillyMode.LOVER)
			{
				if(closestDistance < 100 && userCmd.GenericCmd == 0)
                {
					userCmd.GenericCmd = !lastPlayerState.SaberHolstered ? (byte)GenericCommandJK2.SABERSWITCH : (byte)0; // Switch saber off.
				}
				userCmd.ForwardMove = 127;
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
				if(lastPlayerState.SaberHolstered != previousSaberHolstered)
                {
					kissOrCustomSent = false;
                }
				userCmd.ForwardMove = 127;
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
				switch (sillyflip % 4) {
					case 0:
						userCmd.ForwardMove = 127;
						break;
					case 1:
						yawAngle += 90;
						userCmd.RightMove = 127;
						break;
					case 2:
						yawAngle += 180;
						userCmd.ForwardMove = -128;
						break;
					case 3:
						yawAngle += 270;
						userCmd.RightMove = -128;
						break;
				}

				if (sillyAttack)
				{
					userCmd.Buttons |= (int)UserCommand.Button.Attack;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			} else if(infoPool.sillyModeOneOf( SillyMode.DBS,SillyMode.GRIPKICKDBS))
            {
				
				userCmd.ForwardMove = 127;
                if (amGripped && grippingPlayers.Count > 0 && lastPlayerState.forceData.ForcePower > 0)
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
					if (lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1 && (dbsPossible || dbsPossibleWithJump) && !bsModeActive)
					{
						// Check if we are moving in the right direction
						// Because once we are in the air, we can't change our direction anymore.

						Vector2 myVelocity2D = new Vector2() { X=myself.velocity.X, Y=myself.velocity.Y};
						Vector2 moveVector2D = new Vector2() { X = moveVector.X, Y = moveVector.Y };
						Vector2 moveVector2DNormalized = Vector2.Normalize(moveVector2D);
						float dot = Vector2.Dot(myVelocity2D, moveVector2DNormalized);
						float myVelocity2DAbs = myVelocity2D.Length();
						if(dot > 150 && ((dot > mySpeed * 0.75f && dot > myVelocity2DAbs * 0.95f) || distance2D <= 32 || (closestPlayer.groundEntityNum == Common.MaxGEntities-2 &&(closestPlayer.position.Z > (myself.position.Z + 10.0f))))) // Make sure we are at least 75% in the right direction, or ignore if we are very close to player or if other player is standing on higher ground than us.
                        {
							// Gotta jump
							userCmd.Upmove = 127;
						} else if ((dot < 150 && !amInRageCoolDown) || (dot < 110 && amInRageCoolDown))
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
					} else if (sillyMode == SillyMode.GRIPKICKDBS && gripForceKick) // Kick enemy
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
					else if (sillyMode == SillyMode.GRIPKICKDBS && heIsStandingOnTopOfMe && !meIsDucked) // Release him and then we can dbs
					{
						userCmd.ForwardMove = 0;
						userCmd.Upmove = -128;
						amGripping = false;
					}
					else if(sillyMode == SillyMode.GRIPKICKDBS && gripPossible)
					{
						personImTryingToGrip = closestPlayer.clientNum;
						amGripping = true;

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

					dbsLastRotationOffset = rotationBy;
					dbsLastVertRotationOffset = vertRotationBy;
				} else
                {
					switch (sillyflip %4)
					{
						case 0:
							userCmd.ForwardMove = 127;
							break;
						case 1:
							yawAngle += 90;
							userCmd.RightMove = 127;
							break;
						case 2:
							yawAngle += 180;
							userCmd.ForwardMove = -128;
							break;
						case 3:
							yawAngle += 270;
							userCmd.RightMove = -128;
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

			// Conservative speed usage.
			if((sillyMode == SillyMode.GRIPKICKDBS && infoPool.gripDbsMode == GripKickDBSMode.SPEED) || sillyMode == SillyMode.LOVER)
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
                if (canUseNonRageSpeedPowers)
                {
					userCmd.Buttons |= (int)UserCommand.Button.ForceDrainJK2;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			}

			if (lastPlayerState.Stats[0] <= 0) // Respawn no matter what if dead.
            {
				if (sillyAttack)
				{
					userCmd.Buttons |= (int)UserCommand.Button.Attack;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			} else if (blackListedPlayerIsNearby || strongIgnoreNearby) // If near blacklisted player, don't do anything that could cause damage
            {
				userCmd.Buttons = 0;
				userCmd.GenericCmd = !lastPlayerState.SaberHolstered ? (byte)GenericCommandJK2.SABERSWITCH : (byte)0; // Switch saber off.
                if (doingGripDefense) // Allow grip defense.
                {
					userCmd.GenericCmd = (byte)GenericCommandJK2.FORCE_THROW;
				}
				userCmd.Upmove = userCmd.Upmove > 0 ? (sbyte)0 : userCmd.Upmove;
			} else if (userCmd.GenericCmd == 0 && lastPlayerState.SaberHolstered && (sillyMode != SillyMode.CUSTOM || (closestDistance>200 && sillyMode == SillyMode.LOVER)))
            {
				userCmd.GenericCmd = (byte)GenericCommandJK2.SABERSWITCH; // switch it back on.

			}

			userCmd.Weapon = (byte)infoPool.saberWeaponNum;

			userCmd.Angles[YAW] = Angle2Short(yawAngle);
			userCmd.Angles[PITCH] = Angle2Short(pitchAngle);

			sillyAttack = !sillyAttack;
			//sillyflip++;
			sillyflip+= (lastSnapNum - sillyMoveLastSnapNum);
			//sillyflip = sillyflip % 4;
			sillyMoveLastSnapNum = lastSnapNum;
			lastUserCmdTime = userCmd.ServerTime;
			previousSaberHolstered = lastPlayerState.SaberHolstered;
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
	}
}
