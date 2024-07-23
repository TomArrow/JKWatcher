using JKClient;
using Client = JKClient.JKClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Threading;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Windows.Shell;
using System.IO;

namespace JKWatcher
{


    public class SnapsSettings : INotifyPropertyChanged
    {
        private int _botOnlySnaps = 5;
        private int _emptySnaps = 2;
        private int _afkMaxSnaps = 2;
        private int _pingAdjust = 0;

        public bool pingAdjustActive { get; set; } = false;

        public bool forceBotOnlySnaps { get; set; } = true;
        public int botOnlySnaps { 
            get {
                return _botOnlySnaps;
            } 
            set {
                int fixedValue = Math.Max(1, value);
                if (fixedValue != _botOnlySnaps)
                {
                    _botOnlySnaps = fixedValue;
                    OnPropertyChanged();
                }
            } 
        }
        public bool forceEmptySnaps { get; set; } = true;
        public int emptySnaps
        {
            get
            {
                return _emptySnaps;
            }
            set
            {
                int fixedValue = Math.Max(1, value);
                if (fixedValue != _emptySnaps)
                {
                    _emptySnaps = fixedValue;
                    OnPropertyChanged();
                }
            }
        }

        public bool forceAFKSnapDrop { get; set; } = true;
        public int afkMaxSnaps
        {
            get
            {
                return _afkMaxSnaps;
            }
            set
            {
                int fixedValue = Math.Max(1, value);
                if (fixedValue != _afkMaxSnaps)
                {
                    _afkMaxSnaps = fixedValue;
                    OnPropertyChanged();
                }
            }
        }

        public int pingAdjust
        {
            get
            {
                return _pingAdjust;
            }
            set
            {
                int fixedValue = Math.Clamp(value,-1000,1000);
                if (fixedValue != _pingAdjust)
                {
                    _pingAdjust = fixedValue;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }


    // Because WPF removes _ sometimes.... annoying.
    public class UnderScoreConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value as string)?.Replace("_", "__");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value as string)?.Replace("__", "_");
        }
    }

    /// <summary>
    /// Interaction logic for ConnectedServerWindow.xaml
    /// </summary>
    public partial class ConnectedServerWindow : Window, ConnectionProvider
    {

        public bool LogColoredEnabled { get; set; } = true;
        public bool LogPlainEnabled { get; set; } = false;
        public bool DrawMiniMap { get; set; } = false;
        public float MiniMapVelocityScale { get; set; } = 1.0f;

        //private ServerInfo serverInfo = null;
        //private string ip;
        public NetAddress netAddress { get; private set; }
        public ProtocolVersion protocol { get; internal set; }
        private string serverName = null;
        public string ServerName
        {
            get
            {
                return serverName;
            }
            set
            {
                if (value != serverName)
                {
                    Dispatcher.Invoke(()=> {
                        this.Title = netAddress.ToString() + " (" + value + ")";
                    });
                }
                serverName = value;
            }
        }

        public SnapsSettings snapsSettings = new SnapsSettings();

        private Mutex connectionsCameraOperatorsMutex = new Mutex();
        private FullyObservableCollection<Connection> connections = new FullyObservableCollection<Connection>();
        private FullyObservableCollection<CameraOperator> cameraOperators = new FullyObservableCollection<CameraOperator>();

        private const int maxLogLength = 10000;
        private string logString = "Begin of Log\n";

        private ServerSharedInformationPool infoPool { get; set; }

        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();

        //public bool verboseOutput { get; private set; } = false;
        public int verboseOutput { get; private set; } = 4;

        public bool showCmdMsgNum { get; private set; } = false;

        private string password = null;
        //private string userInfoName = null;
        //private bool demoTimeColorNames = true;
        //private bool attachClientNumToName = true;

        // TODO: Send "score" command to server every second or 2 so we always have up to date scoreboards. will eat a bit more space maybe but should be cool. make it possible to disable this via some option, or to set interval

        public class ConnectionOptions : INotifyPropertyChanged {

            public event PropertyChangedEventHandler PropertyChanged;

            public class ConditionalCommand
            {
                public enum ConditionType
                {
                    PRINT_CONTAINS, // conditionVariable is print output
                    CHAT_CONTAINS, // conditionVariable is chat output
                    PLAYERACTIVE_MATCHNAME // conditionVariable is player name
                }
                public ConditionType type;
                public Regex conditionVariable1;
                public string commands;
                public bool mainConnectionOnly = false;
                public SpamLevel spamLevel = SpamLevel.Queue;
                public LeakyBucketRequester<T1, T2>.RequestBehavior GetSpamLevelAsRequestBehavior<T1, T2>() where T1 : IComparable where T2 : IComparable
                {
                    switch (spamLevel)
                    {
                        default:
                        case SpamLevel.Queue:
                            return LeakyBucketRequester<T1, T2>.RequestBehavior.ENQUEUE;
                            break;
                        case SpamLevel.NoSpam:
                            return LeakyBucketRequester<T1, T2>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS;
                            break;
                        case SpamLevel.NoSpamSame:
                            return LeakyBucketRequester<T1, T2>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS_IF_COMMAND_SAME;
                            break;
                    }
                }
                public RequestCategory getRequestCategory()
                {
                    switch (spamLevel) {
                        default:
                        case SpamLevel.Queue:
                            return RequestCategory.CONDITIONALCOMMAND;
                            break;
                        case SpamLevel.NoSpam:
                            return RequestCategory.CONDITIONALCOMMANDNOSPAM;
                            break;
                        case SpamLevel.NoSpamSame:
                            return RequestCategory.CONDITIONALCOMMANDNOSPAMSAME;
                            break;
                    }
                }
            }

            public bool autoUpgradeToCTF { get; set; } = false;
            public bool autoUpgradeToCTFWithStrobe { get; set; } = false;

            public bool netDebug { get; set; } = false;
            public bool fightDebug { get; set; } = false;

            public bool attachClientNumToName { get; set; } = true;
            public bool demoTimeColorNames { get; set; } = true;
            public bool silentMode { get; set; } = false;
            public bool noBotIgnore { get; set; } = false;
            public bool allowWayPointBotmode { get; set; } = false;
            public bool allowWayPointBotmodeCommands { get; set; } = false;
            public bool ignoreQuietMode { get; set; } = false;
            public bool mohFastSwitchFollow { get; set; } = true;
            public bool mohVeryFastSwitchFollow { get; set; } = true;
            public int mohVeryFastSwitchFollowManualCount { get; set; } = 2;
            public int mohDurationBasedSwitchFollowManualCount { get; set; } = 3;
            public int mohExpansionSwitchMinDuration { get; set; } = 50;
            public string userInfoName { get; set; } = null;
            public string skin { get; set; } = null;
            public string mapChangeCommands { get; set; } = null;
            public string quickCommands { get; set; } = null;


            public void LoadMOHDefaults()
            {
                this.demoTimeColorNames = false;
                this.attachClientNumToName = false;
                this.disconnectTriggers = "kicked";
                this.silentMode = true;
                this.userInfoName = "soldier";
            }

            private string _conditionalCommands = null;
            public string conditionalCommands { 
                get {
                    return _conditionalCommands;
                }
                set { 
                    if(value != _conditionalCommands)
                    {
                        _conditionalCommands = value;
                        parseConditionalCommmands();
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("conditionalCommands"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("conditionalCommandsParsed"));
                    }
                } 
            }
            private string _extraDemoMeta = null;
            public string extraDemoMeta { 
                get {
                    return _extraDemoMeta;
                }
                set { 
                    if(value != _extraDemoMeta)
                    {
                        _extraDemoMeta = value;
                        parseExtraDemoMeta();
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("extraDemoMeta"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("extraDemoMetaParsed"));
                    }
                } 
            }
            public bool conditionalCommandsContainErrors { get; set; } = false;

            private readonly string[] conditionalCommandPrefixes = new string[] { "single","nospam","nospamsame" };
            public enum SpamLevel
            {
                Queue,
                NoSpam,
                NoSpamSame
            }
            private void parseConditionalCommmands()
            {
                bool anyErrors = false;
                // Format is:
                // All conditional commands separated by comma
                // multiple commands can be sent via semicolon
                // condition is prepended with :
                // example: print_contains:randomstring:randomcommand;randomcommand2,chat_contains:randomstring2:randomcommand3 $name;randomcommand4,playeractive_matchname:thomas:wait 5000;follow $clientnum;randomcommand4
                lock (_conditionalCommandsParsed)
                {
                    _conditionalCommandsParsed.Clear();
                    string[] conditionalCommandsSplit = _conditionalCommands?.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                    if(conditionalCommandsSplit != null)
                    {
                        foreach(string ccRaw in conditionalCommandsSplit)
                        {
                            string[] parts = ccRaw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            bool single = false;
                            SpamLevel spamLevel = SpamLevel.Queue;
                            // Deal with prefixes
                            while (parts.Length > 0 && conditionalCommandPrefixes.Contains(parts[0],StringComparer.InvariantCultureIgnoreCase))
                            {
                                switch (parts[0].ToLower())
                                {
                                    case "single":
                                        parts = parts.Skip(1).ToArray();
                                        single = true;
                                        break;
                                    case "nospam":
                                        parts = parts.Skip(1).ToArray();
                                        spamLevel = SpamLevel.NoSpam;
                                        break;
                                    case "nospamsame":
                                        parts = parts.Skip(1).ToArray();
                                        spamLevel = SpamLevel.NoSpamSame;
                                        break;
                                }
                            }
                            if(parts.Length < 3)
                            {
                                // Invalid
                                anyErrors = true;
                            }
                            else
                            {
                                ConditionalCommand newCmd = new ConditionalCommand();
                                string conditionTypeString = parts[0].ToLower();
                                switch (conditionTypeString)
                                {
                                    case "print_contains":
                                        newCmd.type = ConditionalCommand.ConditionType.PRINT_CONTAINS;
                                        break;
                                    case "chat_contains":
                                        newCmd.type = ConditionalCommand.ConditionType.CHAT_CONTAINS;
                                        break;
                                    case "playeractive_matchname":
                                        newCmd.type = ConditionalCommand.ConditionType.PLAYERACTIVE_MATCHNAME;
                                        break;
                                    default:
                                        anyErrors = true;
                                        continue;
                                }
                                try
                                {
                                    newCmd.conditionVariable1 = new Regex(parts[1], RegexOptions.IgnoreCase | RegexOptions.Compiled);
                                }
                                catch (Exception e)
                                {
                                    anyErrors = true;
                                }
                                newCmd.commands = parts[2];
                                newCmd.mainConnectionOnly = single;
                                newCmd.spamLevel = spamLevel;
                                _conditionalCommandsParsed.Add(newCmd);
                            }
                        }
                    }
                }
                conditionalCommandsContainErrors = anyErrors;
            }
            private List<ConditionalCommand> _conditionalCommandsParsed = new List<ConditionalCommand>();
            public ConditionalCommand[] conditionalCommandsParsed
            {
                get
                {
                    lock (_conditionalCommandsParsed) return _conditionalCommandsParsed.ToArray();
                }
            }
            private void parseExtraDemoMeta()
            {
                lock (_extraDemoMetaParsed)
                {
                    _extraDemoMetaParsed.Clear();
                    string[] extraDemoMetaSplit = _extraDemoMeta?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (extraDemoMetaSplit != null)
                    {
                        foreach (string dmRaw in extraDemoMetaSplit)
                        {
                            string[] parts = dmRaw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if(parts.Length == 2)
                            {
                                _extraDemoMetaParsed[parts[0]] = parts[1];
                            } // else who cares
                        }
                    }
                }
            }
            private Dictionary<string,string> _extraDemoMetaParsed = new Dictionary<string, string>();
            public Dictionary<string, string> extraDemoMetaParsed
            {
                get
                {
                    lock (_extraDemoMetaParsed) return new Dictionary<string, string>(_extraDemoMetaParsed);
                }
            }

            int? getRangeOrNumberValue(string rangeOrNumber)
            {
                string[] rangeParts = rangeOrNumber.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (rangeParts.Length > 1)
                {
                    // We decide the actual random value the moment we set this setting and then it stays. 
                    int result = Connection.getNiceRandom(rangeParts[0].Atoi(), rangeParts[1].Atoi() + 1);
                    Debug.WriteLine($"getRangeOrNumberValue: {rangeOrNumber} interpreted as {result}");
                    return result;
                }
                else if (rangeParts.Length == 1)
                {

                    return rangeParts[0].Atoi();
                }
                else
                {
                    //uh?! Maybe typo?
                    return null;
                }
            }

            public enum DisconnectTriggers :UInt64 { // This is for bitfields, so each new value must be twice the last one.
                GAMETYPE_NOT_CTF = 1 << 0,
                KICKED = 1 << 1,
                PLAYERCOUNT_UNDER = 1 << 2,
                CONNECTEDTIME_OVER = 1 << 3,
                MAPCHANGE = 1 << 4,
                GAMETYPE_NOT = 1 << 5,
            }
            private string _disconnectTriggers = null;
            public string disconnectTriggers
            {
                get
                {
                    return _disconnectTriggers;
                }
                set
                {
                    if (value != _disconnectTriggers)
                    {
                        //playercount_under
                        string[] triggersTexts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        _disconnectTriggers = value;
                        _disconnectTriggersParsed = 0;

                        foreach(string triggerText in triggersTexts)
                        {
                            string[] triggerTextParts = triggerText.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if(triggerTextParts.Length > 0)
                            {
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("gameTypeNotCTF", StringComparison.OrdinalIgnoreCase))
                                {
                                    _disconnectTriggersParsed |= DisconnectTriggers.GAMETYPE_NOT_CTF;
                                }
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("kicked", StringComparison.OrdinalIgnoreCase))
                                {
                                    _disconnectTriggersParsed |= DisconnectTriggers.KICKED;
                                }
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("mapchange", StringComparison.OrdinalIgnoreCase))
                                {
                                    _disconnectTriggersParsed |= DisconnectTriggers.MAPCHANGE;
                                }
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("playercount_under", StringComparison.OrdinalIgnoreCase))
                                {
                                    if(triggerTextParts.Length > 1)
                                    {
                                        int? playerCount = getRangeOrNumberValue(triggerTextParts[1]);
                                        if (playerCount.HasValue)
                                        {
                                            if (triggerTextParts.Length == 2)
                                            {
                                                disconnectTriggerPlayerCountUnderPlayerCount = playerCount.Value;
                                                disconnectTriggerPlayerCountUnderDelay = 60000;
                                                _disconnectTriggersParsed |= DisconnectTriggers.PLAYERCOUNT_UNDER;
                                            }
                                            else if (triggerTextParts.Length == 3)
                                            {
                                                disconnectTriggerPlayerCountUnderPlayerCount = playerCount.Value;
                                                disconnectTriggerPlayerCountUnderDelay = triggerTextParts[2].Atoi();
                                                _disconnectTriggersParsed |= DisconnectTriggers.PLAYERCOUNT_UNDER;
                                            }
                                        }
                                    }
                                }
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("gametype_not", StringComparison.OrdinalIgnoreCase))
                                {
                                    if(triggerTextParts.Length > 1)
                                    {
                                        int gameTypesBitMask = Connection.GameTypeStringToBitMask(triggerTextParts[1]);
                                        if (gameTypesBitMask > 0)
                                        {
                                            disconnectTriggerGameTypeNotGameTypeRawString = triggerTextParts[1];
                                            disconnectTriggerGameTypeNotGameTypeBitmask = gameTypesBitMask;
                                            if (triggerTextParts.Length == 2)
                                            {
                                                disconnectTriggerGameTypeNotDelay = 0;
                                                disconnectTriggerGameTypeNotOnlyIfMatchBefore = 0;
                                                _disconnectTriggersParsed |= DisconnectTriggers.GAMETYPE_NOT;
                                            }
                                            else if (triggerTextParts.Length == 3)
                                            {
                                                disconnectTriggerGameTypeNotDelay = triggerTextParts[2].Atoi();
                                                disconnectTriggerGameTypeNotOnlyIfMatchBefore = 0;
                                                _disconnectTriggersParsed |= DisconnectTriggers.GAMETYPE_NOT;
                                            }
                                            else if (triggerTextParts.Length == 4)
                                            {
                                                disconnectTriggerGameTypeNotDelay = triggerTextParts[2].Atoi();
                                                disconnectTriggerGameTypeNotOnlyIfMatchBefore = triggerTextParts[3].Atoi();
                                                _disconnectTriggersParsed |= DisconnectTriggers.GAMETYPE_NOT;
                                            }
                                        }
                                    }
                                }
                                if (_disconnectTriggers != null && triggerTextParts[0].Contains("connectedtime_over", StringComparison.OrdinalIgnoreCase))
                                {
                                    if(triggerTextParts.Length == 2)
                                    {
                                        int? delayTime = getRangeOrNumberValue(triggerTextParts[1]);
                                        if (delayTime.HasValue)
                                        {
                                            disconnectTriggerConnectedTimeOverDelayMinutes = delayTime.Value;
                                            _disconnectTriggersParsed |= DisconnectTriggers.CONNECTEDTIME_OVER;
                                        }
                                    }
                                }
                            }
                        }

                        /*
                        if (_disconnectTriggers != null && _disconnectTriggers.Contains("gameTypeNotCTF", StringComparison.OrdinalIgnoreCase))
                        {
                            _disconnectTriggersParsed |= DisconnectTriggers.GAMETYPE_NOT_CTF;
                        }
                        if (_disconnectTriggers != null && _disconnectTriggers.Contains("kicked", StringComparison.OrdinalIgnoreCase))
                        {
                            _disconnectTriggersParsed |= DisconnectTriggers.KICKED;
                        }
                        if (_disconnectTriggers != null && _disconnectTriggers.Contains("playercount_under", StringComparison.OrdinalIgnoreCase))
                        {
                            _disconnectTriggersParsed |= DisconnectTriggers.PLAYERCOUNT_UNDER;
                        }*/

                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("disconnectTriggers"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("disconnectTriggersParsed"));
                    }
                }
            }
            private DisconnectTriggers _disconnectTriggersParsed = 0;
            public DisconnectTriggers disconnectTriggersParsed
            {
                get
                {
                    return _disconnectTriggersParsed;
                }
            }

            public int disconnectTriggerPlayerCountUnderPlayerCount { get; private set; } = 10;
            public int disconnectTriggerPlayerCountUnderDelay { get; private set; } = 60000;
            public int disconnectTriggerConnectedTimeOverDelayMinutes { get; private set; } = 60;
            public string disconnectTriggerGameTypeNotGameTypeRawString { get; private set; } = null;
            public int disconnectTriggerGameTypeNotGameTypeBitmask { get; private set; } = 0;
            public int disconnectTriggerGameTypeNotDelay { get; private set; } = 0;
            public int disconnectTriggerGameTypeNotOnlyIfMatchBefore { get; private set; } = 0;

        }

        ConnectionOptions _connectionOptions = null;

        private bool mohMode = false;


        private string chatCommandPublic = "say";
        private string chatCommandTeam = "say_team";


        public int serverMaxClientsLimit = 0;
        public int serverPrivateClientsSetting = 0;

        public ConnectedServerWindow(NetAddress netAddressA, ProtocolVersion protocolA, string serverNameA = null, string passwordA = null, ConnectionOptions connectionOptions = null)
        {
            bool connectionOptionsWereProvided = true;
            if(connectionOptions == null)
            {
                connectionOptionsWereProvided = false;
                connectionOptions = new ConnectionOptions();
            }
            _connectionOptions = connectionOptions;

            _connectionOptions.PropertyChanged += _connectionOptions_PropertyChanged;

            //this.DataContext = this;

            //serverInfo = serverInfoA;
            //demoTimeColorNames = demoTimeColorNamesA;
            netAddress = netAddressA;
            protocol = protocolA;

            mohMode = protocol >= ProtocolVersion.Protocol6 && protocol <= ProtocolVersion.Protocol8 || protocol == ProtocolVersion.Protocol17; // TODO Support 15,16 too?

            if (mohMode)
            {
                chatCommandPublic = "dmmessage 0";
                chatCommandTeam = "dmmessage -1";
                if (!connectionOptionsWereProvided)
                {
                    // Different defaults.
                    connectionOptions.LoadMOHDefaults();
                }
            }

            password = passwordA;
            //userInfoName = userInfoNameA;
            InitializeComponent();
            this.Title = netAddressA.ToString();
            if(serverNameA != null)
            {
                ServerName = serverNameA; // This will also change title.
            }

            connectionsDataGrid.ItemsSource = connections;
            cameraOperatorsDataGrid.ItemsSource = cameraOperators;

            infoPool = new ServerSharedInformationPool(protocolA == ProtocolVersion.Protocol26, mohMode ? 64 : 32) {connectionOptions = connectionOptions };

            gameTimeTxt.DataContext = infoPool;
            mapNameTxt.DataContext = infoPool;
            scoreRedTxt.DataContext = infoPool;
            scoreBlueTxt.DataContext = infoPool;
            noActivePlayersCheck.DataContext = infoPool;
            
            Connection newCon = new Connection(netAddress, protocol, this, infoPool, _connectionOptions, password, /*_connectionOptions.userInfoName, _connectionOptions.demoTimeColorNames,attachClientNumToName,*/snapsSettings);
            newCon.ServerInfoChanged += Con_ServerInfoChanged;
            newCon.PropertyChanged += Con_PropertyChanged;
            lock (connectionsCameraOperatorsMutex)
            {
                //connections.Add(new Connection(serverInfo, this,  infoPool));
                connections.Add(newCon);
            }
            updateIndices();

            logTxt.Text = "";
            addToLog("Begin of Log\n");

            this.Closed += ConnectedServerWindow_Closed;

            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { miniMapUpdater(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(), true);
            }, TaskContinuationOptions.OnlyOnFaulted),$"Minimap Updater ({netAddress},{ServerName})");
            backgroundTasks.Add(tokenSource);

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { scoreBoardRequester(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(),true);
            }, TaskContinuationOptions.OnlyOnFaulted), $"Scoreboard Requester ({netAddress},{ServerName})");
            backgroundTasks.Add(tokenSource);

            startPlayerDisplayUpdater();
            startLogStringUpdater();
            startEventNotifierUpdater();

            snapsSettingsControls.DataContext = snapsSettings;
            connectionSettingsControls.DataContext = _connectionOptions;
            advancedSettingsControls.DataContext = _connectionOptions;
            debugStats.DataContext = this;
            quickCommandsControl.ItemsSource = new string[] { "say_team !top", "logout"};
            updateQuickCommands();
        }

        private void updateQuickCommands()
        {
            quickCommandsControl.ItemsSource = _connectionOptions.quickCommands?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        private void _connectionOptions_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "quickCommands")
            {
                updateQuickCommands();
            }
        }

        // For duel modes we require a ghost peer because without it, we get stuck in endless loop of bot playing and going spec and
        // the normal players never get to play.
        // Update: Actually this idea didn't work because once a round is over, the ghost peer gets silently upgraded to real one by the server.
        private void ManageGhostPeer(bool needOne)
        {
            Dispatcher.Invoke(()=> {
                ManageGhostPeerActual(needOne);
            },System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void ManageGhostPeerActual(bool needOne)
        {
            bool haveOne = false;
            Connection ghostPeerConn = null;
            lock (connectionsCameraOperatorsMutex)
            {
                foreach (Connection conn in connections)
                {
                    if (conn.GhostPeer)
                    {
                        haveOne = true;
                        ghostPeerConn = conn;
                        break;
                    }
                }
            }
            if (!haveOne && needOne)
            {
                Connection newConnection = new Connection(netAddress, protocol, this, infoPool, _connectionOptions, password,/* _connectionOptions.userInfoName, _connectionOptions.demoTimeColorNames, attachClientNumToName,*/snapsSettings,ghostPeer:true);
                newConnection.ServerInfoChanged += Con_ServerInfoChanged;
                newConnection.PropertyChanged += Con_PropertyChanged;
                lock (connectionsCameraOperatorsMutex)
                {
                    connections.Add(newConnection);
                }
                updateIndices();
            }
            else if (haveOne && !needOne && ghostPeerConn != null)
            {
                if (ghostPeerConn.CameraOperator != null)
                {
                    addToLog("WEIRD, ManageGhostPeer(): Cannot remove connection bound to a camera operator");
                    return;
                }
                ghostPeerConn.CloseDown();
                ghostPeerConn.ServerInfoChanged -= Con_ServerInfoChanged;
                ghostPeerConn.PropertyChanged -= Con_PropertyChanged;
                lock (connectionsCameraOperatorsMutex)
                {
                    connections.Remove(ghostPeerConn);
                }
                updateIndices();
            }
        }

        private object gotKickedLock = new object();
        private bool gotKicked = false;

        public void requestClose(bool kicked)
        {
            if (kicked)
            {
                lock (gotKickedLock)
                {
                    gotKicked = true;
                }
            }
            Dispatcher.Invoke(() => {
                this.Close();
            });
        }

        private object serverInfoChangedLock = new object();
        private DateTime? playerCountUnderDisconnectTriggerLastSatisfied = null;
        private DateTime? gameTypeNotDisconnectTriggerLastSatisfied = null;
        private int gameTypeNotDisconnectTriggerUnsatisfiedHowOften = 0;


        private DateTime? connectedTimeOverDisconnectTriggerFirstConnected = null;

        private string lastMapName = null;
        private object lastMapNameLock = new object();

        public GameType gameType { get; protected set; } = GameType.FFA;

        private void Con_ServerInfoChanged(ServerInfo obj)
        {
            bool mapChangeDetected = false;
            string mapChangeFrom = null;
            lock (lastMapNameLock)
            {
                if(obj.MapName != lastMapName)
                {
                    infoPool.resetLevelShot(false, true);
                    if (lastMapName != null)
                    {
                        mapChangeDetected = true;
                        mapChangeFrom = lastMapName;
                    }
                    lastMapName = obj.MapName;
                    miniMapResetBounds = true;
                }
            }

            gameType = obj.GameType;

            int activeClientCount = obj.Clients;
            if (mohMode)
            {
                activeClientCount = 0;
                foreach (PlayerInfo pi in infoPool.playerInfo)
                {
                    if (pi.infoValid && !pi.inactiveMOH) activeClientCount++;
                }
            }

            lock (serverInfoChangedLock)
            {
                // Disconnect if "gametypenotctf" disconnecttrigger is set.
                if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.GAMETYPE_NOT_CTF) > 0 && !(obj.GameType == GameType.CTF || obj.GameType == GameType.CTY))
                {
                    this.addToLog($"Disconnect trigger tripped: Gametype {obj.GameType.ToString()} not CTF nor CTY. Disconnecting.");
                    Dispatcher.BeginInvoke((Action)(() =>
                    { // Gotta do begininvoke because I have this in the lock and wanna avoid any weird interaction with the contents of this call leading back into this method causing a deadlock.
                        this.Close();
                    }));
                    return;
                }

                // Disconnect if "mapchange" disconnecttrigger is set.
                if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.MAPCHANGE) > 0 && mapChangeDetected)
                {
                    this.addToLog($"Disconnect trigger tripped: Map change (from {mapChangeFrom} to {obj.MapName}). Disconnecting.");
                    Dispatcher.BeginInvoke((Action)(() =>
                    { // Gotta do begininvoke because I have this in the lock and wanna avoid any weird interaction with the contents of this call leading back into this method causing a deadlock.
                        this.Close();
                    }));
                    return;
                }

                // Disconnect if "playercount_under" disconnecttrigger is set.
                if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.PLAYERCOUNT_UNDER) > 0 && activeClientCount < _connectionOptions.disconnectTriggerPlayerCountUnderPlayerCount)
                {
                    double millisecondsSatisfiedFor = playerCountUnderDisconnectTriggerLastSatisfied.HasValue ? (DateTime.Now - playerCountUnderDisconnectTriggerLastSatisfied.Value).TotalMilliseconds : 0;
                    if (_connectionOptions.disconnectTriggerPlayerCountUnderDelay == 0 || playerCountUnderDisconnectTriggerLastSatisfied.HasValue && millisecondsSatisfiedFor > _connectionOptions.disconnectTriggerPlayerCountUnderDelay)
                    {
                        /*Dispatcher.Invoke(() => {
                            this.Close();
                        });*/
                        this.addToLog($"Disconnect trigger tripped: Player count {obj.Clients} ({activeClientCount}) under minimum {_connectionOptions.disconnectTriggerPlayerCountUnderPlayerCount} for over {_connectionOptions.disconnectTriggerPlayerCountUnderDelay} ms ({millisecondsSatisfiedFor} ms). Disconnecting.");
                        Dispatcher.BeginInvoke((Action)(() =>
                        { // Gotta do begininvoke because I have this in the lock and wanna avoid any weird interaction with the contents of this call leading back into this method causing a deadlock.
                            this.Close();
                        }));
                        return;
                    }
                    else if (playerCountUnderDisconnectTriggerLastSatisfied == null)
                    {
                        playerCountUnderDisconnectTriggerLastSatisfied = DateTime.Now;
                    }
                }
                else
                {
                    playerCountUnderDisconnectTriggerLastSatisfied = null;
                }
                
                // Disconnect if "gametype_not" disconnecttrigger is set.
                if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.GAMETYPE_NOT) > 0 && ((1 << (int)obj.GameType) & _connectionOptions.disconnectTriggerGameTypeNotGameTypeBitmask) == 0)
                {
                    double millisecondsSatisfiedFor = gameTypeNotDisconnectTriggerLastSatisfied.HasValue ? (DateTime.Now - gameTypeNotDisconnectTriggerLastSatisfied.Value).TotalMilliseconds : 0;
                    if (_connectionOptions.disconnectTriggerGameTypeNotDelay == 0 || gameTypeNotDisconnectTriggerLastSatisfied.HasValue && millisecondsSatisfiedFor > _connectionOptions.disconnectTriggerGameTypeNotDelay)
                    {
                        // With the "OnlyIfMatchBefore" option we only disconnect if the gametype has actually been one of the non-disconnect trigger ones for X amount of times.
                        // So that we don't disconnect immediately on connect.
                        if (_connectionOptions.disconnectTriggerGameTypeNotOnlyIfMatchBefore == 0 || gameTypeNotDisconnectTriggerUnsatisfiedHowOften >= _connectionOptions.disconnectTriggerGameTypeNotOnlyIfMatchBefore) { 

                            /*Dispatcher.Invoke(() => {
                                this.Close();
                            });*/
                            this.addToLog($"Disconnect trigger tripped: Gametype {obj.GameType} (index {(int)obj.GameType}, bitmask {1 << (int)obj.GameType}) not in {_connectionOptions.disconnectTriggerGameTypeNotGameTypeRawString} (bitmask {_connectionOptions.disconnectTriggerGameTypeNotGameTypeBitmask}) for over {_connectionOptions.disconnectTriggerGameTypeNotDelay} ms ({millisecondsSatisfiedFor} ms). Disconnecting.");
                            Dispatcher.BeginInvoke((Action)(() =>
                            { // Gotta do begininvoke because I have this in the lock and wanna avoid any weird interaction with the contents of this call leading back into this method causing a deadlock.
                                this.Close();
                            }));
                            return;
                        }
                    }
                    else if (gameTypeNotDisconnectTriggerLastSatisfied == null)
                    {
                        gameTypeNotDisconnectTriggerLastSatisfied = DateTime.Now;
                    }
                }
                else
                {
                    if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.GAMETYPE_NOT) > 0)
                    {
                        gameTypeNotDisconnectTriggerUnsatisfiedHowOften++;
                    } else
                    {
                        gameTypeNotDisconnectTriggerUnsatisfiedHowOften = 0;
                    }
                    gameTypeNotDisconnectTriggerLastSatisfied = null;
                }
                
                if ((_connectionOptions.disconnectTriggersParsed & ConnectionOptions.DisconnectTriggers.CONNECTEDTIME_OVER) > 0)
                {
                    double connectedTime = connectedTimeOverDisconnectTriggerFirstConnected.HasValue ? (DateTime.Now - connectedTimeOverDisconnectTriggerFirstConnected.Value).TotalMinutes : 0;
                    int connectedTimeLimit = Math.Max(_connectionOptions.disconnectTriggerConnectedTimeOverDelayMinutes, 1);
                    if (connectedTime > connectedTimeLimit)
                    {
                        this.addToLog($"Disconnect trigger tripped: Connected for over {connectedTimeLimit} minutes ({connectedTime} min). Disconnecting.");
                        Dispatcher.BeginInvoke((Action)(() =>
                        { // Gotta do begininvoke because I have this in the lock and wanna avoid any weird interaction with the contents of this call leading back into this method causing a deadlock.
                            this.Close();
                        }));
                        return;
                    } else if (!connectedTimeOverDisconnectTriggerFirstConnected.HasValue)
                    {
                        connectedTimeOverDisconnectTriggerFirstConnected = DateTime.Now;
                    }
                } else
                {
                    connectedTimeOverDisconnectTriggerFirstConnected = null;
                }
            }


            // This part below here feels messy. Should prolly somehow do a lock here but I can't think of a clever way to do it because creating a new connection might cause this method to get called and result in a deadlock.
            //ManageGhostPeer(obj.GameType == GameType.Duel || obj.GameType == GameType.PowerDuel); // Was a funny idea but it's actually useless
            if (_connectionOptions.autoUpgradeToCTF && (obj.GameType == GameType.CTF || obj.GameType == GameType.CTY))
            {
                bool existingCTFOperatorHasActiveStrobe = false;
                CameraOperators.CTFCameraOperatorRedBlue existingCTFOperator = null;
                CameraOperators.StrobeCameraOperator existingStrobeOperator = null;
                lock (connectionsCameraOperatorsMutex)
                {
                    foreach (CameraOperator op in cameraOperators)
                    {
                        if (op is CameraOperators.CTFCameraOperatorRedBlue) { 
                            existingCTFOperator = op as CameraOperators.CTFCameraOperatorRedBlue;
                            existingCTFOperatorHasActiveStrobe = existingCTFOperatorHasActiveStrobe || (existingCTFOperator.GetOption("withStrobe") as string).Atoi()>0;
                        }
                        if (op is CameraOperators.StrobeCameraOperator) existingStrobeOperator = op as CameraOperators.StrobeCameraOperator;
                    }
                }
                if (existingCTFOperator is null || ((existingStrobeOperator is null && !existingCTFOperatorHasActiveStrobe) && _connectionOptions.autoUpgradeToCTFWithStrobe))
                {
                    _connectionOptions.attachClientNumToName = true;
                    _connectionOptions.demoTimeColorNames = true;
                    _connectionOptions.silentMode = false;
                    Dispatcher.Invoke(() => {
                        bool anyNewWatcherCreated = false;
                        if (existingCTFOperator is null)
                        {
                            var ctfOperator = this.createCTFOperator();
                            if (_connectionOptions.autoUpgradeToCTFWithStrobe && existingStrobeOperator is null)
                            {
                                if (ctfOperator != null)
                                {
                                    ctfOperator.SetOption("withStrobe", "1");
                                }
                                else
                                {
                                    this.createStrobeOperator(); // Dunno how that'd ever happen tbh but oh well
                                }
                            }
                            anyNewWatcherCreated = true;
                        } else if (!existingCTFOperatorHasActiveStrobe && existingStrobeOperator is null && _connectionOptions.autoUpgradeToCTFWithStrobe)
                        {
                            existingCTFOperator.SetOption("withStrobe", "1");
                            //this.createStrobeOperator();
                            anyNewWatcherCreated = true;
                        }
                        if (anyNewWatcherCreated)
                        {
                            this.recordAll(); // in case of the "withStrobe" option (new method) this is partially obsolete i guess (since strobe is auto spawned and recorded) but whatever, still good for spawning the ctf operator itself.
                        }
                    });
                }
                /*lock (connectionsCameraOperatorsMutex)
                {
                    foreach (CameraOperator op in cameraOperators)
                    {
                        if (op is CameraOperators.CTFCameraOperatorRedBlue) alreadyHaveCTFWatcher = true;
                        if (op is CameraOperators.StrobeCameraOperator) alreadyHaveStrobeWatcher = true;
                    }
                }
                if(!alreadyHaveCTFWatcher || (!alreadyHaveStrobeWatcher && _connectionOptions.autoUpgradeToCTFWithStrobe))
                {
                    _connectionOptions.attachClientNumToName = true;
                    _connectionOptions.demoTimeColorNames = true;
                    _connectionOptions.silentMode = false;
                    Dispatcher.Invoke(() => { 
                        bool anyNewWatcherCreated = false;
                        if (!alreadyHaveCTFWatcher)
                        {
                            this.createCTFOperator();
                            anyNewWatcherCreated = true;
                        }
                        if (!alreadyHaveStrobeWatcher && _connectionOptions.autoUpgradeToCTFWithStrobe)
                        {
                            this.createStrobeOperator();
                            anyNewWatcherCreated = true;
                        }
                        if (anyNewWatcherCreated)
                        {
                            this.recordAll();
                        }
                    });
                }*/
            }
            
        }

        /*public ConnectedServerWindow(string ipA, ProtocolVersion protocolA)
        {
            //this.DataContext = this;
            //serverInfo = serverInfoA;
            ip = ipA;
            protocol = protocolA;
            InitializeComponent();
            this.Title = ipA + " ( Manual connect )"; // TODO Update name later

            connectionsDataGrid.ItemsSource = connections;
            cameraOperatorsDataGrid.ItemsSource = cameraOperators;

            infoPool = new ServerSharedInformationPool();

            gameTimeTxt.DataContext = infoPool;

            lock (connectionsCameraOperatorsMutex)
            {
                connections.Add(new Connection(ipA, protocolA, this,  infoPool));
            }
            updateIndices();

            logTxt.Text = "";
            addToLog("Begin of Log\n");

            this.Closed += ConnectedServerWindow_Closed;

            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task.Factory.StartNew(() => { miniMapUpdater(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(), true);
            }, TaskContinuationOptions.OnlyOnFaulted);
            backgroundTasks.Add(tokenSource);

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task.Factory.StartNew(() => { scoreBoardRequester(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(),true);
            }, TaskContinuationOptions.OnlyOnFaulted);
            backgroundTasks.Add(tokenSource);

            startLogStringUpdater();

        }*/

        private void startLogStringUpdater()
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { logStringUpdater(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                //addToLog(t.Exception.ToString(),true);
                Helpers.logToFile(new string[] { t.Exception.ToString() });
                Helpers.logToFile(dequeuedStrings.ToArray());
                Helpers.logToFile(stringsToForceWriteToLogFile.ToArray());
                Helpers.logToFile(stringsToWriteToMentionLog.ToArray());
                dequeuedStrings.Clear();
                stringsToForceWriteToLogFile.Clear();
                stringsToWriteToMentionLog.Clear();
                startLogStringUpdater();
            }, TaskContinuationOptions.OnlyOnFaulted), $"Log String Updater ({netAddress},{ServerName})");
            backgroundTasks.Add(tokenSource);
        }

        private void startEventNotifierUpdater()
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { eventNotifier(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(), true);
                //startEventNotifierUpdater();
            }, TaskContinuationOptions.OnlyOnFaulted), $"Event Notifier ({netAddress},{ServerName})");
            backgroundTasks.Add(tokenSource);
        }

        private void startPlayerDisplayUpdater()
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { playerDisplayUpdater(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                addToLog(t.Exception.ToString(), true);
                startPlayerDisplayUpdater();
            }, TaskContinuationOptions.OnlyOnFaulted), $"Player Display Updater ({netAddress},{ServerName})");
            backgroundTasks.Add(tokenSource);
        }

        // Determine which connection should be responsible for responding to chat commands
        // We don't want CTF watchers to have to spam responses to chat commands when they should be 
        // using their ratio limit in order to send appropriate follow commands
        private void setMainChatConnection()
        {
            
            lock (connectionsCameraOperatorsMutex)
            {
                bool mainConnectionFound = false;
                for (int i = 0; i < connections.Count; i++) // Prefer main connection that has no watcher
                {
                    if (!mainConnectionFound && (connections[i].CameraOperator == null))
                    {

                        connections[i].IsMainChatConnection = true;
                        connections[i].ChatMemeCommandsDelay = 1000;
                        mainConnectionFound = true;
                    }
                    else
                    {

                        connections[i].IsMainChatConnection = false;
                    }
                }
                if (!mainConnectionFound) // 
                {
                    for (int i = connections.Count - 1; i >= 0; i--) // Reverse because we want the fightbot to be the main chat connection so he can say stuff. we should solve this better somehow...
                    {
                        if (!mainConnectionFound /*&& connections[i].CameraOperator.HasValue && connections[i].CameraOperator.Value != -1 && cameraOperators.Count > connections[i].CameraOperator.Value && (cameraOperators[connections[i].CameraOperator.Value] is CameraOperators.OCDCameraOperator)*/&& connections[i].CameraOperator is CameraOperators.FFACameraOperator)
                        {
                            connections[i].IsMainChatConnection = true;
                            connections[i].ChatMemeCommandsDelay = 1000;
                            mainConnectionFound = true;
                            break;
                        }
                    }
                }
                if (!mainConnectionFound) // Prefer main connection that has strobe watcher (it doesn't send many commands if any at all)
                {
                    for (int i = 0; i < connections.Count; i++)
                    {
                        if (!mainConnectionFound /* && connections[i].CameraOperator != null&& connections[i].CameraOperator.Value != -1 && cameraOperators.Count > connections[i].CameraOperator.Value && (cameraOperators[connections[i].CameraOperator.Value] is CameraOperators.StrobeCameraOperator)*/&& connections[i].CameraOperator is CameraOperators.StrobeCameraOperator)
                        {

                            connections[i].IsMainChatConnection = true;
                            connections[i].ChatMemeCommandsDelay = 1000;
                            mainConnectionFound = true;
                            break;
                        }
                    }
                }
                if (!mainConnectionFound) // Prefer main connection that has silly watcher (it doesn't send many commands if any at all)
                {
                    for (int i = 0; i < connections.Count; i++)
                    {
                        if (!mainConnectionFound/* && connections[i].CameraOperator != null  && connections[i].CameraOperator.Value != -1 && cameraOperators.Count > connections[i].CameraOperator.Value && (cameraOperators[connections[i].CameraOperator.Value] is CameraOperators.SillyCameraOperator)*/&& connections[i].CameraOperator is CameraOperators.SillyCameraOperator)
                        {

                            connections[i].IsMainChatConnection = true;
                            connections[i].ChatMemeCommandsDelay = 1000;
                            mainConnectionFound = true;
                            break;
                        }
                    }
                }
                if (!mainConnectionFound) // Prefer spectator watcher over other types (it doesn't send many commands)
                {
                    for (int i = 0; i < connections.Count; i++)
                    {
                        if (!mainConnectionFound /*&& connections[i].CameraOperator.HasValue && connections[i].CameraOperator.Value != -1 && cameraOperators.Count > connections[i].CameraOperator.Value && (cameraOperators[connections[i].CameraOperator.Value] is CameraOperators.SpectatorCameraOperator)*/&& connections[i].CameraOperator is CameraOperators.SpectatorCameraOperator)
                        {

                            connections[i].IsMainChatConnection = true;
                            connections[i].ChatMemeCommandsDelay = 2000;
                            mainConnectionFound = true;
                            break;
                        }
                    }
                }
                if (!mainConnectionFound) // 
                {
                    for (int i = 0; i < connections.Count; i++)
                    {
                        if (!mainConnectionFound /*&& connections[i].CameraOperator.HasValue && connections[i].CameraOperator.Value != -1 && cameraOperators.Count > connections[i].CameraOperator.Value && (cameraOperators[connections[i].CameraOperator.Value] is CameraOperators.OCDCameraOperator)*/&& connections[i].CameraOperator is CameraOperators.OCDCameraOperator)
                        {

                            connections[i].IsMainChatConnection = true;
                            connections[i].ChatMemeCommandsDelay = 2000;
                            mainConnectionFound = true;
                            break;
                        }
                    }
                }
                if (!mainConnectionFound)
                {
                    // We're desperate, just set 0 as main.
                    connections[0].IsMainChatConnection = true;
                    connections[0].ChatMemeCommandsDelay = 6000; // Prolly CTF operator. Make the delay big to not interfere.
                    mainConnectionFound = true;
                }

            }
            
        }
        private void updateIndices()
        {

            lock (connectionsCameraOperatorsMutex)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    connections[i].Index = i;
                }
                for (int i = 0; i < cameraOperators.Count; i++)
                {
                    cameraOperators[i].Index = i;
                }
                setMainChatConnection();
            }
        }

        internal CameraOperator getCameraOperatorOfConnection(Connection conn)
        {
            return conn.CameraOperator;
            /*lock (connectionsCameraOperatorsMutex)
            {
                if (conn.CameraOperator.HasValue)
                {
                    // Sanity check
                    if(conn.CameraOperator >= 0 && conn.CameraOperator < cameraOperators.Count)
                    {
                        return cameraOperators[conn.CameraOperator.Value];
                    }
                }
                
            }
            return null;*/
        }

        public bool clientNumIsJKWatcherInstance(int clientNum)
        {
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    if (conn.ClientNum == clientNum) return true;
                }
            }
            return false;
        }

        public int[] getJKWatcherClientNums()
        {
            List<int> clientNums = new List<int>();
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    int value = conn.ClientNum.GetValueOrDefault(-1);
                    if (value != -1)
                    {
                        clientNums.Add(value);
                    }
                }
            }
            return clientNums.ToArray();
        }

        public bool dedicatedFightBotChatHandlersExist()
        {
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    if (conn.HandlesFightBotChatCommands)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public int[] getJKWatcherFollowedNums()
        {
            List<int> clientNums = new List<int>();
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    int value = conn.SpectatedPlayer.GetValueOrDefault(-1);
                    if (value != -1)
                    {
                        clientNums.Add(value);
                    }
                }
            }
            return clientNums.ToArray();
        }

        public Int64 getJKWatcherFollowedNumsBitMask()
        {
            Int64 followedNums = 0;
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    int value = conn.SpectatedPlayer.GetValueOrDefault(-1);
                    if (value != -1)
                    {
                        followedNums |= (Int64)1 << value;
                    }
                }
            }
            return followedNums;
        }
        public Int64 getJKWatcherClientNumsBitMask()
        {
            Int64 clientNums = 0;
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    int value = conn.ClientNum.GetValueOrDefault(-1);
                    if (value != -1)
                    {
                        clientNums |= (Int64)1 << value;
                    }
                }
            }
            return clientNums;
        }

        public Int64 getJKWatcherClientOrFollowedNumsBitMask()
        {
            Int64 clientOrFollowedNums = 0;
            lock (connectionsCameraOperatorsMutex)
            {
                foreach(Connection conn in connections)
                {
                    int value1 = conn.SpectatedPlayer.GetValueOrDefault(-1);
                    int value2 = conn.ClientNum.GetValueOrDefault(-1);
                    if (value1 != -1)
                    {
                        clientOrFollowedNums |= 1L << value1;
                    }
                    if (value2 != -1)
                    {
                        clientOrFollowedNums |= 1L << value2;
                    }
                }
            }
            return clientOrFollowedNums;
        }

        public struct LogQueueItem {
            public string logString;
            public bool forceLogToFile;
            public bool logAsMention;
            public DateTime time;
        }

        ConcurrentQueue<LogQueueItem> logQueue = new ConcurrentQueue<LogQueueItem>();
        List<int> linesRunCounts = new List<int>();
        const int countOfLineSAllowed = 100;

        List<string> dequeuedStrings = new List<string>();
        List<string> stringsToForceWriteToLogFile = new List<string>();
        List<string> stringsToWriteToMentionLog = new List<string>();

        string timeString = "", lastTimeString = "", lastTimeStringForced = "", lastTimeStringMentions = "";
        Dictionary<Int64,DateTime> calendarEventsLastAnnounced = new Dictionary<Int64, DateTime>();
        private async void eventNotifier(CancellationToken ct) // TODO Only notify in same game. Don't notify of jk2 in jka etc.
        {
            while (true)
            {
                System.Threading.Thread.Sleep(10000); // Once a minute
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;
                if (_connectionOptions.silentMode) continue;

                CalendarEvent[] ces = CalendarManager.GetCalendarEvents(true);

                if (ces == null) continue;

                foreach (CalendarEvent ce in ces)
                {
                    double minRemindInterval = 1000 * 60 * 10; // 10 minutes (min interval)
                    double maxRemindInterval = 1000 * 60 * 60 * 3; // 3 hours (maximum interval)
                    double remindInterval = maxRemindInterval; // 3 hours (maximum interval)
                    DateTime now = DateTime.Now;
                    bool eventAlreadyRunning = false;
                    bool eventInLessThan5Hours = false;
                    bool eventInLessThan1Hour = false;

                    DateTime eventTime = ce.eventTime;
                    if (eventTime.Kind == DateTimeKind.Unspecified)
                    {
                        eventTime = DateTime.SpecifyKind(eventTime, DateTimeKind.Local);
                    }

                    if ((eventTime - now).TotalDays > 31 && !ce.perpetual) continue;
                    if(!ce.active )
                    {
                        continue;
                    }


                    if (ce.perpetual)
                    {
                        remindInterval = minRemindInterval;
                    }
                    else if (eventTime > now) // upcoming
                    {
                        double timeDeltaInHours = (eventTime - now).TotalHours;
                        eventInLessThan5Hours = timeDeltaInHours < 5;
                        eventInLessThan1Hour = timeDeltaInHours < 1;
                        remindInterval = Math.Clamp((timeDeltaInHours / 10.0)* 1000 * 60 * 60, minRemindInterval, maxRemindInterval);
                    } else 
                    {
                        remindInterval = minRemindInterval;
                        eventAlreadyRunning = true;
                    }

                    if(calendarEventsLastAnnounced.ContainsKey(ce.id) && (DateTime.Now-calendarEventsLastAnnounced[ce.id]).TotalMilliseconds < remindInterval)
                    {
                        continue;
                    }
                    else
                    {

                        NetAddress eventNetAddress = null;

                        if (ce.serverIP != null && ce.serverIP.Trim() != "")
                        {
                            try
                            {
                                eventNetAddress = NetAddress.FromString(ce.serverIP.Trim());
                            }
                            catch (Exception e)
                            {
                                addToLog($"Calendar: Error converting IP: {e.ToString()}", true);
                            }
                        }

                        if (eventNetAddress != null && eventNetAddress == this.netAddress) // Don't announce event on the server it's on
                        {
                            calendarEventsLastAnnounced[ce.id] = DateTime.Now;
                            continue;
                        }

                        Connection mainChatConnection = null;
                        lock (connectionsCameraOperatorsMutex)
                        {
                            foreach(Connection conn in connections)
                            {
                                if (conn.IsMainChatConnection)
                                {
                                    mainChatConnection = conn;
                                    break;
                                }
                            }
                        }
                        bool cancelDisplay = false;
                        if(mainChatConnection != null)
                        {
                            string announcement = ce.announcementTemplate;
                            string timeString = "";
                            if (eventAlreadyRunning || ce.perpetual)
                            {
                                bool eventOlderThan2Hours = (now - eventTime).TotalHours > 2.0;
                                timeString  = "active now";
                                try
                                {
                                    if (eventNetAddress != null)
                                    {
                                        using (ServerBrowser browser = new ServerBrowser(new JKClient.JOBrowserHandler(ProtocolVersion.Protocol15)) { ForceStatus = true })
                                        {
                                            browser.InternalTaskStarted += Browser_InternalTaskStarted;
                                            browser.Start(async (JKClientException ex) => {
                                                this.addToLog("Exception trying to get ServerInfo for calendar event: " + ex.ToString());
                                            });

                                            ServerInfo serverInfo = null;
                                            try
                                            {
                                                serverInfo = await browser.GetFullServerInfo(eventNetAddress,true,false);
                                            }
                                            catch (Exception e)
                                            {
                                                this.addToLog("Exception trying to get ServerInfo for calendar event (during await): " + e.ToString());
                                                //continue;
                                            }

                                            if (serverInfo != null && serverInfo.StatusResponseReceived)
                                            {
                                                if(serverInfo.RealClients > ce.minPlayersToBeConsideredActive) // Don't advertise low player counts, it's unattractive. TODO: Make this number configurable per event?
                                                {
                                                    timeString += $" with {serverInfo.RealClients} players";
                                                }
                                                else
                                                {
                                                    if (eventOlderThan2Hours || ce.perpetual) cancelDisplay = true; // This event is older than 2 hours and it doesn't seem active anymore
                                                }
                                            }
                                            else
                                            {
                                                if (eventOlderThan2Hours || ce.perpetual) cancelDisplay = true; // This event is older than 2 hours and we can't verify it's still active
                                            }

                                            browser.Stop();
                                            browser.InternalTaskStarted -= Browser_InternalTaskStarted;
                                        }
                                    } else
                                    {
                                        if (eventOlderThan2Hours || ce.perpetual) cancelDisplay = true; // This event is older than 2 hours and we can't verify it's still active
                                    }
                                }
                                catch (Exception e)
                                {
                                    addToLog($"Calendar: Error checking server for activity: {e.ToString()}", true);
                                    if (eventOlderThan2Hours || ce.perpetual) cancelDisplay = true; // This event is older than 2 hours and we can't verify it's still active
                                }
                               
                            }
                            else
                            {
                                if (eventInLessThan1Hour)
                                {
                                    timeString = "starting in " +((int)((eventTime - now).TotalMinutes))+" minutes";
                                }
                                else if (eventInLessThan5Hours)
                                {
                                    timeString = "starting in " +(eventTime - now).TotalHours.ToString("0.#")+" hours";
                                } else
                                {
                                    DateTime utcTime = eventTime.ToUniversalTime();
                                    DateTime? cestTime = utcTime.ToCEST();
                                    DateTime? estTime = utcTime.ToEST();
                                    DateTime? cestTimeNow = now.ToUniversalTime().ToCEST();
                                    DateTime? estTimeNow = now.ToUniversalTime().ToEST();
                                    if(!cestTime.HasValue || !estTime.HasValue || !cestTimeNow.HasValue || !estTimeNow.HasValue)
                                    {
                                        string string1, string2;
                                        (string1,string2) = humanReadableFutureDateTime(now.ToUniversalTime(), utcTime);
                                        timeString = $"{string1} {string2} UTC";
                                    } else
                                    {
                                        string string1, string2, string3, string4;
                                        (string1, string2) = humanReadableFutureDateTime(cestTimeNow.Value, cestTime.Value);
                                        (string3, string4) = humanReadableFutureDateTime(estTimeNow.Value, estTime.Value);
                                        timeString = $"{string1} {string2} (CEST) /"+ ((string1==string3) ? "" : $" {string3}") + $" {string4} (EST)";
                                    }
                                }
                            }
                            announcement = announcement.Replace("###TIME###", timeString);
                            if(!cancelDisplay) mainChatConnection.leakyBucketRequester.requestExecution($"say {announcement}", RequestCategory.CALENDAREVENT_ANNOUNCE,0,60000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                            calendarEventsLastAnnounced[ce.id] = DateTime.Now;
                            break; // Only 1 event announced at a time.
                        }
                    }

                }
            }
            
        }

        private void Browser_InternalTaskStarted(object sender, in Task task, string description)
        {
            TaskManager.RegisterTask(task,$"ServerBrowser (ConnectedServerWindow {netAddress}, {ServerName}): {description}");
        }

        private (string,string) humanReadableFutureDateTime(DateTime now, DateTime then)
        {
            string dayString = "";
            if (then.Day == now.Day && (then - now).TotalDays < 4.0)
            {
                dayString = "today";
            }
            else if (now.AddDays(1).Day == then.Day && (then - now).TotalDays < 4.0)
            {
                dayString = "tomorrow";
            }
            else if ((then-now).TotalDays < 6.0)
            {
                dayString = then.ToString("dddd");
            }
            else
            {
                //dayString = then.ToString("dddd, MMMM dd", CultureInfo.CreateSpecificCulture("en-US"));
                dayString = then.ToString("MMMM dd", CultureInfo.CreateSpecificCulture("en-US"));
            }
            return (dayString,then.ToString("HH:mm"));
        }

        bool playerRefreshRequested = false;
        DateTime lastPlayerRefresh = DateTime.Now;
        object playerRefreshStatusLock = new object();
        double lastTaskbarPlayerCount = 0;
        public int truePlayerCountExcludingMyselfDelayed { get; protected set; } = 0;
        public int activePlayerCountExcludingMyselfDelayed { get; protected set; } = 0;
        int lastTaskbarTruePlayerCount = -1;
        int lastTaskbarActivePlayerCount = -1;

        public void requestPlayersRefresh()
        {
            lock (playerRefreshStatusLock)
            {
                playerRefreshRequested = true;
            }
        }

        private void playerDisplayUpdater(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(500);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested && logQueue.IsEmpty) return;

                bool needrefresh = false;
                lock (playerRefreshStatusLock)
                {
                    needrefresh = playerRefreshRequested && (DateTime.Now- lastPlayerRefresh).TotalSeconds >= 5.0;
                    if (needrefresh)
                    {
                        playerRefreshRequested = false;
                        lastPlayerRefresh = DateTime.Now;
                    }
                }

                int truePlayerExcludingMyselfCount = 0;
                int activePlayerExcludingMyselfCount = 0;
                int[] myClientNums = this.getJKWatcherClientNums();
                foreach (PlayerInfo pi in infoPool.playerInfo)
                {
                    if(pi.infoValid && !pi.confirmedBot && !pi.confirmedJKWatcherFightbot &&  (pi.score.ping != 0 || pi.score.pingUpdatesSinceLastNonZeroPing < 4) && !myClientNums.Contains(pi.clientNum))
                    {
                        truePlayerExcludingMyselfCount++;
                        if (!pi.confirmedAfk && pi.team != Team.Spectator)
                        {
                            activePlayerExcludingMyselfCount++;
                        }
                    }
                }
                truePlayerCountExcludingMyselfDelayed = truePlayerExcludingMyselfCount;
                activePlayerCountExcludingMyselfDelayed = activePlayerExcludingMyselfCount;
                if(lastTaskbarTruePlayerCount != truePlayerExcludingMyselfCount || lastTaskbarActivePlayerCount != activePlayerExcludingMyselfCount)
                {
                    Dispatcher.Invoke(() => {
                        ThumbButtonInfo pct = this.playerCountThumb;
                        ThumbButtonInfo apct = this.activePlayerCountThumb;
                        if(pct != null)
                        {
                            pct.ImageSource = RandomHelpers.NumberImages.getImageSource(truePlayerExcludingMyselfCount);
                            lastTaskbarTruePlayerCount = truePlayerExcludingMyselfCount;
                        }
                        if(apct != null)
                        {
                            apct.ImageSource = RandomHelpers.NumberImages.getImageSource(activePlayerExcludingMyselfCount);
                            lastTaskbarActivePlayerCount = activePlayerExcludingMyselfCount;
                        }
                    });
                }
                /*double playerFillRatio = (double)truePlayerCount / (double)serverMaxClientsLimit;
                if (playerFillRatio != lastTaskbarPlayerCount)
                {
                    Dispatcher.Invoke(()=> {
                        TaskbarItemInfo tbii = this.TaskbarItemInfo;
                        if(tbii != null)
                        {
                            tbii.ProgressState = TaskbarItemProgressState.Paused;
                            tbii.ProgressValue = playerFillRatio;
                            lastTaskbarPlayerCount = playerFillRatio;
                        }
                    });
                }*/

                if (needrefresh)
                {
                    Dispatcher.Invoke(()=> {
                        lock (playerListDataGrid)
                        {
                            playerListDataGrid.ItemsSource = null;
                            playerListDataGrid.ItemsSource = infoPool.playerInfo;
                        }
                    });
                }
            }
            
        }
        
        private void logStringUpdater(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(100);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested && logQueue.IsEmpty) return;

                string serverNameString = serverName == null ? netAddress.ToString() : netAddress.ToString() + "_" + serverName;
                stringsToForceWriteToLogFile.Add($"[{serverNameString}]");
                stringsToWriteToMentionLog.Add($"[{serverNameString}]");

                LogQueueItem stringToAdd;
                while (logQueue.TryDequeue(out stringToAdd))
                {
                    timeString = $"[{stringToAdd.time.ToString("yyyy-MM-dd HH:mm:ss")}]";
                    if (stringToAdd.forceLogToFile)
                    {
                        if(lastTimeStringForced != timeString) stringsToForceWriteToLogFile.Add(timeString);
                        stringsToForceWriteToLogFile.Add(stringToAdd.logString);
                        lastTimeStringForced = timeString;
                    }
                    if (stringToAdd.logAsMention)
                    {
                        if(lastTimeStringMentions != timeString) stringsToWriteToMentionLog.Add(timeString);
                        stringsToWriteToMentionLog.Add(stringToAdd.logString);
                        lastTimeStringMentions = timeString;
                        try
                        {

                            Helpers.FlashTaskBarIcon(this);
                        } catch (Exception ex)
                        {
                            dequeuedStrings.Add($"Error flashing taskbar icon: {ex.ToString()}");
                            stringsToForceWriteToLogFile.Add($"Error flashing taskbar icon: {ex.ToString()}");
                        }
                    }
                    if (timeString != lastTimeString) dequeuedStrings.Add(timeString);
                    dequeuedStrings.Add(stringToAdd.logString);
                    lastTimeString = timeString;
                }

                if (LogColoredEnabled)
                {
                    addStringsToColored(dequeuedStrings.ToArray());
                }
                if (LogPlainEnabled)
                {
                    addStringsToPlain(dequeuedStrings.ToArray());
                }

                if(stringsToForceWriteToLogFile.Count > 1) // First one is the server name and IP.
                {
                    Helpers.logToFile(stringsToForceWriteToLogFile.ToArray());
                }
                if(stringsToWriteToMentionLog.Count > 1) // First one is the server name and IP.
                {
                    Helpers.logToSpecificDebugFile(stringsToWriteToMentionLog.ToArray(),"possibleMentions.txt",true);
                }
//#if DEBUG
                // TODO Clean this up, make it get serverInfo from connections if connected via ip.
                //Helpers.debugLogToFile(serverInfo == null ? netAddress.ToString() : serverInfo.Address.ToString() + "_" + serverInfo.HostName , dequeuedStrings.ToArray());
                Helpers.debugLogToFile(serverNameString, dequeuedStrings.ToArray());
//#endif

                dequeuedStrings.Clear();
                stringsToForceWriteToLogFile.Clear();
                stringsToWriteToMentionLog.Clear();
            }
            
        }

        private void addStringsToColored(string[] stringsToAdd)
        {
            Dispatcher.Invoke(() => {

                List<Inline> linesToAdd = new List<Inline>();

                foreach (string stringToAdd in stringsToAdd)
                {
                    Run[] runs = Q3ColorFormatter.Q3StringToInlineArray(stringToAdd);
                    if (runs.Length == 0) continue;
                    linesToAdd.AddRange(runs);
                    linesToAdd.Add(new LineBreak());
                    linesRunCounts.Add(runs.Length + 1);
                }

                // If there are too many lines, count how many runs we need to remove
                int countOfRunsToRemove = 0;
                while (linesRunCounts.Count > countOfLineSAllowed)
                {
                    countOfRunsToRemove += linesRunCounts[0];
                    linesRunCounts.RemoveAt(0);
                }

                for (int i = 0; i < countOfRunsToRemove; i++)
                {
                    logTxt.Inlines.Remove(logTxt.Inlines.FirstInline);
                }
                logTxt.Inlines.AddRange(linesToAdd);
            });
        }
        private void addStringsToPlain(string[] stringsToAdd)
        {
            lock (logString)
            {
                foreach (string stringToAddIt in stringsToAdd)
                {
                    string stringToAdd = stringToAddIt + "\n";
                    int newLogLength = logString.Length + stringToAdd.Length;
                    if (newLogLength <= maxLogLength)
                    {
                        logString += stringToAdd;
                    }
                    else
                    {
                        int cutAway = newLogLength - maxLogLength;
                        logString = logString.Substring(cutAway) + stringToAdd;
                    }
                }
            }

            Dispatcher.Invoke(() => {
                lock (logString)
                {
                    logTxtPlain.Text = logString;
                }
            });
        }

        // Old style log string updater without colors:
        /* private void logStringUpdater(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(10);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                string stringToAdd;
                
                while(logQueue.TryDequeue(out stringToAdd))
                {
                    lock (logString)
                    {
                        stringToAdd += "\n";
                        int newLogLength = logString.Length + stringToAdd.Length;
                        if (newLogLength <= maxLogLength)
                        {
                            logString += stringToAdd;
                        }
                        else
                        {
                            int cutAway = newLogLength - maxLogLength;
                            logString = logString.Substring(cutAway) + stringToAdd;
                        }
                    }
                }
                
                Dispatcher.Invoke(() => {
                    lock (logString)
                    {
                        logTxt.Text = logString;
                    }
                });
            }
            
        }*/


        ConcurrentDictionary<string,DateTime> rateLimitedErrorMessages = new ConcurrentDictionary<string,DateTime>();
        ConcurrentDictionary<string, int> rateLimitedErrorMessagesCount = new ConcurrentDictionary<string,int>();
        // Use timeout (milliseconds) for messages that might happen often.
        public void addToLog(string someString, bool forceLogToFile = false, int timeOut = 0, int logLevel = 0, bool logAsMention = false)
        {
            if (logLevel > verboseOutput) return;
            if(timeOut != 0)
            {
                lock (rateLimitedErrorMessagesCount)
                {
                    if (!rateLimitedErrorMessagesCount.ContainsKey(someString))
                    {
                        rateLimitedErrorMessagesCount[someString] = 0;
                    }
                    if (rateLimitedErrorMessages.ContainsKey(someString) && rateLimitedErrorMessages[someString] > DateTime.Now)
                    {
                        rateLimitedErrorMessagesCount[someString]++;
                        return; // Skipping repeated message.
                    }
                    else
                    {
                        rateLimitedErrorMessages[someString] = DateTime.Now.AddMilliseconds(timeOut);
                        if (rateLimitedErrorMessagesCount[someString] > 0)
                        {
                            int countSkipped = rateLimitedErrorMessagesCount[someString];
                            rateLimitedErrorMessagesCount[someString] = 0;
                            someString = $"[SKIPPED {countSkipped} TIMES]\n{someString}";
                        }
                    }
                }
            }
            logQueue.Enqueue(new LogQueueItem() { logString= someString,forceLogToFile=forceLogToFile,time=DateTime.Now,logAsMention= logAsMention });
        }


        private unsafe void scoreBoardRequester(CancellationToken ct)
        {
            while (true)
            {
                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                int timeout = 2000;
                if (!mohMode)
                {
                    // The team info in MOH is shaky and unreliable so let's not get into this.
                    if (infoPool.NoActivePlayers)
                    {
                        timeout = 30000;
                    }
                    else if (((DateTime.Now - infoPool.lastBotOnlyConfirmed)?.TotalMilliseconds).GetValueOrDefault(double.PositiveInfinity) < 15000)
                    {
                        timeout = 10000;
                    }
                }
                System.Threading.Thread.Sleep(timeout);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                lock (connectionsCameraOperatorsMutex)
                {
                    foreach (Connection connection in connections)
                    {
                        if (connection.client?.Status == ConnectionStatus.Active)
                        {
                            //connection.client.ExecuteCommand("score");
                            connection.leakyBucketRequester.requestExecution("score", RequestCategory.SCOREBOARD, 0, 2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                        }
                    }
                }
            }
        }

        const float miniMapOutdatedDrawTime = 1; // in seconds.

        bool miniMapResetBounds = false;

        private unsafe void miniMapUpdater(CancellationToken ct)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            while (true) {

                System.Threading.Thread.Sleep(100);
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                if (infoPool.playerInfo == null || !DrawMiniMap) continue;

                int imageWidth = (int)miniMapContainer.ActualWidth;
                int imageHeight = (int)miniMapContainer.ActualHeight;

                if (imageWidth < 5 || imageHeight < 5) continue; // avoid crashes and shit

                if (miniMapResetBounds)
                {
                    minX = float.PositiveInfinity;
                    maxX = float.NegativeInfinity;
                    minY = float.PositiveInfinity;
                    maxY = float.NegativeInfinity;
                    miniMapResetBounds = false;
                }

                // We flip imageHeight and imageWidth because it's more efficient to work on rows than on columns. We later rotate the image into the proper position
                    ByteImage miniMapImage = Helpers.BitmapToByteArray(new Bitmap(imageWidth, imageHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb));
                int stride = miniMapImage.stride;

                // Pass 1: Get bounds of all player entities
                /*ClientEntity[] entities = connections[0].client.Entities;
                if(entities == null)
                {
                    continue;
                }*/
                
                for(int i = 0; i < infoPool.playerInfo.Length; i++)
                {
                    minX = Math.Min(minX, -infoPool.playerInfo[i].position.X);
                    maxX = Math.Max(maxX, -infoPool.playerInfo[i].position.X);
                    minY = Math.Min(minY, infoPool.playerInfo[i].position.Y);
                    maxY = Math.Max(maxY, infoPool.playerInfo[i].position.Y);
                }

                // Pass 2: Draw players as pixels
                float xRange = maxX - minX, yRange = maxY-minY;
                float x, y, dirX, dirY;
                int imageX, imageY, imageXEnd, imageYEnd;
                int xFrom, xTo, yFrom, yTo, pixY, yStep, yPixRange, xPixRange, yState, yStart;
                float XYRatio = 1.0f, XYRatioHere;
                //int byteOffset;
                byte[] color = new byte[3];
                Vector3 up = new Vector3();
                Vector3 right = new Vector3();
                Vector3 forward = new Vector3();
                //fixed(byte* imgData = miniMapImage.imageData)
                {
                    byte[] imgData = imgData = miniMapImage.imageData;
                    for (int i = 0; i < infoPool.playerInfo.Length; i++)
                    {
                        if(infoPool.playerInfo[i].lastFullPositionUpdate == null)
                        {
                            continue; // don't have any position data
                        }
                        if ((DateTime.Now - infoPool.playerInfo[i].lastFullPositionUpdate.Value).TotalSeconds > miniMapOutdatedDrawTime)
                        {
                            continue; // data too old (probably bc out of sight)
                        }
                        if(infoPool.playerInfo[i].lastClientInfoUpdate == null)
                        {
                            continue; // don't have any client info.
                        }
                        if(mohMode && i >= serverMaxClientsLimit)
                        {
                            continue;
                        }
                        x = -infoPool.playerInfo[i].position.X;
                        y = infoPool.playerInfo[i].position.Y;
                        //dirX = -infoPool.playerInfo[i].velocity.X;
                        //dirY = infoPool.playerInfo[i].velocity.Y;
                        Connection.AngleVectors(infoPool.playerInfo[i].angles, out forward, out right, out up);
                        forward *= 100 * MiniMapVelocityScale;
                        dirX = -forward.X;
                        dirY = forward.Y;
                        imageX = Math.Clamp((int)( (x - minX) / xRange *(float)(imageWidth-1f)),0,imageWidth-1);
                        imageY = Math.Clamp((int)((y - minY) / yRange *(float)(imageHeight-1f)),0,imageHeight-1);
                        imageXEnd = Math.Clamp((int)( (x + (dirX*MiniMapVelocityScale) - minX) / xRange *(float)(imageWidth-1f)),0,imageWidth-1);
                        imageYEnd = Math.Clamp((int)((y + (dirY* MiniMapVelocityScale) - minY) / yRange *(float)(imageHeight-1f)),0,imageHeight-1);

                        if (infoPool.playerInfo[i].team == Team.Red)
                        {
                            color[0] = 128;
                            color[1] = 128;
                            color[2] = 255;
                        }
                        else if (infoPool.playerInfo[i].team == Team.Blue)
                        {
                            color[0] = 255;
                            color[1] = 128;
                            color[2] = 128;
                        }
                        else if (infoPool.playerInfo[i].team == Team.Spectator)
                        {
                            color[0] = 128;
                            color[1] = 255;
                            color[2] = 255;
                        }
                        else
                        {
                            color[0] = 0;
                            color[1] = 255;
                            color[2] = 0;
                        }


                        xFrom = Math.Min(imageX, imageXEnd);
                        xTo = Math.Max(imageX, imageXEnd);
                        yFrom = Math.Min(imageY, imageYEnd);
                        yTo = Math.Max(imageY, imageYEnd);
                        xPixRange = xTo - xFrom;
                        yPixRange = yTo - yFrom;
                        yState = 0;
                        pixY = yFrom;
                        XYRatio = xTo == xFrom ? 1.0f : Math.Abs((float)yPixRange / (float)xPixRange); // the ?: is just to avoid division by zero
                        yStep = Math.Sign(imageXEnd-imageX) * Math.Sign(imageYEnd-imageY);
                        yStep = yStep == 0 ? 1 : yStep;
                        yStart = yStep < 0 ? yTo : yFrom;
                        for (int pixX = xFrom; pixX <= xTo; pixX++ )
                        {
                            if (pixX == xTo)
                            {
                                while (yState <= yPixRange)
                                {
                                    pixY = yStart + yStep*yState;
                                    imgData[pixY * stride + pixX * 3] = Math.Max(imgData[pixY * stride + pixX * 3], color[0]);
                                    imgData[pixY * stride + pixX * 3 + 1] = Math.Max(imgData[pixY * stride + pixX * 3 + 1], color[1]);
                                    imgData[pixY * stride + pixX * 3 + 2] = Math.Max(imgData[pixY * stride + pixX * 3 + 2], color[2]);
                                    yState++;
                                }
                            } else if(yState < yPixRange && pixX > xFrom)
                            {
                                while (((float)yState / (float)(pixX - xFrom)) < XYRatio && yState <= yPixRange)
                                {
                                    pixY = yStart + yStep * yState;
                                    imgData[pixY * stride + pixX * 3] = Math.Max(imgData[pixY * stride + pixX * 3], color[0]);
                                    imgData[pixY * stride + pixX * 3 + 1] = Math.Max(imgData[pixY * stride + pixX * 3 + 1], color[1]);
                                    imgData[pixY * stride + pixX * 3 + 2] = Math.Max(imgData[pixY * stride + pixX * 3 + 2], color[2]);
                                    yState++;
                                }
                            }
                            if(yState <= yPixRange)
                            {
                                pixY = yStart + yStep * yState;
                                imgData[pixY * stride + pixX * 3] = Math.Max(imgData[pixY * stride + pixX * 3], color[0]);
                                imgData[pixY * stride + pixX * 3 + 1] = Math.Max(imgData[pixY * stride + pixX * 3 + 1], color[1]);
                                imgData[pixY * stride + pixX * 3 + 2] = Math.Max(imgData[pixY * stride + pixX * 3 + 2], color[2]);
                            }
                        }

                        /*byteOffset = imageY * stride + imageX * 3;
                        if(infoPool.playerInfo[i].team == Team.Red)
                        {
                            byteOffset += 2; // red pixel. blue is just 0.
                        } else if (infoPool.playerInfo[i].team != Team.Blue)
                        {
                            byteOffset += 1; // Just make it green then, not sure what it is.
                        }

                        miniMapImage.imageData[byteOffset] = 255;*/


                    }
                }


                //statsImageBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                Dispatcher.Invoke(()=> {
                    Bitmap miniMapImageBitmap = Helpers.ByteArrayToBitmap(miniMapImage);
                    miniMap.Source = Helpers.BitmapToImageSource(miniMapImageBitmap);
                    miniMapImageBitmap.Dispose();
                });
            }
        }

        internal CameraOperators.CTFCameraOperatorRedBlue createCTFOperator()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    return createCameraOperator<CameraOperators.CTFCameraOperatorRedBlue>();
                }
            }
            return null;
        }
        internal CameraOperators.OCDCameraOperator createOCDefragOperator()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    return createCameraOperator<CameraOperators.OCDCameraOperator>();
                }
            }
            return null;
        }
        internal CameraOperators.StrobeCameraOperator createStrobeOperator()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    return createCameraOperator<CameraOperators.StrobeCameraOperator>();
                }
            }
            return null;
        }
        internal CameraOperators.FFACameraOperator createFFAOperator()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    return createCameraOperator<CameraOperators.FFACameraOperator>();
                }
            }
            return null;
        }

        private T createCameraOperator<T>() where T:CameraOperator, new()
        {

            T camOperator = null;
            lock (connectionsCameraOperatorsMutex)
            {
                camOperator = new T();
                camOperator.Errored += CamOperator_Errored;
                int requiredConnectionCount = camOperator.getRequiredConnectionCount();
                Connection[] connectionsForCamOperator = getUnboundConnections(requiredConnectionCount);
                camOperator.provideConnections(connectionsForCamOperator);
                camOperator.provideServerSharedInformationPool(infoPool);
                camOperator.provideServerWindow(this);
                camOperator.provideConnectionProvider(this);
                camOperator.Initialize();
                cameraOperators.Add(camOperator);
                updateIndices();
            }
            return camOperator;
        }

        private void CamOperator_Errored(object sender, CameraOperator.ErroredEventArgs e)
        {
            addToLog("Camera Operator error: " + e.Exception.ToString(),true);
        }

        public Connection[] getUnboundConnections(int count)
        {
            List<Connection> retVal = new List<Connection>();

            // TODO: Put lock (connectionsCameraOperatorsMutex) here?            
            foreach (Connection connection in connections)
            {
                if (connection.CameraOperator == null && !connection.GhostPeer)
                {
                    retVal.Add(connection);
                    if (retVal.Count == count)
                    {
                        break;
                    }
                }
            }            

            while(retVal.Count < count)
            {
                //Connection newConnection = new Connection(connections[0].client.ServerInfo, this,infoPool);
                Connection newConnection = new Connection(netAddress,protocol, this,infoPool, _connectionOptions,password,/* _connectionOptions.userInfoName, _connectionOptions.demoTimeColorNames, attachClientNumToName,*/snapsSettings);
                newConnection.ServerInfoChanged += Con_ServerInfoChanged;
                newConnection.PropertyChanged += Con_PropertyChanged;
                lock (connectionsCameraOperatorsMutex)
                {
                    connections.Add(newConnection);
                }
                updateIndices();
                retVal.Add(newConnection);
            }

            return retVal.ToArray();
        }
        
        public Connection[] getAllConnections()
        {
            lock (connectionsCameraOperatorsMutex)  return this.connections.ToArray();
        }

        private void Con_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CameraOperator")
            {
                setMainChatConnection();
            }
        }

        private void ConnectedServerWindow_Closed(object sender, EventArgs e)
        {
            CloseDown();
        }

        ~ConnectedServerWindow() // Not sure if needed tbh
        {
            CloseDown();
        }

        private Mutex destructionMutex = new Mutex();
        private bool isDestroyed = false;

        private void CloseDown()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;
                isDestroyed = true;

                SaveLevelshot(infoPool.levelShot, 200, 5);

                _connectionOptions.PropertyChanged -= _connectionOptions_PropertyChanged;
                this.Closed -= ConnectedServerWindow_Closed;
                foreach (CancellationTokenSource backgroundTask in backgroundTasks)
                {
                    backgroundTask.Cancel();
                }

                //if (connections.Count == 0) return; // doesnt really matter.

                // TOdo put lock (connectionsCameraOperatorsMutex) here?
                foreach (CameraOperator op in cameraOperators)
                {
                    lock (connectionsCameraOperatorsMutex)
                    {
                        op.Destroy();
                    }
                    //cameraOperators.Remove(op); // Don't , we're inside for each
                    updateIndices();
                    op.Errored -= CamOperator_Errored;
                }
                cameraOperators.Clear();
                lock (connectionsCameraOperatorsMutex)
                {
                    foreach (Connection connection in connections)
                    {
                        connection.stopDemoRecord();
                        //connection.disconnect();
                        connection.CloseDown();
                        //connections.Remove(connection); // Don't, we're inside foreach
                    }
                    connections.Clear();
                }

                MainWindow.setServerLastDisconnectedNow(this.netAddress);
                lock (gotKickedLock)
                {
                    if (gotKicked)
                    {
                        MainWindow.setServerLastKickedNow(this.netAddress);
                    }
                }
            }
        }

        public void recordAll()
        {
            lock (destructionMutex)
            {
                if (!isDestroyed)
                {
                    int i = 0;
                    foreach (Connection conn in connections)
                    {
                        if (conn != null)
                        {
                            conn.startDemoRecord(i++);
                        }
                    }
                }
            }
        }

        
        private void recordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            // we make a copy of the selected items because otherwise the command might change something
            // that also results in a change of selecteditems and then it would only get the first item.
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            int i = 0;
            foreach (Connection conn in conns)
            {
                if (conn != null && !conn.isRecordingADemo)
                {
                    conn.startDemoRecord(i++);
                }
            }

            /*foreach (Connection connection in connections)
            {
                if(connection.client.Status == ConnectionStatus.Active)
                {

                    connection.startDemoRecord();
                    break;
                }
            }*/
        }

        private void stopRecordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            // we make a copy of the selected items because otherwise the command might change something
            // that also results in a change of selecteditems and then it would only get the first item.
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            foreach (Connection conn in conns)
            {
                if (conn != null && conn.isRecordingADemo)
                {
                    conn.stopDemoRecord();
                }
            }

            /*foreach (Connection connection in connections)
            {
                if (connection.client.Demorecording)
                {

                    connection.stopDemoRecord();
                    break;
                }
            }*/
        }

        private void commandSendBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            DoExecuteCommand(commandLine.Text, conns.ToArray());
            /*foreach (Connection connection in conns)
            {
                if (connection.client.Status == ConnectionStatus.Active)
                {
                    string command = commandLine.Text;
                    //commandLine.Text = "";
                    //connection.client.ExecuteCommand(command);
                    connection.leakyBucketRequester.requestExecution(command, RequestCategory.NONE,1,0,LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                    addToLog("Command \"" + command + "\" sent.");
                }
            }*/
        }

        private void updateButtonEnablednesses()
        {
            bool connectionsSelected = connectionsDataGrid.Items.Count > 0 && connectionsDataGrid.SelectedItems.Count > 0;
            bool cameraOperatorsSelected = cameraOperatorsDataGrid.Items.Count > 0 && cameraOperatorsDataGrid.SelectedItems.Count > 0;
            bool playersSelected = playerListDataGrid.Items.Count > 0 && playerListDataGrid.SelectedItems.Count > 0;

            delBtn.IsEnabled = connectionsSelected;
            reconBtn.IsEnabled = connectionsSelected;
            statsBtn.IsEnabled = connectionsSelected;
            unselectBtn.IsEnabled = connectionsSelected;
            recordBtn.IsEnabled = connectionsSelected;
            stopRecordBtn.IsEnabled = connectionsSelected;
            commandSendBtn.IsEnabled = connectionsSelected;

            deleteWatcherBtn.IsEnabled = cameraOperatorsSelected;

            msgSendBtn.IsEnabled = connectionsSelected;
            msgSendTeamBtn.IsEnabled = connectionsSelected;
            buttonHitBtn.IsEnabled = connectionsSelected;
            msgSendPlayerBtn.IsEnabled = playersSelected && connectionsSelected; // Sending to specific players.

            followBtn.IsEnabled = playersSelected && connectionsSelected; // Need to know who to follow and which connection to use
        }

        private void addCtfWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.CTFCameraOperatorRedBlue>();
        }
        
        private void addOCDWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.OCDCameraOperator>();
        }
        
        private void addSpectatorWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.SpectatorCameraOperator>();
        }

        private void verbosityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int verbosity = 0;
            if( int.TryParse(((ComboBoxItem)verbosityComboBox.SelectedItem).Tag.ToString(), out verbosity))
            {
                verboseOutput = verbosity;
            }
        }

        private void connectionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateButtonEnablednesses();
        }

        private void deleteWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            List<CameraOperator> ops = cameraOperatorsDataGrid.SelectedItems.Cast<CameraOperator>().ToList();
            lock (connectionsCameraOperatorsMutex)
            {
                foreach (CameraOperator op in ops)
                {
                    //lock (connectionsCameraOperatorsMutex)
                    //{
                        op.Destroy();
                    //}
                    cameraOperators.Remove(op);
                    updateIndices();
                    op.Errored -= CamOperator_Errored;
                }
            }
        }
        private void watcherConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            List<CameraOperator> ops = cameraOperatorsDataGrid.SelectedItems.Cast<CameraOperator>().ToList();
            lock (connectionsCameraOperatorsMutex)
            {
                foreach (CameraOperator op in ops)
                {
                    op.OpenDialog();
                }
            }
        }

        private void cameraOperatorsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateButtonEnablednesses();
        }

        private void msgSendBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            DoExecuteCommand($"{chatCommandPublic} \"" + commandLine.Text + "\"",conns.ToArray());
        }

        private void msgSendTeamBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            DoExecuteCommand($"{chatCommandTeam} \"" + commandLine.Text + "\"", conns.ToArray());

        }

        private void msgSendPlayerBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();
            List<PlayerInfo> recipients = playerListDataGrid.SelectedItems.Cast<PlayerInfo>().ToList();

            foreach(PlayerInfo recipient in recipients)
            {
                DoExecuteCommand("tell "+recipient.clientNum+" \"" + commandLine.Text + "\"", conns.ToArray());
            }
        }
        private void DoExecuteCommand(string command, Connection conn)
        {
            DoExecuteCommand(command, new Connection[] { conn });
        }

        private void DoExecuteCommand(string command, Connection[] conns)
        {
            foreach (Connection connection in conns)
            {
                if (connection.client.Status == ConnectionStatus.Active)
                {
                    //string command = commandLine.Text;
                    //commandLine.Text = "";
                    //connection.client.ExecuteCommand(command);
                    connection.leakyBucketRequester.requestExecution(command, RequestCategory.NONE, 1, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                    addToLog("[Conn "+connection.Index+", cN "+connection.client.clientNum+"] Command \"" + command + "\" sent.");
                }
            }
        }

        private void playerListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateButtonEnablednesses();
        }

        private void followBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();
            List<PlayerInfo> selectedPlayers = playerListDataGrid.SelectedItems.Cast<PlayerInfo>().ToList();

            if(selectedPlayers.Count > 1)
            {
                if(selectedPlayers.Count != conns.Count)
                {
                    addToLog("JKWatcher error: If you select more than one player to follow, the count of selected players must be equal to the count of selected connections. ("+ selectedPlayers.Count + " != "+ conns.Count + ")");
                    return;
                }
                for(int i = 0; i < selectedPlayers.Count; i++) // First selected connection follows first selected player, and so on
                {
                    DoExecuteCommand("follow " + selectedPlayers[i].clientNum, conns[0]);
                }
            } else if(selectedPlayers.Count > 0)
            {
                DoExecuteCommand("follow " + selectedPlayers[0].clientNum, conns.ToArray());
            }

            
        }

        private void checkDraw_Checked(object sender, RoutedEventArgs e)
        {
            DrawMiniMap = checkDraw.IsChecked.HasValue ? checkDraw.IsChecked.Value : false;
        }

        private void newConBtn_Click(object sender, RoutedEventArgs e)
        {
            //Connection newConnection = new Connection(connections[0].client.ServerInfo, this, infoPool);
            Connection newConnection = new Connection(netAddress,protocol, this, infoPool, _connectionOptions, password,/* _connectionOptions.userInfoName, _connectionOptions.demoTimeColorNames, attachClientNumToName,*/snapsSettings);
            newConnection.ServerInfoChanged += Con_ServerInfoChanged;
            newConnection.PropertyChanged += Con_PropertyChanged;
            lock (connectionsCameraOperatorsMutex)
            {
                connections.Add(newConnection);
            }
            updateIndices();
        }


        private void delBtn_Click(object sender, RoutedEventArgs e)
        {
            /*if(connections.Count < 2) // We allow this now because we no longer have places relying on connections[0], specifically in places that create new connections.
            {
                addToLog("Cannot remove connections if only one connection exists.");
                return; // We won't delete our only remaining connection.
            }*/

            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            /*if(connections.Count - conns.Count < 1)
            {
                addToLog("Cannot remove connections if none would be left.");
                return; // We won't delete all counnections. We want to keep at least one.
            }*/

            lock (connectionsCameraOperatorsMutex) { 
                foreach (Connection conn in conns)
                {
                    //if (conn.Status == ConnectionStatus.Active) // We wanna be able to delete faulty/disconnected connections too. Even more actually! If a connection gets stuck, it shouldn't stay there forever.
                    //{
                        if(conn.CameraOperator != null)
                        {
                            addToLog("Cannot remove connection bound to a camera operator");
                        } else
                        {

                            //conn.disconnect();
                            conn.CloseDown();
                            conn.ServerInfoChanged -= Con_ServerInfoChanged;
                            conn.PropertyChanged -= Con_PropertyChanged;
                            connections.Remove(conn);
                        }
                    //}
                }
            }
            updateIndices();
        }

        private void reconBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            // we make a copy of the selected items because otherwise the command might change something
            // that also results in a change of selecteditems and then it would only get the first item.
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            foreach (Connection conn in conns)
            {
                if (conn != null /*&& conn.Status != ConnectionStatus.Active*/) // Allow to do it always. A connection may crash and wrongly show as active.
                {
                    //_=conn.hardReconnect();
                    _=conn.Reconnect();
                }
            }
        }

        private void btnSetPassword_Click(object sender, RoutedEventArgs e)
        {
            password = passwordTxt.Text == "" ? null : passwordTxt.Text;
            foreach (Connection conn in connections)
            {
                conn.SetPassword(password);
            }
        }

        private void btnClearPassword_Click(object sender, RoutedEventArgs e)
        {
            passwordTxt.Text = "";
        }

        private void btnFillCurrentPassword_Click(object sender, RoutedEventArgs e)
        {
            passwordTxt.Text = password;
        }

        public void setUserInfoName(string name = null)
        {
            _connectionOptions.userInfoName = name;
            //foreach (Connection conn in connections)
            //{
            //    conn.SetUserInfoName(_connectionOptions.userInfoName);
            //}
        }
        private void btnSetName_Click(object sender, RoutedEventArgs e)
        {
            this.setUserInfoName(nameTxt.Text == "" ? null : nameTxt.Text);
        }
        private void btnClearName_Click(object sender, RoutedEventArgs e)
        {
            nameTxt.Text = "";
        }

        private void btnFillCurrentName_Click(object sender, RoutedEventArgs e)
        {
            nameTxt.Text = _connectionOptions.userInfoName;
        }
        /*
        private void unixTimeNameColorsCheck_Checked(object sender, RoutedEventArgs e)
        {
            _connectionOptions.demoTimeColorNames = unixTimeNameColorsCheck.IsChecked == true;
            //foreach (Connection conn in connections)
            //{
            //    conn.SetDemoTimeNameColors(_connectionOptions.demoTimeColorNames);
            //}
        }

        private void attachClientNumToNameCheck_Checked(object sender, RoutedEventArgs e)
        {

            _connectionOptions.attachClientNumToName = attachClientNumToNameCheck.IsChecked == true;
            //foreach (Connection conn in connections)
            //{
            //    conn.SetClientNumNameAttach(attachClientNumToName);
            //}
        }*/
        /*
        private void updateSnapsSettings()
        {
            if (botOnlySnapsCheck == null || emptySnapsCheck == null || botOnlySnapsTxt == null || emptySnapsTxt == null)
            {
                return;
            }
            snapsSettings.forceBotOnlySnaps = botOnlySnapsCheck.IsChecked.HasValue && botOnlySnapsCheck.IsChecked.Value;
            snapsSettings.forceEmptySnaps = emptySnapsCheck.IsChecked.HasValue && emptySnapsCheck.IsChecked.Value;
            int.TryParse(botOnlySnapsTxt.Text, out snapsSettings.botOnlySnaps);
            int.TryParse(emptySnapsTxt.Text, out snapsSettings.emptySnaps);
            if (snapsSettings.botOnlySnaps < 1) snapsSettings.botOnlySnaps = 1;
            if (snapsSettings.emptySnaps < 1) snapsSettings.emptySnaps = 1;
        }
        private void writeSnapsSettingsToGUI()
        {
            if (botOnlySnapsCheck == null || emptySnapsCheck == null || botOnlySnapsTxt == null || emptySnapsTxt == null)
            {
                return;
            }
            botOnlySnapsCheck.IsChecked = snapsSettings.forceBotOnlySnaps;
            emptySnapsCheck.IsChecked = snapsSettings.forceEmptySnaps;
            botOnlySnapsTxt.Text = snapsSettings.botOnlySnaps.ToString();
            emptySnapsTxt.Text = snapsSettings.emptySnaps.ToString();
            if (snapsSettings.botOnlySnaps < 1) snapsSettings.botOnlySnaps = 1;
            if (snapsSettings.emptySnaps < 1) snapsSettings.emptySnaps = 1;
        }

        private void snapsCheck_Checked(object sender, RoutedEventArgs e)
        {
            updateSnapsSettings();
        }

        private void snapsTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            updateSnapsSettings();
        }*/

        private void statsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            // we make a copy of the selected items because otherwise the command might change something
            // that also results in a change of selecteditems and then it would only get the first item.
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            foreach (Connection conn in conns)
            {
                if (conn != null)
                {
                    new ConnectionStatsWindow(conn).Show();
                }
            }
        }

        private void netDebugCheck_Checked(object sender, RoutedEventArgs e)
        {
            bool doNetDebug = netDebugCheck.IsChecked == true;
            foreach (Connection conn in connections)
            {
                Client theclient = conn.client;
                if(theclient != null)
                {
                    theclient.DebugNet = doNetDebug;
                }
            }
        }

        private void refreshPlayersBtn_Click(object sender, RoutedEventArgs e)
        {
            requestPlayersRefresh();
            //lock (playerListDataGrid)
            //{
            //    playerListDataGrid.ItemsSource = null;
            //    playerListDataGrid.ItemsSource = infoPool.playerInfo;
            //}
        }

        private void addSillyWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.SillyCameraOperator>();
        }

        private void showCmdMsgNumCheck_Checked(object sender, RoutedEventArgs e)
        {
            showCmdMsgNum = showCmdMsgNumCheck.IsChecked == true;
        }

        private void addFFAWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.FFACameraOperator>();
        }

        private void quickCmdBtn_Click(object sender, RoutedEventArgs e)
        {
            string command = (e.OriginalSource as Button)?.DataContext as string;
            if (command != null)
            {
                this.addToLog($"Sending quick command '{command}'", true);
                List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

                DoExecuteCommand(command, conns.ToArray());
            }
            else
            {
                this.addToLog("Quick command value was null, wtf.", true);
            }
        }

        private void minimapVelocityScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MiniMapVelocityScale = (float)minimapVelocityScaleSlider.Value;
        }

        private void resetQuietModeBtn_Click(object sender, RoutedEventArgs e)
        {
            Connection[] conns = null;
            lock (connectionsCameraOperatorsMutex)
            {
                conns = connections.ToArray();
            }
            if(conns != null && conns.Length > 0)
            {
                foreach(Connection conn in conns)
                {
                    conn.beQuietUntil = DateTime.Now - new TimeSpan(999,0,0);
                }
            }
        }

        private void unselectBtn_Click(object sender, RoutedEventArgs e)
        {
            DataGrid dataGrid = connectionsDataGrid;
            if (dataGrid != null)
            {
                connectionsDataGrid.SelectedItem = null;
            }
        }

        private void levelshotBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveLevelshot(infoPool.levelShot,0);
        }

        private void levelshotBtnOver200_Click(object sender, RoutedEventArgs e)
        {
            SaveLevelshot(infoPool.levelShot, 200);
        }

        private float above1To2SoftApproach(float value) // the result of this will approach 2 but never reach it. perfection
        {
            return (1.0f-1.0f/(1f+(float)Math.Pow(value,0.833333333333333333f)))*2.0f; // dont ask me why exactly 0.83333. i just aligned the two derivatives in le graphing calculator :)
        }

        private const float invGamma = 1f / 2.4f;
        private const float invGamma5 = 1f / 5f;
        private const float invGamma10 = 1f / 10f;
        public void SaveLevelshot(LevelShotData levelshotData, uint skipLessThanPixelCount = 0, double blockIfOtherLevelshotInPastSeconds = 0.0)
        {
            if (levelshotData is null) return;
            lock (levelshotData.lastSavedLock)
            {
                if (blockIfOtherLevelshotInPastSeconds != 0.0 && (DateTime.Now - levelshotData.lastSaved).TotalSeconds < blockIfOtherLevelshotInPastSeconds)
                {
                    return;
                }
                else if (levelshotData.changesSinceLastSaved < skipLessThanPixelCount)
                {
                    return;
                }
                else
                {
                    levelshotData.lastSaved = DateTime.Now;
                    levelshotData.changesSinceLastSaved++;
                }
            }
            string filenameString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "_" + lastMapName + "_" + (serverName == null ? netAddress.ToString() : netAddress.ToString()) + "_" + serverName;
            float[,,] levelshotDataLocal = (float[,,])levelshotData.data.Clone();
            TaskManager.TaskRun(()=> {

                try
                {
                    string mutexAddress = netAddress is null ? "": netAddress.ToString().Replace('.', '_').Replace(':', '_');

                    //lock (forcedLogFileName)
                    using (new GlobalMutexHelper($"JKWatcherLevelshotFilenameMutex{mutexAddress}"))
                    {
                        SaveLevelshotReal(levelshotDataLocal, skipLessThanPixelCount, filenameString);
                    }
                }
                catch (Exception ex)
                {
                    // Failed to get  mutex, weird...
                }

            }, $"Levelshot saver ({netAddress},{ServerName})");
        }
        public void SaveLevelshotReal(float[,,] levelshotDataLocal, uint skipLessThanPixelCount, string filenameString)
        {
            int width = levelshotDataLocal.GetLength(0);
            int height = levelshotDataLocal.GetLength(1);

            List<float> brightnessValuesList = new List<float>();

            //double totalUsedPixelBrightness = 0;
            double divider = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = levelshotDataLocal[x, y, c];
                        if (valueHere > 0.0f)
                        {
                            //totalUsedPixelBrightness += valueHere;
                            divider++;
                            brightnessValuesList.Add(valueHere);
                        }
                    }
                }
            }

            if (divider < (double)skipLessThanPixelCount) return;

            //double averageBrightness = totalUsedPixelBrightness / divider;
            //float multiplier = 1.0f/ (float)averageBrightness;
            brightnessValuesList.Sort();
            float roughMedianBrightness = brightnessValuesList.Count > 0 ? brightnessValuesList[brightnessValuesList.Count / 2] : 1.0f;
            float multiplier = 1.0f / (float)roughMedianBrightness;

            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            ByteImage bi = Helpers.BitmapToByteArray(bmp);
            bmp.Dispose();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = levelshotDataLocal[x, y, c];
                        //float gammaValue = valueHere > 1.0 ? (float)Math.Pow(valueHere * multiplier, invGamma5) : (float)Math.Pow(valueHere * multiplier, invGamma);
                        float gammaValue = valueHere > 1.0 ? above1To2SoftApproach(valueHere * multiplier) : (float)Math.Pow(valueHere * multiplier, invGamma);
                        byte byteValue = (byte)Math.Clamp(gammaValue* 255.0f * 0.5f, 0, 255.0f);
                        int yInv = height - 1 - y;
                        int xInv = width - 1 - x;
                        bi.imageData[bi.stride * yInv + xInv * 3 + c] = byteValue;
                    }
                }
            }
            bmp = Helpers.ByteArrayToBitmap(bi);
            string imagesSubDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "images", "activityShots");
            Directory.CreateDirectory(imagesSubDir);
            filenameString = Helpers.MakeValidFileName(filenameString) + ".png";
            filenameString = Helpers.GetUnusedFilename(filenameString);
            filenameString = System.IO.Path.Combine(imagesSubDir, filenameString);
            bmp.Save(filenameString);
            bmp.Dispose();
        }

        private void buttonHitBtn_Click(object sender, RoutedEventArgs e)
        {
            List<Connection> conns = connectionsDataGrid.SelectedItems.Cast<Connection>().ToList();

            Int64 btns = 0;
            if(Int64.TryParse(commandLine.Text,out btns))
            {
                foreach(Connection conn in conns)
                {
                    conn.QueueButtonPress(btns);
                }
            }
        }

        private void addStrobeWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.StrobeCameraOperator>();
        }

        public bool requestConnectionDestruction(Connection conn)
        {
            lock (connectionsCameraOperatorsMutex) { 
                if (conn.CameraOperator != null)
                {
                    addToLog("Cannot remove connection bound to a camera operator");
                    return false;
                }
                else if (!connections.Contains(conn))
                {
                    addToLog("WEIRD: Camera operator requesting deletion of a connection that is not part of this ConnectedServerWindow.");
                    return false;
                }
                else
                {

                    //conn.disconnect();
                    conn.CloseDown();
                    connections.Remove(conn);
                    updateIndices();
                    return true;
                }
            }
        }
    }
}
