using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{
	class JKAStuff
	{

		public enum EntityFlags : int
		{
			EF_G2ANIMATING = (1 << 0),      //perform g2 bone anims based on torsoAnim and legsAnim, works for ET_GENERAL -rww
			EF_DEAD = (1 << 1),     // don't draw a foe marker over players with EF_DEAD
									//EF_BOUNCE_SHRAPNEL=(1<<2),		// special shrapnel flag
									//do not use eflags for server-only things, it wastes bandwidth -rww
			EF_RADAROBJECT = (1 << 2),      // display on team radar

			EF_TELEPORT_BIT = (1 << 3),     // toggled every time the origin abruptly changes

			EF_SHADER_ANIM = (1 << 4),      // Animating shader (by s.frame)

			EF_PLAYER_EVENT = (1 << 5),
			//EF_BOUNCE=(1<<5),		// for missiles
			//EF_BOUNCE_HALF=(1<<6),		// for missiles
			//these aren't even referenced in bg or client code and do not need to be eFlags, so I
			//am using these flags for rag stuff -rww

			EF_RAG = (1 << 6),      //ragdoll him even if he's alive


			EF_PERMANENT = (1 << 7),        // rww - I am claiming this. (for permanent entities)

			EF_NODRAW = (1 << 8),       // may have an event, but no model (unspawned items)
			EF_FIRING = (1 << 9),       // for lightning gun
			EF_ALT_FIRING = (1 << 10),      // for alt-fires, mostly for lightning guns though
			EF_JETPACK_ACTIVE = (1 << 11),      //jetpack is activated

			EF_NOT_USED_1 = (1 << 12),      // not used

			EF_TALK = (1 << 13),        // draw a talk balloon
			EF_CONNECTION = (1 << 14),      // draw a connection trouble sprite
			EF_NOT_USED_6 = (1 << 15),      // not used

			EF_NOT_USED_2 = (1 << 16),      // not used
			EF_NOT_USED_3 = (1 << 17),      // not used
			EF_NOT_USED_4 = (1 << 18),      // not used

			EF_BODYPUSH = (1 << 19),        //rww - claiming this for fullbody push effect

			EF_DOUBLE_AMMO = (1 << 20),     // Hacky way to get around ammo max
			EF_SEEKERDRONE = (1 << 21),     // show seeker drone floating around head
			EF_MISSILE_STICK = (1 << 22),       // missiles that stick to the wall.
			EF_ITEMPLACEHOLDER = (1 << 23),     // item effect
			EF_SOUNDTRACKER = (1 << 24),        // sound position needs to be updated in relation to another entity
			EF_DROPPEDWEAPON = (1 << 25),       // it's a dropped weapon
			EF_DISINTEGRATION = (1 << 26),      // being disintegrated by the disruptor
			EF_INVULNERABLE = (1 << 27),        // just spawned in or whatever, so is protected

			EF_CLIENTSMOOTH = (1 << 28),        // standard lerporigin smooth override on client

			EF_JETPACK = (1 << 29),     //rww - wearing a jetpack
			EF_JETPACK_FLAMING = (1 << 30),     //rww - jetpack fire effect

			EF_NOT_USED_5 = (1 << 31),      // not used

			//These new EF2_??? flags were added for NPCs, they really should not be used often.
			//NOTE: we only allow 10 of these!
			EF2_HELD_BY_MONSTER = (1 << 0),     // Being held by something, like a Rancor or a Wampa
			EF2_USE_ALT_ANIM = (1 << 1),        // For certain special runs/stands for creatures like the Rancor and Wampa whose runs/stands are conditional
			EF2_ALERTED = (1 << 2),     // For certain special anims, for Rancor: means you've had an enemy, so use the more alert stand
			EF2_GENERIC_NPC_FLAG = (1 << 3),        // So far, used for Rancor...
			EF2_FLYING = (1 << 4),      // Flying FIXME: only used on NPCs doesn't *really* have to be passed over, does it?
			EF2_HYPERSPACE = (1 << 5),      // Used to both start the hyperspace effect on the predicted client and to let the vehicle know it can now jump into hyperspace (after turning to face the proper angle)
			EF2_BRACKET_ENTITY = (1 << 6),      // Draw as bracketed
			EF2_SHIP_DEATH = (1 << 7),      // "died in ship" mode
			EF2_NOT_USED_1 = (1 << 8),      // not used

		}

		/*public enum GameEntityFlags : int // Probably useless
		{
			FL_GODMODE = 0x00000010,
			FL_NOTARGET = 0x00000020,
			FL_TEAMSLAVE = 0x00000400,  // not the first on the team
			FL_NO_KNOCKBACK = 0x00000800,
			FL_DROPPED_ITEM = 0x00001000,
			FL_NO_BOTS = 0x00002000,    // spawn point not for bot use
			FL_NO_HUMANS = 0x00004000,  // spawn point just for bots
			FL_FORCE_GESTURE = 0x00008000   // force gesture on client
		}*/

		public enum entityType_t
		{
			ET_GENERAL,
			ET_PLAYER,
			ET_ITEM,
			ET_MISSILE,
			ET_SPECIAL,             // rww - force fields
			ET_HOLOCRON,            // rww - holocron icon displays
			ET_MOVER,
			ET_BEAM,
			ET_PORTAL,
			ET_SPEAKER,
			ET_PUSH_TRIGGER,
			ET_TELEPORT_TRIGGER,
			ET_INVISIBLE,
			ET_NPC,                 // ghoul2 player-like entity
			ET_TEAM,
			ET_BODY,
			ET_TERRAIN,
			ET_FX,

			ET_EVENTS               // any of the EV_* events can be added freestanding
									// by setting eType to ET_EVENTS + eventNum
									// this avoids having to set eFlags and eventNum
		}

		public static class ItemList
		{
			public const int MaxItemModels = 4;

			/* Don't use this. Not in sync.
			public enum ModelIndex : int
			{
				// NOTENOTE Update this so that it is in sync.
				//item numbers (make sure they are in sync with bg_itemlist in bg_misc.c)
				//pickups
				MODELINDEX_ARMOR = 1,
				MODELINDEX_HEALTH = 2,
				//items
				MODELINDEX_SEEKER = 3,
				MODELINDEX_MEDPAC = 4,
				MODELINDEX_DATAPAD = 5,
				MODELINDEX_BINOCULARS = 6,
				MODELINDEX_SENTRY_GUN = 7,
				MODELINDEX_GOGGLES = 8,
				//weapons
				MODELINDEX_STUN_BATON = 9,
				MODELINDEX_SABER = 10,
				MODELINDEX_BRYAR_PISTOL = 11,
				MODELINDEX_BLASTER = 12,
				MODELINDEX_DISRUPTOR = 13,
				MODELINDEX_BOWCASTER = 14,
				MODELINDEX_REPEATER = 15,
				MODELINDEX_DEMP2 = 16,
				MODELINDEX_FLECHETTE = 17,
				MODELINDEX_ROCKET_LAUNCHER = 18,
				MODELINDEX_THERMAL = 19,
				MODELINDEX_TRIP_MINE = 20,
				MODELINDEX_DET_PACK = 21,
				//ammo
				MODELINDEX_AMMO_FORCE = 22,
				MODELINDEX_AMMO_BLASTER = 23,
				MODELINDEX_AMMO_BOLTS = 24,
				MODELINDEX_AMMO_ROCKETS = 25,
				//powerups
				MODELINDEX_REDFLAG = 26,
				MODELINDEX_BLUEFLAG = 27,
				MODELINDEX_SCOUT = 28,
				MODELINDEX_GUARD = 29,
				MODELINDEX_DOUBLER = 30,
				MODELINDEX_AMMOREGEN = 31,
				MODELINDEX_NEUTRALFLAG = 32,
				MODELINDEX_REDCUBE = 33,
				MODELINDEX_BLUECUBE = 34,
			}*/

			public enum ammo_t//# ammo_e
			{
				AMMO_NONE,
				AMMO_FORCE,     // AMMO_PHASER
				AMMO_BLASTER,   // AMMO_STARFLEET,
				AMMO_POWERCELL, // AMMO_ALIEN,
				AMMO_METAL_BOLTS,
				AMMO_ROCKETS,
				AMMO_EMPLACED,
				AMMO_THERMAL,
				AMMO_TRIPMINE,
				AMMO_DETPACK,
				AMMO_MAX
			}

			public enum weapon_t
			{
				WP_NONE,

				WP_STUN_BATON,
				WP_MELEE,
				WP_SABER,
				WP_BRYAR_PISTOL,
				WP_BLASTER,
				WP_DISRUPTOR,
				WP_BOWCASTER,
				WP_REPEATER,
				WP_DEMP2,
				WP_FLECHETTE,
				WP_ROCKET_LAUNCHER,
				WP_THERMAL,
				WP_TRIP_MINE,
				WP_DET_PACK,
				WP_CONCUSSION,
				WP_BRYAR_OLD,
				WP_EMPLACED_GUN,
				WP_TURRET,

				//	WP_GAUNTLET,
				//	WP_MACHINEGUN,			// Bryar
				//	WP_SHOTGUN,				// Blaster
				//	WP_GRENADE_LAUNCHER,	// Thermal
				//	WP_LIGHTNING,			// 
				//	WP_RAILGUN,				// 
				//	WP_GRAPPLING_HOOK,

				WP_NUM_WEAPONS
			}

			public enum holdable_t
			{
				HI_NONE,

				HI_SEEKER,
				HI_SHIELD,
				HI_MEDPAC,
				HI_MEDPAC_BIG,
				HI_BINOCULARS,
				HI_SENTRY_GUN,
				HI_JETPACK,

				HI_HEALTHDISP,
				HI_AMMODISP,
				HI_EWEB,
				HI_CLOAK,

				HI_NUM_HOLDABLE
			}
			public enum powerup_t : int
			{
				PW_NONE,

				PW_QUAD,
				PW_BATTLESUIT,
				PW_PULL,
				//PW_INVIS, //rww - removed
				//PW_REGEN, //rww - removed
				//PW_FLIGHT, //rww - removed

				PW_REDFLAG,
				PW_BLUEFLAG,
				PW_NEUTRALFLAG,

				PW_SHIELDHIT,

				//PW_SCOUT, //rww - removed
				//PW_GUARD, //rww - removed
				//PW_DOUBLER, //rww - removed
				//PW_AMMOREGEN, //rww - removed
				PW_SPEEDBURST,
				PW_DISINT_4,
				PW_SPEED,
				PW_CLOAKED,
				PW_FORCE_ENLIGHTENED_LIGHT,
				PW_FORCE_ENLIGHTENED_DARK,
				PW_FORCE_BOON,
				PW_YSALAMIRI,

				PW_NUM_POWERUPS


			}

			public enum itemType_t
			{
				IT_BAD,
				IT_WEAPON,              // EFX: rotate + upscale + minlight
				IT_AMMO,                // EFX: rotate
				IT_ARMOR,               // EFX: rotate + minlight
				IT_HEALTH,              // EFX: static external sphere + rotating internal
				IT_POWERUP,             // instant on, timer based
											// EFX: rotate + external ring that rotates
				IT_HOLDABLE,            // single use, holdable item
											// EFX: rotate + bob
				IT_PERSISTANT_POWERUP,
				IT_TEAM
			}


			public class ItemListArray : List<gitem_s>
			{
				public ItemListArray()
				{
				}
				public void Add(string classname, string pickup_sound, string[] world_model, string view_model, string icon, int quantity, itemType_t giType, int giTag, string precaches, string sounds="", string description="")
				{
					this.Add(new gitem_s()
					{
						classname = classname,
						pickup_sound = pickup_sound,
						world_model = world_model,
						view_model = view_model,
						icon = icon,
						quantity = quantity,
						giType = giType,
						giTag = giTag,
						precaches = precaches,
						sounds = sounds,
						description = description
					});
				}
			}

			public struct gitem_s
			{
				public string classname;    // spawning name
				public string pickup_sound;
				public string[] world_model; // up to MaxItemModels
				public string view_model;

				public string icon;
				//	char		*pickup_name;	// for printing on pickup

				public int quantity;       // for ammo how much, or duration of powerup
				public itemType_t giType;          // itemType_t.IT_* flags

				public int giTag;

				public string precaches;        // string of all models and images this item will use
				public string sounds;       // string of all sounds this item will use
				public string description;
			}

			//static int[] stuff = new int[]  { 1,2,3};

			static public ItemListArray bg_itemlist = new ItemListArray() {
				{
		null,				// classname	
		null,				// pickup_sound
		new string[]{	null,			// world_model[0]
			null,			// world_model[1]
			null, null} ,			// world_model[2],[3]
		null,				// view_model
/* icon */		null,		// icon
/* pickup */	//null,		// pickup_name
		0,					// quantity
		0,					// giType (IT_*)
		0,					// giTag
/* precache */ "",			// precaches
/* sounds */ "",			// sounds
		""					// description
	},  // leave index 0 alone

	//
	// Pickups
	//

/*QUAKED item_shield_sm_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Instant shield pickup, restores 25
*/
	{
		"item_shield_sm_instant",
		"sound/player/pickupshield.wav",
		new string[]{ "models/map_objects/mp/psd_sm.md3",
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/small_shield",
/* pickup *///	"Shield Small",
		25,
	itemType_t.IT_ARMOR,
		1, //special for shield - max on pickup is maxhealth*tag, thus small shield goes up to 100 shield
/* precache */ "",
/* sounds */ "",
		""					// description
	},

/*QUAKED item_shield_lrg_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Instant shield pickup, restores 100
*/
	{
		"item_shield_lrg_instant", 
		"sound/player/pickupshield.wav",
		new string[]{ "models/map_objects/mp/psd.md3",
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/large_shield",
/* pickup *///	"Shield Large",
		100,
	itemType_t.IT_ARMOR,
		2, //special for shield - max on pickup is maxhealth*tag, thus large shield goes up to 200 shield
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_medpak_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Instant medpack pickup, heals 25
*/
	{
	"item_medpak_instant",
		"sound/player/pickuphealth.wav",
		new string[]{
		"models/map_objects/mp/medpac.md3", 
		null, null, null },
/* view */		null,			
/* icon */		"gfx/hud/i_icon_medkit",
/* pickup *///	"Medpack",
		25,
	itemType_t.IT_HEALTH,
		0,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},


	//
	// ITEMS
	//

/*QUAKED item_seeker (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
30 seconds of seeker drone
*/
	{
	"item_seeker", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/remote.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_seeker",
/* pickup *///	"Seeker Drone",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_SEEKER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_AN_ATTACK_DRONE_SIMILAR"                    // description
	},

/*QUAKED item_shield (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Portable shield
*/
	{
	"item_shield", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/map_objects/mp/shield.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_shieldwall",
/* pickup *///	"Forcefield",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_SHIELD,
/* precache */ "",
/* sounds */ "sound/weapons/detpack/stick.wav sound/movers/doors/forcefield_on.wav sound/movers/doors/forcefield_off.wav sound/movers/doors/forcefield_lp.wav sound/effects/bumpfield.wav",
		"@MENUS_THIS_STATIONARY_ENERGY"                 // description
	},

/*QUAKED item_medpac (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Bacta canister pickup, heals 25 on use
*/
	{
	"item_medpac",	//should be item_bacta
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/map_objects/mp/bacta.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_bacta",
/* pickup *///	"Bacta Canister",
		25,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_MEDPAC,
/* precache */ "",
/* sounds */ "",
		"@SP_INGAME_BACTA_DESC"                 // description
	},

/*QUAKED item_medpac_big (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Big bacta canister pickup, heals 50 on use
*/
	{
	"item_medpac_big",	//should be item_bacta
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/big_bacta.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_big_bacta",
/* pickup *///	"Bacta Canister",
		25,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_MEDPAC_BIG,
/* precache */ "",
/* sounds */ "",
		"@SP_INGAME_BACTA_DESC"                 // description
	},

/*QUAKED item_binoculars (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
These will be standard equipment on the player - DO NOT PLACE
*/
	{
	"item_binoculars", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/binoculars.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_zoom",
/* pickup *///	"Binoculars",
		60,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_BINOCULARS,
/* precache */ "",
/* sounds */ "",
		"@SP_INGAME_LA_GOGGLES_DESC"                    // description
	},

/*QUAKED item_sentry_gun (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Sentry gun inventory pickup.
*/
	{
	"item_sentry_gun", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/psgun.glm", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_sentrygun",
/* pickup *///	"Sentry Gun",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_SENTRY_GUN,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THIS_DEADLY_WEAPON_IS"                  // description
	},

/*QUAKED item_jetpack (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Do not place.
*/
	{
	"item_jetpack", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/psgun.glm", //FIXME: no model
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_jetpack",
/* pickup *///	"Sentry Gun",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_JETPACK,
/* precache */ "effects/boba/jet.efx",
/* sounds */ "sound/chars/boba/JETON.wav sound/chars/boba/JETHOVER.wav sound/effects/fire_lp.wav",
		"@MENUS_JETPACK_DESC"                   // description
	},

/*QUAKED item_healthdisp (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Do not place. For siege classes ONLY.
*/
	{
	"item_healthdisp", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/map_objects/mp/bacta.md3", //replace me
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_healthdisp",
/* pickup *///	"Sentry Gun",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_HEALTHDISP,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_ammodisp (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Do not place. For siege classes ONLY.
*/
	{
	"item_ammodisp", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/map_objects/mp/bacta.md3", //replace me
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_ammodisp",
/* pickup *///	"Sentry Gun",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_AMMODISP,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_eweb_holdable (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
Do not place. For siege classes ONLY.
*/
	{
	"item_eweb_holdable", 
		"sound/interface/shieldcon_empty",
		new string[]{
		"models/map_objects/hoth/eweb_model.glm",
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_eweb",
/* pickup *///	"Sentry Gun",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_EWEB,
/* precache */ "",
/* sounds */ "",
		"@MENUS_EWEB_DESC"                  // description
	},

/*QUAKED item_seeker (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
30 seconds of seeker drone
*/
	{
	"item_cloak", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/items/psgun.glm", //FIXME: no model
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/i_icon_cloak",
/* pickup *///	"Seeker Drone",
		120,
	itemType_t.IT_HOLDABLE,
		(int)holdable_t.HI_CLOAK,
/* precache */ "",
/* sounds */ "",
		"@MENUS_CLOAK_DESC"                 // description
	},

/*QUAKED item_force_enlighten_light (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Adds one rank to all Force powers temporarily. Only light jedi can use.
*/
	{
	"item_force_enlighten_light",
		"sound/player/enlightenment.wav",
		new string[]{
		"models/map_objects/mp/jedi_enlightenment.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/mpi_jlight",
/* pickup *///	"Light Force Enlightenment",
		25,
	itemType_t.IT_POWERUP,
		(int)powerup_t.PW_FORCE_ENLIGHTENED_LIGHT,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_force_enlighten_dark (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Adds one rank to all Force powers temporarily. Only dark jedi can use.
*/
	{
	"item_force_enlighten_dark",
		"sound/player/enlightenment.wav",
		new string[]{
		"models/map_objects/mp/dk_enlightenment.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/mpi_dklight",
/* pickup *///	"Dark Force Enlightenment",
		25,
	itemType_t.IT_POWERUP,
		(int)powerup_t.PW_FORCE_ENLIGHTENED_DARK,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_force_boon (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Unlimited Force Pool for a short time.
*/
	{
	"item_force_boon",
		"sound/player/boon.wav",
		new string[]{
		"models/map_objects/mp/force_boon.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/mpi_fboon",
/* pickup *///	"Force Boon",
		25,
	itemType_t.IT_POWERUP,
		(int)powerup_t.PW_FORCE_BOON,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED item_ysalimari (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
A small lizard carried on the player, which prevents the possessor from using any Force power.  However, he is unaffected by any Force power.
*/
	{
	"item_ysalimari",
		"sound/player/ysalimari.wav",
		new string[]{
		"models/map_objects/mp/ysalimari.md3", 
		null, null, null} ,
/* view */		null,			
/* icon */		"gfx/hud/mpi_ysamari",
/* pickup *///	"Ysalamiri",
		25,
	itemType_t.IT_POWERUP,
		(int)powerup_t.PW_YSALAMIRI,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	//
	// WEAPONS 
	//

/*QUAKED weapon_stun_baton (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	"weapon_stun_baton", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/stun_baton/baton_w.glm", 
		null, null, null},
/* view */		"models/weapons2/stun_baton/baton.md3", 
/* icon */		"gfx/hud/w_icon_stunbaton",
/* pickup *///	"Stun Baton",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_STUN_BATON,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED weapon_melee (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	"weapon_melee", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/stun_baton/baton_w.glm", 
		null, null, null},
/* view */		"models/weapons2/stun_baton/baton.md3", 
/* icon */		"gfx/hud/w_icon_melee",
/* pickup *///	"Stun Baton",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_MELEE,
/* precache */ "",
/* sounds */ "",
		"@MENUS_MELEE_DESC"                 // description
	},

/*QUAKED weapon_saber (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	"weapon_saber", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/saber/saber_w.glm",
		null, null, null},
/* view */		"models/weapons2/saber/saber_w.md3",
/* icon */		"gfx/hud/w_icon_lightsaber",
/* pickup *///	"Lightsaber",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_SABER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_AN_ELEGANT_WEAPON_FOR"              // description
	},

/*QUAKED weapon_bryar_pistol (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	//"weapon_bryar_pistol", 
	"weapon_blaster_pistol", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/blaster_pistol/blaster_pistol_w.glm",//"models/weapons2/briar_pistol/briar_pistol_w.glm", 
		null, null, null},
/* view */		"models/weapons2/blaster_pistol/blaster_pistol.md3",//"models/weapons2/briar_pistol/briar_pistol.md3", 
/* icon */		"gfx/hud/w_icon_blaster_pistol",//"gfx/hud/w_icon_rifle",
/* pickup *///	"Bryar Pistol",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_BRYAR_PISTOL,
/* precache */ "",
/* sounds */ "",
		"@MENUS_BLASTER_PISTOL_DESC"                    // description
	},

/*QUAKED weapon_concussion_rifle (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_concussion_rifle", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/concussion/c_rifle_w.glm", 
		null, null, null},
/* view */		"models/weapons2/concussion/c_rifle.md3", 
/* icon */		"gfx/hud/w_icon_c_rifle",//"gfx/hud/w_icon_rifle",
/* pickup *///	"Concussion Rifle",
		50,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_CONCUSSION,
/* precache */ "",
/* sounds */ "",
		"@MENUS_CONC_RIFLE_DESC"                    // description
	},

/*QUAKED weapon_bryar_pistol_old (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	"weapon_bryar_pistol", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/briar_pistol/briar_pistol_w.glm", 
		null, null, null},
/* view */		"models/weapons2/briar_pistol/briar_pistol.md3", 
/* icon */		"gfx/hud/w_icon_briar",//"gfx/hud/w_icon_rifle",
/* pickup *///	"Bryar Pistol",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_BRYAR_OLD,
/* precache */ "",
/* sounds */ "",
		"@SP_INGAME_BLASTER_PISTOL"                 // description
	},

/*QUAKED weapon_blaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_blaster", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/blaster_r/blaster_w.glm", 
		null, null, null},
/* view */		"models/weapons2/blaster_r/blaster.md3", 
/* icon */		"gfx/hud/w_icon_blaster",
/* pickup *///	"E11 Blaster Rifle",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_BLASTER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THE_PRIMARY_WEAPON_OF"              // description
	},

/*QUAKED weapon_disruptor (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_disruptor",
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/disruptor/disruptor_w.glm", 
		null, null, null},
/* view */		"models/weapons2/disruptor/disruptor.md3", 
/* icon */		"gfx/hud/w_icon_disruptor",
/* pickup *///	"Tenloss Disruptor Rifle",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_DISRUPTOR,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THIS_NEFARIOUS_WEAPON"                  // description
	},

/*QUAKED weapon_bowcaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_bowcaster",
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/bowcaster/bowcaster_w.glm", 
		null, null, null},
/* view */		"models/weapons2/bowcaster/bowcaster.md3", 
/* icon */		"gfx/hud/w_icon_bowcaster",
/* pickup *///	"Wookiee Bowcaster",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_BOWCASTER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THIS_ARCHAIC_LOOKING"                   // description
	},

/*QUAKED weapon_repeater (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_repeater", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/heavy_repeater/heavy_repeater_w.glm", 
		null, null, null},
/* view */		"models/weapons2/heavy_repeater/heavy_repeater.md3", 
/* icon */		"gfx/hud/w_icon_repeater",
/* pickup *///	"Imperial Heavy Repeater",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_REPEATER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THIS_DESTRUCTIVE_PROJECTILE"                    // description
	},

/*QUAKED weapon_demp2 (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
NOTENOTE This weapon is not yet complete.  Don't place it.
*/
	{
	"weapon_demp2", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/demp2/demp2_w.glm", 
		null, null, null},
/* view */		"models/weapons2/demp2/demp2.md3", 
/* icon */		"gfx/hud/w_icon_demp2",
/* pickup *///	"DEMP2",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_DEMP2,
/* precache */ "",
/* sounds */ "",
		"@MENUS_COMMONLY_REFERRED_TO"                   // description
	},

/*QUAKED weapon_flechette (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_flechette", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/golan_arms/golan_arms_w.glm", 
		null, null, null},
/* view */		"models/weapons2/golan_arms/golan_arms.md3", 
/* icon */		"gfx/hud/w_icon_flechette",
/* pickup *///	"Golan Arms Flechette",
		100,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_FLECHETTE,
/* precache */ "",
/* sounds */ "",
		"@MENUS_WIDELY_USED_BY_THE_CORPORATE"                   // description
	},

/*QUAKED weapon_rocket_launcher (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_rocket_launcher",
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/merr_sonn/merr_sonn_w.glm", 
		null, null, null},
/* view */		"models/weapons2/merr_sonn/merr_sonn.md3", 
/* icon */		"gfx/hud/w_icon_merrsonn",
/* pickup *///	"Merr-Sonn Missile System",
		3,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_ROCKET_LAUNCHER,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THE_PLX_2M_IS_AN_EXTREMELY"                 // description
	},

/*QUAKED ammo_thermal (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"ammo_thermal",
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/thermal/thermal_pu.md3", 
		"models/weapons2/thermal/thermal_w.glm", null, null},
/* view */		"models/weapons2/thermal/thermal.md3", 
/* icon */		"gfx/hud/w_icon_thermal",
/* pickup *///	"Thermal Detonators",
		4,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_THERMAL,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THE_THERMAL_DETONATOR"                  // description
	},

/*QUAKED ammo_tripmine (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"ammo_tripmine", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/laser_trap/laser_trap_pu.md3", 
		"models/weapons2/laser_trap/laser_trap_w.glm", null, null},
/* view */		"models/weapons2/laser_trap/laser_trap.md3", 
/* icon */		"gfx/hud/w_icon_tripmine",
/* pickup *///	"Trip Mines",
		3,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_TRIPMINE,
/* precache */ "",
/* sounds */ "",
		"@MENUS_TRIP_MINES_CONSIST_OF"                  // description
	},

/*QUAKED ammo_detpack (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"ammo_detpack", 
		"sound/weapons/w_pkup.wav",
		new string[]{ "models/weapons2/detpack/det_pack_pu.md3", "models/weapons2/detpack/det_pack_proj.glm", "models/weapons2/detpack/det_pack_w.glm", null},
/* view */		"models/weapons2/detpack/det_pack.md3", 
/* icon */		"gfx/hud/w_icon_detpack",
/* pickup *///	"Det Packs",
		3,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_DETPACK,
/* precache */ "",
/* sounds */ "",
		"@MENUS_A_DETONATION_PACK_IS"                   // description
	},

/*QUAKED weapon_thermal (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_thermal",
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/thermal/thermal_w.glm", "models/weapons2/thermal/thermal_pu.md3",
		null, null },
/* view */		"models/weapons2/thermal/thermal.md3", 
/* icon */		"gfx/hud/w_icon_thermal",
/* pickup *///	"Thermal Detonator",
		4,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_THERMAL,
/* precache */ "",
/* sounds */ "",
		"@MENUS_THE_THERMAL_DETONATOR"                  // description
	},

/*QUAKED weapon_trip_mine (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_trip_mine", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/laser_trap/laser_trap_w.glm", "models/weapons2/laser_trap/laser_trap_pu.md3",
		null, null},
/* view */		"models/weapons2/laser_trap/laser_trap.md3", 
/* icon */		"gfx/hud/w_icon_tripmine",
/* pickup *///	"Trip Mine",
		3,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_TRIP_MINE,
/* precache */ "",
/* sounds */ "",
		"@MENUS_TRIP_MINES_CONSIST_OF"                  // description
	},

/*QUAKED weapon_det_pack (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_det_pack", 
		"sound/weapons/w_pkup.wav",
		new string[]{ "models/weapons2/detpack/det_pack_proj.glm", "models/weapons2/detpack/det_pack_pu.md3", "models/weapons2/detpack/det_pack_w.glm", null},
/* view */		"models/weapons2/detpack/det_pack.md3", 
/* icon */		"gfx/hud/w_icon_detpack",
/* pickup *///	"Det Pack",
		3,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_DET_PACK,
/* precache */ "",
/* sounds */ "",
		"@MENUS_A_DETONATION_PACK_IS"                   // description
	},

/*QUAKED weapon_emplaced (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
*/
	{
	"weapon_emplaced", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/blaster_r/blaster_w.glm", 
		null, null, null},
/* view */		"models/weapons2/blaster_r/blaster.md3", 
/* icon */		"gfx/hud/w_icon_blaster",
/* pickup *///	"Emplaced Gun",
		50,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_EMPLACED_GUN,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},


//NOTE: This is to keep things from messing up because the turret weapon type isn't real
	{
	"weapon_turretwp", 
		"sound/weapons/w_pkup.wav",
		new string[]{
		"models/weapons2/blaster_r/blaster_w.glm", 
		null, null, null},
/* view */		"models/weapons2/blaster_r/blaster.md3", 
/* icon */		"gfx/hud/w_icon_blaster",
/* pickup *///	"Turret Gun",
		50,
	itemType_t.IT_WEAPON,
		(int)weapon_t.WP_TURRET,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	//
	// AMMO ITEMS
	//

/*QUAKED ammo_force (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Don't place this
*/
	{
	"ammo_force",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/energy_cell.md3", 
		null, null, null},
/* view */		null,			
/* icon */		"gfx/hud/w_icon_blaster",
/* pickup *///	"Force??",
		100,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_FORCE,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED ammo_blaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Ammo for the Bryar and Blaster pistols.
*/
	{
	"ammo_blaster",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/energy_cell.md3", 
		null, null, null},
/* view */		null,			
/* icon */		"gfx/hud/i_icon_battery",
/* pickup *///	"Blaster Pack",
		100,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_BLASTER,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED ammo_powercell (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Ammo for Tenloss Disruptor, Wookie Bowcaster, and the Destructive Electro Magnetic Pulse (demp2 ) guns
*/
	{
	"ammo_powercell",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/power_cell.md3", 
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/ammo_power_cell",
/* pickup *///	"Power Cell",
		100,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_POWERCELL,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED ammo_metallic_bolts (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Ammo for Imperial Heavy Repeater and the Golan Arms Flechette
*/
	{
	"ammo_metallic_bolts",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/metallic_bolts.md3", 
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/ammo_metallic_bolts",
/* pickup *///	"Metallic Bolts",
		100,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_METAL_BOLTS,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED ammo_rockets (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
Ammo for Merr-Sonn portable missile launcher
*/
	{
	"ammo_rockets",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/rockets.md3", 
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/ammo_rockets",
/* pickup *///	"Rockets",
		3,
	itemType_t.IT_AMMO,
		(int)ammo_t.AMMO_ROCKETS,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED ammo_all (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
DO NOT PLACE in a map, this is only for siege classes that have ammo
dispensing ability
*/
	{
	"ammo_all",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/items/battery.md3",  //replace me
		null, null, null},
/* view */		null,			
/* icon */		"gfx/mp/ammo_rockets", //replace me
/* pickup *///	"Rockets",
		0,
	itemType_t.IT_AMMO,
		-1,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	//
	// POWERUP ITEMS
	//
/*QUAKED team_CTF_redflag (1 0 0) (-16 -16 -16) (16 16 16)
Only in CTF games
*/
	{
	"team_CTF_redflag",
		null,
		new string[]{
		"models/flags/r_flag.md3",
		"models/flags/r_flag_ysal.md3", null, null },
/* view */		null,			
/* icon */		"gfx/hud/mpi_rflag",
/* pickup *///	"Red Flag",
		0,
	itemType_t.IT_TEAM,
		(int)powerup_t.PW_REDFLAG,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

/*QUAKED team_CTF_blueflag (0 0 1) (-16 -16 -16) (16 16 16)
Only in CTF games
*/
	{
	"team_CTF_blueflag",
		null,
		new string[]{
		"models/flags/b_flag.md3",
		"models/flags/b_flag_ysal.md3", null, null },
/* view */		null,			
/* icon */		"gfx/hud/mpi_bflag",
/* pickup *///	"Blue Flag",
		0,
	itemType_t.IT_TEAM,
		(int)powerup_t.PW_BLUEFLAG,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	//
	// PERSISTANT POWERUP ITEMS
	//

	/*QUAKED team_CTF_neutralflag (0 0 1) (-16 -16 -16) (16 16 16)
Only in One Flag CTF games
*/
	{
	"team_CTF_neutralflag",
		null,
		new string[]{
		"models/flags/n_flag.md3",
		null, null, null },
/* view */		null,			
/* icon */		"icons/iconf_neutral1",
/* pickup *///	"Neutral Flag",
		0,
	itemType_t.IT_TEAM,
		(int)powerup_t.PW_NEUTRALFLAG,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	{
	"item_redcube",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/powerups/orb/r_orb.md3",
		null, null, null },
/* view */		null,			
/* icon */		"icons/iconh_rorb",
/* pickup *///	"Red Cube",
		0,
	itemType_t.IT_TEAM,
		0,
/* precache */ "",
/* sounds */ "",
		""                  // description
	},

	{
	"item_bluecube",
		"sound/player/pickupenergy.wav",
		new string[]{
		"models/powerups/orb/b_orb.md3",
		null, null, null },
/* view */		null,			
/* icon */		"icons/iconh_borb",
/* pickup *///	"Blue Cube",
		0,
		itemType_t.IT_TEAM,
		0,
/* precache */ "",
/* sounds */ "",
		""                  // description
	}
			};

			/*
			==============
			BG_FindItemForPowerup
			==============
			*/
			public static int? BG_FindItemForPowerup(powerup_t pw,bool isMBII)
			{
                if (isMBII)
                {
                    switch (pw) {
						case powerup_t.PW_REDFLAG:
							return 55;
						case powerup_t.PW_BLUEFLAG:
							return 56;
						case powerup_t.PW_NEUTRALFLAG:
							return 57;
						default:
							throw new NotImplementedException("Dunno about item numbers other than flags for MBII.");
					}

                }

                int i;

				for (i = 0; i < bg_itemlist.Count; i++)
				{
					if ((bg_itemlist[i].giType == itemType_t.IT_POWERUP ||
								bg_itemlist[i].giType == itemType_t.IT_TEAM) &&
						bg_itemlist[i].giTag == (int)pw)
					{
						return i;
					}
				}

				return null;
			}


			/*
			==============
			BG_FindItemForHoldable
			==============
			
			public static int? BG_FindItemForHoldable(holdable_t pw)
			{
				int i;

				for (i = 0; i < bg_itemlist.Count; i++)
				{
					if (bg_itemlist[i].giType == itemType_t.IT_HOLDABLE && bg_itemlist[i].giTag == (int)pw)
					{
						return i;
					}
				}

				//Com_Error(ERR_DROP, "HoldableItem not found");

				return null;
			}*/


			/*
			===============
			BG_FindItemForWeapon

			===============
			public static int? BG_FindItemForWeapon(weapon_t weapon)
			{
				//gitem_t* it;

				//for (it = bg_itemlist + 1; it->classname; it++)
				//{
				//	if (it->giType ==itemType_t.IT_WEAPON && it->giTag == (int)weapon)
				//	{
				//		return it;
				//	}
				//}
//
				//Com_Error(ERR_DROP, "Couldn't find item for weapon %i", weapon);
				//return null;

				int i;

				for (i = 0; i < bg_itemlist.Count; i++)
				{
					if (bg_itemlist[i].giType == itemType_t.IT_WEAPON && bg_itemlist[i].giTag == (int)weapon)
					{
						return i;
					}
				}

				return null;
			}
			*/

			/*
			===============
			BG_FindItem

			===============
			public static int? BG_FindItem(string classname)
			{
				//gitem_t* it;

				//for (it = bg_itemlist + 1 ; it->classname ; it++ ) {
				//	if ( !Q_stricmp(it->classname, classname) )
				//		return it;
				//}

				//return null;
				int i;

				string lowerClassname = classname.ToLower();
				for (i = 0; i < bg_itemlist.Count; i++)
				{
					if (bg_itemlist[i].classname.ToLower() == lowerClassname)
					{
						return i;
					}
				}

				return null;
			}
*/

		}
	}
}
