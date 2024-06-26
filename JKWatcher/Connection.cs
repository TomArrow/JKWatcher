using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JKClient;
using PropertyChanged;
using Client = JKClient.JKClient;
using ConditionalCommand = JKWatcher.ConnectedServerWindow.ConnectionOptions.ConditionalCommand;

namespace JKWatcher
{

    enum ConfigStringDefines
    {
        //CS_MUSIC = 2,
        //CS_MESSAGE = 3,     // from the map worldspawn's message field
        //CS_MOTD = 4,        // g_motd string for server message of the day
        //CS_WARMUP = 5,      // server time when the match will be restarted
        //CS_SCORES1 = 6,
        //CS_SCORES2 = 7,
        //CS_VOTE_TIME = 8,
        //CS_VOTE_STRING = 9,
        //CS_VOTE_YES = 10,
        //CS_VOTE_NO = 11,

        //CS_TEAMVOTE_TIME = 12,
        //CS_TEAMVOTE_STRING = 14,

        //CS_TEAMVOTE_YES = 16,
        //CS_TEAMVOTE_NO = 18,

        //CS_GAME_VERSION = 20,
        //CS_LEVEL_START_TIME = 21,       // so the timer only shows the current level
        //CS_INTERMISSION = 22,       // when 1, fraglimit/timelimit has been hit and intermission will start in a second or two
        CS_FLAGSTATUS = 23,     // string indicating flag status in CTF
        //CS_SHADERSTATE = 24,
        //CS_BOTINFO = 25,

        //CS_MVSDK = 26,      // CS for mvsdk specific configuration

        //CS_ITEMS = 27,      // string of 0's and 1's that tell which items are present

        //CS_CLIENT_JEDIMASTER = 28,      // current jedi master
        //CS_CLIENT_DUELWINNER = 29,      // current duel round winner - needed for printing at top of scoreboard
        //CS_CLIENT_DUELISTS = 30,        // client numbers for both current duelists. Needed for a number of client-side things.

        // these are also in be_aas_def.h - argh (rjr)
        //CS_MODELS=32
    }

    enum PersistantEnum {
        PERS_SCORE=0,                     // !!! MUST NOT CHANGE, SERVER AND GAME BOTH REFERENCE !!!
        //PERS_HITS=1,                      // total points damage inflicted so damage beeps can sound on change
        //PERS_RANK=2,                      // player rank or team rank
        //PERS_TEAM=3,                      // player team
        //PERS_SPAWN_COUNT=4,               // incremented every respawn
        //PERS_PLAYEREVENTS=5,              // 16 bits that can be flipped for events
        //PERS_ATTACKER=6,                  // clientnum of last damage inflicter
        //PERS_ATTACKEE_ARMOR=7,            // health/armor of last person we attacked
        //PERS_KILLED=8,                    // count of the number of times you died
                                        // player awards tracking
        //PERS_IMPRESSIVE_COUNT=9,          // two railgun hits in a row
        //PERS_EXCELLENT_COUNT=10,           // two successive kills in a short amount of time
        //PERS_DEFEND_COUNT=11,              // defend awards
        //PERS_ASSIST_COUNT=12,              // assist awards
        //PERS_GAUNTLET_FRAG_COUNT=13,       // kills with the guantlet
        //PERS_CAPTURES=14                   // captures
    }

    public enum RequestCategory
    {
        NONE,
        SCOREBOARD,
        FOLLOW,
        INFOCOMMANDS,
        MEME,
        KILLTRACKER,
        SELFKILL,
        GOINTOSPEC,
        GOINTOSPECHACK,
        FIGHTBOT,
        FIGHTBOT_QUEUED,
        FIGHTBOT_WAYPOINTCOMMAND,
        BOTSAY,
        FIGHTBOTSPAWNRELATED, // Going to spec, going into free team etc.
        FIGHTBOTSPAWNAFKCHECK,
        CALENDAREVENT_ANNOUNCE,
        MAPCHANGECOMMAND,
        CONDITIONALCOMMAND,
        CONDITIONALCOMMANDNOSPAM,
        CONDITIONALCOMMANDNOSPAMSAME,
        STUFFTEXTECHO,
        JKCLIENTINTERNAL,
        AUTOPRINTSTATS
    }

    struct MvHttpDownloadInfo
    {
        public bool httpIsAvailable;
        public string urlPrefix;
    }

    public enum MovementDir
    {
        W,
        WA,
        A,
        AS,
        S,
        SD,
        D,
        DW,
        CountDirs
    }

    public class SnapStatusInfo {
        const int averageWindow = 30; // Max samples
        const int averageWindowTime = 2000; // Max time window

        private int lastServerTime = 0;
        private int lastMessageNum = 0;
        private struct SnapStatusInfoSnippet
        {
            public int duration; // difference from last serverTime
            public int serverTime;
            public int snapNumIncrementSinceLast; // (skipped packets+1 basically)
        }

        private SnapStatusInfoSnippet[] infoSnippets = new SnapStatusInfoSnippet[averageWindow];
        private int index = 0;

        public void addDataPoint(int messageNum, int serverTime)
        {
            infoSnippets[index].duration = serverTime - lastServerTime;
            infoSnippets[index].snapNumIncrementSinceLast = messageNum - lastMessageNum;
            infoSnippets[index].serverTime = serverTime;
            lastServerTime = serverTime;
            lastMessageNum = messageNum;
            index = (index + 1) % averageWindow;
        }

        public int ReceivedSnaps { get; private set; } = 0;
        public int TotalSnaps { get; private set; } = 0;

        public override string ToString()
        {
            int timeTotal = 0;
            int packetCount = 0;
            int packetCountIncludingSkipped = 0;
            foreach(SnapStatusInfoSnippet snippet in infoSnippets)
            {
                if((lastServerTime- snippet.serverTime) < averageWindowTime)
                {
                    packetCount++;
                    packetCountIncludingSkipped += snippet.snapNumIncrementSinceLast;
                    timeTotal += snippet.duration;
                }
            }
            ReceivedSnaps = timeTotal == 0 ? 0 : 1000 * packetCount / timeTotal;
            TotalSnaps = timeTotal == 0 ? 0 : 1000 * packetCountIncludingSkipped / timeTotal;
            return $"{ReceivedSnaps}/{TotalSnaps}";
        }
    }

    /*
     * General notes regarding flood protection.
     * Old style servers only allow 1 command per second period. If you send a command within a second of another command,
     * it's game over. Newer ones apparently allow bursts of 3 commands, and then the 1 second limit is enforced again.
     * This tool more or less hopes for the latter. It would be hard to control command timing to the degree necessary
     * to avoid ever having less than 1 second delay between two commands.
     * 
     * We try to generally stay within the limits though naturally. Experience will show how well it will work.
     * 
     */
    public partial class Connection : INotifyPropertyChanged
    {
        // Setting it a bit higher than in the jk2 code itself, just to be safe. Internet delays etc. could cause issues.
        // Still not absolutely foolproof I guess but about as good as I can do.
        const int floodProtectPeriod = 1100;

        public Client client;
        private ConnectedServerWindow serverWindow;

        public event PropertyChangedEventHandler PropertyChanged;

        public event UserCommandGeneratedEventHandler ClientUserCommandGenerated;
        public event Action<ServerInfo> ServerInfoChanged; // forward to the outside if desired
        internal void OnClientUserCommandGenerated(ref UserCommand cmd, in UserCommand previousCommand, ref List<UserCommand> insertCommands)
        {
            this.ClientUserCommandGenerated?.Invoke(this, ref cmd, in previousCommand, ref insertCommands);
        }

        public event Action<CommandEventArgs> ServerCommandRan;
        void RunServerCommand(CommandEventArgs eventArgs)
        {
            this.ServerCommandRan?.Invoke(eventArgs);
        }

        public JKClient.Statistics clientStatistics { get; private set; }
        public bool GhostPeer { get; private set; } = false;

        // To detect changes.
        private string lastKnownPakNames = "";
        private string lastKnownPakChecksums = "";

        public string NameOverride { 
            get {
                return nameOverride;
            }
            set { 
                if(value != nameOverride)
                {
                    nameOverride = value;
                    updateName();
                }
            } 
        } 
        private string nameOverride = null; 

        public int? ClientNum { get; set; } = null;
        private DateTime lastServerInfoChange = DateTime.Now;
        private DateTime lastSpectatedPlayerChange = DateTime.Now;
        private int? oldSpectatedPlayer = null;
        public int? SpectatedPlayer { get; set; } = null;
        public int? WishSpectatedPlayer { get; set; } = null;
        public PlayerMoveType? PlayerMoveType { get; set; } = null;

        private bool mohSpectatorFreeFloat = false;
        private DateTime lastMOHSpectatorNonFreeFloat = DateTime.Now;

        private int? _index = null;
        public int? Index
        {
            get
            {
                return _index;
            }
            set
            {
                if(_index != value)
                {
                    _index = value;
                    updateName();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Index"));
                }
            }
        }


        //public int? CameraOperator { get; set; } = null;
        public CameraOperator CameraOperator { get; set; } = null;

        private bool trulyDisconnected = true; // If we disconnected manually we want to stay disconnected.

        public SnapStatusInfo SnapStatus { get; private set; } = new SnapStatusInfo();

        public bool AlwaysFollowSomeone { get; set; } = true;
        public bool HandleAutoCommands { get; set; } = true; // Conditional commands etc.

        //public ConnectionStatus Status => client != null ? client.Status : ConnectionStatus.Disconnected;
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        private ServerSharedInformationPool infoPool;

        public string GameTime { get; set; } = null;

        public bool isRecordingADemo { get; private set; } = false;

        public LeakyBucketRequester<string, RequestCategory> leakyBucketRequester = null;

        MvHttpDownloadInfo? mvHttpDownloadInfo = null;


        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();


        /*public Connection(ConnectedServerWindow serverWindowA, string ip, ProtocolVersion protocol, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            _ = createConnection(ip, protocol);
        }
        public Connection(ServerInfo serverInfo, ConnectedServerWindow serverWindowA,ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            leakyBucketRequester = new LeakyBucketRequester<string, RequestCategory>(3, floodProtectPeriod); // Assuming default sv_floodcontrol 3, but will be adjusted once known
            leakyBucketRequester.CommandExecuting += LeakyBucketRequester_CommandExecuting; ;
            _ = createConnection(serverInfo.Address.ToString(), serverInfo.Protocol);
            createPeriodicReconnecter();
        }*/

        private string password = null;
        //private string userInfoName = null;
        //private bool demoTimeNameColors = false;
        //private bool attachClientNumToName = false;

        SnapsSettings snapsSettings = null;

        ConnectedServerWindow.ConnectionOptions _connectionOptions = null;

        private bool jkaMode = false;
        public bool mohMode { get; private set; } = false;
        private bool mohExpansion = false;
        private bool mohFreezeTagDetected = false;
        private bool mohFreezeTagSendsFrozenMessages = false;
        private bool mohFreezeTagSendsMeltedMessages = false;
        private int mohFreezeTagSwitchToFrozenCount = 0;
        private bool mohFreezeTagAllowsFrozenFollow = false;
        private DateTime mohFreezeTagAllowsFrozenFollowLastConfirmed = DateTime.Now;
        private bool JAPlusDetected = false;
        private bool JAProDetected = false;
        private bool MBIIDetected = false;
        private bool SaberModDetected = false;

        private string chatCommandPublic = "say";
        private string chatCommandTeam = "say_team";

        public Connection( NetAddress addressA, ProtocolVersion protocolA, ConnectedServerWindow serverWindowA, ServerSharedInformationPool infoPoolA, ConnectedServerWindow.ConnectionOptions connectionOptions, string passwordA = null, /*string userInfoNameA = null, bool dateTimeColorNamesA = false, bool attachClientNumToNameA = false,*/ SnapsSettings snapsSettingsA = null, bool ghostPeer = false)
        {
            if(connectionOptions == null)
            {
                throw new InvalidOperationException("Cannot create connection with null connectionOptions");
            }
            this.GhostPeer = ghostPeer;
            _connectionOptions = connectionOptions;
            _connectionOptions.PropertyChanged += _connectionOptions_PropertyChanged;
            if (protocolA == ProtocolVersion.Protocol26)
            {
                jkaMode = true;
            } else if (protocolA >= ProtocolVersion.Protocol6 && protocolA <= ProtocolVersion.Protocol8 || protocolA == ProtocolVersion.Protocol17) // TODO Support 15,16 too?
            {
                mohMode = true;
                if (protocolA > ProtocolVersion.Protocol8)
                {
                    mohExpansion = true;
                }
                chatCommandPublic = "dmmessage 0";
                chatCommandTeam = "dmmessage -1";
            }
            snapsSettings = snapsSettingsA;
            //demoTimeNameColors = dateTimeColorNamesA;
            //attachClientNumToName = attachClientNumToNameA;
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            //userInfoName = userInfoNameA;
            password = passwordA;
            leakyBucketRequester = new LeakyBucketRequester<string, RequestCategory>(3, floodProtectPeriod, serverWindow.ServerName, addressA.ToString()); // Assuming default sv_floodcontrol 3, but will be adjusted once known
            leakyBucketRequester.CommandExecuting += LeakyBucketRequester_CommandExecuting; ;
            _ = createConnection(addressA.ToString(), protocolA);
            createPeriodicReconnecter();
        }

        internal static int GameTypeStringToBitMask(string gameTypesString)
        {
            int gameTypes = 0;
            string[] gameTypesStrings = gameTypesString?.Trim().Split(new char[] { ',' , ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (gameTypesStrings != null)
            {
                foreach (string gameTypeString in gameTypesStrings)
                {
                    switch (gameTypeString)
                    {
                        case "ffa":
                            gameTypes |= (1 << (int)GameType.FFA);
                            break;
                        case "holocron":
                            gameTypes |= (1 << (int)GameType.Holocron);
                            break;
                        case "jedimaster":
                            gameTypes |= (1 << (int)GameType.JediMaster);
                            break;
                        case "duel":
                            gameTypes |= (1 << (int)GameType.Duel);
                            break;
                        case "powerduel":
                            gameTypes |= (1 << (int)GameType.PowerDuel);
                            break;
                        case "sp":
                            gameTypes |= (1 << (int)GameType.SinglePlayer);
                            break;
                        case "tffa":
                            gameTypes |= (1 << (int)GameType.Team);
                            break;
                        case "siege":
                            gameTypes |= (1 << (int)GameType.Siege);
                            break;
                        case "cty":
                            gameTypes |= (1 << (int)GameType.CTY);
                            break;
                        case "ctf":
                            gameTypes |= (1 << (int)GameType.CTF);
                            break;
                        case "1flagctf":
                            gameTypes |= (1 << (int)GameType.OneFlagCTF);
                            break;
                        case "obelisk":
                            gameTypes |= (1 << (int)GameType.Obelisk);
                            break;
                        case "harvester":
                            gameTypes |= (1 << (int)GameType.Harvester);
                            break;
                        case "teamrounds":
                            gameTypes |= (1 << (int)GameType.TeamRounds);
                            break;
                        case "objective":
                            gameTypes |= (1 << (int)GameType.Objective);
                            break;
                        case "tow":
                            gameTypes |= (1 << (int)GameType.TOW);
                            break;
                        case "liberation":
                            gameTypes |= (1 << (int)GameType.Liberation);
                            break;
                    }

                }
            }
            return gameTypes;
        }

        private void _connectionOptions_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "demoTimeColorNames" || e.PropertyName == "userInfoName" || e.PropertyName == "attachClientNumToName")
            {
                updateName();
            } else if(e.PropertyName == "skin")
            {
                updateSkin();
            }
        }

        public void SetPassword(string passwordA)
        {
            password = passwordA;
            if(client != null)
            {
                client.Password = password != null ? password : "";
            }
        }
        /*
        public void SetUserInfoName(string userInfoNameA)
        {
            userInfoName = userInfoNameA;
            updateName();
        }

        public void SetDemoTimeNameColors(bool doColor)
        {
            demoTimeNameColors = doColor;
            updateName();
        }
        public void SetClientNumNameAttach(bool doAttach)
        {
            attachClientNumToName = doAttach;
            updateName();
        }*/

        private string realName = "Padawan";
        private void updateName()
        {
            if (client != null)
            {
                string nameToUse = nameOverride == null ? ( _connectionOptions.userInfoName != null ? _connectionOptions.userInfoName : "Padawan" ) : nameOverride;

                if (nameToUse.Contains('\\'))
                {
                    string[] nameOptions = nameToUse.Split('\\');
                    if (this.Index.HasValue)
                    {
                        int nameIndex = (int)Math.Abs(this.Index.GetValueOrDefault(0) % nameOptions.Length);
                        nameToUse = nameOptions[nameIndex];
                    }
                    else
                    {
                        nameToUse = "Padawan";
                    }
                }

                bool clientNumAlreadyAdded = false;
                if (!mohMode && _connectionOptions.demoTimeColorNames && client.Demorecording && !nameToUse.Contains("^"))
                {
                    DemoName_t demoName = client.getDemoName();
                    if(demoName != null) // Pointless I guess, hmm
                    {
                        DateTime demoStartTime = demoName.time;
                        string colorCodes = Convert.ToString(((DateTimeOffset)demoStartTime.ToUniversalTime()).ToUnixTimeSeconds(), 8);
                        while(colorCodes.Length < 12)
                        {
                            colorCodes = "0" + colorCodes;
                        }
                        if(colorCodes.Length > 12)
                        {
                            serverWindow.addToLog("Datetime Colorcode for name is more than 12 letters! Weird.", true);
                        }
                        else
                        {
                            if (jkaMode) // Lesss elegant but works I guess. JKA doesn't have background colors. TODO: Make this for 1.04 too.
                            {
                                string clientNumAddition = "";
                                if (_connectionOptions.attachClientNumToName) // For JKA we attach the clientnum here already so we can use it for the colors as well. Not elegant but better than filling with points more than necessary
                                {
                                    int clientNum = (client?.clientNum).GetValueOrDefault(-1);
                                    if (clientNum != -1)
                                    {
                                        clientNumAddition = $"({clientNum})";
                                        clientNumAlreadyAdded = true;
                                    }
                                }

                                int nameToUseLength = nameToUse.SpacelessStringLength();
                                while ((nameToUseLength + clientNumAddition.Length) < 12)
                                {
                                    nameToUse += ".";
                                    nameToUseLength++;
                                }
                                nameToUse += clientNumAddition;

                                StringBuilder tmpName = new StringBuilder();

                                int indexExtra = 0;
                                int i = 0;
                                for (i = 0; i < 12; i++)
                                {
                                    if (nameToUse[i + indexExtra].IsSpaceChar()) // Skip space characters
                                    {
                                        tmpName.Append(nameToUse[i + indexExtra]);
                                        indexExtra++;
                                        i--;
                                    } else
                                    {
                                        tmpName.Append("^");
                                        tmpName.Append(colorCodes[i]);
                                        tmpName.Append(nameToUse[i + indexExtra]);
                                    }
                                }
                                while (nameToUse.Length > (i+indexExtra))
                                {
                                    tmpName.Append(nameToUse[i + indexExtra]);
                                    i++;
                                }
                                //if (nameToUse.Length > 12)
                                //{
                                 //   tmpName.Append(nameToUse.Substring(12));
                                //}
                                nameToUse = tmpName.ToString();
                            } else
                            {

                                StringBuilder tmpName = new StringBuilder();

                                int nameToUseLength = nameToUse.SpacelessStringLength();
                                while (nameToUseLength < 6)
                                {
                                    nameToUse += ".";
                                    nameToUseLength++;
                                }
                                int indexExtra = 0;
                                int i = 0;
                                for (i = 0; i < 6; i++)
                                {
                                    if (nameToUse[i + indexExtra].IsSpaceChar()) // Skip space characters
                                    {
                                        tmpName.Append(nameToUse[i + indexExtra]);
                                        indexExtra++;
                                        i--;
                                    }
                                    else
                                    {
                                        tmpName.Append("^");
                                        tmpName.Append(colorCodes[i * 2]);
                                        tmpName.Append("^");
                                        tmpName.Append(colorCodes[i * 2 + 1]);
                                        tmpName.Append("^");
                                        tmpName.Append(colorCodes[i * 2]);
                                        tmpName.Append(nameToUse[i + indexExtra]);
                                    }
                                }
                                while (nameToUse.Length > (i + indexExtra))
                                {
                                    tmpName.Append(nameToUse[i + indexExtra]);
                                    i++;
                                }
                                //if (nameToUse.Length > 6)
                                //{
                                //    tmpName.Append(nameToUse.Substring(6));
                                //}
                                nameToUse = tmpName.ToString();
                            }
                        }
                    }
                }
                if (_connectionOptions.attachClientNumToName && !clientNumAlreadyAdded)
                {
                    int clientNum = (client?.clientNum).GetValueOrDefault(-1);
                    if(clientNum != -1)
                    {
                        nameToUse += $" ^7(^2{clientNum}^7)";
                    }
                }
                realName = nameToUse;
                client.Name = nameToUse;
            }
        }
        
        private void updateSkin()
        {
            if (client != null)
            {
                string skinToUse = _connectionOptions.skin != null ? _connectionOptions.userInfoName : "kyle/default";
                
                client.Skin = skinToUse;
            }
        }

        private void createPeriodicReconnecter()
        {
            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { periodicReconnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
               serverWindow.addToLog(t.Exception.ToString(), true);
            }, TaskContinuationOptions.OnlyOnFaulted), $"Periodic reconnecter ({serverWindow.netAddress},{serverWindow.ServerName})");
            backgroundTasks.Add(tokenSource);
        }

        private void periodicReconnecter(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(5*60*1000); // 5 minutes
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                if (client.Status != ConnectionStatus.Active && !trulyDisconnected)
                {
                    Reconnect();
                }
                if (client.Status == ConnectionStatus.Active && shouldBeRecordingADemo && !isRecordingADemo)
                {
                    serverWindow.addToLog("periodicReconnecter: Attempting to start/resume demo recording. (shouldBeRecordingADemo = true)");
                    startDemoRecord();
                }
            }
        }

        // !!!!TODO: If we are currently in an intermission, only allow whitelisted commands. Some servers will
        // turn any non whitelisted commands into a "say", particularly base, basejk and basejka servers.
        // Implement a way to delay commands so we can go on with the next one instead of retrying the same over and over.
        // Once intermission ends we can do others.
        // Also, when in an intermission, send an occasional click or 2 to end the intermission? Not that important tho i guess
        string[] intermissionCommandWhitelistJKA = new string[] { 
            
            // Server
            "userinfo","disconnect","cp","vdr","download","nextdl","stopdl","donedl",

            // Game always
            "say","say_team","tell","voice_cmd","score",

            // Bot commands
            // There's a bunch of bot related commands that seem to be ok too but have other restrictions. Whatever. Let's not allow them I guess

            // Game Intermission
            // Technically the below won't turn into a say but will give an error which isnt nice either.
            "give", "giveother", "god", "notarget", "noclip", "kill", "teamtask", "levelshot", "follow", "follownext", "followprev", "team", "duelteam", "siegeclass", "forcechanged", "where", "callvote", "vote", "callteamvote", "teamvote", "gc", "setviewpos", "stats" 
        };
        string[] intermissionCommandWhitelistJK2 = new string[] { 
            
            // Server
            "userinfo","disconnect","cp","vdr","download","nextdl","stopdl","donedl",

            // Game always
            "say","say_team","tell","vsay","vsay_team","vtell","vosay","vosay_team","votell","vtaunt","score",

            // Bot commands
            // There's a bunch of bot related commands that seem to be ok too but have other restrictions. Whatever. Let's not allow them I guess

            // Game Intermission
            // Technically the below won't turn into a say but will give an error which isnt nice either.
            "give", "god" ,"notarget" ,"noclip" ,"kill" ,"teamtask" ,"levelshot" ,"follow", "follownext", "followprev", "team", "forcechanged", "where", "callvote", "vote", "callteamvote", "teamvote", "gc", "setviewpos", "stats"
        };

        public bool CurrentTimeSecondEven { get; set; } = (DateTime.Now.Second % 2) > 0; // Really shitty lol. Just wanna be updating this value once per second so some values can get updated.
        public DateTime beQuietUntil { get; set; } = DateTime.Now - new TimeSpan(999, 0, 0);

        [DependsOn("beQuietUntil", "CurrentTimeSecondEven")]
        public bool QuietMode { get {
                return beQuietUntil > DateTime.Now;
            }
        }
        [DependsOn("beQuietUntil", "CurrentTimeSecondEven")]
        public int QuietModeTimeOut { get {
                return beQuietUntil > DateTime.Now ? (int)(beQuietUntil-DateTime.Now).TotalSeconds : 0;
            } }

        private void LeakyBucketRequester_CommandExecuting(object sender, LeakyBucketRequester<string, RequestCategory>.CommandExecutingEventArgs e)
        {
            // Check if the command is supported by server (it's just a crude array that gets elements added if server responds that a command is unsupported. Don't waste time, burst allowance, bandwidth and demo size sending useless commands).
            bool firstNonSpaceFound = false;
            int firstTrueSpace = -1;
            for(int i = 0; i < e.Command.Length; i++)
            {
                if(e.Command[i] == ' ')
                {
                    if (firstNonSpaceFound)
                    {
                        firstTrueSpace = i;
                        break;
                    }
                }
                else
                {
                    firstNonSpaceFound = true;
                }
            }
            string commandForValidityCheck = (firstTrueSpace != -1 ? e.Command.Substring(0, firstTrueSpace) : e.Command).Trim().ToLower();
            if (infoPool.unsupportedCommands.Contains(commandForValidityCheck))
            {
                e.Discard = true;
                return;
            }
            if (infoPool.isIntermission)
            {
                if (jkaMode)
                {
                    if (!intermissionCommandWhitelistJKA.Contains(commandForValidityCheck))
                    {
                        e.Delay = true;
                        e.NextTryAllowedIfDelayed = 500;
                        return;
                    }
                }
                else
                {
                    if (!intermissionCommandWhitelistJK2.Contains(commandForValidityCheck))
                    {
                        e.Delay = true;
                        e.NextTryAllowedIfDelayed = 500;
                        return;
                    }
                }
            }

            // Ok command is valid, let's see...
            if (client.Status == ConnectionStatus.Active) // safety check
            {
                int unacked = client.GetUnacknowledgedReliableCommandCount();
                if(unacked < 5)
                {

                    if (beQuietUntil > DateTime.Now && !_connectionOptions.ignoreQuietMode) // Server requested us to be quiet for a bit.
                    {
                        if (e.RequestBehavior == LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE)
                        {
                            e.Discard = true; // It's a queued command. We might accrue a LOT of queued commands over the beQuietUntil timespan (10 minutes default). Let's just discard them so we don't spam them all once we can send again.
                        }
                        else
                        {
                            e.Cancel = true; // Just try again later.
                        }
                    }
                    else
                    {
                        client.ExecuteCommand(e.Command);
                    }
                }
                else
                {
                    // If there is more than 5 unacked commands, let's just chill.
                    // This may happen due to bad connections.
                    // Benefit is, this way we may send less overall if the connection is bad, because
                    // Leakybucketrequester overwrites former commands with later ones of the same type.
                    // It should also allow a lower delay when sending commands because otherwise the queue
                    // may fill up to a crazy degree.
                    e.Cancel = true;
                }
            } else
            {
                e.Cancel = true;
            }
        }

        ~Connection()
        {
            CloseDown();
        }

        bool closedDown = false;
        Mutex closeDownMutex = new Mutex();

        public void CloseDown()
        {
            lock (closeDownMutex)
            {
                if (closedDown) return;
                closedDown = true;
                foreach (CancellationTokenSource backgroundTask in backgroundTasks)
                {
                    backgroundTask.Cancel();
                }
                _connectionOptions.PropertyChanged -= _connectionOptions_PropertyChanged;
                disconnect();
                leakyBucketRequester.Stop();
                //leakyBucketRequester = null;
            }
        }

        private string ip;
        private ProtocolVersion protocol;

        private void afterConnect()
        {
            Status = client.Status;
            infoPool.MapName = client.ServerInfo.MapName;
            if (!mohMode)
            {
                infoPool.teamInfo[(int)Team.Red].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
                infoPool.ScoreRed = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
                infoPool.teamInfo[(int)Team.Blue].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
                infoPool.ScoreBlue = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
                EvaluateFlagStatus(client.GetMappedConfigstring(ClientGame.Configstring.FlagStatus));
            }
        }

        private async Task<bool> createConnection( string ipA, ProtocolVersion protocolA,int timeOut = 30000)
        {
            if (closedDown) return false;

            trulyDisconnected = false;

            ip = ipA;
            protocol = protocolA;

            IClientHandler handler = null;
            if(protocol == ProtocolVersion.Protocol15)
            {
                handler = new JOClientHandler(ProtocolVersion.Protocol15, ClientVersion.JO_v1_02);
            } else if(protocol == ProtocolVersion.Protocol16)
            {
                handler = new JOClientHandler(ProtocolVersion.Protocol16, ClientVersion.JO_v1_04);
            } else if(protocol == ProtocolVersion.Protocol26)
            {
                handler = new JAClientHandler(ProtocolVersion.Protocol26, ClientVersion.JA_v1_01);
            } else if(protocol >= ProtocolVersion.Protocol6 && protocol <= ProtocolVersion.Protocol8 || protocol == ProtocolVersion.Protocol17) // TODO support protocols 15 and 16 for moh too? Or useless?
            {
                handler = new MOHClientHandler(protocol, ClientVersion.MOH);
            } else
            {
                serverWindow.addToLog($"ERROR: Tried to create connection using protocol {protocol}. Not supported.",true);
                return false;
            }
            string nwhEngine = Helpers.cachedFileRead("nwhEngine.txt");
            client = new Client(handler) { GhostPeer = this.GhostPeer,NWHEngine= nwhEngine }; // Todo make more flexible

            client.SetExtraDemoMetaData(_connectionOptions.extraDemoMetaParsed);

            //client.Name = "Padawan";
            client.Name = _connectionOptions.userInfoName == null ? "Padawan" : _connectionOptions.userInfoName;
            if (jkaMode) // TODO Detect mods and proceed accordingly
            {
                CheckSumFile[] checkSumFiles = new CheckSumFile[]{
                    new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejka/assets0.hl")},
                    new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejka/assets1.hl")},
                    new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejka/assets2.hl")},
                    new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejka/assets3.hl"),hasCgame=true,hasUI=true},
                }; 
                client.SetAssetChecksumFiles(checkSumFiles);
            }
            else
            {
                CheckSumFile[] checkSumFiles = null;
                // TODO Fix this if we ever allow connecting to 1.03/1.04
                if (protocol == ProtocolVersion.Protocol15) // JK2 1.02 
                {
                    checkSumFiles = new CheckSumFile[]{
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets0.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets1.hl"),hasCgame=true,hasUI=true},
                    };
                }
                else if ( protocol == ProtocolVersion.Protocol16) // JK2 1.04
                {
                    checkSumFiles = new CheckSumFile[]{
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets0.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets1.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets2.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets5.hl"),hasCgame=true,hasUI=true},
                    };
                } else // JK2 1.03 // TODO Detect this properly here.
                {
                    checkSumFiles = new CheckSumFile[]{
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets0.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets1.hl")},
                        new CheckSumFile(){ headerLongData = Helpers.GetResourceData("hl/basejk2/assets2.hl"),hasCgame=true,hasUI=true},
                    };
                } 
                client.SetAssetChecksumFiles(checkSumFiles);
            }

            if(password != null)
            {
                client.Password = password;
            }

            client.ServerCommandExecuted += ServerCommandExecuted;
            client.ServerInfoChanged += Connection_ServerInfoChanged;
            client.SnapshotParsed += Client_SnapshotParsed;
            client.EntityEvent += Client_EntityEvent;
            client.Disconnected += Client_Disconnected;
            client.UserCommandGenerated += Client_UserCommandGenerated;
            client.DebugEventHappened += Client_DebugEventHappened;
            client.InternalTaskStarted += Client_InternalTaskStarted;
            client.ErrorMessageCreated += Client_ErrorMessageCreated;
            client.InternalCommandCreated += Client_InternalCommandCreated;
            clientStatistics = client.Stats;
            Status = client.Status;
            
            client.Start(ExceptionCallback);
            Status = client.Status;

            try
            {

                //Task connectTask = client.Connect(ip, protocol);
                Task connectTask = client.Connect(ip);
                bool didConnect = false;
                await TaskManager.TaskRun(()=> {
                    try
                    {

                        didConnect = connectTask.Wait(timeOut);
                    } catch(TaskCanceledException e)
                    {
                        // Who cares.
                        didConnect = false;
                    }
                },$"Connection Connecter ({ip},{serverWindow.ServerName})");
                if (!didConnect)
                {
                    Status = client.Status;
                    serverWindow.addToLog($"Failed to create connection. Timeout after {timeOut} milliseconds. May still connect who knows.", true);
                    connectTask.ContinueWith((a)=> {
                        Status = client.Status;
                        if (shouldBeRecordingADemo)
                        {
                            serverWindow.addToLog("createConnection: Attempting to start/resume demo recording after delayed connect. (shouldBeRecordingADemo = true)");
                            startDemoRecord();
                        }
                        afterConnect();
                    },TaskContinuationOptions.NotOnCanceled);
                    return false;
                } 

            } catch(Exception e)
            {
                Status = client.Status;
                serverWindow.addToLog("Failed to create connection: "+e.ToString(),true);
                return false;
            }
            Status = client.Status;
            if (shouldBeRecordingADemo)
            {
                serverWindow.addToLog("createConnection: Attempting to start/resume demo recording. (shouldBeRecordingADemo = true)");
                startDemoRecord();
            }
            afterConnect();

            serverWindow.addToLog("New connection created.");
            return true;
        }

        private void Client_InternalCommandCreated(object sender, InternalCommandCreatedEventArgs e)
        {
            // We wanna integrate internal commands here with our nice leaky bucket flood protection
            leakyBucketRequester?.requestExecution(e.command,RequestCategory.JKCLIENTINTERNAL, 10, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
            e.handledExternally = true;
        }

        private void Client_ErrorMessageCreated(object sender, ErrorMessageEventArgs e)
        {
            serverWindow.addToLog($"JKClient error message: {e.errorMessage}; detail length: {(e.errorMessageDetail is null ? 0 : e.errorMessageDetail.Length)} characters. Detail logged to jkClientErrors.log",true);
            Helpers.logToSpecificDebugFile(new string[] { DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss G\\MTzzz]"), serverWindow.ServerName, serverWindow.netAddress?.ToString(), e.errorMessage, e.errorMessageDetail, e.possibleRelatedMessage.debugMessage, (e.possibleRelatedMessage != null && e.possibleRelatedMessage.Data != null) ? $"Error contained possibly related message, dumping into jkClientErrorMessageDump.bin":null }, "jkClientErrors.log", true);
            if(e.possibleRelatedMessage != null && e.possibleRelatedMessage.Data != null)
            {
                Helpers.logToSpecificDebugFile(e.possibleRelatedMessage.Data, "jkClientErrorMessageDump.bin", false);
            }
        }

        private void Client_InternalTaskStarted(object sender, in Task task, string description)
        {
            TaskManager.RegisterTask(task, $"JKClient (Connection {serverWindow.netAddress}, {serverWindow.ServerName}): {description}");
        }

        private void Client_DebugEventHappened(object sender, object e)
        {
            if(e is ConfigStringMismatch)
            {
                ConfigStringMismatch info = (ConfigStringMismatch)e;
                serverWindow.addToLog($"DEBUG: Config string mismatch: \"{info.intendedString}\" became \"{info.actualString}\"",true);
                TaskManager.TaskRun(()=> {
                    MemoryStream ms = new MemoryStream();
                    ms.Write(Encoding.UTF8.GetBytes($"{info.intendedString}\n{info.actualString}\n"));
                    if(info.oldGsStringData != null)
                    {
                        ms.Write(info.oldGsStringData);
                        ms.Write(Encoding.UTF8.GetBytes($"\n"));
                    }
                    if(info.newGsStringData != null)
                    {
                        ms.Write(info.newGsStringData);
                        ms.Write(Encoding.UTF8.GetBytes($"\n"));
                    }
                    Helpers.logToSpecificDebugFile(ms.ToArray(),"configStringMismatch.data");
                },$"Configstring Mismatch Logger ({ip},{serverWindow.ServerName})");
            } else if(e is NetDebug)
            {
                NetDebug nb = (NetDebug)e;
                Helpers.logToSpecificDebugFile(new string[] {nb.debugString },"netDebug.log",true);
            }
        }

        DateTime lastForcedActivity = DateTime.Now;

        Int64 queuedButtonPress = 0;
        public void QueueSingleButtonPress(Int64 btn)
        {
            queuedButtonPress |= btn;
        }

        Queue<Int64> queuedButtonPresses = new Queue<Int64>();
        Int64 lastDequeuedAppliedButtonPress = 0;
        DateTime lastAppliedQueueButtonPress = DateTime.Now;
        public void QueueButtonPress(Int64 btn)
        {
            lock (queuedButtonPresses)
            {
                queuedButtonPresses.Enqueue(btn);
            }
        }

        public enum DurationButtonPressType
        {
            NONE,
            ALTERNATE,
            KEEPPRESSED
        }
        Int64 durationButtonPress = 0;
        bool durationButtonPressLastFrameActive = false;
        DurationButtonPressType durationButtonPressType = DurationButtonPressType.ALTERNATE;
        DateTime lastAppliedDurationButtonPress = DateTime.Now;
        DateTime durationButtonPressUntil = DateTime.Now;
        public void SetDurationButtonPress(Int64 btn, int milliseconds, DurationButtonPressType type = DurationButtonPressType.ALTERNATE)
        {
            durationButtonPressType = type;
            durationButtonPress = btn;
            durationButtonPressUntil = DateTime.Now + new TimeSpan(0,0,0,0,milliseconds);
        }

        enum FakeButton : Int64
        {
            Jump = 1L << 32,
            Crouch = 1L << 33
        }


        int doClicks = 0;
        bool lastWasClick = false;

        // We relay this so any potential watchers can latch on to this and do their own modifications if they want to.
        // It also means we don't have to have watchers subscribe directly to the client because then that would break
        // when we get disconnected/reconnected etc.
        private void Client_UserCommandGenerated(object sender, ref UserCommand modifiableCommand, in UserCommand previousCommand, ref List<UserCommand> insertCommands)
        {

            // If we haven't gotten any response from the server in the last 10 seconds or so, stop doing any of these. 
            // Because generating these commands can force the client to send usercommands at 142-ish fps and if the server went down or something,
            // we don't wanna spam it.
            if ((DateTime.Now-lastSnapshotParsedOrServerInfoChange).TotalSeconds > 10)
            {
                return;
            }

            if (durationButtonPress > 0) // For example in MOH not every button press is processed for spectator change. Instead button presses are processed once per server frame. So the only thing we can do to speed things up is to just alternate the button for a duration that in combination with a known sv_fps will give us the amount of changes we need. This is not a precise science or anything and kinda fucked.
            {
                int durationButtonPressRealButtons = (int)(durationButtonPress & ((1L << 31) - 1)); // Only bits 0 to 30. We reserve higher ones for fake buttons like jump.
                if (DateTime.Now > durationButtonPressUntil)
                {
                    modifiableCommand.Buttons &= ~durationButtonPressRealButtons;
                    if ((durationButtonPress & (Int64)FakeButton.Jump) > 0 || (durationButtonPress & (Int64)FakeButton.Crouch) > 0)
                    {
                        modifiableCommand.Upmove = 0;
                    }
                    durationButtonPress = 0;
                    durationButtonPressLastFrameActive = false;
                    durationButtonPressType = DurationButtonPressType.NONE;
                } else
                {
                    if (durationButtonPressLastFrameActive && durationButtonPressType == DurationButtonPressType.ALTERNATE)
                    {
                        modifiableCommand.Buttons &= ~durationButtonPressRealButtons;
                        if ((durationButtonPress & (Int64)FakeButton.Jump) > 0 || (durationButtonPress & (Int64)FakeButton.Crouch) > 0)
                        {
                            modifiableCommand.Upmove = 0;
                        }
                    } else
                    {
                        modifiableCommand.Buttons |= durationButtonPressRealButtons;
                        if ((durationButtonPress & (Int64)FakeButton.Jump) > 0 || (durationButtonPress & (Int64)FakeButton.Crouch) > 0)
                        {
                            modifiableCommand.Upmove = (durationButtonPress & (Int64)FakeButton.Jump) > 0 ? (sbyte)127 : (sbyte)-128;
                        }
                    }
                    durationButtonPressLastFrameActive = !durationButtonPressLastFrameActive;
                }
                lastAppliedDurationButtonPress = DateTime.Now;
            }
            else if (queuedButtonPress > 0)
            {
                int queuedButtonPressRealButtons = (int)(queuedButtonPress & ((1L << 31) - 1)); // Only bits 0 to 30. We reserve higher ones for fake buttons like jump.
                modifiableCommand.Buttons |= queuedButtonPressRealButtons;
                if ((queuedButtonPress & (Int64)FakeButton.Jump) > 0 || (queuedButtonPress & (Int64)FakeButton.Crouch) > 0)
                {
                    modifiableCommand.Upmove = (queuedButtonPress & (Int64)FakeButton.Jump) > 0 ? (sbyte)127 : (sbyte)-128;
                }
                queuedButtonPress = 0;
            } else
            {
                lock (queuedButtonPresses)
                {
                    int previousServerTime = previousCommand.ServerTime;
                    while (queuedButtonPresses.Count > 0 && previousServerTime < modifiableCommand.ServerTime)
                    {
                        Int64 newCmd = queuedButtonPresses.Peek();
                        if ((lastDequeuedAppliedButtonPress & newCmd) > 0) // We have overlap between last pressed buttons and pressed buttons this round. Insert an empty no-buttons-pressed packet, else it will just count as a single button press
                        {
                            newCmd = 0;
                        } else
                        {
                            queuedButtonPresses.Dequeue();
                        }

                        int cmdRealButtons = (int)(newCmd & ((1L << 31) - 1)); // Only bits 0 to 30. We reserve higher ones for fake buttons like jump.
                        sbyte upMove = 0;
                        if ((newCmd & (Int64)FakeButton.Jump) > 0 || (newCmd & (Int64)FakeButton.Crouch) > 0)
                        {
                            upMove = (newCmd & (Int64)FakeButton.Jump) > 0 ? (sbyte)127 : (sbyte)-128;
                        }

                        int newServerTime = previousServerTime + 1;
                        if (newServerTime == modifiableCommand.ServerTime /*|| (queuedButtonPresses.Count == 0 && newServerTime < modifiableCommand.ServerTime)*/)
                        {
                            modifiableCommand.Buttons |= cmdRealButtons;
                            modifiableCommand.Upmove = upMove;
                        } else if (newServerTime < modifiableCommand.ServerTime)
                        {
                            insertCommands.Add(new UserCommand() { Buttons = cmdRealButtons, ServerTime = newServerTime,Upmove= upMove });
                        } else
                        {
                            // Shouldn't happen
                            Debug.WriteLine("queuedButtonPresses processing: Weird anomaly with serverTime");
                        }
                        lastDequeuedAppliedButtonPress = newCmd;
                        previousServerTime = newServerTime;
                        lastAppliedQueueButtonPress = DateTime.Now;
                    }
                    
                }
            }
            if (amNotInSpec)
            {
                DoSillyThings(ref modifiableCommand, in previousCommand);
            }            
            else
            {
                if ((DateTime.Now - lastForcedActivity).TotalMilliseconds > 60000) // Avoid getting inactivity dropped, so just send a single forward move once a minute.
                {
                    modifiableCommand.ForwardMove = 127;
                    if (mohMode)
                    {
                        if (!mohExpansion)
                        {
                            modifiableCommand.Upmove = 127; // In expansions, upmove changes who we follow.
                        }
                        modifiableCommand.Buttons |= (int)UserCommand.Button.MouseMOH;
                    }

                    lastForcedActivity = DateTime.Now;
                }
                else if (weAreSpectatorSlowFalling)
                {
                    // if we are falling down VEEEERY slowly (spectator mode thing), accelerate it so we reach the bottom quick and stop wasting net bandwidth
                    modifiableCommand.Upmove = -127; // crouch lets us go down real quick
                }
            }
            if(this.CameraOperator == null)
            {
                if(doClicks > 0 && lastWasClick == false)
                {
                    doClicks--;
                    lastWasClick = true;
                    modifiableCommand.Buttons |= (int)UserCommand.Button.Attack;
                    modifiableCommand.Buttons |= (int)UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
                }
                else
                {
                    lastWasClick = false;
                }
            }
            OnClientUserCommandGenerated(ref modifiableCommand, in previousCommand, ref insertCommands);
        }

        int reconnectTriesCount = 0;
        const int reconnectMaxTries = 10;

        int DisconnectCallbackRecursion = 0;
        const int DisconnectCallbackRecursionLimit = 10;

        public async Task<bool> hardReconnect()
        {
            Status = client.Status;
            bool success = false;
            while (success == false)
            {
                int delay = 1000 + (int)(1000 * Math.Pow(2, reconnectTriesCount));
                System.Threading.Thread.Sleep(delay); // The more retries fail, the larger the delay between tries grows.
                serverWindow.addToLog($"Reconnect try {reconnectTriesCount+1}. Delay {delay} ms.");
                if (reconnectTriesCount >= reconnectMaxTries)
                {
                    serverWindow.addToLog($"Giving up on reconnect after {reconnectTriesCount} tries.", true);
                    break;
                }
                if (client.Status == ConnectionStatus.Active) // Don't try to reconnect if we somehow managed to already reconnect in some other way.
                {
                    break;
                }
                Status = client.Status;
                success = await Reconnect();
                Status = client.Status;
                if (!success)
                {
                    reconnectTriesCount++;
                } else
                {
                    reconnectTriesCount = 0;
                }
            }
            reconnectTriesCount = 0;
            Status = client.Status;
            return success;
        }

        public async Task<bool> Reconnect()
        {
            disconnect();
            return await createConnection(ip, protocol);
        }

        private async void Client_Disconnected(object sender, EventArgs e)
        {
            if (DisconnectCallbackRecursion++ > DisconnectCallbackRecursionLimit)
            {
                serverWindow.addToLog("[Client_Disconnected] Hit Disconnect recursion limit trying to restart the connection. Giving up.", true);
                return;
            }

            serverWindow.addToLog("Involuntary disconnect for some reason.", true);
            Status = client.Status;

            if (isRecordingADemo)
            {
                wasRecordingADemo = true;
            }
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord(true);
            }

            // Reconnect
            System.Threading.Thread.Sleep(1000);
            serverWindow.addToLog("Attempting to reconnect.");

            //client.Start(ExceptionCallback); // I think that's only necessary once?
            //Status = client.Status;

            /*await client.Connect(ip, protocol); // TODO This can get cancelled. In that case,  handle it somehow.
             */
            // Be safe and just reset everything
            if (await hardReconnect())
            {
                serverWindow.addToLog("Reconnected.");

                if (wasRecordingADemo || shouldBeRecordingADemo)
                {
                    serverWindow.addToLog("Attempting to resume demo recording.");
                    startDemoRecord();
                }
            }
            DisconnectCallbackRecursion--;

        }

        bool wasRecordingADemo = false;
        bool shouldBeRecordingADemo = false;

        // Client crashed for some reason
        private async Task ExceptionCallback(JKClientException exception)
        {
            if (DisconnectCallbackRecursion++ > DisconnectCallbackRecursionLimit)
            {
                serverWindow.addToLog("[ExceptionCallback] Hit Disconnect recursion limit trying to restart the connection. Giving up.", true);
                return;
            }
            serverWindow.addToLog("JKClient crashed: " + exception.ToString(),true);
            Debug.WriteLine(exception);

            if (isRecordingADemo)
            {
                wasRecordingADemo = true;
            }
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord(true);
            }

            if (closedDown) return;

            // Reconnect
            System.Threading.Thread.Sleep(1000);
            /*serverWindow.addToLog("Attempting to restart.");

            client.Start(ExceptionCallback); // I think that's only necessary once?
            Status = client.Status;

            serverWindow.addToLog("Attempting to reconnect.");
            await client.Connect(ip, protocol);
            Status = client.Status;*/

            serverWindow.addToLog("Attempting to reconnect.");

            // Be safe and just reset everything
            if (await hardReconnect())
            {
                serverWindow.addToLog("Reconnected.");

                if (wasRecordingADemo || shouldBeRecordingADemo)
                {
                    serverWindow.addToLog("Attempting to resume demo recording.");
                    startDemoRecord();
                }
            }
            DisconnectCallbackRecursion--;
        }

        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;

        int lastEventSnapshotNumber = 0;
        Dictionary<int, Vector3> thisSnapshotObituaryVictims = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> thisSnapshotObituaryAttackers = new Dictionary<int, Vector3>();
        private unsafe void Client_EntityEvent(object sender, EntityEventArgs e)
        {
            int snapshotNumber, serverTime;
            ((IJKClientImport)client).GetCurrentSnapshotNumber(out snapshotNumber, out serverTime);

            if(snapshotNumber != lastEventSnapshotNumber)
            {
                thisSnapshotObituaryVictims.Clear();
                thisSnapshotObituaryAttackers.Clear();
            }

            if (e.EventType == ClientGame.EntityEvent.Obituary) // TODO Fix it up for JKA
            {
                // TODO Important. See if we can correlate death events to ctf frag events. That way we could know where
                //  the flag carrier was killed and thus where the flag is
                // We know the death event comes first. If we just pass the snapshotnumber, we can correlate them.
                // Todo do more elaborate logging. Death method etc. Detect multikills maybe
                int target = e.Entity.CurrentState.OtherEntityNum;
                int attacker = e.Entity.CurrentState.OtherEntityNum2;

                bool targetWasFlagCarrier = false;
                foreach (TeamInfo teamInfo in infoPool.teamInfo)
                {
                    if(teamInfo.reliableFlagCarrierTracker.getFlagCarrier() == target)
                    {
                        targetWasFlagCarrier = true;
                        break;
                    }
                }

                MeansOfDeath mod = (MeansOfDeath)e.Entity.CurrentState.EventParm;

                if (attacker >= 0 && attacker < client.ClientHandler.MaxClients && attacker != target) // Kill tracking, only do on one connection to keep things consistent.
                {

                    lock (infoPool.killTrackers) { // Just in case unlucky timing and mainchatconnection changes :) 
                        
                        if(mod == MeansOfDeath.MOD_SABER)
                        {
                            if (entityOrPSVisible[attacker])
                            {

                                int saberMoveAttacker = saberMove[attacker];
                                SaberMovesGeneral generalized = RandomArraysAndStuff.GeneralizeSaberMove(saberMoveAttacker,jkaMode);

                                //string killType = Enum.GetName(typeof(SaberMovesGeneral), generalized);
                                string killType = RandomArraysAndStuff.saberMoveNamesGeneral.ContainsKey(generalized) ? RandomArraysAndStuff.saberMoveNamesGeneral[generalized] : "WEIRDSABER";
                                if (killType.StartsWith("_"))
                                {
                                    killType = killType.Substring(1);
                                }
                                if(killType == "")
                                {
                                    switch (saberStyle[attacker]) {
                                        case 1:
                                            killType = "BLUE";
                                            break;
                                        case 2:
                                            killType = "YELLOW";
                                            break;
                                        case 3:
                                            killType = "RED";
                                            break;
                                        default:
                                            killType = "SABER";
                                            break;
                                    }

                                }
                                UInt64 killHash = e.Entity.CurrentState.GetKillHash(infoPool);
                                infoPool.playerInfo[attacker].chatCommandTrackingStuff.TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                                infoPool.playerInfo[attacker].chatCommandTrackingStuffThisGame.TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                                infoPool.killTrackers[attacker, target].TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                                infoPool.killTrackersThisGame[attacker, target].TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                            }
                        } else
                        {
                            string killType = Enum.GetName(typeof(MeansOfDeath), mod);
                            if (killType == null) {
                                killType = "WEIRD_UNKNOWN";
                            } else if (killType.StartsWith("MOD_"))
                            {
                                killType = killType.Substring(4);
                            }
                            UInt64 killHash = e.Entity.CurrentState.GetKillHash(infoPool);
                            infoPool.playerInfo[attacker].chatCommandTrackingStuff.TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                            infoPool.playerInfo[attacker].chatCommandTrackingStuffThisGame.TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                            infoPool.killTrackers[attacker, target].TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                            infoPool.killTrackersThisGame[attacker, target].TrackKill(killType, killHash, targetWasFlagCarrier); // This avoids dupes automatically
                        }

                        if (this.IsMainChatConnection) {  // Avoid dupes.

                            if (targetWasFlagCarrier)
                            {
                               // infoPool.playerInfo[attacker].chatCommandTrackingStuff.returns++; // tracking elsewhere already
                                infoPool.playerInfo[target].chatCommandTrackingStuff.returned++;
                                infoPool.playerInfo[target].chatCommandTrackingStuffThisGame.returned++;
                                infoPool.killTrackers[attacker, target].returns++;
                                infoPool.killTrackersThisGame[attacker, target].returns++;
                            }
                            infoPool.playerInfo[attacker].chatCommandTrackingStuff.totalKills++;
                            infoPool.playerInfo[target].chatCommandTrackingStuff.totalDeaths++;
                            infoPool.playerInfo[attacker].chatCommandTrackingStuffThisGame.totalKills++;
                            infoPool.playerInfo[target].chatCommandTrackingStuffThisGame.totalDeaths++;
                            infoPool.killTrackers[attacker, target].kills++;
                            infoPool.killTrackers[attacker, target].lastKillTime = DateTime.Now;
                            infoPool.killTrackersThisGame[attacker, target].kills++;
                            infoPool.killTrackersThisGame[attacker, target].lastKillTime = DateTime.Now;

                            if ((DateTime.Now-infoPool.playerInfo[target].lastMovementDirChange).TotalSeconds < 10.0) // for purposes of elo, don't count kills on afk players
                            {
                                infoPool.ratingPeriodResults.AddResult(infoPool.playerInfo[attacker].chatCommandTrackingStuff.rating, infoPool.playerInfo[target].chatCommandTrackingStuff.rating);
                                infoPool.ratingPeriodResultsThisGame.AddResult(infoPool.playerInfo[attacker].chatCommandTrackingStuffThisGame.rating, infoPool.playerInfo[target].chatCommandTrackingStuffThisGame.rating);
                                if(infoPool.ratingPeriodResults.GetResultCount() >= 15)
                                {
                                    infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults);
                                } 
                                if(infoPool.ratingPeriodResultsThisGame.GetResultCount() >= 15)
                                {
                                    infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame);
                                }
                            }

                            bool killTrackersSynced = infoPool.killTrackers[attacker, target].trackingMatch && infoPool.killTrackers[target, attacker].trackingMatch && infoPool.killTrackers[attacker, target].trackedMatchKills == infoPool.killTrackers[target, attacker].trackedMatchDeaths && infoPool.killTrackers[attacker, target].trackedMatchDeaths == infoPool.killTrackers[target, attacker].trackedMatchKills;
                            if (infoPool.killTrackers[attacker, target].trackingMatch)
                            {
                                infoPool.killTrackers[attacker, target].trackedMatchKills++;
                                if(!killTrackersSynced && !_connectionOptions.silentMode) { 
                                    leakyBucketRequester.requestExecution($"tell {attacker} \"   ^7^0^7Match against {infoPool.playerInfo[target].name}^7^0^7: {infoPool.killTrackers[attacker, target].trackedMatchKills}-{infoPool.killTrackers[attacker, target].trackedMatchDeaths}\"",RequestCategory.KILLTRACKER,0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE,null,null);
                                }
                            }
                            if (infoPool.killTrackers[target, attacker].trackingMatch)
                            {
                                infoPool.killTrackers[target, attacker].trackedMatchDeaths++;
                                if (!killTrackersSynced && !_connectionOptions.silentMode)
                                {
                                    leakyBucketRequester.requestExecution($"tell {attacker} \"   ^7^0^7Match against {infoPool.playerInfo[target].name}^7^0^7: {infoPool.killTrackers[attacker, target].trackedMatchKills}-{infoPool.killTrackers[attacker, target].trackedMatchDeaths}\"", RequestCategory.KILLTRACKER, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null, null);
                                }
                            }
                            if(killTrackersSynced && !_connectionOptions.silentMode)
                            {
                                int smallerClientNum = Math.Min(attacker, target); // Keep the public kill tracker always in same order.
                                int biggerClientNum = Math.Max(attacker, target);
                                leakyBucketRequester.requestExecution($"say \"   ^7^0^7Match {infoPool.playerInfo[smallerClientNum].name} ^7^0^7vs. {infoPool.playerInfo[biggerClientNum].name}^7^0^7: {infoPool.killTrackers[smallerClientNum, biggerClientNum].trackedMatchKills}-{infoPool.killTrackers[smallerClientNum, biggerClientNum].trackedMatchDeaths}\"", RequestCategory.KILLTRACKER, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null, null);
                            }
                        }
                    }
                }

                ClientEntity copyOfEntity = e.Entity; // This is necessary in order to read the fixed float arrays. Don't ask why, idk.
                Vector3 locationOfDeath;
                locationOfDeath.X = copyOfEntity.CurrentState.Position.Base[0];
                locationOfDeath.Y = copyOfEntity.CurrentState.Position.Base[1];
                locationOfDeath.Z = copyOfEntity.CurrentState.Position.Base[2];
                if (target < 0 || target >= client.ClientHandler.MaxClients)
                {
                    serverWindow.addToLog("EntityEvent Obituary: value "+target+" is out of bounds.");
                    return;
                }

                // Was it the flag carrier?
                foreach (int teamToCheck in Enum.GetValues(typeof(Team)))
                {
                    if (infoPool.teamInfo[teamToCheck].lastFlagCarrierUpdate != null && infoPool.teamInfo[teamToCheck].lastFlagCarrier == target && infoPool.teamInfo[teamToCheck].lastFlagCarrierValid)
                    {
                        infoPool.teamInfo[teamToCheck].flagDroppedPosition = locationOfDeath;
                        infoPool.teamInfo[teamToCheck].lastFlagDroppedPositionUpdate = DateTime.Now;
                        // Remmeber flag carrier deaths so we can keep the camera nearby for a bit longer if it was a relevant manner of death (everything that's not suicide)
                        if(attacker != target) // If it was suicide we don't care
                        {
                            if (attacker < 0 || attacker >= client.ClientHandler.MaxClients)
                            {
                                infoPool.teamInfo[teamToCheck].lastFlagCarrierWorldDeath = DateTime.Now; // This could be unintentional so still funny and interesting
                            }
                            else  // was a normal frag
                            {
                                infoPool.teamInfo[teamToCheck].lastFlagCarrierFragged = DateTime.Now;
                            }
                        }
                        
                            
                    }
                }

                thisSnapshotObituaryVictims.Add(target, locationOfDeath);

                infoPool.playerInfo[target].IsAlive = false;
                infoPool.playerInfo[target].lastAliveStatusUpdated = DateTime.Now;
                infoPool.playerInfo[target].lastDeathPosition = locationOfDeath;
                infoPool.playerInfo[target].lastDeath = DateTime.Now;
                infoPool.playerInfo[target].position = locationOfDeath;
                infoPool.playerInfo[target].lastPositionUpdate = DateTime.Now;
                string targetName = infoPool.playerInfo[target].name;


                if (this.IsMainChatConnection && MeansOfDeath.MOD_FALLING == mod && attacker!=target && attacker>=0 && attacker< client.ClientHandler.MaxClients)
                {
                    infoPool.playerInfo[attacker].chatCommandTrackingStuff.doomkills++;
                    infoPool.playerInfo[attacker].chatCommandTrackingStuffThisGame.doomkills++;
                }

                string killString = null;
                bool generic = false;
                switch (mod)
                {
                    case MeansOfDeath.MOD_STUN_BATON:
                        killString = "stunned";
                        break;
                    case MeansOfDeath.MOD_MELEE:
                        killString = "beat down";
                        break;
                    case MeansOfDeath.MOD_SABER:
                        killString = "sabered";
                        break;
                    case MeansOfDeath.MOD_BRYAR_PISTOL:
                    case MeansOfDeath.MOD_BRYAR_PISTOL_ALT:
                    case MeansOfDeath.MOD_BLASTER:
                    case MeansOfDeath.MOD_BOWCASTER:
                    case MeansOfDeath.MOD_REPEATER:
                    case MeansOfDeath.MOD_REPEATER_ALT:
                    case MeansOfDeath.MOD_REPEATER_ALT_SPLASH:
                    case MeansOfDeath.MOD_DEMP2:
                    case MeansOfDeath.MOD_DEMP2_ALT:
                    case MeansOfDeath.MOD_FLECHETTE:
                    case MeansOfDeath.MOD_FLECHETTE_ALT_SPLASH:
                        killString = "shot";
                        generic = true;
                        break;
                    case MeansOfDeath.MOD_DISRUPTOR:
                    case MeansOfDeath.MOD_DISRUPTOR_SPLASH:
                    case MeansOfDeath.MOD_DISRUPTOR_SNIPER:
                        generic = true;
                        killString = "sniped";
                        break;
                    case MeansOfDeath.MOD_ROCKET:
                    case MeansOfDeath.MOD_ROCKET_SPLASH:
                    case MeansOfDeath.MOD_ROCKET_HOMING:
                    case MeansOfDeath.MOD_ROCKET_HOMING_SPLASH:
                        generic = true;
                        killString = "rocketed";
                        break;
                    case MeansOfDeath.MOD_THERMAL:
                    case MeansOfDeath.MOD_THERMAL_SPLASH:
                    case MeansOfDeath.MOD_DET_PACK_SPLASH:
                        generic = true;
                        killString = "detonated";
                        break;
                    case MeansOfDeath.MOD_TRIP_MINE_SPLASH:
                    case MeansOfDeath.MOD_TIMED_MINE_SPLASH:
                        generic = true;
                        killString = "tripped";
                        break;
                    case MeansOfDeath.MOD_FORCE_DARK:
                        killString = "annihilated";
                        break;
                    case MeansOfDeath.MOD_SENTRY:
                        killString = "sentry-killed";
                        break;
                    case MeansOfDeath.MOD_WATER:
                        killString = "drowned";
                        break;
                    case MeansOfDeath.MOD_SLIME:
                        killString = "slimed";
                        break;
                    case MeansOfDeath.MOD_LAVA:
                        killString = "lava-burned";
                        break;
                    case MeansOfDeath.MOD_CRUSH:
                        killString = "crushed";
                        break;
                    case MeansOfDeath.MOD_TELEFRAG:
                        killString = "admin-killed";
                        break;
                    case MeansOfDeath.MOD_FALLING:
                        killString = "doomed";
                        break;
                    case MeansOfDeath.MOD_SUICIDE:
                        killString = "anheroed";
                        break;
                    case MeansOfDeath.MOD_TARGET_LASER:
                        killString = "lasered";
                        break;
                    case MeansOfDeath.MOD_TRIGGER_HURT:
                        killString = "triggered";
                        break;
                    case MeansOfDeath.MOD_MAX:
                        break;
                    case MeansOfDeath.MOD_UNKNOWN:
                    default:
                        break;
                }

                if (attacker < 0 || attacker >= client.ClientHandler.MaxClients)
                {
                    serverWindow.addToLog(targetName + " was "+ (killString == null ? "killed" : killString) + (killString == null || generic ? " [" + mod.ToString() + "]" : ""));
                } else
                {
                    thisSnapshotObituaryAttackers.Add(attacker, locationOfDeath);
                    infoPool.playerInfo[attacker].position = locationOfDeath;
                    infoPool.playerInfo[attacker].lastPositionUpdate = DateTime.Now;
                    // Can we also set the setalive of the attacker here? he might have blown himself up too.
                    // Would his self blowup message come before or after this?
                    string attackerName = infoPool.playerInfo[attacker].name;
                    serverWindow.addToLog(attackerName + " "+(killString == null ? "killed" : killString)+" " +( (target==attacker)? "himself": targetName) + (killString == null || generic? " [" + mod.ToString() + "]" : ""));
                }
            } else if(e.EventType == ClientGame.EntityEvent.CtfMessage)
            {
                CtfMessageType messageType = (CtfMessageType)e.Entity.CurrentState.EventParm;
                int playerNum = e.Entity.CurrentState.TrickedEntityIndex;
                Team team = (Team)e.Entity.CurrentState.TrickedEntityIndex2;

                if (team != Team.Red && team != Team.Blue)
                {
                    // Some other team, weird.
                    return;
                }

                Team otherTeam = team == Team.Red ? Team.Blue : Team.Red;

                string teamAsString = "";
                string otherTeamAsString = "";
                switch (team)
                {
                    case Team.Blue:
                        teamAsString = "blue";
                        otherTeamAsString = "red";
                        break;
                    case Team.Red:
                        teamAsString = "red";
                        otherTeamAsString = "blue";
                        break;
                    default:break;
                }

                PlayerInfo pi = null;

                if(playerNum >= 0 && playerNum <= client.ClientHandler.MaxClients)
                {
                    pi = infoPool.playerInfo[playerNum];
                }

                // If it was picked up or generally status changed, and it was at base before, remember this as the last time it was at base.
                if (infoPool.teamInfo[(int)team].flag == FlagStatus.FLAG_ATBASE)
                {
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                }

                if (messageType == CtfMessageType.PlayerGotFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].lastFlagCarrier = playerNum;
                    infoPool.teamInfo[(int)team].lastFlagCarrierUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_TAKEN;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    serverWindow.addToLog(pi.name + " got the " + teamAsString + " flag.");

                } else if (messageType == CtfMessageType.FraggedFlagCarrier && pi != null)
                {
                    // Teams are inverted here because team is the team of the person who got killed
                    infoPool.teamInfo[(int)otherTeam].flag = FlagStatus.FLAG_DROPPED;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)otherTeam].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierFragged = DateTime.Now;
                    if ((client.Entities?[playerNum].CurrentValid).GetValueOrDefault(false)) // Player who did kill is currently visible!
                    {
                        // We know where the flag is!
                        Vector3 locationOfFrag;
                        locationOfFrag.X = (client.Entities?[playerNum].CurrentState.Position.Base[0]).GetValueOrDefault(0.0f);
                        locationOfFrag.Y = (client.Entities?[playerNum].CurrentState.Position.Base[1]).GetValueOrDefault(0.0f);
                        locationOfFrag.Z = (client.Entities?[playerNum].CurrentState.Position.Base[2]).GetValueOrDefault(0.0f);
                        infoPool.teamInfo[(int)otherTeam].flagDroppedPosition = locationOfFrag;
                        infoPool.teamInfo[(int)otherTeam].lastFlagDroppedPositionUpdate = DateTime.Now;
                    } else if (thisSnapshotObituaryAttackers.ContainsKey(playerNum))
                    {
                        // We remember the death message. It had a position. We can use that. :)
                        infoPool.teamInfo[(int)otherTeam].flagDroppedPosition = thisSnapshotObituaryAttackers[playerNum];
                        infoPool.teamInfo[(int)otherTeam].lastFlagDroppedPositionUpdate = DateTime.Now;
                    } 
                    serverWindow.addToLog(pi.name + " killed carrier of " + otherTeamAsString + " flag.");
                    if (this.IsMainChatConnection)
                    {
                        infoPool.playerInfo[playerNum].chatCommandTrackingStuff.returns++; // Our own tracking of rets in case server doesn't send them.
                        infoPool.playerInfo[playerNum].chatCommandTrackingStuffThisGame.returns++; // Our own tracking of rets in case server doesn't send them.
                    }

                } else if (messageType == CtfMessageType.FlagReturned)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(textInfo.ToTitleCase(teamAsString) + " flag was returned.");
                }
                else if (messageType == CtfMessageType.PlayerCapturedFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(pi.name + " captured the "+teamAsString+" flag.");
                }
                else if (messageType == CtfMessageType.PlayerReturnedFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(pi.name + " returned the " + teamAsString + " flag.");
                }

            }
            else if (e.EventType == ClientGame.EntityEvent.ForceDrained)
            {
                int targetNum = e.Entity.CurrentState.Owner;
                if(targetNum >= 0 && targetNum < client.ClientHandler.MaxClients)
                {
                    infoPool.playerInfo[targetNum].lastDrainedEvent = DateTime.Now;
                }
            }
            else if (e.EventType == ClientGame.EntityEvent.Jump)
            {
                if(e.Entity.CurrentState.Number == ClientNum)
                {
                    //jumpReleasedThisJump = false;
                    countFramesJumpReleasedThisJump = 0;
                }
            }
            // Todo: look into various sound events that are broarcast to everyone, also global item pickup,
            // then we immediately know who's carrying the flag
        }

        private void snapsEnforcementUpdate()
        {
            // Bot and Team info in MOH is completely unreliable so we don't do this nice fancy stuff there.
            if (!mohMode && snapsSettings != null)
            {
                client.AfkDropSnaps = snapsSettings.forceAFKSnapDrop;
                client.AfkDropSnapsMinFPS = snapsSettings.afkMaxSnaps;
                client.AfkDropSnapsMinFPSBots = snapsSettings.forceBotOnlySnaps ? snapsSettings.botOnlySnaps : 1000;
                if (snapsSettings.forceEmptySnaps && infoPool.NoActivePlayers)
                {
                    client.ClientForceSnaps = true;
                    client.DesiredSnaps = snapsSettings.emptySnaps;
                } else if (snapsSettings.forceBotOnlySnaps && (((DateTime.Now-infoPool.lastBotOnlyConfirmed)?.TotalMilliseconds).GetValueOrDefault(double.PositiveInfinity) < 10000 || infoPool.botOnlyGuaranteed) )
                {
                    client.ClientForceSnaps = true;
                    client.DesiredSnaps = snapsSettings.botOnlySnaps;
                } else
                {
                    client.ClientForceSnaps = false;
                    client.DesiredSnaps = 1000;
                }
            }
            else
            {
                client.ClientForceSnaps = false;
                client.DesiredSnaps = 1000;
            }
            client.PingAdjust = snapsSettings.pingAdjustActive ? snapsSettings.pingAdjust : 0;
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        static uint MOH_CPT_NormalizePlayerStateFlags_ver_6(uint flags)
        {
            uint normalizedFlags = 0;

            // Convert AA PlayerMove flags to SH/BT flags
            normalizedFlags |= flags & (1 << 0);
            for (int i = 2; i < 32; ++i)
            {
                if ((flags & (1 << (i + 2))) != 0)
                {
                    normalizedFlags |= (1U << i);
                }
            }

            // So that flags are normalized across modules
            return normalizedFlags;
        }
        static uint MOH_CPT_NormalizePlayerStateFlags_ver_15(uint flags)
        {
            return flags;
        }
        static uint MOH_CPT_NormalizePlayerStateFlags(uint flags, ProtocolVersion protocol)
        {
            if (protocol <= ProtocolVersion.Protocol8)
            {
                return MOH_CPT_NormalizePlayerStateFlags_ver_6(flags);
            }
            else
            {
                return MOH_CPT_NormalizePlayerStateFlags_ver_15(flags);
            }
        }
        const int PMF_CAMERA_VIEW_MOH = (1 << 7);
        const int PMF_SPECTATING_MOH = (1 << 2);

        int lastRequestedAlwaysFollowSpecClientNum = -1;
        DateTime[] clientsWhoDontWantTOrCannotoBeSpectated = new DateTime[64] { // Looool this is cringe xd
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), 
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
            DateTime.Now.AddYears(-1), DateTime.Now.AddYears(-1),
        };


        private void CommitRatings()
        {
            if (this.IsMainChatConnection)
            {
                if (infoPool.ratingPeriodResults.GetResultCount() > 0)
                {
                    infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults);
                }
                if (infoPool.ratingPeriodResultsThisGame.GetResultCount() > 0)
                {
                    infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame);
                }
            }
        }

        public bool[] entityOrPSVisible = new bool[Common.MaxGEntities];
        public int[] saberMove = new int[64];
        public int[] saberStyle = new int[64];
        public Vector3[] lastVelocity = new Vector3[64];
        public Vector3[] lastPosition = new Vector3[64];
        public int[] lastLegsAnim = new int[64];
        public float[] lastXYVelocity = new float[64];
        public bool[] playerHasFlag = new bool[64];

        private Vector3 delta_angles;
        private float baseSpeed = 0;
        public float Speed { get; private set; } = 0;
        private int saberDrawAnimLevel = -1;

        private Snapshot lastSnapshot = new Snapshot();
        private PlayerState lastPlayerState = new PlayerState();
        private int lastSnapNum = -1;

        const int EF_ALLIES_MOH = 0x00000080;       // su44: this player is in allies team
        const int EF_AXIS_MOH = 0x00000100;     // su44: this player is in axis team
        const int EF_ANY_TEAM_MOH = (EF_ALLIES_MOH | EF_AXIS_MOH);

        public DateTime lastMOHFollowChangeButtonPressQueued = DateTime.Now; // Last time we requested to watch a new player
        public DateTime lastMOHChangeToFollowModeCommandSent = DateTime.Now;

        public DateTime lastAnyMovementDirChange = DateTime.Now; // Last time the player position or angle changed

        public DateTime lastSnapshotParsedOrServerInfoChange = DateTime.Now;

        private bool weAreSpectatorSlowFalling = false; // if we are a spectator in PM_SPECTATOR mode and we are not on the ground, we will be floating and VEEEERY slowly falling down. this means we will get a snapshot every second with a fresh position/velocity value but no meaningful info otherwise, wasting space. let's just quickly fly to the ground to prevent that if needed.

        private bool wasIntermission = false;
        private unsafe void Client_SnapshotParsed(object sender, SnapshotParsedEventArgs e)
        {
            lastSnapshotParsedOrServerInfoChange = DateTime.Now;
            CurrentTimeSecondEven = (DateTime.Now.Second % 2) > 0; // So we can update some values once per second LOL.

            snapsEnforcementUpdate();

            SnapStatus.addDataPoint(client.SnapNum,client.ServerTime);
            OnPropertyChanged(new PropertyChangedEventArgs("SnapStatus"));

            infoPool.setGameTime(client.gameTime);
            //infoPool.isIntermission = client.IsInterMission;
            Snapshot snap = e.snap;
            int oldServerTime = lastSnapshot.ServerTime;
            lastSnapshot = snap;
            lastPlayerState = snap.PlayerState;
            lastSnapNum = e.snapNum;
            bool isIntermission = snap.PlayerState.PlayerMoveType == JKClient.PlayerMoveType.Intermission;
            bool changedToIntermission = isIntermission && !wasIntermission;
            wasIntermission = isIntermission;
            infoPool.isIntermission = isIntermission;
            this.Speed = e.snap.PlayerState.Speed;
            PlayerMoveType = snap.PlayerState.PlayerMoveType;

            if (isDuelMode && isIntermission)
            {
                doClicks = Math.Min(3, doClicks + 1);
            }

            if (isIntermission && this.IsMainChatConnection)
            {
                CommitRatings();
            }

            if(changedToIntermission && this.IsMainChatConnection && !_connectionOptions.silentMode)
            {
                string glicko2String = MakeGlicko2RatingsString(true,true);
                leakyBucketRequester.requestExecution($"say \"   ^7^0^7Top Glicko2: {glicko2String}\"", RequestCategory.AUTOPRINTSTATS, 5, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null, null);
            }

            SpectatedPlayer = client.playerStateClientNum; // Might technically need a playerstate parsed event but ig this will do?
            if (oldSpectatedPlayer != SpectatedPlayer)
            {
                lastSpectatedPlayerChange = DateTime.Now;
            }

            int[] snapEntityMapping = new int[Common.MaxGEntities];
            for(int i = 0; i < Common.MaxGEntities; i++)
            {
                snapEntityMapping[i] = -1;
            }
            for(int i = 0; i < e.snap.NumEntities; i++)
            {
                snapEntityMapping[e.snap.Entities[i].Number] = i;
            }
            entityOrPSVisible[snap.PlayerState.ClientNum] = true;
            saberMove[snap.PlayerState.ClientNum] = snap.PlayerState.SaberMove;
            saberStyle[snap.PlayerState.ClientNum] = snap.PlayerState.forceData.SaberAnimLevel;
            //ClientEntity[] entities = client.Entities;
            //if (entities == null)
            //{
            //    return;
            //}

            if (mohFreezeTagAllowsFrozenFollow && (DateTime.Now-mohFreezeTagAllowsFrozenFollowLastConfirmed).TotalMinutes > 5)
            {
                serverWindow.addToLog("MOH Freeze Tag detection: More than 5 minutes since we confirmed that server allows following frozen players. Resetting setting.");
                mohFreezeTagAllowsFrozenFollow = false;
            }

            int EFDeadFlag = jkaMode ? (int)JKAStuff.EntityFlags.EF_DEAD : (int)JOStuff.EntityFlags.EF_DEAD;
            int PWRedFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_REDFLAG : (int)JOStuff.ItemList.powerup_t.PW_REDFLAG;
            int PWBlueFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_BLUEFLAG : (int)JOStuff.ItemList.powerup_t.PW_BLUEFLAG;
            int ETTeam = jkaMode ? (int)JKAStuff.entityType_t.ET_TEAM : (int)JOStuff.entityType_t.ET_TEAM;
            int ETItem = jkaMode ? (int)JKAStuff.entityType_t.ET_ITEM : (int)JOStuff.entityType_t.ET_ITEM;
            if (mohMode)
            {
                EFDeadFlag = 0x00000200; // Ofc MOHAA has to be the most special one :)
                ETItem = 4; 
            }
            //int EFBounceHalf = jkaMode ? 0 : (int)JOStuff.EntityFlags.EF_BOUNCE_HALF; // ?!?!

            int knockDownLower = jkaMode ? -2 : 829; // TODO Adapt to 1.04 too? But why, its so different.
            int knockDownUpper = jkaMode ? -2 : 848;

            if (mohMode)
            {
                uint normalizePMFlags = MOH_CPT_NormalizePlayerStateFlags((uint)snap.PlayerState.PlayerMoveFlags, this.protocol);
                amNotInSpec = (normalizePMFlags & PMF_SPECTATING_MOH) == 0;
                weAreSpectatorSlowFalling = false; // idk if this is a thing in moh.
            } else
            {
                amNotInSpec = snap.PlayerState.ClientNum == client.clientNum && snap.PlayerState.PlayerMoveType != JKClient.PlayerMoveType.Spectator && snap.PlayerState.PlayerMoveType != JKClient.PlayerMoveType.Intermission; // Some servers (or duel mode) doesn't allow me to go spec. Do funny things then.
                //weAreSpectatorSlowFalling = snap.PlayerState.PlayerMoveType == JKClient.PlayerMoveType.Spectator && (snap.PlayerState.Velocity[0] != 0.0f || snap.PlayerState.Velocity[1] != 0.0f || snap.PlayerState.Velocity[2] != 0.0f); // doesnt work as our crouch method affects velocity or idk something something :/
                //weAreSpectatorSlowFalling = snap.PlayerState.PlayerMoveType == JKClient.PlayerMoveType.Spectator && (snap.PlayerState.Origin[0] != lastPosition[snap.PlayerState.ClientNum].X || snap.PlayerState.Origin[1] != lastPosition[snap.PlayerState.ClientNum].Y || snap.PlayerState.Origin[2] != lastPosition[snap.PlayerState.ClientNum].Z);
                weAreSpectatorSlowFalling = snap.PlayerState.PlayerMoveType == JKClient.PlayerMoveType.Spectator && (snap.PlayerState.Origin[2] < lastPosition[snap.PlayerState.ClientNum].Z);
            }

            bool teamChangesDetected = false;

            int visibleOtherPlayers = 0;

            Dictionary<int, Vector3> possiblyBlockedPlayers = new Dictionary<int, Vector3>();

            for (int i = 0; i < client.ClientHandler.MaxClients; i++)
            {

                bool wasVisibleLastFrame = entityOrPSVisible[i];

                bool oldKnockedDown = infoPool.playerInfo[i].knockedDown;
                
                int snapEntityNum = snapEntityMapping[i];
                if((snapEntityNum == -1 || mohMode) && i == snap.PlayerState.ClientNum)
                {
                    infoPool.playerInfo[i].IsAlive = snap.PlayerState.Stats[0] > 0; // We do this so that if a player respawns but isn't visible, we don't use his (useless) position
                    infoPool.playerInfo[i].lastAliveStatusUpdated = DateTime.Now;
                    if (
                        infoPool.playerInfo[i].movementDir != snap.PlayerState.MovementDirection
                        /*infoPool.playerInfo[i].position.X != snap.PlayerState.Origin[0] ||
                        infoPool.playerInfo[i].position.Y != snap.PlayerState.Origin[1]||
                        infoPool.playerInfo[i].position.Z != snap.PlayerState.Origin[2] ||
                        infoPool.playerInfo[i].angles.X != snap.PlayerState.ViewAngles[0] ||
                        infoPool.playerInfo[i].angles.Y != snap.PlayerState.ViewAngles[1] ||
                        infoPool.playerInfo[i].angles.Z != snap.PlayerState.ViewAngles[2] */
                    )
                    {
                        infoPool.playerInfo[i].lastMovementDirChange = DateTime.Now;
                        infoPool.playerInfo[i].consecutiveAfkMillisecondsCounter = 0;
                        lastAnyMovementDirChange = DateTime.Now;
                    } else if(wasVisibleLastFrame)
                    {
                        infoPool.playerInfo[i].consecutiveAfkMillisecondsCounter += (snap.ServerTime-oldServerTime);
                    }
                    infoPool.playerInfo[i].position.X = snap.PlayerState.Origin[0];
                    infoPool.playerInfo[i].position.Y = snap.PlayerState.Origin[1];
                    infoPool.playerInfo[i].position.Z = snap.PlayerState.Origin[2];
                    infoPool.playerInfo[i].velocity.X = snap.PlayerState.Velocity[0];
                    infoPool.playerInfo[i].velocity.Y = snap.PlayerState.Velocity[1];
                    infoPool.playerInfo[i].velocity.Z = snap.PlayerState.Velocity[2];
                    infoPool.playerInfo[i].angles.X = snap.PlayerState.ViewAngles[0];
                    infoPool.playerInfo[i].angles.Y = snap.PlayerState.ViewAngles[1];
                    infoPool.playerInfo[i].angles.Z = snap.PlayerState.ViewAngles[2];
                    infoPool.playerInfo[i].curWeapon = snap.PlayerState.Weapon;
                    infoPool.playerInfo[i].speed = snap.PlayerState.Speed;
                    infoPool.playerInfo[i].groundEntityNum = snap.PlayerState.GroundEntityNum;
                    infoPool.playerInfo[i].torsoAnim = snap.PlayerState.TorsoAnim;
                    infoPool.playerInfo[i].legsAnim = snap.PlayerState.LegsAnimation;
                    infoPool.playerInfo[i].duelInProgress = snap.PlayerState.DuelInProgress;
                    infoPool.playerInfo[i].saberMove = snap.PlayerState.SaberMove;
                    infoPool.playerInfo[i].forcePowersActive = snap.PlayerState.forceData.ForcePowersActive;
                    infoPool.playerInfo[i].movementDir = snap.PlayerState.MovementDirection;
                    this.saberDrawAnimLevel = snap.PlayerState.forceData.SaberDrawAnimLevel;
                    this.baseSpeed = snap.PlayerState.Basespeed;
                    this.delta_angles.X = Short2Angle(snap.PlayerState.DeltaAngles[0]);
                    this.delta_angles.Y = Short2Angle(snap.PlayerState.DeltaAngles[1]);
                    this.delta_angles.Z = Short2Angle(snap.PlayerState.DeltaAngles[2]);

                    

                    if (mohMode && !mohExpansion)
                    {
                        int playerTeam = snap.PlayerState.Stats[20];
                        switch (playerTeam)
                        {
                            default:
                            case 1:
                                playerTeam = (int)Team.Spectator;
                                break;
                            case 2:
                                playerTeam = (int)Team.Free;
                                break;
                            case 3: // Allies
                                playerTeam = (int)Team.Blue;
                                break;
                            case 4: // Axis
                                playerTeam = (int)Team.Red;
                                break;
                        }
                        infoPool.playerInfo[i].team = (Team)playerTeam;
                    }

                    infoPool.playerInfo[i].powerUps = 0;
                    for (int y = 0; y < Common.MaxPowerUps; y++)
                    {
                        if (snap.PlayerState.PowerUps[y] > 0)
                        {
                            infoPool.playerInfo[i].powerUps |= 1 << y;
                        }
                    }
                    infoPool.playerInfo[i].lastPositionUpdate = infoPool.playerInfo[i].lastFullPositionUpdate = DateTime.Now;

                    bool hasFlag = false;

                    if (((infoPool.playerInfo[i].powerUps & (1 << PWRedFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Red].reliableFlagCarrierTracker.setFlagCarrier(i,snap.ServerTime);
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                        hasFlag = true;
                    }
                    else if (((infoPool.playerInfo[i].powerUps & (1 << PWBlueFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Blue].reliableFlagCarrierTracker.setFlagCarrier(i, snap.ServerTime);
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                        hasFlag = true;
                    }

                    playerHasFlag[i] = hasFlag;

                    int legsAnim = snap.PlayerState.LegsAnimation & ~2048;
                    if (legsAnim != lastLegsAnim[i])
                    {
                        if (legsAnim >= 781 && legsAnim <= 784 && !(lastLegsAnim[i] >= 781 && lastLegsAnim[i] <= 784)) // Is in roll. TODO Make work with JKA and 1.04
                        {
                            infoPool.playerInfo[i].chatCommandTrackingStuff.rolls.Count(true);
                            infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rolls.Count(true);
                            if (hasFlag)
                            {
                                infoPool.playerInfo[i].chatCommandTrackingStuff.rollsWithFlag.Count(true);
                                infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rollsWithFlag.Count(true);
                            }
                        }
                    }
                    lastLegsAnim[i] = legsAnim;

                    float xyVelocity = (float)Math.Sqrt(snap.PlayerState.Velocity[0] * snap.PlayerState.Velocity[0] + snap.PlayerState.Velocity[1] * snap.PlayerState.Velocity[1]);
                    if (snap.PlayerState.GroundEntityNum == (Common.MaxGEntities - 1) && wasVisibleLastFrame && xyVelocity > lastXYVelocity[i])
                    {
                        // We accelerated in air!
                        // What keys did we use to accelerate?
                        int hereMovementDir = snap.PlayerState.MovementDirection;
                        if (hereMovementDir >= 0 && hereMovementDir < (int)MovementDir.CountDirs)
                        {
                            infoPool.playerInfo[i].chatCommandTrackingStuff.strafeStyleSamples[hereMovementDir]++;
                            infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.strafeStyleSamples[hereMovementDir]++;
                        }
                    }
                    else if (wasVisibleLastFrame && xyVelocity < lastXYVelocity[i] - 200)
                    {
                        possiblyBlockedPlayers.Add(i, Vector3.Normalize(lastVelocity[i] - new Vector3()
                        {
                            X = snap.PlayerState.Velocity[0],
                            Y = snap.PlayerState.Velocity[1],
                            Z = snap.PlayerState.Velocity[2]
                        })); // Save a guess of where the block came from
                    }
                    lastXYVelocity[i] = xyVelocity;
                    lastPosition[i].X = snap.PlayerState.Origin[0];
                    lastPosition[i].Y = snap.PlayerState.Origin[1];
                    lastPosition[i].Z = snap.PlayerState.Origin[2];
                    lastVelocity[i].X = snap.PlayerState.Velocity[0];
                    lastVelocity[i].Y = snap.PlayerState.Velocity[1];
                    lastVelocity[i].Z = snap.PlayerState.Velocity[2];

                    if (SpectatedPlayer.HasValue)
                    {
                        infoPool.lastConfirmedVisible[SpectatedPlayer.Value, i] = DateTime.Now;
                        entityOrPSVisible[i] = true;
                        saberMove[i] = snap.PlayerState.SaberMove;
                        saberStyle[i] = snap.PlayerState.forceData.SaberAnimLevel;
                    }

                    if (mohMode)
                    {
                        uint normalizePMFlags = MOH_CPT_NormalizePlayerStateFlags((uint)snap.PlayerState.PlayerMoveFlags, this.protocol);
                        this.mohSpectatorFreeFloat = (normalizePMFlags & PMF_CAMERA_VIEW_MOH) == 0;
                        if (!this.mohSpectatorFreeFloat)
                        {
                            lastMOHSpectatorNonFreeFloat = DateTime.Now;
                        }
                    }
                }
                else if (snapEntityNum != -1 /* entities[i].CurrentValid || entities[i].CurrentFilledFromPlayerState */) {

                    // TODO
                    // This isAlive thing sometimes evaluated wrongly in unpredictable ways. In one instance, it appears it might have 
                    // evaluated to false for a single frame, unless I mistraced the error and this isn't the source of the error at all.
                    // Weird thing is, EntityFlags was not being copied from PlayerState at all! So how come the value changed at all?! It doesn't really make sense.
                    if (mohMode && !mohExpansion && i < serverMaxClientsLimit)
                    {
                        if (!infoPool.playerInfo[i].infoValid)
                        {
                            teamChangesDetected = true;
                            serverWindow.addToLog($"Entity {i} exists but player {i}'s infoValid is false. Player name: {infoPool.playerInfo[i].name}. Setting to true.");
                        }
                        infoPool.playerInfo[i].infoValid = true;
                    }
                    bool deadFlagSet = (snap.Entities[snapEntityNum].EntityFlags & EFDeadFlag) > 0;
                    infoPool.playerInfo[i].IsAlive = !deadFlagSet; // We do this so that if a player respawns but isn't visible, we don't use his (useless) position
                    bool frozenStatus = mohFreezeTagDetected && snap.Entities[snapEntityNum].Solid==0;
                    if(infoPool.playerInfo[i].IsFrozen != frozenStatus)
                    {
                        serverWindow.addToLog($"MOH Freeze Tag detection: Player name \"{infoPool.playerInfo[i].name}\", clientnum {i} frozen status {frozenStatus} from solid property.", false, 0, 5);
                    }
                    infoPool.playerInfo[i].IsFrozen = frozenStatus;
                    infoPool.playerInfo[i].lastAliveStatusUpdated = DateTime.Now; // We do this so that if a player respawns but isn't visible, we don't use his (useless) position
                    if (
                        infoPool.playerInfo[i].movementDir != snap.Entities[snapEntityNum].Angles2[YAW]
                        /*infoPool.playerInfo[i].position.X != snap.Entities[snapEntityNum].Position.Base[0] ||
                        infoPool.playerInfo[i].position.Y != snap.Entities[snapEntityNum].Position.Base[1] ||
                        infoPool.playerInfo[i].position.Z != snap.Entities[snapEntityNum].Position.Base[2] ||
                        infoPool.playerInfo[i].velocity.X != snap.Entities[snapEntityNum].Position.Delta[0] ||
                        infoPool.playerInfo[i].velocity.Y != snap.Entities[snapEntityNum].Position.Delta[1] ||
                        infoPool.playerInfo[i].velocity.Z != snap.Entities[snapEntityNum].Position.Delta[2]*/
                    )
                    {
                        infoPool.playerInfo[i].lastMovementDirChange = DateTime.Now;
                        infoPool.playerInfo[i].consecutiveAfkMillisecondsCounter = 0;
                        lastAnyMovementDirChange = DateTime.Now;
                    } else if (wasVisibleLastFrame)
                    {
                        infoPool.playerInfo[i].consecutiveAfkMillisecondsCounter += (snap.ServerTime - oldServerTime);
                    }
                    infoPool.playerInfo[i].position.X = snap.Entities[snapEntityNum].Position.Base[0];
                    infoPool.playerInfo[i].position.Y = snap.Entities[snapEntityNum].Position.Base[1];
                    infoPool.playerInfo[i].position.Z = snap.Entities[snapEntityNum].Position.Base[2];
                    infoPool.playerInfo[i].velocity.X = snap.Entities[snapEntityNum].Position.Delta[0];
                    infoPool.playerInfo[i].velocity.Y = snap.Entities[snapEntityNum].Position.Delta[1];
                    infoPool.playerInfo[i].velocity.Z = snap.Entities[snapEntityNum].Position.Delta[2];
                    infoPool.playerInfo[i].angles.X = snap.Entities[snapEntityNum].AngularPosition.Base[0];
                    infoPool.playerInfo[i].angles.Y = snap.Entities[snapEntityNum].AngularPosition.Base[1];
                    infoPool.playerInfo[i].angles.Z = snap.Entities[snapEntityNum].AngularPosition.Base[2];
                    infoPool.playerInfo[i].curWeapon = snap.Entities[snapEntityNum].Weapon;
                    infoPool.playerInfo[i].speed = snap.Entities[snapEntityNum].Speed;
                    infoPool.playerInfo[i].groundEntityNum = snap.Entities[snapEntityNum].GroundEntityNum;
                    infoPool.playerInfo[i].torsoAnim = snap.Entities[snapEntityNum].TorsoAnimation;
                    infoPool.playerInfo[i].legsAnim = snap.Entities[snapEntityNum].LegsAnimation;
                    infoPool.playerInfo[i].duelInProgress = snap.Entities[snapEntityNum].Bolt1 == 1;
                    infoPool.playerInfo[i].saberMove = snap.Entities[snapEntityNum].SaberMove;
                    infoPool.playerInfo[i].forcePowersActive = snap.Entities[snapEntityNum].ForcePowersActive;
                    infoPool.playerInfo[i].powerUps = snap.Entities[snapEntityNum].Powerups; // 1/3 places where powerups is transmitted
                    infoPool.playerInfo[i].movementDir = (int)snap.Entities[snapEntityNum].Angles2[YAW]; // 1/3 places where powerups is transmitted
                    infoPool.playerInfo[i].lastPositionUpdate = infoPool.playerInfo[i].lastFullPositionUpdate = DateTime.Now;


                    if (mohMode && !mohExpansion)
                    {
                        Team entityTeam =  ((snap.Entities[snapEntityNum].EntityFlags & EF_ANY_TEAM_MOH) > 0) ? (((snap.Entities[snapEntityNum].EntityFlags & EF_AXIS_MOH) > 0) ? Team.Red : Team.Blue) : Team.Free;
                        if(currentGameType <= GameType.FFA)
                        {
                            entityTeam = Team.Free;
                        }
                        if (entityTeam != infoPool.playerInfo[i].team)
                        {
                            infoPool.playerInfo[i].team = entityTeam;
                            teamChangesDetected = true;
                        }
                    }

                    bool mohFollowingPlayerBaseMOH = mohMode && !mohExpansion  // MOH hack to find out who we are spectating.... lol
                        && snap.PlayerState.Origin[0] == snap.Entities[snapEntityNum].Position.Base[0]
                        && snap.PlayerState.Origin[1] == snap.Entities[snapEntityNum].Position.Base[1]
                        && snap.PlayerState.Origin[2] == snap.Entities[snapEntityNum].Position.Base[2];
                    bool mohFollowingPlayerExpansion = mohMode && mohExpansion  
                        && Vector3.Distance(
                        new Vector3() { X= snap.PlayerState.Origin[0], Y= snap.PlayerState.Origin[1], Z= snap.PlayerState.Origin[2] }, 
                        new Vector3() { X = snap.Entities[snapEntityNum].Position.Base[0], Y = snap.Entities[snapEntityNum].Position.Base[1], Z = snap.Entities[snapEntityNum].Position.Base[2] }
                        ) < 0.5f;

                    if (mohFollowingPlayerBaseMOH || mohFollowingPlayerExpansion )
                    {
                        uint normalizePMFlags = MOH_CPT_NormalizePlayerStateFlags((uint)snap.PlayerState.PlayerMoveFlags,this.protocol);
                        if((normalizePMFlags & PMF_CAMERA_VIEW_MOH) != 0 && (normalizePMFlags & PMF_SPECTATING_MOH) != 0)
                        {
                            SpectatedPlayer = i;

                            if (mohFreezeTagDetected)
                            {
                                if(i != oldSpectatedPlayer && infoPool.playerInfo[i].IsFrozen && frozenStatus && !deadFlagSet)
                                {
                                    if (!mohFreezeTagAllowsFrozenFollow)
                                    {
                                        serverWindow.addToLog("MOH Freeze Tag detection: Server seems to allow following frozen players.");
                                        mohFreezeTagAllowsFrozenFollow = true;
                                    }
                                    mohFreezeTagAllowsFrozenFollowLastConfirmed = DateTime.Now;
                                }
                            }
                        } 
                    }

                    bool hasFlag = false;

                    if(((infoPool.playerInfo[i].powerUps & (1 << PWRedFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Red].reliableFlagCarrierTracker.setFlagCarrier(i, snap.ServerTime);
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                        hasFlag = true;
                    } else if (((infoPool.playerInfo[i].powerUps & (1 << PWBlueFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Blue].reliableFlagCarrierTracker.setFlagCarrier(i, snap.ServerTime);
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                        hasFlag = true;
                    }

                    if (SpectatedPlayer.HasValue)
                    {
                        infoPool.lastConfirmedVisible[SpectatedPlayer.Value, i] = DateTime.Now;
                        entityOrPSVisible[i] = true;
                        saberMove[i] = snap.Entities[snapEntityNum].SaberMove;
                        saberStyle[i] = snap.Entities[snapEntityNum].FireFlag;
                    }

                    visibleOtherPlayers++;

                    playerHasFlag[i] = hasFlag;

                    int legsAnim = snap.Entities[snapEntityNum].LegsAnimation & ~2048;
                    if (legsAnim != lastLegsAnim[i])
                    {
                        if (legsAnim >= 781 && legsAnim <= 784 && !(lastLegsAnim[i] >= 781 && lastLegsAnim[i] <= 784)) // Is in roll. TODO Make work with JKA and 1.04
                        {
                            infoPool.playerInfo[i].chatCommandTrackingStuff.rolls.Count(true);
                            infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rolls.Count(true);
                            if (hasFlag)
                            {
                                infoPool.playerInfo[i].chatCommandTrackingStuff.rollsWithFlag.Count(true);
                                infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rollsWithFlag.Count(true);
                            }
                        }
                    }
                    lastLegsAnim[i] = legsAnim;


                    float xyVelocity = (float)Math.Sqrt(snap.Entities[snapEntityNum].Position.Delta[0] * snap.Entities[snapEntityNum].Position.Delta[0] + snap.Entities[snapEntityNum].Position.Delta[1] * snap.Entities[snapEntityNum].Position.Delta[1]);
                    if (snap.Entities[snapEntityNum].GroundEntityNum == (Common.MaxGEntities - 1) && wasVisibleLastFrame && xyVelocity > lastXYVelocity[i])
                    {
                        // We accelerated in air!
                        // What keys did we use to accelerate?
                        int hereMovementDir = (int)snap.Entities[snapEntityNum].Angles2[YAW];
                        if (hereMovementDir >= 0 && hereMovementDir < (int)MovementDir.CountDirs)
                        {
                            infoPool.playerInfo[i].chatCommandTrackingStuff.strafeStyleSamples[hereMovementDir]++;
                            infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.strafeStyleSamples[hereMovementDir]++;
                        }
                    }
                    else if (wasVisibleLastFrame && xyVelocity < lastXYVelocity[i] - 200)
                    {
                        possiblyBlockedPlayers.Add(i, Vector3.Normalize(lastVelocity[i] - new Vector3()
                        {
                            X = snap.Entities[snapEntityNum].Position.Delta[0],
                            Y = snap.Entities[snapEntityNum].Position.Delta[1],
                            Z = snap.Entities[snapEntityNum].Position.Delta[2]
                        })); // Save a guess of where the block came from
                    }
                    lastXYVelocity[i] = xyVelocity;
                    lastPosition[i].X = snap.Entities[snapEntityNum].Position.Base[0];
                    lastPosition[i].Y = snap.Entities[snapEntityNum].Position.Base[1];
                    lastPosition[i].Z = snap.Entities[snapEntityNum].Position.Base[2];
                    lastVelocity[i].X = snap.Entities[snapEntityNum].Position.Delta[0];
                    lastVelocity[i].Y = snap.Entities[snapEntityNum].Position.Delta[1];
                    lastVelocity[i].Z = snap.Entities[snapEntityNum].Position.Delta[2];

                } else
                {
                    //if(((DateTime.Now-infoPool.playerInfo[i].lastFullPositionUpdate)?.TotalMilliseconds).GetValueOrDefault(5000) > 200)
                    //{
                        // We have not gotten a position update on this player for at least 200 milliseconds anywhere. Confirm him as not visible.
                        // This is used as a helper value for afk detection. We cannot detect that someone is afk unless we know he's visible.
                        // WAIT. I have a better idea.
                        //infoPool.playerInfo[i].lastNotVisible = DateTime.Now;
                    //}
                    
                    if (SpectatedPlayer.HasValue)
                    {
                        infoPool.lastConfirmedInvisible[SpectatedPlayer.Value, i] = DateTime.Now;
                        entityOrPSVisible[i] = false;
                    }
                }

                if (infoPool.playerInfo[i].consecutiveAfkMillisecondsCounter > 20000 && infoPool.playerInfo[i].lastFullPositionUpdate.HasValue && (DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalMinutes < 10) // After 10 minutes afk confirmation expires
                {
                    // If we have 10 seconds worth of unchanged frames, we confirm this player as afk. TODO: This value will inevitably get distorted by multiple connections incrementing the value at the same time. Don't treat it as a perfect science..
                    // Originally I tried to do this with amount of frames without change, but it's foolish because frames don't have a fixed duration, especially with my packet ditching techs
                    infoPool.playerInfo[i].confirmedAfk = true;
                    //infoPool.playerInfo[i].timeConfirmedAfk = DateTime.Now;
                } else
                {
                    infoPool.playerInfo[i].confirmedAfk = false;
                }

                int currentLegsAnim = infoPool.playerInfo[i].legsAnim & ~2048;
                int currentTorsoAnim = infoPool.playerInfo[i].torsoAnim & ~2048;
                infoPool.playerInfo[i].knockedDown = (currentLegsAnim >= knockDownLower && currentLegsAnim <= knockDownUpper) || (currentTorsoAnim >= knockDownLower && currentTorsoAnim <= knockDownUpper);

                if (infoPool.playerInfo[i].knockedDown && !oldKnockedDown)
                {
                    infoPool.playerInfo[i].chatCommandTrackingStuff.falls++;
                    infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.falls++;
                }
            }

            infoPool.playerInfo[snap.PlayerState.ClientNum].VisiblePlayers.VisiblePlayers = (byte)visibleOtherPlayers;

            if (mohMode && oldSpectatedPlayer != SpectatedPlayer) // Since we have to derive this from entity positions in MOH, we have to do the check here.
            {
                lastSpectatedPlayerChange = DateTime.Now;
               

            }

            PlayerState currentPs = snap.PlayerState;
            if(currentPs.ClientNum >= 0 && currentPs.ClientNum < client.ClientHandler.MaxClients) // Dunno why it shouldnt be but i dont want any crashes.
            {
                // Update the followed player's score in realtime.
                infoPool.playerInfo[currentPs.ClientNum].score.score = currentPs.Persistant[(int)PersistantEnum.PERS_SCORE];
            }


            foreach(var pbp in possiblyBlockedPlayers)
            {
                for (int i = 0; i < client.ClientHandler.MaxClients; i++)
                {
                    if ((DateTime.Now-infoPool.playerInfo[i].chatCommandTrackingStuff.blocksTracker.lastBlockedRegistered).TotalSeconds < 1.0)
                    {
                        // Don't count cascading blocks. Aka, if someone was already blocked recently, don't count him blocking someone else.
                        continue;
                    }
                    //16 down 40 up
                    if(entityOrPSVisible[i] && i != pbp.Key && lastPosition[i].Z+40 > lastPosition[pbp.Key].Z-24 && lastPosition[i].Z-24 < lastPosition[pbp.Key].Z+40) // was he likely blocked by this player? More of a rough check than a 100% safe calculation but oh well, it's just a guess
                    {
                        Vector2 blockedPos = new Vector2() { X= lastPosition[pbp.Key].X, Y= lastPosition[pbp.Key].Y};
                        Vector2 blockerPos = new Vector2() { X= lastPosition[i].X, Y= lastPosition[i].Y};
                        if (Vector2.Distance(blockerPos, blockedPos) < 50 && Vector3.Dot(lastPosition[i] - lastPosition[pbp.Key], pbp.Value) > 0)
                        {
                            if (infoPool.killTrackersThisGame[i, pbp.Key].blocksTracker.CountBlock(true))
                            {
                                // that function also checks whether we already registered a block between these 2 in the last 500 ms, to avoid dupes. Returns true if it doesn't seem to be a dupe
                                infoPool.killTrackers[i, pbp.Key].blocksTracker.CountBlock(true);

                                // Not ratelimited since already checked.
                                infoPool.playerInfo[i].chatCommandTrackingStuff.blocksTracker.CountBlock(false);
                                infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.blocksTracker.CountBlock(false);
                                infoPool.playerInfo[pbp.Key].chatCommandTrackingStuff.blocksTracker.CountBlocked(false);
                                infoPool.playerInfo[pbp.Key].chatCommandTrackingStuffThisGame.blocksTracker.CountBlocked(false);

                                if (infoPool.playerInfo[i].team == infoPool.playerInfo[pbp.Key].team && currentGameType >= GameType.Team)
                                {
                                    infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.blocksFriendly++;
                                    infoPool.playerInfo[i].chatCommandTrackingStuff.blocksFriendly++;
                                } else
                                {
                                    infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.blocksEnemy++;
                                    infoPool.playerInfo[i].chatCommandTrackingStuff.blocksEnemy++;
                                }

                                if (playerHasFlag[pbp.Key])
                                {
                                    if (infoPool.playerInfo[i].team == infoPool.playerInfo[pbp.Key].team && currentGameType >= GameType.Team)
                                    {
                                        infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.blocksFlagCarrierFriendly++;
                                        infoPool.playerInfo[i].chatCommandTrackingStuff.blocksFlagCarrierFriendly++;
                                    } else
                                    {
                                        infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.blocksFlagCarrierEnemy++;
                                        infoPool.playerInfo[i].chatCommandTrackingStuff.blocksFlagCarrierEnemy++;
                                    }
                                }
                            }
                        }
                        // Just debug:
                        //else
                        //{
                        //    Debug.WriteLine($"Vector2.Distance(blockerPos, blockedPos) < 50 : {Vector2.Distance(blockerPos, blockedPos) < 50}");
                         //   Debug.WriteLine($"Vector3.Dot(lastPosition[i] - lastPosition[pbp.Key], pbp.Value) > 0 : {Vector3.Dot(lastPosition[i] - lastPosition[pbp.Key], pbp.Value) > 0}");
                        //}
                    } 
                    // Just debug:
                    //else if (entityOrPSVisible[i])
                    //{
                    //    Debug.WriteLine($"lastPosition[i].Z+40 > lastPosition[pbp.Key].Z-16 : {lastPosition[i].Z+40 > lastPosition[pbp.Key].Z-16}");
                     //   Debug.WriteLine($" lastPosition[i].Z-16 < lastPosition[pbp.Key].Z+40 : { lastPosition[i].Z - 16 < lastPosition[pbp.Key].Z + 40}");
                   // }
                }
            }

            // Save flag positions. 
            // Each flag (red & blue) has a base entity and a dropped entity (if dropped and not yet returned)
            // We want to save each for extra info. 
            // A currently picked up/carried/dropped flag's base entity is not sent to clients, but it still exists
            // as a separate game entity from the dropped flag. 
            // How can we tell if a flag entity is a dropped or a base flag?
            // The game entity has the FL_DROPPED_ITEM flag added, however this doesn't appear to be shared with the clients.
            // We can try and get some clues from other stuff though.
            // Dropped flags get an EF_BOUNCE_HALF eFlag (eFlag is different from flag, latter is server only). 
            // Dropped flags also have a pos.trType of TR_GRAVITY.
            // However pos.trType actually gets reset to TR_STATIONARY once the bouncing is over.
            // The bounce flag might however remain, I do not see it being deleted, so that's a way to go?
            // 
            // In theory we should be able to just check the flag item against the flag status (cs 23) but 
            // we might be (?) at a point in time where the new flag status has not yet been parsed, but the new 
            // entities have, so we might mistake a base flag for a dropped one or vice versa.
            List<Vector3> mohBeamsFound = mohFreezeTagDetected ? null : new List<Vector3>();
            for (int i = client.ClientHandler.MaxClients; i < JKClient.Common.MaxGEntities; i++)
            {
                int snapEntityNum = snapEntityMapping[i];
                if (snapEntityNum != -1/*entities[i].CurrentValid*/)
                {
                    if (SpectatedPlayer.HasValue)
                    {
                        infoPool.lastConfirmedVisible[SpectatedPlayer.Value, i] = DateTime.Now;
                        entityOrPSVisible[i] = true;
                    }

                    if (mohMode && !mohFreezeTagDetected && snap.Entities[snapEntityNum].EntityType == 8) // ET _BEAM
                    {
                        // We try to detect beams to see if we are in freeze tag gamemode.
                        // Beams are used for that lil colorful "prison" around frozen players.
                        // We need to detect it because the scoreboard "dead" status is broken for freeze tag, for whatever reason,
                        // and using it breaks the calculation of who to follow
                        //
                        // We do not do this detection if we already have detected this.
                        if(snap.Entities[snapEntityNum].ModelIndex == 1
                            && snap.Entities[snapEntityNum].Parent == 1023 // ENTITYNUM_NONE
                            && snap.Entities[snapEntityNum].RenderFx == 48 // RF_BEAM | RF_FRAMELERP
                            && snap.Entities[snapEntityNum].BoneTag[0] == -1 // IDk. all bonetags set like that.
                            && snap.Entities[snapEntityNum].Scale == 4 // Idk
                            && snap.Entities[snapEntityNum].EntityFlags == 64 // EF_LINKANGLES ?
                            && Math.Abs(snap.Entities[snapEntityNum].Alpha-0.5f) < 0.01f // The weird transmission seems to change it from actual 0.5f to some value like 0.5019608f. So make more of a "soft" comparison
                            )
                        {
                            // This seems like one of those beams
                            Vector3 thisBeamPos = new Vector3() {
                                X= snap.Entities[snapEntityNum].Position.Base[0],
                                Y= snap.Entities[snapEntityNum].Position.Base[1],
                                Z= snap.Entities[snapEntityNum].Position.Base[2]
                            };

                            int foundCloseBeams = 0;
                            // Compare to previous found beams.
                            foreach(Vector3 otherBeamPos in mohBeamsFound)
                            {
                                if(otherBeamPos.Z == thisBeamPos.Z && Vector3.Distance(thisBeamPos,otherBeamPos) < 30) // The actual distance between the beams appears to be 25.9xxxxx but who knows if it varies a bit between servers. Let's go safe
                                {
                                    foundCloseBeams++;
                                }
                            }

                            if(foundCloseBeams > 1)
                            {
                                mohFreezeTagDetected = true;
                                resetAllFrozenStatus();
                                serverWindow.addToLog("MOH Freeze Tag detected from beams.");
                            }
                            else
                            {
                                mohBeamsFound.Add(thisBeamPos);
                            }
                        }
                    }
                    // Flag bases
                    else if (snap.Entities[snapEntityNum].EntityType == ETTeam)
                    {
                        Team team = (Team)snap.Entities[snapEntityNum].ModelIndex;
                        if (team == Team.Blue || team == Team.Red)
                        {
                            infoPool.teamInfo[(int)team].flagBasePosition.X = snap.Entities[snapEntityNum].Position.Base[0];
                            infoPool.teamInfo[(int)team].flagBasePosition.Y = snap.Entities[snapEntityNum].Position.Base[1];
                            infoPool.teamInfo[(int)team].flagBasePosition.Z = snap.Entities[snapEntityNum].Position.Base[2];
                            infoPool.teamInfo[(int)team].flagBaseEntityNumber = i;
                            infoPool.teamInfo[(int)team].lastFlagBasePositionUpdate = DateTime.Now;
                        }
                    } else if (snap.Entities[snapEntityNum].EntityType == ETItem)
                    {
                        if(snap.Entities[snapEntityNum].ModelIndex == infoPool.teamInfo[(int)Team.Red].flagItemNumber ||
                            snap.Entities[snapEntityNum].ModelIndex == infoPool.teamInfo[(int)Team.Blue].flagItemNumber
                            )
                        {

                            Team team = snap.Entities[snapEntityNum].ModelIndex == infoPool.teamInfo[(int)Team.Red].flagItemNumber ? Team.Red : Team.Blue;

                            // Check if it's base flag item or dropped one
                            if ((snap.Entities[snapEntityNum].EntityFlags & (int)JOStuff.EntityFlags.EF_BOUNCE_HALF) != 0 || (jkaMode && infoPool.teamInfo[(int)team].flag == FlagStatus.FLAG_DROPPED)) // This is DIRTY.
                            {
                                // This very likely is a dropped flag, as dropped flags get the EF_BOUNCE_HALF entity flag.
                                infoPool.teamInfo[(int)team].flagDroppedPosition.X = snap.Entities[snapEntityNum].Position.Base[0];
                                infoPool.teamInfo[(int)team].flagDroppedPosition.Y = snap.Entities[snapEntityNum].Position.Base[1];
                                infoPool.teamInfo[(int)team].flagDroppedPosition.Z = snap.Entities[snapEntityNum].Position.Base[2];
                                infoPool.teamInfo[(int)team].droppedFlagEntityNumber = i;
                                infoPool.teamInfo[(int)team].lastFlagDroppedPositionUpdate = DateTime.Now;

                            } else if (!jkaMode || infoPool.teamInfo[(int)team].flag == FlagStatus.FLAG_ATBASE) // This is DIRTY. I hate it. Timing could mess this up. Hmm or maybe not? Configstrings are handled first. Hmm. Well it's the best I can do for JKA.
                            {
                                // This very likely is a base flag item, as it doesn't have an EF_BOUNCE_HALF entity flag.
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.X = snap.Entities[snapEntityNum].Position.Base[0];
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.Y = snap.Entities[snapEntityNum].Position.Base[1];
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.Z = snap.Entities[snapEntityNum].Position.Base[2];
                                infoPool.teamInfo[(int)team].flagBaseItemEntityNumber = i;
                                infoPool.teamInfo[(int)team].lastFlagBaseItemPositionUpdate = DateTime.Now;
                                
                            }
                        }
                    }
                }
                else
                {
                    if (SpectatedPlayer.HasValue)
                    {
                        infoPool.lastConfirmedInvisible[SpectatedPlayer.Value, i] = DateTime.Now;
                        entityOrPSVisible[i] = false;
                    }
                }
            }

            bool isSillyCameraOperator = this.CameraOperator is CameraOperators.SillyCameraOperator;//this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator;
            bool isFFACameraOperator = this.CameraOperator is CameraOperators.FFACameraOperator;//this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator;
            bool isDefragCameraOperator = this.CameraOperator is CameraOperators.OCDCameraOperator;//this.CameraOperator.HasValue && serverWindow.getCameraOperatorOfConnection(this) is CameraOperators.SillyCameraOperator;
            bool maySendFollow = (!isSillyCameraOperator || !amNotInSpec) && (!amNotInSpec || !isDuelMode || jkaMode) && snap.PlayerState.PlayerMoveType != JKClient.PlayerMoveType.Intermission; // In jk2, sending follow while it being ur turn in duel will put you back in spec but fuck up the whole game for everyone as it is always your turn then.


            if (teamChangesDetected)
            {
                serverWindow.requestPlayersRefresh();
                //serverWindow.Dispatcher.Invoke(() => {
                    //lock (serverWindow.playerListDataGrid)
                    //{
                    //    serverWindow.playerListDataGrid.ItemsSource = null;
                    //    serverWindow.playerListDataGrid.ItemsSource = infoPool.playerInfo;
                    //}
                //});
            }

            /*if (amNotInSpec) // Maybe in the future I will
            {
                
                if (!isSillyCameraOperator) // Silly operator means we actually don't want to be in spec. That is its only purpose.
                {
                    // TODO: Special handling for selfkill when g_allowduelSuicide 1?
                    // TODO: Why does it fuck up the order in jk2?

                    // Try to get back out of spec
                    // Depending on server settings, this might not work though, but hey, we can try.

                    // Duel could theoretically allow suicide, in which case this could be used to let next player play safely.
                    // Actually don't do this: If we can't go spec, oh well.
                    //leakyBucketRequester.requestExecution("kill", RequestCategory.SELFKILL, 5, 10000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);

                    if (maySendFollow) // In jka i can actually weasel out of duels like this, but not in jk2 sadly. It puts me spec but immediately queues me back up for the next fight. Sad. Never found out why.
                    {
                        // Do this with a big delay. We may not be allowed to go into spec at all so eh... just try once a minute.
                        // We still ahve to do this to begin with because on some servers we can't force ourselves spec via the follow command, but we can via team spectator.
                        leakyBucketRequester.requestExecution("team spectator", RequestCategory.GOINTOSPEC, 5, 60000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                    
                        // Ironic... this does allow me to go spec in duel, but it creates an endless loop where I am always the next upcoming player, at leasts in jk2. :( Even worse
                        // Actually don't do this: Our normal follow command accomplishes the same thing and at least follows someone worth following instead of random person            
                        //leakyBucketRequester.requestExecution("follownext", RequestCategory.GOINTOSPECHACK, 5, 3000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS); // Evil hack to go spec in duel mode LOL
                    }
                }
            }
            else
            {
                // Can't reproduce it but once ended up with a weird endless loop of going spec and following. Musts be very unlucky timing combined with some weird shit.
                leakyBucketRequester.purgeByKinds( new RequestCategory[] { RequestCategory.SELFKILL, RequestCategory.GOINTOSPEC, RequestCategory.GOINTOSPECHACK});
            }*/

            if (mohMode)
            {
                if (mohExpansion && this.mohSpectatorFreeFloat)
                { 
                    // In expansions we can't just use use button to follow someone. We have to use use button to go into following mode and then jump or crouch to change players we follow.
                    // So if we detect that we are free floating... make sure we aren't. Check once every 2 seconds.
                    if((DateTime.Now- lastMOHChangeToFollowModeCommandSent).TotalMilliseconds > 2000 && (DateTime.Now- lastMOHSpectatorNonFreeFloat).TotalMilliseconds > 5000)
                    {
                        this.QueueSingleButtonPress((Int64)UserCommand.Button.UseMOHAA);
                        lastMOHChangeToFollowModeCommandSent = DateTime.Now;
                    }
                } else
                {


                    Int64 nextPlayerButton = this.protocol > ProtocolVersion.Protocol8 ? (Int64)FakeButton.Jump : (Int64)UserCommand.Button.UseMOHAA;
                    bool serverFPSKnown = this.serverFPS > 0;

                    // MOHAA is a special child and needs its own kind of handling
                    if (amNotInSpec) // Often sending a "follow" command automatically puts us in spec but on some mods it doesn't. So do this as a backup.
                    {
                        if (lastPlayerState.Stats[0] > 0)
                        {
                            // Also kill myself if I'm alive. Can still spectate from dead position but at least I'm not standing around bothering ppl.
                            leakyBucketRequester.requestExecution("kill", RequestCategory.SELFKILL, 5, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS, null, 6000);
                        }
                        leakyBucketRequester.requestExecution("spectator", RequestCategory.GOINTOSPEC, 5, 60000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS, null, 6000);
                   
                    }

                    // Determine best player.
                    float bestScore = float.NegativeInfinity;
                    int bestScorePlayer = -1;
                    int index = 0;
                    int currentlySpectatedIndex = 0;
                    int wishPlayerIndex = 0;
                    int preferredPlayer = this.CameraOperator is CameraOperators.SpectatorCameraOperator? ((this.CameraOperator as CameraOperators.SpectatorCameraOperator)?.spectatorToFollow).GetValueOrDefault(-1) :-1;
                    foreach (PlayerInfo player in infoPool.playerInfo)
                    {
                        if (SpectatedPlayer == player.clientNum) currentlySpectatedIndex = index;

                        // TODO If player disconnects, detect disconnect string and set infovalid false.
                        // TODO Detect frozen players as dead.
                        if (!player.lastFullPositionUpdate.HasValue || (DateTime.Now - player.lastFullPositionUpdate.Value).TotalMinutes > 5) continue; // Player is probably gone... but MOH failed to tell us :)
                        if (!player.IsAlive && (!player.lastAliveStatusUpdated.HasValue || (DateTime.Now - player.lastAliveStatusUpdated.Value).TotalSeconds < 10)) continue; // MOH is a difficult beast. We can't follow dead ppl or we get flipped away. To avoid an endless loop ... avoid players we KNOW were dead within last 10 seconds and of whom we don't have any confirmation of being alive
                        if (player.team == Team.Spectator) continue; // MOH is a difficult beast. We can't follow dead ppl or we get flipped away. To avoid an endless loop ... avoid players we KNOW were dead within last 10 seconds and of whom we don't have any confirmation of being alive
                        if (player.score.ping >= 999) continue; 
                        if (!player.infoValid || player.inactiveMOH) continue; // We can't rely on infovalid true to mean actually valid, but we can somewhat rely on not true to be invalid.
                        if (player.IsFrozen && mohFreezeTagDetected)
                        {
                            if (mohFreezeTagAllowsFrozenFollow) // This might always be the case, not sure. Better safe than sorry?
                            {
                                index++; // If following frozen people is possible, make sure we do increment the index here so that fast skipping remains possible without getting a too low estimate of required skips.
                            }
                            continue;
                        }


                        float currentScore = float.NegativeInfinity;
                        if(currentGameType > GameType.Team)
                        {
                            // Total kill count. We don't get death count here.
                            currentScore = player.score.totalKills;
                        } else if(currentGameType > GameType.FFA)
                        {
                            // K/D
                            //currentScore = (float)player.score.kills/ Math.Max(1.0f,(float)player.score.deaths);
                            currentScore = (float)(Math.Pow((double)player.score.kills,1.5)/ Math.Max(1.0,(double)player.score.deaths)); // Modified K/D with more emphasis on kills. 30/10 would be similar to 10/2 for example. We recognize that players can get lucky at the start of a game, and also that campers might get a better K/D but more boring gameplay. Nice side effect: At equal kill counts, this still behaves linearly when comparing two players, e.g. the player with only half the deaths will have 2x as good of a ratio.
                        } else
                        {
                            // K/D
                            //currentScore = (float)player.score.kills / Math.Max(1.0f, (float)player.score.deaths);
                            currentScore = (float)(Math.Pow((double)player.score.kills, 1.5) / Math.Max(1.0, (double)player.score.deaths)); // Modified K/D with more emphasis on kills. 30/10 would be similar to 10/2 for example. We recognize that players can get lucky at the start of a game, and also that campers might get a better K/D but more boring gameplay. Nice side effect: At equal kill counts, this still behaves linearly when comparing two players, e.g. the player with only half the deaths will have 2x as good of a ratio.
                        }
                        if(currentScore > bestScore || preferredPlayer == player.clientNum)
                        {
                            bestScorePlayer = player.clientNum;
                            bestScore = currentScore;
                            wishPlayerIndex = index;

                            if (preferredPlayer == player.clientNum)
                            {
                                break;
                            }
                        }
                        index++;
                    }

                    if(bestScorePlayer != -1)
                    {
                        WishSpectatedPlayer = bestScorePlayer;
                    }

                    int countButtonPressesRequired = 0;
                    int countButtonPressesRequiredInverse = 0;

                    if (wishPlayerIndex > currentlySpectatedIndex)
                    {
                        countButtonPressesRequired = wishPlayerIndex - currentlySpectatedIndex;
                    }
                    else
                    {
                        int highestIndexPlayer = index - 1;
                        countButtonPressesRequired = (highestIndexPlayer - currentlySpectatedIndex) + (wishPlayerIndex + 1);
                    }

                    if (mohExpansion)
                    {
                        if (wishPlayerIndex < currentlySpectatedIndex)
                        {
                            countButtonPressesRequiredInverse = currentlySpectatedIndex - wishPlayerIndex;
                        }
                        else
                        {
                            int highestIndexPlayer = index - 1;
                            countButtonPressesRequiredInverse = (highestIndexPlayer - wishPlayerIndex) + (currentlySpectatedIndex + 1);
                        }
                        if(countButtonPressesRequiredInverse < countButtonPressesRequired)
                        { // Move backwards if it's faster
                            countButtonPressesRequired = countButtonPressesRequiredInverse;
                            nextPlayerButton = (Int64)FakeButton.Crouch;
                        }
                    }

                    if (_connectionOptions.mohFastSwitchFollow && !mohExpansion) // Expansions really can't handle the fast skip cuz they use jump button which always just uses the last value instead of a cumulative one.
                    {
                        if (bestScorePlayer != -1 && SpectatedPlayer != bestScorePlayer && (DateTime.Now - lastMOHFollowChangeButtonPressQueued).TotalMilliseconds > (lastSnapshot.ping * 2) && (DateTime.Now - lastAppliedQueueButtonPress).TotalMilliseconds > (lastSnapshot.ping * 2) && (DateTime.Now - lastAppliedDurationButtonPress).TotalMilliseconds > (lastSnapshot.ping * 2))
                        {

                            int fastSwitchManualCount = _connectionOptions.mohVeryFastSwitchFollowManualCount;
                            int durationBasedSwitchManualCount = _connectionOptions.mohDurationBasedSwitchFollowManualCount; // This needs more buffer because it's even less precise than the fast switch thing. Default 3 atm.
                            int countButtonPressesToRequest = 0;
                            if (serverFPSKnown)
                            {
                                countButtonPressesToRequest = countButtonPressesRequired > durationBasedSwitchManualCount ? (countButtonPressesRequired - durationBasedSwitchManualCount) : 1;
                            } else if (_connectionOptions.mohVeryFastSwitchFollow && fastSwitchManualCount > 0)
                            {
                                countButtonPressesToRequest = countButtonPressesRequired > fastSwitchManualCount ? (countButtonPressesRequired - fastSwitchManualCount) : 1;
                            } else
                            {
                                countButtonPressesToRequest = countButtonPressesRequired >= 4 ? countButtonPressesRequired / 2 : 1; // We can't 100% rely on our indexes and that they accurately represent who can be watched. Aka we don't know exactly who we will get with a single press. So just do half the required switches in one go. Rest by single presses. Just feels safer.
                            }

                            // No follow command in MOH. We just have to press the change player button a million times :)
                            lastMOHFollowChangeButtonPressQueued = DateTime.Now;
                            if(serverFPSKnown)
                            {
                                if(countButtonPressesToRequest > 1)
                                {
                                    int serverFrameDuration = 1000 / serverFPS;
                                    SetDurationButtonPress(nextPlayerButton, countButtonPressesToRequest* serverFrameDuration);
                                } else
                                {
                                    QueueSingleButtonPress(nextPlayerButton);
                                }
                            } else
                            {
                                // This was a nice idea but doesn't actually work because
                                for(int i=0;i< countButtonPressesToRequest; i++)
                                {
                                    this.QueueButtonPress(nextPlayerButton);
                                }
                            }
                        }
                    }
                    else if (mohExpansion)
                    {
                        // MOH expansions are even worse than the original MOHAA... if I understand it currectly.
                        // Their player changes are based on upmove instead of button presses.
                        // Hence, only the last processed usercommand is actually considered, not like in the base game
                        // so at the time of each server frame, the last usercommand must have had upmove set, else 
                        // it just gets lost.
                        // And then of course it also must process that there was a command before it that DIDN'T have it (a bit speculation)
                        // So to be safe I must keep pressed for 2*server frame time and then keep released for 2*server frame time as well.
                        //
                        // Actually, 2*server frame time isn't enough, still doesn't work. It only started working after I went 4* server frame time. DON'T ASK ME WHY I DON'T KNOW.
                        int serverFrameDuration = serverFPS == 0 ? 0 : 1000 / serverFPS;
                        if (bestScorePlayer != -1 && SpectatedPlayer != bestScorePlayer && (DateTime.Now - lastMOHFollowChangeButtonPressQueued).TotalMilliseconds > (lastSnapshot.ping * 2) && (DateTime.Now - lastAppliedDurationButtonPress).TotalMilliseconds > Math.Max(Math.Max(serverFrameDuration * 4,lastSnapshot.ping * 2), _connectionOptions.mohExpansionSwitchMinDuration))
                        {
                            lastMOHFollowChangeButtonPressQueued = DateTime.Now;
                            SetDurationButtonPress(nextPlayerButton, Math.Max(_connectionOptions.mohExpansionSwitchMinDuration, serverFrameDuration *4),DurationButtonPressType.KEEPPRESSED);
                        }
                    }
                    else
                    {
                        if (bestScorePlayer != -1 && SpectatedPlayer != bestScorePlayer && (DateTime.Now - lastMOHFollowChangeButtonPressQueued).TotalMilliseconds > (lastSnapshot.ping * 2))
                        {
                            // No follow command in MOH. We just have to press the change player button a million times :)
                            lastMOHFollowChangeButtonPressQueued = DateTime.Now;
                            this.QueueSingleButtonPress(nextPlayerButton);
                        }
                    }

                }

            } else
            {


                bool spectatedPlayerIsBot = SpectatedPlayer.HasValue && playerIsLikelyBot(SpectatedPlayer.Value);
                bool spectatedPlayerIsVeryAfk = SpectatedPlayer.HasValue && playerIsVeryAfk(SpectatedPlayer.Value,true);
                bool onlyBotsActive = (((DateTime.Now - infoPool.lastBotOnlyConfirmed)?.TotalMilliseconds).GetValueOrDefault(double.PositiveInfinity) < 10000) || infoPool.botOnlyGuaranteed;
                Int64 myClientNums = serverWindow.getJKWatcherClientOrFollowedNumsBitMask();
                if (((DateTime.Now-lastServerInfoChange).TotalMilliseconds > 500 || isDuelMode) && // Some mods/gametypes (appear to! maybe im imagining) specall and then slowly add players, not all in one go. Wait until no changes happening for at least half a second. Exception: Duel. Because there's an intermission for each player change anyway.
                    maySendFollow && AlwaysFollowSomeone && infoPool.lastScoreboardReceived != null 
                    && (ClientNum == SpectatedPlayer || (
                    (isFFACameraOperator || this.CameraOperator == null) && (
                    (spectatedPlayerIsBot && !onlyBotsActive)|| ((DateTime.Now - lastSpectatedPlayerChange).TotalSeconds > 10 && spectatedPlayerIsVeryAfk)))
                    )
                    ) // Not following anyone. Let's follow someone.
                {
                    if (amNotInSpec) // Often sending a "follow" command automatically puts us in spec but on some mods it doesn't. So do this as a backup.
                    {
                        if(lastPlayerState.Stats[0] > 0)
                        {
                            // Also kill myself if I'm alive. Can still spectate from dead position but at least I'm not standing around bothering ppl.
                            leakyBucketRequester.requestExecution("kill", RequestCategory.SELFKILL, 5, 5000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS, null,6000);
                        }
                        leakyBucketRequester.requestExecution("team spectator", RequestCategory.GOINTOSPEC, 5, 60000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS,null,6000);
                        
                    }

                    int highestScore = int.MinValue;
                    List<int> highestScorePlayer = new List<int>();
                    //int highestScorePlayer = -1;
                    float highestScoreRatio = float.NegativeInfinity;
                    //int highestScoreRatioPlayer = -1;
                    List<int> highestScoreRatioPlayer = new List<int>();
                    // Pick player with highest score.
    findHighestScore:
                    //bool allowAFK = true;
                    for(int allowAfkLevel = 0; allowAfkLevel < 3; allowAfkLevel++)
                    {
                        //if (highestScorePlayer != -1) break; // first search only for players that arent afk. then if that doesnt work, include afk ones but not main
                        if (highestScorePlayer.Count > 0) break; // first search only for players that arent afk. then if that doesnt work, include afk ones but not main
                        //allowAFK = !allowAFK;
                        foreach (PlayerInfo player in infoPool.playerInfo)
                        {
                            bool afkCriteriaSatisfied = !playerIsVeryAfk(player.clientNum, false) || (player.confirmedAfk ? (allowAfkLevel >= 2) : (allowAfkLevel >= 1)); // Only allow confirmed afk ppl in the last stage.

                            if (((myClientNums & (1L << player.clientNum)) == 0 /*|| isDefragCameraOperator*/) // Don't follow ourselves or someone another connection of ours is already following;// strike this: except in Defrag since defrag is weird and otherwise extra connections don't despawn quickly enough
                                && (DateTime.Now-clientsWhoDontWantTOrCannotoBeSpectated[player.clientNum]).TotalMilliseconds > 120000 && player.infoValid && player.team != Team.Spectator 
                                && (onlyBotsActive || !playerIsLikelyBot(player.clientNum)) 
                                && (player.clientNum != SpectatedPlayer || !spectatedPlayerIsVeryAfk) // TODO: Why allow spectating currently spectated at all? That's the whole point we're in this loop - to find someone else?
                                && afkCriteriaSatisfied
                                //&& (!playerIsVeryAfk(player.clientNum, false) || allowAFK)
                            )
                            {
                                if (player.score.score > highestScore || highestScorePlayer.Count == 0)
                                {
                                    highestScore = player.score.score;
                                    //highestScorePlayer = player.clientNum;
                                    highestScorePlayer.Clear();
                                    highestScorePlayer.Add(player.clientNum);
                                } else if (player.score.score == highestScore)
                                {
                                    highestScorePlayer.Add(player.clientNum);
                                }
                                int thisPlayerScoreTime = player.score.time;
                                if (thisPlayerScoreTime > 5) // we try to find the player with highest score ratio (score per time in game) if we can. but don't count people under 5 minutes, their values might not be statistically representative? Also avoid division by 0 that way
                                {
                                    // Would be even cooler if we could remove afk time from the equation since that distorts the score/time ratio. 
                                    // Sadly there is no 100% reliable way of observing afk time and we might very well get the number very wrong.
                                    // For example we might act like someone is afk because we aren't seeing him but he's actually there.
                                    // Alternatively, we could just track confirmed afk time - player visible and afk. And subtract that?
                                    // Similarly to tracking total time visible for some other stats.
                                    float thisPlayerScoreRatio = (float)player.score.score / (float)thisPlayerScoreTime;
                                    if (thisPlayerScoreRatio > highestScoreRatio || highestScoreRatioPlayer.Count == 0)
                                    {
                                        highestScoreRatio = thisPlayerScoreRatio;
                                        //highestScoreRatioPlayer = player.clientNum;
                                        highestScoreRatioPlayer.Clear();
                                        highestScoreRatioPlayer.Add(player.clientNum);
                                    } else if (thisPlayerScoreRatio == highestScoreRatio)
                                    {
                                        highestScoreRatioPlayer.Add(player.clientNum);
                                    }
                                }
                            }
                        }
                    }
                    //if(highestScoreRatioPlayer != -1)
                    if(highestScoreRatioPlayer.Count > 0)
                    {
                        int clientToFollow = highestScoreRatioPlayer.Count > 1 ? highestScoreRatioPlayer[getNiceRandom(0, highestScoreRatioPlayer.Count)] : highestScoreRatioPlayer[0]; 
                        lastRequestedAlwaysFollowSpecClientNum = clientToFollow;
                        WishSpectatedPlayer = clientToFollow;
                        leakyBucketRequester.requestExecution("follow " + clientToFollow, RequestCategory.FOLLOW, 1, 2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                    }
                    //else if (highestScorePlayer != -1) // Assuming any players at all exist that are playing atm.
                    else if (highestScorePlayer.Count > 0) // Assuming any players at all exist that are playing atm.
                    {
                        int clientToFollow = highestScorePlayer.Count > 1 ? highestScorePlayer[getNiceRandom(0, highestScorePlayer.Count)] : highestScorePlayer[0];
                        lastRequestedAlwaysFollowSpecClientNum = clientToFollow;
                        WishSpectatedPlayer = clientToFollow;
                        leakyBucketRequester.requestExecution("follow " + clientToFollow, RequestCategory.FOLLOW, 1, 2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                    }
                }

            }

            foreach(PlayerInfo pi in infoPool.playerInfo)
            {
                if (pi.infoValid)  // update this so we can show ppl who recently disconnected and such
                {
                    if (!infoPool.ratingsAndNames.ContainsKey(pi.chatCommandTrackingStuff.rating))
                    {
                        infoPool.ratingsAndNames[pi.chatCommandTrackingStuff.rating] = new Glicko2RatingInfo();
                        infoPool.ratingsAndNames[pi.chatCommandTrackingStuff.rating].name = pi.name;
                    }
                    if (!infoPool.ratingsAndNamesThisGame.ContainsKey(pi.chatCommandTrackingStuffThisGame.rating))
                    {
                        infoPool.ratingsAndNamesThisGame[pi.chatCommandTrackingStuffThisGame.rating] = new Glicko2RatingInfo();
                        infoPool.ratingsAndNamesThisGame[pi.chatCommandTrackingStuffThisGame.rating].name = pi.name;
                    }
                    infoPool.ratingsAndNames[pi.chatCommandTrackingStuff.rating].lastSeenActive = DateTime.Now;
                    infoPool.ratingsAndNamesThisGame[pi.chatCommandTrackingStuffThisGame.rating].lastSeenActive = DateTime.Now;
                }
            }


            oldSpectatedPlayer = SpectatedPlayer;
        }

        private bool playerIsLikelyBot(int clientNumber)
        {
            return clientNumber >= 0 && clientNumber < client.ClientHandler.MaxClients && 
                (infoPool.playerInfo[clientNumber].confirmedBot || 
                (!infoPool.playerInfo[clientNumber].score.lastNonZeroPing.HasValue || (DateTime.Now - infoPool.playerInfo[clientNumber].score.lastNonZeroPing.Value).TotalMilliseconds > 10000) 
                && infoPool.playerInfo[clientNumber].score.pingUpdatesSinceLastNonZeroPing > 10);
        }
        private bool playerIsVeryAfk(int clientNumber, bool followed = false)
        {
            return (clientNumber >= 0 && clientNumber < client.ClientHandler.MaxClients) && (
                (DateTime.Now-infoPool.playerInfo[clientNumber].lastMovementDirChange).TotalMinutes > 30 
                || (infoPool.playerInfo[clientNumber].lastFullPositionUpdate.HasValue && (DateTime.Now - infoPool.playerInfo[clientNumber].lastFullPositionUpdate.Value).TotalMinutes < 5 && (DateTime.Now - infoPool.playerInfo[clientNumber].lastMovementDirChange).TotalMinutes > 15)
                || (followed && /*infoPool.playerInfo[clientNumber].lastPositionOrAngleChange.HasValue &&*/ (DateTime.Now - lastSpectatedPlayerChange).TotalSeconds > 10 && (DateTime.Now- infoPool.playerInfo[clientNumber].lastMovementDirChange).TotalMinutes > 15)
                || (followed && /*infoPool.playerInfo[clientNumber].lastPositionOrAngleChange.HasValue &&*/ (DateTime.Now - lastSpectatedPlayerChange).TotalSeconds > 10 && (DateTime.Now- infoPool.playerInfo[clientNumber].lastMovementDirChange).TotalMinutes > 5 && (DateTime.Now - lastAnyMovementDirChange).TotalMinutes > 5)
                );
        }

        private void OnServerInfoChanged(ServerInfo obj)
        {
            this.ServerInfoChanged?.Invoke(obj);
        }

        private string oldMapName = "";
        private string oldGameName = "";
        private string oldMOHHUDMEssage = "";
        PathFinder pathFinder = null;

        Regex waitCmdRegex = new Regex(@"^\s*wait\s*(\d+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex saySameCmdRegex = new Regex(@"^\s*say_same\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void ExecuteCommandList(string commandList, RequestCategory requestCategory, LeakyBucketRequester<string, RequestCategory>.RequestBehavior behavior = LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE,string saySame = "say")
        {
            string[] mapChangeCommands = commandList?.Split(';',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if(mapChangeCommands != null)
            {
                int waitTime = 0;
                foreach(string cmd in mapChangeCommands)
                {
                    Match match;
                    string mutableCmd = cmd;
                    mutableCmd = saySameCmdRegex.Replace(mutableCmd, (s)=> { // for chat commands we may wanna respond the same way they were sent.
                        return $"{saySame} ";
                    });
                    if ((match = waitCmdRegex.Match(mutableCmd)).Success && match.Groups.Count > 1)
                    {
                        waitTime += (match.Groups[1].Value?.Atoi()).GetValueOrDefault(0); // This might be a bit overly careful lol.
                    }
                    else
                    {
                        leakyBucketRequester.requestExecution(mutableCmd, requestCategory, 0,3000, behavior, null,waitTime > 0 ? waitTime : null);
                    }
                }

            }
        }

        private void resetThisGameStats()
        {
            CommitRatings();
            int maxClients = (client?.ClientHandler?.MaxClients).GetValueOrDefault(32);
            infoPool.ratingCalculatorThisGame = new Glicko2.RatingCalculator();
            infoPool.ratingPeriodResultsThisGame = new Glicko2.RatingPeriodResults();
            infoPool.ratingsAndNamesThisGame.Clear();
            for (int i=0;i< maxClients; i++)
            {
                for (int p = 0; p < maxClients; p++)
                {
                    infoPool.killTrackersThisGame[i, p] = new KillTracker();
                    infoPool.killTrackersThisGame[p, i] = new KillTracker();
                }
                infoPool.playerInfo[i].chatCommandTrackingStuffThisGame = new ChatCommandTrackingStuff(infoPool.ratingCalculatorThisGame) { onlineSince = DateTime.Now };
            }
        }

        private void resetAllFrozenStatus()
        {
            foreach(PlayerInfo pi in infoPool.playerInfo)
            {
                pi.IsFrozen = false;
            }
        }

        int serverMaxClientsLimit = 0;
        int serverPrivateClientsSetting = 0;
        ClientInfo[] oldClientInfo = new ClientInfo[64];

        int serverFPS = 0;

        // Update player list
        private void Connection_ServerInfoChanged(ServerInfo obj, bool newGameState)
        {
            if (obj.SendsAllEntities && !infoPool.serverSendsAllEntities)
            {
                serverWindow.addToLog("SERVER SEEMS TO HAVE SENDING ALL ENTITIES ACTIVATED!", false, 5000);
            }
            infoPool.serverSendsAllEntities = obj.SendsAllEntities;

            lastSnapshotParsedOrServerInfoChange = DateTime.Now;
            lastServerInfoChange = DateTime.Now;
            OnServerInfoChanged(obj);

            serverFPS = obj.FPS;

            serverWindow.serverMaxClientsLimit = serverMaxClientsLimit = obj.MaxClients > 1 ? obj.MaxClients : (client?.ClientHandler.MaxClients).GetValueOrDefault(32);
            if (obj.PrivateClients.HasValue)
            {
                serverWindow.serverPrivateClientsSetting = serverPrivateClientsSetting = obj.PrivateClients.GetValueOrDefault(0);
            }

            if (mohMode && (newGameState || obj.GameName != oldGameName || obj.MapName != oldMapName))
            {
                mohFreezeTagDetected = false;
                resetAllFrozenStatus();
                foreach (PlayerInfo pi in infoPool.playerInfo)
                {
                    pi.score.score = 0; // Reset after map changes so we don't think about following timed out players who still have the old high score remembered.
                    pi.score.totalKills = 0;
                    pi.score.kills = 0;
                    pi.score.deaths = 0;
                }
            }

            //obj.GameName
            if (/*obj.GameName.Contains("JA+ Mod", StringComparison.OrdinalIgnoreCase) || */obj.GameName.Contains("JA+", StringComparison.OrdinalIgnoreCase) || obj.GameName.Contains("^5X^2Jedi ^5Academy", StringComparison.OrdinalIgnoreCase) || obj.GameName.Contains("^4U^3A^5Galaxy", StringComparison.OrdinalIgnoreCase) || obj.GameName.Contains("AbyssMod", StringComparison.OrdinalIgnoreCase))
            {
                this.JAPlusDetected = true;
            }
            else if(obj.GameName.Contains("japro", StringComparison.OrdinalIgnoreCase))
            {
                this.JAProDetected = true;
            } 
            else if(obj.GameName.Contains("Freeze-Tag", StringComparison.OrdinalIgnoreCase))
            {
                if (!this.mohFreezeTagDetected)
                {
                    resetAllFrozenStatus();
                }
                this.mohFreezeTagDetected = true; // This might not always work tho if the server doesn't send that info. So there's a redundant detection of it elsewhere
                serverWindow.addToLog("MOH Freeze-Tag detected from game name.");
            } 
            else if(obj.GameName.Contains("SaberMod", StringComparison.OrdinalIgnoreCase))
            {
                this.SaberModDetected = true;
            } else if(obj.GameName.Contains("Movie Battles II", StringComparison.OrdinalIgnoreCase))
            {
                if (!this.MBIIDetected)
                {
                    infoPool.ResetFlagItemNumbers(true);
                }
                this.MBIIDetected = true;
            }
            infoPool.lastBotOnlyConfirmed = null; // Because if a new player just entered, we have no idea if it's only a bot or  not until we get his ping via score command.
            infoPool.ServerSlotsTaken = obj.ClientsIncludingBots;
            infoPool.MaxServerClients = obj.MaxClients > 0 ? obj.MaxClients : (client?.ClientHandler?.MaxClients).GetValueOrDefault(32);
            string serverName = client.ServerInfo.HostName;
            if (serverName != "")
            {
                serverWindow.ServerName = obj.HostName;
            }

            if (mohMode && obj.MOHScoreboardPICover != oldMOHHUDMEssage)
            {
                if(obj.MOHScoreboardPICover == "textures/hud/axiswin" || obj.MOHScoreboardPICover == "textures/hud/allieswin") // End of round. Clear all frozen status.
                {
                    serverWindow.addToLog("A team has won. Resetting frozen status for all players.");
                    resetAllFrozenStatus();
                }
                oldMOHHUDMEssage = obj.MOHScoreboardPICover;
            }

            bool executeMapChangeCommands = newGameState;
            if(obj.MapName != oldMapName)
            {
                resetAllFrozenStatus();
                resetThisGameStats();
                executeMapChangeCommands = true;
                string mapNameRaw = obj.MapName;
                int lastSlashIndex = mapNameRaw.LastIndexOf('/');
                if (lastSlashIndex != -1 &&( mapNameRaw.Length > lastSlashIndex+1)) // JKA mapnames sometimes look like "mp/ctf3". We get rid of the "mp/" part.
                {
                    mapNameRaw = mapNameRaw.Substring(lastSlashIndex + 1);
                }
                pathFinder = BotRouteManager.GetPathFinder(mapNameRaw);
                oldMapName = obj.MapName;
            }
            if (executeMapChangeCommands && this.HandleAutoCommands)
            {
                ExecuteCommandList(_connectionOptions.mapChangeCommands,RequestCategory.MAPCHANGECOMMAND);
            }

            oldGameName = obj.GameName;
            currentGameType = obj.GameType;
            isDuelMode = obj.GameType == GameType.Duel || obj.GameType == GameType.PowerDuel;

            // Check for referencedPaks
            InfoString systemInfo = new InfoString( client.GetMappedConfigstring(ClientGame.Configstring.SystemInfo));
            if(systemInfo["sv_referencedPakNames"] != lastKnownPakNames || systemInfo["sv_referencedPaks"] != lastKnownPakChecksums)
            {
                // Referenced paks changed:
                lastKnownPakNames = systemInfo["sv_referencedPakNames"];
                lastKnownPakChecksums = systemInfo["sv_referencedPaks"];

                string lastKnownPakNamesCaptured = lastKnownPakNames; // Capture for parallel thread
                string lastKnownPakChecksumsCaptured = lastKnownPakChecksums;
                if(mvHttpDownloadInfo == null || mvHttpDownloadInfo.Value.httpIsAvailable){
                    serverWindow.addToLog("Systeminfo: Referenced paks changed, trying to save to download list.");
                    TaskManager.TaskRun(async () => {
                        string[] pakNames = lastKnownPakNamesCaptured.Trim(' ').Split(" ");
                        string[] pakChecksums = lastKnownPakChecksumsCaptured.Trim(' ').Split(" ");
                        if(pakNames.Length != pakChecksums.Length)
                        {
                            serverWindow.addToLog("WARNING: Amount of pak names does not match amount of pak checksums. Weird. Aborting pak name logging this time.");
                            return;
                        } else if (pakNames.Length == 0)
                        {
                            serverWindow.addToLog("Referenced paks count is 0.");
                            return;
                        }

                        if(mvHttpDownloadInfo == null)
                        {
                            // Let's get server info packet.
                            using (ServerBrowser browser = new ServerBrowser(new JKClient.JOBrowserHandler(obj.Protocol)) { ForceStatus = true })
                            {
                                browser.InternalTaskStarted += Browser_InternalTaskStarted;
                                browser.Start(async (JKClientException ex)=> {
                                    serverWindow.addToLog("Exception trying to get ServerInfo for mvHttp purposes: "+ex.ToString());
                                });
                                NetAddress serverAddress = NetAddress.FromString(ip);

                                InfoString infoString = null;
                                try { 
                                    infoString = await browser.GetServerInfoInfo(serverAddress);
                                }
                                catch (Exception e)
                                {
                                    browser.Stop();
                                    browser.InternalTaskStarted -= Browser_InternalTaskStarted;
                                    serverWindow.addToLog("Exception trying to get ServerInfo for mvHttp purposes (during await): " + e.ToString());
                                    return;
                                }

                                MvHttpDownloadInfo tmpDLInfo = new MvHttpDownloadInfo();
                                tmpDLInfo.httpIsAvailable = false;
                                if (infoString.ContainsKey("mvhttp"))
                                {
                                    string serverAddressString = serverAddress.ToString();
                                    string[] serverAddressStringParts = serverAddressString.Split(":");
                                    if(serverAddressStringParts.Length > 0)
                                    {
                                        tmpDLInfo.httpIsAvailable = true;
                                        tmpDLInfo.urlPrefix = "http://" + serverAddressStringParts[0] + ":" + infoString["mvhttp"] + "/";
                                        serverWindow.addToLog($"Http downloads possible via port: {tmpDLInfo.urlPrefix}");
                                    }
                                } else if(infoString.ContainsKey("mvhttpurl"))
                                {
                                    tmpDLInfo.httpIsAvailable = true;
                                    tmpDLInfo.urlPrefix = infoString["mvhttpurl"];
                                    serverWindow.addToLog($"Http downloads possible via possibly external url: {tmpDLInfo.urlPrefix}");
                                } else
                                {
                                    tmpDLInfo.httpIsAvailable = false; 
                                    serverWindow.addToLog("Http downloads not available.");
                                }

                                mvHttpDownloadInfo = tmpDLInfo;

                                browser.Stop();
                                browser.InternalTaskStarted -= Browser_InternalTaskStarted;
                            }
                        }

                        if(mvHttpDownloadInfo != null && mvHttpDownloadInfo.Value.httpIsAvailable)
                        {

                            List<string> downloadLinks = new List<string>();
                            for(int pkI = 0; pkI<pakNames.Length; pkI++)
                            {
                                string pakName = pakNames[pkI];
                                string pakChecksum = pakChecksums[pkI];
                                int pakChecksumInt;
                                if (int.TryParse(pakChecksum, out pakChecksumInt))
                                {
                                    string dlLink = mvHttpDownloadInfo.Value.urlPrefix + (!mvHttpDownloadInfo.Value.urlPrefix.EndsWith("/") ? "/" : "") + pakName + ".pk3";
                                    string hashString = Convert.ToHexString(BitConverter.GetBytes(pakChecksumInt));
                                    serverWindow.addToLog($"Logged pk3 download url: {dlLink}");
                                    downloadLinks.Add($"{pakName},{hashString},{dlLink}");
                                    PakDownloader.Enqueue(dlLink, pakChecksumInt);
                                } else
                                {
                                    serverWindow.addToLog("Could not parse checksum integer, strange. Discarding.");
                                }
                            }
                            string[] downloadLinksArray = downloadLinks.ToArray();
                            Dispatcher.CurrentDispatcher.Invoke(()=> {
                                Helpers.logDownloadLinks(downloadLinksArray);
                            });
                        }


                    }, $"Pak HTTP Downloader ({ip},{serverWindow.ServerName})");
                } else
                {
                    serverWindow.addToLog("Systeminfo: Referenced paks changed, but http downloads are disabled.");
                }
            }

            ClientNum = client.clientNum;
            SpectatedPlayer = client.playerStateClientNum;

            infoPool.MapName = client.ServerInfo.MapName;
            if (!mohMode)
            {
                infoPool.teamInfo[(int)Team.Red].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
                infoPool.ScoreRed = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
                infoPool.teamInfo[(int)Team.Blue].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
                infoPool.ScoreBlue = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
            }

            if (obj.FloodProtect >=-1)
            {
                // Don't trust a setting of 0. Could still have non-engine limiting that isn't captured by the sv_floodprotect cvar.
                // In short, assume the worst: only 1 per second.
                int burst = obj.FloodProtect > 0 ? obj.FloodProtect : 1; // 0 means flood protection is disabled. Let's still try to be somewhat gracious and just set burst to 10
                leakyBucketRequester.changeParameters(burst, floodProtectPeriod);
            } else if (obj.FloodProtect == -2)
            {
                // This server has not sent an sv_floodprotect variable. Might be a legacy server without the leaky bucket algo
                // Be safe and set burst to 1, or risk losing commands
                leakyBucketRequester.changeParameters(1, floodProtectPeriod);
            }

            if(client.ClientInfo == null)
            {
                return;
            }
            bool noActivePlayers = true;
            bool anyNonBotActivePlayers = false;
            lock (infoPoolResetStuffMutex) { // Try to make sure various connections don't get in conflict here since we are doing some resetting by comparing previouss and new values.
                for(int i = 0; i < client.ClientHandler.MaxClients; i++)
                {
                    if(client.ClientInfo[i].Team != Team.Spectator && client.ClientInfo[i].InfoValid)
                    {
                        noActivePlayers = false;
                    }

                    infoPool.playerInfo[i].inactiveMOH = (mohMode && mohExpansion) ? !client.ClientInfo[i].IsActiveMOH : false;

                    if (!mohMode || mohExpansion) // Spearhead and Breakthrough actually do send valid team info in configstrings :)
                    {
                        infoPool.playerInfo[i].team = client.ClientInfo[i].Team;
                    } else if(oldClientInfo[i].InfoValid != client.ClientInfo[i].InfoValid)
                    {
                        // MOHAA information on teams is non-existent, we have to derive it from scoreboard and entity flags. (cringe yea)
                        // So whenever we have a confirmed connect or disconnect we set this to spectator here.
                        infoPool.playerInfo[i].team = Team.Spectator;
                    }

                    PlayerIdentification thisPlayerID = PlayerIdentification.FromClientInfo(client.ClientInfo[i]);
                    
                    // Whole JkWatcher instance based
                    if (infoPool.playerInfo[i].infoValid != client.ClientInfo[i].InfoValid) {

                        // Client connected/disconnected. Masybe reset some stats
                        if (client.ClientInfo[i].InfoValid)
                        {
                            // Wasn't connected before, is connected now.
                            // Is it a reconnect? If not, reset some stats.
                            bool isReconnect = infoPool.playerInfo[i].lastSeenValid.HasValue && (DateTime.Now - infoPool.playerInfo[i].lastSeenValid.Value).TotalMilliseconds < 60000
                                && infoPool.playerInfo[i].lastValidPlayerData == thisPlayerID;
                            if (!isReconnect)
                            {
                                for (int p = 0; p < client.ClientHandler.MaxClients; p++)
                                {
                                    infoPool.killTrackers[i, p] = new KillTracker();
                                    infoPool.killTrackers[p, i] = new KillTracker();
                                    infoPool.killTrackersThisGame[i, p] = new KillTracker();
                                    infoPool.killTrackersThisGame[p, i] = new KillTracker();
                                }
                                infoPool.playerInfo[i].chatCommandTrackingStuff = new ChatCommandTrackingStuff(infoPool.ratingCalculator) { onlineSince = DateTime.Now };
                                infoPool.playerInfo[i].chatCommandTrackingStuffThisGame = new ChatCommandTrackingStuff(infoPool.ratingCalculatorThisGame) { onlineSince = DateTime.Now };
                            }
                            else
                            {
                                serverWindow.addToLog($"Reconnect detected: {i}: {client.ClientInfo[i].Name}");
                            }
                        }
                        else
                        {
                            // Player is disconnecting. Remember time to check for reconnect.
                            infoPool.playerInfo[i].lastSeenValid = DateTime.Now;

                        }
                        
                    }

                    if (client.ClientInfo[i].InfoValid)
                    {
                        infoPool.playerInfo[i].lastSeenValid = DateTime.Now;
                        infoPool.playerInfo[i].lastValidPlayerData = thisPlayerID;
                    }

                    if (oldClientInfo[i].InfoValid != client.ClientInfo[i].InfoValid)
                    {
                        clientsWhoDontWantTOrCannotoBeSpectated[i] = DateTime.Now - new TimeSpan(1, 0, 0); // Reset this if he connected/disconnected, or there will be a timeout on the slot next time someone connects
                    }

                    if (this.HandleAutoCommands) // Check conditional commands
                    {
                        bool playerBecameActive = false;
                        if (client.ClientInfo[i].Team != Team.Spectator && client.ClientInfo[i].InfoValid)
                        {
                            if (oldClientInfo[i].Team != client.ClientInfo[i].Team || oldClientInfo[i].Name != client.ClientInfo[i].Name || oldClientInfo[i].InfoValid != client.ClientInfo[i].InfoValid)
                            {
                                playerBecameActive = true;
                            }
                        }
                        if (playerBecameActive)
                        {
                            ConditionalCommand[] conditionalCommands = _connectionOptions.conditionalCommandsParsed;
                            foreach (ConditionalCommand cmd in conditionalCommands) // TODO This seems inefficient, hmm
                            {
                                if ((!cmd.mainConnectionOnly || this.IsMainChatConnection) && cmd.type == ConditionalCommand.ConditionType.PLAYERACTIVE_MATCHNAME && (cmd.conditionVariable1.Match(client.ClientInfo[i].Name).Success || cmd.conditionVariable1.Match(Q3ColorFormatter.cleanupString(client.ClientInfo[i].Name)).Success))
                                {
                                    string commands = cmd.commands
                                        .Replace("$name", client.ClientInfo[i].Name, StringComparison.OrdinalIgnoreCase)
                                        .Replace("$clientnum", i.ToString(), StringComparison.OrdinalIgnoreCase)
                                        .Replace("$myclientnum", this.ClientNum.GetValueOrDefault(-1).ToString(), StringComparison.OrdinalIgnoreCase);
                                    ExecuteCommandList(commands, cmd.getRequestCategory(),cmd.GetSpamLevelAsRequestBehavior<string,RequestCategory>());
                                }
                            }
                        }
                    }

                    // Connection based
                    if (clientInfoValid[i] != client.ClientInfo[i].InfoValid) { 
                        this.demoRateLimiters[i] = new DemoRequestRateLimiter(); // Not part of infopool because its unique to each connection.
                    }

                    if (client.ClientInfo[i].InfoValid && infoPool.playerInfo[i].name != client.ClientInfo[i].Name)
                    {
                        if (CheckPlayerBlacklist(client.ClientInfo[i].Name))
                        {
                            infoPool.playerInfo[i].chatCommandTrackingStuff.fightBotBlacklist = true;
                        }
                    }

                    infoPool.playerInfo[i].name = client.ClientInfo[i].Name;
                    infoPool.playerInfo[i].model = client.ClientInfo[i].Model;

                    // To track rating of ppl who disco. TODO add more than just name to this.
                    if(client.ClientInfo[i].InfoValid && client.ClientInfo[i].Name != null)
                    {
                        if (!infoPool.ratingsAndNames.ContainsKey(infoPool.playerInfo[i].chatCommandTrackingStuff.rating))
                        {
                            infoPool.ratingsAndNames[infoPool.playerInfo[i].chatCommandTrackingStuff.rating] = new Glicko2RatingInfo();
                        }
                        if (!infoPool.ratingsAndNamesThisGame.ContainsKey(infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rating))
                        {
                            infoPool.ratingsAndNamesThisGame[infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rating] = new Glicko2RatingInfo();
                        }
                        infoPool.ratingsAndNames[infoPool.playerInfo[i].chatCommandTrackingStuff.rating].name = client.ClientInfo[i].Name;
                        infoPool.ratingsAndNames[infoPool.playerInfo[i].chatCommandTrackingStuff.rating].lastSeenActive = DateTime.Now;
                        infoPool.ratingsAndNamesThisGame[infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rating].name = client.ClientInfo[i].Name;
                        infoPool.ratingsAndNamesThisGame[infoPool.playerInfo[i].chatCommandTrackingStuffThisGame.rating].lastSeenActive = DateTime.Now;
                    }

                    clientInfoValid[i] = client.ClientInfo[i].InfoValid;
                    if(!mohMode || mohExpansion || (oldClientInfo[i].InfoValid != client.ClientInfo[i].InfoValid))
                    {
                        // Information about players coming from MOHAA is a bit worthless. 
                        // We can only trust it if it's changing.
                        // AKA: it wasn't valid before and now it is (player connected).
                        // or it was valid before and now it isn't (rare server that does inform us, or got a new gamestate that resetted everything)
                        infoPool.playerInfo[i].infoValid = client.ClientInfo[i].InfoValid;
                        infoPool.playerInfo[i].IsFrozen = false;
                    }
                    infoPool.playerInfo[i].clientNum = client.ClientInfo[i].ClientNum;
                    infoPool.playerInfo[i].confirmedBot = client.ClientInfo[i].BotSkill > (this.SaberModDetected ? 0.1f : -0.5f); // Checking for -1 basically but it's float so be safe. Also, if saber mod is detected, it must be > 0 because sabermod gives EVERY player skill 0 even if not bot.

                    if (!infoPool.playerInfo[i].confirmedBot && infoPool.playerInfo[i].team != Team.Spectator && infoPool.playerInfo[i].infoValid)
                    {
                        anyNonBotActivePlayers = true;
                    }

                    infoPool.playerInfo[i].lastClientInfoUpdate = DateTime.Now;
                }
            }
            infoPool.botOnlyGuaranteed = !anyNonBotActivePlayers;
            infoPool.NoActivePlayers = noActivePlayers;
            serverWindow.requestPlayersRefresh();
            //serverWindow.Dispatcher.Invoke(() => {
            //    lock (serverWindow.playerListDataGrid)
            //    {
            //        serverWindow.playerListDataGrid.ItemsSource = null;
            //        serverWindow.playerListDataGrid.ItemsSource = infoPool.playerInfo;
            //    }
            //});


            // Any reason to have this here when it's already in snapshotparsed?
            // The one in snapshotparsed is also more advanced and does more cool stuff like check for bots
            /*if (AlwaysFollowSomeone && ClientNum == SpectatedPlayer) // Not following anyone. Let's follow someone.
            {
                int highestScore = int.MinValue;
                int highestScorePlayer = -1;
                // Pick player with highest score.
                foreach(PlayerInfo player in infoPool.playerInfo)
                {
                    if((DateTime.Now - clientsWhoDontWantToBeSpectated[player.clientNum]).TotalMilliseconds > 120000 && player.infoValid && player.team != Team.Spectator && (player.score.score > highestScore || highestScorePlayer == -1))
                    {
                        highestScore = player.score.score;
                        highestScorePlayer = player.clientNum;
                    }
                }
                if(highestScorePlayer != -1) // Assuming any players at all exist that are playing atm.
                {
                    lastRequestedAlwaysFollowSpecClientNum = highestScorePlayer;
                    leakyBucketRequester.requestExecution("follow " + highestScorePlayer, RequestCategory.FOLLOW, 1, 2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                }
            }*/

            oldClientInfo = (ClientInfo[])client.ClientInfo.Clone();

            snapsEnforcementUpdate();
        }

        private void Browser_InternalTaskStarted(object sender, in Task task, string description)
        {
            TaskManager.RegisterTask(task, $"ServerBrowser (Connection {serverWindow.netAddress}, {serverWindow.ServerName}): {description}");
        }

        public void disconnect()
        {
            // In very very rare cases (some bug?) a weird disconnect can happen
            // And it thinks demo is still recording or sth? So just be clean.
            client.StopRecord_f();
            updateName();
            isRecordingADemo = false;

            client.Disconnected -= Client_Disconnected; // This only handles involuntary disconnects
            Client oldClientForHandler = client; // Since maybe we reconnect straight after this.
            client.Disconnected += (a,b)=> { // Replace handler that auto-reconnects with handler that disposes of client.
                oldClientForHandler.Stop();
                oldClientForHandler.Dispose();
                oldClientForHandler.StopRecord_f();
                try // I'm putting in a try because maybe at this point we are destroying the Connection object so it might not exist anymore later and lead to errors?
                {
                    updateName();
                } catch(Exception e)
                {
                    try
                    {
                        serverWindow.addToLog("Error updating name after disconnect, coulda seen it coming I guess. "+e.ToString(),true);
                    } catch(Exception e2)
                    {
                        // Eh whatever.
                    }
                }
                serverWindow.addToLog("Disconnected.");
            };
            client.Disconnect();
            trulyDisconnected = true;
            client.ServerCommandExecuted -= ServerCommandExecuted;
            client.ServerInfoChanged -= Connection_ServerInfoChanged;
            client.SnapshotParsed -= Client_SnapshotParsed;
            client.EntityEvent -= Client_EntityEvent;
            client.UserCommandGenerated -= Client_UserCommandGenerated;
            client.DebugEventHappened -= Client_DebugEventHappened;
            client.InternalTaskStarted -= Client_InternalTaskStarted;
            client.ErrorMessageCreated -= Client_ErrorMessageCreated;
            client.InternalCommandCreated -= Client_InternalCommandCreated;
            clientStatistics = null;
        }

        List<string> kickInfo = new List<string>();

        List<string> serverCommandsVerbosityLevel0WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect" };
        List<string> serverCommandsVerbosityLevel2WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect","cs" };
        List<string> serverCommandsVerbosityLevel4BlackList = new List<string>() {"scores","tinfo", "newDefered", "pstats" };

        void ServerCommandExecuted(CommandEventArgs commandEventArgs)
        {
            string command = commandEventArgs.Command.Argv(0);

            switch (command.ToLower())
            {
                case "disconnect":
                    if(LastTimeProbablyKicked.HasValue && (DateTime.Now-LastTimeProbablyKicked.Value).TotalMilliseconds < 5000)
                    {
                        LastTimeConfirmedKicked = DateTime.Now;
                        serverWindow.addToLog("KICK DETECTION: Disconnect after kick detection");

                        serverWindow.Dispatcher.BeginInvoke(()=> {

                            lock (kickInfo)
                            {
                                int validClientCount = 0;
                                int privateClientCount = 0;
                                foreach (PlayerInfo pi in infoPool.playerInfo)
                                {
                                    if (pi.infoValid)
                                    {
                                        if (pi.clientNum < serverPrivateClientsSetting)
                                        {
                                            privateClientCount++;
                                        }
                                        validClientCount++;
                                    }
                                }
                                string status = $"Status: {validClientCount} valid clients, {serverMaxClientsLimit} server client limit, {privateClientCount}/{serverPrivateClientsSetting} private clients).";


                                List<string> kickDebugInfo = new List<string>();
                                kickDebugInfo.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                kickDebugInfo.Add(serverWindow.Title);
                                kickDebugInfo.Add(status);
                                kickDebugInfo.AddRange(kickInfo);
                                kickDebugInfo.Add("\n");
                                Helpers.logToSpecificDebugFile(kickDebugInfo.ToArray(), "kickLog.log", true);
                                kickInfo.Clear();
                            }
                        });

                        if ((_connectionOptions.disconnectTriggersParsed & ConnectedServerWindow.ConnectionOptions.DisconnectTriggers.KICKED) > 0)
                        {
                            serverWindow.addToLog("KICK DISCONNECT TRIGGER: Kick detected. Disconnecting.");
                            serverWindow.requestClose(true);
                        }
                    }
                    break;
                case "droperror": // MOH servers sometimes send this with optionally a reason
                    if (commandEventArgs.Command.Argc > 1)
                    {
                        // We're not sure if this was kick, but if it was, add this to kick log?
                        lock(kickInfo) kickInfo.Add(commandEventArgs.Command.RawStringOrConcatenated());

                        if((commandEventArgs.Command.Argv(1)?.Contains("kicked", StringComparison.OrdinalIgnoreCase) == true || commandEventArgs.Command.Argv(1)?.Contains("banned", StringComparison.OrdinalIgnoreCase) == true))
                        {
                            // We have been kicked. Take note.
                            LastTimeProbablyKicked = DateTime.Now;
                            int validClientCount = 0;
                            int privateClientCount = 0;
                            foreach (PlayerInfo pi in infoPool.playerInfo)
                            {
                                if (pi.infoValid)
                                {
                                    if (pi.clientNum < serverPrivateClientsSetting)
                                    {
                                        privateClientCount++;
                                    }
                                    validClientCount++;
                                }
                            }
                            serverWindow.addToLog($"KICK DETECTION: Seems we were kicked (status: {validClientCount} valid clients, {serverMaxClientsLimit} server client limit, {privateClientCount}/{serverPrivateClientsSetting} private clients).");
                        }
                    }
                    lastDropError = DateTime.Now; // MOH, connection was dropped serverside. This precedes possible kick messages which we wanna check for.
                    break;
                case "tinfo":
                    EvaluateTInfo(commandEventArgs);
                    break;
                case "scores":
                    EvaluateScore(commandEventArgs);
                    break;
                case "cs":
                    EvaluateCS(commandEventArgs);
                    break;
                case "fprint":
                case "print":
                    EvaluatePrint(commandEventArgs);
                    break;
                case "stufftext":
                    EvaluateStufftext(commandEventArgs);
                    break;
                case "chat":
                case "tchat":
                    EvaluateChat(commandEventArgs);
                    break;
                case "bequietplease":
                    this.beQuietUntil = DateTime.Now + new TimeSpan(0,10,0); // Server owner requested us to be quiet for a while. Maybe he wants to debug things. Stop sending commands for 10 minutes.
                    break;
                case "map_restart":
                    if (this.HandleAutoCommands) ExecuteCommandList(_connectionOptions.mapChangeCommands, RequestCategory.MAPCHANGECOMMAND);
                    resetThisGameStats();
                    break;
                default:
                    break;
            }

            if (serverWindow.verboseOutput ==5 || 
                (serverWindow.verboseOutput >=4 && !serverCommandsVerbosityLevel4BlackList.Contains(commandEventArgs.Command.Argv(0))) || 
                (serverWindow.verboseOutput >=2 && serverCommandsVerbosityLevel2WhiteList.Contains(commandEventArgs.Command.Argv(0))) || 
                (serverWindow.verboseOutput ==0 && serverCommandsVerbosityLevel0WhiteList.Contains(commandEventArgs.Command.Argv(0)))
                ) { 
                StringBuilder allArgs = new StringBuilder();
                if (serverWindow.showCmdMsgNum)
                {
                    allArgs.Append($"{commandEventArgs.MessageNum}: ");
                }
                for (int i = 0; i < commandEventArgs.Command.Argc; i++)
                {
                    if(mohMode && i == 1 && commandEventArgs.Command.Argv(0) == "print" && commandEventArgs.Command.Argv(1).Length > 0)
                    {
                        switch ((byte)commandEventArgs.Command.Argv(1)[0]) // MOHAA print colors translated to q3 colors
                        {
                            case 1:
                                allArgs.Append("^3"); // Yellow
                                break;
                            case 2:
                                allArgs.Append("^7"); // White
                                break;
                            case 3:
                                allArgs.Append("^7"); // White too
                                break;
                            case 4:
                                allArgs.Append("^1"); // Red (kill messages)
                                break;
                        }
                    }
                    allArgs.Append(commandEventArgs.Command.Argv(i));
                    allArgs.Append(" ");
                }
                if(mohMode && command.Equals("stufftext", StringComparison.OrdinalIgnoreCase))
                {
                    serverWindow.addToLog(allArgs.ToString(),false,60000);
                } else
                {
                    serverWindow.addToLog(allArgs.ToString());
                }
            }
            //addToLog(commandEventArgs.Command.Argv(0)+" "+ commandEventArgs.Command.Argv(1)+" "+ commandEventArgs.Command.Argv(2)+" "+ commandEventArgs.Command.Argv(3));
            Debug.WriteLine(commandEventArgs.Command.Argv(0));

            RunServerCommand(commandEventArgs);
        }

        void EvaluateFlagStatus(string str)
        {
            if (client.ServerInfo.GameType == GameType.CTF || client.ServerInfo.GameType == GameType.CTY)
            {
                // format is rb where its red/blue, 0 is at base, 1 is taken, 2 is dropped
                if (str.Length < 2)
                {
                    // This happens sometimes, for example on NWH servers between 2 games
                    // Server will send cs 23 0 and cs 23 00 in succession, dunno why.
                    // The first one with the single zero is the obvious problem.
                    serverWindow.addToLog("Configstring weirdness, cs 23 had parameter " + str + "(Length " + str.Length + ")");
                    if (str.Length == 1 && str == "0")
                    {
                        infoPool.teamInfo[(int)Team.Red].flag = 0;
                        infoPool.teamInfo[(int)Team.Red].lastFlagUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)Team.Blue].flag = 0;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagUpdate = DateTime.Now;
                    }
                }
                else
                {
                    // If it was picked up or generally status changed, and it was at base before, remember this as the last time it was at base.
                    foreach (int teamToCheck in Enum.GetValues(typeof(Team)))
                    {
                        if (infoPool.teamInfo[teamToCheck].flag == FlagStatus.FLAG_ATBASE)
                        {
                            infoPool.teamInfo[teamToCheck].lastTimeFlagWasSeenAtBase = DateTime.Now;
                        }
                    }
                    int tmp = 0;
                    if(!int.TryParse(str[0].ToString(), out tmp))
                    {
                        serverWindow.addToLog($"ERROR parsing integer in flagstatus configstring [0]: {str}",true);
                    }
                    else
                    {
                        infoPool.teamInfo[(int)Team.Red].flag = tmp == 2 ? FlagStatus.FLAG_DROPPED : (FlagStatus)tmp;
                        infoPool.teamInfo[(int)Team.Red].lastFlagUpdate = DateTime.Now;
                    }
                    //int tmp = int.Parse(str[0].ToString());
                    //tmp = int.Parse(str[1].ToString());
                    if(!int.TryParse(str[1].ToString(), out tmp))
                    {
                        serverWindow.addToLog($"ERROR parsing integer in flagstatus configstring [1]: {str}",true);
                    }
                    else
                    {
                        infoPool.teamInfo[(int)Team.Blue].flag = tmp == 2 ? FlagStatus.FLAG_DROPPED : (FlagStatus)tmp;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagUpdate = DateTime.Now;
                    }
                }

                // Reasoning: If a flag is dropped/in base, and then it is taken and our cam operator knows this, but the current flag carrier isn't updated *yet*, we will otherwise assume the wrong flag carrier.
                // So assume the lastflagcarrier info invalid until it is actually set again anew.
                if (infoPool.teamInfo[(int)Team.Red].flag != FlagStatus.FLAG_TAKEN)
                {
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                }
                if (infoPool.teamInfo[(int)Team.Blue].flag != FlagStatus.FLAG_TAKEN)
                {
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                }
                /*infoPool.redFlag = str[0] - '0';
                infoPool.blueFlag = str[1] - '0';
                if (cgs.isCTFMod && cgs.CTF3ModeActive)
                    cgs.yellowflag = str[2] - '0';
                else
                    cgs.yellowflag = 0;*/
            }
        }

        void EvaluateCS(CommandEventArgs commandEventArgs)
        {
            if (mohMode) return; // MOH Doesn't have flag status.

            int num = commandEventArgs.Command.Argv(1).Atoi();

            switch (num)
            {
                case (int)ConfigStringDefines.CS_FLAGSTATUS:
                    EvaluateFlagStatus(commandEventArgs.Command.Argv(2));
                    break;
                default:break;
            }
        }

        public bool ConnectionLimitReached { get; private set; } = false;
        public DateTime? LastTimeProbablyKicked { get; private set; } = null;
        public DateTime? LastTimeConfirmedKicked { get; private set; } = null;

        private DateTime? lastDropError = null;

        private DateTime lastClientDoesNotWishToBeSpectated = DateTime.Now.AddYears(-1);

        //private DateTime lastInvalidPassword = DateTime.Now.AddYears(-1);
        private DateTime lastNewPasswordTried = DateTime.Now.AddYears(-1);
        private int nextNewPasswordToTryIndex = 0;

        // Parse specs sent through NWH "specs" command.
        // [1] = clientNumSpectator
        // [2] = nameSpectator (with trailing whitespaces)
        // [3] = nameSpectated (with trailing whitespaces)
        // [4] = clientNumSpectated
        Regex specsRegex = new Regex(@"^(\d+)\^3\) \^7 (.*?)\^7   (.*?)\((\d+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        bool skipSanityCheck = false;

        Regex unknownCmdRegex = new Regex(@"^unknown (?:cmd|command) ([^\n]+?)\n\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex unknownCmdRegexMOH = new Regex(@"^Command '([^\n]+?)' not available from console.\n\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex clientInactiveRegex = new Regex(@"Client '?(\d+)'? is not active", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex couldNotFindClientRegex = new Regex(@"Could not find client '?(\d+)'?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex badClientSlotRegex = new Regex(@"Bad client slot: '?(\d+)'?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Regex mohPlayerFrozenRegex = new Regex(@"^\x03(?<team>\w+) player \((?<playerName>.*?)\) frozen. \[(?<location>.*?)\](?:$|\n)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex mohPlayerMeltedRegex = new Regex(@"^\x01(?<team>\w+) player \((?<playerName>.*?)\) melted by (?<melterName>.*?)\. \[(?<location>.*?)\](?:$|\n)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        Regex mohSimpleChatParse = new Regex(@"^\x02(?:\((?<prefix>[^\)]+)\))? ?(?<chatterName>.*?):\s*(?<message>.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Regex playerNameSpecialCharsRegex = new Regex(@"[^\w\d ]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex playerNameSpecialCharsExceptRoofRegex = new Regex(@"[^\w\d\^ ]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        Regex serverUsesProtocolRegex = new Regex(@"Server uses protocol version (?<protocol>\d+).", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        string disconnectedString = " disconnected\n";

        void EvaluatePrint(CommandEventArgs commandEventArgs)
        {
            Match specMatch;
            Match unknownCmdMatch;
            Match mohChatMatch;
            Match mohFrozenMatch = null;
            Match mohMeltedMatch = null;
            Match protocolMatch = null;
            string tmpString = null;
            int tmpInt;
            if (commandEventArgs.Command.Argc >= 2)
            {
                string printText = commandEventArgs.Command.Argv(1);

                if(printText != null && this.HandleAutoCommands)
                {
                    ConditionalCommand[] conditionalCommands = _connectionOptions.conditionalCommandsParsed;
                    foreach (ConditionalCommand cmd in conditionalCommands) // TODO This seems inefficient, hmm
                    {
                        if ((!cmd.mainConnectionOnly || this.IsMainChatConnection) && cmd.type == ConditionalCommand.ConditionType.PRINT_CONTAINS && (cmd.conditionVariable1.Match(printText).Success || cmd.conditionVariable1.Match(Q3ColorFormatter.cleanupString(printText)).Success))
                        {
                            string commands = cmd.commands
                                    .Replace("$myclientnum", this.ClientNum.GetValueOrDefault(-1).ToString(), StringComparison.OrdinalIgnoreCase);
                            ExecuteCommandList(commands, cmd.getRequestCategory(), cmd.GetSpamLevelAsRequestBehavior<string, RequestCategory>());
                        }
                    }
                }

                if (printText != null && printText.Contains("@@@INVALID_PASSWORD")) {

                    if ((DateTime.Now - lastNewPasswordTried).TotalSeconds > 5) {
                        string passwordsString = Helpers.cachedFileRead("passwords.txt");
                        if (!string.IsNullOrWhiteSpace(passwordsString))
                        {

                            string[] passwords = passwordsString.Split(new char[] { '\n','\r' },StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                            if(nextNewPasswordToTryIndex >= passwords.Length)
                            {
                                nextNewPasswordToTryIndex = 0;
                            }
                            if (passwords.Length > 0)
                            {
                                int passwordsConsidered = 0;
                                string passwordToActuallyTry = null;
                                while(passwordsConsidered < passwords.Length)
                                {
                                    string passwordToTry = passwords[nextNewPasswordToTryIndex];
                                    lastNewPasswordTried = DateTime.Now;

                                    string[] parts = passwordToTry.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                    if (parts.Length > 1)
                                    {
                                        NetAddress referenceIP = null;
                                        try
                                        {
                                            referenceIP = NetAddress.FromString(parts[0],default,false);
                                        }
                                        catch (Exception e)
                                        {
                                            serverWindow.addToLog($"Invalid Password Handler, cannot resolve IP {parts[0]}: {e.ToString()}", true);
                                        }
                                        if(referenceIP == serverWindow.netAddress)
                                        {
                                            passwordToActuallyTry = passwordToTry;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        passwordToActuallyTry = passwordToTry;
                                        break;
                                    }

                                    nextNewPasswordToTryIndex++;
                                    passwordsConsidered++;
                                }

                                if(passwordToActuallyTry != null)
                                {
                                    serverWindow.addToLog($"Trying password {nextNewPasswordToTryIndex}", true);
                                    this.SetPassword(passwordToActuallyTry);
                                    nextNewPasswordToTryIndex++;
                                }
                            }
                        }
                    }
                }
                else if((commandEventArgs.Command.Argv(1).Contains("^7Error^1:^7 The client is currently on another map^1.^7") || commandEventArgs.Command.Argv(1).Contains("^7Error^1:^7 The client does not wish to be spectated^1.^7")) && commandEventArgs.Command.Argv(0) == "print")
                {
                    if(lastRequestedAlwaysFollowSpecClientNum >= 0 && lastRequestedAlwaysFollowSpecClientNum<32)
                    {
                        clientsWhoDontWantTOrCannotoBeSpectated[lastRequestedAlwaysFollowSpecClientNum] = DateTime.Now;
                    }
                    lastClientDoesNotWishToBeSpectated = DateTime.Now;
                } else if(commandEventArgs.Command.Argv(1) == "Connection limit reached.\n" || ((commandEventArgs.Command.Argv(1).Contains("Too many connections from the same IP.") || commandEventArgs.Command.Argv(1).Contains("Too many connections from your IP.")) && commandEventArgs.Command.Argv(0) == "print"))
                {
                    ConnectionLimitReached = true;
                } else if ((specMatch = specsRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (specMatch.Groups.Count < 5) return;

                    int spectatingPlayer = -1;
                    int spectatedPlayer = -1;
                    int.TryParse(specMatch.Groups[1].Value, out spectatingPlayer);
                    int.TryParse(specMatch.Groups[4].Value, out spectatedPlayer);

                    //if(spectatingPlayer != -1 && spectatedPlayer != -1)
                    if(spectatingPlayer >= 0 && spectatingPlayer < 32 && spectatedPlayer >= 0 && spectatedPlayer < 32)
                    {
                        // Do sanity check that names match
                        // The regex match has trailing spaces so we just take a substring
                        string spectatingActualName = infoPool.playerInfo[spectatingPlayer].name;
                        string spectatingRegexName = specMatch.Groups[2].Value;
                        string spectatedActualName = infoPool.playerInfo[spectatedPlayer].name;
                        string spectatedRegexName = specMatch.Groups[3].Value;

                        int spectatingStringMaxLength = Math.Min(spectatingActualName.Length, spectatingRegexName.Length);
                        int spectatedStringMaxLength = Math.Min(spectatedActualName.Length, spectatedRegexName.Length);

                        spectatingActualName = spectatingActualName.Substring(0, spectatingStringMaxLength);
                        spectatingRegexName = spectatingRegexName.Substring(0, spectatingStringMaxLength);
                        spectatedActualName = spectatedActualName.Substring(0, spectatedStringMaxLength);
                        spectatedRegexName = spectatedRegexName.Substring(0, spectatedStringMaxLength);

                        if (skipSanityCheck || (spectatingActualName == spectatingRegexName && spectatedActualName == spectatedRegexName && spectatingStringMaxLength > 0 && spectatedStringMaxLength > 0))
                        {
                            infoPool.playerInfo[spectatingPlayer].nwhSpectatedPlayer = spectatedPlayer;
                            infoPool.playerInfo[spectatingPlayer].nwhSpectatedPlayerLastUpdate = DateTime.Now;
                        }
                    }
                } if ((specMatch = badClientSlotRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (specMatch.Groups.Count < 2) return;

                    int inactiveClientNum = -1;
                    int.TryParse(specMatch.Groups[1].Value, out inactiveClientNum);

                    if(inactiveClientNum >= 0 && inactiveClientNum < 32)
                    {
                        clientsWhoDontWantTOrCannotoBeSpectated[inactiveClientNum] = DateTime.Now;
                        infoPool.playerInfo[inactiveClientNum].lastTimeClientInvalid = DateTime.Now;
                    }
                } else if ((specMatch = couldNotFindClientRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (specMatch.Groups.Count < 2) return;

                    int inactiveClientNum = -1;
                    int.TryParse(specMatch.Groups[1].Value, out inactiveClientNum);

                    if(inactiveClientNum >= 0 && inactiveClientNum < 32)
                    {
                        clientsWhoDontWantTOrCannotoBeSpectated[inactiveClientNum] = DateTime.Now;
                        infoPool.playerInfo[inactiveClientNum].lastTimeClientInvalid = DateTime.Now;
                    }
                } else if ((specMatch = clientInactiveRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (specMatch.Groups.Count < 2) return;

                    int inactiveClientNum = -1;
                    int.TryParse(specMatch.Groups[1].Value, out inactiveClientNum);

                    if(inactiveClientNum >= 0 && inactiveClientNum < 32)
                    {
                        clientsWhoDontWantTOrCannotoBeSpectated[inactiveClientNum] = DateTime.Now;
                        infoPool.playerInfo[inactiveClientNum].lastTimeClientInvalid = DateTime.Now;
                    }
                } else if ((protocolMatch = serverUsesProtocolRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (!protocolMatch.Groups.ContainsKey("protocol")) return;

                    int newProtocol = -1;
                    if(client?.Status != ConnectionStatus.Active && int.TryParse(protocolMatch.Groups["protocol"].Value, out newProtocol))
                    {
                        serverWindow.addToLog($"SERVER PROTOCOL CHANGE DETECTED, APPLYING: {protocol} to {newProtocol}", true);
                        protocol = (JKClient.ProtocolVersion)newProtocol;
                        serverWindow.protocol = (JKClient.ProtocolVersion)newProtocol;
                        Reconnect(); // is it safe to call it here? idk. let's find out
                    }
                } else if ((unknownCmdMatch = unknownCmdRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (unknownCmdMatch.Groups.Count < 2) return;

                    string unknownCmd = unknownCmdMatch.Groups[1].Value.Trim().ToLower();


                    if (!infoPool.unsupportedCommands.Contains(unknownCmd)) // This isn't PERFECTLY threadsafe, but it should be fine. Shouldn't end up with too many duplicates.
                    {
                        serverWindow.addToLog($"NOTE: Command {unknownCmd} is not supported by this server. Noting.");
                        infoPool.unsupportedCommands.Add(unknownCmd);
                    }
                } else if ( mohMode && (unknownCmdMatch = unknownCmdRegexMOH.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (unknownCmdMatch.Groups.Count < 2) return;

                    string unknownCmd = unknownCmdMatch.Groups[1].Value.Trim().ToLower();


                    if (!infoPool.unsupportedCommands.Contains(unknownCmd)) // This isn't PERFECTLY threadsafe, but it should be fine. Shouldn't end up with too many duplicates.
                    {
                        serverWindow.addToLog($"NOTE: Command {unknownCmd} is not supported by this MOH server. Noting.");
                        infoPool.unsupportedCommands.Add(unknownCmd);
                    }
                } else if ( mohMode && (mohChatMatch = mohSimpleChatParse.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    if (mohChatMatch.Groups["message"].Success)
                    {
                        string message = mohChatMatch.Groups["message"].Value;
                        int? clientNum = ClientNum;
                        if (clientNum.HasValue)
                        {
                            string myName = infoPool.playerInfo[clientNum.Value].name;
                            if (myName != null && (message.Contains(myName,StringComparison.InvariantCultureIgnoreCase) || message.Contains(playerNameSpecialCharsRegex.Replace(myName,""), StringComparison.InvariantCultureIgnoreCase)))
                            {
                                serverWindow.addToLog($"MOH CHAT MESSAGE POSSIBLY MENTIONS ME: {commandEventArgs.Command.Argv(1)}", false, 0, 0, true);
                            }
                        }
                    }
                } else if ( mohMode && mohFreezeTagDetected && ((mohFrozenMatch = mohPlayerFrozenRegex.Match(commandEventArgs.Command.Argv(1))).Success || (mohMeltedMatch = mohPlayerMeltedRegex.Match(commandEventArgs.Command.Argv(1))).Success))
                {
                    Match relevantMatch = mohMeltedMatch != null ? mohMeltedMatch : mohFrozenMatch;
                    bool frozenStatus = mohMeltedMatch != null ? false : true;
                    if (frozenStatus)
                    {
                        mohFreezeTagSendsFrozenMessages = true;
                    } else
                    {
                        mohFreezeTagSendsMeltedMessages = true;
                    }
                    if (mohFreezeTagSendsFrozenMessages && mohFreezeTagSendsMeltedMessages && relevantMatch.Success)
                    {
                        string playerName = relevantMatch.Groups.ContainsKey("playerName") && relevantMatch.Groups["playerName"].Success ? relevantMatch.Groups["playerName"].Value : null;

                        if(playerName != null)
                        {
                            playerName = playerName.Trim();
                            List<int> clientNumMatches = new List<int>();
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                if(pi.infoValid && pi.name == playerName)
                                {
                                    clientNumMatches.Add(pi.clientNum);
                                    pi.IsFrozen = frozenStatus;
                                    serverWindow.addToLog($"MOH Freeze Tag detection: Player name \"{playerName}\", clientnum {pi.clientNum} frozen status {frozenStatus} from message.",false,0,5);
                                }
                            }
                            if(clientNumMatches.Count == 0)
                            {
                                serverWindow.addToLog($"MOH Freeze Tag detection: Player name \"{playerName}\" matches nobody.");
                            } else if(clientNumMatches.Count == 1)
                            {
                                
                            } else
                            {
                                serverWindow.addToLog($"MOH Freeze Tag detection: Player name \"{playerName}\" matches {clientNumMatches.Count} people.");
                            }
                        }
                    }

                } else if ( mohMode && /*lastDropError.HasValue &&(DateTime.Now- lastDropError.Value).TotalMilliseconds < 2000  &&*/ ClientNum.HasValue && infoPool.playerInfo[ClientNum.Value].name != null && commandEventArgs.Command.Argv(1).Length >= (2+(tmpInt=infoPool.playerInfo[ClientNum.Value].name.Length)) && 
                    (commandEventArgs.Command.Argv(1).Substring(0, tmpInt).Equals(infoPool.playerInfo[ClientNum.Value].name, StringComparison.OrdinalIgnoreCase) ||
                    commandEventArgs.Command.Argv(1).Substring(1, tmpInt).Equals(infoPool.playerInfo[ClientNum.Value].name, StringComparison.OrdinalIgnoreCase)) && 
                    (commandEventArgs.Command.Argv(1).Substring(tmpInt).StartsWith(" was kicked", StringComparison.OrdinalIgnoreCase) ||
                    commandEventArgs.Command.Argv(1).Substring(tmpInt + 1).StartsWith(" was kicked", StringComparison.OrdinalIgnoreCase)||
                    commandEventArgs.Command.Argv(1).Substring(tmpInt + 1).StartsWith(" has been kicked", StringComparison.OrdinalIgnoreCase)||
                    commandEventArgs.Command.Argv(1).Substring(tmpInt).StartsWith(" Kicked", StringComparison.OrdinalIgnoreCase)||
                    commandEventArgs.Command.Argv(1).Substring(tmpInt + 1).StartsWith(" Kicked", StringComparison.OrdinalIgnoreCase)||
                    commandEventArgs.Command.Argv(1).Substring(tmpInt).Equals(" \n", StringComparison.OrdinalIgnoreCase) // Special case for some servers where kicks don't actually send "was kicked". aka reason left empty. idk if this is really right but let's run with it
                    )
                    )
                {
                    // We have been kicked. Take note.
                    lock (kickInfo) kickInfo.Add(commandEventArgs.Command.RawStringOrConcatenated());
                    LastTimeProbablyKicked = DateTime.Now;
                    int validClientCount = 0; 
                    int privateClientCount = 0;
                    foreach (PlayerInfo pi in infoPool.playerInfo)
                    {
                        if (pi.infoValid)
                        {
                            if (pi.clientNum < serverPrivateClientsSetting)
                            {
                                privateClientCount++;
                            }
                            validClientCount++;
                        }
                    }
                    serverWindow.addToLog($"KICK DETECTION: Seems we were kicked (status: {validClientCount} valid clients, {serverMaxClientsLimit} server client limit, {privateClientCount}/{serverPrivateClientsSetting} private clients).");
                } else if (ClientNum.HasValue && infoPool.playerInfo[ClientNum.Value].name != null && commandEventArgs.Command.Argv(1).EndsWithReturnStart("^7 @@@WAS_KICKED\n") == infoPool.playerInfo[ClientNum.Value].name)
                {
                    // We have been kicked. Take note.
                    lock (kickInfo) kickInfo.Add(commandEventArgs.Command.RawStringOrConcatenated());
                    LastTimeProbablyKicked = DateTime.Now;
                    int validClientCount = 0;
                    int privateClientCount = 0;
                    foreach (PlayerInfo pi in infoPool.playerInfo)
                    {
                        if (pi.infoValid) {
                            if (pi.clientNum < serverPrivateClientsSetting)
                            {
                                privateClientCount++;
                            }
                            validClientCount++; 
                        }
                    }
                    serverWindow.addToLog($"KICK DETECTION: Seems we were kicked (status: {validClientCount} valid clients, {serverMaxClientsLimit} server client limit, {privateClientCount}/{serverPrivateClientsSetting} private clients).");
                }/* else if ( mohMode && commandEventArgs.Command.Argv(1).Length > disconnectedString.Length 
                    && commandEventArgs.Command.Argv(1).Substring(commandEventArgs.Command.Argv(1).Length- disconnectedString.Length).Equals(disconnectedString,StringComparison.OrdinalIgnoreCase)
                    )
                {
                    // A player disconnected. Set Infovalid to false. MOHAA doesn't update this stuff for us so we have to..
                    string disconnectedPlayerName = commandEventArgs.Command.Argv(1).Substring(0,commandEventArgs.Command.Argv(1).Length - disconnectedString.Length);
                    bool importantStatusChange = false;
                    foreach (PlayerInfo pi in infoPool.playerInfo)
                    {
                        if(pi.name == disconnectedPlayerName)
                        {
                            serverWindow.addToLog($"MOH DISCONNECT DETECTION: \"{pi.name}\" ({pi.clientNum}) disconnected, setting to invalid.");
                            if (pi.infoValid)
                            {
                                importantStatusChange = true;
                            }
                            pi.infoValid = false; // This might hit the wrong ppl too with duplicate names but oh well. We reset it to true other places if needed.
                        }
                    }
                    if (importantStatusChange)
                    {
                        // Maybe update GUI? Or whatever, fuck it.
                    }
                } */else if ( mohMode && !mohExpansion && (tmpString=commandEventArgs.Command.Argv(1).EndsWithReturnStart(" disconnected\n", " timed out\n", " Server command overflow\n", " was kicked\n"))!= null)
                {
                    // A player disconnected. Set Infovalid to false. MOHAA doesn't update this stuff for us so we have to..
                    string disconnectedPlayerName = tmpString;
                    bool importantStatusChange = false;
                    foreach (PlayerInfo pi in infoPool.playerInfo)
                    {
                        if(pi.name == disconnectedPlayerName)
                        {
                            serverWindow.addToLog($"MOH DISCONNECT DETECTION: \"{pi.name}\" ({pi.clientNum}) disconnected, setting to invalid.");
                            if (pi.infoValid)
                            {
                                importantStatusChange = true;
                            }
                            pi.infoValid = false; // This might hit the wrong ppl too with duplicate names but oh well. We reset it to true other places if needed.
                        }
                    }
                    if (importantStatusChange)
                    {
                        // Maybe update GUI? Or whatever, fuck it.
                    }
                }
            }
        }


        static readonly HashSet<string> mohCmds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
            // Cmds
            "ui_removehud", "-back", "-moveright", "-leanright", "cvarlist", "-moveleft", "-scores", "+scores", "record", "-use", "ui_addhud", "ui_hud", "vote", "callvote", "pushmenu_weaponselect", "pushmenu_teamselect", "instamsg_group_e", "instamsg_group_d", "instamsg_group_c", "instamsg_group_b", "instamsg_group_a", "instamsg_main", "wisper", "sayone", "sayprivate", "teamsay", "sayteam", "say", "messagemode_private", "messagemode_team", "messagemode_all", "messagemode", "editchshader", "getchshader", "resetvss", "loademitter", "dumpemitter", "deleteemittercommand", "newemittercommand", "nextemittercommand", "prevemittercommand", "triggertestemitter", "testemitter", "cg_dumpallclasses", "cg_dumpclassevents", "cg_classevents", "cg_classtree", "cg_classlist", "cg_pendingevents", "cg_dumpevents", "cg_eventhelp", "cg_eventlist", "sizedown", "sizeup", "viewpos", "toggleitem", "weapdrop", "holster", "uselast", "weapprev", "weapnext", "useweaponclass", "ter_restart", "-attackprimary", "connect", "popmenu", "gotoreturnmenu", "wait", "pushmenu", "disconnect", "-moveup", "widgetcommand", "set", "devcon", "setreturnmenu", "-statistics", "+statistics", "ui_getplayermodel", "ui_applyplayermodel", "playermodel", "finishloadingscreen", "startserver", "locationprint", "centerprint", "ui_checkrestart", "ui_resetcvars", "clear", "ui_testlist", "ui_loadconsolepos", "ui_saveconsolepos", "ui_hidemouse", "ui_showmouse", "inv_restart", "editspecificshader", "editshader", "editscript", "notepad", "soundpicker", "lod_spawnlist", "viewspawnlist", "ui_startdmmap", "dmmapselect", "maplist", "loadmenu", "togglemenu", "listmenus", "globalwidgetcommand", "hidemenu", "showmenu", "forcemenu", "pushmenu_dm", "pushmenu_sp", "tmstop", "tmstartloop", "tmstart", "pitch", "playsong", "loadsoundtrack", "stopmp3", "playmp3", "sounddump", "soundinfo", "soundlist", "play", "ff_disable", "r_infoworldtris", "r_infostaticmodels", "farplane_info", "gfxinfo", "screenshot", "modelist", "modellist", "shaderlist", "imagelist", "cl_dumpallclasses", "cl_dumpclassevents", "cl_classevents", "cl_classtree", "cl_classlist", "cl_pendingevents", "cl_dumpevents", "cl_eventhelp", "cl_eventlist", "playdemo", "fastconnect", "aliasdump", "dialog", "saveshot", "vidmode", "tiki", "animlist", "tikilist", "tikianimlist", "ping", "setenv", "rcon", "localservers", "reconnect", "menuconnect", "stoprecord", "cinematic", "vid_restart", "snd_restart", "clientinfo", "configstrings", "cmd", "-cameralook", "+cameralook", "+togglemouse", "-mlook", "+mlook", "-button14", "+button14", "-button13", "+button13", "-button12", "+button12", "-button11", "+button11", "-button10", "+button10", "-button9", "+button9", "-button8", "+button8", "-button7", "+button7", "-button6", "+button6", "-button5", "+button5", "-button4", "+button4", "-button3", "+button3", "-button2", "+button2", "-button1", "+button1", "-button0", "+button0", "-speed", "+speed", "+leanright", "-leanleft", "+leanleft", "+use", "-attacksecondary", "+attacksecondary", "+attackprimary", "-attack", "+attack", "+moveright", "+moveleft", "-strafe", "+strafe", "-lookdown", "+lookdown", "-lookup", "+lookup", "+back", "-forward", "+forward", "-right", "+right", "-left", "+left", "-movedown", "+movedown", "+moveup", "centerview", "difficultyHard", "difficultyMedium", "difficultyEasy", "loadlastgame", "loadgame", "autosavegame", "savegame", "killserver", "gamemap", "devmap", "map", "spdevmap", "spmap", "sectorlist", "restart", "dumpuser", "systeminfo", "serverinfo", "status", "clientkick", "kick", "heartbeat", "midiinfo", "net_restart", "in_restart", "pause", "writeconfig", "changeVectors", "quit", "exec", "bind", "alias", "seta", "unbindall", "touchFile", "cd", "fdir", "dir", "path", "ctrlbindlist", "altbindlist", "bindlist", "unctrlbind", "ctrlbind", "unaltbind", "altbind", "unbind", "append", "scale", "subtract", "add", "cvar_savegame_restart", "cvar_restart", "reset", "setu", "sets", "toggle", "echo", "vstr", "meminfo",
        //};
        //static readonly HashSet<string> mohCvars = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
            // Cvars
            "g_immediateswitch","r_debugSurfaceUpdate","ui_weaponsign","thereisnomonkey","ui_name","ui_disp_playergermanmodel","ui_dm_playergermanmodel","ui_disp_playermodel","ui_dm_playermodel","ui_timemessage","debugSound","viewmodelentity","g_aimLagTime","ter_count","ter_cautiousframes","ter_lock","ter_cull","cg_scoreboardpicover","cg_scoreboardpic","cg_obj_axistext3","cg_obj_axistext2","cg_obj_axistext1","cg_obj_alliedtext3","cg_obj_alliedtext2","cg_obj_alliedtext1","cg_maxclients","cg_timelimit","cg_fraglimit","cg_treadmark_test","cg_te_numCommands","cg_te_currCommand","cg_te_mode_name","cg_te_mode","cg_te_emittermodel","cg_te_zangles","cg_te_yangles","cg_te_xangles","cg_te_tag","cg_te_singlelinecommand","cg_te_command_time","cg_te_alignstretch_scale","cg_te_cone_height","cg_te_spawnrange_b","cg_te_spawnrange_a","cg_te_spritegridlighting","cg_te_varycolor","cg_te_friction","cg_te_radial_max","cg_te_radial_min","cg_te_radial_scale","cg_te_avelamp_r","cg_te_avelamp_y","cg_te_avelamp_p","cg_te_avelbase_r","cg_te_avelbase_y","cg_te_avelbase_p","cg_te_swarm_delta","cg_te_swarm_maxspeed","cg_te_swarm_freq","cg_te_axisoffsamp_z","cg_te_axisoffsamp_y","cg_te_axisoffsamp_x","cg_te_axisoffsbase_z","cg_te_axisoffsbase_y","cg_te_axisoffsbase_x","cg_te_randaxis","cg_te_volumetric","cg_te_forwardvel","cg_te_clampvelaxis","cg_te_clampvelmax_z","cg_te_clampvelmin_z","cg_te_clampvelmax_y","cg_te_clampvelmin_y","cg_te_clampvelmax_x","cg_te_clampvelmin_x","cg_te_randvelamp_z","cg_te_randvelamp_y","cg_te_randvelamp_x","cg_te_randvelbase_z","cg_te_randvelbase_y","cg_te_randvelbase_x","cg_te_anglesamp_r","cg_te_anglesamp_y","cg_te_anglesamp_p","cg_te_anglesbase_r","cg_te_anglesbase_y","cg_te_anglesbase_p","cg_te_offsamp_z","cg_te_offsamp_y","cg_te_offsamp_x","cg_te_offsbase_z","cg_te_offsbase_y","cg_te_offsbase_x","cg_te_randomroll","cg_te_collision","cg_te_flickeralpha","cg_te_align","cg_te_radius","cg_te_insphere","cg_te_sphere","cg_te_circle","cg_te_scalerate","cg_te_spawnrate","cg_te_fadein","cg_te_fadedelay","cg_te_fade","cg_te_count","cg_te_accel_z","cg_te_accel_y","cg_te_accel_x","cg_te_model","cg_te_scalemax","cg_te_scalemin","cg_te_scale","cg_te_bouncefactor","cg_te_dietouch","cg_te_life","cg_rain_drawcoverage","vss_lighting_fps","vss_default_b","vss_default_g","vss_default_r","vss_gridsize","vss_maxvisible","vss_movement_dampen","vss_wind_strength","vss_wind_z","vss_wind_y","vss_wind_x","vss_showsources","vss_color","vss_repulsion_fps","vss_physics_fps","vss_draw","cg_effect_physicsrate","cg_showtempmodels","cg_showemitters","cg_eventstats","cg_timeevents","cg_eventlimit","cg_showevents","cg_voicechat","vm_lean_lower","vm_offset_upvel","vm_offset_vel_up","vm_offset_vel_side","vm_offset_vel_front","vm_offset_vel_base","vm_offset_shotguncrouch_up","vm_offset_shotguncrouch_side","vm_offset_shotguncrouch_front","vm_offset_rocketcrouch_up","vm_offset_rocketcrouch_side","vm_offset_rocketcrouch_front","vm_offset_crouch_up","vm_offset_crouch_side","vm_offset_crouch_front","vm_offset_air_up","vm_offset_air_side","vm_offset_air_front","vm_sway_up","vm_sway_side","vm_sway_front","vm_offset_speed","vm_offset_max","cg_drawsvlag","cg_huddraw_force","cg_acidtrip","cg_hitmessages","cg_animationviewmodel","cg_shadowdebug","cg_shadowscount","cg_pmove_msec","cg_smoothClientsTime","cg_smoothClients","cg_debugfootsteps","cg_traceinfo","cg_camerascale","cg_cameraverticaldisplacement","cg_cameradist","cg_cameraheight","cg_3rd_person","cg_lagometer","cg_stereosep","g_synchronousClients","cg_hidetempmodels","cg_stats","cg_showmiss","cg_nopredict","cg_errordecay","cg_debuganimwatch","cg_debuganim","cg_animspeed","cg_marks_max","cgamedll","sv_referencedPakNames","sv_referencedPaks","g_showflypath","ai_pathcheckdist","ai_pathchecktime","ai_debugpath","ai_fallheight","ai_showpath","ai_showallnode","ai_shownode","ai_shownodenums","ai_showroutes_distance","ai_showroutes","cm_ter_usesphere","cm_FCMdebug","cm_FCMcacheall","cm_playerCurveClip","cm_noCurves","cm_noAreas","session","g_eventstats","g_watch","g_timeevents","g_eventlimit","g_showevents","g_showinfo","g_scoreboardpicover","g_scoreboardpic","g_obj_axistext3","g_obj_axistext2","g_obj_axistext1","g_obj_alliedtext3","g_obj_alliedtext2","g_obj_alliedtext1","g_spectate_allow_full_chat","g_spectatefollow_pitch","g_spectatefollow_up","g_spectatefollow_right","g_spectatefollow_forward","g_forceteamspectate","g_gotmedal","g_failed","g_success","g_playerdeltamethod","g_drawattackertime","g_viewkick_dmmult","g_viewkick_roll","g_viewkick_yaw","g_viewkick_pitch","s_debugmusic","pmove_msec","pmove_fixed","g_smoothClients","g_maxintermission","g_forcerespawn","g_forceready","g_doWarmup","g_warmup","g_allowvote","g_rankedserver","ai_debug_grenades","g_ai_soundscale","g_ai_noticescale","g_ai_notifyradius","g_showdamage","g_animdump","g_dropclips","g_droppeditemlife","g_patherror","g_spawnai","g_spawnentities","g_monitorNum","g_monitor","g_vehicle","g_ai","g_scripttrace","g_scriptdebug","g_nodecheck","g_scriptcheck","g_showopcodes","g_showtokens","g_logstats","g_debugdamage","g_debugtargets","g_showautoaim","g_statefile","g_playermodel","g_spiffyvelocity_z","g_spiffyvelocity_y","g_spiffyvelocity_x","g_spiffyplayer","g_numdebugstrings","g_numdebuglinedelays","g_showlookat","g_entinfo","g_showawareness","g_showbullettrace","g_showplayeranim","g_showplayerstate","g_showaxis","g_timeents","g_showmem","sv_crouchspeedmult","sv_dmspeedmult","sv_walkspeed","sv_runspeed","sv_cinematic","sv_waterspeed","sv_waterfriction","sv_stopspeed","sv_friction","sv_showentnums","sv_showcameras","sv_testloc_offset2_z","sv_testloc_offset2_y","sv_testloc_offset2_x","sv_testloc_radius2","sv_testloc_offset_z","sv_testloc_offset_y","sv_testloc_offset_x","sv_testloc_radius","sv_testloc_secondary","sv_testloc_num","sv_showbboxes","sv_drawtrace","sv_traceinfo","sv_gravity","sv_maxvelocity","sv_rollangle","sv_rollspeed","bosshealth","whereami","com_blood","flood_waitdelay","flood_persecond","flood_msgs","nomonsters","g_allowjointime","roundlimit","filterban","maxentities","sv_precache","gamedll","subAlpha","loadingbar","sv_location","sv_debuggamespy","g_inactivekick","g_inactivespectate","g_teamdamage","sv_gamespy","ui_dedicated","ui_multiplayersign","ui_briefingsign","g_lastsave","com_autodialdata","snd_maxdelay","snd_mindelay","snd_chance","snd_volume","snd_mindist","snd_reverblevel","snd_reverbtype","snd_yaw","snd_height","snd_length","snd_width","cg_te_alpha","cg_te_color_g","cg_te_color_r","cg_te_color_b","cg_te_filename","cam_angles_yaw","cam_angles_pitch","cam_angles_roll","viewmodelactionweight","viewmodelnormaltime","viewmodelanimnum2","viewmodelblend","viewmodelanimslot","viewmodelsyncrate","subteam3","subtitle3","subteam2","subtitle2","subteam1","subtitle1","subteam0","subtitle0","cg_hud","dlg_badsave","ui_startmap","cl_movieaudio","cl_greenfps","ui_returnmenu","ui_failed","ui_success","ui_gotmedal","ui_gmboxspam","ui_NumShotsFired","ui_NumHits","ui_NumComplete","ui_NumObjectives","ui_Accuracy","ui_PreferredWeapon","ui_NumHitsTaken","ui_NumObjectsDestroyed","ui_NumEnemysKilled","ui_HeadShots","ui_TorsoShots","ui_LeftLegShots","ui_RightLegShots","ui_LeftArmShots","ui_RightArmShots","ui_GroinShots","ui_GunneryEvaluation","ui_health_end","ui_health_start","ui_drawcoords","ui_inventoryfile","ui_newvidmode","ui_compass","ui_debugload","soundoverlay","ui_itemsbar","ui_weaponsbartime","ui_weaponsbar","ui_consoleposition","ui_gmbox","ui_minicon","s_obstruction_cal_time","s_show_sounds","s_show_num_active_sounds","s_show_cpu","s_initsound","s_dialogscale","s_testsound","s_show","s_mixPreStep","s_loadas8bit","s_separation","s_ambientvolume","net_port","net_ip","net_socksPassword","net_socksUsername","net_socksPort","net_socksServer","net_socksEnabled","net_noipx","net_noudp","graphshift","graphscale","graphheight","debuggraph","timegraph","ff_disabled","ff_developer","ff_ensureShake","ff_defaultTension","use_ff","dcl_texturescale","dcl_maxoffset","dcl_minsegment","dcl_maxsegment","dcl_pathmode","dcl_dostring","dcl_dobmodels","dcl_doterrain","dcl_doworld","dcl_dolighting","dcl_alpha","dcl_b","dcl_g","dcl_r","dcl_rotation","dcl_widthscale","dcl_heightscale","dcl_radius","dcl_shader","dcl_shiftstep","dcl_autogetinfo","dcl_showcurrent","dcl_editmode","r_gfxinfo","r_maskMinidriver","r_allowSoftwareGL","r_loadftx","r_loadjpg","ter_fastMarks","ter_minMarkRadius","r_precacheimages","r_static_shadermultiplier3","r_static_shadermultiplier2","r_static_shadermultiplier1","r_static_shadermultiplier0","r_static_shaderdata3","r_static_shaderdata2","r_static_shaderdata1","r_static_shaderdata0","r_sse","r_showportal","vss_smoothsmokelight","r_debuglines_depthmask","r_useglfog","r_lightcoronasize","r_farplane_nofog","r_farplane_nocull","r_farplane_color","r_farplane","r_skyportal_origin","r_skyportal","r_light_showgrid","r_light_nolight","r_light_int_scale","r_light_sun_line","r_light_lines","r_stipplelines","r_maxtermarks","r_maxpolyverts","r_maxpolys","r_entlight_maxcalc","r_entlight_cubefraction","r_entlight_cubelevel","r_entlight_errbound","r_entlight_scale","r_entlightmap","r_noportals","r_lockpvs","r_drawBuffer","r_offsetunits","r_offsetfactor","r_clear","r_showstaticbboxes","r_showhbox","r_shownormals","r_showsky","r_showtris","r_nobind","r_debugSurface","r_logFile","r_verbose","r_speeds","r_showcluster","r_novis","r_showcull","r_nocull","r_ignore","r_staticlod","r_drawspherelights","r_drawsprites","r_drawterrain","r_drawbrushmodels","r_drawbrushes","r_drawstaticmodelpoly","r_drawstaticmodels","r_drawentitypoly","r_drawentities","r_norefresh","r_measureOverdraw","r_skipBackEnd","r_showSmp","r_flareFade","r_flareSize","r_portalOnly","r_lightmap","r_drawworld","r_nocurves","r_printShaders","r_debugSort","lod_tool","lod_position","lod_save","lod_tris","lod_metric","lod_tikiname","lod_meshname","lod_mesh","lod_zee_val","lod_pitch_val","lod_curve_4_slider","lod_curve_3_slider","lod_curve_2_slider","lod_curve_1_slider","lod_curve_0_slider","lod_curve_4_val","lod_curve_3_val","lod_curve_2_val","lod_curve_1_val","lod_curve_0_val","lod_edit_4","lod_edit_3","lod_edit_2","lod_edit_1","lod_edit_0","lod_LOD_slider","lod_maxLOD","lod_minLOD","lod_LOD","r_showstaticlod","r_showlod","r_showImages","r_directedScale","r_ambientScale","r_primitives","r_facePlaneCull","r_swapInterval","r_finish","r_dlightBacks","r_fastsky","r_ignoreGLErrors","r_znear","r_lodCurveError","r_lerpmodels","r_singleShader","g_numdebuglines","r_intensity","r_mapOverBrightBits","r_fullbright","r_displayRefresh","r_ignoreFastPath","r_smp","r_vertexLight","r_customaspect","r_ignorehwgamma","r_overBrightBits","r_depthbits","r_stencilbits","r_stereo","r_textureDetails","r_colorMipLevels","r_roundImagesDown","r_reset_tc_array","r_geForce3WorkAround","r_ext_aniso_filter","r_ext_texture_env_combine","r_ext_texture_env_add","r_ext_compiled_vertex_array","r_ext_multitexture","r_ext_gamma_control","r_allowExtensions","r_glDriver","dm_playergermanmodel","password","m_invert_pitch","cg_forceModel","cl_maxPing","cg_autoswitch","cg_gametype","cl_langamerefreshstatus","cl_motdString","m_filter","m_side","m_up","m_forward","m_yaw","m_pitch","cl_allowDownload","cl_showmouserate","cl_mouseAccel","freelook","cl_run","cl_packetdup","cl_anglespeedkey","cl_pitchspeed","cl_yawspeed","rconAddress","cl_forceavidemo","cl_avidemo","activeAction","cl_freezeDemo","cl_showTimeDelta","cl_showSend","cl_shownet","cl_timeNudge","cl_connect_timeout","cl_timeout","cl_cdkey","cl_motd","cl_eventstats","cl_timeevents","cl_eventlimit","cl_showevents","cl_debugMove","cl_nodelta","sv_deeptracedebug","sv_drawentities","sv_mapChecksum","sv_killserver","sv_padPackets","sv_showloss","sv_reconnectlimit","sv_master5","sv_master4","sv_master3","sv_master2","sv_master1","sv_allowDownload","nextmap","sv_zombietime","sv_timeout","sv_fps","sv_privatePassword","rconPassword","sv_paks","sv_pure","sv_serverid","g_gametypestring","g_gametype","sv_maplist","sv_floodProtect","sv_maxPing","sv_minPing","sv_maxRate","sv_maxclients","sv_hostname","sv_privateClients","mapname","protocol","sv_keywords","timelimit","fraglimit","dmflags","skill","g_maxplayerhealth","net_multiLANpackets","net_qport","showdrop","showpackets","in_disablealttab","joy_threshold","in_debugjoystick","in_joyBallScale","in_joystick","in_mouse","in_mididevice","in_midichannel","in_midi","username","sys_cpuid","sys_cpustring","win_wndproc","win_hinstance","arch","arch_minor_version","arch_major_version","shortversion","version","com_buildScript","cl_running","sv_running","dedicated","timedemo","com_speeds","viewlog","com_dropsim","com_showtrace","fixedtime","timescale","fps","autopaused","paused","deathmatch","convertAnim","showLoad","low_anim_memory","dumploadedanims","pagememory","ui_legalscreen_stay","ui_legalscreen_fadeout","ui_legalscreen_fadein","ui_titlescreen_stay","ui_titlescreen_fadeout","ui_titlescreen_fadein","ui_skip_legalscreen","ui_skip_titlescreen","ui_skip_eamovie","cl_playintro","g_voiceChat","s_speaker_type","r_uselod","r_drawSun","r_flares","sensitivity","r_gamma","r_textureMode","dm_playermodel","snaps","rate","s_musicvolume","s_volume","vid_ypos","vid_xpos","r_customwidth","r_fullscreen","name","s_milesdriver","r_forceClampToEdge","r_lastValidRenderer","com_maxfps","r_customheight","s_reverb","cl_maxpackets","ui_console","config","r_ext_compressed_textures","r_drawstaticdecals","g_ddayshingleguys","g_ddayfog","g_ddayfodderguys","r_texturebits","r_colorbits","r_picmip","r_mode","cg_marks_add","s_khz","cg_shadows","cg_rain","ter_maxtris","ter_maxlod","ter_error","vss_maxcount","cg_effectdetail","r_lodviewmodelcap","r_lodcap","r_lodscale","r_subdivisions","r_fastentlight","r_fastdlights","cg_drawviewmodel","g_m6l3","g_m6l2","g_m6l1","g_m5l3","g_m5l2","g_m5l1","g_m4l3","g_m4l2","g_m4l1","g_m3l3","g_m3l2","g_m3l1","g_m2l3","g_m2l2","g_m2l1","g_m1l3","g_m1l2","g_m1l1","g_eogmedal2","g_eogmedal1","g_eogmedal0","g_medal5","g_medal4","g_medal3","g_medal2","g_medal1","g_medal0","ui_medalsign","ui_signshader","g_subtitle","g_skill","detail","ui_hostname","ui_maplist_obj","ui_maplist_round","ui_maplist_team","ui_maplist_ffa","ui_inactivekick","ui_inactivespectate","ui_connectip","ui_teamdamage","ui_timelimit","ui_fraglimit","ui_gamespy","ui_maxclients","ui_gametypestring","ui_gametype","ui_dmmap","ui_voodoo","cl_ctrlbindings","cl_altbindings","ui_crosshair","viewsize","journal","fs_filedir","mapdir","logfile","fs_restrict","fs_game","fs_basepath","fs_cdpath","fs_copyfiles","fs_debug","developer","cheats"
        };
        static readonly Dictionary<string,string> mohFixed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"r_useglfog","1"},
            {"r_ext_multitexture","1"},
            {"cl_motdString",""},
            {"sv_mapChecksum",""},
            {"sv_paks",""},
            //{"sv_serverid","34645374"},
            {"mapname","nomap"},
            {"protocol","8"},
            {"win_wndproc","4754704"},
            {"win_hinstance","4194304"},
            {"shortversion","1.11"},
            {"version","Medal of Honor Allied Assault 1.11 win-x86 Mar  5 2002"},
            {"cl_running","1"},
            {"sv_running","0"},
            {"paused","0"},
        };
        void EvaluateStufftext(CommandEventArgs commandEventArgs)
        {
            if (!mohMode) return;

            //return; // It doesn't actually matter. I set the userinfo stuff and server doesn't care, keeps sending stufftext. Shrug.
            StringBuilder stuffParams = new StringBuilder();
            for(int i = 1; i < commandEventArgs.Command.Argc; i++)
            {
                if(i > 1)
                {
                    stuffParams.Append(" ");
                }
                stuffParams.Append(commandEventArgs.Command.Argv(i));
            }
            string[] commands = stuffParams.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach(string command in commands)
            {
                string[] commandParts = command.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                if (commandParts[0].Equals("setu",StringComparison.OrdinalIgnoreCase) && commandParts.Length > 2)
                {
                    /*bool isAllowed = false;
                    foreach (string allowedUserInfoStufftext in stuffTextSetuWhiteList)
                    {
                        if (allowedUserInfoStufftext.Equals(commandParts[1], StringComparison.OrdinalIgnoreCase))
                        {
                            isAllowed = true;
                            break;
                        }
                    }
                    if (isAllowed)*/
                    //if(mohMode)
                    //{ // Whatever, just do it.
                    string newValue = commandParts[2];
                    string key = commandParts[1].Trim();
                    if (key.Equals("rate",StringComparison.OrdinalIgnoreCase) || key.Equals("snaps", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Dont wanna change these :) Rest is fair game.
                    }
                    if (mohFixed.ContainsKey(key))
                    {
                        newValue = mohFixed[key];
                    }
                    bool valueIsSetYet = false; // Value could be an empty string too in which case we can't compare properly because client will say its an empty string as well. But setting an empty string is not the same as not setting at all.
                    string oldValue = this.client?.GetUserInfoKeyValue(commandParts[1], out valueIsSetYet);
                    if(oldValue != newValue || !valueIsSetYet)
                    {
                        serverWindow.addToLog($"Stufftext userinfo cvar setter executed: {command}");
                        this.client?.SkipUserInfoUpdatesAfterNextNChanges(1); // The real game also doesn't update this immediately, only next time userinfo is sent.
                        this.client?.SetUserInfoKeyValue(key, newValue);
                    }
                    //}
                } else if (!mohCmds.Contains(commandParts[0]))
                {
                    serverWindow.addToLog($"Stufftext non-client command detected, echoing: {command}");
                    leakyBucketRequester.requestExecution(command.Trim(),RequestCategory.STUFFTEXTECHO,3,0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }
            }
        }
        
        
        
        void EvaluateTInfo(CommandEventArgs commandEventArgs)
        {
            int i;
            int theClient;

            int numSortedTeamPlayers = commandEventArgs.Command.Argv(1).Atoi();


            int PWRedFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_REDFLAG : (int)JOStuff.ItemList.powerup_t.PW_REDFLAG;
            int PWBlueFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_BLUEFLAG : (int)JOStuff.ItemList.powerup_t.PW_BLUEFLAG;

            for (i = 0; i < numSortedTeamPlayers; i++)
            {
                theClient = commandEventArgs.Command.Argv(i * 6 + 2).Atoi();

                //sortedTeamPlayers[i] = client;

                if(theClient < 0 || theClient >= 32)
                {
                    serverWindow.addToLog("TeamInfo client weird number "+theClient.ToString());
                } else { 

                    infoPool.playerInfo[theClient].location = commandEventArgs.Command.Argv(i * 6 + 3).Atoi();
                    infoPool.playerInfo[theClient].health = commandEventArgs.Command.Argv(i * 6 + 4).Atoi();
                    infoPool.playerInfo[theClient].armor = commandEventArgs.Command.Argv(i * 6 + 5).Atoi();
                    infoPool.playerInfo[theClient].curWeapon = commandEventArgs.Command.Argv(i * 6 + 6).Atoi();
                    infoPool.playerInfo[theClient].powerUps = commandEventArgs.Command.Argv(i * 6 + 7).Atoi(); // 2/3 places where powerups is transmitted
                    if (((infoPool.playerInfo[i].powerUps & (1 << PWRedFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                    }
                    else if (((infoPool.playerInfo[i].powerUps & (1 << PWBlueFlag)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                    }
                }
            }
        }

        Team mohTeamToRealTeam(TeamMOH mohTeam)
        {
            switch (mohTeam)
            {
                case TeamMOH.Spectator:
                default:
                    return Team.Spectator;
                case TeamMOH.Axis:
                    return Team.Red;
                case TeamMOH.Allies:
                    return Team.Blue;
                case TeamMOH.FreeForAll:
                    return Team.Free;
            }
        }
        int mohTimeStringToSeconds(string timeString)
        {
            string[] parts = timeString.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if(parts.Length == 0)
            {
                return 0;
            } else if (parts.Length == 1)
            {
                return parts[0].Atoi();
            } else if(parts.Length == 2)
            {
                return parts[0].Atoi()*60+ parts[1].Atoi();
            }else if(parts.Length == 3)
            {
                return parts[0].Atoi()*3600+parts[1].Atoi()*60+ parts[2].Atoi();
            } else
            {
                serverWindow.addToLog($"MOH time string parsing error. More than 3 parts: {timeString}",true);
                return 0;
            }
        }

        enum TeamMOH {
            None,
            Spectator,
            FreeForAll,
            Allies,
            Axis
        }
        // Based on OpenMOHAA
        unsafe void EvaluateScoreMOH(CommandEventArgs commandEventArgs) // TODO This is only v6 btw. Aka base MOHAA, no extensions
        {
            int i;
            int iEntryCount;
            TeamMOH iClientTeam;
            int iClientNum;
            int iDatumCount;
            TeamMOH iMatchTeam;
            int iCurrentEntry;
            bool bIsDead, bIsHeader;
            string szString2 = null;
            string szString3 = null;
            string szString4 = null;
            string szString5 = null;
            string szString6 = null;

            iMatchTeam = (TeamMOH)(-1);

            bool isMOHExtension = this.protocol > ProtocolVersion.Protocol8;


            iCurrentEntry = 1;
            if (currentGameType > GameType.FFA)
            {
                iDatumCount = 6;
                iMatchTeam = (TeamMOH)lastPlayerState.Stats[20];
                if (iMatchTeam != TeamMOH.Allies && iMatchTeam != TeamMOH.Axis)
                {
                    iMatchTeam = TeamMOH.Allies;
                }
            }
            else
            {
                // free-for-all
                iDatumCount = 5;
            }

            iEntryCount = commandEventArgs.Command.Argv(iCurrentEntry++).Atoi();
            if (iEntryCount > 64)
            {
                iEntryCount = 64;
            }

            if (currentGameType == GameType.TOW)
            {
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_allied_obj1
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_allied_obj2
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_allied_obj3
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_allied_obj4
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_allied_obj5
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_axis_obj1
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_axis_obj2
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_axis_obj3
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_axis_obj4
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // tow_axis_obj5
            }

            if (currentGameType == GameType.Liberation)
            {
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // scoreboard_toggle1
                commandEventArgs.Command.Argv(iCurrentEntry++).Atof(); // scoreboard_toggle2
            }

            if (iEntryCount * iDatumCount > (commandEventArgs.Command.Argc - 2))
            {
                // Shouldn't happen I think, I added this as a precaution
                serverWindow.addToLog($"MOH Scoreboard error: {iEntryCount} entries a {iDatumCount} numbers but only {commandEventArgs.Command.Argc} total command parameters.", true, 1000 * 10); // Only show this error once per hour.
                return;
            }

            Team lastTeamHeader = (Team)(-1);
            for (i = 0; i < iEntryCount; ++i)
            {
                Team realTeam = (Team)(-1);

                bIsHeader = false;
                if (currentGameType > GameType.FFA)
                {
                    iClientNum = commandEventArgs.Command.Argv(iCurrentEntry + iDatumCount * i).Atoi();
                    iClientTeam = (TeamMOH) commandEventArgs.Command.Argv(1 + iCurrentEntry + iDatumCount * i).Atoi();
                    if (iClientTeam >= 0)
                    {
                        bIsDead = false;
                    }
                    else
                    {
                        bIsDead = true;
                        iClientTeam = (TeamMOH)(-(int)iClientTeam);
                    }

                    realTeam = mohTeamToRealTeam(iClientTeam);



                    if (iClientNum == -1)
                    {
                        bIsHeader = true;

                        switch ((int)iClientTeam)
                        {
                            case 1:
                                szString2 = "Spectators";
                                lastTeamHeader = Team.Spectator;
                                break;
                            case 2:
                                szString2 = "Free-For-Allers";
                                lastTeamHeader = Team.Free;
                                break;
                            case 3:
                                szString2 = "Allies";
                                lastTeamHeader = Team.Blue;
                                if (isMOHExtension)
                                {
                                    iCurrentEntry++; // Has the player count as well.
                                }
                                break;
                            case 4:
                                szString2 = "Axis";
                                lastTeamHeader = Team.Red;
                                if (isMOHExtension)
                                {
                                    iCurrentEntry++; // Has the player count as well.
                                }
                                break;
                            default:
                                szString2 = "No Team"; // ?!
                                lastTeamHeader = Team.Spectator;
                                break;
                        }
                    }
                    else if (iClientNum == -2)
                    {
                        // spectating !!?
                        szString2 = "";
                        lastTeamHeader = Team.Spectator; // ?!
                    }
                    else if (iClientNum >= 0 && iClientNum < 64)
                    {
                        szString2 = infoPool.playerInfo[iClientNum].name;
                        lastTeamHeader = Team.Spectator; // ?!
                    } else
                    {
                        serverWindow.addToLog($"ParseScoresMOH > FFA: iClientNum is {iClientNum}, wtf.");
                    }

                    if (!bIsHeader && iClientNum >= 0 && iClientNum < 64)
                    {
                        if (!infoPool.playerInfo[iClientNum].infoValid)
                        {
                            serverWindow.addToLog($"Retrieved scoreboard entry for player {iClientNum} but player {iClientNum}'s infoValid is false. Player name: {infoPool.playerInfo[iClientNum].name}. Setting to true.");
                        }
                        if(!mohExpansion) infoPool.playerInfo[iClientNum].infoValid = true;
                        if (!mohFreezeTagDetected || bIsDead) // Freeze-Tag doesn't get proper death info in scoreboard. It does seem to get it short-term, so we can count "dead" as reliable, but not "alive".
                        { // Freeze tag breaks the alive status in scoreboards for some reason
                            infoPool.playerInfo[iClientNum].IsAlive = !bIsDead;
                            infoPool.playerInfo[iClientNum].lastAliveStatusUpdated = DateTime.Now;
                        }
                        infoPool.playerInfo[iClientNum].team = realTeam;
                        infoPool.playerInfo[iClientNum].score.kills = commandEventArgs.Command.Argv(2 + iCurrentEntry + iDatumCount * i).Atoi();
                        if(currentGameType > GameType.Team)
                        {
                            infoPool.playerInfo[iClientNum].score.totalKills = commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i).Atoi();
                            infoPool.playerInfo[iClientNum].score.score = infoPool.playerInfo[iClientNum].score.totalKills;
                        } else
                        {
                            infoPool.playerInfo[iClientNum].score.deaths = commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i).Atoi();
                            infoPool.playerInfo[iClientNum].score.score = infoPool.playerInfo[iClientNum].score.kills;
                        }
                        infoPool.playerInfo[iClientNum].score.time = mohTimeStringToSeconds(commandEventArgs.Command.Argv(4 + iCurrentEntry + iDatumCount * i));
                        string pingString = commandEventArgs.Command.Argv(5 + iCurrentEntry + iDatumCount * i);
                        if (pingString.Trim().Equals("bot", StringComparison.OrdinalIgnoreCase))
                        {

                        } else
                        {
                            infoPool.playerInfo[iClientNum].score.ping = pingString.Atoi();
                        }

                    } else if (bIsHeader && lastTeamHeader >= Team.Free && lastTeamHeader<= Team.Spectator)
                    {
                        infoPool.teamInfo[(int)lastTeamHeader].teamScore = infoPool.teamInfo[(int)lastTeamHeader].teamKills = commandEventArgs.Command.Argv(2 + iCurrentEntry + iDatumCount * i).Atoi();
                        infoPool.teamInfo[(int)lastTeamHeader].teamDeaths = commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i).Atoi();
                        infoPool.teamInfo[(int)lastTeamHeader].teamAveragePing = commandEventArgs.Command.Argv(4 + iCurrentEntry + iDatumCount * i).Atoi();
                    }

                    //szString3 = commandEventArgs.Command.Argv(2 + iCurrentEntry + iDatumCount * i);
                    //szString4 = commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i);
                    //szString5 = commandEventArgs.Command.Argv(4 + iCurrentEntry + iDatumCount * i);
                    //szString6 = commandEventArgs.Command.Argv(5 + iCurrentEntry + iDatumCount * i);

                    if (iClientNum == lastPlayerState.ClientNum)
                    {
                        // This is me
                    }
                    else if (iClientNum == -2)
                    {
                        // No team. Is this the filler stuff? not sure
                    }
                    else if (iClientTeam == TeamMOH.Allies || iClientTeam == TeamMOH.Axis)
                    {
                        if (iClientTeam == iMatchTeam)
                        {
                            // My team
                        }
                        else
                        {
                            // Other team
                        }
                    }
                    else
                    {
                        // No team. Huh? Free for all I guess?
                    }

                    if (bIsDead)
                    {
                        // Dead.;
                    }
                }
                else
                {
                    iClientNum = commandEventArgs.Command.Argv(iCurrentEntry + iDatumCount * i).Atoi();
                    if (iClientNum >= 64)
                    {
                        serverWindow.addToLog($"ParseScoresMOH: iClientNum is {iClientNum}, wtf.");
                    }
                    else if (iClientNum >= 0)
                    {
                        szString2 = infoPool.playerInfo[iClientNum].name;
                        szString3 = commandEventArgs.Command.Argv(1 + iCurrentEntry + iDatumCount * i);
                        szString4 = commandEventArgs.Command.Argv(2 + iCurrentEntry + iDatumCount * i);
                        szString5 = commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i);
                        szString6 = commandEventArgs.Command.Argv(4 + iCurrentEntry + iDatumCount * i);
                    }
                    else
                    {
                        if (iClientNum == -3)
                        {
                            szString2 = "Players";
                            lastTeamHeader = Team.Free;
                            bIsHeader = true;
                        }
                        else if (iClientNum == -2)
                        {
                            szString2 = "Spectators";
                            lastTeamHeader = Team.Spectator;
                            bIsHeader = true;
                        }
                        else
                        {
                            // unknown
                            szString2 = "";
                        }
                        szString3 = "";
                        szString4 = "";
                        szString5 = "";
                        szString6 = "";
                    }

                    if (!bIsHeader && iClientNum >= 0 && iClientNum < 64)
                    {
                        if (!infoPool.playerInfo[iClientNum].infoValid)
                        {
                            serverWindow.addToLog($"Retrieved scoreboard entry for player {iClientNum} but player {iClientNum}'s infoValid is false. Player name: {infoPool.playerInfo[iClientNum].name}. Setting to true.");
                        }
                        if(!mohExpansion) infoPool.playerInfo[iClientNum].infoValid = true;
                        if ((int)lastTeamHeader != -1)
                        {
                            infoPool.playerInfo[iClientNum].team = lastTeamHeader;
                        }
                        infoPool.playerInfo[iClientNum].score.kills = commandEventArgs.Command.Argv(1 + iCurrentEntry + iDatumCount * i).Atoi();
                        infoPool.playerInfo[iClientNum].score.deaths = commandEventArgs.Command.Argv(2 + iCurrentEntry + iDatumCount * i).Atoi();
                        infoPool.playerInfo[iClientNum].score.score = infoPool.playerInfo[iClientNum].score.kills;
                        infoPool.playerInfo[iClientNum].score.time = mohTimeStringToSeconds(commandEventArgs.Command.Argv(3 + iCurrentEntry + iDatumCount * i));
                        string pingString = commandEventArgs.Command.Argv(4 + iCurrentEntry + iDatumCount * i);
                        if (pingString.Trim().Equals("bot", StringComparison.OrdinalIgnoreCase))
                        {

                        }
                        else
                        {
                            infoPool.playerInfo[iClientNum].score.ping = pingString.Atoi();
                        }

                    }

                    if (iClientNum == lastPlayerState.ClientNum)
                    {
                        // This is me
                    }
                    else
                    {
                        // No team. ?!
                    }
                }

                /*cgi.UI_SetScoreBoardItem(
                    i,
                    szString2,
                    szString3,
                    szString4,
                    szString5,
                    szString6,
                    NULL,
                    NULL,
                    NULL,
                    pItemTextColor,
                    pItemBackColor,
                    bIsHeader
                );*/
            }

        }

        void EvaluateScore(CommandEventArgs commandEventArgs)
        {
            if (mohMode)
            {
                EvaluateScoreMOH(commandEventArgs);
                return;
            }

            int i, powerups, readScores;

            readScores = commandEventArgs.Command.Argv(1).Atoi();

            int PWRedFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_REDFLAG : (int)JOStuff.ItemList.powerup_t.PW_REDFLAG;
            int PWBlueFlag = jkaMode ? (int)JKAStuff.ItemList.powerup_t.PW_BLUEFLAG : (int)JOStuff.ItemList.powerup_t.PW_BLUEFLAG;

            infoPool.teamInfo[(int)Team.Red].teamScore = commandEventArgs.Command.Argv(2).Atoi();
            infoPool.ScoreRed = commandEventArgs.Command.Argv(2).Atoi();
            infoPool.teamInfo[(int)Team.Blue].teamScore = commandEventArgs.Command.Argv(3).Atoi();
            infoPool.ScoreBlue = commandEventArgs.Command.Argv(3).Atoi();

            bool anyNonBotPlayerActive = false;
            bool anyPlayersActive = false;


            // Flexible detection of expanded scoreboard data. See how many entries per person we get. 
            // 14 is default. Some JKA mods (japlus and japro) send 15, an additional "killed" count.
            // Likewise, japlus and some mods can send more than the usual MaxClientScoreSend (20) scoreboard
            // players. Normally we would detect the server mod and see if we allow more than 20 players here to avoid weird crashes.
            // But instead we're doing the math to see if the numbers add up. If the offset ends up not working out,
            // we revert to the conservative legacy behavior. Could potentially result in weirdness sometimes but we'll see.
            // We'll throw  a little warning.
            int scoreboardOffset = (commandEventArgs.Command.Argc-4)/readScores;
            if(!(scoreboardOffset == 14 && !this.MBIIDetected) && !(scoreboardOffset == 15 && !this.MBIIDetected) && !(scoreboardOffset == 9 && this.MBIIDetected ))
            {
                if(this.MBIIDetected)
                {
                    serverWindow.addToLog($"Scoreboard error: calculated offset with MB II detected is not 9 (argc:{commandEventArgs.Command.Argc},readScores={readScores},offset:{scoreboardOffset}), wtf? Resetting score count and setting offset 9.", true, 1000 * 60 * 60); // Only show this error once per hour.
                    scoreboardOffset = 9;
                    if (readScores > JKClient.Common.MaxClientScoreSend)
                    {
                        readScores = JKClient.Common.MaxClientScoreSend;
                    }
                }
                else if(this.JAPlusDetected || this.JAProDetected)
                {
                    serverWindow.addToLog($"Scoreboard error: calculated offset is neither 14 nor 15 (argc:{commandEventArgs.Command.Argc},readScores={readScores},offset:{scoreboardOffset}), wtf? But detected japlus or japro. Resetting score count but using offset 15.", true, 1000 * 60 * 60); // Only show this error once per hour.
                    scoreboardOffset = 15;
                    if (readScores > JKClient.Common.MaxClientScoreSend)
                    {
                        readScores = JKClient.Common.MaxClientScoreSend;
                    }
                }
                else
                {
                    serverWindow.addToLog($"Scoreboard error: calculated offset is neither 14 nor 15 (argc:{commandEventArgs.Command.Argc},readScores={readScores},offset:{scoreboardOffset}), wtf? Defaulting to legacy behavior.", true,1000*60*60); // Only show this error once per hour.
                    scoreboardOffset = 14;
                    if (readScores > JKClient.Common.MaxClientScoreSend)
                    {
                        readScores = JKClient.Common.MaxClientScoreSend;
                    }
                }
                if (commandEventArgs.Command.Argc - 4 < readScores * scoreboardOffset)
                {
                    serverWindow.addToLog($"Scoreboard error: Not enough data received even after checks (argc-4:{commandEventArgs.Command.Argc - 4},readScores*offset={readScores * scoreboardOffset}), WTF? Reducing readScores until ok.", true, 1000 * 60 * 60); // Only show this error once per hour.
                    while(commandEventArgs.Command.Argc - 4 < readScores * scoreboardOffset)
                    {
                        readScores--;
                    }
                }
            }

            bool anyRetCounts = false;

            for (i = 0; i < readScores; i++)
            {
                //
                int clientNum = commandEventArgs.Command.Argv(i * scoreboardOffset + 4).Atoi();
                if (clientNum < 0 || clientNum >= client.ClientHandler.MaxClients)
                {
                    continue;
                }
                infoPool.playerInfo[clientNum].score.deathsIsFilled = false;
                if (!this.MBIIDetected) // Wtf is this i hear you say? MBII only has 9. And no, I have no idea which values are what for MBII anyway.
                {
                    infoPool.playerInfo[clientNum].score.shortScoresMBII = false;

                    infoPool.playerInfo[clientNum].score.client = commandEventArgs.Command.Argv(i * scoreboardOffset + 4).Atoi();
                    infoPool.playerInfo[clientNum].score.score = commandEventArgs.Command.Argv(i * scoreboardOffset + 5).Atoi();
                    infoPool.playerInfo[clientNum].score.ping = commandEventArgs.Command.Argv(i * scoreboardOffset + 6).Atoi();
                    infoPool.playerInfo[clientNum].score.time = commandEventArgs.Command.Argv(i * scoreboardOffset + 7).Atoi();
                    infoPool.playerInfo[clientNum].score.scoreFlags = commandEventArgs.Command.Argv(i * scoreboardOffset + 8).Atoi();
                    powerups = commandEventArgs.Command.Argv(i * scoreboardOffset + 9).Atoi();
                    infoPool.playerInfo[clientNum].score.powerUps = powerups; // duplicated from entities?
                    infoPool.playerInfo[clientNum].powerUps = powerups; // 3/3 places where powerups is transmitted
                    infoPool.playerInfo[clientNum].score.accuracy = commandEventArgs.Command.Argv(i * scoreboardOffset + 10).Atoi();
                    infoPool.playerInfo[clientNum].score.impressiveCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 11).Atoi();

                    if (infoPool.playerInfo[clientNum].score.impressiveCount > 0)
                    {
                        anyRetCounts = true;
                    }

                    infoPool.playerInfo[clientNum].score.excellentCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 12).Atoi();

                    infoPool.playerInfo[clientNum].score.guantletCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 13).Atoi();
                    infoPool.playerInfo[clientNum].score.defendCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 14).Atoi();
                    infoPool.playerInfo[clientNum].score.assistCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 15).Atoi();
                    infoPool.playerInfo[clientNum].score.perfect = commandEventArgs.Command.Argv(i * scoreboardOffset + 16).Atoi() == 0 ? false : true;
                    infoPool.playerInfo[clientNum].score.captures = commandEventArgs.Command.Argv(i * scoreboardOffset + 17).Atoi();

                    if (scoreboardOffset == 15)
                    {
                        infoPool.playerInfo[clientNum].score.deaths = commandEventArgs.Command.Argv(i * scoreboardOffset + 18).Atoi();
                        infoPool.playerInfo[clientNum].score.deathsIsFilled = true;
                    }
                } else
                {
                    infoPool.playerInfo[clientNum].score.shortScoresMBII = true;
                    // Scores in MBII appear to be: ClientNum, Ping, Remaining Lives, Score, R, K, D, A, 1(not sure what)
                    infoPool.playerInfo[clientNum].score.client = commandEventArgs.Command.Argv(i * scoreboardOffset + 4).Atoi();
                    infoPool.playerInfo[clientNum].score.ping = commandEventArgs.Command.Argv(i * scoreboardOffset + 5).Atoi();
                    infoPool.playerInfo[clientNum].score.remainingLives = commandEventArgs.Command.Argv(i * scoreboardOffset + 6).Atoi();
                    infoPool.playerInfo[clientNum].score.score = commandEventArgs.Command.Argv(i * scoreboardOffset + 7).Atoi();
                    infoPool.playerInfo[clientNum].score.mbIIrounds = commandEventArgs.Command.Argv(i * scoreboardOffset + 8).Atoi();
                    infoPool.playerInfo[clientNum].score.kills = commandEventArgs.Command.Argv(i * scoreboardOffset + 9).Atoi();
                    infoPool.playerInfo[clientNum].score.deaths = commandEventArgs.Command.Argv(i * scoreboardOffset + 10).Atoi();
                    infoPool.playerInfo[clientNum].score.assistCount = commandEventArgs.Command.Argv(i * scoreboardOffset + 11).Atoi();
                    infoPool.playerInfo[clientNum].score.mbIImysteryValue = commandEventArgs.Command.Argv(i * scoreboardOffset + 12).Atoi();
                }
                infoPool.playerInfo[clientNum].lastScoreUpdated = DateTime.Now;

                if(infoPool.playerInfo[clientNum].score.ping != 0)
                {
                    infoPool.playerInfo[clientNum].score.lastNonZeroPing = DateTime.Now;
                    infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing = 0;
                } else
                {
                    infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing++;
                }
                if (infoPool.playerInfo[clientNum].team != Team.Spectator)
                {
                    anyPlayersActive = true;
                    if (!infoPool.playerInfo[clientNum].confirmedBot && (infoPool.playerInfo[clientNum].score.ping != 0 || infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing < 4)) // Be more safe. Anyone could have ping 0 by freak accident in theory.
                    {
                        anyNonBotPlayerActive = true;
                    }
                }

                if (((infoPool.playerInfo[clientNum].powerUps & (1 << PWRedFlag)) != 0) && infoPool.playerInfo[clientNum].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                {
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrier = clientNum;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                }
                else if (((infoPool.playerInfo[clientNum].powerUps & (1 << PWBlueFlag)) != 0) && infoPool.playerInfo[clientNum].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                {
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrier = clientNum;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                }

                // Serverside:
                //cl->ps.persistant[PERS_SCORE], // score
                //ping, // ping
                //(level.time - cl->pers.enterTime)/60000, // time
                //scoreFlags, // scoreflags
                //g_entities[level.sortedClients[i]].s.powerups, // powerups
                //accuracy,  // accuracy
                //cl->ps.persistant[PERS_IMPRESSIVE_COUNT], // impressive
                //cl->ps.persistant[PERS_EXCELLENT_COUNT], // excellent
                //cl->ps.persistant[PERS_GAUNTLET_FRAG_COUNT],  // gauntlet frag count
                //cl->ps.persistant[PERS_DEFEND_COUNT],  // defend count
                //cl->ps.persistant[PERS_ASSIST_COUNT],  // assist count 
                //perfect, // perfect count 
                //cl->ps.persistant[PERS_CAPTURES] // captures


            }

            infoPool.serverSeemsToSupportRetsCountScoreboard = anyRetCounts; // I had it before in such a way that it would only turn on but not off, but I think it can cause issues (when scoreboard gets parsed wrong or when server sends some other random impressivecount?). Just do it dynamically?

            if (!anyNonBotPlayerActive && anyPlayersActive)
            {
                infoPool.lastBotOnlyConfirmed = DateTime.Now;
            } else
            {
                infoPool.lastBotOnlyConfirmed = null;
            }
            snapsEnforcementUpdate();


            infoPool.lastScoreboardReceived = DateTime.Now;
        }

        int lastDemoIterator = 0;


        public async void startDemoRecord(int iterator=0)
        {
            client.SetExtraDemoMetaData(_connectionOptions.extraDemoMetaParsed);
            shouldBeRecordingADemo = true;
            if(client.Status != ConnectionStatus.Active)
            {
                serverWindow.addToLog("Can't record demo when disconnected. But trying to queue recording in case we connect.");
                return;
            }
            if (isRecordingADemo)
            {
                if (!client.Demorecording)
                {

                    serverWindow.addToLog("isRecordingADemo indicates demo is already being recorded, but client says otherwise? Shouldn't really happen, some bug I guess. Try record anyway...");
                    //isRecordingADemo = false;
                } else
                {

                    serverWindow.addToLog("Demo is already being recorded...");
                    return;
                }
            }

            lastDemoIterator = iterator;

            serverWindow.addToLog("Initializing demo recording...");
            DateTime nowTime = DateTime.Now;
            string timeString = nowTime.ToString("yyyy-MM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(Helpers.MakeValidFileName(timeString + "-" + client.ServerInfo.MapName+"_"+client.ServerInfo.HostName+(iterator==0 ? "" : "_"+(iterator+1).ToString())), client.ServerInfo.Protocol);

            TaskCompletionSource<bool> firstPacketRecordedTCS = new TaskCompletionSource<bool>();

            _ = firstPacketRecordedTCS.Task.ContinueWith((Task<bool> s) =>
            {
                if (mohMode) return; // Can't write message to myself and these commands don't do anything in MOH anyway.

                // Send a few commands that give interesting outputs, nothing more to it.

                // Some or most of these commands won't do anything on many servers.
                // On some servers they might display something
                // clientlist seems to have been a servercommand once, but its now a client command
                // In short, this stuff might not do anything except throw a wrong cmd error
                // But on some servers it might give mildly interesting output.

                // Need a timeout because of flood protection which is roughly speaking 1 command per second
                // We already do a scoreboard command every 2 seconds, so we have about 1 command every 2 seconds left
                // Go 3 seconds here to be safe. We also still need room to make commands for changing the camera angle.
                const int timeoutBetweenCommands = 3000;

                string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss G\\MTzzz");
                ServerInfo curServerInfo = client.ServerInfo;
                string[] serverInfoParts = new string[] { "Recording demo",
                    time,
                    curServerInfo.Address?.ToString(),
                    curServerInfo.HostName,
                    curServerInfo.ServerGameVersionString,
                    curServerInfo.Location,
                    curServerInfo.Game,
                    curServerInfo.GameType.ToString(),
                    curServerInfo.GameName,
                    curServerInfo.MapName,
                    curServerInfo.Protocol.ToString(),
                    curServerInfo.Version.ToString(),
                    "sv_floodProtect: "+curServerInfo.FloodProtect.ToString(),
                    time,}; // Just for the occasional lost chat message, send time a second time. Most useful from these infos.
                serverInfoParts = Helpers.StringChunksOfMaxSize(serverInfoParts,140,", ^7^0^7", "^7^0^7"); // 150 is max message length. We split to 140 size chunks just to be safe
                foreach (string serverInfoPart in serverInfoParts)
                {
                    // Tell some info about the server... to myself
                    // Convenience feature.
                    leakyBucketRequester.requestExecution("tell " + client.clientNum + " \"   "+ serverInfoPart + "\"", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // NWH / CTFMod (?)
                leakyBucketRequester.requestExecution("info", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("afk", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("specs", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                if (client.ServerInfo.GameType == GameType.FFA && client.ServerInfo.NWH == false && !this.jkaMode && !_connectionOptions.silentMode)
                { // replace with more sophisticated detection
                    // doing a detection here to not annoy ctf players.
                    // will still annoy ffa players until better detection.
                    // Show top 10 scores at start of demo recording.
                    leakyBucketRequester.requestExecution("say_team !top", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // TwiMod (DARK etc)
                leakyBucketRequester.requestExecution("ammodinfo", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("ammodinfo_twitch", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                if (client.ServerInfo.GameType == GameType.FFA && client.ServerInfo.NWH == false && !this.jkaMode && !_connectionOptions.silentMode) // Might not be accurate idk
                {
                    leakyBucketRequester.requestExecution("say_team !dimensions", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                    leakyBucketRequester.requestExecution("say_team !where", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // Whatever
                leakyBucketRequester.requestExecution("serverstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientinfo", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientlist", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                /*client.ExecuteCommand("info");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("afk");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("clientstatus");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("specs");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);

                // OC Defrag
                if(client.ServerInfo.GameType == GameType.FFA) { // replace with more sophisticated detection
                    // doing a detection here to not annoy ctf players.
                    // will still annoy ffa players until better detection.
                    client.ExecuteCommand("say_team !top"); // Show top 10 scores at start of demo recording.
                    System.Threading.Thread.Sleep(timeoutBetweenCommands);
                }

                // TwiMod (DARK etc)
                client.ExecuteCommand("ammodinfo"); 
                System.Threading.Thread.Sleep(timeoutBetweenCommands);

                // Whatever
                client.ExecuteCommand("clientinfo");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("clientlist");*/
            });

            bool success = await client.Record_f(new DemoName_t {name= unusedDemoFilename,time=nowTime }, firstPacketRecordedTCS);

            if (success)
            {

                serverWindow.addToLog("Demo recording started.");
                isRecordingADemo = true;
                if (_connectionOptions.demoTimeColorNames)
                {
                    updateName();
                }
            }
            else
            {

                serverWindow.addToLog("Demo recording failed to start for some reason.");
                isRecordingADemo = false;
            }
        }
        public void stopDemoRecord(bool afterInvoluntaryDisconnect = false)
        {
            if (!afterInvoluntaryDisconnect)
            {
                shouldBeRecordingADemo = false;
            }
            serverWindow.addToLog("Stopping demo recording...");
            client.StopRecord_f();
            updateName();
            isRecordingADemo = false;
            serverWindow.addToLog("Demo recording stopped.");
        }

    }

    // means of death
    enum MeansOfDeath {
        MOD_UNKNOWN,
        MOD_STUN_BATON,
        MOD_MELEE,
        MOD_SABER,
        MOD_BRYAR_PISTOL,
        MOD_BRYAR_PISTOL_ALT,
        MOD_BLASTER,
        MOD_DISRUPTOR,
        MOD_DISRUPTOR_SPLASH,
        MOD_DISRUPTOR_SNIPER,
        MOD_BOWCASTER,
        MOD_REPEATER,
        MOD_REPEATER_ALT,
        MOD_REPEATER_ALT_SPLASH,
        MOD_DEMP2,
        MOD_DEMP2_ALT,
        MOD_FLECHETTE,
        MOD_FLECHETTE_ALT_SPLASH,
        MOD_ROCKET,
        MOD_ROCKET_SPLASH,
        MOD_ROCKET_HOMING,
        MOD_ROCKET_HOMING_SPLASH,
        MOD_THERMAL,
        MOD_THERMAL_SPLASH,
        MOD_TRIP_MINE_SPLASH,
        MOD_TIMED_MINE_SPLASH,
        MOD_DET_PACK_SPLASH,
        MOD_FORCE_DARK,
        MOD_SENTRY,
        MOD_WATER,
        MOD_SLIME,
        MOD_LAVA,
        MOD_CRUSH,
        MOD_TELEFRAG,
        MOD_FALLING,
        MOD_SUICIDE,
        MOD_TARGET_LASER,
        MOD_TRIGGER_HURT,
        MOD_MAX
    }
}
