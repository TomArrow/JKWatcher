using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{
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
