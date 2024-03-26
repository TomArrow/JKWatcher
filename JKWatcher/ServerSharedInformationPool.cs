using JKClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static JKWatcher.ConnectedServerWindow;

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


    public static unsafe class EntityStateExtensionMethods {
        public static UInt64 GetKillHash(this EntityState es, ServerSharedInformationPool infoPool)
        {
            StringBuilder sb = new StringBuilder();

            int target = es.OtherEntityNum;
            int attacker = es.OtherEntityNum2;
            MeansOfDeath mod = (MeansOfDeath)es.EventParm;
            Vector3 locationOfDeath;
            locationOfDeath.X = es.Position.Base[0];
            locationOfDeath.Y = es.Position.Base[1];
            locationOfDeath.Z = es.Position.Base[2];
            string playerName = attacker >= 0 && attacker < infoPool.playerInfo.Length ? infoPool.playerInfo[attacker].name : "WEIRDATTACKER";
            string victimname = target >= 0 && target < infoPool.playerInfo.Length ? infoPool.playerInfo[target].name : "WEIRDVICTIM";
            sb.Append(playerName);
            sb.Append("_");
            sb.Append(victimname);
            sb.Append("_");
            sb.Append(attacker);
            sb.Append("_");
            sb.Append(target);
            sb.Append("_");
            sb.Append(mod);
            sb.Append("_");
            sb.Append(locationOfDeath.X);
            sb.Append("_");
            sb.Append(locationOfDeath.Y);
            sb.Append("_");
            sb.Append(locationOfDeath.Z);
            SHA512 sha512 = new SHA512Managed();
            return BitConverter.ToUInt64(sha512.ComputeHash(Encoding.Latin1.GetBytes(sb.ToString())));
        }
    }


    public class ChatCommandTrackingStuff
    {
        public float maxDbsSpeed;
        public int kickDeaths;
        public int falls;
        public int doomkills;
        public int returns;
        public int returned;
        public int totalKills;
        public int totalDeaths;
        public bool fightBotIgnore;
        public bool fightBotStrongIgnore;
        public bool fightBotBlacklist;
        public bool fightBotBlacklistAllowBrave;
        public bool wantsBotFight;
        public bool berserkerVote;
        public DateTime lastBodyguardStart;
        public DateTime onlineSince;
        //public int totalTimeVisible;
        //public int lastKnownServerTime;

        private object trackedKillsLock = new object();
        private HashSet<UInt64> trackedKills = new HashSet<ulong>();
        private Dictionary<string,int> killTypes = new Dictionary<string, int>();
        private Dictionary<string,int> killTypesReturns = new Dictionary<string, int>();
        public void TrackKill(string killType, UInt64 killHash, bool isReturn)
        {
            lock (trackedKillsLock)
            {
                if (trackedKills.Add(killHash))
                {
                    if (!killTypes.ContainsKey(killType))
                    {
                        killTypes[killType] = 1;
                    } else
                    {
                        killTypes[killType]++;
                    }
                    if (isReturn)
                    {
                        if (!killTypesReturns.ContainsKey(killType))
                        {
                            killTypesReturns[killType] = 1;
                        }
                        else
                        {
                            killTypesReturns[killType]++;
                        }
                    }
                }
            }
        }
        public Dictionary<string, int> GetKillTypes()
        {
            lock (trackedKillsLock)
            {
                return killTypes.ToDictionary(blah=>blah.Key,blah=>blah.Value); // Make a copy for thread safety.
            }
        }
        public Dictionary<string, int> GetKillTypesReturns()
        {
            lock (trackedKillsLock)
            {
                return killTypesReturns.ToDictionary(blah=>blah.Key,blah=>blah.Value); // Make a copy for thread safety.
            }
        }
    }

    public struct PlayerIdentification
    {
        public string name { get; init; }
        public string model { get; init; }
        public string color1 { get; init; }
        public string color2 { get; init; }
        public string g_redteam { get; init; }
        public string g_blueteam { get; init; }

        public static bool operator ==(PlayerIdentification u1, PlayerIdentification u2)
        {
            return u1.name == u2.name &&
                u1.model == u2.model &&
                u1.color1 == u2.color1 &&
                u1.color2 == u2.color2// && 
                //u1.g_redteam == u2.g_redteam && // Sadly we can't use these 2 because for some reason they aren't always consistent. Idk why.
                //u1.g_blueteam == u2.g_blueteam
                ;
        }
        public static bool operator !=(PlayerIdentification u1, PlayerIdentification u2)
        {
            return !(u1==u2);
        }

        public static PlayerIdentification FromClientInfo(ClientInfo info)
        {
            return new PlayerIdentification() { 
                name=info.Name,
                model=info.Model,
                color1=info.Color1,
                color2=info.Color2,
                g_redteam=info.GRedTeam,
                g_blueteam=info.GBlueTeam
            };
        }
    }

    public class VisiblePlayersTracker
    {
        object dataLock = new object();
        const int averageTotal = 500;
        //byte[] sampleBuffer = new byte[averageTotal];
        //Int64 sampleBufferIndex = 0;
        double visiblePlayersSum = 0;
        int sampleCount = 0;
        float visiblePlayersAvg = -1;
        public float VisiblePlayersAvg
        {
            get
            {
                return visiblePlayersAvg;
            }
        }
        byte visiblePlayers = 0;
        public byte VisiblePlayers
        {
            get
            {
                return visiblePlayers;
            }
            set
            {
                lock (dataLock) { 
                    visiblePlayers = value;
                    visiblePlayersSum += value;
                    sampleCount++;  
                    if(sampleCount >= averageTotal)
                    { // Store average and reset
                        visiblePlayersAvg = (float)(visiblePlayersSum / (double)sampleCount);
                        sampleCount = 0;
                        visiblePlayersSum = 0;
                    }
                }
            }
        }
        public string VisiblePlayersString
        {
            get
            {
                return $"{visiblePlayers}/{visiblePlayersAvg}";
            }
        }
    }

    // TODO MAke it easier to reset these between games or when maps change. Probably just make new new STatements?
    public class PlayerInfo
    {
        public ServerSharedInformationPool infoPool { get; init; } = null;

        public int ProbableRetCount => (infoPool?.serverSeemsToSupportRetsCountScoreboard).GetValueOrDefault(true) ? this.score.impressiveCount : this.chatCommandTrackingStuff.returns;

        #region position
        public Vector3 position;
        public DateTime? lastPositionUpdate; // Last time the player position alone was updated (from events or such)
        public DateTime lastMovementDirChange = DateTime.Now; // Last time the player position or angle changed
        public Vector3 angles;
        //public Vector3 delta_angles;
        public Vector3 velocity;
        public bool IsAlive { get; set; }
        public bool IsFrozen { get; set; } // MOH Freeze Tag.
        public DateTime? lastAliveStatusUpdated; // Need it for MOHAA :)
        public long consecutiveAfkMillisecondsCounter = 0; // Amount of consecutive milliseconds this player was SEEN and no change in pos.
        public bool confirmedAfk = false; // If consecutiveAfkMillisecondsCounter reaches a certain threshold we confirm the player as AFK
        //public DateTime? timeConfirmedAfk; // How long ago did we confirm afk?
        //public DateTime? lastNotVisible; // Last time we confirmed this player to not be visible on any connecttion
        //public DateTime? lastConfirmedAfk; // Last time we kinda confirmed this player to be afk
        public DateTime? lastFullPositionUpdate; // Last time all the info above was updated from entities
        public float speed;
        public int groundEntityNum;
        public int torsoAnim;
        public bool knockedDown;
        public int legsAnim;
        public bool duelInProgress;
        public int saberMove;
        public int forcePowersActive;
        public int movementDir;
        public DateTime? lastDrainedEvent;
        //public int legsTimer;
        #endregion

        public Vector3 lastDeathPosition;
        public DateTime? lastDeath;

        // For killtrackers/memes and such
        public PlayerIdentification lastValidPlayerData = new PlayerIdentification();
        public DateTime? lastSeenValid = null;
        public ChatCommandTrackingStuff chatCommandTrackingStuff = new ChatCommandTrackingStuff();
        public void ResetChatCommandTrackingStuff()
        {
            chatCommandTrackingStuff = new ChatCommandTrackingStuff();
        }
        public VisiblePlayersTracker VisiblePlayers { get; init; } = new VisiblePlayersTracker(); // For vis check debug?

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
        public bool inactiveMOH { get; set; }
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
        public int time { get; set; }
        public float scorePerMinute { 
            get
            {
                int tmpTime = time;// Due to multithreading. It might change from one line to the next.
                return tmpTime == 0 ? 0 : (float)score / (float)tmpTime;
            }
        }
        public float scorePerDeaths { 
            get
            {
                //int tmpDeaths = deaths;// Due to multithreading. It might change from one line to the next.
                //return tmpDeaths == 0 ? 0 : (float)score / (float)tmpDeaths;
                return (float)score / (float)Math.Max(1, deaths);
            }
        }
        public float kdMod { 
            get
            {
                // Modified K/D with more emphasis on kills. 30/10 would be similar to 10/2 for example.
                // We recognize that players can get lucky at the start of a game, and also that campers might get a better K/D but more boring gameplay.
                // Nice side effect: At equal kill counts, this still behaves linearly when comparing two players, e.g. the player with only half the deaths will have 2x as good of a ratio.
                //return deaths == 0 ? 0 : (float)(Math.Pow((double)kills, 1.5) / Math.Max(1.0, (double)deaths));
                return (float)(Math.Pow((double)kills, 1.5) / (double)Math.Max(1, deaths));
            }
        }
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

        public int deaths { get; set; } // times he got killed. Some JKA mods and some MOH gametypes send this.
        public volatile bool deathsIsFilled; // Indicate if killed value was sent

        // Special values only MB II uses.
        public volatile int mbIIrounds; // shows as "R" on scoreboard 
        public volatile int remainingLives; 
        public volatile int kills; 
        public volatile int totalKills; // MOHAA
        public volatile int mbIImysteryValue; 
        public volatile bool shortScoresMBII; // Indicate if only 9 score info things were sent.

    }


    public class ReliableFlagCarrierTracker { // To track flag carriers across multiple connections :/
        private int flagCarrier = -1;
        private int infoServerTime = -9999;
        private object infoLock = new object();
        public void Reset()
        {
            lock (infoLock)
            {
                flagCarrier = -1;
                infoServerTime = -9999;
            }
        }
        public void setFlagCarrier(int flagCarrierA, int serverTime)
        {
            lock (infoLock)
            {
                if(serverTime > infoServerTime || (serverTime + 10000) < infoServerTime) // Overwrite info if it's newer or (safety check) it's likely that serverTime was reset.
                {
                    flagCarrier = flagCarrierA;
                    infoServerTime = serverTime;
                }
            }
        }
        public int getFlagCarrier()
        {
            lock (infoLock)
            {
                return flagCarrier;
            }
        }
    }


    public class TeamInfo
    {

        public volatile int teamScore;

        public volatile FlagStatus flag;
        public DateTime? lastFlagUpdate;
        public DateTime? lastTimeFlagWasSeenAtBase;

        // The following infos are all related to the flag of the team this struct is for
        public volatile int flagItemNumber;

        public ReliableFlagCarrierTracker reliableFlagCarrierTracker = new ReliableFlagCarrierTracker();

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

        public volatile int teamKills; // MOHAA
        public volatile int teamDeaths; // MOHAA
        public volatile int teamAveragePing; // MOHAA

    }


    public class KillTracker
    {
        public int returns; // Not currently used.
        public int kills;
        public DateTime? lastKillTime;
        public bool trackingMatch;
        public int trackedMatchKills;
        public int trackedMatchDeaths;

        private object trackedKillsLock = new object();
        private HashSet<UInt64> trackedKills = new HashSet<ulong>();
        private Dictionary<string, int> killTypes = new Dictionary<string, int>();
        private Dictionary<string, int> killTypesReturns = new Dictionary<string, int>();
        public void TrackKill(string killType, UInt64 killHash, bool isReturn)
        {
            lock (trackedKillsLock)
            {
                if (trackedKills.Add(killHash))
                {
                    if (!killTypes.ContainsKey(killType))
                    {
                        killTypes[killType] = 1;
                    }
                    else
                    {
                        killTypes[killType]++;
                    }
                    if (isReturn)
                    {
                        if (!killTypesReturns.ContainsKey(killType))
                        {
                            killTypesReturns[killType] = 1;
                        }
                        else
                        {
                            killTypesReturns[killType]++;
                        }
                    }
                }
            }
        }
        public Dictionary<string, int> GetKillTypes()
        {
            lock (trackedKillsLock)
            {
                return killTypes.ToDictionary(blah => blah.Key, blah => blah.Value); // Make a copy for thread safety.
            }
        }
        public Dictionary<string, int> GetKillTypesReturns()
        {
            lock (trackedKillsLock)
            {
                return killTypesReturns.ToDictionary(blah => blah.Key, blah => blah.Value); // Make a copy for thread safety.
            }
        }
    }

    // Todo reset stuff on level restart and especially map change
    public class ServerSharedInformationPool : INotifyPropertyChanged
    {

        #region sillyModeStuff
        public FightBotTargetingMode fightBotTargetingMode = FightBotTargetingMode.NONE;
        public SillyMode sillyMode = SillyMode.DBS;
        public GripKickDBSMode gripDbsMode = GripKickDBSMode.VANILLA;
        public float dbsTriggerDistance = 90; // 128
        public float bsTriggerDistance = 64;
        public bool fastDbs = true; // Assume we are in the air if the last user command had jump in it.
        public bool selfPredict = true; // Predict the bots own position with ping value
        public float deluxePredict = 1.0f;
        public string sillyModeCustomCommand = null;
        public DateTime lastBerserkerStarted = DateTime.Now - new TimeSpan(10, 0, 0);
        public DateTime lastBodyguardStarted = DateTime.Now - new TimeSpan(10, 0, 0);
        public int sillyBodyguardPlayer = -1;
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

        public ConnectionOptions connectionOptions { get; init; }

        public volatile int ServerSlotsTaken = 0;
        public volatile int MaxServerClients = 32;
        public bool isIntermission { get; set; } = false;

        private volatile int gameTime = 0;
        public string GameTime { get; private set; }
        public string MapName { get; set; }
        public int ScoreRed { get; set; }
        public int ScoreBlue { get; set; }
        public List<WayPoint> wayPoints = new List<WayPoint>();

        public KillTracker[,] killTrackers;

        public int getProbableRetCount(int clientNum)
        {
            if(clientNum < 0 || clientNum > _maxClients)
            {
                return 0;
            } else
            {
                return serverSeemsToSupportRetsCountScoreboard ? this.playerInfo[clientNum].score.impressiveCount : this.playerInfo[clientNum].chatCommandTrackingStuff.returns;
            }
        }

        public bool NoActivePlayers { get; set; }
        public bool serverSeemsToSupportRetsCountScoreboard = false;

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

            for (int i = 0; i < teamInfo.Length; i++)
            {
                teamInfo[i] = new TeamInfo();
            }

            killTrackers = new KillTracker[maxClients, maxClients];
            for(int i = 0; i < maxClients; i++)
            {
                for (int a = 0; a < maxClients; a++)
                {
                    killTrackers[i,a] = new KillTracker();
                }
            }

            lastConfirmedVisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);
            lastConfirmedInvisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);

            for (int i = 0; i < playerInfo.Length; i++)
            {
                playerInfo[i] = new PlayerInfo() { infoPool=this};
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
        /*
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
        }*/

        Regex numberRegex = new Regex(@"^\d+$",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);

        public int FindClientNumFromString(string searchString, int teamBitMask = ~(1<<(int)Team.Spectator), bool tryInterpretAsNumbers = false)
        {
            if (searchString == null) return -1;
            searchString = searchString.Trim();
            if (tryInterpretAsNumbers && numberRegex.Match(searchString).Success)
            {
                int match = -1;
                if(int.TryParse(searchString, out match))
                {
                    if(match >= 0 && match < playerInfo.Length)
                    {
                        return match;
                    }
                }
            }

            List<int> clientNumsMatch = new List<int>();
            List<int> clientNumsCaseInsensitiveMatch = new List<int>();
            List<int> clientNumsNoColorMatch = new List<int>();
            List<int> clientNumsNoColorCaseInsensitiveMatch = new List<int>();

            string lowerCaseName = searchString.ToLower();
            string noColorName = Q3ColorFormatter.cleanupString(searchString);
            string noColorLowercaseName = noColorName.ToLower();

            foreach (PlayerInfo pi in playerInfo)
            {
                if (pi.infoValid && ((1 << (int)pi.team) & teamBitMask)>0)
                {
                    string compareLowerCaseName = pi.name.ToLower();
                    string compareNoColorName = Q3ColorFormatter.cleanupString(pi.name);
                    string compareNoColorLowercaseName = compareNoColorName.ToLower();

                    if (pi.name.Contains(searchString))
                    {
                        clientNumsMatch.Add(pi.clientNum);
                    }
                    if (compareLowerCaseName.Contains(lowerCaseName))
                    {
                        clientNumsCaseInsensitiveMatch.Add(pi.clientNum);
                    }
                    if (compareNoColorName.Contains(noColorName))
                    {
                        clientNumsNoColorMatch.Add(pi.clientNum);
                    }
                    if (compareNoColorLowercaseName.Contains(noColorLowercaseName))
                    {
                        clientNumsNoColorCaseInsensitiveMatch.Add(pi.clientNum);
                    }

                    if (clientNumsMatch.Count > 0)
                    {
                        return clientNumsMatch[0];
                    } else if (clientNumsCaseInsensitiveMatch.Count > 0)
                    {
                        return clientNumsCaseInsensitiveMatch[0];
                    } else if (clientNumsNoColorMatch.Count > 0)
                    {
                        return clientNumsNoColorMatch[0];
                    } else if (clientNumsNoColorCaseInsensitiveMatch.Count > 0)
                    {
                        return clientNumsNoColorCaseInsensitiveMatch[0];
                    }
                }
            }
            return -1;

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
