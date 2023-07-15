using JKClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{

    // Two dimensional array that can be accessed in any order of indizi and return the same rsuult
    // Aka: [a][b] gives same result as [b][a]
    // How? It just orders the indizi. Biggest first.
    public class ArbitraryOrder2DArray<T>
    {

        T[][] theArray;

        public T this[int a, int b] {
            get {
                return a > b ? theArray[a][b] : theArray[b][a];
            }
            set
            {
                if (a > b)
                {
                    theArray[a][b] = value;
                } else
                {
                    theArray[b][a] = value;
                }
            }
        }

        public ArbitraryOrder2DArray(int maxCount)
        {
            theArray = new T[maxCount][];
            for(int i=0; i < maxCount; i++)
            {
                theArray[i] = new T[i+1]; // We can save a bit of space here. Since the first index is always the biggest, the second array can't contain any index bigger than the first index
            }
        }
    }

    public struct ChatCommandTrackingStuff
    {
        public float maxDbsSpeed;
        public int kickDeaths;
        public int falls;
        public int totalKills;
        public int totalDeaths;
        public bool fightBotIgnore;
        public bool fightBotStrongIgnore;
        public bool fightBotBlacklist;
    }

    // TODO MAke it easier to reset these between games or when maps change. Probably just make new new STatements?
    public class PlayerInfo
    {
        #region position
        public Vector3 position;
        public DateTime? lastPositionUpdate; // Last time the player position alone was updated (from events or such)
        public DateTime? lastPositionOrAngleChange; // Last time the player position or angle changed
        public Vector3 angles;
        //public Vector3 delta_angles;
        public Vector3 velocity;
        public bool IsAlive;
        public DateTime? lastFullPositionUpdate; // Last time all the info above was updated from entities
        public float speed;
        public int groundEntityNum;
        public int torsoAnim;
        public int legsAnim;
        public bool duelInProgress;
        public int saberMove;
        public int forcePowersActive;
        public DateTime? lastDrainedEvent;
        //public int legsTimer;
        #endregion

        public Vector3 lastDeathPosition;
        public DateTime? lastDeath;

        // For killtrackers/memes and such
        public ChatCommandTrackingStuff chatCommandTrackingStuff = new ChatCommandTrackingStuff();
        public void ResetChatCommandTrackingStuff()
        {
            chatCommandTrackingStuff = new ChatCommandTrackingStuff();
        }

        #region score
        public PlayerScore score { get; set; } = new PlayerScore();
        public DateTime? lastScoreUpdated;
        #endregion

        #region nwh
        public int nwhSpectatedPlayer { get; set; }
        public DateTime? nwhSpectatedPlayerLastUpdate;
        #endregion

        #region clientinfo
        public string name { get; set; }
        public Team team { get; set; }
        public bool infoValid { get; set; }
        public bool confirmedBot { get; set; }
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
    public class PlayerScore
    {
        public volatile int client;
        public int score { get; set; }
        public int ping { get; set; }
        public volatile int time;
        public volatile int scoreFlags;
        public volatile int powerUps;
        public volatile int accuracy;
        public int impressiveCount { get; set; } // rets?
        public volatile int excellentCount;
        public volatile int guantletCount;
        public int defendCount { get; set; } // bc?
        public volatile int assistCount;
        public int captures { get; set; } // captures

        public volatile bool perfect;
        public volatile int team;
        public DateTime? lastNonZeroPing;
        public volatile int pingUpdatesSinceLastNonZeroPing;

        public volatile int deaths; // times he got killed. Some JKA mods send this.
        public volatile bool deathsIsFilled; // Indicate if killed value was sent

        // Special values only MB II uses.
        public volatile int mbIIrounds; // shows as "R" on scoreboard 
        public volatile int remainingLives; 
        public volatile int kills; 
        public volatile int mbIImysteryValue; 
        public volatile bool shortScoresMBII; // Indicate if only 9 score info things were sent.

    }




    public struct TeamInfo
    {

        public volatile int teamScore;

        public volatile FlagStatus flag;
        public DateTime? lastFlagUpdate;
        public DateTime? lastTimeFlagWasSeenAtBase;

        // The following infos are all related to the flag of the team this struct is for
        public volatile int flagItemNumber;

        public volatile int lastFlagCarrier;
        public volatile bool lastFlagCarrierValid; // We set this to false if the flag is dropped or goes back to base. Or we might assume the wrong carrier when the flag is taken again if the proper carrier hasn't been set yet.
        public DateTime? lastFlagCarrierValidUpdate;
        public DateTime? lastFlagCarrierFragged;
        public DateTime? lastFlagCarrierWorldDeath;
        public DateTime? lastFlagCarrierUpdate;

        // Positions of flag bases ( as fallback)
        public Vector3 flagBasePosition;
        public volatile int flagBaseEntityNumber;
        public DateTime? lastFlagBasePositionUpdate;

        // Positions of base flag items (the flag item is separate from the flag base)
        public Vector3 flagBaseItemPosition;
        public volatile int flagBaseItemEntityNumber;
        public DateTime? lastFlagBaseItemPositionUpdate;

        // Positions of dropped flags
        public Vector3 flagDroppedPosition;
        public volatile int droppedFlagEntityNumber;
        public DateTime? lastFlagDroppedPositionUpdate;

    }


    public struct KillTracker
    {
        public int kills;
        public DateTime? lastKillTime;
        public bool trackingMatch;
        public int trackedMatchKills;
        public int trackedMatchDeaths;
    }

    // Todo reset stuff on level restart and especially map change
    public class ServerSharedInformationPool : INotifyPropertyChanged
    {

        #region sillyModeStuff
        public SillyMode sillyMode = SillyMode.DBS;
        public GripKickDBSMode gripDbsMode = GripKickDBSMode.VANILLA;
        public float dbsTriggerDistance = 128;
        public float bsTriggerDistance = 64;
        public string sillyModeCustomCommand = null;
        public bool sillyModeOneOf(params SillyMode[] sillyModes)
        {
            if (sillyModes.Contains(sillyMode))
            {
                return true;
            }
            return false;
        }
        public bool gripDbsModeOneOf(params GripKickDBSMode[] gripDbsModes)
        {
            if (gripDbsModes.Contains(gripDbsMode))
            {
                return true;
            }
            return false;
        }
        #endregion

        public bool isIntermission { get; set; } = false;

        private int gameTime = 0;
        public string GameTime { get; private set; }
        public string MapName { get; set; }
        public int ScoreRed { get; set; }
        public int ScoreBlue { get; set; }

        public KillTracker[,] killTrackers;

        public bool NoActivePlayers { get; set; }

        public DateTime? lastBotOnlyConfirmed = null;
        public DateTime? lastScoreboardReceived = null;
        
        public bool botOnlyGuaranteed = false;

        public ConcurrentBag<string> unsupportedCommands = new ConcurrentBag<string>();

        public ArbitraryOrder2DArray<DateTime?> lastConfirmedVisible;
        public ArbitraryOrder2DArray<DateTime?> lastConfirmedInvisible;

        // < 1 = confirmed visible recently
        // 1 = nothing to report
        // > 1 = confirmed invisible recently
        public float getVisibilityMultiplier(int entityNumA, int entityNumB, int validTime=300)
        {
            DateTime lastVisibility = lastConfirmedVisible[entityNumA, entityNumB].GetValueOrDefault(DateTime.Now-new TimeSpan(0,0,0,0, validTime*2));
            DateTime lastInvisibility = lastConfirmedInvisible[entityNumA, entityNumB].GetValueOrDefault(DateTime.Now - new TimeSpan(0, 0, 0, 0, validTime * 2));
            if((DateTime.Now- lastVisibility).TotalMilliseconds > validTime && (DateTime.Now-lastInvisibility).TotalMilliseconds > validTime)
            {
                // Neither of these values is current enough to be relevant.
                return 1f;
            }

            if (lastVisibility > lastInvisibility)
            {
                float timeMultiplier = (float)Math.Clamp( (DateTime.Now - lastVisibility).TotalMilliseconds / (double)validTime,0f,1f);
                return 1f* timeMultiplier+(1f- timeMultiplier)* 0.75f; // Small bonus. The older the confirmed visibility, the lesser the bonus
            } else if (lastVisibility < lastInvisibility)
            {
                float timeMultiplier = (float)Math.Clamp((DateTime.Now - lastInvisibility).TotalMilliseconds / (double)validTime, 0f, 1f);
                return 1f * timeMultiplier + (1f - timeMultiplier) * 4f; // Big penalty. The older the confirmed invisibility, the lesser the penalty
            }
            else
            {
                return 1f;
            }
        }
        public float getVisibilityMultiplier(int? entityNumA, int? entityNumB, int validTime = 300)
        {
            if(!entityNumA.HasValue || !entityNumB.HasValue)
            {
                return 1.0f;
            }

            return getVisibilityMultiplier(entityNumA.Value, entityNumB.Value, validTime);
        }

        public void setGameTime(int gameTime)
        {
            this.gameTime = gameTime;
            int msec = gameTime;
            int secs = msec / 1000;
            int mins = secs / 60;

            secs = secs % 60;
            msec = msec % 1000;

            GameTime = mins.ToString() +":"+ secs.ToString("D2");
        }

        public PlayerInfo[] playerInfo;
        public TeamInfo[] teamInfo = new TeamInfo[Enum.GetNames(typeof(JKClient.Team)).Length];



        public event PropertyChangedEventHandler PropertyChanged;

        public volatile int saberWeaponNum = -1;

        private int _maxClients = 0;
        private bool jkaMode = false;
        public ServerSharedInformationPool(bool jkaModeA, int maxClients)
        {
            _maxClients = maxClients;
            playerInfo = new PlayerInfo[maxClients];
            teamInfo = new TeamInfo[Enum.GetNames(typeof(JKClient.Team)).Length];
            killTrackers = new KillTracker[maxClients, maxClients];

            lastConfirmedVisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);
            lastConfirmedInvisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);

            for (int i = 0; i < playerInfo.Length; i++)
            {
                playerInfo[i] = new PlayerInfo();
            }
            jkaMode = jkaModeA;
            if (jkaMode)
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_REDFLAG,false).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_BLUEFLAG,false).Value;
                //this.saberWeaponNum = JKAStuff.ItemList.bg_itemlist[JKAStuff.ItemList.BG_FindItem("weapon_saber").Value].giTag;
                this.saberWeaponNum = (int)JKAStuff.ItemList.weapon_t.WP_SABER;

            } else
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_REDFLAG).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_BLUEFLAG).Value;
                //this.saberWeaponNum = JOStuff.ItemList.bg_itemlist[ JOStuff.ItemList.BG_FindItem("weapon_saber").Value].giTag;
                this.saberWeaponNum = (int)JOStuff.ItemList.weapon_t.WP_SABER;
            }
        }

        public void ResetFlagItemNumbers(bool isMBII)
        {
            if (jkaMode)
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_REDFLAG, isMBII).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_BLUEFLAG, isMBII).Value;
            } else
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_REDFLAG).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_BLUEFLAG).Value;
            }
        }
        public void ResetInfo(bool isMBII)
        {
            playerInfo = new PlayerInfo[_maxClients];
            teamInfo = new TeamInfo[Enum.GetNames(typeof(JKClient.Team)).Length];
            if (jkaMode)
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_REDFLAG, isMBII).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JKAStuff.ItemList.BG_FindItemForPowerup(JKAStuff.ItemList.powerup_t.PW_BLUEFLAG, isMBII).Value;
                this.saberWeaponNum = (int)JKAStuff.ItemList.weapon_t.WP_SABER;
            } else
            {
                teamInfo[(int)JKClient.Team.Red].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_REDFLAG).Value;
                teamInfo[(int)JKClient.Team.Blue].flagItemNumber = JOStuff.ItemList.BG_FindItemForPowerup(JOStuff.ItemList.powerup_t.PW_BLUEFLAG).Value;
                this.saberWeaponNum = (int)JOStuff.ItemList.weapon_t.WP_SABER;
            }
        }
    }

    public enum FlagStatus : int
    {
        FLAG_ATBASE = 0,
        FLAG_TAKEN,         // CTF
        FLAG_TAKEN_RED,     // One Flag CTF
        FLAG_TAKEN_BLUE,    // One Flag CTF
        FLAG_DROPPED
    }
}
