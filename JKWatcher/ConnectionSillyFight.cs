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
	// Silly fighting when forced out of spec
	public partial class Connection
	{

		enum SillyMode
        {
			SILLY,
			DBS,
			GRIPKICKDBS
        }

		enum GenericCommand
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

		SillyMode sillyMode = SillyMode.DBS;

		private bool amNotInSpec = false; // If not in spec for whatever reason, do funny things
		private bool isDuelMode = false; // If we are in duel mode, different behavior. Some servers don't like us attacking innocents but for duel we have to, to end it quick. But if someone attacks US, then all bets are off.

		private void DoSillyThings(ref UserCommand userCmd)
		{
			// Of course normally the priority is to get back in spec
			// But sometimes it might not be possible, OR we might not want it (for silly reasons)
			// So as long as we aren't in spec, let's do silly things.
			bool isSillyCameraOperator = this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator;
			if (isDuelMode || isSillyCameraOperator)
			{
				sillyMode = isDuelMode ? SillyMode.SILLY : SillyMode.GRIPKICKDBS;
				DoSillyThingsDuel(ref userCmd);
			}
		}

		int sillyflip = 0;
		bool sillyAttack = false;

		DateTime lastSaberAttackCycleSent = DateTime.Now;

		int sillyMoveLastSnapNum = 0;
		int lastUserCmdTime = 0;
		float dbsLastRotationOffset = 0;
		float dbsLastVertRotationOffset = 0;
		bool amGripping = false;
		int personImTryingToGrip = -1;
		private unsafe void DoSillyThingsDuel(ref UserCommand userCmd)
		{
			int myNum = ClientNum.GetValueOrDefault(-1);
			if (myNum < 0 || myNum > infoPool.playerInfo.Length) return;

			PlayerInfo myself = infoPool.playerInfo[myNum];
			PlayerInfo closestPlayer = null;
			float closestDistance = float.PositiveInfinity;

			bool grippingSomebody = lastPlayerState.PowerUps[9] != 0 && 0 < (lastPlayerState.forceData.ForcePowersActive & (1 << 6)); // TODO 1.04 and JKA
			bool amInRage = (lastPlayerState.forceData.ForcePowersActive & (1 << 8)) > 0; // TODO 1.04 and JKA
			bool amInSpeed = (lastPlayerState.forceData.ForcePowersActive & (1 << 2)) > 0; // TODO 1.04 and JKA

			// Find nearest player
			foreach (PlayerInfo pi in infoPool.playerInfo)
			{
				if (pi.infoValid && pi.IsAlive && pi.team != Team.Spectator && pi.clientNum != myNum  && pi.IsAlive)
				{
					if (grippingSomebody && personImTryingToGrip != pi.clientNum) continue; // This is shit. There must be better way. Basically, wanna get the player we're actually gripping.
					float curdistance = (pi.position - myself.position).Length();
					if (curdistance < closestDistance)
                    {
						closestDistance = curdistance;
						closestPlayer = pi;
					}
				}
			}

			if (closestPlayer == null) return;

			Vector3 myViewHeightPos = myself.position;
			myViewHeightPos.Z += lastPlayerState.ViewHeight;


			int genCmdSaberAttackCycle = jkaMode ? 26 : 20;
			int bsLSMove = jkaMode ? 12 : 12;
			int dbsLSMove = jkaMode ? 13 : 13;
			int parryLower = jkaMode ? 152 : 108;
			int parryUpper = jkaMode ? 156 : 112;
			int dbsTriggerDistance = 110; //128 is max possible but that results mostly in just jumps without hits as too far away.
			int maxGripDistance = 256; // 256 is default in jk2
			int maxPullDistance = 1024; // 256 is default in jk2


			bool heInAttack = closestPlayer.saberMove > 3;

			//int grippedEntity = lastPlayerState.forceData.ForceGripEntityNum != Common.MaxGEntities - 1 ? lastPlayerState.forceData.ForceGripEntityNum : -1;
			bool pullPossibleDistanceWise = closestDistance < maxPullDistance;//&& grippedEntity == -1;
			bool pullPossible = pullPossibleDistanceWise && (heInAttack || (closestPlayer.groundEntityNum == Common.MaxGEntities-1));
			bool gripPossibleDistanceWise = (myViewHeightPos - closestPlayer.position).Length() < maxGripDistance;//&& grippedEntity == -1;
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
			bool amInAttack = lastPlayerState.SaberMove > 3;
			bool amInParry = lastPlayerState.SaberMove >= parryLower && lastPlayerState.SaberMove <= parryUpper;

			bool gripForcePitchUp = false;
			bool gripForcePitchDown = false;
			bool gripForceKick = false;
			bool releaseGrip = false;
			bool doPull = false;

			bool heIsStandingOnTopOfMe = closestPlayer.groundEntityNum == myself.clientNum ||( vecToClosestPlayer2D.Length() < 15 && closestPlayer.position.Z <= (myself.position.Z + myMax + hisMin + 10.0f) && closestPlayer.position.Z > (myself.position.Z + myMax + hisMin - 1.0f) && closestPlayer.velocity.Z < 5.0f);
			bool dbsPossiblePositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z > (closestPlayer.position.Z - hisMin) && myself.position.Z < (closestPlayer.position.Z + hisMax);
			bool dbsPossible = dbsPossiblePositionWise && !grippingSomebody; // Don't dbs while gripped. Is it even possible?
			bool dbsPossibleWithJumpPositionWise = !heIsStandingOnTopOfMe && distance2D < dbsTriggerDistance && myself.position.Z < (closestPlayer.position.Z - hisMin) && (myself.position.Z + 96) > (closestPlayer.position.Z - hisMin); // 96 is force level 1 jump height. adapt to different force jump heights?
			bool dbsPossibleWithJump = dbsPossibleWithJumpPositionWise && !grippingSomebody; // Don't dbs while gripped. Is it even possible?

			if (amCurrentlyDbsing)
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
				moveVector.Z += hisMax - myMax;
			} else if ((sillyMode == SillyMode.DBS || sillyMode == SillyMode.GRIPKICKDBS) && dbsPossible && lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
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
				moveVector = closestPlayer.position - myViewHeightPos;
			}
			else if (sillyMode == SillyMode.GRIPKICKDBS && pullPossible && !gripPossibleDistanceWise && !amGripping && lastPlayerState.forceData.ForcePower >= 25)
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
				for(float interceptTime=0.1f; interceptTime < 10.0f; interceptTime+= 0.1f)
                {
					// Imagine our 1-second reach like a circle. If that circle intersects with his movement line, we can intercept him quickly)
					// If the intersection does not exist, we expand the circle, by giving ourselves more time to intercept.
					Vector2 hisPosThen = enemyPosition2D + enemyVelocity2D * interceptTime;
					moveVector2d = (hisPosThen - myPosition2D);
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
            if(sillyMode == SillyMode.SILLY) { 
				switch (sillyflip) {
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
			} else if(sillyMode == SillyMode.DBS || sillyMode == SillyMode.GRIPKICKDBS)
            {
				
				userCmd.ForwardMove = 127;
                if (!amInAttack /*&& !amCurrentlyDbsing*/)
                {
					//if (lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1 && dbsPossible)
					if (lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1 && (dbsPossible || dbsPossibleWithJump))
					{
						// Check if we are moving in the right direction
						// Because once we are in the air, we can't change our direction anymore.

						Vector2 myVelocity2D = new Vector2() { X=myself.velocity.X, Y=myself.velocity.Y};
						Vector2 moveVector2D = new Vector2() { X = moveVector.X, Y = moveVector.Y };
						Vector2 moveVector2DNormalized = Vector2.Normalize(moveVector2D);
						float dot = Vector2.Dot(myVelocity2D, moveVector2DNormalized);

						if(dot > baseSpeed * 0.75f || distance2D <= 32 || (closestPlayer.groundEntityNum == Common.MaxGEntities-2 &&(closestPlayer.position.Z > (myself.position.Z + 10.0f)))) // Make sure we are at least 75% in the right direction, or ignore if we are very close to player or if other player is standing on higher ground than us.
                        {
							// Gotta jump
							userCmd.Upmove = 127;
						}

					}
					else if (dbsPossible)
					{
						// Do dbs
						userCmd.Upmove = -128;
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
						userCmd.GenericCmd = (byte)GenericCommand.FORCE_PULL;

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
					switch (sillyflip)
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

			if(userCmd.GenericCmd == 0) // Other stuff has priority.
            {
				if (!amInRage && lastPlayerState.Stats[0] > 1 && lastPlayerState.Stats[0] < 50 && closestDistance < 500)
				{
					// Go rage
					userCmd.GenericCmd = (byte)GenericCommand.FORCE_RAGE;
				}
				else if (amInRage && (closestDistance > 500 || lastPlayerState.Stats[0] > 50))
				{
					// Disable rage if nobody within 500 units
					userCmd.GenericCmd = (byte)GenericCommand.FORCE_RAGE;
				}
				else if (!amInSpeed && lastPlayerState.forceData.ForcePower == 100 && closestDistance < 700)
				{
					// Go Speed
					userCmd.GenericCmd = (byte)GenericCommand.FORCE_SPEED;
				}
				else if (amInSpeed && (lastPlayerState.forceData.ForcePower < 25 && !amInRage || closestDistance > 700))
				{
					// Disable speed unless in rage
					userCmd.GenericCmd = (byte)GenericCommand.FORCE_SPEED;
  
				}
			}

			if (amGripping)
			{
				userCmd.Buttons |= (int)UserCommand.Button.ForceGripJK2;
				userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
			}

			if (lastPlayerState.Stats[0] <= 0) // Respawn no matter what if dead.
            {
				if (sillyAttack)
				{
					userCmd.Buttons |= (int)UserCommand.Button.Attack;
					userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
				}
			}

			userCmd.Weapon = (byte)infoPool.saberWeaponNum;

			userCmd.Angles[YAW] = Angle2Short(yawAngle);
			userCmd.Angles[PITCH] = Angle2Short(pitchAngle);

			sillyAttack = !sillyAttack;
			//sillyflip++;
			sillyflip+= (lastSnapNum - sillyMoveLastSnapNum);
			sillyflip = sillyflip % 4;
			sillyMoveLastSnapNum = lastSnapNum;
			lastUserCmdTime = userCmd.ServerTime;
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
