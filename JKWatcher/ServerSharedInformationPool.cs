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
        public volatile int powerups;		// so can display quad/flag status
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
        public PlayerInfo[] playerInfo = new PlayerInfo[JKClient.Common.MaxClients];
        public volatile int[] teamScores = new int[2];
    }
}
