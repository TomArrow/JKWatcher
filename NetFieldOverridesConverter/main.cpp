

// Most of this code copied from GPL source code release of Jedi Academy
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fstream>

typedef enum { qfalse, qtrue }	qboolean;
#ifdef _XBOX
#define	qboolean	int		//don't want strict type checking on the qboolean
#endif
#include <cassert>
#include <iostream>
#include <cstdarg>

typedef float vec_t;
typedef vec_t vec2_t[2];
typedef vec_t vec3_t[3];
typedef vec_t vec4_t[4];
typedef vec_t vec5_t[5];

// bit field limits
#define	MAX_STATS				16
#define	MAX_PERSISTANT			16
#define	MAX_POWERUPS			16
#define	MAX_WEAPONS				19		

#define	MAX_PS_EVENTS			2

#define PS_PMOVEFRAMECOUNTBITS	6

#ifdef _XBOX
#define	GENTITYNUM_BITS	9		// don't need to send any more
#else
#define	GENTITYNUM_BITS	10		// don't need to send any more
#endif
#define	MAX_GENTITIES	(1<<GENTITYNUM_BITS)


typedef enum
{
	FP_FIRST = 0,//marker
	FP_HEAL = 0,//instant
	FP_LEVITATION,//hold/duration
	FP_SPEED,//duration
	FP_PUSH,//hold/duration
	FP_PULL,//hold/duration
	FP_TELEPATHY,//instant
	FP_GRIP,//hold/duration
	FP_LIGHTNING,//hold/duration
	FP_RAGE,//duration
	FP_PROTECT,
	FP_ABSORB,
	FP_TEAM_HEAL,
	FP_TEAM_FORCE,
	FP_DRAIN,
	FP_SEE,
	FP_SABER_OFFENSE,
	FP_SABER_DEFENSE,
	FP_SABERTHROW,
	NUM_FORCE_POWERS
};
typedef int forcePowers_t;

// all the different tracking "channels"
typedef enum {
	TRACK_CHANNEL_NONE = 50,
	TRACK_CHANNEL_1,
	TRACK_CHANNEL_2,
	TRACK_CHANNEL_3,
	TRACK_CHANNEL_4,
	TRACK_CHANNEL_5,
	NUM_TRACK_CHANNELS
} trackchan_t;
#define TRACK_CHANNEL_MAX (NUM_TRACK_CHANNELS-50)
typedef struct forcedata_s {
	int			forcePowerDebounce[NUM_FORCE_POWERS];	//for effects that must have an interval
	int			forcePowersKnown;
	int			forcePowersActive;
	int			forcePowerSelected;
	int			forceButtonNeedRelease;
	int			forcePowerDuration[NUM_FORCE_POWERS];
	int			forcePower;
	int			forcePowerMax;
	int			forcePowerRegenDebounceTime;
	int			forcePowerLevel[NUM_FORCE_POWERS];		//so we know the max forceJump power you have
	int			forcePowerBaseLevel[NUM_FORCE_POWERS];
	int			forceUsingAdded;
	float		forceJumpZStart;					//So when you land, you don't get hurt as much
	float		forceJumpCharge;					//you're current forceJump charge-up level, increases the longer you hold the force jump button down
	int			forceJumpSound;
	int			forceJumpAddTime;
	int			forceGripEntityNum;					//what entity I'm gripping
	int			forceGripDamageDebounceTime;		//debounce for grip damage
	float		forceGripBeingGripped;				//if > level.time then client is in someone's grip
	int			forceGripCripple;					//if != 0 then make it so this client can't move quickly (he's being gripped)
	int			forceGripUseTime;					//can't use if > level.time
	float		forceGripSoundTime;
	float		forceGripStarted;					//level.time when the grip was activated
	int			forceHealTime;
	int			forceHealAmount;

	//This hurts me somewhat to do, but there's no other real way to allow completely "dynamic" mindtricking.
	int			forceMindtrickTargetIndex; //0-15
	int			forceMindtrickTargetIndex2; //16-32
	int			forceMindtrickTargetIndex3; //33-48
	int			forceMindtrickTargetIndex4; //49-64

	int			forceRageRecoveryTime;
	int			forceDrainEntNum;
	float		forceDrainTime;

	int			forceDoInit;

	int			forceSide;
	int			forceRank;

	int			forceDeactivateAll;

	int			killSoundEntIndex[TRACK_CHANNEL_MAX]; //this goes here so it doesn't get wiped over respawn

	qboolean	sentryDeployed;

	int			saberAnimLevelBase;//sigh...
	int			saberAnimLevel;
	int			saberDrawAnimLevel;

	int			suicides;

	int			privateDuelTime;
} forcedata_t;


typedef enum {
	TR_STATIONARY,
	TR_INTERPOLATE,				// non-parametric, but interpolate between snapshots
	TR_LINEAR,
	TR_LINEAR_STOP,
	TR_NONLINEAR_STOP,
	TR_SINE,					// value = base + sin( time / duration ) * delta
	TR_GRAVITY
} trType_t;

typedef struct {
	trType_t	trType;
	int		trTime;
	int		trDuration;			// if non 0, trTime + trDuration = stop time
	vec3_t	trBase;
	vec3_t	trDelta;			// velocity, etc
} trajectory_t;

// entityState_t is the information conveyed from the server
// in an update message about entities that the client will
// need to render in some way
// Different eTypes may use the information in different ways
// The messages are delta compressed, so it doesn't really matter if
// the structure size is fairly large
#ifndef _XBOX	// First, real version for the PC, with all members 32-bits

typedef struct entityState_s {
	int		number;			// entity index
	int		eType;			// entityType_t
	int		eFlags;
	int		eFlags2;		// EF2_??? used much less frequently

	trajectory_t	pos;	// for calculating position
	trajectory_t	apos;	// for calculating angles

	int		time;
	int		time2;

	vec3_t	origin;
	vec3_t	origin2;

	vec3_t	angles;
	vec3_t	angles2;

	//rww - these were originally because we shared g2 info client and server side. Now they
	//just get used as generic values everywhere.
	int		bolt1;
	int		bolt2;

	//rww - this is necessary for determining player visibility during a jedi mindtrick
	int		trickedentindex; //0-15
	int		trickedentindex2; //16-32
	int		trickedentindex3; //33-48
	int		trickedentindex4; //49-64

	float	speed;

	int		fireflag;

	int		genericenemyindex;

	int		activeForcePass;

	int		emplacedOwner;

	int		otherEntityNum;	// shotgun sources, etc
	int		otherEntityNum2;

	int		groundEntityNum;	// -1 = in air

	int		constantLight;	// r + (g<<8) + (b<<16) + (intensity<<24)
	int		loopSound;		// constantly loop this sound
	qboolean	loopIsSoundset; //qtrue if the loopSound index is actually a soundset index

	int		soundSetIndex;

	int		modelGhoul2;
	int		g2radius;
	int		modelindex;
	int		modelindex2;
	int		clientNum;		// 0 to (MAX_CLIENTS - 1), for players and corpses
	int		frame;

	qboolean	saberInFlight;
	int			saberEntityNum;
	int			saberMove;
	int			forcePowersActive;
	int			saberHolstered;//sent in only only 2 bits - should be 0, 1 or 2

	qboolean	isJediMaster;

	qboolean	isPortalEnt; //this needs to be seperate for all entities I guess, which is why I couldn't reuse another value.

	int		solid;			// for client side prediction, trap_linkentity sets this properly

	int		event;			// impulse events -- muzzle flashes, footsteps, etc
	int		eventParm;

	// so crosshair knows what it's looking at
	int			owner;
	int			teamowner;
	qboolean	shouldtarget;

	// for players
	int		powerups;		// bit flags
	int		weapon;			// determines weapon and flash model, etc
	int		legsAnim;
	int		torsoAnim;

	qboolean	legsFlip; //set to opposite when the same anim needs restarting, sent over in only 1 bit. Cleaner and makes porting easier than having that god forsaken ANIM_TOGGLEBIT.
	qboolean	torsoFlip;

	int		forceFrame;		//if non-zero, force the anim frame

	int		generic1;

	int		heldByClient; //can only be a client index - this client should be holding onto my arm using IK stuff.

	int		ragAttach; //attach to ent while ragging

	int		iModelScale; //rww - transfer a percentage of the normal scale in a single int instead of 3 x-y-z scale values

	int		brokenLimbs;

	int		boltToPlayer; //set to index of a real client+1 to bolt the ent to that client. Must be a real client, NOT an NPC.

	//for looking at an entity's origin (NPCs and players)
	qboolean	hasLookTarget;
	int			lookTarget;

	int			customRGBA[4];

	//I didn't want to do this, but I.. have no choice. However, we aren't setting this for all ents or anything,
	//only ones we want health knowledge about on cgame (like siege objective breakables) -rww
	int			health;
	int			maxhealth; //so I know how to draw the stupid health bar

	//NPC-SPECIFIC FIELDS
	//------------------------------------------------------------
	int		npcSaber1;
	int		npcSaber2;

	//index values for each type of sound, gets the folder the sounds
	//are in. I wish there were a better way to do this,
	int		csSounds_Std;
	int		csSounds_Combat;
	int		csSounds_Extra;
	int		csSounds_Jedi;

	int		surfacesOn; //a bitflag of corresponding surfaces from a lookup table. These surfaces will be forced on.
	int		surfacesOff; //same as above, but forced off instead.

	//Allow up to 4 PCJ lookup values to be stored here.
	//The resolve to configstrings which contain the name of the
	//desired bone.
	int		boneIndex1;
	int		boneIndex2;
	int		boneIndex3;
	int		boneIndex4;

	//packed with x, y, z orientations for bone angles
	int		boneOrient;

	//I.. feel bad for doing this, but NPCs really just need to
	//be able to control this sort of thing from the server sometimes.
	//At least it's at the end so this stuff is never going to get sent
	//over for anything that isn't an NPC.
	vec3_t	boneAngles1; //angles of boneIndex1
	vec3_t	boneAngles2; //angles of boneIndex2
	vec3_t	boneAngles3; //angles of boneIndex3
	vec3_t	boneAngles4; //angles of boneIndex4

	int		NPC_class; //we need to see what it is on the client for a few effects.

	//If non-0, this is the index of the vehicle a player/NPC is riding.
	int		m_iVehicleNum;

	//rww - spare values specifically for use by mod authors.
	//See netf_overrides.txt if you want to increase the send
	//amount of any of these above 1 bit.
	int			userInt1;
	int			userInt2;
	int			userInt3;
	float		userFloat1;
	float		userFloat2;
	float		userFloat3;
	vec3_t		userVec1;
	vec3_t		userVec2;
} entityState_t;

#else
// Now, XBOX version with members packed in tightly to save gobs of memory
// This is rather confusing. All members are in 1, 2, or 4 bytes, and then
// re-ordered within the structure to keep everything aligned.

#pragma pack(push, 1)

typedef struct entityState_s {
	// Large (32-bit) fields first

	int		number;			// entity index
	int		eFlags;

	trajectory_t	pos;	// for calculating position
	trajectory_t	apos;	// for calculating angles

	int		time;
	int		time2;

	vec3_t	origin;
	vec3_t	origin2;

	vec3_t	angles;
	vec3_t	angles2;

	float	speed;

	int		genericenemyindex;

	int		emplacedOwner;

	int		constantLight;	// r + (g<<8) + (b<<16) + (intensity<<24)
	int		forcePowersActive;

	int		solid;			// for client side prediction, trap_linkentity sets this properly

	byte	customRGBA[4];

	int		surfacesOn; //a bitflag of corresponding surfaces from a lookup table. These surfaces will be forced on.
	int		surfacesOff; //same as above, but forced off instead.

	//I.. feel bad for doing this, but NPCs really just need to
	//be able to control this sort of thing from the server sometimes.
	//At least it's at the end so this stuff is never going to get sent
	//over for anything that isn't an NPC.
	vec3_t	boneAngles1; //angles of boneIndex1
	vec3_t	boneAngles2; //angles of boneIndex2
	vec3_t	boneAngles3; //angles of boneIndex3
	vec3_t	boneAngles4; //angles of boneIndex4


	// Now, the 16-bit members


	word	bolt2;
	word	trickedentindex; //0-15

	word	trickedentindex2; //16-32
	word	trickedentindex3; //33-48

	word	trickedentindex4; //49-64
	word	otherEntityNum;	// shotgun sources, etc

	word	otherEntityNum2;
	word	groundEntityNum;	// -1 = in air

	short	modelindex;
	word	clientNum;		// 0 to (MAX_CLIENTS - 1), for players and corpses

	word	frame;
	word	saberEntityNum;

	word	event;			// impulse events -- muzzle flashes, footsteps, etc
	word	owner; // so crosshair knows what it's looking at

	word	powerups;		// bit flags
	word	legsAnim;

	word	torsoAnim;
	word	forceFrame;		//if non-zero, force the anim frame

	word	ragAttach; //attach to ent while ragging
	short	iModelScale; //rww - transfer a percentage of the normal scale in a single int instead of 3 x-y-z scale values

	word	lookTarget;
	word	health;

	word	maxhealth; //so I know how to draw the stupid health bar
	word	npcSaber1;

	word	npcSaber2;
	word	boneOrient; //packed with x, y, z orientations for bone angles

	//If non-0, this is the index of the vehicle a player/NPC is riding.
	word	m_iVehicleNum;


	// Now, the 8-bit members. These start out two bytes off, thanks to the above word


	byte	eType;			// entityType_t
	byte	eFlags2;		// EF2_??? used much less frequently

	byte	bolt1;
	byte	fireflag;
	byte	activeForcePass;
	byte	loopSound;		// constantly loop this sound

	byte	loopIsSoundset; //qtrue if the loopSound index is actually a soundset index
	byte	soundSetIndex;
	byte	modelGhoul2;
	byte	g2radius;

	byte	modelindex2;
	byte	saberInFlight;
	byte	saberMove;
	byte	isJediMaster;
	byte	saberHolstered;//sent in only 2 bytes, should be 0, 1 or 2

	byte	isPortalEnt; //this needs to be seperate for all entities I guess, which is why I couldn't reuse another value.
	byte	eventParm;
	byte	teamowner;
	byte	shouldtarget;

	byte	weapon;			// determines weapon and flash model, etc
	byte	legsFlip; //set to opposite when the same anim needs restarting, sent over in only 1 bit. Cleaner and makes porting easier than having that god forsaken ANIM_TOGGLEBIT.
	byte	torsoFlip;
	byte	generic1;

	byte	heldByClient; //can only be a client index - this client should be holding onto my arm using IK stuff.
	byte	brokenLimbs;
	byte	boltToPlayer; //set to index of a real client+1 to bolt the ent to that client. Must be a real client, NOT an NPC.
	byte	hasLookTarget; //for looking at an entity's origin (NPCs and players)

	//index values for each type of sound, gets the folder the sounds
	//are in. I wish there were a better way to do this,
	byte	csSounds_Std;
	byte	csSounds_Combat;
	byte	csSounds_Extra;
	byte	csSounds_Jedi;

	//Allow up to 4 PCJ lookup values to be stored here.
	//The resolve to configstrings which contain the name of the
	//desired bone.
	byte	boneIndex1;
	byte	boneIndex2;
	byte	boneIndex3;
	byte	boneIndex4;

	byte	NPC_class; //we need to see what it is on the client for a few effects.
	byte	alignPad[3];
} entityState_t;

#pragma pack(pop)

#endif

// playerState_t is a full superset of entityState_t as it is used by players,
// so if a playerState_t is transmitted, the entityState_t can be fully derived
// from it.
typedef struct playerState_s {
	int			commandTime;	// cmd->serverTime of last executed command
	int			pm_type;
	int			bobCycle;		// for view bobbing and footstep generation
	int			pm_flags;		// ducked, jump_held, etc
	int			pm_time;

	vec3_t		origin;
	vec3_t		velocity;

	vec3_t		moveDir; //NOT sent over the net - nor should it be.

	int			weaponTime;
	int			weaponChargeTime;
	int			weaponChargeSubtractTime;
	int			gravity;
	float		speed;
	int			basespeed; //used in prediction to know base server g_speed value when modifying speed between updates
	int			delta_angles[3];	// add to command angles to get view direction
									// changed by spawns, rotating objects, and teleporters

	int			slopeRecalcTime; //this is NOT sent across the net and is maintained seperately on game and cgame in pmove code.

	int			useTime;

	int			groundEntityNum;// ENTITYNUM_NONE = in air

	int			legsTimer;		// don't change low priority animations until this runs out
	int			legsAnim;

	int			torsoTimer;		// don't change low priority animations until this runs out
	int			torsoAnim;

	qboolean	legsFlip; //set to opposite when the same anim needs restarting, sent over in only 1 bit. Cleaner and makes porting easier than having that god forsaken ANIM_TOGGLEBIT.
	qboolean	torsoFlip;

	int			movementDir;	// a number 0 to 7 that represents the reletive angle
								// of movement to the view angle (axial and diagonals)
								// when at rest, the value will remain unchanged
								// used to twist the legs during strafing

	int			eFlags;			// copied to entityState_t->eFlags
	int			eFlags2;		// copied to entityState_t->eFlags2, EF2_??? used much less frequently

	int			eventSequence;	// pmove generated events
	int			events[MAX_PS_EVENTS];
	int			eventParms[MAX_PS_EVENTS];

	int			externalEvent;	// events set on player from another source
	int			externalEventParm;
	int			externalEventTime;

	int			clientNum;		// ranges from 0 to MAX_CLIENTS-1
	int			weapon;			// copied to entityState_t->weapon
	int			weaponstate;

	vec3_t		viewangles;		// for fixed views
	int			viewheight;

	// damage feedback
	int			damageEvent;	// when it changes, latch the other parms
	int			damageYaw;
	int			damagePitch;
	int			damageCount;
	int			damageType;

	int			painTime;		// used for both game and client side to process the pain twitch - NOT sent across the network
	int			painDirection;	// NOT sent across the network
	float		yawAngle;		// NOT sent across the network
	qboolean	yawing;			// NOT sent across the network
	float		pitchAngle;		// NOT sent across the network
	qboolean	pitching;		// NOT sent across the network

	int			stats[MAX_STATS];
	int			persistant[MAX_PERSISTANT];	// stats that aren't cleared on death
	int			powerups[MAX_POWERUPS];	// level.time that the powerup runs out
	int			ammo[MAX_WEAPONS];

	int			generic1;
	int			loopSound;
	int			jumppad_ent;	// jumppad entity hit this frame

	// not communicated over the net at all
	int			ping;			// server to game info for scoreboard
	int			pmove_framecount;	// FIXME: don't transmit over the network
	int			jumppad_frame;
	int			entityEventSequence;

	int			lastOnGround;	//last time you were on the ground

	qboolean	saberInFlight;

	int			saberMove;
	int			saberBlocking;
	int			saberBlocked;

	int			saberLockTime;
	int			saberLockEnemy;
	int			saberLockFrame; //since we don't actually have the ability to get the current anim frame
	int			saberLockHits; //every x number of buttons hits, allow one push forward in a saber lock (server only)
	int			saberLockHitCheckTime; //so we don't allow more than 1 push per server frame
	int			saberLockHitIncrementTime; //so we don't add a hit per attack button press more than once per server frame
	qboolean	saberLockAdvance; //do an advance (sent across net as 1 bit)

	int			saberEntityNum;
	float		saberEntityDist;
	int			saberEntityState;
	int			saberThrowDelay;
	qboolean	saberCanThrow;
	int			saberDidThrowTime;
	int			saberDamageDebounceTime;
	int			saberHitWallSoundDebounceTime;
	int			saberEventFlags;

	int			rocketLockIndex;
	float		rocketLastValidTime;
	float		rocketLockTime;
	float		rocketTargetTime;

	int			emplacedIndex;
	float		emplacedTime;

	qboolean	isJediMaster;
	qboolean	forceRestricted;
	qboolean	trueJedi;
	qboolean	trueNonJedi;
	int			saberIndex;

	int			genericEnemyIndex;
	float		droneFireTime;
	float		droneExistTime;

	int			activeForcePass;

	qboolean	hasDetPackPlanted; //better than taking up an eFlag isn't it?

	float		holocronsCarried[NUM_FORCE_POWERS];
	int			holocronCantTouch;
	float		holocronCantTouchTime; //for keeping track of the last holocron that just popped out of me (if any)
	int			holocronBits;

	int			electrifyTime;

	int			saberAttackSequence;
	int			saberIdleWound;
	int			saberAttackWound;
	int			saberBlockTime;

	int			otherKiller;
	int			otherKillerTime;
	int			otherKillerDebounceTime;

	forcedata_t	fd;
	qboolean	forceJumpFlip;
	int			forceHandExtend;
	int			forceHandExtendTime;

	int			forceRageDrainTime;

	int			forceDodgeAnim;
	qboolean	quickerGetup;

	int			groundTime;		// time when first left ground

	int			footstepTime;

	int			otherSoundTime;
	float		otherSoundLen;

	int			forceGripMoveInterval;
	int			forceGripChangeMovetype;

	int			forceKickFlip;

	int			duelIndex;
	int			duelTime;
	qboolean	duelInProgress;

	int			saberAttackChainCount;

	int			saberHolstered;

	int			forceAllowDeactivateTime;

	// zoom key
	int			zoomMode;		// 0 - not zoomed, 1 - disruptor weapon
	int			zoomTime;
	qboolean	zoomLocked;
	float		zoomFov;
	int			zoomLockTime;

	int			fallingToDeath;

	int			useDelay;

	qboolean	inAirAnim;

	vec3_t		lastHitLoc;

	int			heldByClient; //can only be a client index - this client should be holding onto my arm using IK stuff.

	int			ragAttach; //attach to ent while ragging

	int			iModelScale;

	int			brokenLimbs;

	//for looking at an entity's origin (NPCs and players)
	qboolean	hasLookTarget;
	int			lookTarget;

	int			customRGBA[4];

	int			standheight;
	int			crouchheight;

	//If non-0, this is the index of the vehicle a player/NPC is riding.
	int			m_iVehicleNum;

	//lovely hack for keeping vehicle orientation in sync with prediction
	vec3_t		vehOrientation;
	qboolean	vehBoarding;
	int			vehSurfaces;

	//vehicle turnaround stuff (need this in ps so it doesn't jerk too much in prediction)
	int			vehTurnaroundIndex;
	int			vehTurnaroundTime;

	//vehicle has weapons linked
	qboolean	vehWeaponsLinked;

	//when hyperspacing, you just go forward really fast for HYPERSPACE_TIME
	int			hyperSpaceTime;
	vec3_t		hyperSpaceAngles;

	//hacking when > time
	int			hackingTime;
	//actual hack amount - only for the proper percentage display when
	//drawing progress bar (is there a less bandwidth-eating way to do
	//this without a lot of hassle?)
	int			hackingBaseTime;

	//keeps track of jetpack fuel
	int			jetpackFuel;

	//keeps track of cloak fuel
	int			cloakFuel;

	//rww - spare values specifically for use by mod authors.
	//See psf_overrides.txt if you want to increase the send
	//amount of any of these above 1 bit.
#ifndef _XBOX
	int			userInt1;
	int			userInt2;
	int			userInt3;
	float		userFloat1;
	float		userFloat2;
	float		userFloat3;
	vec3_t		userVec1;
	vec3_t		userVec2;
#endif

#ifdef _ONEBIT_COMBO
	int			deltaOneBits;
	int			deltaNumBits;
#endif
} playerState_t;


typedef struct {
	const char* name;
	int		offset;
#ifdef _XBOX
	int		realSize;	// in bytes (1, 2, 4)
#endif
	int		bits;		// 0 = float
#ifndef FINAL_BUILD
	unsigned	mCount;
#endif

} netField_t;


// using the stringizing operator to save typing...
#ifdef _XBOX
#define	NETF(x) #x,(int)&((entityState_t*)0)->x,sizeof(((entityState_t*)0)->x)
#else
#define	NETF(x) #x,(int)&((entityState_t*)0)->x
#endif

//rww - Remember to update ext_data/MP/netf_overrides.txt if you change any of this!
//(for the sake of being consistent)

// BTO - This was mis-documented before. We do allow datatypes less than 32 bits on Xbox
// now, but our macros and such handle it all automagically. No need to be anal about
// keeping q_shared.h in sync with this.
netField_t	entityStateFields[] =
{
{ NETF(pos.trTime), 32 },
{ NETF(pos.trBase[1]), 0 },
{ NETF(pos.trBase[0]), 0 },
{ NETF(apos.trBase[1]), 0 },
{ NETF(pos.trBase[2]), 0 },
{ NETF(apos.trBase[0]), 0 },
{ NETF(pos.trDelta[0]), 0 },
{ NETF(pos.trDelta[1]), 0 },
{ NETF(eType), 8 },
{ NETF(angles[1]), 0 },
{ NETF(pos.trDelta[2]), 0 },
{ NETF(origin[0]), 0 },
{ NETF(origin[1]), 0 },
{ NETF(origin[2]), 0 },
// does this need to be 8 bits?
{ NETF(weapon), 8 },
{ NETF(apos.trType), 8 },
// changed from 12 to 16
{ NETF(legsAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
// suspicious
{ NETF(torsoAnim), 16 },		// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
// large use beyond GENTITYNUM_BITS - should use generic1 insead
{ NETF(genericenemyindex), 32 }, //Do not change to GENTITYNUM_BITS, used as a time offset for seeker
{ NETF(eFlags), 32 },
{ NETF(pos.trDuration), 32 },
// might be able to reduce
{ NETF(teamowner), 8 },
{ NETF(groundEntityNum), GENTITYNUM_BITS },
{ NETF(pos.trType), 8 },
{ NETF(angles[2]), 0 },
{ NETF(angles[0]), 0 },
{ NETF(solid), 24 },
// flag states barely used - could be moved elsewhere
{ NETF(fireflag), 2 },
{ NETF(event), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
// used mostly for players and npcs - appears to be static / never changing
{ NETF(customRGBA[3]), 8 }, //0-255
// used mostly for players and npcs - appears to be static / never changing
{ NETF(customRGBA[0]), 8 }, //0-255
// only used in fx system (which rick did) and chunks
{ NETF(speed), 0 },
// why are npc's clientnum's that big?
{ NETF(clientNum), GENTITYNUM_BITS }, //with npc's clientnum can be > MAX_CLIENTS so use entnum bits now instead.
{ NETF(apos.trBase[2]), 0 },
{ NETF(apos.trTime), 32 },
// used mostly for players and npcs - appears to be static / never changing
{ NETF(customRGBA[1]), 8 }, //0-255
// used mostly for players and npcs - appears to be static / never changing
{ NETF(customRGBA[2]), 8 }, //0-255
// multiple meanings
{ NETF(saberEntityNum), GENTITYNUM_BITS },
// could probably just eliminate and assume a big number
{ NETF(g2radius), 8 },
{ NETF(otherEntityNum2), GENTITYNUM_BITS },
// used all over the place
{ NETF(owner), GENTITYNUM_BITS },
{ NETF(modelindex2), 8 },
// why was this changed from 0 to 8 ?
{ NETF(eventParm), 8 },
// unknown about size?
{ NETF(saberMove), 8 },
{ NETF(apos.trDelta[1]), 0 },
{ NETF(boneAngles1[1]), 0 },
// why raised from 8 to -16?
{ NETF(modelindex), -16 },
// barely used, could probably be replaced
{ NETF(emplacedOwner), 32 }, //As above, also used as a time value (for electricity render time)
{ NETF(apos.trDelta[0]), 0 },
{ NETF(apos.trDelta[2]), 0 },
// shouldn't these be better off as flags?  otherwise, they may consume more bits this way
{ NETF(torsoFlip), 1 },
{ NETF(angles2[1]), 0 },
// used mostly in saber and npc
{ NETF(lookTarget), GENTITYNUM_BITS },
{ NETF(origin2[2]), 0 },
// randomly used, not sure why this was used instead of svc_noclient
//	if (cent->currentState.modelGhoul2 == 127)
//	{ //not ready to be drawn or initialized..
//		return;
//	}
{ NETF(modelGhoul2), 8 },
{ NETF(loopSound), 8 },
{ NETF(origin2[0]), 0 },
// multiple purpose bit flag
{ NETF(shouldtarget), 1 },
// widely used, does not appear that they have to be 16 bits
{ NETF(trickedentindex), 16 }, //See note in PSF
{ NETF(otherEntityNum), GENTITYNUM_BITS },
{ NETF(origin2[1]), 0 },
{ NETF(time2), 32 },
{ NETF(legsFlip), 1 },
// fully used
{ NETF(bolt2), GENTITYNUM_BITS },
{ NETF(constantLight), 32 },
{ NETF(time), 32 },
// why doesn't lookTarget just indicate this?
{ NETF(hasLookTarget), 1 },
{ NETF(boneAngles1[2]), 0 },
// used for both force pass and an emplaced gun - gun is just a flag indicator
{ NETF(activeForcePass), 6 },
// used to indicate health
{ NETF(health), 10 }, //if something's health exceeds 1024, then.. too bad!
// appears to have multiple means, could be eliminated by indicating a sound set differently
{ NETF(loopIsSoundset), 1 },
{ NETF(saberHolstered), 2 },
//NPC-SPECIFIC:
// both are used for NPCs sabers, though limited
{ NETF(npcSaber1), 9 },
{ NETF(maxhealth), 10 },
{ NETF(trickedentindex2), 16 },
// appear to only be 18 powers?
{ NETF(forcePowersActive), 32 },
// used, doesn't appear to be flexible
{ NETF(iModelScale), 10 }, //0-1024 (guess it's gotta be increased if we want larger allowable scale.. but 1024% is pretty big)
// full bits used
{ NETF(powerups), 16 },
// can this be reduced?
{ NETF(soundSetIndex), 8 }, //rww - if MAX_AMBIENT_SETS is changed from 256, REMEMBER TO CHANGE THIS
// looks like this can be reduced to 4? (ship parts = 4, people parts = 2)
{ NETF(brokenLimbs), 8 }, //up to 8 limbs at once (not that that many are used)
{ NETF(csSounds_Std), 8 }, //soundindex must be 8 unless max sounds is changed
// used extensively
{ NETF(saberInFlight), 1 },
{ NETF(angles2[0]), 0 },
{ NETF(frame), 16 },
{ NETF(angles2[2]), 0 },
// why not use torsoAnim and set a flag to do the same thing as forceFrame (saberLockFrame)
{ NETF(forceFrame), 16 }, //if you have over 65536 frames, then this will explode. Of course if you have that many things then lots of things will probably explode.
{ NETF(generic1), 8 },
// do we really need 4 indexes?
{ NETF(boneIndex1), 6 }, //up to 64 bones can be accessed by this indexing method
// only 54 classes, could cut down 2 bits
{ NETF(NPC_class), 8 },
{ NETF(apos.trDuration), 32 },
// there appears to be only 2 different version of parms passed - a flag would better be suited
{ NETF(boneOrient), 9 }, //3 bits per orientation dir
// this looks to be a single bit flag
{ NETF(bolt1), 8 },
{ NETF(trickedentindex3), 16 },
// in use for vehicles
{ NETF(m_iVehicleNum), GENTITYNUM_BITS }, // 10 bits fits all possible entity nums (2^10 = 1024). - AReis
{ NETF(trickedentindex4), 16 },
// but why is there an opposite state of surfaces field?
{ NETF(surfacesOff), 32 },
{ NETF(eFlags2), 10 },
// should be bit field
{ NETF(isJediMaster), 1 },
// should be bit field
{ NETF(isPortalEnt), 1 },
// possible multiple definitions
{ NETF(heldByClient), 6 },
// this does not appear to be used in any production or non-cheat fashion - REMOVE
{ NETF(ragAttach), GENTITYNUM_BITS },
// used only in one spot for seige
{ NETF(boltToPlayer), 6 },
{ NETF(npcSaber2), 9 },
{ NETF(csSounds_Combat), 8 },
{ NETF(csSounds_Extra), 8 },
{ NETF(csSounds_Jedi), 8 },
// used only for surfaces on NPCs
{ NETF(surfacesOn), 32 }, //allow up to 32 surfaces in the bitflag
{ NETF(boneIndex2), 6 },
{ NETF(boneIndex3), 6 },
{ NETF(boneIndex4), 6 },
{ NETF(boneAngles1[0]), 0 },
{ NETF(boneAngles2[0]), 0 },
{ NETF(boneAngles2[1]), 0 },
{ NETF(boneAngles2[2]), 0 },
{ NETF(boneAngles3[0]), 0 },
{ NETF(boneAngles3[1]), 0 },
{ NETF(boneAngles3[2]), 0 },
{ NETF(boneAngles4[0]), 0 },
{ NETF(boneAngles4[1]), 0 },
{ NETF(boneAngles4[2]), 0 },

//rww - for use by mod authors only
#ifndef _XBOX
{ NETF(userInt1), 1 },
{ NETF(userInt2), 1 },
{ NETF(userInt3), 1 },
{ NETF(userFloat1), 1 },
{ NETF(userFloat2), 1 },
{ NETF(userFloat3), 1 },
{ NETF(userVec1[0]), 1 },
{ NETF(userVec1[1]), 1 },
{ NETF(userVec1[2]), 1 },
{ NETF(userVec2[0]), 1 },
{ NETF(userVec2[1]), 1 },
{ NETF(userVec2[2]), 1 }
#endif
};

#define _OPTIMIZED_VEHICLE_NETWORKING



#ifdef _XBOX
#define	PSF(x) #x,(int)&((playerState_t*)0)->x,sizeof(((playerState_t*)0)->x)
#else
#define	PSF(x) #x,(int)&((playerState_t*)0)->x
#endif

//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================
#ifdef _OPTIMIZED_VEHICLE_NETWORKING
//Instead of sending 2 full playerStates for the pilot and the vehicle, send a smaller,
//specialized pilot playerState and vehicle playerState.  Also removes some vehicle
//fields from the normal playerState -mcg
//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================




netField_t	playerStateFields[] =
{
{ PSF(commandTime), 32 },
{ PSF(origin[1]), 0 },
{ PSF(origin[0]), 0 },
{ PSF(viewangles[1]), 0 },
{ PSF(viewangles[0]), 0 },
{ PSF(origin[2]), 0 },
{ PSF(velocity[0]), 0 },
{ PSF(velocity[1]), 0 },
{ PSF(velocity[2]), 0 },
{ PSF(bobCycle), 8 },
{ PSF(weaponTime), -16 },
{ PSF(delta_angles[1]), 16 },
{ PSF(speed), 0 }, //sadly, the vehicles require negative speed values, so..
{ PSF(legsAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(delta_angles[0]), 16 },
{ PSF(torsoAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(groundEntityNum), GENTITYNUM_BITS },
{ PSF(eFlags), 32 },
{ PSF(fd.forcePower), 8 },
{ PSF(eventSequence), 16 },
{ PSF(torsoTimer), 16 },
{ PSF(legsTimer), 16 },
{ PSF(viewheight), -8 },
{ PSF(fd.saberAnimLevel), 4 },
{ PSF(rocketLockIndex), GENTITYNUM_BITS },
{ PSF(fd.saberDrawAnimLevel), 4 },
{ PSF(genericEnemyIndex), 32 }, //NOTE: This isn't just an index all the time, it's often used as a time value, and thus needs 32 bits
{ PSF(events[0]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(events[1]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(customRGBA[0]), 8 }, //0-255
{ PSF(movementDir), 4 },
{ PSF(saberEntityNum), GENTITYNUM_BITS }, //Also used for channel tracker storage, but should never exceed entity number
{ PSF(customRGBA[3]), 8 }, //0-255
{ PSF(weaponstate), 4 },
{ PSF(saberMove), 32 }, //This value sometimes exceeds the max LS_ value and gets set to a crazy amount, so it needs 32 bits
{ PSF(standheight), 10 },
{ PSF(crouchheight), 10 },
{ PSF(basespeed), -16 },
{ PSF(pm_flags), 16 },
{ PSF(jetpackFuel), 8 },
{ PSF(cloakFuel), 8 },
{ PSF(pm_time), -16 },
{ PSF(customRGBA[1]), 8 }, //0-255
{ PSF(clientNum), GENTITYNUM_BITS },
{ PSF(duelIndex), GENTITYNUM_BITS },
{ PSF(customRGBA[2]), 8 }, //0-255
{ PSF(gravity), 16 },
{ PSF(weapon), 8 },
{ PSF(delta_angles[2]), 16 },
{ PSF(saberCanThrow), 1 },
{ PSF(viewangles[2]), 0 },
{ PSF(fd.forcePowersKnown), 32 },
{ PSF(fd.forcePowerLevel[FP_LEVITATION]), 2 }, //unfortunately we need this for fall damage calculation (client needs to know the distance for the fall noise)
{ PSF(fd.forcePowerDebounce[FP_LEVITATION]), 32 },
{ PSF(fd.forcePowerSelected), 8 },
{ PSF(torsoFlip), 1 },
{ PSF(externalEvent), 10 },
{ PSF(damageYaw), 8 },
{ PSF(damageCount), 8 },
{ PSF(inAirAnim), 1 }, //just transmit it for the sake of knowing right when on the client to play a land anim, it's only 1 bit
{ PSF(eventParms[1]), 8 },
{ PSF(fd.forceSide), 2 }, //so we know if we should apply greyed out shaders to dark/light force enlightenment
{ PSF(saberAttackChainCount), 4 },
{ PSF(pm_type), 8 },
{ PSF(externalEventParm), 8 },
{ PSF(eventParms[0]), -16 },
{ PSF(lookTarget), GENTITYNUM_BITS },
//{ PSF(vehOrientation[0]), 0 },
{ PSF(weaponChargeSubtractTime), 32 }, //? really need 32 bits??
//{ PSF(vehOrientation[1]), 0 },
//{ PSF(moveDir[1]), 0 },
//{ PSF(moveDir[0]), 0 },
{ PSF(weaponChargeTime), 32 }, //? really need 32 bits??
//{ PSF(vehOrientation[2]), 0 },
{ PSF(legsFlip), 1 },
{ PSF(damageEvent), 8 },
//{ PSF(moveDir[2]), 0 },
{ PSF(rocketTargetTime), 32 },
{ PSF(activeForcePass), 6 },
{ PSF(electrifyTime), 32 },
{ PSF(fd.forceJumpZStart), 0 },
{ PSF(loopSound), 16 }, //rwwFIXMEFIXME: max sounds is 256, doesn't this only need to be 8?
{ PSF(hasLookTarget), 1 },
{ PSF(saberBlocked), 8 },
{ PSF(damageType), 2 },
{ PSF(rocketLockTime), 32 },
{ PSF(forceHandExtend), 8 },
{ PSF(saberHolstered), 2 },
{ PSF(fd.forcePowersActive), 32 },
{ PSF(damagePitch), 8 },
{ PSF(m_iVehicleNum), GENTITYNUM_BITS }, // 10 bits fits all possible entity nums (2^10 = 1024). - AReis
//{ PSF(vehTurnaroundTime), 32 },//only used by vehicle?
{ PSF(generic1), 8 },
{ PSF(jumppad_ent), 10 },
{ PSF(hasDetPackPlanted), 1 },
{ PSF(saberInFlight), 1 },
{ PSF(forceDodgeAnim), 16 },
{ PSF(zoomMode), 2 }, // NOTENOTE Are all of these necessary?
{ PSF(hackingTime), 32 },
{ PSF(zoomTime), 32 },	// NOTENOTE Are all of these necessary?
{ PSF(brokenLimbs), 8 }, //up to 8 limbs at once (not that that many are used)
{ PSF(zoomLocked), 1 },	// NOTENOTE Are all of these necessary?
{ PSF(zoomFov), 0 },	// NOTENOTE Are all of these necessary?
{ PSF(fd.forceRageRecoveryTime), 32 },
{ PSF(fallingToDeath), 32 },
{ PSF(fd.forceMindtrickTargetIndex), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.forceMindtrickTargetIndex2), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
//{ PSF(vehWeaponsLinked), 1 },//only used by vehicle?
{ PSF(lastHitLoc[2]), 0 },
//{ PSF(hyperSpaceTime), 32 },//only used by vehicle?
{ PSF(fd.forceMindtrickTargetIndex3), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(lastHitLoc[0]), 0 },
{ PSF(eFlags2), 10 },
{ PSF(fd.forceMindtrickTargetIndex4), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
//{ PSF(hyperSpaceAngles[1]), 0 },//only used by vehicle?
{ PSF(lastHitLoc[1]), 0 }, //currently only used so client knows to orient disruptor disintegration.. seems a bit much for just that though.
//{ PSF(vehBoarding), 1 }, //only used by vehicle? not like the normal boarding value, this is a simple "1 or 0" value
{ PSF(fd.sentryDeployed), 1 },
{ PSF(saberLockTime), 32 },
{ PSF(saberLockFrame), 16 },
//{ PSF(vehTurnaroundIndex), GENTITYNUM_BITS },//only used by vehicle?
//{ PSF(vehSurfaces), 16 }, //only used by vehicle? allow up to 16 surfaces in the flag I guess
{ PSF(fd.forcePowerLevel[FP_SEE]), 2 }, //needed for knowing when to display players through walls
{ PSF(saberLockEnemy), GENTITYNUM_BITS },
{ PSF(fd.forceGripCripple), 1 }, //should only be 0 or 1 ever
{ PSF(emplacedIndex), GENTITYNUM_BITS },
{ PSF(holocronBits), 32 },
{ PSF(isJediMaster), 1 },
{ PSF(forceRestricted), 1 },
{ PSF(trueJedi), 1 },
{ PSF(trueNonJedi), 1 },
{ PSF(duelTime), 32 },
{ PSF(duelInProgress), 1 },
{ PSF(saberLockAdvance), 1 },
{ PSF(heldByClient), 6 },
{ PSF(ragAttach), GENTITYNUM_BITS },
{ PSF(iModelScale), 10 }, //0-1024 (guess it's gotta be increased if we want larger allowable scale.. but 1024% is pretty big)
{ PSF(hackingBaseTime), 16 }, //up to 65536ms, over 10 seconds would just be silly anyway
//{ PSF(hyperSpaceAngles[0]), 0 },//only used by vehicle?
//{ PSF(hyperSpaceAngles[2]), 0 },//only used by vehicle?

//rww - for use by mod authors only
#ifndef _XBOX
{ PSF(userInt1), 1 },
{ PSF(userInt2), 1 },
{ PSF(userInt3), 1 },
{ PSF(userFloat1), 1 },
{ PSF(userFloat2), 1 },
{ PSF(userFloat3), 1 },
{ PSF(userVec1[0]), 1 },
{ PSF(userVec1[1]), 1 },
{ PSF(userVec1[2]), 1 },
{ PSF(userVec2[0]), 1 },
{ PSF(userVec2[1]), 1 },
{ PSF(userVec2[2]), 1 }
#endif
};

netField_t	pilotPlayerStateFields[] =
{
{ PSF(commandTime), 32 },
{ PSF(origin[1]), 0 },
{ PSF(origin[0]), 0 },
{ PSF(viewangles[1]), 0 },
{ PSF(viewangles[0]), 0 },
{ PSF(origin[2]), 0 },
{ PSF(weaponTime), -16 },
{ PSF(delta_angles[1]), 16 },
{ PSF(delta_angles[0]), 16 },
{ PSF(eFlags), 32 },
{ PSF(eventSequence), 16 },
{ PSF(rocketLockIndex), GENTITYNUM_BITS },
{ PSF(events[0]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(events[1]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(weaponstate), 4 },
{ PSF(pm_flags), 16 },
{ PSF(pm_time), -16 },
{ PSF(clientNum), GENTITYNUM_BITS },
{ PSF(weapon), 8 },
{ PSF(delta_angles[2]), 16 },
{ PSF(viewangles[2]), 0 },
{ PSF(externalEvent), 10 },
{ PSF(eventParms[1]), 8 },
{ PSF(pm_type), 8 },
{ PSF(externalEventParm), 8 },
{ PSF(eventParms[0]), -16 },
{ PSF(weaponChargeSubtractTime), 32 }, //? really need 32 bits??
{ PSF(weaponChargeTime), 32 }, //? really need 32 bits??
{ PSF(rocketTargetTime), 32 },
{ PSF(fd.forceJumpZStart), 0 },
{ PSF(rocketLockTime), 32 },
{ PSF(m_iVehicleNum), GENTITYNUM_BITS }, // 10 bits fits all possible entity nums (2^10 = 1024). - AReis
{ PSF(generic1), 8 },//used by passengers
{ PSF(eFlags2), 10 },

//===THESE SHOULD NOT BE CHANGING OFTEN====================================================================
{ PSF(legsAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(torsoAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(torsoTimer), 16 },
{ PSF(legsTimer), 16 },
{ PSF(jetpackFuel), 8 },
{ PSF(cloakFuel), 8 },
{ PSF(saberCanThrow), 1 },
{ PSF(fd.forcePowerDebounce[FP_LEVITATION]), 32 },
{ PSF(torsoFlip), 1 },
{ PSF(legsFlip), 1 },
{ PSF(fd.forcePowersActive), 32 },
{ PSF(hasDetPackPlanted), 1 },
{ PSF(fd.forceRageRecoveryTime), 32 },
{ PSF(saberInFlight), 1 },
{ PSF(fd.forceMindtrickTargetIndex), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.forceMindtrickTargetIndex2), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.forceMindtrickTargetIndex3), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.forceMindtrickTargetIndex4), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.sentryDeployed), 1 },
{ PSF(fd.forcePowerLevel[FP_SEE]), 2 }, //needed for knowing when to display players through walls
{ PSF(holocronBits), 32 },
{ PSF(fd.forcePower), 8 },

//===THE REST OF THESE SHOULD NOT BE RELEVANT, BUT, FOR SAFETY, INCLUDE THEM ANYWAY, JUST AT THE BOTTOM===============================================================
{ PSF(velocity[0]), 0 },
{ PSF(velocity[1]), 0 },
{ PSF(velocity[2]), 0 },
{ PSF(bobCycle), 8 },
{ PSF(speed), 0 }, //sadly, the vehicles require negative speed values, so..
{ PSF(groundEntityNum), GENTITYNUM_BITS },
{ PSF(viewheight), -8 },
{ PSF(fd.saberAnimLevel), 4 },
{ PSF(fd.saberDrawAnimLevel), 4 },
{ PSF(genericEnemyIndex), 32 }, //NOTE: This isn't just an index all the time, it's often used as a time value, and thus needs 32 bits
{ PSF(customRGBA[0]), 8 }, //0-255
{ PSF(movementDir), 4 },
{ PSF(saberEntityNum), GENTITYNUM_BITS }, //Also used for channel tracker storage, but should never exceed entity number
{ PSF(customRGBA[3]), 8 }, //0-255
{ PSF(saberMove), 32 }, //This value sometimes exceeds the max LS_ value and gets set to a crazy amount, so it needs 32 bits
{ PSF(standheight), 10 },
{ PSF(crouchheight), 10 },
{ PSF(basespeed), -16 },
{ PSF(customRGBA[1]), 8 }, //0-255
{ PSF(duelIndex), GENTITYNUM_BITS },
{ PSF(customRGBA[2]), 8 }, //0-255
{ PSF(gravity), 16 },
{ PSF(fd.forcePowersKnown), 32 },
{ PSF(fd.forcePowerLevel[FP_LEVITATION]), 2 }, //unfortunately we need this for fall damage calculation (client needs to know the distance for the fall noise)
{ PSF(fd.forcePowerSelected), 8 },
{ PSF(damageYaw), 8 },
{ PSF(damageCount), 8 },
{ PSF(inAirAnim), 1 }, //just transmit it for the sake of knowing right when on the client to play a land anim, it's only 1 bit
{ PSF(fd.forceSide), 2 }, //so we know if we should apply greyed out shaders to dark/light force enlightenment
{ PSF(saberAttackChainCount), 4 },
{ PSF(lookTarget), GENTITYNUM_BITS },
{ PSF(moveDir[1]), 0 },
{ PSF(moveDir[0]), 0 },
{ PSF(damageEvent), 8 },
{ PSF(moveDir[2]), 0 },
{ PSF(activeForcePass), 6 },
{ PSF(electrifyTime), 32 },
{ PSF(damageType), 2 },
{ PSF(loopSound), 16 }, //rwwFIXMEFIXME: max sounds is 256, doesn't this only need to be 8?
{ PSF(hasLookTarget), 1 },
{ PSF(saberBlocked), 8 },
{ PSF(forceHandExtend), 8 },
{ PSF(saberHolstered), 2 },
{ PSF(damagePitch), 8 },
{ PSF(jumppad_ent), 10 },
{ PSF(forceDodgeAnim), 16 },
{ PSF(zoomMode), 2 }, // NOTENOTE Are all of these necessary?
{ PSF(hackingTime), 32 },
{ PSF(zoomTime), 32 },	// NOTENOTE Are all of these necessary?
{ PSF(brokenLimbs), 8 }, //up to 8 limbs at once (not that that many are used)
{ PSF(zoomLocked), 1 },	// NOTENOTE Are all of these necessary?
{ PSF(zoomFov), 0 },	// NOTENOTE Are all of these necessary?
{ PSF(fallingToDeath), 32 },
{ PSF(lastHitLoc[2]), 0 },
{ PSF(lastHitLoc[0]), 0 },
{ PSF(lastHitLoc[1]), 0 }, //currently only used so client knows to orient disruptor disintegration.. seems a bit much for just that though.
{ PSF(saberLockTime), 32 },
{ PSF(saberLockFrame), 16 },
{ PSF(saberLockEnemy), GENTITYNUM_BITS },
{ PSF(fd.forceGripCripple), 1 }, //should only be 0 or 1 ever
{ PSF(emplacedIndex), GENTITYNUM_BITS },
{ PSF(isJediMaster), 1 },
{ PSF(forceRestricted), 1 },
{ PSF(trueJedi), 1 },
{ PSF(trueNonJedi), 1 },
{ PSF(duelTime), 32 },
{ PSF(duelInProgress), 1 },
{ PSF(saberLockAdvance), 1 },
{ PSF(heldByClient), 6 },
{ PSF(ragAttach), GENTITYNUM_BITS },
{ PSF(iModelScale), 10 }, //0-1024 (guess it's gotta be increased if we want larger allowable scale.. but 1024% is pretty big)
{ PSF(hackingBaseTime), 16 }, //up to 65536ms, over 10 seconds would just be silly anyway
//===NEVER SEND THESE, ONLY USED BY VEHICLES==============================================================

//{ PSF(vehOrientation[0]), 0 },
//{ PSF(vehOrientation[1]), 0 },
//{ PSF(vehOrientation[2]), 0 },
//{ PSF(vehTurnaroundTime), 32 },//only used by vehicle?
//{ PSF(vehWeaponsLinked), 1 },//only used by vehicle?
//{ PSF(hyperSpaceTime), 32 },//only used by vehicle?
//{ PSF(vehTurnaroundIndex), GENTITYNUM_BITS },//only used by vehicle?
//{ PSF(vehSurfaces), 16 }, //only used by vehicle? allow up to 16 surfaces in the flag I guess
//{ PSF(vehBoarding), 1 }, //only used by vehicle? not like the normal boarding value, this is a simple "1 or 0" value
//{ PSF(hyperSpaceAngles[1]), 0 },//only used by vehicle?
//{ PSF(hyperSpaceAngles[0]), 0 },//only used by vehicle?
//{ PSF(hyperSpaceAngles[2]), 0 },//only used by vehicle?

//rww - for use by mod authors only
#ifndef _XBOX
{ PSF(userInt1), 1 },
{ PSF(userInt2), 1 },
{ PSF(userInt3), 1 },
{ PSF(userFloat1), 1 },
{ PSF(userFloat2), 1 },
{ PSF(userFloat3), 1 },
{ PSF(userVec1[0]), 1 },
{ PSF(userVec1[1]), 1 },
{ PSF(userVec1[2]), 1 },
{ PSF(userVec2[0]), 1 },
{ PSF(userVec2[1]), 1 },
{ PSF(userVec2[2]), 1 }
#endif
};

netField_t	vehPlayerStateFields[] =
{
{ PSF(commandTime), 32 },
{ PSF(origin[1]), 0 },
{ PSF(origin[0]), 0 },
{ PSF(viewangles[1]), 0 },
{ PSF(viewangles[0]), 0 },
{ PSF(origin[2]), 0 },
{ PSF(velocity[0]), 0 },
{ PSF(velocity[1]), 0 },
{ PSF(velocity[2]), 0 },
{ PSF(weaponTime), -16 },
{ PSF(delta_angles[1]), 16 },
{ PSF(speed), 0 }, //sadly, the vehicles require negative speed values, so..
{ PSF(legsAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(delta_angles[0]), 16 },
{ PSF(groundEntityNum), GENTITYNUM_BITS },
{ PSF(eFlags), 32 },
{ PSF(eventSequence), 16 },
{ PSF(legsTimer), 16 },
{ PSF(rocketLockIndex), GENTITYNUM_BITS },
//{ PSF(genericEnemyIndex), 32 }, //NOTE: This isn't just an index all the time, it's often used as a time value, and thus needs 32 bits
{ PSF(events[0]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(events[1]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
//{ PSF(customRGBA[0]), 8 }, //0-255
//{ PSF(movementDir), 4 },
//{ PSF(customRGBA[3]), 8 }, //0-255
{ PSF(weaponstate), 4 },
//{ PSF(basespeed), -16 },
{ PSF(pm_flags), 16 },
{ PSF(pm_time), -16 },
//{ PSF(customRGBA[1]), 8 }, //0-255
{ PSF(clientNum), GENTITYNUM_BITS },
//{ PSF(duelIndex), GENTITYNUM_BITS },
//{ PSF(customRGBA[2]), 8 }, //0-255
{ PSF(gravity), 16 },
{ PSF(weapon), 8 },
{ PSF(delta_angles[2]), 16 },
{ PSF(viewangles[2]), 0 },
{ PSF(externalEvent), 10 },
{ PSF(eventParms[1]), 8 },
{ PSF(pm_type), 8 },
{ PSF(externalEventParm), 8 },
{ PSF(eventParms[0]), -16 },
{ PSF(vehOrientation[0]), 0 },
{ PSF(vehOrientation[1]), 0 },
{ PSF(moveDir[1]), 0 },
{ PSF(moveDir[0]), 0 },
{ PSF(vehOrientation[2]), 0 },
{ PSF(moveDir[2]), 0 },
{ PSF(rocketTargetTime), 32 },
//{ PSF(activeForcePass), 6 },//actually, you only need to know this for other vehicles, not your own
{ PSF(electrifyTime), 32 },
//{ PSF(fd.forceJumpZStart), 0 },//set on rider by vehicle, but not used by vehicle
{ PSF(loopSound), 16 }, //rwwFIXMEFIXME: max sounds is 256, doesn't this only need to be 8?
{ PSF(rocketLockTime), 32 },
{ PSF(m_iVehicleNum), GENTITYNUM_BITS }, // 10 bits fits all possible entity nums (2^10 = 1024). - AReis
{ PSF(vehTurnaroundTime), 32 },
//{ PSF(generic1), 8 },//used by passengers of vehicles, but not vehicles themselves
{ PSF(hackingTime), 32 },
{ PSF(brokenLimbs), 8 }, //up to 8 limbs at once (not that that many are used)
{ PSF(vehWeaponsLinked), 1 },
{ PSF(hyperSpaceTime), 32 },
{ PSF(eFlags2), 10 },
{ PSF(hyperSpaceAngles[1]), 0 },
{ PSF(vehBoarding), 1 }, //not like the normal boarding value, this is a simple "1 or 0" value
{ PSF(vehTurnaroundIndex), GENTITYNUM_BITS },
{ PSF(vehSurfaces), 16 }, //allow up to 16 surfaces in the flag I guess
{ PSF(hyperSpaceAngles[0]), 0 },
{ PSF(hyperSpaceAngles[2]), 0 },

//rww - for use by mod authors only
#ifndef _XBOX
{ PSF(userInt1), 1 },
{ PSF(userInt2), 1 },
{ PSF(userInt3), 1 },
{ PSF(userFloat1), 1 },
{ PSF(userFloat2), 1 },
{ PSF(userFloat3), 1 },
{ PSF(userVec1[0]), 1 },
{ PSF(userVec1[1]), 1 },
{ PSF(userVec1[2]), 1 },
{ PSF(userVec2[0]), 1 },
{ PSF(userVec2[1]), 1 },
{ PSF(userVec2[2]), 1 }
#endif
};

//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================
#else//_OPTIMIZED_VEHICLE_NETWORKING
//The unoptimized way, throw *all* the vehicle stuff into the playerState along with everything else... :(
//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================

netField_t	playerStateFields[] =
{
{ PSF(commandTime), 32 },
{ PSF(origin[1]), 0 },
{ PSF(origin[0]), 0 },
{ PSF(viewangles[1]), 0 },
{ PSF(viewangles[0]), 0 },
{ PSF(origin[2]), 0 },
{ PSF(velocity[0]), 0 },
{ PSF(velocity[1]), 0 },
{ PSF(velocity[2]), 0 },
{ PSF(bobCycle), 8 },
{ PSF(weaponTime), -16 },
{ PSF(delta_angles[1]), 16 },
{ PSF(speed), 0 }, //sadly, the vehicles require negative speed values, so..
{ PSF(legsAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(delta_angles[0]), 16 },
{ PSF(torsoAnim), 16 },			// Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
{ PSF(groundEntityNum), GENTITYNUM_BITS },
{ PSF(eFlags), 32 },
{ PSF(fd.forcePower), 8 },
{ PSF(eventSequence), 16 },
{ PSF(torsoTimer), 16 },
{ PSF(legsTimer), 16 },
{ PSF(viewheight), -8 },
{ PSF(fd.saberAnimLevel), 4 },
{ PSF(rocketLockIndex), GENTITYNUM_BITS },
{ PSF(fd.saberDrawAnimLevel), 4 },
{ PSF(genericEnemyIndex), 32 }, //NOTE: This isn't just an index all the time, it's often used as a time value, and thus needs 32 bits
{ PSF(events[0]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(events[1]), 10 },			// There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
{ PSF(customRGBA[0]), 8 }, //0-255
{ PSF(movementDir), 4 },
{ PSF(saberEntityNum), GENTITYNUM_BITS }, //Also used for channel tracker storage, but should never exceed entity number
{ PSF(customRGBA[3]), 8 }, //0-255
{ PSF(weaponstate), 4 },
{ PSF(saberMove), 32 }, //This value sometimes exceeds the max LS_ value and gets set to a crazy amount, so it needs 32 bits
{ PSF(standheight), 10 },
{ PSF(crouchheight), 10 },
{ PSF(basespeed), -16 },
{ PSF(pm_flags), 16 },
{ PSF(jetpackFuel), 8 },
{ PSF(cloakFuel), 8 },
{ PSF(pm_time), -16 },
{ PSF(customRGBA[1]), 8 }, //0-255
{ PSF(clientNum), GENTITYNUM_BITS },
{ PSF(duelIndex), GENTITYNUM_BITS },
{ PSF(customRGBA[2]), 8 }, //0-255
{ PSF(gravity), 16 },
{ PSF(weapon), 8 },
{ PSF(delta_angles[2]), 16 },
{ PSF(saberCanThrow), 1 },
{ PSF(viewangles[2]), 0 },
{ PSF(fd.forcePowersKnown), 32 },
{ PSF(fd.forcePowerLevel[FP_LEVITATION]), 2 }, //unfortunately we need this for fall damage calculation (client needs to know the distance for the fall noise)
{ PSF(fd.forcePowerDebounce[FP_LEVITATION]), 32 },
{ PSF(fd.forcePowerSelected), 8 },
{ PSF(torsoFlip), 1 },
{ PSF(externalEvent), 10 },
{ PSF(damageYaw), 8 },
{ PSF(damageCount), 8 },
{ PSF(inAirAnim), 1 }, //just transmit it for the sake of knowing right when on the client to play a land anim, it's only 1 bit
{ PSF(eventParms[1]), 8 },
{ PSF(fd.forceSide), 2 }, //so we know if we should apply greyed out shaders to dark/light force enlightenment
{ PSF(saberAttackChainCount), 4 },
{ PSF(pm_type), 8 },
{ PSF(externalEventParm), 8 },
{ PSF(eventParms[0]), -16 },
{ PSF(lookTarget), GENTITYNUM_BITS },
{ PSF(vehOrientation[0]), 0 },
{ PSF(weaponChargeSubtractTime), 32 }, //? really need 32 bits??
{ PSF(vehOrientation[1]), 0 },
{ PSF(moveDir[1]), 0 },
{ PSF(moveDir[0]), 0 },
{ PSF(weaponChargeTime), 32 }, //? really need 32 bits??
{ PSF(vehOrientation[2]), 0 },
{ PSF(legsFlip), 1 },
{ PSF(damageEvent), 8 },
{ PSF(moveDir[2]), 0 },
{ PSF(rocketTargetTime), 32 },
{ PSF(activeForcePass), 6 },
{ PSF(electrifyTime), 32 },
{ PSF(fd.forceJumpZStart), 0 },
{ PSF(loopSound), 16 }, //rwwFIXMEFIXME: max sounds is 256, doesn't this only need to be 8?
{ PSF(hasLookTarget), 1 },
{ PSF(saberBlocked), 8 },
{ PSF(damageType), 2 },
{ PSF(rocketLockTime), 32 },
{ PSF(forceHandExtend), 8 },
{ PSF(saberHolstered), 2 },
{ PSF(fd.forcePowersActive), 32 },
{ PSF(damagePitch), 8 },
{ PSF(m_iVehicleNum), GENTITYNUM_BITS }, // 10 bits fits all possible entity nums (2^10 = 1024). - AReis
{ PSF(vehTurnaroundTime), 32 },
{ PSF(generic1), 8 },
{ PSF(jumppad_ent), 10 },
{ PSF(hasDetPackPlanted), 1 },
{ PSF(saberInFlight), 1 },
{ PSF(forceDodgeAnim), 16 },
{ PSF(zoomMode), 2 }, // NOTENOTE Are all of these necessary?
{ PSF(hackingTime), 32 },
{ PSF(zoomTime), 32 },	// NOTENOTE Are all of these necessary?
{ PSF(brokenLimbs), 8 }, //up to 8 limbs at once (not that that many are used)
{ PSF(zoomLocked), 1 },	// NOTENOTE Are all of these necessary?
{ PSF(zoomFov), 0 },	// NOTENOTE Are all of these necessary?
{ PSF(fd.forceRageRecoveryTime), 32 },
{ PSF(fallingToDeath), 32 },
{ PSF(fd.forceMindtrickTargetIndex), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(fd.forceMindtrickTargetIndex2), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(vehWeaponsLinked), 1 },
{ PSF(lastHitLoc[2]), 0 },
{ PSF(hyperSpaceTime), 32 },
{ PSF(fd.forceMindtrickTargetIndex3), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(lastHitLoc[0]), 0 },
{ PSF(eFlags2), 10 },
{ PSF(fd.forceMindtrickTargetIndex4), 16 }, //NOTE: Not just an index, used as a (1 << val) bitflag for up to 16 clients
{ PSF(hyperSpaceAngles[1]), 0 },
{ PSF(lastHitLoc[1]), 0 }, //currently only used so client knows to orient disruptor disintegration.. seems a bit much for just that though.
{ PSF(vehBoarding), 1 }, //not like the normal boarding value, this is a simple "1 or 0" value
{ PSF(fd.sentryDeployed), 1 },
{ PSF(saberLockTime), 32 },
{ PSF(saberLockFrame), 16 },
{ PSF(vehTurnaroundIndex), GENTITYNUM_BITS },
{ PSF(vehSurfaces), 16 }, //allow up to 16 surfaces in the flag I guess
{ PSF(fd.forcePowerLevel[FP_SEE]), 2 }, //needed for knowing when to display players through walls
{ PSF(saberLockEnemy), GENTITYNUM_BITS },
{ PSF(fd.forceGripCripple), 1 }, //should only be 0 or 1 ever
{ PSF(emplacedIndex), GENTITYNUM_BITS },
{ PSF(holocronBits), 32 },
{ PSF(isJediMaster), 1 },
{ PSF(forceRestricted), 1 },
{ PSF(trueJedi), 1 },
{ PSF(trueNonJedi), 1 },
{ PSF(duelTime), 32 },
{ PSF(duelInProgress), 1 },
{ PSF(saberLockAdvance), 1 },
{ PSF(heldByClient), 6 },
{ PSF(ragAttach), GENTITYNUM_BITS },
{ PSF(iModelScale), 10 }, //0-1024 (guess it's gotta be increased if we want larger allowable scale.. but 1024% is pretty big)
{ PSF(hackingBaseTime), 16 }, //up to 65536ms, over 10 seconds would just be silly anyway
{ PSF(hyperSpaceAngles[0]), 0 },
{ PSF(hyperSpaceAngles[2]), 0 },

//rww - for use by mod authors only
#ifndef _XBOX
{ PSF(userInt1), 1 },
{ PSF(userInt2), 1 },
{ PSF(userInt3), 1 },
{ PSF(userFloat1), 1 },
{ PSF(userFloat2), 1 },
{ PSF(userFloat3), 1 },
{ PSF(userVec1[0]), 1 },
{ PSF(userVec1[1]), 1 },
{ PSF(userVec1[2]), 1 },
{ PSF(userVec2[0]), 1 },
{ PSF(userVec2[1]), 1 },
{ PSF(userVec2[2]), 1 }
#endif
};


//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================
#endif//_OPTIMIZED_VEHICLE_NETWORKING
//=====_OPTIMIZED_VEHICLE_NETWORKING=======================================================================


#define	MAXPRINTMSG	4096
void Com_Printf(const char* fmt, ...) {
	va_list		argptr;
	char		msg[MAXPRINTMSG];

	va_start(argptr, fmt);
	//vsprintf(msg, fmt, argptr);
	vsprintf_s(msg, sizeof(msg), fmt, argptr);
	va_end(argptr);

	std::cout << "// " << msg;
}


#ifndef _XBOX	// No mods on Xbox
typedef struct bitStorage_s bitStorage_t;

struct bitStorage_s
{
	bitStorage_t* next;
	int				bits;
};

static bitStorage_t* g_netfBitStorage = NULL;
static bitStorage_t* g_psfBitStorage = NULL;

//rww - Check the overrides files to see if the mod wants anything changed
void MSG_CheckNETFPSFOverrides(qboolean psfOverrides)
{
	char overrideFile[4096];
	char entryName[4096];
	char bits[4096];
	const char* fileName;
	int ibits;
	int i = 0;
	int j;
	int len;
	int numFields;
	bitStorage_t** bitStorage;

	if (psfOverrides)
	{ //do PSF overrides instead of NETF
		fileName = "psf_overrides.txt";
		bitStorage = &g_psfBitStorage;
		numFields = sizeof(playerStateFields) / sizeof(playerStateFields[0]);
	}
	else
	{
		fileName = "netf_overrides.txt";
		bitStorage = &g_netfBitStorage;
		numFields = sizeof(entityStateFields) / sizeof(entityStateFields[0]);
	}


	if (*bitStorage)
	{ //if we have saved off the defaults before we want to stuff them all back in now
		bitStorage_t* restore = *bitStorage;

		while (i < numFields)
		{
			assert(restore);

			if (psfOverrides)
			{
				playerStateFields[i].bits = restore->bits;
			}
			else
			{
				entityStateFields[i].bits = restore->bits;
			}

			i++;
			restore = restore->next;
		}
	}

	std::ifstream f(fileName, std::ios::binary);
	f.seekg(0, std::ios::end);
	len = f.tellg();
	f.seekg(0, std::ios::beg);

	if (!f)
	{ //silently exit since this file is not needed to proceed.
		return;
	}

	if (len >= 4096)
	{
		Com_Printf("WARNING: %s is >= 4096 bytes and is being ignored\n", fileName);
		f.close();
		return;
	}

	//Get contents of the file
	f.read(overrideFile, len);

	//because FS_Read does not do this for us.
	overrideFile[len] = 0;

	//If we haven't saved off the initial stuff yet then stuff it all into
	//a list.
	if (!*bitStorage)
	{
		i = 0;

		while (i < numFields)
		{
			//Alloc memory for this new ptr
			*bitStorage = (bitStorage_t*)malloc(sizeof(bitStorage_t)); // we should prolly free this again somewhere but idc this iss jusst a simple helper tool.

			if (psfOverrides)
			{
				(*bitStorage)->bits = playerStateFields[i].bits;
			}
			else
			{
				(*bitStorage)->bits = entityStateFields[i].bits;
			}

			//Point to the ->next of the existing current ptr
			bitStorage = &(*bitStorage)->next;
			i++;
		}
	}

	i = 0;
	//Now parse through. Lines beginning with ; are disabled.
	while (overrideFile[i])
	{
		if (overrideFile[i] == ';')
		{ //parse to end of the line
			while (overrideFile[i] != '\n')
			{
				i++;
			}
		}

		if (overrideFile[i] != ';' &&
			overrideFile[i] != '\n' &&
			overrideFile[i] != '\r')
		{ //on a valid char I guess, parse it
			j = 0;

			while (overrideFile[i] && overrideFile[i] != ',')
			{
				entryName[j] = overrideFile[i];
				j++;
				i++;
			}
			entryName[j] = 0;

			if (!overrideFile[i])
			{ //just give up, this shouldn't happen
				Com_Printf("WARNING: Parsing error for %s\n", fileName);
				return;
			}

			while (overrideFile[i] == ',' || overrideFile[i] == ' ')
			{ //parse to the start of the value
				i++;
			}

			j = 0;
			while (overrideFile[i] != '\n' && overrideFile[i] != '\r')
			{ //now read the value in
				bits[j] = overrideFile[i];
				j++;
				i++;
			}
			bits[j] = 0;

			if (bits[0])
			{
				qboolean isGentityNumBits = qfalse;
				if (!strcmp(bits, "GENTITYNUM_BITS"))
				{ //special case
					ibits = GENTITYNUM_BITS;
					isGentityNumBits = qtrue;
				}
				else
				{
					ibits = atoi(bits);
				}

				j = 0;

				//Now go through all the fields and see if we can find a match
				while (j < numFields)
				{
					if (psfOverrides)
					{ //check psf fields
						if (!strcmp(playerStateFields[j].name, entryName))
						{ //found it, set the bits
							playerStateFields[j].bits = ibits;
							std::cout << ".Override(" << j << ",";
							if (isGentityNumBits) {
								std::cout << "Common.GEntitynumBits) //" << playerStateFields[j].name << "\n";
							}
							else {
								std::cout << ibits << ") //" << playerStateFields[j].name << "\n";
							}
							break;
						}
					}
					else
					{ //otherwise check netf fields
						if (!strcmp(entityStateFields[j].name, entryName))
						{ //found it, set the bits
							entityStateFields[j].bits = ibits;
							std::cout << ".Override(" << j << ",";
							if (isGentityNumBits) {
								std::cout << "Common.GEntitynumBits) //" << entityStateFields[j].name << "\n";
							}
							else {
								std::cout << ibits << ") //" << entityStateFields[j].name << "\n";
							}
							break;
						}
					}
					j++;
				}

				if (j == numFields)
				{ //failed to find the value
					Com_Printf("WARNING: Value '%s' from %s is not valid\n", entryName, fileName);
				}
			}
			else
			{ //also should not happen
				Com_Printf("WARNING: Parsing error for %s\n", fileName);
				return;
			}
		}

		i++;
	}
}
#endif	// Xbox	- No mods on Xbox





int main() {
	std::cout << "Checking entity overrides\n";
	MSG_CheckNETFPSFOverrides(qfalse);
	std::cout << "Checking playerstate overrides\n";
	MSG_CheckNETFPSFOverrides(qtrue);
}