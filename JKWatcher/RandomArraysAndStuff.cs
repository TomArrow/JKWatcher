using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{

	public enum MeansOfDeathGeneral {
		MOD_UNKNOWN_GENERAL,
		MOD_STUN_BATON_GENERAL,
		MOD_MELEE_GENERAL,
		MOD_SABER_GENERAL,
		MOD_BRYAR_PISTOL_GENERAL,
		MOD_BRYAR_PISTOL_ALT_GENERAL,
		MOD_BLASTER_GENERAL,
		MOD_DISRUPTOR_GENERAL,
		MOD_DISRUPTOR_SPLASH_GENERAL,
		MOD_DISRUPTOR_SNIPER_GENERAL,
		MOD_BOWCASTER_GENERAL,
		MOD_REPEATER_GENERAL,
		MOD_REPEATER_ALT_GENERAL,
		MOD_REPEATER_ALT_SPLASH_GENERAL,
		MOD_DEMP2_GENERAL,
		MOD_DEMP2_ALT_GENERAL,
		MOD_FLECHETTE_GENERAL,
		MOD_FLECHETTE_ALT_SPLASH_GENERAL,
		MOD_ROCKET_GENERAL,
		MOD_ROCKET_SPLASH_GENERAL,
		MOD_ROCKET_HOMING_GENERAL,
		MOD_ROCKET_HOMING_SPLASH_GENERAL,
		MOD_THERMAL_GENERAL,
		MOD_THERMAL_SPLASH_GENERAL,
		MOD_TRIP_MINE_SPLASH_GENERAL,
		MOD_TIMED_MINE_SPLASH_GENERAL,
		MOD_DET_PACK_SPLASH_GENERAL,
		MOD_FORCE_DARK_GENERAL,
		MOD_SENTRY_GENERAL,
		MOD_WATER_GENERAL,
		MOD_SLIME_GENERAL,
		MOD_LAVA_GENERAL,
		MOD_CRUSH_GENERAL,
		MOD_TELEFRAG_GENERAL,
		MOD_FALLING_GENERAL,
		MOD_SUICIDE_GENERAL,
		MOD_TARGET_LASER_GENERAL,
		MOD_TRIGGER_HURT_GENERAL,

		// JK3
		MOD_TURBLAST_GENERAL,
		MOD_VEHICLE_GENERAL,
		MOD_CONC_GENERAL,
		MOD_CONC_ALT_GENERAL,
		MOD_COLLISION_GENERAL,
		MOD_VEH_EXPLOSION_GENERAL,
		MOD_TEAM_CHANGE_GENERAL,

		//q3
		MOD_SHOTGUN_GENERAL,
		MOD_GAUNTLET_GENERAL,
		MOD_MACHINEGUN_GENERAL,
		MOD_GRENADELAUNCHER_GENERAL,
		MOD_GRENADE_SPLASH_GENERAL,
		MOD_PLASMA_GENERAL,
		MOD_PLASMA_SPLASH_GENERAL,
		MOD_RAILGUN_GENERAL,
		MOD_LIGHTNING_GENERAL,
		MOD_BFG_GENERAL,
		MOD_BFG_SPLASH_GENERAL,
		//#ifdef MISSIONPACK
		MOD_NAIL_GENERAL,
		MOD_CHAINGUN_GENERAL,
		MOD_PROXIMITY_MINE_GENERAL,
		MOD_KAMIKAZE_GENERAL,
		MOD_JUICED_GENERAL,
		//#endif
		MOD_GRAPPLE_GENERAL,

		//quake live:
		MOD_SWITCH_TEAMS_GENERAL,  // 29
		MOD_THAW_GENERAL,
		MOD_LIGHTNING_DISCHARGE_GENERAL,  // demo?
		MOD_HMG_GENERAL,
		MOD_RAILGUN_HEADSHOT_GENERAL,

		//jk2sp:
		MOD_BLASTER_ALT_GENERAL,
		MOD_SNIPER_GENERAL, // this is when energy crackle kills yu?!?
		MOD_BOWCASTER_ALT_GENERAL,
		MOD_SEEKER_GENERAL,
		MOD_FORCE_GRIP_GENERAL,
		MOD_EMPLACED_GENERAL,
		MOD_ELECTROCUTE_GENERAL,
		MOD_EXPLOSIVE_GENERAL,
		MOD_EXPLOSIVE_SPLASH_GENERAL,
		MOD_KNOCKOUT_GENERAL,
		MOD_ENERGY_GENERAL,
		MOD_ENERGY_SPLASH_GENERAL,
		MOD_IMPACT_GENERAL,

		// MOHAA:
		MOD_CRUSH_EVERY_FRAME_GENERAL,
		MOD_LAST_SELF_INFLICTED_GENERAL,
		MOD_EXPLOSION_GENERAL,
		MOD_EXPLODEWALL_GENERAL,
		MOD_ELECTRIC_GENERAL,
		MOD_ELECTRICWATER_GENERAL,
		MOD_THROWNOBJECT_GENERAL,
		MOD_GRENADE_GENERAL,
		MOD_BEAM_GENERAL,
		MOD_BULLET_GENERAL,
		MOD_FAST_BULLET_GENERAL,
		MOD_FIRE_GENERAL,
		MOD_FLASHBANG_GENERAL,
		MOD_ON_FIRE_GENERAL,
		MOD_GIB_GENERAL,
		MOD_IMPALE_GENERAL,
		MOD_BASH_GENERAL,
		MOD_LANDMINE_GENERAL, // Breakthrough/SH? Not sure if actually used.

		//MBII
		MOD_CHANGEDCLASSES_GENERAL,
		MOD_WENTSPECTATOR_GENERAL,
		MOD_CHANGEDTEAMS_GENERAL,
		MOD_TOOMANYTKS_GENERAL,
		MOD_EWEBEXPLOSION_GENERAL,
		MOD_SPACE_GENERAL,
		MOD_ENV_LIGHTNING_GENERAL,
		MOD_TRIGGER_HURT_ELECTRICAL_GENERAL,
		MOD_TRIGGER_HURT_FLAME_GENERAL,
		MOD_TRIGGER_HURT_POISON_GENERAL,
		MOD_WATERELECTRICAL_GENERAL,
		MOD_FORCE_LIGHTNING_GENERAL,
		MOD_FORCE_DESTRUCTION_GENERAL,
		MOD_FORCE_DEADLY_SIGHT_GENERAL,
		MOD_BRYAR_OLD_GENERAL,
		MOD_BRYAR_OLD_ALT_GENERAL,
		MOD_BOWCASTER_CHARGED_GENERAL,
		MOD_SEEKERDRONE_GENERAL,
		MOD_LASER_GENERAL,
		MOD_FLAMETHROWER_GENERAL,
		MOD_ICETHROWER_GENERAL,
		MOD_ION_BLAST_GENERAL,
		MOD_SBD_CANNON_GENERAL,
		MOD_REALCONC_GENERAL,
		MOD_REALCONC_ALT_GENERAL,
		MOD_REALCONC_SPLASH_GENERAL,
		MOD_T21_GENERAL,
		MOD_T21ALT_GENERAL,
		MOD_MICRO_THERMAL_GENERAL,
		MOD_REAL_THERMAL_GENERAL,
		MOD_PULSENADE_GENERAL,
		MOD_LAUNCHED_PULSENADE_GENERAL,
		MOD_DEP_PACK_SPLASH_GENERAL,
		MOD_CRYOBAN_BLAST_GENERAL,
		MOD_FIRE_BLAST_GENERAL,
		MOD_FIRE_BLAST_BURN_GENERAL,
		MOD_SONIC_BLAST_GENERAL,
		MOD_TRACKINGDART_GENERAL,
		MOD_POISONDART_GENERAL,
		MOD_POISON_GENERAL,
		MOD_ACID_GENERAL,
		MOD_MELEE_KICK_GENERAL,
		MOD_MELEE_KATA_GENERAL,
		MOD_ASSA_GENERAL,
		MOD_SABER_THROW_GENERAL,
		MOD_SABER_HPDRAIN_GENERAL,
		MOD_SHOCKWAVE_GENERAL,

		MOD_MAX_GENERAL,
	}

	public enum SaberMovesGeneral
	{
		//totally invalid
		LS_INVALID_GENERAL = -1,
		// Invalid, or saber not armed
		LS_NONE_GENERAL = 0,

		// General movements with saber
		LS_READY_GENERAL,
		LS_DRAW_GENERAL,
		LS_PUTAWAY_GENERAL,

		// Attacks
		LS_A_TL2BR_GENERAL,//4
		LS_A_L2R_GENERAL,
		LS_A_BL2TR_GENERAL,
		LS_A_BR2TL_GENERAL,
		LS_A_R2L_GENERAL,
		LS_A_TR2BL_GENERAL,
		LS_A_T2B_GENERAL,
		LS_A_BACKSTAB_GENERAL,
		LS_A_BACK_GENERAL,
		LS_A_BACK_CR_GENERAL,
		LS_ROLL_STAB_GENERAL,
		LS_A_LUNGE_GENERAL,
		LS_A_JUMP_T__B__GENERAL,
		LS_A_FLIP_STAB_GENERAL,
		LS_A_FLIP_SLASH_GENERAL,
		LS_JUMPATTACK_DUAL_GENERAL,
		LS_JUMPATTACK_ARIAL_LEFT_GENERAL,
		LS_JUMPATTACK_ARIAL_RIGHT_GENERAL,
		LS_JUMPATTACK_CART_LEFT_GENERAL,
		LS_JUMPATTACK_CART_RIGHT_GENERAL,
		LS_JUMPATTACK_STAFF_LEFT_GENERAL,
		LS_JUMPATTACK_STAFF_RIGHT_GENERAL,
		LS_BUTTERFLY_LEFT_GENERAL,
		LS_BUTTERFLY_RIGHT_GENERAL,
		LS_A_BACKFLIP_ATK_GENERAL,
		LS_SPINATTACK_DUAL_GENERAL,
		LS_SPINATTACK_GENERAL,
		LS_LEAP_ATTACK_GENERAL,
		LS_SWOOP_ATTACK_RIGHT_GENERAL,
		LS_SWOOP_ATTACK_LEFT_GENERAL,
		LS_TAUNTAUN_ATTACK_RIGHT_GENERAL,
		LS_TAUNTAUN_ATTACK_LEFT_GENERAL,
		LS_KICK_F_GENERAL,
		LS_KICK_B_GENERAL,
		LS_KICK_R_GENERAL,
		LS_KICK_L_GENERAL,
		LS_KICK_S_GENERAL,
		LS_KICK_BF_GENERAL,
		LS_KICK_RL_GENERAL,
		LS_KICK_F_AIR_GENERAL,
		LS_KICK_B_AIR_GENERAL,
		LS_KICK_R_AIR_GENERAL,
		LS_KICK_L_AIR_GENERAL,
		LS_STABDOWN_GENERAL,
		LS_STABDOWN_STAFF_GENERAL,
		LS_STABDOWN_DUAL_GENERAL,
		LS_DUAL_SPIN_PROTECT_GENERAL,
		LS_STAFF_SOULCAL_GENERAL,
		LS_A1_SPECIAL_GENERAL,
		LS_A2_SPECIAL_GENERAL,
		LS_A3_SPECIAL_GENERAL,
		LS_UPSIDE_DOWN_ATTACK_GENERAL,
		LS_PULL_ATTACK_STAB_GENERAL,
		LS_PULL_ATTACK_SWING_GENERAL,
		LS_SPINATTACK_ALORA_GENERAL,
		LS_DUAL_FB_GENERAL,
		LS_DUAL_LR_GENERAL,
		LS_HILT_BASH_GENERAL,

		//starts
		LS_S_TL2BR_GENERAL,//26
		LS_S_L2R_GENERAL,
		LS_S_BL2TR_GENERAL,//# Start of attack chaining to SLASH LR2UL
		LS_S_BR2TL_GENERAL,//# Start of attack chaining to SLASH LR2UL
		LS_S_R2L_GENERAL,
		LS_S_TR2BL_GENERAL,
		LS_S_T2B_GENERAL,

		//returns
		LS_R_TL2BR_GENERAL,//33
		LS_R_L2R_GENERAL,
		LS_R_BL2TR_GENERAL,
		LS_R_BR2TL_GENERAL,
		LS_R_R2L_GENERAL,
		LS_R_TR2BL_GENERAL,
		LS_R_T2B_GENERAL,

		//transitions
		LS_T1_BR__R_GENERAL,//40
		LS_T1_BR_TR_GENERAL,
		LS_T1_BR_T__GENERAL,
		LS_T1_BR_TL_GENERAL,
		LS_T1_BR__L_GENERAL,
		LS_T1_BR_BL_GENERAL,
		LS_T1__R_BR_GENERAL,//46
		LS_T1__R_TR_GENERAL,
		LS_T1__R_T__GENERAL,
		LS_T1__R_TL_GENERAL,
		LS_T1__R__L_GENERAL,
		LS_T1__R_BL_GENERAL,
		LS_T1_TR_BR_GENERAL,//52
		LS_T1_TR__R_GENERAL,
		LS_T1_TR_T__GENERAL,
		LS_T1_TR_TL_GENERAL,
		LS_T1_TR__L_GENERAL,
		LS_T1_TR_BL_GENERAL,
		LS_T1_T__BR_GENERAL,//58
		LS_T1_T___R_GENERAL,
		LS_T1_T__TR_GENERAL,
		LS_T1_T__TL_GENERAL,
		LS_T1_T___L_GENERAL,
		LS_T1_T__BL_GENERAL,
		LS_T1_TL_BR_GENERAL,//64
		LS_T1_TL__R_GENERAL,
		LS_T1_TL_TR_GENERAL,
		LS_T1_TL_T__GENERAL,
		LS_T1_TL__L_GENERAL,
		LS_T1_TL_BL_GENERAL,
		LS_T1__L_BR_GENERAL,//70
		LS_T1__L__R_GENERAL,
		LS_T1__L_TR_GENERAL,
		LS_T1__L_T__GENERAL,
		LS_T1__L_TL_GENERAL,
		LS_T1__L_BL_GENERAL,
		LS_T1_BL_BR_GENERAL,//76
		LS_T1_BL__R_GENERAL,
		LS_T1_BL_TR_GENERAL,
		LS_T1_BL_T__GENERAL,
		LS_T1_BL_TL_GENERAL,
		LS_T1_BL__L_GENERAL,

		//Bounces
		LS_B1_BR_GENERAL,
		LS_B1__R_GENERAL,
		LS_B1_TR_GENERAL,
		LS_B1_T__GENERAL,
		LS_B1_TL_GENERAL,
		LS_B1__L_GENERAL,
		LS_B1_BL_GENERAL,

		//Deflected attacks
		LS_D1_BR_GENERAL,
		LS_D1__R_GENERAL,
		LS_D1_TR_GENERAL,
		LS_D1_T__GENERAL,
		LS_D1_TL_GENERAL,
		LS_D1__L_GENERAL,
		LS_D1_BL_GENERAL,
		LS_D1_B__GENERAL,

		//Reflected attacks
		LS_V1_BR_GENERAL,
		LS_V1__R_GENERAL,
		LS_V1_TR_GENERAL,
		LS_V1_T__GENERAL,
		LS_V1_TL_GENERAL,
		LS_V1__L_GENERAL,
		LS_V1_BL_GENERAL,
		LS_V1_B__GENERAL,

		// Broken parries
		LS_H1_T__GENERAL,//
		LS_H1_TR_GENERAL,
		LS_H1_TL_GENERAL,
		LS_H1_BR_GENERAL,
		LS_H1_B__GENERAL,
		LS_H1_BL_GENERAL,

		// Knockaways
		LS_K1_T__GENERAL,//
		LS_K1_TR_GENERAL,
		LS_K1_TL_GENERAL,
		LS_K1_BR_GENERAL,
		LS_K1_BL_GENERAL,

		// Parries
		LS_PARRY_UP_GENERAL,//
		LS_PARRY_UR_GENERAL,
		LS_PARRY_UL_GENERAL,
		LS_PARRY_LR_GENERAL,
		LS_PARRY_LL_GENERAL,

		// Projectile Reflections
		LS_REFLECT_UP_GENERAL,//
		LS_REFLECT_UR_GENERAL,
		LS_REFLECT_UL_GENERAL,
		LS_REFLECT_LR_GENERAL,
		LS_REFLECT_LL_GENERAL,

		LS_MOVE_MAX_GENERAL//
	}

	public static class RandomArraysAndStuff
    {
		public static SaberMovesGeneral GeneralizeSaberMove(int saberMove, bool isJKA = false)
        {
            if (isJKA && (saberMove + 1) < jkaToSaberMoveGeneral.Length && saberMove >= -1)
            {
				return jkaToSaberMoveGeneral[saberMove + 1]; // +1 because the array starts with invalid one, which has value -1
			} else if((saberMove+1)<jk2ToSaberMoveGeneral.Length && saberMove >= -1)
            {
				return jk2ToSaberMoveGeneral[saberMove+1]; // +1 because the array starts with invalid one, which has value -1
            }
            return SaberMovesGeneral.LS_INVALID_GENERAL;

        }
		public static MeansOfDeathGeneral GeneralizeMod(int mod, bool isJKA = false, bool mbII = false)
        {
            if (mbII && mod < mbIIModToGeneralMap1_9_3_1.Length && mod >= 0)
            {
				return mbIIModToGeneralMap1_9_3_1[mod]; 
			} else  if (isJKA && mod < jkaModToGeneralMap.Length && mod >= 0)
            {
				return jkaModToGeneralMap[mod];
			} else if(mod < jk2ModToGeneralMap.Length && mod >= 0)
            {
				return jk2ModToGeneralMap[mod];
            }
            return MeansOfDeathGeneral.MOD_UNKNOWN_GENERAL;

        }


        public static Dictionary<SaberMovesGeneral, string> saberMoveNamesGeneral = new Dictionary<SaberMovesGeneral, string>() {
			{SaberMovesGeneral.LS_INVALID_GENERAL, "_INVALID"},
	  {SaberMovesGeneral.LS_NONE_GENERAL, "_WEIRD"},

	  // General movements with saber
	  {SaberMovesGeneral.LS_READY_GENERAL, "_IDLE"},
	  {SaberMovesGeneral.LS_DRAW_GENERAL, "_DRAW"},
	  {SaberMovesGeneral.LS_PUTAWAY_GENERAL, "_PUTAWAY"},

	  // Attacks
	  {SaberMovesGeneral.LS_A_TL2BR_GENERAL, ""},//4
	  {SaberMovesGeneral.LS_A_L2R_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BL2TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BR2TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_R2L_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_TR2BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_T2B_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BACKSTAB_GENERAL, "_BLUBS"},
	  {SaberMovesGeneral.LS_A_BACK_GENERAL, "_BS"},
	  {SaberMovesGeneral.LS_A_BACK_CR_GENERAL, "_DBS"},

	  {SaberMovesGeneral.LS_ROLL_STAB_GENERAL, "_ROLLSTAB"}, // JKA

	  {SaberMovesGeneral.LS_A_LUNGE_GENERAL, "_UPCUT"},
	  {SaberMovesGeneral.LS_A_JUMP_T__B__GENERAL, "_DFA"},
	  {SaberMovesGeneral.LS_A_FLIP_STAB_GENERAL, "_YDFA"},
	  {SaberMovesGeneral.LS_A_FLIP_SLASH_GENERAL, "_YDFA"},

	  // JKA
		{SaberMovesGeneral.LS_JUMPATTACK_DUAL_GENERAL,"_BUTTERFLYDUAL"}, // Flip forward attack
		{SaberMovesGeneral.LS_JUMPATTACK_ARIAL_LEFT_GENERAL,"_CARTWHEEL"},
		{SaberMovesGeneral.LS_JUMPATTACK_ARIAL_RIGHT_GENERAL,"_CARTWHEEL"},
		{SaberMovesGeneral.LS_JUMPATTACK_CART_LEFT_GENERAL,"_CARTWHEEL"},
		{SaberMovesGeneral.LS_JUMPATTACK_CART_RIGHT_GENERAL,"_CARTWHEEL"},
		{SaberMovesGeneral.LS_JUMPATTACK_STAFF_LEFT_GENERAL,"_BUTTERFLYSTAFF"}, // Official butterfly but sabermoveData calls it dual jump attack staff(?!)
		{SaberMovesGeneral.LS_JUMPATTACK_STAFF_RIGHT_GENERAL,"_BUTTERFLYSTAFF"},
		{SaberMovesGeneral.LS_BUTTERFLY_LEFT_GENERAL,"_BUTTERFLYSTAFF2"}, // Not the official butterfly but actually named butterfly.. wtf
		{SaberMovesGeneral.LS_BUTTERFLY_RIGHT_GENERAL,"_BUTTERFLYSTAFF2"},
		{SaberMovesGeneral.LS_A_BACKFLIP_ATK_GENERAL,"_BFATK"},
		{SaberMovesGeneral.LS_SPINATTACK_DUAL_GENERAL,"_KATADUAL"}, // Dual spin attack
		{SaberMovesGeneral.LS_SPINATTACK_GENERAL,"_KATASTAFF2"}, // Saber staff twirl
		{SaberMovesGeneral.LS_LEAP_ATTACK_GENERAL,"_LONGLEAP"}, // idk wtf this is
		{SaberMovesGeneral.LS_SWOOP_ATTACK_RIGHT_GENERAL,"_SWOOP"}, // Idk if this is an actual attack. The animation is a guy sitting and swooping.. ?!?
		{SaberMovesGeneral.LS_SWOOP_ATTACK_LEFT_GENERAL,"_SWOOP"},	// Ooh. It might be if sitting on an animal or in a vehicle otherwise? Oh are they called "swoop bikes" ?
		{SaberMovesGeneral.LS_TAUNTAUN_ATTACK_RIGHT_GENERAL,"_TAUNTAUN"}, // thes are also sitting... hmm. sitting on a tauntaun? 
		{SaberMovesGeneral.LS_TAUNTAUN_ATTACK_LEFT_GENERAL,"_TAUNTAUN"},
		{SaberMovesGeneral.LS_KICK_F_GENERAL,"_KICKFRONT"},
		{SaberMovesGeneral.LS_KICK_B_GENERAL,"_KICKBACK"},
		{SaberMovesGeneral.LS_KICK_R_GENERAL,"_KICKSIDE"}, // what difference does it make...
		{SaberMovesGeneral.LS_KICK_L_GENERAL,"_KICKSIDE"},
		{SaberMovesGeneral.LS_KICK_S_GENERAL,"_KICKSPIN"}, // I havent investigated this too deeply. Idk how to do it
		{SaberMovesGeneral.LS_KICK_BF_GENERAL,"_KICKFRONTBACK"},
		{SaberMovesGeneral.LS_KICK_RL_GENERAL,"_KICKBOTHSIDES"},
		{SaberMovesGeneral.LS_KICK_F_AIR_GENERAL,"_KICKFRONTAIR"},
		{SaberMovesGeneral.LS_KICK_B_AIR_GENERAL,"_KICKBACKAIR"},
		{SaberMovesGeneral.LS_KICK_R_AIR_GENERAL,"_KICKSIDEAIR"},
		{SaberMovesGeneral.LS_KICK_L_AIR_GENERAL,"_KICKSIDEAIR"},
		{SaberMovesGeneral.LS_STABDOWN_GENERAL,"_STABGROUND"},
		{SaberMovesGeneral.LS_STABDOWN_STAFF_GENERAL,"_STABGROUNDSTAFF"},
		{SaberMovesGeneral.LS_STABDOWN_DUAL_GENERAL,"_STABGROUNDDUAL"},
		{SaberMovesGeneral.LS_DUAL_SPIN_PROTECT_GENERAL,"_DUALBARRIER"},	// Dual saber barrier (spinning sabers)
		{SaberMovesGeneral.LS_STAFF_SOULCAL_GENERAL,"_KATASTAFF"},
		{SaberMovesGeneral.LS_A1_SPECIAL_GENERAL,"_KATABLUE"}, // Fast attack kata
		{SaberMovesGeneral.LS_A2_SPECIAL_GENERAL,"_KATAYEL"},
		{SaberMovesGeneral.LS_A3_SPECIAL_GENERAL,"_KATARED"},
		{SaberMovesGeneral.LS_UPSIDE_DOWN_ATTACK_GENERAL,"_FLIPATK"}, // Can't find info on this. Animation looks like a vampire hanging upside down and wiggling a saber downwards
		{SaberMovesGeneral.LS_PULL_ATTACK_STAB_GENERAL,"_PULLSTAB"},	// Can't find info on this either. 
		{SaberMovesGeneral.LS_PULL_ATTACK_SWING_GENERAL,"_PULLSWING"},	// Some kind of animation that pulls someone in and stabs? Idk if its actually usable or how...
		{SaberMovesGeneral.LS_SPINATTACK_ALORA_GENERAL,"_ALORA"}, // "Alora Spin slash"? No info on it either idk. Might just all be single player stuff
		{SaberMovesGeneral.LS_DUAL_FB_GENERAL,"_DUALSTABFB"}, // Dual stab front back
		{SaberMovesGeneral.LS_DUAL_LR_GENERAL,"_DUALSTABLR"}, // dual stab left right
		{SaberMovesGeneral.LS_HILT_BASH_GENERAL,"_HILTBASH"}, // Staff handle bashed into face (like darth maul i guess?)
		// JKA end

	  //starts
	  {SaberMovesGeneral.LS_S_TL2BR_GENERAL, ""},//26
	  {SaberMovesGeneral.LS_S_L2R_GENERAL, ""},
	  {SaberMovesGeneral.LS_S_BL2TR_GENERAL, ""},//# Start of attack chaining to SLASH LR2UL
	  {SaberMovesGeneral.LS_S_BR2TL_GENERAL, ""},//# Start of attack chaining to SLASH LR2UL
	  {SaberMovesGeneral.LS_S_R2L_GENERAL, ""},
	  {SaberMovesGeneral.LS_S_TR2BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_S_T2B_GENERAL, ""},

	  //returns
	  {SaberMovesGeneral.LS_R_TL2BR_GENERAL, ""},//33
	  {SaberMovesGeneral.LS_R_L2R_GENERAL, ""},
	  {SaberMovesGeneral.LS_R_BL2TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_R_BR2TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_R_R2L_GENERAL, ""},
	  {SaberMovesGeneral.LS_R_TR2BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_R_T2B_GENERAL, ""},

	  //transitions
	  {SaberMovesGeneral.LS_T1_BR__R_GENERAL, ""},//40
	  {SaberMovesGeneral.LS_T1_BR_TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BR_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BR_TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BR__L_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BR_BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__R_BR_GENERAL, ""},//46
	  {SaberMovesGeneral.LS_T1__R_TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__R_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__R_TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__R__L_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__R_BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TR_BR_GENERAL, ""},//52
	  {SaberMovesGeneral.LS_T1_TR__R_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TR_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TR_TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TR__L_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TR_BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_T__BR_GENERAL, ""},//58
	  {SaberMovesGeneral.LS_T1_T___R_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_T__TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_T__TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_T___L_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_T__BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TL_BR_GENERAL, ""},//64
	  {SaberMovesGeneral.LS_T1_TL__R_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TL_TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TL_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TL__L_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_TL_BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__L_BR_GENERAL, ""},//70
	  {SaberMovesGeneral.LS_T1__L__R_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__L_TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__L_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__L_TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1__L_BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BL_BR_GENERAL, ""},//76
	  {SaberMovesGeneral.LS_T1_BL__R_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BL_TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BL_T__GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BL_TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_T1_BL__L_GENERAL, ""},

	  //Bounces
	  {SaberMovesGeneral.LS_B1_BR_GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1__R_GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1_TR_GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1_T__GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1_TL_GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1__L_GENERAL, "_BOUNCE"},
	  {SaberMovesGeneral.LS_B1_BL_GENERAL, "_BOUNCE"},

	  //Deflected attacks
	  {SaberMovesGeneral.LS_D1_BR_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1__R_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1_TR_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1_T__GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1_TL_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1__L_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1_BL_GENERAL, "_DEFLECT"},
	  {SaberMovesGeneral.LS_D1_B__GENERAL, "_DEFLECT"},

	  //Reflected attacks
	  {SaberMovesGeneral.LS_V1_BR_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1__R_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1_TR_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1_T__GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1_TL_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1__L_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1_BL_GENERAL, "_REFLECT"},
	  {SaberMovesGeneral.LS_V1_B__GENERAL, "_REFLECT"},

	  // Broken parries
	  {SaberMovesGeneral.LS_H1_T__GENERAL, "_BPARRY"},//
	  {SaberMovesGeneral.LS_H1_TR_GENERAL, "_BPARRY"},
	  {SaberMovesGeneral.LS_H1_TL_GENERAL, "_BPARRY"},
	  {SaberMovesGeneral.LS_H1_BR_GENERAL, "_BPARRY"},
	  {SaberMovesGeneral.LS_H1_B__GENERAL, "_BPARRY"},
	  {SaberMovesGeneral.LS_H1_BL_GENERAL, "_BPARRY"},

	  // Knockaways
	  {SaberMovesGeneral.LS_K1_T__GENERAL, "_KNOCKAWAY"},//
	  {SaberMovesGeneral.LS_K1_TR_GENERAL, "_KNOCKAWAY"},
	  {SaberMovesGeneral.LS_K1_TL_GENERAL, "_KNOCKAWAY"},
	  {SaberMovesGeneral.LS_K1_BR_GENERAL, "_KNOCKAWAY"},
	  {SaberMovesGeneral.LS_K1_BL_GENERAL, "_KNOCKAWAY"},

	  // Parries
	  {SaberMovesGeneral.LS_PARRY_UP_GENERAL, "_PARRY"},//
	  {SaberMovesGeneral.LS_PARRY_UR_GENERAL, "_PARRY"},
	  {SaberMovesGeneral.LS_PARRY_UL_GENERAL, "_PARRY"},
	  {SaberMovesGeneral.LS_PARRY_LR_GENERAL, "_PARRY"},
	  {SaberMovesGeneral.LS_PARRY_LL_GENERAL, "_PARRY"},

	  // Projectile Reflections
	  {SaberMovesGeneral.LS_REFLECT_UP_GENERAL, "_PREFLECT"},//
	  {SaberMovesGeneral.LS_REFLECT_UR_GENERAL, "_PREFLECT"},
	  {SaberMovesGeneral.LS_REFLECT_UL_GENERAL, "_PREFLECT"},
	  {SaberMovesGeneral.LS_REFLECT_LR_GENERAL, "_PREFLECT"},
	  {SaberMovesGeneral.LS_REFLECT_LL_GENERAL, "_PREFLECT"},
		};
        public static Dictionary<SaberMovesGeneral, string> saberMoveNamesGeneralShort = new Dictionary<SaberMovesGeneral, string>() {
			{SaberMovesGeneral.LS_INVALID_GENERAL, "_INV"},
	  {SaberMovesGeneral.LS_NONE_GENERAL, "_WEIRD"},

	  // General movements with saber
	  {SaberMovesGeneral.LS_READY_GENERAL, "_IDLE"},
	  {SaberMovesGeneral.LS_DRAW_GENERAL, "_DRAW"},
	  {SaberMovesGeneral.LS_PUTAWAY_GENERAL, "_PUTAW"},

	  // Attacks
	  {SaberMovesGeneral.LS_A_TL2BR_GENERAL, ""},//4
	  {SaberMovesGeneral.LS_A_L2R_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BL2TR_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BR2TL_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_R2L_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_TR2BL_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_T2B_GENERAL, ""},
	  {SaberMovesGeneral.LS_A_BACKSTAB_GENERAL, "_BLUBS"},
	  {SaberMovesGeneral.LS_A_BACK_GENERAL, "_BS"},
	  {SaberMovesGeneral.LS_A_BACK_CR_GENERAL, "_DBS"},

	  {SaberMovesGeneral.LS_ROLL_STAB_GENERAL, "_RLSTB"}, // JKA

	  {SaberMovesGeneral.LS_A_LUNGE_GENERAL, "_UPCUT"},
	  {SaberMovesGeneral.LS_A_JUMP_T__B__GENERAL, "_DFA"},
	  {SaberMovesGeneral.LS_A_FLIP_STAB_GENERAL, "_YDFA"},
	  {SaberMovesGeneral.LS_A_FLIP_SLASH_GENERAL, "_YDFA"},

	  // JKA
		{SaberMovesGeneral.LS_JUMPATTACK_DUAL_GENERAL,"_BFLY"}, // Flip forward attack
		{SaberMovesGeneral.LS_JUMPATTACK_ARIAL_LEFT_GENERAL,"_CRTW"},
		{SaberMovesGeneral.LS_JUMPATTACK_ARIAL_RIGHT_GENERAL,"_CRTW"},
		{SaberMovesGeneral.LS_JUMPATTACK_CART_LEFT_GENERAL,"_CRTW"},
		{SaberMovesGeneral.LS_JUMPATTACK_CART_RIGHT_GENERAL,"_CRTW"},
		{SaberMovesGeneral.LS_JUMPATTACK_STAFF_LEFT_GENERAL,"_BFLY"}, // Official butterfly but sabermoveData calls it dual jump attack staff(?!)
		{SaberMovesGeneral.LS_JUMPATTACK_STAFF_RIGHT_GENERAL,"_BFLY"},
		{SaberMovesGeneral.LS_BUTTERFLY_LEFT_GENERAL,"_BFLY"}, // Not the official butterfly but actually named butterfly.. wtf
		{SaberMovesGeneral.LS_BUTTERFLY_RIGHT_GENERAL,"_BFLY"},
		{SaberMovesGeneral.LS_A_BACKFLIP_ATK_GENERAL,"_BFATK"},
		{SaberMovesGeneral.LS_SPINATTACK_DUAL_GENERAL,"_KATA"}, // Dual spin attack
		{SaberMovesGeneral.LS_SPINATTACK_GENERAL,"_KATA"}, // Saber staff twirl
		{SaberMovesGeneral.LS_LEAP_ATTACK_GENERAL,"_LLEAP"}, // idk wtf this is
		{SaberMovesGeneral.LS_SWOOP_ATTACK_RIGHT_GENERAL,"_SWOOP"}, // Idk if this is an actual attack. The animation is a guy sitting and swooping.. ?!?
		{SaberMovesGeneral.LS_SWOOP_ATTACK_LEFT_GENERAL,"_SWOOP"},	// Ooh. It might be if sitting on an animal or in a vehicle otherwise? Oh are they called "swoop bikes" ?
		{SaberMovesGeneral.LS_TAUNTAUN_ATTACK_RIGHT_GENERAL,"_TAUN"}, // thes are also sitting... hmm. sitting on a tauntaun? 
		{SaberMovesGeneral.LS_TAUNTAUN_ATTACK_LEFT_GENERAL,"_TAUN"},
		{SaberMovesGeneral.LS_KICK_F_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_B_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_R_GENERAL,"_KICK"}, // what difference does it make...
		{SaberMovesGeneral.LS_KICK_L_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_S_GENERAL,"_KICK"}, // I havent investigated this too deeply. Idk how to do it
		{SaberMovesGeneral.LS_KICK_BF_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_RL_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_F_AIR_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_B_AIR_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_R_AIR_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_KICK_L_AIR_GENERAL,"_KICK"},
		{SaberMovesGeneral.LS_STABDOWN_GENERAL,"_STBDN"},
		{SaberMovesGeneral.LS_STABDOWN_STAFF_GENERAL,"_STBDN"},
		{SaberMovesGeneral.LS_STABDOWN_DUAL_GENERAL,"_STBDN"},
		{SaberMovesGeneral.LS_DUAL_SPIN_PROTECT_GENERAL,"_DUALBR"},	// Dual saber barrier (spinning sabers)
		{SaberMovesGeneral.LS_STAFF_SOULCAL_GENERAL,"_KATA"},
		{SaberMovesGeneral.LS_A1_SPECIAL_GENERAL,"_KATA"}, // Fast attack kata
		{SaberMovesGeneral.LS_A2_SPECIAL_GENERAL,"_KATA"},
		{SaberMovesGeneral.LS_A3_SPECIAL_GENERAL,"_KATA"},
		{SaberMovesGeneral.LS_UPSIDE_DOWN_ATTACK_GENERAL,"_FLIPATK"}, // Can't find info on this. Animation looks like a vampire hanging upside down and wiggling a saber downwards
		{SaberMovesGeneral.LS_PULL_ATTACK_STAB_GENERAL,"_PULLSTB"},	// Can't find info on this either. 
		{SaberMovesGeneral.LS_PULL_ATTACK_SWING_GENERAL,"_PULLSWNG"},	// Some kind of animation that pulls someone in and stabs? Idk if its actually usable or how...
		{SaberMovesGeneral.LS_SPINATTACK_ALORA_GENERAL,"_ALORA"}, // "Alora Spin slash"? No info on it either idk. Might just all be single player stuff
		{SaberMovesGeneral.LS_DUAL_FB_GENERAL,"_DUALSTB"}, // Dual stab front back
		{SaberMovesGeneral.LS_DUAL_LR_GENERAL,"_DUALSTB"}, // dual stab left right
		{SaberMovesGeneral.LS_HILT_BASH_GENERAL,"_HILT"}, // Staff handle bashed into face (like darth maul i guess?)
		// JKA end

	  //starts
		  {SaberMovesGeneral.LS_S_TL2BR_GENERAL, ""},//26
		  {SaberMovesGeneral.LS_S_L2R_GENERAL, ""},
		  {SaberMovesGeneral.LS_S_BL2TR_GENERAL, ""},//# Start of attack chaining to SLASH LR2UL
		  {SaberMovesGeneral.LS_S_BR2TL_GENERAL, ""},//# Start of attack chaining to SLASH LR2UL
		  {SaberMovesGeneral.LS_S_R2L_GENERAL, ""},
		  {SaberMovesGeneral.LS_S_TR2BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_S_T2B_GENERAL, ""},

		  //returns
		  {SaberMovesGeneral.LS_R_TL2BR_GENERAL, ""},//33
		  {SaberMovesGeneral.LS_R_L2R_GENERAL, ""},
		  {SaberMovesGeneral.LS_R_BL2TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_R_BR2TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_R_R2L_GENERAL, ""},
		  {SaberMovesGeneral.LS_R_TR2BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_R_T2B_GENERAL, ""},

		  //transitions
		  {SaberMovesGeneral.LS_T1_BR__R_GENERAL, ""},//40
		  {SaberMovesGeneral.LS_T1_BR_TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BR_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BR_TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BR__L_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BR_BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__R_BR_GENERAL, ""},//46
		  {SaberMovesGeneral.LS_T1__R_TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__R_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__R_TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__R__L_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__R_BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TR_BR_GENERAL, ""},//52
		  {SaberMovesGeneral.LS_T1_TR__R_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TR_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TR_TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TR__L_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TR_BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_T__BR_GENERAL, ""},//58
		  {SaberMovesGeneral.LS_T1_T___R_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_T__TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_T__TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_T___L_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_T__BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TL_BR_GENERAL, ""},//64
		  {SaberMovesGeneral.LS_T1_TL__R_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TL_TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TL_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TL__L_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_TL_BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__L_BR_GENERAL, ""},//70
		  {SaberMovesGeneral.LS_T1__L__R_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__L_TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__L_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__L_TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1__L_BL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BL_BR_GENERAL, ""},//76
		  {SaberMovesGeneral.LS_T1_BL__R_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BL_TR_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BL_T__GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BL_TL_GENERAL, ""},
		  {SaberMovesGeneral.LS_T1_BL__L_GENERAL, ""},

		  //Bounces
		  {SaberMovesGeneral.LS_B1_BR_GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1__R_GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1_TR_GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1_T__GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1_TL_GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1__L_GENERAL, "_BNCE"},
		  {SaberMovesGeneral.LS_B1_BL_GENERAL, "_BNCE"},

		  //Deflected attacks
		  {SaberMovesGeneral.LS_D1_BR_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1__R_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1_TR_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1_T__GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1_TL_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1__L_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1_BL_GENERAL, "_DFL"},
		  {SaberMovesGeneral.LS_D1_B__GENERAL, "_DFL"},

		  //Reflected attacks
		  {SaberMovesGeneral.LS_V1_BR_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1__R_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1_TR_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1_T__GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1_TL_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1__L_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1_BL_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_V1_B__GENERAL, "_RFL"},

		  // Broken parries
		  {SaberMovesGeneral.LS_H1_T__GENERAL, "_PARR"},//
		  {SaberMovesGeneral.LS_H1_TR_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_H1_TL_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_H1_BR_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_H1_B__GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_H1_BL_GENERAL, "_PARR"},

		  // Knockaways
		  {SaberMovesGeneral.LS_K1_T__GENERAL, "_KNCK"},//
		  {SaberMovesGeneral.LS_K1_TR_GENERAL, "_KNCK"},
		  {SaberMovesGeneral.LS_K1_TL_GENERAL, "_KNCK"},
		  {SaberMovesGeneral.LS_K1_BR_GENERAL, "_KNCK"},
		  {SaberMovesGeneral.LS_K1_BL_GENERAL, "_KNCK"},

		  // Parries
		  {SaberMovesGeneral.LS_PARRY_UP_GENERAL, "_PARR"},//
		  {SaberMovesGeneral.LS_PARRY_UR_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_PARRY_UL_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_PARRY_LR_GENERAL, "_PARR"},
		  {SaberMovesGeneral.LS_PARRY_LL_GENERAL, "_PARR"},

		  // Projectile Reflections
		  {SaberMovesGeneral.LS_REFLECT_UP_GENERAL, "_RFL"},//
		  {SaberMovesGeneral.LS_REFLECT_UR_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_REFLECT_UL_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_REFLECT_LR_GENERAL, "_RFL"},
		  {SaberMovesGeneral.LS_REFLECT_LL_GENERAL, "_RFL"},
		};

		private static SaberMovesGeneral[] jk2ToSaberMoveGeneral = new SaberMovesGeneral[] { // First entry is for -1
			SaberMovesGeneral.LS_INVALID_GENERAL,

			// Invalid_GENERAL, or saber not armed
			SaberMovesGeneral.LS_NONE_GENERAL,

			// General movements with saber
			SaberMovesGeneral.LS_READY_GENERAL,
			SaberMovesGeneral.LS_DRAW_GENERAL,
			SaberMovesGeneral.LS_PUTAWAY_GENERAL,

			// Attacks
			SaberMovesGeneral.LS_A_TL2BR_GENERAL,//4
			SaberMovesGeneral.LS_A_L2R_GENERAL,
			SaberMovesGeneral.LS_A_BL2TR_GENERAL,
			SaberMovesGeneral.LS_A_BR2TL_GENERAL,
			SaberMovesGeneral.LS_A_R2L_GENERAL,
			SaberMovesGeneral.LS_A_TR2BL_GENERAL,
			SaberMovesGeneral.LS_A_T2B_GENERAL,
			SaberMovesGeneral.LS_A_BACKSTAB_GENERAL,
			SaberMovesGeneral.LS_A_BACK_GENERAL,
			SaberMovesGeneral.LS_A_BACK_CR_GENERAL,
			SaberMovesGeneral.LS_A_LUNGE_GENERAL,
			SaberMovesGeneral.LS_A_JUMP_T__B__GENERAL,
			SaberMovesGeneral.LS_A_FLIP_STAB_GENERAL,
			SaberMovesGeneral.LS_A_FLIP_SLASH_GENERAL,

			//starts
			SaberMovesGeneral.LS_S_TL2BR_GENERAL,//26
			SaberMovesGeneral.LS_S_L2R_GENERAL,
			SaberMovesGeneral.LS_S_BL2TR_GENERAL,//# Start of attack chaining to SLASH LR2UL
			SaberMovesGeneral.LS_S_BR2TL_GENERAL,//# Start of attack chaining to SLASH LR2UL
			SaberMovesGeneral.LS_S_R2L_GENERAL,
			SaberMovesGeneral.LS_S_TR2BL_GENERAL,
			SaberMovesGeneral.LS_S_T2B_GENERAL,

			//returns
			SaberMovesGeneral.LS_R_TL2BR_GENERAL,//33
			SaberMovesGeneral.LS_R_L2R_GENERAL,
			SaberMovesGeneral.LS_R_BL2TR_GENERAL,
			SaberMovesGeneral.LS_R_BR2TL_GENERAL,
			SaberMovesGeneral.LS_R_R2L_GENERAL,
			SaberMovesGeneral.LS_R_TR2BL_GENERAL,
			SaberMovesGeneral.LS_R_T2B_GENERAL,

			//transitions
			SaberMovesGeneral.LS_T1_BR__R_GENERAL,//40
			SaberMovesGeneral.LS_T1_BR_TR_GENERAL,
			SaberMovesGeneral.LS_T1_BR_T__GENERAL,
			SaberMovesGeneral.LS_T1_BR_TL_GENERAL,
			SaberMovesGeneral.LS_T1_BR__L_GENERAL,
			SaberMovesGeneral.LS_T1_BR_BL_GENERAL,
			SaberMovesGeneral.LS_T1__R_BR_GENERAL,//46
			SaberMovesGeneral.LS_T1__R_TR_GENERAL,
			SaberMovesGeneral.LS_T1__R_T__GENERAL,
			SaberMovesGeneral.LS_T1__R_TL_GENERAL,
			SaberMovesGeneral.LS_T1__R__L_GENERAL,
			SaberMovesGeneral.LS_T1__R_BL_GENERAL,
			SaberMovesGeneral.LS_T1_TR_BR_GENERAL,//52
			SaberMovesGeneral.LS_T1_TR__R_GENERAL,
			SaberMovesGeneral.LS_T1_TR_T__GENERAL,
			SaberMovesGeneral.LS_T1_TR_TL_GENERAL,
			SaberMovesGeneral.LS_T1_TR__L_GENERAL,
			SaberMovesGeneral.LS_T1_TR_BL_GENERAL,
			SaberMovesGeneral.LS_T1_T__BR_GENERAL,//58
			SaberMovesGeneral.LS_T1_T___R_GENERAL,
			SaberMovesGeneral.LS_T1_T__TR_GENERAL,
			SaberMovesGeneral.LS_T1_T__TL_GENERAL,
			SaberMovesGeneral.LS_T1_T___L_GENERAL,
			SaberMovesGeneral.LS_T1_T__BL_GENERAL,
			SaberMovesGeneral.LS_T1_TL_BR_GENERAL,//64
			SaberMovesGeneral.LS_T1_TL__R_GENERAL,
			SaberMovesGeneral.LS_T1_TL_TR_GENERAL,
			SaberMovesGeneral.LS_T1_TL_T__GENERAL,
			SaberMovesGeneral.LS_T1_TL__L_GENERAL,
			SaberMovesGeneral.LS_T1_TL_BL_GENERAL,
			SaberMovesGeneral.LS_T1__L_BR_GENERAL,//70
			SaberMovesGeneral.LS_T1__L__R_GENERAL,
			SaberMovesGeneral.LS_T1__L_TR_GENERAL,
			SaberMovesGeneral.LS_T1__L_T__GENERAL,
			SaberMovesGeneral.LS_T1__L_TL_GENERAL,
			SaberMovesGeneral.LS_T1__L_BL_GENERAL,
			SaberMovesGeneral.LS_T1_BL_BR_GENERAL,//76
			SaberMovesGeneral.LS_T1_BL__R_GENERAL,
			SaberMovesGeneral.LS_T1_BL_TR_GENERAL,
			SaberMovesGeneral.LS_T1_BL_T__GENERAL,
			SaberMovesGeneral.LS_T1_BL_TL_GENERAL,
			SaberMovesGeneral.LS_T1_BL__L_GENERAL,

			//Bounces
			SaberMovesGeneral.LS_B1_BR_GENERAL,
			SaberMovesGeneral.LS_B1__R_GENERAL,
			SaberMovesGeneral.LS_B1_TR_GENERAL,
			SaberMovesGeneral.LS_B1_T__GENERAL,
			SaberMovesGeneral.LS_B1_TL_GENERAL,
			SaberMovesGeneral.LS_B1__L_GENERAL,
			SaberMovesGeneral.LS_B1_BL_GENERAL,

			//Deflected attacks
			SaberMovesGeneral.LS_D1_BR_GENERAL,
			SaberMovesGeneral.LS_D1__R_GENERAL,
			SaberMovesGeneral.LS_D1_TR_GENERAL,
			SaberMovesGeneral.LS_D1_T__GENERAL,
			SaberMovesGeneral.LS_D1_TL_GENERAL,
			SaberMovesGeneral.LS_D1__L_GENERAL,
			SaberMovesGeneral.LS_D1_BL_GENERAL,
			SaberMovesGeneral.LS_D1_B__GENERAL,

			//Reflected attacks
			SaberMovesGeneral.LS_V1_BR_GENERAL,
			SaberMovesGeneral.LS_V1__R_GENERAL,
			SaberMovesGeneral.LS_V1_TR_GENERAL,
			SaberMovesGeneral.LS_V1_T__GENERAL,
			SaberMovesGeneral.LS_V1_TL_GENERAL,
			SaberMovesGeneral.LS_V1__L_GENERAL,
			SaberMovesGeneral.LS_V1_BL_GENERAL,
			SaberMovesGeneral.LS_V1_B__GENERAL,

			// Broken parries
			SaberMovesGeneral.LS_H1_T__GENERAL,//
			SaberMovesGeneral.LS_H1_TR_GENERAL,
			SaberMovesGeneral.LS_H1_TL_GENERAL,
			SaberMovesGeneral.LS_H1_BR_GENERAL,
			SaberMovesGeneral.LS_H1_B__GENERAL,
			SaberMovesGeneral.LS_H1_BL_GENERAL,

			// Knockaways
			SaberMovesGeneral.LS_K1_T__GENERAL,//
			SaberMovesGeneral.LS_K1_TR_GENERAL,
			SaberMovesGeneral.LS_K1_TL_GENERAL,
			SaberMovesGeneral.LS_K1_BR_GENERAL,
			SaberMovesGeneral.LS_K1_BL_GENERAL,

			// Parries
			SaberMovesGeneral.LS_PARRY_UP_GENERAL,//
			SaberMovesGeneral.LS_PARRY_UR_GENERAL,
			SaberMovesGeneral.LS_PARRY_UL_GENERAL,
			SaberMovesGeneral.LS_PARRY_LR_GENERAL,
			SaberMovesGeneral.LS_PARRY_LL_GENERAL,

			// Projectile Reflections
			SaberMovesGeneral.LS_REFLECT_UP_GENERAL,//
			SaberMovesGeneral.LS_REFLECT_UR_GENERAL,
			SaberMovesGeneral.LS_REFLECT_UL_GENERAL,
			SaberMovesGeneral.LS_REFLECT_LR_GENERAL,
			SaberMovesGeneral.LS_REFLECT_LL_GENERAL,

			SaberMovesGeneral.LS_MOVE_MAX_GENERAL//
		};


	static MeansOfDeathGeneral[] mbIIModToGeneralMap1_9_3_1 = new MeansOfDeathGeneral[]{
		MeansOfDeathGeneral.MOD_UNKNOWN_GENERAL,
		MeansOfDeathGeneral.MOD_WATER_GENERAL,
		MeansOfDeathGeneral.MOD_SLIME_GENERAL,
		MeansOfDeathGeneral.MOD_LAVA_GENERAL,
		MeansOfDeathGeneral.MOD_CRUSH_GENERAL,
		MeansOfDeathGeneral.MOD_TELEFRAG_GENERAL,
		MeansOfDeathGeneral.MOD_FALLING_GENERAL,
		MeansOfDeathGeneral.MOD_SUICIDE_GENERAL,
		MeansOfDeathGeneral.MOD_CHANGEDCLASSES_GENERAL,
		MeansOfDeathGeneral.MOD_WENTSPECTATOR_GENERAL,
		MeansOfDeathGeneral.MOD_CHANGEDTEAMS_GENERAL,
		MeansOfDeathGeneral.MOD_TOOMANYTKS_GENERAL,
		MeansOfDeathGeneral.MOD_EWEBEXPLOSION_GENERAL,
		MeansOfDeathGeneral.MOD_SPACE_GENERAL,
		MeansOfDeathGeneral.MOD_EXPLOSIVE_GENERAL,
		MeansOfDeathGeneral.MOD_ENV_LIGHTNING_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_ELECTRICAL_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_FLAME_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_POISON_GENERAL,
		MeansOfDeathGeneral.MOD_WATERELECTRICAL_GENERAL,
		MeansOfDeathGeneral.MOD_TEAM_CHANGE_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_LIGHTNING_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_GRIP_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_DESTRUCTION_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_DEADLY_SIGHT_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_OLD_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_OLD_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_BLASTER_GENERAL,
		MeansOfDeathGeneral.MOD_SHOTGUN_GENERAL,
		MeansOfDeathGeneral.MOD_BOWCASTER_GENERAL,
		MeansOfDeathGeneral.MOD_BOWCASTER_CHARGED_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_GENERAL,
		MeansOfDeathGeneral.MOD_CONC_GENERAL,
		MeansOfDeathGeneral.MOD_CONC_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_VEHICLE_GENERAL,
		MeansOfDeathGeneral.MOD_TURBLAST_GENERAL,
		MeansOfDeathGeneral.MOD_SENTRY_GENERAL,
		MeansOfDeathGeneral.MOD_SEEKERDRONE_GENERAL,
		MeansOfDeathGeneral.MOD_LASER_GENERAL,
		MeansOfDeathGeneral.MOD_FLAMETHROWER_GENERAL,
		MeansOfDeathGeneral.MOD_ICETHROWER_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ION_BLAST_GENERAL,
		MeansOfDeathGeneral.MOD_SBD_CANNON_GENERAL,
		MeansOfDeathGeneral.MOD_REALCONC_GENERAL,
		MeansOfDeathGeneral.MOD_REALCONC_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_REALCONC_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SNIPER_GENERAL,
		MeansOfDeathGeneral.MOD_TARGET_LASER_GENERAL,
		MeansOfDeathGeneral.MOD_STUN_BATON_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_T21_GENERAL,
		MeansOfDeathGeneral.MOD_T21ALT_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_MICRO_THERMAL_GENERAL,
		MeansOfDeathGeneral.MOD_REAL_THERMAL_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_PULSENADE_GENERAL,
		MeansOfDeathGeneral.MOD_LAUNCHED_PULSENADE_GENERAL,
		MeansOfDeathGeneral.MOD_TRIP_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_TIMED_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DEP_PACK_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_CRYOBAN_BLAST_GENERAL,
		MeansOfDeathGeneral.MOD_FIRE_BLAST_GENERAL,
		MeansOfDeathGeneral.MOD_FIRE_BLAST_BURN_GENERAL,
		MeansOfDeathGeneral.MOD_SONIC_BLAST_GENERAL,
		MeansOfDeathGeneral.MOD_TRACKINGDART_GENERAL,
		MeansOfDeathGeneral.MOD_POISONDART_GENERAL,
		MeansOfDeathGeneral.MOD_POISON_GENERAL,
		MeansOfDeathGeneral.MOD_ACID_GENERAL,
		MeansOfDeathGeneral.MOD_MELEE_GENERAL,
		MeansOfDeathGeneral.MOD_MELEE_KICK_GENERAL,
		MeansOfDeathGeneral.MOD_MELEE_KATA_GENERAL,
		MeansOfDeathGeneral.MOD_ASSA_GENERAL,
		MeansOfDeathGeneral.MOD_SABER_GENERAL,
		MeansOfDeathGeneral.MOD_SABER_THROW_GENERAL,
		MeansOfDeathGeneral.MOD_SABER_HPDRAIN_GENERAL,
		MeansOfDeathGeneral.MOD_SHOCKWAVE_GENERAL,
		MeansOfDeathGeneral.MOD_MAX_GENERAL
	};
	static MeansOfDeathGeneral[] jkaModToGeneralMap = new MeansOfDeathGeneral[]{
		MeansOfDeathGeneral.MOD_UNKNOWN_GENERAL,
		MeansOfDeathGeneral.MOD_STUN_BATON_GENERAL,
		MeansOfDeathGeneral.MOD_MELEE_GENERAL,
		MeansOfDeathGeneral.MOD_SABER_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_BLASTER_GENERAL,
		MeansOfDeathGeneral.MOD_TURBLAST_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SNIPER_GENERAL,
		MeansOfDeathGeneral.MOD_BOWCASTER_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_TRIP_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_TIMED_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DET_PACK_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_VEHICLE_GENERAL,
		MeansOfDeathGeneral.MOD_CONC_GENERAL,
		MeansOfDeathGeneral.MOD_CONC_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_DARK_GENERAL,
		MeansOfDeathGeneral.MOD_SENTRY_GENERAL,
		MeansOfDeathGeneral.MOD_WATER_GENERAL,
		MeansOfDeathGeneral.MOD_SLIME_GENERAL,
		MeansOfDeathGeneral.MOD_LAVA_GENERAL,
		MeansOfDeathGeneral.MOD_CRUSH_GENERAL,
		MeansOfDeathGeneral.MOD_TELEFRAG_GENERAL,
		MeansOfDeathGeneral.MOD_FALLING_GENERAL,
		//MOD_COLLISION_GENERAL,		// OpenJK removed this in commit b319c52fd4ed1e4fb5dae4e468a1a791091b63a5
		//MOD_VEH_EXPLOSION_GENERAL,	// Sad.
		MeansOfDeathGeneral.MOD_SUICIDE_GENERAL,
		MeansOfDeathGeneral.MOD_TARGET_LASER_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_GENERAL,
		MeansOfDeathGeneral.MOD_TEAM_CHANGE_GENERAL,
		//AURELIO: when/if you put this back in_GENERAL, remember to make a case for it in all the other places where
		//mod's are checked. Also_GENERAL, it probably isn't the most elegant solution for what you want - just add
		//a frag back to the player after you call the player_die (and keep a local of his pre-death score to
		//make sure he actually lost points_GENERAL, there may be cases where you don't lose points on changing teams
		//or suiciding_GENERAL, and so you would actually be giving him a point) -Rich
		// I put it back in for now_GENERAL, if it becomes a problem we'll work around it later (it shouldn't though)...
		MeansOfDeathGeneral.MOD_MAX_GENERAL
	};

	static MeansOfDeathGeneral[] jk2ModToGeneralMap = new MeansOfDeathGeneral[]{

		MeansOfDeathGeneral.MOD_UNKNOWN_GENERAL,
		MeansOfDeathGeneral.MOD_STUN_BATON_GENERAL,
		MeansOfDeathGeneral.MOD_MELEE_GENERAL,
		MeansOfDeathGeneral.MOD_SABER_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_GENERAL,
		MeansOfDeathGeneral.MOD_BRYAR_PISTOL_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_BLASTER_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DISRUPTOR_SNIPER_GENERAL,
		MeansOfDeathGeneral.MOD_BOWCASTER_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_REPEATER_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_GENERAL,
		MeansOfDeathGeneral.MOD_DEMP2_ALT_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_GENERAL,
		MeansOfDeathGeneral.MOD_FLECHETTE_ALT_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_GENERAL,
		MeansOfDeathGeneral.MOD_ROCKET_HOMING_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_GENERAL,
		MeansOfDeathGeneral.MOD_THERMAL_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_TRIP_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_TIMED_MINE_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_DET_PACK_SPLASH_GENERAL,
		MeansOfDeathGeneral.MOD_FORCE_DARK_GENERAL,
		MeansOfDeathGeneral.MOD_SENTRY_GENERAL,
		MeansOfDeathGeneral.MOD_WATER_GENERAL,
		MeansOfDeathGeneral.MOD_SLIME_GENERAL,
		MeansOfDeathGeneral.MOD_LAVA_GENERAL,
		MeansOfDeathGeneral.MOD_CRUSH_GENERAL,
		MeansOfDeathGeneral.MOD_TELEFRAG_GENERAL,
		MeansOfDeathGeneral.MOD_FALLING_GENERAL,
		MeansOfDeathGeneral.MOD_SUICIDE_GENERAL,
		MeansOfDeathGeneral.MOD_TARGET_LASER_GENERAL,
		MeansOfDeathGeneral.MOD_TRIGGER_HURT_GENERAL,
		MeansOfDeathGeneral.MOD_MAX_GENERAL
	};


	private static SaberMovesGeneral[] jkaToSaberMoveGeneral = new SaberMovesGeneral[] { // First entry is for -1
			//totally invalid
			SaberMovesGeneral.LS_INVALID_GENERAL,
			// Invalid, or saber not armed
			SaberMovesGeneral.LS_NONE_GENERAL,

			// General movements with saber
			SaberMovesGeneral.LS_READY_GENERAL,
			SaberMovesGeneral.LS_DRAW_GENERAL,
			SaberMovesGeneral.LS_PUTAWAY_GENERAL,

			// Attacks
			SaberMovesGeneral.LS_A_TL2BR_GENERAL,//4
			SaberMovesGeneral.LS_A_L2R_GENERAL,
			SaberMovesGeneral.LS_A_BL2TR_GENERAL,
			SaberMovesGeneral.LS_A_BR2TL_GENERAL,
			SaberMovesGeneral.LS_A_R2L_GENERAL,
			SaberMovesGeneral.LS_A_TR2BL_GENERAL,
			SaberMovesGeneral.LS_A_T2B_GENERAL,
			SaberMovesGeneral.LS_A_BACKSTAB_GENERAL,
			SaberMovesGeneral.LS_A_BACK_GENERAL,
			SaberMovesGeneral.LS_A_BACK_CR_GENERAL,
			SaberMovesGeneral.LS_ROLL_STAB_GENERAL,
			SaberMovesGeneral.LS_A_LUNGE_GENERAL,
			SaberMovesGeneral.LS_A_JUMP_T__B__GENERAL,
			SaberMovesGeneral.LS_A_FLIP_STAB_GENERAL,
			SaberMovesGeneral.LS_A_FLIP_SLASH_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_DUAL_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_ARIAL_LEFT_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_ARIAL_RIGHT_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_CART_LEFT_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_CART_RIGHT_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_STAFF_LEFT_GENERAL,
			SaberMovesGeneral.LS_JUMPATTACK_STAFF_RIGHT_GENERAL,
			SaberMovesGeneral.LS_BUTTERFLY_LEFT_GENERAL,
			SaberMovesGeneral.LS_BUTTERFLY_RIGHT_GENERAL,
			SaberMovesGeneral.LS_A_BACKFLIP_ATK_GENERAL,
			SaberMovesGeneral.LS_SPINATTACK_DUAL_GENERAL,
			SaberMovesGeneral.LS_SPINATTACK_GENERAL,
			SaberMovesGeneral.LS_LEAP_ATTACK_GENERAL,
			SaberMovesGeneral.LS_SWOOP_ATTACK_RIGHT_GENERAL,
			SaberMovesGeneral.LS_SWOOP_ATTACK_LEFT_GENERAL,
			SaberMovesGeneral.LS_TAUNTAUN_ATTACK_RIGHT_GENERAL,
			SaberMovesGeneral.LS_TAUNTAUN_ATTACK_LEFT_GENERAL,
			SaberMovesGeneral.LS_KICK_F_GENERAL,
			SaberMovesGeneral.LS_KICK_B_GENERAL,
			SaberMovesGeneral.LS_KICK_R_GENERAL,
			SaberMovesGeneral.LS_KICK_L_GENERAL,
			SaberMovesGeneral.LS_KICK_S_GENERAL,
			SaberMovesGeneral.LS_KICK_BF_GENERAL,
			SaberMovesGeneral.LS_KICK_RL_GENERAL,
			SaberMovesGeneral.LS_KICK_F_AIR_GENERAL,
			SaberMovesGeneral.LS_KICK_B_AIR_GENERAL,
			SaberMovesGeneral.LS_KICK_R_AIR_GENERAL,
			SaberMovesGeneral.LS_KICK_L_AIR_GENERAL,
			SaberMovesGeneral.LS_STABDOWN_GENERAL,
			SaberMovesGeneral.LS_STABDOWN_STAFF_GENERAL,
			SaberMovesGeneral.LS_STABDOWN_DUAL_GENERAL,
			SaberMovesGeneral.LS_DUAL_SPIN_PROTECT_GENERAL,
			SaberMovesGeneral.LS_STAFF_SOULCAL_GENERAL,
			SaberMovesGeneral.LS_A1_SPECIAL_GENERAL,
			SaberMovesGeneral.LS_A2_SPECIAL_GENERAL,
			SaberMovesGeneral.LS_A3_SPECIAL_GENERAL,
			SaberMovesGeneral.LS_UPSIDE_DOWN_ATTACK_GENERAL,
			SaberMovesGeneral.LS_PULL_ATTACK_STAB_GENERAL,
			SaberMovesGeneral.LS_PULL_ATTACK_SWING_GENERAL,
			SaberMovesGeneral.LS_SPINATTACK_ALORA_GENERAL,
			SaberMovesGeneral.LS_DUAL_FB_GENERAL,
			SaberMovesGeneral.LS_DUAL_LR_GENERAL,
			SaberMovesGeneral.LS_HILT_BASH_GENERAL,

			//starts
			SaberMovesGeneral.LS_S_TL2BR_GENERAL,//26
			SaberMovesGeneral.LS_S_L2R_GENERAL,
			SaberMovesGeneral.LS_S_BL2TR_GENERAL,//# Start of attack chaining to SLASH LR2UL
			SaberMovesGeneral.LS_S_BR2TL_GENERAL,//# Start of attack chaining to SLASH LR2UL
			SaberMovesGeneral.LS_S_R2L_GENERAL,
			SaberMovesGeneral.LS_S_TR2BL_GENERAL,
			SaberMovesGeneral.LS_S_T2B_GENERAL,

			//returns
			SaberMovesGeneral.LS_R_TL2BR_GENERAL,//33
			SaberMovesGeneral.LS_R_L2R_GENERAL,
			SaberMovesGeneral.LS_R_BL2TR_GENERAL,
			SaberMovesGeneral.LS_R_BR2TL_GENERAL,
			SaberMovesGeneral.LS_R_R2L_GENERAL,
			SaberMovesGeneral.LS_R_TR2BL_GENERAL,
			SaberMovesGeneral.LS_R_T2B_GENERAL,

			//transitions
			SaberMovesGeneral.LS_T1_BR__R_GENERAL,//40
			SaberMovesGeneral.LS_T1_BR_TR_GENERAL,
			SaberMovesGeneral.LS_T1_BR_T__GENERAL,
			SaberMovesGeneral.LS_T1_BR_TL_GENERAL,
			SaberMovesGeneral.LS_T1_BR__L_GENERAL,
			SaberMovesGeneral.LS_T1_BR_BL_GENERAL,
			SaberMovesGeneral.LS_T1__R_BR_GENERAL,//46
			SaberMovesGeneral.LS_T1__R_TR_GENERAL,
			SaberMovesGeneral.LS_T1__R_T__GENERAL,
			SaberMovesGeneral.LS_T1__R_TL_GENERAL,
			SaberMovesGeneral.LS_T1__R__L_GENERAL,
			SaberMovesGeneral.LS_T1__R_BL_GENERAL,
			SaberMovesGeneral.LS_T1_TR_BR_GENERAL,//52
			SaberMovesGeneral.LS_T1_TR__R_GENERAL,
			SaberMovesGeneral.LS_T1_TR_T__GENERAL,
			SaberMovesGeneral.LS_T1_TR_TL_GENERAL,
			SaberMovesGeneral.LS_T1_TR__L_GENERAL,
			SaberMovesGeneral.LS_T1_TR_BL_GENERAL,
			SaberMovesGeneral.LS_T1_T__BR_GENERAL,//58
			SaberMovesGeneral.LS_T1_T___R_GENERAL,
			SaberMovesGeneral.LS_T1_T__TR_GENERAL,
			SaberMovesGeneral.LS_T1_T__TL_GENERAL,
			SaberMovesGeneral.LS_T1_T___L_GENERAL,
			SaberMovesGeneral.LS_T1_T__BL_GENERAL,
			SaberMovesGeneral.LS_T1_TL_BR_GENERAL,//64
			SaberMovesGeneral.LS_T1_TL__R_GENERAL,
			SaberMovesGeneral.LS_T1_TL_TR_GENERAL,
			SaberMovesGeneral.LS_T1_TL_T__GENERAL,
			SaberMovesGeneral.LS_T1_TL__L_GENERAL,
			SaberMovesGeneral.LS_T1_TL_BL_GENERAL,
			SaberMovesGeneral.LS_T1__L_BR_GENERAL,//70
			SaberMovesGeneral.LS_T1__L__R_GENERAL,
			SaberMovesGeneral.LS_T1__L_TR_GENERAL,
			SaberMovesGeneral.LS_T1__L_T__GENERAL,
			SaberMovesGeneral.LS_T1__L_TL_GENERAL,
			SaberMovesGeneral.LS_T1__L_BL_GENERAL,
			SaberMovesGeneral.LS_T1_BL_BR_GENERAL,//76
			SaberMovesGeneral.LS_T1_BL__R_GENERAL,
			SaberMovesGeneral.LS_T1_BL_TR_GENERAL,
			SaberMovesGeneral.LS_T1_BL_T__GENERAL,
			SaberMovesGeneral.LS_T1_BL_TL_GENERAL,
			SaberMovesGeneral.LS_T1_BL__L_GENERAL,

			//Bounces
			SaberMovesGeneral.LS_B1_BR_GENERAL,
			SaberMovesGeneral.LS_B1__R_GENERAL,
			SaberMovesGeneral.LS_B1_TR_GENERAL,
			SaberMovesGeneral.LS_B1_T__GENERAL,
			SaberMovesGeneral.LS_B1_TL_GENERAL,
			SaberMovesGeneral.LS_B1__L_GENERAL,
			SaberMovesGeneral.LS_B1_BL_GENERAL,

			//Deflected attacks
			SaberMovesGeneral.LS_D1_BR_GENERAL,
			SaberMovesGeneral.LS_D1__R_GENERAL,
			SaberMovesGeneral.LS_D1_TR_GENERAL,
			SaberMovesGeneral.LS_D1_T__GENERAL,
			SaberMovesGeneral.LS_D1_TL_GENERAL,
			SaberMovesGeneral.LS_D1__L_GENERAL,
			SaberMovesGeneral.LS_D1_BL_GENERAL,
			SaberMovesGeneral.LS_D1_B__GENERAL,

			//Reflected attacks
			SaberMovesGeneral.LS_V1_BR_GENERAL,
			SaberMovesGeneral.LS_V1__R_GENERAL,
			SaberMovesGeneral.LS_V1_TR_GENERAL,
			SaberMovesGeneral.LS_V1_T__GENERAL,
			SaberMovesGeneral.LS_V1_TL_GENERAL,
			SaberMovesGeneral.LS_V1__L_GENERAL,
			SaberMovesGeneral.LS_V1_BL_GENERAL,
			SaberMovesGeneral.LS_V1_B__GENERAL,

			// Broken parries
			SaberMovesGeneral.LS_H1_T__GENERAL,//
			SaberMovesGeneral.LS_H1_TR_GENERAL,
			SaberMovesGeneral.LS_H1_TL_GENERAL,
			SaberMovesGeneral.LS_H1_BR_GENERAL,
			SaberMovesGeneral.LS_H1_B__GENERAL,
			SaberMovesGeneral.LS_H1_BL_GENERAL,

			// Knockaways
			SaberMovesGeneral.LS_K1_T__GENERAL,//
			SaberMovesGeneral.LS_K1_TR_GENERAL,
			SaberMovesGeneral.LS_K1_TL_GENERAL,
			SaberMovesGeneral.LS_K1_BR_GENERAL,
			SaberMovesGeneral.LS_K1_BL_GENERAL,

			// Parries
			SaberMovesGeneral.LS_PARRY_UP_GENERAL,//
			SaberMovesGeneral.LS_PARRY_UR_GENERAL,
			SaberMovesGeneral.LS_PARRY_UL_GENERAL,
			SaberMovesGeneral.LS_PARRY_LR_GENERAL,
			SaberMovesGeneral.LS_PARRY_LL_GENERAL,

			// Projectile Reflections
			SaberMovesGeneral.LS_REFLECT_UP_GENERAL,//
			SaberMovesGeneral.LS_REFLECT_UR_GENERAL,
			SaberMovesGeneral.LS_REFLECT_UL_GENERAL,
			SaberMovesGeneral.LS_REFLECT_LR_GENERAL,
			SaberMovesGeneral.LS_REFLECT_LL_GENERAL,

			SaberMovesGeneral.LS_MOVE_MAX_GENERAL//
		};
	}
}
