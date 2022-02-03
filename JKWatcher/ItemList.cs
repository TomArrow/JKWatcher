using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{

	public enum EntityFlags :int {
		EF_DEAD = 0x00000001,       // don't draw a foe marker over players with EF_DEAD
		EF_BOUNCE_SHRAPNEL = 0x00000002,        // special shrapnel flag
		EF_TELEPORT_BIT = 0x00000004,       // toggled every time the origin abruptly changes

		//doesn't do anything
		EF_AWARD_EXCELLENT = 0x00000008,        // draw an excellent sprite

		EF_PLAYER_EVENT = 0x00000010,
		EF_BOUNCE = 0x00000010,     // for missiles

		EF_BOUNCE_HALF = 0x00000020,        // for missiles

		//doesn't do anything
		EF_AWARD_GAUNTLET = 0x00000040,     // draw a gauntlet sprite

		EF_NODRAW = 0x00000080,     // may have an event, but no model (unspawned items)
		EF_FIRING = 0x00000100,     // for lightning gun
		EF_ALT_FIRING = 0x00000200,     // for alt-fires, mostly for lightning guns though
		EF_MOVER_STOP = 0x00000400,     // will push otherwise

		//doesn't do anything
		EF_AWARD_CAP = 0x00000800,      // draw the capture sprite

		EF_TALK = 0x00001000,       // draw a talk balloon
		EF_CONNECTION = 0x00002000,     // draw a connection trouble sprite
		EF_VOTED = 0x00004000,      // already cast a vote

		//next 4 don't actually do anything
		EF_AWARD_IMPRESSIVE = 0x00008000,       // draw an impressive sprite
		EF_AWARD_DEFEND = 0x00010000,       // draw a defend sprite
		EF_AWARD_ASSIST = 0x00020000,       // draw a assist sprite
		EF_AWARD_DENIED = 0x00040000,       // denied

		EF_TEAMVOTED = 0x00080000,      // already cast a team vote
		EF_SEEKERDRONE = 0x00100000,        // show seeker drone floating around head
		EF_MISSILE_STICK = 0x00200000,      // missiles that stick to the wall.
		EF_ITEMPLACEHOLDER = 0x00400000,        // item effect
		EF_SOUNDTRACKER = 0x00800000,       // sound position needs to be updated in relation to another entity
		EF_DROPPEDWEAPON = 0x01000000,      // it's a dropped weapon
		EF_DISINTEGRATION = 0x02000000,     // being disintegrated by the disruptor
		EF_INVULNERABLE = 0x04000000,       // just spawned in or whatever, so is protected
	}

	public enum GameEntityFlags : int // Probably useless
    {
		FL_GODMODE = 0x00000010,
		FL_NOTARGET = 0x00000020,
		FL_TEAMSLAVE = 0x00000400,  // not the first on the team
		FL_NO_KNOCKBACK = 0x00000800,
		FL_DROPPED_ITEM = 0x00001000,
		FL_NO_BOTS = 0x00002000,    // spawn point not for bot use
		FL_NO_HUMANS = 0x00004000,  // spawn point just for bots
		FL_FORCE_GESTURE = 0x00008000   // force gesture on client
	}

	public enum entityType_t{
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
		ET_GRAPPLE,             // grapple hooked on wall
		ET_TEAM,
		ET_BODY,

		ET_EVENTS               // any of the EV_* events can be added freestanding
								// by setting eType to ET_EVENTS + eventNum
								// this avoids having to set eFlags and eventNum
	}

	public static class ItemList
	{
		public const int MaxItemModels = 4;

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
		}

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
			WP_SABER,                // NOTE: lots of code assumes this is the first weapon (... which is crap) so be careful -Ste.
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
			HI_DATAPAD,
			HI_BINOCULARS,
			HI_SENTRY_GUN,

			HI_NUM_HOLDABLE
		}
		public enum powerup_t : int
		{
			PW_NONE,

			PW_QUAD,
			PW_BATTLESUIT,
			PW_HASTE,
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
			PW_FORCE_LIGHTNING,
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
			public void Add(string classname,string pickup_sound,string[] world_model,string view_model,string icon,int quantity,itemType_t giType,int giTag,string precaches,string sounds)
			{
				this.Add(new gitem_s()
				{
					classname = classname,
					pickup_sound = pickup_sound,
					world_model = world_model,
					view_model=view_model,
					icon=icon,
					quantity=quantity,
					giType=giType,
					giTag=giTag,
					precaches=precaches,
					sounds=sounds
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
		}

		//static int[] stuff = new int[]  { 1,2,3};

		static public ItemListArray bg_itemlist = new ItemListArray() {
				{
					null,				// classname	
					null,				// pickup_sound
					new string[]{   null,			// world_model[0]
						null,			// world_model[1]
						null, null} ,			// world_model[2],[3]
					null,				// view_model
			/* icon */		null,		// icon
			/* pickup */	//null,		// pickup_name
					0,					// quantity
					0,					// giType (itemType_t.IT_*)
					0,					// giTag
			/* precache */ "",			// precaches
			/* sounds */ ""				// sounds
				},	// leave index 0 alone

				//
				// Pickups
				//

			/*QUAKED item_shield_sm_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Instant shield pickup, restores 25
			*/
				{
					"item_shield_sm_instant",
					"sound/player/pickupshield.wav",
					new string[] { "models/map_objects/mp/psd_sm.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/mp/small_shield",
			/* pickup *///	"Shield Small",
					25,
					itemType_t.IT_ARMOR,
					1, //special for shield - max on pickup is maxhealth*tag, thus small shield goes up to 100 shield
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_shield_lrg_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Instant shield pickup, restores 100
			*/
				{
					"item_shield_lrg_instant",
					"sound/player/pickupshield.wav",
					new string[] { "models/map_objects/mp/psd.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/mp/large_shield",
			/* pickup *///	"Shield Large",
					100,
					itemType_t.IT_ARMOR,
					2, //special for shield - max on pickup is maxhealth*tag, thus large shield goes up to 200 shield
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_medpak_instant (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Instant medpack pickup, heals 25
			*/
				{
					"item_medpak_instant",
					"sound/player/pickuphealth.wav",
					new string[] { "models/map_objects/mp/medpac.md3",
					null, null, null },
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_medkit",
			/* pickup *///	"Medpack",
					25,
					itemType_t.IT_HEALTH,
					0,
			/* precache */ "",
			/* sounds */ ""
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
					new string[] { "models/items/remote.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_seeker",
			/* pickup *///	"Seeker Drone",
					120,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_SEEKER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_shield (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
			Portable shield
			*/
				{
					"item_shield",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/map_objects/mp/shield.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_shieldwall",
			/* pickup *///	"Forcefield",
					120,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_SHIELD,
			/* precache */ "",
			/* sounds */ "sound/weapons/detpack/stick.wav sound/movers/doors/forcefield_on.wav sound/movers/doors/forcefield_off.wav sound/movers/doors/forcefield_lp.wav sound/effects/bumpfield.wav"
				},

			/*QUAKED item_medpac (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
			Bacta canister pickup, heals 25 on use
			*/
				{
					"item_medpac",	//should be item_bacta
					"sound/weapons/w_pkup.wav",
					new string[] { "models/map_objects/mp/bacta.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_bacta",
			/* pickup *///	"Bacta Canister",
					25,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_MEDPAC,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_datapad (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
			Do not place this.
			*/
				{
					"item_datapad",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/items/datapad.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		null,
			/* pickup *///	"Datapad",
					1,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_DATAPAD,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_binoculars (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
			These will be standard equipment on the player - DO NOT PLACE
			*/
				{
					"item_binoculars",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/items/binoculars.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_zoom",
			/* pickup *///	"Binoculars",
					60,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_BINOCULARS,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_sentry_gun (.3 .3 1) (-8 -8 -0) (8 8 16) suspended
			Sentry gun inventory pickup.
			*/
				{
					"item_sentry_gun",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/items/psgun.glm",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_sentrygun",
			/* pickup *///	"Sentry Gun",
					120,
					itemType_t.IT_HOLDABLE,
					(int)holdable_t.HI_SENTRY_GUN,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_force_enlighten_light (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Adds one rank to all Force powers temporarily. Only light jedi can use.
			*/
				{
					"item_force_enlighten_light",
					"sound/player/enlightenment.wav",
					new string[] { "models/map_objects/mp/jedi_enlightenment.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_jlight",
			/* pickup *///	"Light Force Enlightenment",
					25,
					itemType_t.IT_POWERUP,
					(int)powerup_t.PW_FORCE_ENLIGHTENED_LIGHT,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_force_enlighten_dark (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Adds one rank to all Force powers temporarily. Only dark jedi can use.
			*/
				{
					"item_force_enlighten_dark",
					"sound/player/enlightenment.wav",
					new string[] { "models/map_objects/mp/dk_enlightenment.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_dklight",
			/* pickup *///	"Dark Force Enlightenment",
					25,
					itemType_t.IT_POWERUP,
					(int)powerup_t.PW_FORCE_ENLIGHTENED_DARK,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_force_boon (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Unlimited Force Pool for a short time.
			*/
				{
					"item_force_boon",
					"sound/player/boon.wav",
					new string[] { "models/map_objects/mp/force_boon.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_fboon",
			/* pickup *///	"Force Boon",
					25,
					itemType_t.IT_POWERUP,
					(int)powerup_t.PW_FORCE_BOON,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED item_ysalimari (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			A small lizard carried on the player, which prevents the possessor from using any Force power.  However, he is unaffected by any Force power.
			*/
				{
					"item_ysalimari",
					"sound/player/ysalimari.wav",
					new string[] { "models/map_objects/mp/ysalimari.md3",
					null, null, null} ,
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_ysamari",
			/* pickup *///	"Ysalamiri",
					25,
					itemType_t.IT_POWERUP,
					(int)powerup_t.PW_YSALAMIRI,
			/* precache */ "",
			/* sounds */ ""
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
					new string[] { "models/weapons2/stun_baton/baton_w.glm",
					null, null, null},
			/* view */		"models/weapons2/stun_baton/baton.md3", 
			/* icon */		"gfx/hud/w_icon_stunbaton",
			/* pickup *///	"Stun Baton",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_STUN_BATON,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_saber (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Don't place this
			*/
				{
					"weapon_saber",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/saber/saber_w.glm",
					null, null, null},
			/* view */		"models/weapons2/saber/saber_w.md3",
			/* icon */		"gfx/hud/w_icon_lightsaber",
			/* pickup *///	"Lightsaber",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_SABER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_bryar_pistol (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Don't place this
			*/
				{
					"weapon_bryar_pistol",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/briar_pistol/briar_pistol_w.glm",
					null, null, null},
			/* view */		"models/weapons2/briar_pistol/briar_pistol.md3", 
			/* icon */		"gfx/hud/w_icon_rifle",
			/* pickup *///	"Bryar Pistol",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_BRYAR_PISTOL,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_blaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_blaster",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/blaster_r/blaster_w.glm",
					null, null, null},
			/* view */		"models/weapons2/blaster_r/blaster.md3", 
			/* icon */		"gfx/hud/w_icon_blaster",
			/* pickup *///	"E11 Blaster Rifle",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_BLASTER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_disruptor (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_disruptor",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/disruptor/disruptor_w.glm",
					null, null, null},
			/* view */		"models/weapons2/disruptor/disruptor.md3", 
			/* icon */		"gfx/hud/w_icon_disruptor",
			/* pickup *///	"Tenloss Disruptor Rifle",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_DISRUPTOR,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_bowcaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_bowcaster",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/bowcaster/bowcaster_w.glm",
					null, null, null},
			/* view */		"models/weapons2/bowcaster/bowcaster.md3", 
			/* icon */		"gfx/hud/w_icon_bowcaster",
			/* pickup *///	"Wookiee Bowcaster",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_BOWCASTER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_repeater (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_repeater",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/heavy_repeater/heavy_repeater_w.glm",
					null, null, null},
			/* view */		"models/weapons2/heavy_repeater/heavy_repeater.md3", 
			/* icon */		"gfx/hud/w_icon_repeater",
			/* pickup *///	"Imperial Heavy Repeater",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_REPEATER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_demp2 (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			NOTENOTE This weapon is not yet complete.  Don't place it.
			*/
				{
					"weapon_demp2",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/demp2/demp2_w.glm",
					null, null, null},
			/* view */		"models/weapons2/demp2/demp2.md3", 
			/* icon */		"gfx/hud/w_icon_demp2",
			/* pickup *///	"DEMP2",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_DEMP2,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_flechette (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_flechette",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/golan_arms/golan_arms_w.glm",
					null, null, null},
			/* view */		"models/weapons2/golan_arms/golan_arms.md3", 
			/* icon */		"gfx/hud/w_icon_flechette",
			/* pickup *///	"Golan Arms Flechette",
					100,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_FLECHETTE,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_rocket_launcher (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_rocket_launcher",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/merr_sonn/merr_sonn_w.glm",
					null, null, null},
			/* view */		"models/weapons2/merr_sonn/merr_sonn.md3", 
			/* icon */		"gfx/hud/w_icon_merrsonn",
			/* pickup *///	"Merr-Sonn Missile System",
					3,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_ROCKET_LAUNCHER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_thermal (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"(int)ammo_t.AMMO_thermal",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/thermal/thermal_pu.md3",
					"models/weapons2/thermal/thermal_w.glm", null, null},
			/* view */		"models/weapons2/thermal/thermal.md3", 
			/* icon */		"gfx/hud/w_icon_thermal",
			/* pickup *///	"Thermal Detonators",
					4,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_THERMAL,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_tripmine (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"(int)ammo_t.AMMO_tripmine",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/laser_trap/laser_trap_pu.md3",
					"models/weapons2/laser_trap/laser_trap_w.glm", null, null},
			/* view */		"models/weapons2/laser_trap/laser_trap.md3", 
			/* icon */		"gfx/hud/w_icon_tripmine",
			/* pickup *///	"Trip Mines",
					3,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_TRIPMINE,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_detpack (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"(int)ammo_t.AMMO_detpack",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/detpack/det_pack_pu.md3", "models/weapons2/detpack/det_pack_proj.glm", "models/weapons2/detpack/det_pack_w.glm", null},
			/* view */		"models/weapons2/detpack/det_pack.md3", 
			/* icon */		"gfx/hud/w_icon_detpack",
			/* pickup *///	"Det Packs",
					3,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_DETPACK,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_thermal (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_thermal",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/thermal/thermal_w.glm", "models/weapons2/thermal/thermal_pu.md3",
					null, null },
			/* view */		"models/weapons2/thermal/thermal.md3", 
			/* icon */		"gfx/hud/w_icon_thermal",
			/* pickup *///	"Thermal Detonator",
					4,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_THERMAL,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_trip_mine (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_trip_mine",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/laser_trap/laser_trap_w.glm", "models/weapons2/laser_trap/laser_trap_pu.md3",
					null, null},
			/* view */		"models/weapons2/laser_trap/laser_trap.md3", 
			/* icon */		"gfx/hud/w_icon_tripmine",
			/* pickup *///	"Trip Mine",
					3,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_TRIP_MINE,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_det_pack (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_det_pack",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/detpack/det_pack_proj.glm", "models/weapons2/detpack/det_pack_pu.md3", "models/weapons2/detpack/det_pack_w.glm", null},
			/* view */		"models/weapons2/detpack/det_pack.md3", 
			/* icon */		"gfx/hud/w_icon_detpack",
			/* pickup *///	"Det Pack",
					3,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_DET_PACK,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED weapon_emplaced (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			*/
				{
					"weapon_emplaced",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/blaster_r/blaster_w.glm",
					null, null, null},
			/* view */		"models/weapons2/blaster_r/blaster.md3", 
			/* icon */		"gfx/hud/w_icon_blaster",
			/* pickup *///	"Emplaced Gun",
					50,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_EMPLACED_GUN,
			/* precache */ "",
			/* sounds */ ""
				},


			//NOTE: This is to keep things from messing up because the turret weapon type isn't real
				{
					"weapon_turretwp",
					"sound/weapons/w_pkup.wav",
					new string[] { "models/weapons2/blaster_r/blaster_w.glm",
					null, null, null},
			/* view */		"models/weapons2/blaster_r/blaster.md3", 
			/* icon */		"gfx/hud/w_icon_blaster",
			/* pickup *///	"Turret Gun",
					50,
					itemType_t.IT_WEAPON,
					(int)weapon_t.WP_TURRET,
			/* precache */ "",
			/* sounds */ ""
				},

				//
				// AMMO ITEMS
				//

			/*QUAKED (int)ammo_t.AMMO_force (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Don't place this
			*/
				{
					"(int)ammo_t.AMMO_force",
					"sound/player/pickupenergy.wav",
					new string[] { "models/items/energy_cell.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/hud/w_icon_blaster",
			/* pickup *///	"Force??",
					100,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_FORCE,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_blaster (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Ammo for the Bryar and Blaster pistols.
			*/
				{
					"(int)ammo_t.AMMO_blaster",
					"sound/player/pickupenergy.wav",
					new string[] { "models/items/energy_cell.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/hud/i_icon_battery",
			/* pickup *///	"Blaster Pack",
					100,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_BLASTER,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_powercell (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Ammo for Tenloss Disruptor, Wookie Bowcaster, and the Destructive Electro Magnetic Pulse (demp2 ) guns
			*/
				{
					"(int)ammo_t.AMMO_powercell",
					"sound/player/pickupenergy.wav",
					new string[] { "models/items/power_cell.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/mp/(int)ammo_t.AMMO_power_cell",
			/* pickup *///	"Power Cell",
					100,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_POWERCELL,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_metallic_bolts (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Ammo for Imperial Heavy Repeater and the Golan Arms Flechette
			*/
				{
					"(int)ammo_t.AMMO_metallic_bolts",
					"sound/player/pickupenergy.wav",
					new string[] { "models/items/metallic_bolts.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/mp/(int)ammo_t.AMMO_metallic_bolts",
			/* pickup *///	"Metallic Bolts",
					100,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_METAL_BOLTS,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED (int)ammo_t.AMMO_rockets (.3 .3 1) (-16 -16 -16) (16 16 16) suspended
			Ammo for Merr-Sonn portable missile launcher
			*/
				{
					"(int)ammo_t.AMMO_rockets",
					"sound/player/pickupenergy.wav",
					new string[] { "models/items/rockets.md3",
					null, null, null},
			/* view */		null,			
			/* icon */		"gfx/mp/(int)ammo_t.AMMO_rockets",
			/* pickup *///	"Rockets",
					3,
					itemType_t.IT_AMMO,
					(int)ammo_t.AMMO_ROCKETS,
			/* precache */ "",
			/* sounds */ ""
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
					new string[] { "models/flags/r_flag.md3",
					"models/flags/r_flag_ysal.md3", null, null },
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_rflag",
			/* pickup *///	"Red Flag",
					0,
					itemType_t.IT_TEAM,
					(int)powerup_t.PW_REDFLAG,
			/* precache */ "",
			/* sounds */ ""
				},

			/*QUAKED team_CTF_blueflag (0 0 1) (-16 -16 -16) (16 16 16)
			Only in CTF games
			*/
				{
					"team_CTF_blueflag",
					null,
					new string[] { "models/flags/b_flag.md3",
					"models/flags/b_flag_ysal.md3", null, null },
			/* view */		null,			
			/* icon */		"gfx/hud/mpi_bflag",
			/* pickup *///	"Blue Flag",
					0,
					itemType_t.IT_TEAM,
					(int)powerup_t.PW_BLUEFLAG,
			/* precache */ "",
			/* sounds */ ""
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
					new string[] { "models/flags/n_flag.md3",
					null, null, null },
			/* view */		null,			
			/* icon */		"icons/iconf_neutral1",
			/* pickup *///	"Neutral Flag",
					0,
					itemType_t.IT_TEAM,
					(int)powerup_t.PW_NEUTRALFLAG,
			/* precache */ "",
			/* sounds */ ""
				},

				{
					"item_redcube",
					"sound/player/pickupenergy.wav",
					new string[] { "models/powerups/orb/r_orb.md3",
					null, null, null },
			/* view */		null,			
			/* icon */		"icons/iconh_rorb",
			/* pickup *///	"Red Cube",
					0,
					itemType_t.IT_TEAM,
					0,
			/* precache */ "",
			/* sounds */ ""
				},

				{
					"item_bluecube",
					"sound/player/pickupenergy.wav",
					new string[] { "models/powerups/orb/b_orb.md3",
					null, null, null },
			/* view */		null,			
			/* icon */		"icons/iconh_borb",
			/* pickup *///	"Blue Cube",
					0,
					itemType_t.IT_TEAM,
					0,
			/* precache */ "",
			/* sounds */ ""
				}
			};

		/*
		==============
		BG_FindItemForPowerup
		==============
		*/
		public static int? BG_FindItemForPowerup(powerup_t pw)
		{
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
		*/
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
		}


		/*
		===============
		BG_FindItemForWeapon

		===============
		*/
		public static int? BG_FindItemForWeapon(weapon_t weapon)
		{
			/*gitem_t* it;

			for (it = bg_itemlist + 1; it->classname; it++)
			{
				if (it->giType == IT_WEAPON && it->giTag == (int)weapon)
				{
					return it;
				}
			}

			Com_Error(ERR_DROP, "Couldn't find item for weapon %i", weapon);
			return NULL;*/

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

		/*
		===============
		BG_FindItem

		===============
		*/
		public static int? BG_FindItem(string classname)
		{
			/*gitem_t* it;

			for (it = bg_itemlist + 1 ; it->classname ; it++ ) {
				if ( !Q_stricmp(it->classname, classname) )
					return it;
			}

			return NULL;*/
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

	}
}
