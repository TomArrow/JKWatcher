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
			DBS
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
				DoSillyThingsDuel(ref userCmd);
			}
		}

		int sillyflip = 0;
		bool sillyAttack = false;

		DateTime lastSaberAttackCycleSent = DateTime.Now;

		int sillyMoveLastSnapNum = 0;
		int lastUserCmdTime = 0;
		float dbsLastRotationOffset = 0;
		private unsafe void DoSillyThingsDuel(ref UserCommand userCmd)
		{
			int myNum = ClientNum.GetValueOrDefault(-1);
			if (myNum < 0 || myNum > infoPool.playerInfo.Length) return;

			PlayerInfo myself = infoPool.playerInfo[myNum];
			PlayerInfo closestPlayer = null;
			float closestDistance = float.PositiveInfinity;
			// Find nearest player
			foreach (PlayerInfo pi in infoPool.playerInfo)
			{
				if (pi.infoValid && pi.IsAlive && pi.team != Team.Spectator && pi.clientNum != myNum  && pi.IsAlive)
				{
					float curdistance = (pi.position - myself.position).Length();
					if (curdistance < closestDistance)
                    {
						closestDistance = curdistance;
						closestPlayer = pi;
					}
				}
			}

			if (closestPlayer == null) return;

			int genCmdSaberAttackCycle = jkaMode ? 26 : 20;
			int bsLSMove = jkaMode ? 12 : 12;
			int dbsLSMove = jkaMode ? 13 : 13;
			int parryLower = jkaMode ? 152 : 108;
			int parryUpper = jkaMode ? 156 : 112;
			int dbsTriggerDistance = 110; //128 is max possible but that results mostly in just jumps without hits as too far away.

			float mySpeed = this.baseSpeed == 0 ? (myself.speed == 0 ? 250: myself.speed) : this.baseSpeed; // We only walk so walk speed is our speed.
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
			float verticalDistance = Math.Abs(closestPlayer.position.Z - myself.position.Z);
			bool dbsPossible = distance2D < dbsTriggerDistance && verticalDistance < 64; // Backslash distance. The vertical distance we gotta do better, take crouch into account etc.

			bool amCurrentlyDbsing = lastPlayerState.SaberMove == dbsLSMove || lastPlayerState.SaberMove == bsLSMove;
			bool amInAttack = lastPlayerState.SaberMove > 3;
			bool amInParry = lastPlayerState.SaberMove >= parryLower && lastPlayerState.SaberMove <= parryUpper;
			if (sillyMode == SillyMode.DBS && dbsPossible && lastPlayerState.GroundEntityNum == Common.MaxGEntities - 1)
            {
				moveVector = vecToClosestPlayer; // For the actual triggering of dbs we need to be precise
            }
			else if(dotProduct > (mySpeed-10)) // -10 so we don't end up with infinite intercept routes and weird potential number issues
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
			} else if(sillyMode == SillyMode.DBS)
            {
				
				userCmd.ForwardMove = 127;
                if (!amInAttack /*&& !amCurrentlyDbsing*/)
                {

					if (lastPlayerState.GroundEntityNum != Common.MaxGEntities - 1 && dbsPossible)
					{
						// Gotta jump
						userCmd.Upmove = 127;
					}
					else if (dbsPossible)
					{
						// Do dbs
						userCmd.Upmove = -128;
						yawAngle += 180;
						pitchAngle -= 180;
						userCmd.ForwardMove = -128;
						if (sillyAttack)
						{
							userCmd.Buttons |= (int)UserCommand.Button.Attack;
							userCmd.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
						}
					}
				}
				else if(amCurrentlyDbsing) 
				{
					float serverFrameDuration = 1000.0f/ (float)this.SnapStatus.TotalSnaps;
					float maxRotationPerMillisecond = 170.0f / serverFrameDuration; // -10 to be safe. must be under 180 i think to be reasonable.
					float thisCommandDuration = userCmd.ServerTime - lastUserCmdTime;
					float rotationBy = thisCommandDuration * maxRotationPerMillisecond + dbsLastRotationOffset;

					yawAngle += rotationBy;

					dbsLastRotationOffset = rotationBy;
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

			if(lastPlayerState.Stats[0] <= 0) // Respawn no matter what if dead.
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
