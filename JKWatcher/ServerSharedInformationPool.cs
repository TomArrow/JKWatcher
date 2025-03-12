using JKClient;
using System;
using Glicko2;
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
using JKWatcher.RandomHelpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            for (int i = 0; i < maxCount; i++)
            {
                theArray[i] = new T[i + 1]; // We can save a bit of space here. Since the first index is always the biggest, the second array can't contain any index bigger than the first index
            }
        }
    }


    public class AliveInfo
    {
        public int weapon;
        public int saberMove;
        public int saberHolstered;
        public DateTime when = DateTime.Now;
    }



    public static unsafe class EntityStateExtensionMethods {
        public static UInt64 GetKillHash(this EntityState es, ServerSharedInformationPool infoPool)
        {
            StringBuilder sb = new StringBuilder();

            int target = es.OtherEntityNum;
            int attacker = es.OtherEntityNum2;
            //MeansOfDeath mod = (MeansOfDeath)es.EventParm;
            string mod = $"MOD{es.EventParm}";
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
            using (SHA512 sha512 = new SHA512Managed())
            {
                return BitConverter.ToUInt64(sha512.ComputeHash(Encoding.Latin1.GetBytes(sb.ToString())));
            }
        }
    }


    public class ReliableValueCounter
    {
        public int value { get; private set; } = 0;
        private object ourLock = new object();
        public DateTime lastValueChanged { get; private set; } = DateTime.Now;

        public bool Add(int plus, bool rateLimit = true)
        {
            lock (ourLock)
            {
                if (!rateLimit || (DateTime.Now - lastValueChanged).TotalMilliseconds > 500)
                {
                    value += plus;
                    lastValueChanged = DateTime.Now;
                    return true;
                }
                return false;
            }
        }
    }

    public class BlocksTracker
    {
        public int blocks { get; private set; } = 0;
        public int blockeds { get; private set; } = 0;
        private object ourLock = new object();
        public DateTime lastBlockRegistered { get; private set; } = DateTime.Now;
        public DateTime lastBlockedRegistered { get; private set; } = DateTime.Now;
        public bool CountBlock(bool rateLimit = true)
        {
            lock (ourLock)
            {
                if (!rateLimit || (DateTime.Now - lastBlockRegistered).TotalMilliseconds > 500)
                {
                    blocks++;
                    lastBlockRegistered = DateTime.Now;
                    return true;
                }
                return false;
            }
        }
        public bool CountBlocked(bool rateLimit = true)
        {
            lock (ourLock)
            {
                if (!rateLimit || (DateTime.Now - lastBlockedRegistered).TotalMilliseconds > 500)
                {
                    blockeds++;
                    lastBlockedRegistered = DateTime.Now;
                    return true;
                }
                return false;
            }
        }
    }

    public class RateLimitedTracker
    {
        public int value { get; private set; } = 0;
        private object ourLock = new object();
        public DateTime lastChange { get; private set; } = DateTime.Now;
        private double _rateLimit = 500.0;
        public bool Count(bool rateLimit = true)
        {
            lock (ourLock)
            {
                if (!rateLimit || (DateTime.Now - lastChange).TotalMilliseconds > _rateLimit)
                {
                    value++;
                    lastChange = DateTime.Now;
                    return true;
                }
                return false;
            }
        }
        public RateLimitedTracker(int rateLimit = 500)
        {
            _rateLimit = (double)rateLimit;
        }
    }

    public class AverageHelper
    {
        private double total;
        private double divider = 0.0;
        public void AddValue(double value, double weight = 1.0)
        {
            if(weight <= 0.0)
            {
                throw new InvalidOperationException("Cannot call AverageHelper with a weight of 0");
            }
            total += value;
            divider += weight;
        }
        public double? GetAverage()
        {
            return divider == 0.0 ? null : total / divider;
        }
    }

    public class AveragingDictionary<T> : ConcurrentDictionary<T, AverageHelper>
    {
        public void AddSample(T key, double value)
        {
            this.AddOrUpdate(key, (a) => {
                AverageHelper helper = new AverageHelper();
                helper.AddValue(value);
                return helper;
            }, (a, b) => {
                b.AddValue(value);
                return b;
            });
        }
        public double? GetAverage(T key)
        {
            if (this.TryGetValue(key, out AverageHelper helper))
            {
                return helper.GetAverage();
            }
            return null;
        }
        public AveragingDictionary(IEqualityComparer<T> comparer) : base(comparer){
        }
    }

    public class ChatCommandTrackingStuff
    {
        // defrag
        public int defragRunsFinished;
        public int defragTop10RunCount;
        public int defragWRCount;
        public Int64 defragTotalRunTime;
        public ConcurrentDictionary<string, int> defragMapsRun = new ConcurrentDictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        public ConcurrentDictionary<string, int> defragMapsTop10 = new ConcurrentDictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        public ConcurrentDictionary<string, int> defragMapsWR = new ConcurrentDictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        public ConcurrentDictionary<string, int> defragBestTimes = new ConcurrentDictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        public AveragingDictionary<string> defragAverageMapTimes = new AveragingDictionary<string>(StringComparer.InvariantCultureIgnoreCase);

        public float maxDbsSpeed;
        public int kickDeaths;
        public RateLimitedTracker rolls = new RateLimitedTracker();
        public RateLimitedTracker rollsWithFlag = new RateLimitedTracker();
        public int blocksFlagCarrierFriendly;
        public int blocksFlagCarrierEnemy;
        public int blocksFriendly;
        public int blocksEnemy;
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
        public DateTime onlineSince = DateTime.Now;
        //public int totalTimeVisible;
        //public int lastKnownServerTime;
        public UInt64[] strafeStyleSamples = new UInt64[8];

        // adapt from demo tools
        Int64 hitBySaberCount;
        Int64 hitBySaberBlockableCount;
        Int64 parryCount;
        Int64 attackFromParryCount;

        public Rating rating = null;

        public BlocksTracker blocksTracker = new BlocksTracker();
        public ReliableValueCounter dbsCounter = new ReliableValueCounter();

        private object trackedKillsLock = new object();
        private HashSet<UInt64> trackedKills = new HashSet<ulong>();
        private Dictionary<KillType, int> killTypes = new Dictionary<KillType, int>();
        private Dictionary<KillType, int> killTypesReturns = new Dictionary<KillType, int>();

        // This is just saved in here as a reference, we access them via infoPool.killTrackers, but this way we can keep track even of stuff with players that disconnected.
        public ConcurrentDictionary<SessionPlayerInfo, KillTracker> killTrackersOnOthers = new ConcurrentDictionary<SessionPlayerInfo, KillTracker>();
        public ConcurrentDictionary<SessionPlayerInfo, KillTracker> killTrackersOnMe = new ConcurrentDictionary<SessionPlayerInfo, KillTracker>();

        public void TrackKill(KillType killType, UInt64 killHash, bool isReturn)
        {
            killType.Internize();
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
        public Dictionary<KillType, int> GetKillTypes()
        {
            lock (trackedKillsLock)
            {
                return killTypes.ToDictionary(blah=>blah.Key,blah=>blah.Value); // Make a copy for thread safety.
            }
        }
        public Dictionary<string, int> GetKillTypesShortname()
        {
            lock (trackedKillsLock)
            {
                return killTypes.ToDictionary(blah=>blah.Key.shortname,blah=>blah.Value); // Make a copy for thread safety.
            }
        }
        public Dictionary<KillType, int> GetKillTypesReturns()
        {
            lock (trackedKillsLock)
            {
                return killTypesReturns.ToDictionary(blah=>blah.Key,blah=>blah.Value); // Make a copy for thread safety.
            }
        }
        ServerSharedInformationPool infoPool;
        public ChatCommandTrackingStuff(RatingCalculator ratingCalculator, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            rating = new Rating(ratingCalculator);
        }
        public Team LastNonSpectatorTeam { get; set; } = Team.Spectator; // xd


        #region score
        public PlayerScore score { get; set; } = new PlayerScore();
        public PlayerScore spectatingScore { get; set; } = new PlayerScore(); // score data we receive for people on spectator team. can't really trust it. does not apply to MOH.

        public bool lastScoreWasSpectating = false;

        public DateTime? lastScoreUpdated;
        #endregion

        public int ProbableRetCount => (infoPool?.serverSeemsToSupportRetsCountScoreboard).GetValueOrDefault(true) ? this.score.impressiveCount : this.returns;

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

    public struct Hitbox
    {
        static public readonly Vector3 defaultMins = new Vector3(-15,-15,-24);
        static public readonly Vector3 defaultMaxs = new Vector3(15,15,40);
        public Vector3 mins;
        public Vector3 maxs;
    }

    //public class PlayerIdentity { }

    // gets reset on reconnect
    public class SessionPlayerInfo
    {

        //public PlayerIdentity identity = new PlayerIdentity(); // changes when other player fills the slot
        
        public int clientNum { get; set; }
        public ChatCommandTrackingStuff chatCommandTrackingStuff = null;
        public ChatCommandTrackingStuff chatCommandTrackingStuffThisGame = null;
        public void ResetChatCommandTrackingStuff(RatingCalculator ratingCalculator, RatingCalculator ratingCalculatorThisGame)
        {
            chatCommandTrackingStuff = new ChatCommandTrackingStuff(ratingCalculator, infoPool);
            chatCommandTrackingStuffThisGame = new ChatCommandTrackingStuff(ratingCalculatorThisGame, infoPool);
        }
        public void ResetChatCommandTrackingStuffThisGame(RatingCalculator ratingCalculatorThisGame)
        {
            chatCommandTrackingStuffThisGame = new ChatCommandTrackingStuff(ratingCalculatorThisGame, infoPool);
        }
        ServerSharedInformationPool infoPool;
        public SessionPlayerInfo(RatingCalculator ratingCalculator, RatingCalculator ratingCalculatorThisGame,int clientNumA, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            chatCommandTrackingStuff = new ChatCommandTrackingStuff(ratingCalculator,infoPool);
            chatCommandTrackingStuffThisGame = new ChatCommandTrackingStuff(ratingCalculatorThisGame, infoPool);
            clientNum = clientNumA;
        }
        static readonly Regex PadawanNameMatch = new Regex(@"^\s*Padawan\s*(?:[\(\[]\s*\d*[\)\]]\s*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private HashSet<string> usedNames = new HashSet<string>();
        public string LastNonPadawanName { get; private set; }
        private string _name = null;

        public string[] GetUsedNames()
        {
            lock (usedNames)
            {
                return usedNames.ToArray();
            }
        }
        public string GetNameOrLastNonPadaName()
        {
            string lastNonPadaName = LastNonPadawanName;
            string currentName = _name;
            if (lastNonPadaName == currentName)
            {
                return currentName;
            } 
            else if(!string.IsNullOrWhiteSpace(lastNonPadaName))
            {
                return lastNonPadaName;
            }
            else
            {
                return currentName;
            }
        }
        public string name { 
            get {
                return _name;
            } 
            set {
                lock (usedNames)
                {
                    usedNames.Add(value);
                }
                if (!PadawanNameMatch.Match(Q3ColorFormatter.cleanupString(value)).Success && !string.IsNullOrWhiteSpace(value))
                {
                    LastNonPadawanName = value;
                }
                _name = value;
            }
        }
        private Team _team;
        public Team team
        {
            get {
                return _team;
            }
            set
            {
                if (value != Team.Spectator)
                {
                    this.chatCommandTrackingStuff.LastNonSpectatorTeam = value;
                    this.chatCommandTrackingStuffThisGame.LastNonSpectatorTeam = value;
                }
                _team = value;
            }
        }
        public bool confirmedBot { get; set; } = false;
        public bool confirmedJKWatcherFightbot { get; set; } = false;
        public string model { get; set; }
    }

    public class DelayedStatusWarner
    {
        object meLock = new object();
        DateTime? _firstStatusSatisfied = null;
        DateTime? _lastTimeWarned = null;
        // soft: dont warn.
        public bool check(bool statusSatisfied, double secondsDelayWarning, double secondsDelayWarnAgain, bool soft)
        {
            lock (meLock)
            {
                if (!statusSatisfied)
                {
                    _firstStatusSatisfied = null;
                    _lastTimeWarned = null;
                    return false;
                }
                if (!_firstStatusSatisfied.HasValue)
                {
                    _firstStatusSatisfied = DateTime.Now;
                    _lastTimeWarned = null;
                    return false;
                }
                if (soft)
                {
                    return false;
                }
                if ((DateTime.Now - _firstStatusSatisfied.Value).TotalSeconds > secondsDelayWarning)
                {
                    if (!_lastTimeWarned.HasValue || (DateTime.Now - _lastTimeWarned.Value).TotalSeconds > secondsDelayWarnAgain)
                    {
                        _lastTimeWarned = DateTime.Now;
                        return true;
                    }
                }
                return false;
            }
        }
    }


    // TODO MAke it easier to reset these between games or when maps change. Probably just make new new STatements?
    public class PlayerInfo
    {
        public ServerSharedInformationPool infoPool { get; init; } = null;


        public ChatCommandTrackingStuff chatCommandTrackingStuff => this.session.chatCommandTrackingStuff;
        public ChatCommandTrackingStuff chatCommandTrackingStuffThisGame => this.session.chatCommandTrackingStuffThisGame;
        public IEnumerable<ChatCommandTrackingStuff> GetChatCommandTrackers()
        {
            yield return this.chatCommandTrackingStuff;
            yield return this.chatCommandTrackingStuffThisGame;
        }
        public string name => this.session.name;
        public string model => this.session.model;
        public Team team => this.session.team;
        public PlayerScore scoreThisGame => this.chatCommandTrackingStuffThisGame.score;
        public PlayerScore scoreAll => this.chatCommandTrackingStuff.score;
        public PlayerScore currentScore => this.chatCommandTrackingStuff.lastScoreWasSpectating ? this.chatCommandTrackingStuff.spectatingScore: this.chatCommandTrackingStuff.score;
        public bool confirmedBot => this.session.confirmedBot;
        public bool confirmedJKWatcherFightbot => this.session.confirmedJKWatcherFightbot;

        public DelayedStatusWarner pingWarner = new DelayedStatusWarner();
        public DelayedStatusWarner afkWarner = new DelayedStatusWarner();

        #region position
        public Vector3 position;
        public DateTime? lastPositionUpdate; // Last time the player position alone was updated (from events or such)
        public DateTime lastMovementDirChange = DateTime.Now; // Last time the player position or angle changed
        public DateTime lastViewAngleChange = DateTime.Now; // Last view angle change. Used for afk warning prevention, but not for normal afk detection (avoid spamming warnjing in case someone is camping but still moving around his view)
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
        public DateTime? lastTimeClientInvalid; // Not on server (bugged served) or connecting.
        public float speed;
        public int groundEntityNum;
        public int torsoAnim;
        public int torsoAnimStartTime;
        public bool knockedDown;
        public int legsAnim;
        public bool duelInProgress;
        public int saberMove;
        public int forcePowersActive;

        private enum MovementDir {
            KEY_W = 0,
            KEY_WA = 1,
            KEY_A = 2,
            KEY_AS = 3,
            KEY_S = 4,
            KEY_SD = 5,
            KEY_D = 6,
            KEY_DW = 7,
            KEY_CENTER = 8,
        }

        private int _movementDir;
        private Vector3 _movementDirVector = new Vector3();
        private void _updateMovementDirVector() // for deluxe prediction :)
        {
            Vector2 dir =new Vector2();
            switch ((MovementDir)_movementDir)
            {
                case MovementDir.KEY_W:
                    dir.X = 127;
                    dir.Y = 0;
                    break;
                case MovementDir.KEY_WA:
                    dir.X = 127;
                    dir.Y = -127;
                    break;
                case MovementDir.KEY_A:
                    dir.X = 0;
                    dir.Y = -127;
                    break;
                case MovementDir.KEY_AS:
                    dir.X = -127;
                    dir.Y = -127;
                    break;
                case MovementDir.KEY_S:
                    dir.X = -127;
                    dir.Y = 0;
                    break;
                case MovementDir.KEY_SD:
                    dir.X = -127;
                    dir.Y = 127;
                    break;
                case MovementDir.KEY_D:
                    dir.X = 0;
                    dir.Y = 127;
                    break;
                case MovementDir.KEY_DW:
                    dir.X = 127;
                    dir.Y = 127;
                    break;
                default:
                    break;
            }
            dir = Vector2.Normalize(dir);
            Vector3 forward = new Vector3(), right = new Vector3(), up = new Vector3();
            Q3MathStuff.AngleVectors(angles, out forward, out right, out up);
            Vector3 dirVector = forward * dir.X + right * dir.Y;
            _movementDirVector = Vector3.Normalize(dirVector);
        }
        public Vector3 deluxePredict(float timeInSeconds)
        {
            //Vector3 pos = position;
            Vector3 accelVector = _movementDirVector;
            float accelerateRate = (groundEntityNum == Common.MaxGEntities - 1) ? 1.0f : 10.0f; // air accelerate vs walk accelerate
            float currentspeed = Vector3.Dot(velocity, accelVector);
            float wishspeed = speed == 0 ? 250 : speed;
            float addspeed = wishspeed - currentspeed;
            if(addspeed <= 0)
            {
                Vector3 naive2 = position + velocity * timeInSeconds; // old school;
                // good old naive prediction
                return naive2;
            }
            //accelspeed = accel * pml.frametime * wishspeed;
            float accelPerSecond = accelerateRate * wishspeed;
            float timeToFullAccel = addspeed / accelPerSecond;
            if(timeInSeconds < timeToFullAccel)
            {
                Vector3 naive2 = position + velocity * timeInSeconds; // old school;
                float displacement2 = 0.5f * (timeInSeconds * timeInSeconds) * accelPerSecond;
                return naive2 + displacement2 * accelVector;
            }
            Vector3 naive = position + velocity * timeToFullAccel; // old school;
            float displacement = 0.5f * (timeToFullAccel * timeToFullAccel) * accelPerSecond;
            Vector3 pos = naive + displacement * accelVector;
            Vector3 fullAccelSpeed = velocity + addspeed * accelVector;
            float timeLeft = timeInSeconds - timeToFullAccel;
            pos += timeLeft * fullAccelSpeed;
            return pos;
        }
        // must set this AFTER position (origin) and angles
        public int movementDir
        {
            get
            {
                return _movementDir;
            }
            set
            {
                _movementDir = value;
                _updateMovementDirVector();
            }
        }
        public Vector3 movementDirVector => _movementDirVector;
        public Hitbox hitBox;
        public DateTime? lastDrainedEvent;
        //public int legsTimer;
        #endregion

        public Vector3 lastDeathPosition;
        public DateTime? lastDeath;

        // The following 2 things probably aren't very useful when not on a server that sends all entities so take with a pinch of salt. They're kinda coded under the assumption that all entities are sent
        public DateTime?[] lastProximitySwing = new DateTime?[64]; // When have we last swung our saber within 200 units distance of other players?
        public DateTime?[] inProximitySince = new DateTime?[64]; // When is the last time this player came into proximity to another player (within 400 units)

        public DateTime lastSwing;

        // For killtrackers/memes and such
        public PlayerIdentification lastValidPlayerData = new PlayerIdentification();
        public DateTime? lastSeenValid = null;

        public SessionPlayerInfo session = null;

        public PlayerInfo(RatingCalculator ratingCalculator, RatingCalculator ratingCalculatorThisGame, int clientNumA, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            session = new SessionPlayerInfo(ratingCalculator, ratingCalculatorThisGame, clientNumA, infoPool);
        }

        public AliveInfo lastAliveInfo = null;


        public VisiblePlayersTracker VisiblePlayers { get; init; } = new VisiblePlayersTracker(); // For vis check debug?


        #region nwh
        public int nwhSpectatedPlayer { get; set; }
        public DateTime? nwhSpectatedPlayerLastUpdate;
        #endregion

        #region clientinfo
        public bool infoValid { get; set; }
        public bool inactiveMOH { get; set; }
        public int clientNum => this.session.clientNum;
        public DateTime? lastClientInfoUpdate;

        public AngleDecoder angleDecoder = new AngleDecoder();

        #endregion

        #region tinfo
        public volatile int location;       // location index for team mode
        public volatile int health;         // you only get this info about your teammates
        public volatile int armor;
        public volatile int curWeapon;
        public volatile int powerUps;		// so can display quad/flag status
        #endregion

        public string g2Rating => $"{(int)this.session.chatCommandTrackingStuff.rating.GetRating(true)}±{(int)this.session.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}";
        public string g2RatingThisGame => $"{(int)this.session.chatCommandTrackingStuffThisGame.rating.GetRating(true)}±{(int)this.session.chatCommandTrackingStuffThisGame.rating.GetRatingDeviation(true)}";
    }


    // Very silly?
    public interface IOldNewValueCondition {
        public bool ConditionTrue(int oldValue, int newValue);
    }

    public class ResetValueOnDecrease : IOldNewValueCondition
    {
        public bool ConditionTrue(int oldValue, int newValue)
        {
            return newValue < oldValue;
        }
    }
    public class ResetValueOnZero : IOldNewValueCondition
    {
        public bool ConditionTrue(int oldValue, int newValue)
        {
            return 0 < oldValue &&  newValue == 0;
        }
    }

    // Basically we keep a sum of the previous values in addition to the current value,
    // but only when we detect a "reset" which can be specified in more detail via IResetValueCondition
    // e.g. if a value decreases that means it was reset, and thus we add the old value to the sum.
    // A bit counter-intuitively, you can't use the assing operator but just use +=
    //
    // When IOldNewValueCondition returns true, new value is added to sum of old
    //
    // DON'T EVER CHANGE THIS FROM STRUCT TO CLASS!!!!!
    public struct ConditionalResetValueSummer<T> where T : IOldNewValueCondition, new()
    {
        int value;
        public int oldSum { get; private set; }

        private static T resetValueCondition = new T();
        public static ConditionalResetValueSummer<T> operator +(ConditionalResetValueSummer<T> a, int b)
        {
            if(resetValueCondition.ConditionTrue(a.value,b))
            {
                a.oldSum += a.value;
            }
            a.value = b;
            return a;
        }
        public static implicit operator int(ConditionalResetValueSummer<T> a) => a.value;
        public override string ToString()
        {
            return this.value.ToString();
        }
        public ConditionalResetValueSummer(int startValue)
        {
            value = startValue;
            oldSum = 0;
        }
        // Current value or sum
        public string GetString1(bool block0 = false,string prefixIfNotBlocked = null,Func<int,string> formatCallback = null)
        {
            int baseValue = value;
            string theString = formatCallback != null ? formatCallback(value) : value.ToString();
            if(oldSum > 0)
            {
                baseValue = oldSum + value;
                theString = formatCallback != null ? $"^yfff8∑^7{formatCallback(baseValue)}" : $"^yfff8∑^7{baseValue}";
            }

            if (block0 && baseValue == 0)
            {
                theString = "";
            } else if ((!block0 || baseValue != 0) && prefixIfNotBlocked != null)
            {
                theString = $"{prefixIfNotBlocked}{theString}";
            }

            return theString;
            /*if (oldSum > 0)
            {
                return $"^yfff8∑^7{oldSum + value}";
            }
            else
            {
                return value.ToString();
            }
            */
            
        }
        // Current value if sum exists or nothing
        public string GetString2(bool block0 = false, string prefixIfNotBlocked = null, Func<int, string> formatCallback = null)
        {
            if(oldSum <= 0)
            {
                return "";
            }

            int baseValue = value;
            string theString = formatCallback != null ? formatCallback(value) : value.ToString();

            if (block0 && baseValue == 0)
            {
                theString = "";
            }
            else if ((!block0 || baseValue != 0) && prefixIfNotBlocked != null)
            {
                theString = $"{prefixIfNotBlocked}{theString}";
            }

            return theString;
            /*if (oldSum > 0)
            {
                return value.ToString();
            } else
            {
                return "";
            }*/
        }
    }

    public class AveragePositiveNon999 : IOldNewValueCondition
    {
        public bool ConditionTrue(int oldValue, int newValue)
        {
            return newValue >= 0 && newValue < 999;
        }
    }
    public struct ValueAverager<T> where T : IOldNewValueCondition, new()
    {

        int value;
        Int64 total;
        Int64 divider;
        double m2;
        private static T addToAverageCondition = new T();
        public static ValueAverager<T> operator +(ValueAverager<T> a, int b)
        {
            if (addToAverageCondition.ConditionTrue(a.value, b))
            {
                // very inefficient way of doing welford :) 
                // but at least we keep the raw int values for total and divider.
                double mean = a.divider == 0.0 ? 0.0 : (double)a.total / (double)a.divider;
                double delta1 = (double)b - mean;
                a.total += b;
                a.divider++;

                // Welford standard deviation stuff. don't ask me how/why it works, im just applying it.
                mean = (double)a.total / (double)a.divider;
                double delta2 = (double)b - mean;
                a.m2 += delta1 * delta2;
            }
            a.value = b;
            return a;
        }
        public static implicit operator int(ValueAverager<T> a) => a.value;
        public override string ToString()
        {
            return this.value.ToString();
        }
        public double? GetPreciseAverage()
        {
            if (divider == 0) return null;
            return (double)((double)total / (double)divider);
        }
        public int? GetAverage()
        {
            if (divider == 0) return null;
            return (int)(total / divider);
        }
        public double? GetStandardDeviation()
        {
            if (divider < 2) return null;
            return Math.Sqrt(m2 / (double)divider);
        }
        // Mean + standar deviation
        public string GetMeanSDString()
        {
            StringBuilder sb = new StringBuilder();
            double? avg = this.GetPreciseAverage();
            if (!avg.HasValue || double.IsNaN(avg.Value)) return "";
            int roundedValue = (int)(Math.Round(avg.Value) + 0.5);
            sb.Append($"μ{roundedValue}");
            double? sd = this.GetStandardDeviation();
            if (!sd.HasValue || double.IsNaN(sd.Value)) return sb.ToString();
            roundedValue = (int)(Math.Round(sd.Value) + 0.5);
            if (roundedValue == 0) return sb.ToString();
            sb.Append($"σ{roundedValue}");
            return sb.ToString();
        }
        // Mean + standar deviation
        public string GetPreciseMeanSDString()
        {
            StringBuilder sb = new StringBuilder();
            double? avg = this.GetPreciseAverage();
            if (!avg.HasValue) return "";
            sb.Append("μ");
            sb.Append(avg.Value.ToString("0.##"));
            double? sd = this.GetStandardDeviation();
            if (!sd.HasValue || double.IsNaN(sd.Value)) return sb.ToString();
            sb.Append("σ");
            sb.Append(sd.Value.ToString("0.##"));
            return sb.ToString();
        }
    }

    public class PlayerScore : ICloneable
    {
        public volatile int client;
        private ConditionalResetValueSummer<ResetValueOnZero> _score = new ConditionalResetValueSummer<ResetValueOnZero>(-9999);
        public DateTime lastScoreValueChanged = DateTime.Now - new TimeSpan(1,0,0);
        public int score { 
            get {
                return _score;
            }
            set {
                if (_score != value)
                {
                    lastScoreValueChanged = DateTime.Now;
                }
                _score += value;
            }
        }
        public int oldScoreSum => _score.oldSum;
        public ValueAverager<AveragePositiveNon999> ping { get; set; }
        public ConditionalResetValueSummer<ResetValueOnDecrease> time { get; set; }
        public ConditionalResetValueSummer<ResetValueOnZero> timeResetOn0 { get; set; } // handles pauses better. why did i use ondecrease originally?
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
        public ConditionalResetValueSummer<ResetValueOnDecrease> impressiveCount { get; set; } // rets?
        public ConditionalResetValueSummer<ResetValueOnDecrease> excellentCount;
        public ConditionalResetValueSummer<ResetValueOnDecrease> guantletCount;
        public ConditionalResetValueSummer<ResetValueOnDecrease> defendCount { get; set; } // bc?
        public ConditionalResetValueSummer<ResetValueOnDecrease> assistCount;
        public ConditionalResetValueSummer<ResetValueOnDecrease> captures { get; set; } // captures

        public volatile bool perfect;
        public volatile int team;
        public DateTime? lastNonZeroPing;
        public volatile int pingUpdatesSinceLastNonZeroPing;

        public ConditionalResetValueSummer<ResetValueOnDecrease> deaths { get; set; } // times he got killed. Some JKA mods and some MOH gametypes send this.
        public volatile bool deathsIsFilled; // Indicate if killed value was sent

        // Special values only MB II uses.
        public volatile int mbIIrounds; // shows as "R" on scoreboard 
        public volatile int remainingLives; 
        public ConditionalResetValueSummer<ResetValueOnDecrease> kills; 
        public ConditionalResetValueSummer<ResetValueOnDecrease> totalKills; // MOHAA
        public volatile int mbIImysteryValue; 
        public volatile bool shortScoresMBII; // Indicate if only 9 score info things were sent.

        public object Clone()
        {
            return this.MemberwiseClone();
        }
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

    public record KillType
    {
        public string name;
        public string shortname;
        public void Internize()
        {
            name = string.Intern(name);
            shortname = string.Intern(shortname);
        }
    }

    public class KillTracker
    {
        public int returns; // Not currently used.
        public int kills;
        public DateTime? lastKillTime;
        public bool trackingMatch;
        public int trackedMatchKills;
        public int trackedMatchDeaths;

        public BlocksTracker blocksTracker = new BlocksTracker();

        private object trackedKillsLock = new object();
        private HashSet<UInt64> trackedKills = new HashSet<ulong>();
        private Dictionary<KillType, int> killTypes = new Dictionary<KillType, int>();
        private Dictionary<KillType, int> killTypesReturns = new Dictionary<KillType, int>();
        public void TrackKill(KillType killType, UInt64 killHash, bool isReturn)
        {
            killType.Internize();
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
        public Dictionary<KillType, int> GetKillTypes()
        {
            lock (trackedKillsLock)
            {
                return killTypes.ToDictionary(blah => blah.Key, blah => blah.Value); // Make a copy for thread safety.
            }
        }
        public Dictionary<KillType, int> GetKillTypesReturns()
        {
            lock (trackedKillsLock)
            {
                return killTypesReturns.ToDictionary(blah => blah.Key, blah => blah.Value); // Make a copy for thread safety.
            }
        }
    }

    public class IdentifiedPlayerStats {
        public SessionPlayerInfo playerSessInfo;
        public Rating rating => chatCommandTrackingStuff.rating;
        public PlayerScore score => chatCommandTrackingStuff.score;
        public ChatCommandTrackingStuff chatCommandTrackingStuff => thisGame ? playerSessInfo.chatCommandTrackingStuffThisGame : playerSessInfo.chatCommandTrackingStuff;
        public DateTime lastSeenActive = DateTime.Now;
        public bool thisGame = false;

        public IdentifiedPlayerStats(SessionPlayerInfo piS, bool thisGameA)
        {
            playerSessInfo = piS;
            thisGame = thisGameA;
            UpdateValid();
        }

        public void UpdateValid()
        {
            lastSeenActive = DateTime.Now;
        }
    }

    public enum SaberPrecision
    {
        Legacy,
        Precise,
        Random
    }

    public class GameEventFlags // we put this in a class so we can reset it in the func that receives it. idea is: each event will have the flag set on only ONE frame
    {
        [Flags]
        public enum Flags
        {
            RedReturn=1,
            BlueReturn=2,
            RedCapture=4,
            BlueCapture=8,
            RedPickup=16,
            BluePickup=32,
        }
        public GameEventFlags.Flags flags;

    }

    public class GameStatsFrame {
        public float redFlagRatio { get; init; } // 0 at base, 1 =in enemy base
        public float blueFlagRatio { get; init; }
        public bool paused { get; init; }
        public int flagCarrierRed { get; init; }
        public int flagCarrierBlue { get; init; }
        public GameEventFlags.Flags flags { get; init; }
    }

    public class GameStats {
        int lastServerTime = -1;
        DateTime lastFrameRecorded = DateTime.Now;
        public int timeTotal { get; private set; }
        public int pausedTime { get; private set; }
        object datalock = new object();
        List<GameStatsFrame> frames = new List<GameStatsFrame> ();
        public GameStatsFrame[] getFrames()
        {
            lock (datalock)
            {
                return frames.ToArray();
            }
        }
        public void SetStats(int serverTime, bool isCtf, float redFlagRatio, float blueFlagRatio, bool paused, int flagCarrierRed, int flagCarrierBlue, GameEventFlags flags)
        {
            lock (datalock)
            {
                int timeHere;
                if(serverTime < lastServerTime || lastServerTime == -1)
                {
                    timeHere = 0;
                }
                else
                {
                    timeHere = serverTime - lastServerTime;
                }
                if(timeHere > 10000)
                {
                    timeHere = 0; // this is clearly some kidn of bug. 
                }
                lastServerTime = serverTime;
                if (timeHere > 0)
                {
                    if ((DateTime.Now - lastFrameRecorded).TotalMilliseconds >= 1000 && isCtf) // record once per second
                    {
                        if(frames.Count > (14400+900))
                        {
                            frames.RemoveRange(0, frames.Count - 14400); // 14400 frames should cover a 4 hour game. enough i think? dont wanna have an endless memory leak. reset every 15 min
                        }
                        frames.Add(new GameStatsFrame() { redFlagRatio = redFlagRatio, blueFlagRatio = blueFlagRatio, paused = paused, flagCarrierBlue = flagCarrierBlue, flagCarrierRed = flagCarrierRed,flags =flags.flags });
                        flags.flags = 0;
                        lastFrameRecorded = DateTime.Now;
                    }
                    if (paused)
                    {
                        pausedTime += timeHere;
                    }
                    timeTotal += timeHere;
                }
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
        public SaberPrecision precision = SaberPrecision.Random; // Try to hit people precisely
        public bool selfPredict = true; // Predict the bots own position with ping value
        public float deluxePredict = 1.0f;
        public string sillyModeCustomCommand = null;
        public DateTime lastBerserkerStarted = DateTime.Now - new TimeSpan(10, 0, 0);
        public DateTime lastBodyguardStarted = DateTime.Now - new TimeSpan(10, 0, 0);
        public DateTime lastAnyFlagSeen = DateTime.Now - new TimeSpan(10, 0, 0);
        public int sillyBodyguardPlayer = -1;
        public bool NWHDetected = false;
        public bool mohMode = false;
        public GameType gameType;
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
        public bool gameIsPaused { get; set; } = false;

        public volatile int serverTime = 0;
        private volatile int gameTime = 0;
        public SaberAnimationVersion saberVersion = SaberAnimationVersion.JK2_102;
        public string GameTime { get; private set; }
        public int GameSeconds => gameTime;
        public string ServerName = "";
        public string MapName { get; set; }
        public int ScoreRed { get; set; }
        public int ScoreBlue { get; set; }
        public List<WayPoint> wayPoints = new List<WayPoint>();

        public object killTrackersLock = new object();
        public KillTracker[,] killTrackers;
        public KillTracker[,] killTrackersThisGame;
        public GameEventFlags eventFlagsThisGame = new GameEventFlags();
        public GameStats gameStatsThisGame = new GameStats();

        public RatingCalculator ratingCalculator = new RatingCalculator();
        public RatingCalculator ratingCalculatorThisGame = new RatingCalculator();

        public RatingPeriodResults ratingPeriodResults = new RatingPeriodResults();
        public RatingPeriodResults ratingPeriodResultsThisGame = new RatingPeriodResults();

        public ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats> ratingsAndNames = new ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats>();
        public ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats> ratingsAndNamesThisGame = new ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats>();

        public LevelShotData levelShot = new LevelShotData();
        public LevelShotData levelShotZCompNoBot = new LevelShotData();
        public LevelShotData levelShotThisGame = new LevelShotData();

        public DateTime lastThisGameReset = DateTime.Now - new TimeSpan(0, 1, 0);

        // we do this with sqlite instead
        //public AveragingDictionary<string> averageMapTimes = new AveragingDictionary<string>();

        public void resetLevelShot(bool thisGame, bool normal)
        {
            if (thisGame)
            {
                levelShotThisGame = new LevelShotData();
            }
            if (normal)
            {
                levelShot = new LevelShotData();
            }
        }

        public int getProbableRetCount(int clientNum, bool thisGame = false) // TODO if thisgame==false should this return all rets of all time? hm
        {
            if(clientNum < 0 || clientNum > _maxClients)
            {
                return 0;
            } else if(thisGame)
            {
                return serverSeemsToSupportRetsCountScoreboard ? this.playerInfo[clientNum].chatCommandTrackingStuffThisGame.score.impressiveCount : this.playerInfo[clientNum].chatCommandTrackingStuffThisGame.returns;
            } else
            {
                return serverSeemsToSupportRetsCountScoreboard ? this.playerInfo[clientNum].chatCommandTrackingStuff.score.impressiveCount : this.playerInfo[clientNum].chatCommandTrackingStuff.returns;
            }
        }

        public bool NoActivePlayers { get; set; }
        public bool serverSeemsToSupportRetsCountScoreboard = false;
        public bool serverSendsAllEntities = false;

        public DateTime? lastBotOnlyConfirmed = null;
        public DateTime? lastScoreboardReceived = null;
        public DateTime? lastServerInfoReceived = null;
        public DateTime infoPoolCreated = DateTime.Now;
        
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
        /*public void ResetKillTracker(int fromPlayer, int toPlayer, bool thisGame)
        {
            if (thisGame)
            {
                killTrackers[fromPlayer, toPlayer] = new KillTracker();
            }
            else
            {
                killTrackersThisGame[fromPlayer, toPlayer] = new KillTracker();
            }
        }*/

        // TODO Rethink all calls to this function in terms of multithreading and race conditions...
        // Right now the approach is to kinda bulldoze through it all and just call it whenever anything is done with a killtracker
        // But we should probably do something way way smarter and more well thought out and more organized.
        public void UpdateKillTrackerReferences(int p1, int p2)
        {
            lock (killTrackers)
            {
                KillTracker theTracker = killTrackers[p1, p2];
                this.playerInfo[p1].session.chatCommandTrackingStuff.killTrackersOnOthers[this.playerInfo[p2].session] = theTracker;
                this.playerInfo[p2].session.chatCommandTrackingStuff.killTrackersOnMe[this.playerInfo[p1].session] = theTracker;
                theTracker = killTrackersThisGame[p1, p2];
                this.playerInfo[p1].session.chatCommandTrackingStuffThisGame.killTrackersOnOthers[this.playerInfo[p2].session] = theTracker;
                this.playerInfo[p2].session.chatCommandTrackingStuffThisGame.killTrackersOnMe[this.playerInfo[p1].session] = theTracker;
            }
        }

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
            killTrackersThisGame = new KillTracker[maxClients, maxClients];
            for(int i = 0; i < maxClients; i++)
            {
                for (int a = 0; a < maxClients; a++)
                {
                    killTrackers[i,a] = new KillTracker();
                    killTrackersThisGame[i,a] = new KillTracker();
                }
            }

            lastConfirmedVisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);
            lastConfirmedInvisible = new ArbitraryOrder2DArray<DateTime?>(Common.MaxGEntities);

            for (int i = 0; i < playerInfo.Length; i++)
            {
                playerInfo[i] = new PlayerInfo(ratingCalculator,ratingCalculatorThisGame,i, this) ;
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
                if (pi.infoValid && ((1 << (int)pi.session.team) & teamBitMask)>0)
                {
                    string compareLowerCaseName = pi.session.name.ToLower();
                    string compareNoColorName = Q3ColorFormatter.cleanupString(pi.session.name);
                    string compareNoColorLowercaseName = compareNoColorName.ToLower();

                    if (pi.session.name.Contains(searchString))
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
