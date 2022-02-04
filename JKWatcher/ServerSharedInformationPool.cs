using JKClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{

    struct PlayerInfo
    {
        #region position
        public Vector3 position;
        public Vector3 angles;
        public Vector3 velocity;
        public DateTime? lastPositionUpdate;
        #endregion

        public Vector3 lastDeathPosition;
        public DateTime? lastDeath;

        #region score
        public PlayerScore score;
        public DateTime? lastScoreUpdated;
        #endregion

        #region clientinfo
        public string name { get; set; }
        public JKClient.Team team { get; set; }
        public bool infoValid { get; set; }
        public int clientNum { get; set; }
        public DateTime? lastClientInfoUpdate;
        #endregion

        #region tinfo
        public volatile int location;       // location index for team mode
        public volatile int health;         // you only get this info about your teammates
        public volatile int armor;
        public volatile int curWeapon;
        public volatile int powerUps;		// so can display quad/flag status
        #endregion
    }
    struct PlayerScore
    {
        public volatile int client;
        public volatile int score;
        public volatile int ping;
        public volatile int time;
        public volatile int scoreFlags;
        public volatile int powerUps;
        public volatile int accuracy;
        public volatile int impressiveCount; // rets?
        public volatile int excellentCount;
        public volatile int guantletCount;
        public volatile int defendCount; // bc?
        public volatile int assistCount;
        public volatile int captures; // captures
        public volatile bool perfect;
        public volatile int team;
    }

    class ServerSharedInformationPool
    {
        public PlayerInfo[] playerInfo = new PlayerInfo[JKClient.Common.MaxClients(ProtocolVersion.Protocol15)];
        public volatile int[] teamScores = new int[2];
        public volatile FlagStatus blueFlag = (FlagStatus)(-1);
        public volatile FlagStatus redFlag = (FlagStatus)(-1);

        public int redFlagItemNumber { get; private set; } = ItemList.BG_FindItemForPowerup(ItemList.powerup_t.PW_REDFLAG).Value;
        public int blueFlagItemNumber { get; private set; } = ItemList.BG_FindItemForPowerup(ItemList.powerup_t.PW_BLUEFLAG).Value;
        public volatile int lastRedFlagCarrier = -1;
        public DateTime? lastRedFlagCarrierUpdate = null;
        public volatile int lastBlueFlagCarrier = -1;
        public DateTime? lastBlueFlagCarrierUpdate = null;

        // Positions of flag bases ( as fallback)
        public Vector3 redFlagBasePosition;
        public DateTime? lastRedFlagBasePositionUpdate = null;
        public Vector3 blueFlagBasePosition;
        public DateTime? lastBlueFlagBasePositionUpdate = null;

        // Positions of base flag items (the flag item is separate from the flag base)
        public Vector3 redFlagBaseItemPosition;
        public DateTime? lastRedFlagBaseItemPositionUpdate = null;
        public Vector3 blueFlagBaseItemPosition;
        public DateTime? lastBlueFlagBaseItemPositionUpdate = null;

        // Positions of dropped flags
        public Vector3 redFlagDroppedPosition;
        public DateTime? lastRedFlagDroppedPositionUpdate = null;
        public Vector3 blueFlagDroppedPosition;
        public DateTime? lastBlueFlagDroppedPositionUpdate = null;
    }

    enum FlagStatus : int
    {
        FLAG_ATBASE = 0,
        FLAG_TAKEN,         // CTF
        FLAG_TAKEN_RED,     // One Flag CTF
        FLAG_TAKEN_BLUE,    // One Flag CTF
        FLAG_DROPPED
    }
}
