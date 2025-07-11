﻿using JKClient;
using JKWatcher.RandomHelpers;
using Salaros.Configuration;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using System.Windows.Threading;

// TODO: Javascripts that can be executed and interoperate with the program?
// Or if too hard, just .ini files that can be parsed for instructions on servers that must be connected etc.
// TODO Save server players info as json


namespace JKWatcher
{

    public class SocksSettingsGlobal : INotifyPropertyChanged
    {
        [Flags]
        public enum SocksCondition
        {
            NWH = 1,
            JK2 = 2,
            JKA = 4,
            MOH = 8,
        }
        public string server { get; set; } = null;
        public int port { get; set; } = -1;
        public string user { get; set; } = null;
        public string pw { get; set; } = null;
        public SocksCondition conditions { get; set; } = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool nwhCondition { get; set; } = false;

        public SocksSettingsGlobal()
        {
            this.PropertyChanged += SocksSettings_PropertyChanged;
        }

        private void SocksSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "nwhCondition")
            {
                SocksCondition newConditions = 0;
                if (this.nwhCondition)
                {
                    newConditions |= SocksCondition.NWH;
                }
                if(conditions != newConditions)
                {
                    conditions = newConditions;
                }
            }
            else if(e.PropertyName == "conditions")
            {
                if (conditions.HasFlag(SocksCondition.NWH) != nwhCondition)
                {
                    nwhCondition = conditions.HasFlag(SocksCondition.NWH);
                }
            }
        }
        public SocksProxy? GetProxy()
        {
            SocksProxy proxy = new SocksProxy();
            try
            {
                proxy.address = NetAddress.FromString(this.server, (ushort)this.port, false);
            } catch(Exception ex)
            {
                return null;
            }
            proxy.username = this.user;
            proxy.password = this.pw;
            if (string.IsNullOrWhiteSpace(proxy.username))
            {
                return null;
            }
            if (proxy.address == null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(proxy.password))
            {
                return null;
            }
            return proxy;
        }
        public SocksProxy? GetProxyForServer(ServerInfo si)
        {
            if(si.NWH && this.conditions.HasFlag(SocksCondition.NWH))
            {
                return GetProxy();
            }
            return null;
        }

        ~SocksSettingsGlobal()
        {
            this.PropertyChanged -= SocksSettings_PropertyChanged;
        }
    }
    class IntermissionCamPositionTuple
    {
        public string mapName { get; set; }
        public enum OverrideChoice {
            None,
            Override,
            KeepOld
        }
        public IntermissionCamPosition newPos { get; set; }
        public IntermissionCamPosition oldPos { get; set; }
        public UInt64 dupeCount { get; set; }
        public float differencePos { get; set; } = 0;
        public float differenceAng { get; set; } = 0;
        public OverrideChoice overrideChoice { get; set; } = OverrideChoice.None;

        public IntermissionCamPositionTuple(string map, IntermissionCamPosition newPosition, IntermissionCamPosition oldPosition, UInt64 dupesCount)
        {
            mapName = map;
            newPos = newPosition;
            oldPos = oldPosition;
            dupeCount = dupesCount;
            (differencePos, differenceAng) = newPos.DistanceToOther(oldPos);
        }
    }

    //using IntermissionCamPositionTuple = System.Tuple<IntermissionCamPosition, IntermissionCamPosition,UInt64>; // new, old, dupe count

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private List<ConnectedServerWindow> connectedServerWindows = new List<ConnectedServerWindow>();


        static Dictionary<NetAddress,Tuple<DateTime,double>> lastTimeDisconnected = new Dictionary<NetAddress, Tuple<DateTime, double>>(new NetAddressComparer());
        static Dictionary<NetAddress,Tuple<DateTime,double>> lastTimeKicked = new Dictionary<NetAddress, Tuple<DateTime, double>>(new NetAddressComparer());
        static Random timeFromDisconnectedTimeRangeModifierRandom = new Random();
        static Random timeFromKickedTimeRangeModifierRandom = new Random();

        public static void setServerLastDisconnectedNow(NetAddress address)
        {
            if (address == null)
            {
                // idk how it would happen but whatever
                return;
            }
            lock (lastTimeDisconnected)
            {
                lastTimeDisconnected[address] = new Tuple<DateTime, double>(DateTime.Now, timeFromDisconnectedTimeRangeModifierRandom.NextDouble());
            }
        }
        public static void setServerLastKickedNow(NetAddress address)
        {
            if (address == null)
            {
                // idk how it would happen but whatever
                return;
            }
            lock (lastTimeKicked)
            {
                lastTimeKicked[address] = new Tuple<DateTime, double>(DateTime.Now, timeFromKickedTimeRangeModifierRandom.NextDouble());
            }
        }
        public static (DateTime?,double?) getServerLastDisconnected(NetAddress address)
        {
            if (address == null)
            {
                return (null,null);
            }
            lock (lastTimeDisconnected)
            {
                if (lastTimeDisconnected.ContainsKey(address))
                {
                    return (lastTimeDisconnected[address].Item1, lastTimeDisconnected[address].Item2);
                } else
                {
                    return (null, null);
                }
            }
        }
        public static (DateTime?,double?) getServerLastKicked(NetAddress address)
        {
            if (address == null)
            {
                return (null,null);
            }
            lock (lastTimeKicked)
            {
                if (lastTimeKicked.ContainsKey(address))
                {
                    return (lastTimeKicked[address].Item1, lastTimeKicked[address].Item2);
                } else
                {
                    return (null, null);
                }
            }
        }

        public SocksSettingsGlobal socksSettingsGlobal { get; private set; } = new SocksSettingsGlobal();

        public bool executionInProgress { get; private set; } = false;

        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();

        private void SpawnArchiveScript()
        {
            try
            {
                using (new GlobalMutexHelper("JKWatcherArchiveScriptSpawn"))
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts","archive.sh");
                    if (!File.Exists(path))
                    {
                        File.WriteAllBytes(path,Helpers.GetResourceData("archive.sh"));
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to spawn archive script.", e.ToString() });
            }
        }

        public MainWindow()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher"));
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            AsyncPersistentDataManager<IntermissionCamPosition>.Init();
            AsyncPersistentDataManager<DefragAverageMapTime>.Init();

            SaberAnimationStuff.Init();

            RandomHelpers.NumberImages.Init();

            // Check botroutes
            BotRouteManager.Initialize();

            InitializeComponent();

            protocols.ItemsSource = System.Enum.GetValues(typeof(ProtocolVersion));
            protocols.SelectedItem = ProtocolVersion.Protocol15;
            //getServers();
            string[] configs =null;
            try
            {
                configs = Directory.GetFiles("configs", "*.ini").Select(s => System.IO.Path.GetFileNameWithoutExtension(s)).ToArray();
                configsComboBox.ItemsSource = configs;
            } catch(Exception e)
            {
                // Don't care.
            }

            SunsNotificationClient.sunsNotificationReceived += SunsNotificationClient_sunsNotificationReceived;



            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { ctfAutoConnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted),"CTF Auto Connecter");
            lock (backgroundTasks)
            {
                backgroundTasks.Add(tokenSource);
            }

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { fastDelayedConnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted),"Fast Delayed Connecter");
            lock (backgroundTasks)
            {
                backgroundTasks.Add(tokenSource);
            }

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { playerCountProgressBarUpdater(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted),"Player count progress bar updater");
            lock (backgroundTasks)
            {
                backgroundTasks.Add(tokenSource);
            }

            //Timeline.DesiredFrameRateProperty.OverrideMetadata(
            //    typeof(Timeline),
            //    new FrameworkPropertyMetadata { DefaultValue = 165 }
            //);
            HiResTimerSetter.UnlockTimerResolution();
            WindowPositionManager.Activate();

            executingTxt.DataContext = this;
            socksSettingsGlobalBox.DataContext = this.socksSettingsGlobal;
            SpawnArchiveScript();
            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed; ;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            CloseDown();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            var tasks = TaskManager.GetRunningTasks();
            foreach(var task in tasks)
            {
                if (task.essential)
                {
                    e.Cancel = true;
                    MessageBox.Show($"Cannot close main window yet. Essential task {task.taskName} is still running.");
                    return;
                }
            }
        }

        private void SunsNotificationClient_sunsNotificationReceived(object sender, NotificationEventArgs e)
        {
            ServerToConnect[] serversToTest = null;
            lock (serversToConnectDelayed)
            {
                serversToTest = serversToConnectDelayed.ToArray();
            }
            foreach(ServerToConnect server in serversToTest)
            {
                if (server.sunsNotificationServer == e.Address && server.sunsNotificationKey == e.Key)
                {
                    delayedForceConnect(server,true);
                }
            }
        }

        private bool cleanedUp = false;
        private object cleanUpLock = new object();

        ~MainWindow()
        {
            CloseDown();
        }

        private void CloseDown()
        {
            lock (cleanUpLock)
            {
                if (cleanedUp)
                {
                    return;
                }
                SunsNotificationClient.sunsNotificationReceived -= SunsNotificationClient_sunsNotificationReceived;
                lock (backgroundTasks)
                {
                    foreach (CancellationTokenSource backgroundTask in backgroundTasks)
                    {
                        backgroundTask.Cancel();
                    }
                    backgroundTasks.Clear();
                }
                cleanedUp = true;
            }
        }

        Mutex serversTextFileMutex = new Mutex();
        private NetAddress[] getManualServers()
        {
            lock (serversTextFileMutex)
            {
                List<NetAddress> manualServers = new List<NetAddress>();
                try
                {
                    if (File.Exists("servers.txt"))
                    {
                        string[] manualIPs = File.ReadAllLines("servers.txt");
                        foreach (string manualIP in manualIPs)
                        {
                            string stripped = manualIP.Trim();
                            if(stripped.Length > 0)
                            {
                                NetAddress tmpAddress = NetAddress.FromString(stripped);
                                if(tmpAddress != null)
                                {
                                    manualServers.Add(tmpAddress);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.logToFile(new string[] { e.ToString() });
                }
                return manualServers.ToArray();
            }
        }


        private void saveServerStats(IEnumerable<ServerInfo> data)
        {

            try
            {
                using (new GlobalMutexHelper("JKWatcherSQliteStatsMutex"))
                {
                    var db = new SQLiteConnection(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "serverStats.db"),false);

                    db.CreateTable<ServerInfoPublic>();
                    db.BeginTransaction();
                    // Save stats.
                    foreach (ServerInfo serverInfo in data)
                    {
                        if (serverInfo.StatusResponseReceived)
                        {
                            db.Insert(ServerInfoPublic.convertFromJKClient(serverInfo));
                        }
                    }
                    db.Commit();
                    db.Close();
                    db.Dispose();
                }
            } catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to save server stats to database.",e.ToString() });
            }
        }


        List<ServerToConnect> serversToConnectDelayed = new List<ServerToConnect>();

        List<NetAddress> autoConnectRecentlyClosedBlockList = new List<NetAddress>(); // When we close a window, we don't wanna reconnect to it immediately (because the auto connecter might have requested the server info before we closed the window and thus think players are still on there, meaning our own connection that we just closed)

        Regex ipv4Regex = new Regex(@"^(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]\d|\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]\d|\d)){3}(?<port>:\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private async void ctfAutoConnecter(CancellationToken ct)
        {
            bool nextCheckFast = false;
            while (true)
            {
                int autoJoinCheckInterval = 2;
                Dispatcher.Invoke(() => {
                    if (!int.TryParse(autoJoinCheckIntervalTxt.Text, out autoJoinCheckInterval))
                    {
                        autoJoinCheckInterval = 2;
                    }
                });

                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                System.Threading.Thread.Sleep(nextCheckFast ? 60000 :  60000 * autoJoinCheckInterval); // every 2 min or 1 min if fast recheck requested (see code below)

                lock (autoConnectRecentlyClosedBlockList)
                {
                    autoConnectRecentlyClosedBlockList.Clear();
                }

                if (executionInProgress) continue;

                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                nextCheckFast = false;

                bool ctfAutoJoinActive = false;
                bool ctfAutoJoinWithStrobeActive = false;
                bool ffaAutoJoinActive = false;
                bool ffaAutoJoinSilentActive = false;
                bool ffaAutoJoinKickable = false;
                string ffaAutoJoinExclude = null;
                string ffaAutoJoinConditionalCommands = null;
                string ctfAutoJoinConditionalCommands = null;
                int ctfMinPlayersForJoin = 4;
                int ffaMinPlayersForJoin = 2;
                int ffaKickReconnectDelay = 0;
                bool jkaMode = false;
                bool mohMode = false;
                bool allJK2Versions = false;
                bool delayedConnecterActive = false;
                Dispatcher.Invoke(()=> {
                    ctfAutoJoinActive = ctfAutoJoin.IsChecked == true;
                    ctfAutoJoinWithStrobeActive = ctfAutoJoinWithStrobe.IsChecked == true;
                    ffaAutoJoinActive = ffaAutoJoin.IsChecked == true;
                    ffaAutoJoinSilentActive = ffaAutoJoinSilent.IsChecked == true;
                    ffaAutoJoinKickable = ffaAutoJoinKickableCheck.IsChecked == true;
                    ffaAutoJoinExclude = ffaAutoJoinExcludeTxt.Text;
                    ffaAutoJoinConditionalCommands = ffaAutojoinConditionalCmdsTxt.Text;
                    ctfAutoJoinConditionalCommands = ctfAutojoinConditionalCmdsTxt.Text;
                    jkaMode = jkaModeCheck.IsChecked == true;
                    mohMode = mohModeCheck.IsChecked == true;
                    allJK2Versions = allJK2VersionsCheck.IsChecked == true;
                    delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true;
                    if (!int.TryParse(ctfAutoJoinMinPlayersTxt.Text, out ctfMinPlayersForJoin))
                    {
                        ctfMinPlayersForJoin = 3;
                    }
                    if (!int.TryParse(ffaAutoJoinMinPlayersTxt.Text, out ffaMinPlayersForJoin))
                    {
                        ffaMinPlayersForJoin = 2;
                    }
                    if (!int.TryParse(ffaAutoJoinKickReconnectDelayTxt.Text, out ffaKickReconnectDelay))
                    {
                        ffaKickReconnectDelay = 0;
                    }
                });

                string[] ffaAutoJoinExcludeList = ffaAutoJoinExclude == null ? null : ffaAutoJoinExclude.Split(",",StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);

                List<Tuple<NetAddress, bool>> ffaAutoJoinIPExludeList = new List<Tuple<NetAddress, bool>>(); // The bool is there to say whether port is specified.

                if (ffaAutoJoinExcludeList != null)
                {
                    foreach (string excludeString in ffaAutoJoinExcludeList)
                    {
                        Match ipv4match;
                        if ((ipv4match = ipv4Regex.Match(excludeString)).Success)
                        {
                            NetAddress addr = NetAddress.FromString(excludeString);
                            if (!ipv4match.Groups["port"].Success)
                            {
                                ffaAutoJoinIPExludeList.Add(new Tuple<NetAddress, bool>(addr, false));
                            } else {
                                ffaAutoJoinIPExludeList.Add(new Tuple<NetAddress, bool>(addr, true));
                            }
                        }
                    }
                }



                NetAddress[] manualServers = getManualServers();
                List<NetAddress> hiddenServersAll = new List<NetAddress>();
                hiddenServersAll.AddRange(manualServers);
                lock (serversToConnectDelayed) {
                    foreach (ServerToConnect srvTC in serversToConnectDelayed)
                    {
                        //if (srvTC.pollingInterval.HasValue && !fastDelayedConnecterBroken) continue; // Servers with custom polling interval are handled elsewhere.
                        if (srvTC.ip != null)
                        {
                            hiddenServersAll.Add(srvTC.ip);
                        }
                    }
                }
                ServerBrowser.SetHiddenServers(hiddenServersAll.ToArray());


                int delayedConnectServersCount = 0;
                lock (serversToConnectDelayed)
                {
                    delayedConnectServersCount = serversToConnectDelayed.Count;
                }

                if (ctfAutoJoinActive || ffaAutoJoinActive || (delayedConnectServersCount > 0 && delayedConnecterActive))
                {
                    IEnumerable<ServerInfo> servers = null;


                    ServerBrowser serverBrowser = null;
                    if (jkaMode)
                    {
                        serverBrowser = new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { RefreshTimeout = 30000L, ForceStatus = true };
                    }
                    else if (mohMode)
                    {
                        serverBrowser = new ServerBrowser(new MOHBrowserHandler(ProtocolVersion.Protocol8,true)) { RefreshTimeout = 30000L, ForceStatus = true };
                    }
                    else
                    {
                        serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, allJK2Versions || delayedConnectServersCount > 0)) { RefreshTimeout = 30000L, ForceStatus = true }; // The autojoin gets a nice long refresh time out to avoid wrong client numbers being reported.
                    }

                    serverBrowser.InternalTaskStarted += ServerBrowser_InternalTaskStarted;

                    try
                    {
                        serverBrowser.Start(ExceptionCallback);
                        servers = await serverBrowser.GetNewList();
                        //servers = await serverBrowser.RefreshList();
                    }
                    catch (Exception e)
                    {
                        serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                        // Just in case getting servers crashes or sth.
                        serverBrowser.Dispose();
                        continue;
                    }

                    if (servers == null) {
                        serverBrowser.Dispose();
                        continue;
                    }

                    List<ServerInfo> baselineFilteredServers = new List<ServerInfo>();
                    foreach (ServerInfo serverInfo in servers)
                    {
                        if(serverInfo.HostName != null) // Some just come back like that sometimes, idk why.
                        {
                            baselineFilteredServers.Add(serverInfo);
                        }
                        else
                        {
                            continue;
                        }
                        lock (autoConnectRecentlyClosedBlockList)
                        {
                            if (autoConnectRecentlyClosedBlockList.Contains(serverInfo.Address,new NetAddressComparer()))
                            {
                                // We were connected to this server recently, and disconnected after the current iteration of the autoconnecter started.
                                // Thus the info we get about how many players are on the server might include ourselves, which is not intended and 
                                // can lead to unintended reconnecting to an empty server.
                                // Hence, skip this server in this run.
                                continue;
                            }
                        }
                        bool statusReceived = serverInfo.StatusResponseReceived; // We get this value first because it seems in rare situations 1.03 servers can slip through. Maybe status just arrives a bit later and then with very unlucky timing the status received underneath passes but higher up the version was still set to JO_v1_02 because the status hadn't been received yet?
                        if (serverInfo.Version == ClientVersion.JO_v1_02 || jkaMode || mohMode || allJK2Versions || delayedConnectServersCount > 0)
                        {
                            bool alreadyConnected = false;
                            lock (connectedServerWindows)
                            {
                                foreach (ConnectedServerWindow window in connectedServerWindows)
                                {
                                    if (window.netAddress == serverInfo.Address && window.protocol == serverInfo.Protocol)
                                    {
                                        alreadyConnected = true;
                                    }
                                }
                            }
                            // We want to be speccing/recording this.
                            // Check if we are already connected. If so, do nothing.
                            if(!alreadyConnected) {

                                bool configgedRequirementsExplicitlyNotMet = false;
                                if (delayedConnectServersCount > 0 && delayedConnecterActive)
                                {
                                    ServerToConnect srvTCChosen = null;
                                    lock (serversToConnectDelayed) { 
                                        foreach (ServerToConnect srvTC in serversToConnectDelayed)
                                        {
                                            bool serverMatchedButMayNotSatisfyConditions = false;
                                            if (srvTC.FitsRequirements(serverInfo, ref serverMatchedButMayNotSatisfyConditions))
                                            {
                                                if (!srvTC.pollingInterval.HasValue || srvTC.pollingBroken)
                                                { // Servers with custom polling interval are handled elsewhere.

                                                    srvTCChosen = srvTC;
                                                    break;
                                                }
                                            }
                                            configgedRequirementsExplicitlyNotMet = configgedRequirementsExplicitlyNotMet || serverMatchedButMayNotSatisfyConditions;
                                        }
                                        if(srvTCChosen != null)
                                        {
                                            Dispatcher.Invoke(() => {
                                                delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true; // Just double check to be safe.
                                            });
                                            if (delayedConnecterActive)
                                            {
                                                ConnectFromConfig(serverInfo, srvTCChosen);
                                            }
                                            continue;
                                            //serversToConnectDelayed.Remove(srvTCChosen); // actually dont delete it.
                                        }
                                    }
                                }

                                if (!configgedRequirementsExplicitlyNotMet)
                                {

                                    if(ctfAutoJoinActive && (serverInfo.GameType == GameType.CTF || serverInfo.GameType == GameType.CTY)) { 
                                        if (serverInfo.RealClients >= ctfMinPlayersForJoin && statusReceived)
                                        {
                                
                                            Dispatcher.Invoke(()=> {

                                                lock (connectedServerWindows)
                                                {
                                                    ConnectedServerWindow.ConnectionOptions connOptions = string.IsNullOrWhiteSpace(ctfAutoJoinConditionalCommands) ? null : new ConnectedServerWindow.ConnectionOptions() { conditionalCommands = ctfAutoJoinConditionalCommands };
                                                    SocksProxy? proxy = this.socksSettingsGlobal.GetProxyForServer(serverInfo);
                                                    if(proxy != null)
                                                    {
                                                        if(connOptions == null)
                                                        {
                                                            connOptions = new ConnectedServerWindow.ConnectionOptions();
                                                        }
                                                        connOptions.proxy = proxy;
                                                    }
                                                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,null, connOptions);
                                                    connectedServerWindows.Add(newWindow);
                                                    newWindow.Loaded += NewWindow_Loaded;
                                                    newWindow.Closed += NewWindow_Closed;
                                                    newWindow.ShowActivated = false;
                                                    newWindow.Show();
                                                    var ctfOperator = newWindow.createCTFOperator();
                                                    if (ctfAutoJoinWithStrobeActive)
                                                    {
                                                        if(ctfOperator != null)
                                                        {
                                                            ctfOperator.SetOption("withStrobe","1");
                                                        } else
                                                        {
                                                            newWindow.createStrobeOperator(); // Dunno how that'd ever happen tbh but oh well
                                                        }
                                                    }
                                                    newWindow.recordAll();
                                                }
                                            });
                                        } else if (serverInfo.Clients >= ctfMinPlayersForJoin && !statusReceived)
                                        {
                                            // If there's a potential candidate but we haven't received info about whether the players are real players, make next refresh with less waiting time. It's possible the StatusResponse just didn't
                                            // arrive for some reason
                                            nextCheckFast = true;
                                        }
                                    } else if (ffaAutoJoinActive && !(serverInfo.GameType == GameType.CTF || serverInfo.GameType == GameType.CTY) && !serverInfo.NeedPassword)
                                    {
                                        // Check for exclude filter
                                        if(ffaAutoJoinExcludeList != null)
                                        {
                                            bool serverIsExcluded = false;
                                            foreach(string excludeString in ffaAutoJoinExcludeList)
                                            {
                                                //Match ipv4match;
                                                if(serverInfo.HostName.Contains(excludeString,StringComparison.OrdinalIgnoreCase) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(excludeString, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    serverIsExcluded = true;
                                                    break;
                                                } /*else if ((ipv4match = ipv4Regex.Match(excludeString)).Success)
                                                {
                                                    NetAddress addr = NetAddress.FromString(excludeString);
                                                    if (!ipv4match.Groups["port"].Success)
                                                    {
                                                        addr = new NetAddress(addr.IP, serverInfo.Address.Port);
                                                    }
                                                    if(addr == serverInfo.Address)
                                                    {
                                                        serverIsExcluded = true;
                                                        break;
                                                    }
                                                }*/
                                            }
                                            foreach(Tuple<NetAddress,bool> excludedIP in ffaAutoJoinIPExludeList)
                                            {
                                                if (excludedIP.Item2 && excludedIP.Item1 == serverInfo.Address)
                                                {
                                                    serverIsExcluded = true;
                                                    break;
                                                } else if (!excludedIP.Item2)
                                                {
                                                    NetAddress addr = new NetAddress(excludedIP.Item1.IP, serverInfo.Address.Port);
                                                    if (addr == serverInfo.Address)
                                                    {
                                                        serverIsExcluded = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            if (serverIsExcluded) continue; // Skip this one.
                                        }

                                        // If we got kicked recently and we have an ffa kick reconnect delay configured,
                                        // only connect if we are past the delay.
                                        if(ffaKickReconnectDelay > 0)
                                        {
                                            (DateTime? time,_) = getServerLastKicked(serverInfo.Address);
                                            if (time.HasValue)
                                            {
                                                if ((DateTime.Now-time.Value).TotalMinutes < ffaKickReconnectDelay)
                                                {
                                                    continue;
                                                }
                                            }
                                        }

                                        if (serverInfo.RealClients >= ffaMinPlayersForJoin && statusReceived && !serverInfo.NoBots)
                                        {

                                            Dispatcher.Invoke(() => {

                                                lock (connectedServerWindows)
                                                {
                                                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,null,new ConnectedServerWindow.ConnectionOptions(){ conditionalCommands = string.IsNullOrWhiteSpace(ffaAutoJoinConditionalCommands) ? null : ffaAutoJoinConditionalCommands, autoUpgradeToCTF = true, autoUpgradeToCTFWithStrobe = ctfAutoJoinWithStrobeActive, attachClientNumToName=false, demoTimeColorNames = false, silentMode = ffaAutoJoinSilentActive, disconnectTriggers = ffaAutoJoinKickable ? "kicked,playercount_under:1:1200000" : null, proxy = this.socksSettingsGlobal.GetProxyForServer(serverInfo) });
                                                    connectedServerWindows.Add(newWindow);
                                                    newWindow.Loaded += NewWindow_Loaded;
                                                    newWindow.Closed += NewWindow_Closed;
                                                    newWindow.ShowActivated = false;
                                                    newWindow.Show();
                                                    newWindow.recordAll();
                                                }
                                            });
                                        }
                                        else if (serverInfo.Clients >= ffaMinPlayersForJoin && !statusReceived)
                                        {
                                            // If there's a potential candidate but we haven't received info about whether the players are real players, make next refresh with less waiting time. It's possible the StatusResponse just didn't
                                            // arrive for some reason
                                            nextCheckFast = true;
                                        } else if (serverInfo.NoBots)
                                        {
                                            Helpers.logToSpecificDebugFile(new string[] { DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss G\\MTzzz]"),serverInfo.HostName?.ToString(),serverInfo.Address?.ToString(),serverInfo.InfoStringValues?.ToString(),serverInfo.StatusInfoStringValues?.ToString(),"" },"noBotServersDebug.log",true);
                                        }
                                    }
                                } // if (!configgedRequirementsExplicitlyNotMet)
                            } // if(!alreadyConnected && !serverInfo.NeedPassword) {

                        }


                    }
                    saveServerStats(baselineFilteredServers);
                    serverBrowser.Stop();
                    serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                    serverBrowser.Dispose();

                }
            }
        }

        private void ServerBrowser_InternalTaskStarted(object sender, in Task task, string description)
        {
            TaskManager.RegisterTask(task, $"ServerBrowser (MainWindow): {description}");
        }

        class DelayConnecterData
        {
            public Task<ServerInfo> pollTask = null;
            public DateTime lastTimePolled = DateTime.Now - new TimeSpan(240000, 0, 0);
            public DateTime lastTimeGotAnswer = DateTime.Now;
        };

       // bool fastDelayedConnecterBroken = false;

        //public bool delayedConnecterActiveCheckIsChecked { get; set; } = false;

        private async void fastDelayedConnecter(CancellationToken ct)
        {
            bool nextCheckFast = false;

            ServerBrowser serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, true));
            ServerBrowser serverBrowserMOH = new ServerBrowser(new MOHBrowserHandler(ProtocolVersion.Protocol8, true));

            serverBrowser.InternalTaskStarted += ServerBrowser_InternalTaskStarted;
            serverBrowserMOH.InternalTaskStarted += ServerBrowser_InternalTaskStarted;

            try
            {

                serverBrowser.Start(ExceptionCallback);
                serverBrowserMOH.Start(ExceptionCallback);
                //servers = await serverBrowser.GetNewList();
                //servers = await serverBrowser.RefreshList();
            }
            catch (Exception e)
            {
                // Just in case getting servers crashes or sth.
                //continue;
                Helpers.logToFile(e.ToString());
            }

            Dictionary<ServerToConnect, DelayConnecterData> serverPollingInfo = new Dictionary<ServerToConnect, DelayConnecterData>();
            while (true)
            {

                System.Threading.Thread.Sleep(500);
                if (ct.IsCancellationRequested) goto cleanUp;
                bool delayedConnecterActive = true;
                Dispatcher.Invoke(() => {
                    delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true;
                });

                if (!delayedConnecterActive) continue;

                lock (serversToConnectDelayed)
                {
                    foreach (ServerToConnect srvTC in serversToConnectDelayed)
                    {
                        if (!srvTC.pollingInterval.HasValue || srvTC.ip == null || !srvTC.active) continue; // Servers with custom polling interval are handled elsewhere.

                        bool alreadyConnected = false;
                        lock (connectedServerWindows)
                        {
                            foreach (ConnectedServerWindow window in connectedServerWindows)
                            {
                                if (window.netAddress == srvTC.ip)
                                {
                                    alreadyConnected = true;
                                }
                            }
                        }

                        if (alreadyConnected)
                        {
                            if (serverPollingInfo.ContainsKey(srvTC))
                            {
                                serverPollingInfo[srvTC].lastTimePolled = DateTime.Now; // Avoid fast connecter being identified as broken just because we were connected and then disconnected.
                                serverPollingInfo[srvTC].lastTimeGotAnswer = DateTime.Now; // Avoid fast connecter being identified as broken when we were connected and then disconnect (will then trigger as broken a few times)
                            }
                            continue;
                        }

                        if (!serverPollingInfo.ContainsKey(srvTC))
                        {
                            serverPollingInfo[srvTC] = new DelayConnecterData();
                        }

                        if(serverPollingInfo[srvTC].pollTask == null && (DateTime.Now-serverPollingInfo[srvTC].lastTimePolled).TotalMilliseconds > srvTC.pollingInterval.Value)
                        {
                            serverPollingInfo[srvTC].pollTask = (srvTC.mohProtocol ? serverBrowserMOH : serverBrowser).GetFullServerInfo(srvTC.ip, srvTC.minRealPlayers > 0, true, srvTC.pollingInterval.Value); // Only need status if we have a min real player requirement.
                            serverPollingInfo[srvTC].lastTimePolled = DateTime.Now;
                        }

                        double millisecondsSinceLastAnswer = (DateTime.Now - serverPollingInfo[srvTC].lastTimeGotAnswer).TotalMilliseconds;
                        if (millisecondsSinceLastAnswer > Math.Max(srvTC.pollingInterval.Value*10,60000))
                        {
                            if (!srvTC.pollingBroken)
                            {
                                srvTC.pollingBroken = true;
                                // Let main loop take care of it then.
                                Helpers.logToFile($"ERROR: fastDelayedConnecter {srvTC.sectionName} seems to be broken. No answer received in a long time ({millisecondsSinceLastAnswer}ms).");
                            }
                        } else
                        {
                            if (srvTC.pollingBroken)
                            {
                                srvTC.pollingBroken = false;
                                // Let main loop take care of it then.
                                Helpers.logToFile($"ERROR: fastDelayedConnecter {srvTC.sectionName} unbroken again. Last answer recent ({millisecondsSinceLastAnswer}ms).");
                            }
                        }

                    }
                }

                foreach (KeyValuePair<ServerToConnect, DelayConnecterData> kvp in serverPollingInfo)
                {
                    DelayConnecterData dcd = kvp.Value;
                    if (dcd.pollTask != null && dcd.pollTask.IsCompleted)
                    {
                        if(dcd.pollTask.Status == TaskStatus.RanToCompletion)
                        {
                            dcd.lastTimeGotAnswer = DateTime.Now;
                            ServerInfo thisServerInfo = dcd.pollTask.Result;
                            ServerToConnect stc = kvp.Key;
                            bool isMatchThough = false;
                            if (stc.FitsRequirements(thisServerInfo, ref isMatchThough))
                            {
                                Dispatcher.Invoke(() => {
                                    delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true; // Just double check to be safe.
                                });
                                if (delayedConnecterActive)
                                {
                                    Debug.WriteLine($"Server {stc.ip.ToString()} polled, fits requirements. IsMatch: {isMatchThough}.");
                                    ConnectFromConfig(thisServerInfo, stc);
                                }
                            } else
                            {
                                Debug.WriteLine($"Server {stc.ip.ToString()} polled, doesn't fit requirements. IsMatch: {isMatchThough}.");
                            }
                        } else
                        {
                            Debug.WriteLine($"Unknown error polling server {kvp.Key.ip.ToString()}.");
                        }
                        dcd.pollTask = null;
                    }
                }
                /*lock (serversToConnectDelayed)
                {
                    foreach (ServerToConnect srvTC in serversToConnectDelayed)
                    {
                        if (!srvTC.pollingInterval.HasValue) continue; // Servers with custom polling interval are handled elsewhere.
                        bool serverMatchedButMayNotSatisfyConditions = false;
                        if (srvTC.FitsRequirements(serverInfo, ref serverMatchedButMayNotSatisfyConditions))
                        {
                            srvTCChosen = srvTC;
                            break;
                        }
                        configgedRequirementsExplicitlyNotMet = configgedRequirementsExplicitlyNotMet || serverMatchedButMayNotSatisfyConditions;
                    }
                    if (srvTCChosen != null)
                    {
                        ConnectFromConfig(serverInfo, srvTCChosen);
                        continue;
                        //serversToConnectDelayed.Remove(srvTCChosen); // actually dont delete it.
                    }
                }*/


            }
            
            cleanUp:
            serverBrowser.Dispose();
            serverBrowserMOH.Dispose();
        }

        private double lastTaskbarPlayerCountState = -1;

        private async void playerCountProgressBarUpdater(CancellationToken ct)
        {

            while (true)
            {

                System.Threading.Thread.Sleep(500);
                if (ct.IsCancellationRequested) return;
                string relevantGameTypesString = null;
                Dispatcher.Invoke(() => {
                    relevantGameTypesString = taskbarPlayerCountStatusGametypesTxt.Text;
                });

                //if (string.IsNullOrWhiteSpace(relevantGameTypesString)) continue;

                int relevantGameTypes = Connection.GameTypeStringToBitMask(relevantGameTypesString);

                //if (relevantGameTypes == 0) continue;

                double maxPlayerCount = 0;

                lock (connectedServerWindows)
                {
                    foreach (ConnectedServerWindow window in connectedServerWindows)
                    {
                        if (((1 << (int)window.gameType) & relevantGameTypes) > 0)
                        {
                            double numHEre = (double)window.truePlayerCountExcludingMyselfDelayed / (double)window.serverMaxClientsLimit;
                            if (!double.IsNaN(numHEre))
                            {
                                maxPlayerCount = Math.Max(maxPlayerCount, numHEre);
                            }
                        }
                    }
                }

                if (lastTaskbarPlayerCountState != maxPlayerCount)
                {
                    Dispatcher.Invoke(()=> {
                        TaskbarItemInfo tbii = this.TaskbarItemInfo;
                        if (tbii != null)
                        {
                            tbii.ProgressState = TaskbarItemProgressState.Paused;
                            tbii.ProgressValue = maxPlayerCount;
                            lastTaskbarPlayerCountState = maxPlayerCount;
                        }
                    });
                }

            }
        }

        private void NewWindow_Closed(object sender, EventArgs e)
        {
            ConnectedServerWindow newWindow = sender as ConnectedServerWindow;
            if(newWindow != null)
            {
                lock (connectedServerWindows)
                {
                    connectedServerWindows.Remove(newWindow);
                    newWindow.Closed -= NewWindow_Closed;
                }
                lock (autoConnectRecentlyClosedBlockList)
                {
                    // Tell our auto connecter that we only just disconnected from this server,
                    // so if it is currently in the process of requesting server infos,
                    // it should ignore this server because it might still include us.
                    autoConnectRecentlyClosedBlockList.Add(newWindow.netAddress);
                }
            } else
            {
                Helpers.logToFile("Error in NewWindow_Closed handler.");
            }
        }

        private void NewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Window wnd = (Window)sender;
            WindowPositionManager.RegisterWindow(wnd);
            wnd.Loaded -= NewWindow_Loaded;
        }

        public async void getServers()
        {
            IEnumerable<ServerInfo> servers = null;
            connectBtn.IsEnabled = false;
            connectRBtn.IsEnabled = false;
            connectSBtn.IsEnabled = false;
            connectRSBtn.IsEnabled = false;
            connectRS60Btn.IsEnabled = false;
            connectRS120Btn.IsEnabled = false;
            connectRS180Btn.IsEnabled = false;
            connectRS360Btn.IsEnabled = false;
            connectRSFRBtn.IsEnabled = false;

            //NetAddress[] manualServers = getManualServers();
            //ServerBrowser.SetHiddenServers(manualServers);
            NetAddress[] manualServers = getManualServers();
            List<NetAddress> hiddenServersAll = new List<NetAddress>();
            hiddenServersAll.AddRange(manualServers);
            lock (serversToConnectDelayed)
            {
                foreach (ServerToConnect srvTC in serversToConnectDelayed)
                {
                    if (srvTC.ip != null)
                    {
                        hiddenServersAll.Add(srvTC.ip);
                    }
                }
            }
            ServerBrowser.SetHiddenServers(hiddenServersAll.ToArray());


            bool jkaMode = false;
            bool mohMode = false;
            bool allJK2Versions = false;
            Dispatcher.Invoke(() => {
                jkaMode = jkaModeCheck.IsChecked == true;
                mohMode = mohModeCheck.IsChecked == true;
                allJK2Versions = allJK2VersionsCheck.IsChecked == true;
            });

            ServerBrowser serverBrowser = null;
            if (jkaMode)
            {
                serverBrowser = new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { ForceStatus = true };
            }
            else if (mohMode)
            {
                serverBrowser = new ServerBrowser(new MOHBrowserHandler(ProtocolVersion.Protocol8, true)) { ForceStatus = true };
            }
            else
            {
                serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, allJK2Versions)) { ForceStatus = true };
            }

            serverBrowser.InternalTaskStarted += ServerBrowser_InternalTaskStarted;

            try
            {

                serverBrowser.Start(ExceptionCallback);
                servers = await serverBrowser.GetNewList();
                //servers = await serverBrowser.RefreshList();
            } catch(Exception e)
            {
                serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                // Just in case getting servers crashes or sth.
                serverBrowser.Dispose();
                return;
            }

            if (servers == null) {
                serverBrowser.Dispose();
                return;
            }

            List<ServerInfo> filteredServers = new List<ServerInfo>();

            bool tmp = false;
            lock (serversToConnectDelayed)
            {
                foreach (ServerInfo serverInfo in servers)
                {
                    foreach (ServerToConnect stc in serversToConnectDelayed)
                    {
                        stc.FitsRequirements(serverInfo, ref tmp); // Just to update a serverinfo in there if needed.
                    }
                    if (serverInfo.Version == ClientVersion.JO_v1_02 || jkaMode || mohMode || allJK2Versions)
                    {
                        filteredServers.Add(serverInfo);
                    }
                }
            }


            serverBrowser.Stop();
            serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
            serverBrowser.Dispose();
            serverListDataGrid.ItemsSource = filteredServers;
            if(filteredServers.Count == 0)
            {
                connectBtn.IsEnabled = false;
                connectRBtn.IsEnabled = false;
                connectSBtn.IsEnabled = false;
                connectRSBtn.IsEnabled = false;
                connectRS60Btn.IsEnabled = false;
                connectRS120Btn.IsEnabled = false;
                connectRS180Btn.IsEnabled = false;
                connectRS360Btn.IsEnabled = false;
                connectRSFRBtn.IsEnabled = false;
            }
            saveServerStats(servers);
        }

        Task ExceptionCallback(JKClientException exception)
        {
            Helpers.logToFile(new string[] { exception.ToString() });
            Debug.WriteLine(exception);
            return null;
        }

        private void connectFromButton(ServerInfo serverInfo, string userinfoName, string pw, bool autoRecord, bool silent, bool pretendRealClient = false, int? disconnectAfter=null)
        {
            lock (connectedServerWindows)
            {
                ConnectedServerWindow.ConnectionOptions connOpts = null;
                if (serverInfo.Protocol >= ProtocolVersion.Protocol6 && serverInfo.Protocol <= ProtocolVersion.Protocol8 || serverInfo.Protocol == ProtocolVersion.Protocol17) // TODO Support 15,16?
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.LoadMOHDefaults(); // MOH needs different defaults.
                }
                if (silent)
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.silentMode = true;
                    connOpts.demoTimeColorNames = false;
                    connOpts.attachClientNumToName = false;
                }
                if (userinfoName != null)
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.userInfoName = userinfoName;
                }
                if (pretendRealClient)
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.pretendToBeRealClient = true;
                }
                if (disconnectAfter.HasValue)
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.disconnectTriggers = $"kicked,connectedtime_over:{disconnectAfter.Value}";
                }
                SocksProxy? proxy = this.socksSettingsGlobal.GetProxyForServer(serverInfo);
                if (proxy != null)
                {
                    if (connOpts == null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    }
                    connOpts.proxy = proxy;
                }
                //ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName, pw, connOpts);
                connectedServerWindows.Add(newWindow);
                newWindow.Loaded += NewWindow_Loaded;
                newWindow.Closed += NewWindow_Closed;
                newWindow.ShowActivated = false;
                newWindow.Show();
                if (autoRecord)
                {
                    newWindow.recordAll();
                }
            }
        }

        private void connectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            //string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo,userinfoName,pw,false,false);
            }
        }
        private void connectRBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
           // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true,false);
            }
        }

        private void connectSBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, false, true);
            }
        }

        private void connectRSBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true);
            }
        }

        private void connectRS60Btn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true,false,60);
            }
        }

        private void connectRS120Btn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true, false, 120);
            }
        }

        private void connectRS180Btn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true, false, 180);
            }
        }

        private void connectRS360Btn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true, false, 360);
            }
        }

        private void connectRSFRBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            // string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                connectFromButton(serverInfo, userinfoName, pw, true, true,true);
            }
        }

        private void refreshBtn_Click(object sender, RoutedEventArgs e)
        {
            connectBtn.IsEnabled = false;
            connectRBtn.IsEnabled = false;
            connectSBtn.IsEnabled = false;
            connectRSBtn.IsEnabled = false;
            connectRS60Btn.IsEnabled = false;
            connectRS120Btn.IsEnabled = false;
            connectRS180Btn.IsEnabled = false;
            connectRS360Btn.IsEnabled = false;
            connectRSFRBtn.IsEnabled = false;
            getServers();
        }

        private void serverListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(serverListDataGrid.Items.Count > 0 && serverListDataGrid.SelectedItems.Count > 0)
            {
                connectBtn.IsEnabled = true;
                connectRBtn.IsEnabled = true;
                connectSBtn.IsEnabled = true;
                connectRSBtn.IsEnabled = true;
                connectRS60Btn.IsEnabled = true;
                connectRS120Btn.IsEnabled = true;
                connectRS180Btn.IsEnabled = true;
                connectRS360Btn.IsEnabled = true;
                connectRSFRBtn.IsEnabled = true;
            } else
            {
                connectBtn.IsEnabled = false;
                connectRBtn.IsEnabled = false;
                connectSBtn.IsEnabled = false;
                connectRSBtn.IsEnabled = false;
                connectRS60Btn.IsEnabled = false;
                connectRS120Btn.IsEnabled = false;
                connectRS180Btn.IsEnabled = false;
                connectRS360Btn.IsEnabled = false;
                connectRSFRBtn.IsEnabled = false;
            }
        }

        private void connectIPBtn_Click(object sender, RoutedEventArgs e)
        {
            string ip = ipTxt.Text;
            string pw = pwTxt.Text.Length > 0 ? pwTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            ProtocolVersion? protocol = protocols.SelectedItem != null ? (ProtocolVersion)protocols.SelectedItem : null;
            if(ip.Length > 0 && protocol != null)
            {
                lock (connectedServerWindows)
                {
                    ConnectedServerWindow.ConnectionOptions connOpts = null;
                    if (userinfoName != null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                        if (protocol.Value >= ProtocolVersion.Protocol6 && protocol.Value <= ProtocolVersion.Protocol8 || protocol.Value == ProtocolVersion.Protocol17) // TODO Support 15,16?
                        {
                            connOpts.LoadMOHDefaults(); // MOH needs different defaults.
                        }
                        connOpts.userInfoName = userinfoName;
                    }
                    // cant rly determine if nwh here..
                    //SocksProxy? proxy = this.socksSettingsGlobal.GetProxyForServer(serverInfo);
                    //if (proxy != null)
                    //{
                    //    if (connOpts == null)
                    //    {
                    //        connOpts = new ConnectedServerWindow.ConnectionOptions();
                    //    }
                    //    connOpts.proxy = proxy;
                    //}
                    //ConnectedServerWindow newWindow = new ConnectedServerWindow(ip, protocol.Value);
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(NetAddress.FromString(ip.Trim()), protocol.Value, null, pw, connOpts);
                    connectedServerWindows.Add(newWindow);
                    newWindow.Loaded += NewWindow_Loaded;
                    newWindow.Closed += NewWindow_Closed;
                    newWindow.ShowActivated = false;
                    newWindow.Show();
                }
            }
            /*ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                connectedServerWindows.Add(newWindow);
                newWindow.Closed += (a, b) => { connectedServerWindows.Remove(newWindow); };
                                                newWindow.ShowActivated = false;
                newWindow.Show();
            }*/
        }

        private void colorDecoderBtn_Click(object sender, RoutedEventArgs e)
        {
            var colorDecoderWindow = new ColorTimeDecoder();
            colorDecoderWindow.Show();
        }


        class ServerToConnect
        {
            private static Regex specialCharacterReplacer = new Regex(@"(?<!\\)\\x(?<number>[0-9a-f]{1,2})", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Compiled);

            public bool generic { get; init; } = false;
            public string sectionName { get; init; } = null;
            public NetAddress ip { get; init; } = null;
            public string hostName { get; init; } = null;
            public bool active { get; set; } = true;
            public string playerName { get; init; } = null;
            public string password = null;
            public bool autoRecord { get; init; } = false;
            public bool delayed { get; init; } = false;
            public int delayPerWatcher { get; init; } = 0;
            public int retries { get; init; } = 5;
            public int chance { get; init; } = 100;
            public int dailyChance { get; init; } = 100;
            public int minRealPlayers { get; init; } = 0;
            public int timeFromDisconnect { get; init; } = 0;
            public int timeFromDisconnectUpperRange { get; init; } = 0;
            public int? botSnaps { get; init; } = 5;
            public int? pingAdjust { get; init; } = null;
            public bool forceSnaps { get; init; } = false;
            public int? snaps { get; init; } = null;
            public string[] watchers { get; init; } = null;
            public string[] mapNames { get; init; } = null;
            public string watchersDisp
            {
                get
                {
                    try
                    {
                        return watchers == null ? null : string.Join(',', watchers);
                    }
                    catch (System.ArgumentNullException e) // Just in case of some weird multithreading race stuff blahblah
                    {
                        return null;
                    }
                }
            }
            public string mapNamesDisp
            {
                get
                {
                    try
                    {
                        return mapNames == null ? null : string.Join(',', mapNames);
                    }
                    catch (System.ArgumentNullException e) // Just in case of some weird multithreading race stuff blahblah
                    {
                        return null;
                    }
                }
            }
            public string mapChangeCommands { get; init; } = null;
            public string quickCommands { get; init; } = null;
            public string conditionalCommands { get; init; } = null;
            public string disconnectTriggers { get; init; } = null;
            public string demoMeta { get; init; } = null;
            public int gameTypes { get; init; } = 0;
            public bool attachClientNumToName { get; init; } = true;
            public bool demoTimeColorNames { get; init; } = true;
            public bool silentMode { get; init; } = false;
            public bool autoUpgradeToCTF { get; init; } = false;
            public int? pollingInterval { get; init; } = null;
            public bool pollingBroken { get; set; } = false;
            public bool mohProtocol { get; init; } = false;
            public int? maxTimeSinceMapChange { get; init; } = null;

            public ServerInfo lastFittingServerInfo = null;

            public bool dailyChanceTrueToday { get; private set; } = true;
            public int lastTodaySeed { get; private set; } = 0;
            public string sunsConnectSubscribe { get; init; } = null;

            public NetAddress sunsNotificationServer = null;
            public string sunsNotificationKey = null;

            private void UpdateDailyChance()
            {
                if(dailyChance == 100)
                {
                    dailyChanceTrueToday = true;
                    return;
                }
                DateTime now = DateTime.Now + new TimeSpan(12, 0, 0); // Wanna reset mid-day, not mid-night. Gamers are night creatures, it makes more sense to end gaming days at mid-day.
                int todaySeed = now.Year * 372 + now.Month * 31 + now.Day;
                //todaySeed ^=  sectionName.GetHashCode(); // Can't use this, it changes on each program start ffs.
                using (SHA256 hasher = SHA256.Create())
                {
                    todaySeed ^= BitConverter.ToInt32(hasher.ComputeHash(Encoding.UTF8.GetBytes(sectionName)));
                }
                if (lastTodaySeed != todaySeed)
                {
                    Random rnd = new Random(todaySeed);
                    dailyChanceTrueToday = rnd.Next(1, 101) <= dailyChance; 
                    lastTodaySeed = todaySeed;
                }
            }

            public ServerToConnect(ConfigSection config)
            {
                sectionName = config.SectionName;

                generic = (config["generic"]?.Trim().Atoi()).GetValueOrDefault(0) > 0;
                try
                {
                    string ipString = config["ip"]?.Trim();
                    if(ipString != null && ipString.Length != 0)
                    {
                        ip = NetAddress.FromString(ipString);
                    }
                } catch(Exception e)
                {
                    throw new Exception("ServerConnectConfig: error parsing IP {}");
                }
                hostName = config["hostName"]?.Trim();
                if (hostName != null && hostName.Length == 0) hostName = null;
                if (hostName == null && ip==null && !generic) throw new Exception("ServerConnectConfig: hostName or ip must be provided or 'generic' must be set.");
                playerName = config["playerName"]?.Trim();
                password = config["password"]?.Trim();
                mapChangeCommands = config["mapChangeCommands"]?.Trim();
                quickCommands = config["quickCommands"]?.Trim();
                conditionalCommands = config["conditionalCommands"]?.Trim();
                demoMeta = config["demoMeta"]?.Trim();
                disconnectTriggers = config["disconnectTriggers"]?.Trim();
                autoRecord = config["autoRecord"]?.Trim().Atoi()>0;
                mohProtocol = config["mohProtocol"]?.Trim().Atoi()>0;
                active = !(config["inactive"]?.Trim().Atoi()>0);
                retries = (config["retries"]?.Trim().Atoi()).GetValueOrDefault(5);
                delayPerWatcher = (config["delayPerWatcher"]?.Trim().Atoi()).GetValueOrDefault(0);
                dailyChance = (config["dailyChance"]?.Trim().Atoi()).GetValueOrDefault(100);
                chance = (config["chance"]?.Trim().Atoi()).GetValueOrDefault(100);
                botSnaps = config["botSnaps"]?.Trim().Atoi();
                pingAdjust = config["pingAdjust"]?.Trim().Atoi();
                snaps = config["snaps"]?.Trim().Atoi();
                forceSnaps = config["forceSnaps"]?.Trim().Atoi() > 0;
                pollingInterval = config["pollingInterval"]?.Trim().Atoi();
                maxTimeSinceMapChange = config["maxTimeSinceMapChange"]?.Trim().Atoi();
                watchers = config["watchers"]?.Trim().Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                mapNames = config["maps"]?.Trim().Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                minRealPlayers = Math.Max(0,(config["minPlayers"]?.Trim().Atoi()).GetValueOrDefault(0));
                sunsConnectSubscribe = config["sunsConnectSubscribe"]?.Trim();

                if (!string.IsNullOrWhiteSpace(sunsConnectSubscribe))
                {
                    string[] parts = sunsConnectSubscribe.Split(';');
                    if(parts.Length == 2)
                    {
                        sunsNotificationServer = NetAddress.FromString(parts[0]);
                        sunsNotificationKey = specialCharacterReplacer.Replace(parts[1], (Match a) => {
                            if(a.Groups.ContainsKey("number") && a.Groups["number"].Success)
                            {
                                string retVal = ((char)Convert.ToByte(a.Groups["number"].Value, 16)).ToString();
                                return retVal;
                            }
                            return a.Value;
                        });
                    } else
                    {
                        throw new Exception($"sunsConnectSubscribe is in wrong format. What was given is {sunsConnectSubscribe}, but required format is: ip:port;key");
                    }
                }

                string timeFromDisconnectString = config["timeFromDisconnect"];
                if (timeFromDisconnectString != null)
                {
                    string[] rangeParts = timeFromDisconnectString.Split('-',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                    if(rangeParts.Length > 0)
                    {
                        timeFromDisconnect = Math.Max(0, rangeParts[0].Atoi());
                    }
                    if(rangeParts.Length > 1)
                    {
                        timeFromDisconnectUpperRange = Math.Max(0, rangeParts[1].Atoi());
                    }
                }
                delayed = config["delayed"]?.Trim().Atoi() > 0;

                attachClientNumToName = (config["attachClientNumToName"]?.Trim().Atoi()).GetValueOrDefault(1) > 0;
                demoTimeColorNames = (config["demoTimeColorNames"]?.Trim().Atoi()).GetValueOrDefault(1) > 0;
                silentMode = (config["silentMode"]?.Trim().Atoi()).GetValueOrDefault(0) > 0;
                autoUpgradeToCTF = (config["autoUpgradeToCTF"]?.Trim().Atoi()).GetValueOrDefault(0) > 0;

                gameTypes |= Connection.GameTypeStringToBitMask(config["gameTypes"]);

                /*string[] gameTypesStrings = config["gameTypes"]?.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (gameTypesStrings != null)
                {
                    foreach (string gameTypeString in gameTypesStrings)
                    {
                        switch (gameTypeString) {
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
                }*/

                if(pollingInterval.HasValue && ip == null)
                {
                    throw new Exception("ServerConnectConfig: pollingInterval for server requires IP to be set");
                }

                if (minRealPlayers>0 && !delayed)
                {
                    throw new Exception("ServerConnectConfig: minPlayers value > 0 requires delayed=1");
                }

                if (pollingInterval.HasValue && !delayed)
                {
                    throw new Exception("ServerConnectConfig: pollingInterval requires delayed=1");
                }

                UpdateDailyChance();
            }

            //private DateTime? lastMapChange = null;
            //private string lastMapName = null;

            private DateTime? lastSunsSubscriptionSent = null;

            private Dictionary<NetAddress, string> lastMapNames = new Dictionary<NetAddress, string>(NetAddressComparer.Default); // These are now dictionaries because sometimes you get multiple servers that fit an entry (if only because someone is trolling and making servers with an identical name)
            private Dictionary<NetAddress, DateTime> lastMapChanges = new Dictionary<NetAddress, DateTime>(NetAddressComparer.Default);

            public bool FitsRequirements(ServerInfo serverInfo, ref bool matchesButMightNotMeetRequirements)
            {
                if(sunsNotificationServer != null && sunsNotificationKey != null && (!lastSunsSubscriptionSent.HasValue || (DateTime.Now- lastSunsSubscriptionSent.Value).TotalMilliseconds > 5000))
                {
                    SunsNotificationClient.Subscribe(sunsNotificationServer, sunsNotificationKey);
                    lastSunsSubscriptionSent = DateTime.Now;
                }
                UpdateDailyChance();
                matchesButMightNotMeetRequirements = false;
                if (serverInfo.HostName == null) return false;
                bool matched = serverInfo.Address == ip || hostName != null && (serverInfo.HostName.Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(Q3ColorFormatter.cleanupString(hostName)));
                if (generic || matched) // Improve this to also find non-colorcoded terms etc
                {
                    if (!lastMapNames.ContainsKey(serverInfo.Address) || lastMapNames[serverInfo.Address] != serverInfo.MapName)
                    {
                        if (lastMapNames.ContainsKey(serverInfo.Address) && lastMapNames[serverInfo.Address] != null)
                        {
                            lastMapChanges[serverInfo.Address] = DateTime.Now;
                        }
                        lastMapNames[serverInfo.Address] = serverInfo.MapName;
                    }
                    if (lastFittingServerInfo == null || !(lastFittingServerInfo.Address == ip))
                    {
                        lastFittingServerInfo = serverInfo;
                    }
                    matchesButMightNotMeetRequirements = matched;
                    if (!this.active) return false;
                    if (!dailyChanceTrueToday) return false;
                    if (chance < 100 && Connection.getNiceRandom(1,101) > chance) return false;
                    //if (maxTimeSinceMapChange.HasValue && lastMapChange.HasValue && (DateTime.Now - lastMapChange.Value).TotalMilliseconds > maxTimeSinceMapChange.Value) return false;
                    if (maxTimeSinceMapChange.HasValue && lastMapChanges.ContainsKey(serverInfo.Address) && (DateTime.Now - lastMapChanges[serverInfo.Address]).TotalMilliseconds > maxTimeSinceMapChange.Value) return false;
                    if (timeFromDisconnect > 0 || timeFromDisconnectUpperRange > 0)
                    {
                        // Whenever we disconnect, we save the time we disconnected along with a randomly generated 0.0-1.0 double.
                        // This double modifies the time delay until we can connect again if it was specified as a range, for example 5-10
                        // Then the random double will define the relative position between 5 and 10. 
                        // We could directly generate the random here but then it would be a new roll of the dice every time we check if the server fits requirements
                        // which will sooner or later just end up giving something close to the lower end of the range due to probability,
                        // thus destroying the potential for true randomness. Hence the random value is generated the moment we disconnect
                        (DateTime? lastDisconnect,double? lastDisconnectTimeRangeModifier) = MainWindow.getServerLastDisconnected(serverInfo.Address);
                        
                        if (lastDisconnect.HasValue)
                        {
                            double actualMinTimeDelay = timeFromDisconnect;
                            if (timeFromDisconnectUpperRange > 0 && lastDisconnectTimeRangeModifier.HasValue) // No reason why lastDisconnectTimeRangeModifier shouldn't have a value if lastDisconnect does but let's be safe?
                            {
                                actualMinTimeDelay = (double)timeFromDisconnect + ((double)timeFromDisconnectUpperRange - (double)timeFromDisconnect) * lastDisconnectTimeRangeModifier.Value;
                            }
                            if ((DateTime.Now - lastDisconnect.Value).TotalMinutes < actualMinTimeDelay)
                            {
                                return false;
                            }
                        }
                        
                    }
                    if(mapNames != null)
                    {
                        bool mapMatches = false;
                        foreach(string mapName in mapNames)
                        {
                            if (mapName.Equals(serverInfo.MapName,StringComparison.OrdinalIgnoreCase))
                            {
                                mapMatches = true;
                            }
                        }
                        if (!mapMatches) return false;
                    }

                    bool isMOH = Common.ProtocolIsMOH(serverInfo.Protocol);
                    int? clientCountToCompare = isMOH ? (serverInfo.StatusResponseReceived ? serverInfo.RealClients : serverInfo.Clients) : serverInfo.RealClients; // MOH servers are more unreliable, sometimes just don't send status packet etc.
                    return (gameTypes == 0 || serverInfo.InfoPacketReceived && 0 < (gameTypes & (1 << (int)serverInfo.GameType)) ) 
                        && (clientCountToCompare >= minRealPlayers || minRealPlayers == 0) 
                        && (!serverInfo.NeedPassword || serverInfo.NeedPassword 
                        && password != null 
                        && password.Length > 0);
                }
                else
                {
                    return false;
                }
            }
        }


        void ConnectFromConfig(ServerInfo serverInfo, ServerToConnect serverToConnect)
        {
            Debug.WriteLine(serverInfo.Address.ToString());
            Dispatcher.Invoke(() => {

                lock (connectedServerWindows)
                {
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,serverToConnect.password, new ConnectedServerWindow.ConnectionOptions() { userInfoName= serverToConnect.playerName, mapChangeCommands=serverToConnect.mapChangeCommands, quickCommands=serverToConnect.quickCommands,conditionalCommands=serverToConnect.conditionalCommands,disconnectTriggers=serverToConnect.disconnectTriggers,attachClientNumToName=serverToConnect.attachClientNumToName,demoTimeColorNames=serverToConnect.demoTimeColorNames,silentMode=serverToConnect.silentMode, extraDemoMeta=serverToConnect.demoMeta, autoUpgradeToCTF= serverToConnect.autoUpgradeToCTF, autoUpgradeToCTFWithStrobe= serverToConnect.autoUpgradeToCTF,proxy = this.socksSettingsGlobal.GetProxyForServer(serverInfo) });
                    connectedServerWindows.Add(newWindow);
                    newWindow.Loaded += NewWindow_Loaded;
                    newWindow.Closed += NewWindow_Closed;
                    newWindow.ShowActivated = false;
                    newWindow.Show();
                    /*if(serverToConnect.playerName != null)
                    {
                        newWindow.setUserInfoName(serverToConnect.playerName);
                    }*/
                    if (serverToConnect.botSnaps != null)
                    {
                        newWindow.snapsSettings.botOnlySnaps = serverToConnect.botSnaps.Value;
                    }
                    if (serverToConnect.pingAdjust != null)
                    {
                        newWindow.snapsSettings.pingAdjust = serverToConnect.pingAdjust.Value;
                        if(newWindow.snapsSettings.pingAdjust != 0)
                        {
                            newWindow.snapsSettings.pingAdjustActive = true;
                        }
                    }
                    if (serverToConnect.snaps != null)
                    {
                        newWindow.snapsSettings.baseSnaps = serverToConnect.snaps.Value;
                        if (newWindow.snapsSettings.baseSnaps != 0)
                        {
                            newWindow.snapsSettings.setBaseSnaps = true;
                            if (serverToConnect.forceSnaps)
                            {
                                newWindow.snapsSettings.forceBaseSnaps = true;
                            }
                        }
                    }
                    if (serverToConnect.watchers != null)
                    {
                        int index = 0;
                        foreach (string camera in serverToConnect.watchers)
                        {
                            string cameraLocal = camera;
                            string[] cameraDataParts = camera.Split('{',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                            Dictionary<string,string> optionsToSetForCamera = new Dictionary<string, string>();
                            if(cameraDataParts.Length > 1)
                            {
                                cameraLocal = cameraDataParts[0];
                                string[] optionsStrings = cameraDataParts[1].TrimEnd('}').Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                foreach(string optionsString in optionsStrings)
                                {
                                    string[] keyValueParts = optionsString.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                    if(keyValueParts.Length != 2)
                                    {
                                        Helpers.logToFile($"Camera Operator option {optionsString} has the wrong format. Full camera operator string: {camera}");
                                    } else
                                    {
                                        optionsToSetForCamera[keyValueParts[0]] = keyValueParts[1];
                                    }
                                }
                            }
                            Action watcherCreate = new Action(()=> {
                                CameraOperator cameraOperator = null;
                                switch (cameraLocal)
                                {
                                    case "defrag":
                                    case "ocd":
                                        cameraOperator = newWindow.createOCDefragOperator();
                                        break;
                                    case "ctf":
                                        cameraOperator = newWindow.createCTFOperator();
                                        break;
                                    case "strobe":
                                        cameraOperator = newWindow.createStrobeOperator();
                                        break;
                                    case "ffa":
                                        cameraOperator = newWindow.createFFAOperator();
                                        break;
                                    default:
                                        break;
                                }
                                if (cameraOperator != null) { 
                                    foreach (var optionToSet in optionsToSetForCamera)
                                    {
                                        cameraOperator.SetOption(optionToSet.Key, optionToSet.Value);
                                    }
                                }
                            });

                            index++;
                            int delayHere = index * serverToConnect.delayPerWatcher;

                            if(delayHere > 0)
                            {
                                bool localAutoRecord = serverToConnect.autoRecord;
                                TaskManager.TaskRun(()=> { 
                                    System.Threading.Thread.Sleep(delayHere);
                                    Dispatcher.Invoke(()=> {
                                        watcherCreate();
                                        if (localAutoRecord)
                                        {
                                            newWindow.recordAll();
                                        }
                                    });
                                },$"Delayed Connecter Delayed Watcher Spawner ({camera},{serverInfo.HostName},{serverToConnect.sectionName},{serverInfo.Address})");
                            }
                            else
                            {
                                watcherCreate();
                            }


                        }
                    }
                    if (serverToConnect.autoRecord)
                    {
                        newWindow.recordAll();
                    }
                }
            });
        }

        private List<CancellationTokenSource> markovTrainCancellationTokens = new List<CancellationTokenSource>();
        private void executeConfig(ConfigParser cp)
        {
            if (cp.GetValue("__general__","ctfAutoConnect",0) == 1)
            {
                ctfAutoJoin.IsChecked = true;
            }
            if (cp.GetValue("__general__", "ctfAutoConnectWithStrobe", 0) == 1)
            {
                ctfAutoJoinWithStrobe.IsChecked = true;
            }
            if (cp.GetValue("__general__", "ffaAutoConnect", 0) == 1)
            {
                ffaAutoJoin.IsChecked = true;
            }
            if (cp.GetValue("__general__", "ffaAutoConnectSilent", 0) == 1)
            {
                ffaAutoJoinSilent.IsChecked = true;
            }
            if (cp.GetValue("__general__", "ffaAutoConnectKickable", 0) == 1)
            {
                ffaAutoJoinKickableCheck.IsChecked = true;
            }

            string socksConditions = cp.GetValue("__general__", "socksConditions", "");
            if (!string.IsNullOrWhiteSpace(socksConditions))
            {
                SocksSettingsGlobal.SocksCondition newConditions = 0;
                socksConditions = socksConditions.Trim().ToLowerInvariant();
                string[] splitConds = socksConditions.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                foreach(string splitCond in splitConds)
                {
                    switch (splitCond)
                    {
                        case "nwh":
                            newConditions |= SocksSettingsGlobal.SocksCondition.NWH;
                            break;
                    }
                }
                this.socksSettingsGlobal.conditions = newConditions;
            }
            string socksServer = cp.GetValue("__general__", "socksServer", "");
            if (!string.IsNullOrWhiteSpace(socksServer))
            {
                this.socksSettingsGlobal.server = socksServer;
            }
            string socksPort = cp.GetValue("__general__", "socksPort", "");
            if (!string.IsNullOrWhiteSpace(socksPort))
            {
                if(int.TryParse(socksPort,out int port))
                {
                    this.socksSettingsGlobal.port = port;
                }
            }
            string socksUser = cp.GetValue("__general__", "socksUser", "");
            if (!string.IsNullOrWhiteSpace(socksUser))
            {
                this.socksSettingsGlobal.user = socksUser;
            }
            string socksPW = cp.GetValue("__general__", "socksPW", "");
            if (!string.IsNullOrWhiteSpace(socksPW))
            {
                this.socksSettingsGlobal.pw = socksPW;
            }

            string markovTrain = cp.GetValue("__general__", "markovTrain", "");
            if (!string.IsNullOrWhiteSpace(markovTrain))
            {
                bool markovCleared = false;
                string[] markovFiles = markovTrain.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                foreach(string markovFile in markovFiles)
                {
                    string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher");
                    Directory.CreateDirectory(baseFolder);
                    string file = Path.IsPathRooted(markovFile) ? markovFile : Path.Combine(baseFolder,markovFile);
                    if (!File.Exists(file))
                    {
                        MessageBox.Show($"{file} does not exist. Cannot load markov chatbot from it.");
                        continue;
                    }
                    if (!markovCleared)
                    {
                        lock (markovTrainCancellationTokens)
                        {
                            foreach(var token in markovTrainCancellationTokens)
                            {
                                token.Cancel(); // TODO there might still be some race conditions here in some situations. eh. especially when combining the automatic loading with the manual one
                            }
                            markovTrainCancellationTokens.Clear();
                        }
                        Markov.Clear();
                        markovCleared = true;
                    }
                    CancellationTokenSource cts = new CancellationTokenSource();
                    CancellationToken ct = cts.Token;
                    Task markovTask = TaskManager.TaskRun(() => {
                        Markov.RegisterMarkovChain(file, (a, b) => {
                            //UpdateTrainingStatus(a, b);
                        }, ct);
                    }, $"Training Markov Chain on {file}");
                    markovTrainCancellationTokens.Add(cts);
                }
            }

            string ffaAutoConnectExclude = cp.GetValue("__general__", "ffaAutoConnectExclude", "");
            if (!string.IsNullOrWhiteSpace(ffaAutoConnectExclude))
            {
                ffaAutoConnectExclude = ffaAutoConnectExclude.Trim();
                ffaAutoJoinExcludeTxt.Text = ffaAutoConnectExclude;
            }
            string ffaAutoConnectKickReconnectDelay = cp.GetValue("__general__", "ffaAutoConnectKickReconnectDelay", "");
            if (!string.IsNullOrWhiteSpace(ffaAutoConnectKickReconnectDelay))
            {
                ffaAutoConnectKickReconnectDelay = ffaAutoConnectKickReconnectDelay.Trim();
                ffaAutoJoinKickReconnectDelayTxt.Text = ffaAutoConnectKickReconnectDelay;
            }
            string ffaAutoConnectConditionalCmds = cp.GetValue("__general__", "ffaAutoConnectConditionalCommands", "");
            if (!string.IsNullOrWhiteSpace(ffaAutoConnectConditionalCmds))
            {
                ffaAutoConnectConditionalCmds = ffaAutoConnectConditionalCmds.Trim();
                ffaAutojoinConditionalCmdsTxt.Text = ffaAutoConnectConditionalCmds;
            }
            string ctfAutoConnectConditionalCmds = cp.GetValue("__general__", "ctfAutoConnectConditionalCommands", "");
            if (!string.IsNullOrWhiteSpace(ctfAutoConnectConditionalCmds))
            {
                ctfAutoConnectConditionalCmds = ctfAutoConnectConditionalCmds.Trim();
                ctfAutojoinConditionalCmdsTxt.Text = ctfAutoConnectConditionalCmds;
            }
            string taskbarPlayerCountStatusGametypes = cp.GetValue("__general__", "taskbarPlayerCountStatusGametypes", "");
            if (!string.IsNullOrWhiteSpace(taskbarPlayerCountStatusGametypes))
            {
                taskbarPlayerCountStatusGametypes = taskbarPlayerCountStatusGametypes.Trim();
                taskbarPlayerCountStatusGametypesTxt.Text = taskbarPlayerCountStatusGametypes;
            }
            string autoJoinCheckInterval = cp.GetValue("__general__", "autoJoinCheckInterval", "");
            if (!string.IsNullOrWhiteSpace(autoJoinCheckInterval))
            {
                autoJoinCheckInterval = autoJoinCheckInterval.Trim();
                autoJoinCheckIntervalTxt.Text = autoJoinCheckInterval;
            }
            if (cp.GetValue("__general__", "jkaMode", 0) == 1)
            {
                jkaModeCheck.IsChecked = true;
            }
            if (cp.GetValue("__general__", "mohMode", 0) == 1)
            {
                mohModeCheck.IsChecked = true;
            }
            if (cp.GetValue("__general__", "allJK2Versions", 0) == 1)
            {
                allJK2VersionsCheck.IsChecked = true;
            }
            bool jkaMode = jkaModeCheck.IsChecked == true;
            bool mohMode = mohModeCheck.IsChecked == true;
            List<ServerToConnect> serversToConnect = new List<ServerToConnect>();
            lock (serversToConnectDelayed)
            {
                serversToConnectDelayed.Clear();
                foreach (ConfigSection section in cp.Sections)
                {
                    if (section.SectionName == "__general__") continue;

                    if (section["hostName"] != null || section["ip"] != null || !string.IsNullOrWhiteSpace(section["generic"]))
                    {
                        var newServer = new ServerToConnect(section);
                        if (newServer.delayed)
                        {
                            serversToConnectDelayed.Add(newServer);
                        }
                        serversToConnect.Add(newServer);
                    }
                }
            }

            NetAddress[] manualServers = getManualServers();
            List<NetAddress> hiddenServersAll = new List<NetAddress>();
            hiddenServersAll.AddRange(manualServers);
            lock (serversToConnectDelayed)
            {
                foreach (ServerToConnect srvTC in serversToConnect)
                {
                    if (srvTC.ip != null)
                    {
                        hiddenServersAll.Add(srvTC.ip);
                    }
                }
            }
            ServerBrowser.SetHiddenServers(hiddenServersAll.ToArray());

            bool delayedConnecterActive = true;
            Dispatcher.Invoke(() => {
                delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true; // Just double check to be safe.
            });

            if (!delayedConnecterActive)
            {
                lock (serversToConnectDelayed)
                {
                    serversToConnectDelayed.Clear();
                    serversToConnectDelayed.AddRange(serversToConnect);
                    executionInProgress = false;
                    return;
                }
            }

            if (serversToConnect.Count > 0)
            {
                int tries = 0;

                TaskManager.TaskRun(async () => {

                    while (serversToConnect.Count > 0)
                    {


                        List<ServerToConnect> elementsToRemove = new List<ServerToConnect>();

                        foreach (ServerToConnect serverToConnect in serversToConnect)
                        {
                            if (serverToConnect.retries < tries)
                            {
                                elementsToRemove.Add(serverToConnect);
                            }
                        }
                        foreach (ServerToConnect elementToRemove in elementsToRemove)
                        {
                            lock (serversToConnectDelayed)
                            {
                                if (!serversToConnectDelayed.Contains(elementToRemove)) // Try to connect to it over time?
                                {
                                    serversToConnectDelayed.Add(elementToRemove);
                                }
                            }
                            serversToConnect.Remove(elementToRemove);
                        }
                        elementsToRemove.Clear();


                        IEnumerable<ServerInfo> servers = null;

                        ServerBrowser serverBrowser = null;
                        if (jkaMode)
                        {
                            serverBrowser = new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { RefreshTimeout = 10000L, ForceStatus = true };
                        }
                        else if (mohMode)
                        {
                            serverBrowser = new ServerBrowser(new MOHBrowserHandler(ProtocolVersion.Protocol8, true)) { RefreshTimeout = 10000L, ForceStatus = true };
                        } else
                        {
                            serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, true)) { RefreshTimeout = 10000L, ForceStatus = true }; 
                        }

                        serverBrowser.InternalTaskStarted += ServerBrowser_InternalTaskStarted;

                        try
                        {

                            serverBrowser.Start(ExceptionCallback);
                            servers = await serverBrowser.GetNewList();
                            //servers = await serverBrowser.RefreshList();
                        }
                        catch (Exception e)
                        {
                            // Just in case getting servers crashes or sth.
                            serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                            continue;
                        }

                        if (servers == null) continue;


                        foreach (ServerInfo serverInfo in servers)
                        {
                            if (/*serverInfo.Version == ClientVersion.JO_v1_02*/true) // Don't care for the config execution.
                            {
                                bool wantToConnect = false;
                                ServerToConnect wishServer = null;
                                foreach (ServerToConnect serverToConnect in serversToConnect)
                                {
                                    bool matchesButMayNotFitRequirements = false;
                                    if (serverToConnect.FitsRequirements(serverInfo, ref matchesButMayNotFitRequirements))
                                    {
                                        wantToConnect = true;
                                        wishServer = serverToConnect;
                                        elementsToRemove.Add(serverToConnect);
                                    }
                                }
                                if (!wantToConnect) continue;

                                bool alreadyConnected = false;
                                lock (connectedServerWindows)
                                {
                                    foreach (ConnectedServerWindow window in connectedServerWindows)
                                    {
                                        if (window.netAddress == serverInfo.Address && window.protocol == serverInfo.Protocol)
                                        {
                                            alreadyConnected = true;
                                        }
                                    }
                                }
                                // We want to be speccing/recording this.
                                // Check if we are already connected. If so, do nothing.
                                if (!alreadyConnected && !serverInfo.NeedPassword)
                                {

                                    ConnectFromConfig(serverInfo,wishServer);
                                    
                                }

                            }


                        }

                        foreach (ServerToConnect elementToRemove in elementsToRemove)
                        {
                            serversToConnect.Remove(elementToRemove);
                        }
                        elementsToRemove.Clear();

                        saveServerStats(servers);
                        serverBrowser.Stop();
                        serverBrowser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                        serverBrowser.Dispose();

                        tries++;
                    }
                    executionInProgress = false;


                },$"Config Executer"); // TODO Config name in task name
            }
        }

        private Mutex executionInProgressMutex = new Mutex();

        public event PropertyChangedEventHandler PropertyChanged;

        private void executeConfig_Click(object sender, RoutedEventArgs e)
        {
            lock (executionInProgressMutex) { // Wait, does this make sense?
                if(executionInProgress)
                {
                    MessageBox.Show("Execution already in progress, please wait.");
                    return;
                }
                string config = configsComboBox.SelectedItem != null ? (string)configsComboBox.SelectedItem : null;
                if (config != null)
                {
                    try
                    {
                        ConfigParser cp = new ConfigParser($"configs/{config}.ini");
                        executeConfig(cp);
                        executionInProgress = true;
                    } catch(Exception ex)
                    {
                        string errorString = $"Error executing config: {ex.ToString()}";
                        Helpers.logToFile(new string[] { errorString });
                        MessageBox.Show(errorString);
                    }
                }
            }
        }

        private void calendarBtn_Click(object sender, RoutedEventArgs e)
        {
            var calendarWindow = new CalendarWindow();
            calendarWindow.Show();
        }

        private void delayedConnectsRefreshListBtn_Click(object sender, RoutedEventArgs e)
        {
            lock (serversToConnectDelayed)
            {

                delayedConnectsList.ItemsSource = serversToConnectDelayed.ToArray();
            }
        }

        private void delayedConnectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (delayedConnectsList.Items.Count > 0 && delayedConnectsList.SelectedItems.Count > 0)
            {
                delayedForceConnectBtn.IsEnabled = true;
            }
            else
            {
                delayedForceConnectBtn.IsEnabled = false;
            }
        }

        private async Task delayedForceConnect(ServerToConnect serverToConnect, bool checkAlreadyConnected = false)
        {
            string errorString = null;
            if (serverToConnect != null)
            {
                NetAddress ip = serverToConnect.ip;
                if (serverToConnect.lastFittingServerInfo != null)
                {
                    bool alreadyConnected = false;
                    if (checkAlreadyConnected)
                    {
                        lock (connectedServerWindows)
                        {
                            foreach (ConnectedServerWindow window in connectedServerWindows)
                            {
                                if (window.netAddress == serverToConnect.lastFittingServerInfo.Address && window.protocol == serverToConnect.lastFittingServerInfo.Protocol)
                                {
                                    alreadyConnected = true;
                                }
                            }
                        }
                    }
                    if (!alreadyConnected)
                    {
                        ConnectFromConfig(serverToConnect.lastFittingServerInfo, serverToConnect);
                    }
                }
                else if (ip == null)
                {
                    errorString = ("Cannot force connect to a server without IP. Not implemented.");
                }
                else
                {
                    try
                    {
                        bool jkaMode = jkaModeCheck.IsChecked == true;
                        bool mohMode = mohModeCheck.IsChecked == true;

                        IBrowserHandler bHandler = null;
                        if (jkaMode)
                        {
                            bHandler = new JABrowserHandler(ProtocolVersion.Protocol26);
                        }
                        else if (mohMode)
                        {
                            bHandler = new MOHBrowserHandler(ProtocolVersion.Protocol8, true);
                        }
                        else
                        {
                            bHandler = new JOBrowserHandler(ProtocolVersion.Protocol15, true);
                        }

                        using (ServerBrowser browser = new ServerBrowser(bHandler))
                        {
                            browser.InternalTaskStarted += ServerBrowser_InternalTaskStarted;
                            browser.Start(async (JKClientException ex) => {
                                errorString = ("Exception trying to get ServerInfo for forced connect: " + ex.ToString());
                            });

                            ServerInfo serverInfo = null;
                            try
                            {
                                serverInfo = await browser.GetFullServerInfo(ip, false, true);
                            }
                            catch (Exception ex2)
                            {
                                errorString = ("Exception trying to get ServerInfo for forced connect (during await): " + ex2.ToString());
                                //continue;
                            }

                            if (serverInfo != null && (serverInfo.StatusResponseReceived || serverInfo.InfoPacketReceived))
                            {
                                bool alreadyConnected = false;
                                if (checkAlreadyConnected)
                                {
                                    lock (connectedServerWindows)
                                    {
                                        foreach (ConnectedServerWindow window in connectedServerWindows)
                                        {
                                            if (window.netAddress == serverInfo.Address && window.protocol == serverInfo.Protocol)
                                            {
                                                alreadyConnected = true;
                                            }
                                        }
                                    }
                                }
                                if (!alreadyConnected)
                                {
                                    ConnectFromConfig(serverInfo, serverToConnect);
                                }
                            }
                            else if (serverInfo == null)
                            {
                                errorString = ("Unknown error trying to get ServerInfo for forced connect. serverInfo is null");
                            }
                            else
                            {
                                errorString = ($"Unknown error trying to get ServerInfo for forced connect. status received: {serverInfo.StatusResponseReceived}, info received: {serverInfo.InfoPacketReceived} ");
                            }

                            browser.Stop();
                            browser.InternalTaskStarted -= ServerBrowser_InternalTaskStarted;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorString = ("Exception trying to get ServerInfo for forced connect (outer): " + ex.ToString());
                    }
                }
            }

            if (errorString != null)
            {
                MessageBox.Show(errorString);
                Helpers.logToFile(errorString);
            }
        }

        private async void delayedForceConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerToConnect serverToConnect = (ServerToConnect)delayedConnectsList.SelectedItem;
            await delayedForceConnect(serverToConnect);
        }

        private void taskManagerRefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            taskManagerList.ItemsSource = TaskManager.GetRunningTasks();
        }

        private void helpBtn_Click(object sender, RoutedEventArgs e)
        {

            var helpWindow = new Help();
            helpWindow.Show();
        }
        private void markovBtn_Click(object sender, RoutedEventArgs e)
        {

            var markovManager = new MarkovManager();
            markovManager.Show();
        }

        Dictionary<string, IntermissionCamPositionTuple> differentOldNewIntermissionCamPositions = new Dictionary<string, IntermissionCamPositionTuple>(StringComparer.InvariantCultureIgnoreCase);

        private void processConflictingIntermissionCamPositions()
        {
            Dictionary<string, IntermissionCamPositionTuple> conflictingCams = null;
            lock (differentOldNewIntermissionCamPositions)
            {
                conflictingCams = new Dictionary<string, IntermissionCamPositionTuple>(differentOldNewIntermissionCamPositions, StringComparer.InvariantCultureIgnoreCase);
                differentOldNewIntermissionCamPositions.Clear();
            }
            if (conflictingCams.Count == 0) return;
            StringBuilder informText = new StringBuilder();
            if (conflictingCams.Count < 50)
            {

            }
            foreach(var kvp in conflictingCams)
            {
                (float distance1, float distance2) = kvp.Value.newPos.DistanceToOther(kvp.Value.oldPos);
                informText.Append($"{kvp.Key}: {distance1} posdiff {distance2} angdiff");
                if(kvp.Value.dupeCount > 0)
                {
                    informText.Append($" ({kvp.Value.dupeCount} map dupes, might conflict)");
                }
                informText.Append("\n");

            }
            MessageBoxResult result = MessageBoxResult.None;

            Dispatcher.Invoke(()=> {
                GenericDialogBoxes.DetailedDialogBox db = new GenericDialogBoxes.DetailedDialogBox("Click Yes to manually decide for each, No to overwrite all, Cancel to not overwrite any. Set override choice where you want to quickly set now.", conflictingCams.Values.ToArray(), "Intermission cam conflicts found", null);
                db.ShowDialog();
                result = db.result;
            });

            //if (result == MessageBoxResult.Cancel)
            //{
            //    return;
            //}
            bool doubleCheckEach = result == MessageBoxResult.Yes;
            foreach (var kvp in conflictingCams)
            {
                if(kvp.Value.overrideChoice != IntermissionCamPositionTuple.OverrideChoice.None)
                {
                    switch (kvp.Value.overrideChoice)
                    {
                        case IntermissionCamPositionTuple.OverrideChoice.KeepOld:
                            continue;
                        case IntermissionCamPositionTuple.OverrideChoice.Override:
                            AsyncPersistentDataManager<IntermissionCamPosition>.addItem(kvp.Value.newPos, true);
                            continue;
                    }
                }
                if(result == MessageBoxResult.Cancel)
                {
                    continue;
                }
                if (!doubleCheckEach)
                {
                    AsyncPersistentDataManager<IntermissionCamPosition>.addItem(kvp.Value.newPos, true);
                    continue;
                }
                informText.Clear();
                (float distance1, float distance2) = kvp.Value.newPos.DistanceToOther(kvp.Value.oldPos);
                informText.AppendLine($"{kvp.Key}:\nposdiff {distance1} angdiff {distance2}");
                if (kvp.Value.dupeCount > 0)
                {
                    informText.AppendLine($"{kvp.Value.dupeCount} map dupes, might conflict");
                }
                informText.AppendLine($"old pos/ang: {kvp.Value.oldPos.position} {kvp.Value.oldPos.angles}");
                informText.AppendLine($"new pos/ang: {kvp.Value.newPos.position} {kvp.Value.newPos.angles}");
                informText.AppendLine($"old was intermission ent: {kvp.Value.oldPos.trueIntermissionEntity}");
                informText.AppendLine($"new is intermission ent: {kvp.Value.newPos.trueIntermissionEntity}");
                informText.AppendLine();

                bool doIt = false;

                MessageBoxResult itemResult = MessageBoxResult.None;

                Dispatcher.Invoke(() => {
                    GenericDialogBoxes.DetailedDialogBox itemDb = new GenericDialogBoxes.DetailedDialogBox("Press Yes to overwrite, No to not overwrite, Cancel to cancel the rest and Override All to override all the rest without asking", informText.ToString(), $"Override {kvp.Key} intermission cam?", "Override All");
                    itemDb.ShowDialog();
                    itemResult = itemDb.result;
                });
                switch (itemResult)
                {
                    default:
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        AsyncPersistentDataManager<IntermissionCamPosition>.addItem(kvp.Value.newPos, true);
                        break;
                    case MessageBoxResult.No:
                        continue;
                    case MessageBoxResult.OK:
                        AsyncPersistentDataManager<IntermissionCamPosition>.addItem(kvp.Value.newPos, true);
                        doubleCheckEach = false;
                        break;
                }
            }
        }

        Regex bspRegex = new Regex(@"\.bsp$",RegexOptions.Compiled|RegexOptions.IgnoreCase);
        private void btnFindIntermissionInFolderPath_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                string folder = fbd.SelectedPath;
                TaskManager.TaskRun(()=> {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, FindIntermissionInBsp);
                    zipRecursor.HandleFolder(folder);
                    processConflictingIntermissionCamPositions();
                },$"Intermission cam finding in folder path {folder}");
            }
        }
        private void btnFindIntermissionInFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "BSP, ZIP, RAR, PK3 files (.bsp;.zip;.pk3)|*.bsp;*.zip;*.rar;*.pk3";
            if (ofd.ShowDialog() == true)
            {
                string filename = ofd.FileName;

                TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, FindIntermissionInBsp);
                    zipRecursor.HandleFile(filename);
                    processConflictingIntermissionCamPositions();
                }, $"Intermission cam finding in file {filename}");
            }
        }

        private void FindIntermissionInBsp(string filename, byte[] fileData, string path)
        {
            Stack<string> pathStack = new Stack<string>();
            string[] pathPartsArr = path is null ? new string[0] : path.Split(new char[] { '\\','/'});
            bool mapsFolderFound = false;
            for(int i = pathPartsArr.Length - 1; i >= 0; i--)
            {
                string pathPart = pathPartsArr[i];
                if (pathPart.Equals("maps", StringComparison.InvariantCultureIgnoreCase))
                {
                    mapsFolderFound = true;
                    break;
                } else if (pathPart.EndsWith(".pk3",StringComparison.InvariantCultureIgnoreCase) || pathPart.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    // this is weird. must be some weird isolated file
                    pathStack.Clear();
                    break;
                }
                else
                {
                    pathStack.Push(pathPart);
                }
            }
            if (!mapsFolderFound)
            {
                pathStack.Clear();
            }
            // TODO What if there is some maps folder really low in the hierarchy?
            StringBuilder sb = new StringBuilder();
            while (pathStack.Count > 0)
            {
                sb.Append($"{pathStack.Pop().ToLowerInvariant()}/");
            }
            sb.Append(System.IO.Path.GetFileNameWithoutExtension(filename).ToLowerInvariant());
            string mapname = sb.ToString();
            Debug.WriteLine($"Found {filename} ({mapname}) in {path}");
            (Vector3? origin,Vector3? angles, bool intermissionEnt)=BSPHelper.GetIntermissionCamFromBSPData(fileData);

            if(origin.HasValue && angles.HasValue)
            {
                IntermissionCamPosition oldSavedPosition = AsyncPersistentDataManager<IntermissionCamPosition>.getByPrimaryKey(mapname);
                IntermissionCamPosition newPosition = new IntermissionCamPosition()
                {
                    MapName = mapname.ToLowerInvariant(),
                    posX = origin.Value.X,
                    posY = origin.Value.Y,
                    posZ = origin.Value.Z,
                    angX = angles.Value.X,
                    angY = angles.Value.Y,
                    angZ = angles.Value.Z,
                    trueIntermissionCam = true,
                    trueIntermissionEntity = intermissionEnt,
                    nonIntermissionEntityAlgorithmVersion = BSPHelper.nonIntermissionEntityAlgorithmVersion
                };
                if (oldSavedPosition == null || oldSavedPosition.trueIntermissionCam == false || oldSavedPosition.trueIntermissionEntity == false && intermissionEnt || BSPHelper.nonIntermissionEntityAlgorithmVersion > oldSavedPosition.nonIntermissionEntityAlgorithmVersion && !intermissionEnt && !oldSavedPosition.trueIntermissionEntity)
                {
                    AsyncPersistentDataManager<IntermissionCamPosition>.addItem(newPosition, true);
                } else if(oldSavedPosition.trueIntermissionEntity == intermissionEnt)
                {
                    (float posDiff, float angDiff) = oldSavedPosition.DistanceToOther(newPosition);
                    if(posDiff > 0.01 || angDiff > 0.01)
                    {
                        lock (differentOldNewIntermissionCamPositions)
                        {
                            if (differentOldNewIntermissionCamPositions.ContainsKey(mapname))
                            {
                                differentOldNewIntermissionCamPositions[mapname].dupeCount++;// = new IntermissionCamPositionTuple(newPosition, oldSavedPosition, differentOldNewIntermissionCamPositions[mapname].Item3+1);
                            }
                            else
                            {
                                differentOldNewIntermissionCamPositions[mapname] = new IntermissionCamPositionTuple(mapname,newPosition, oldSavedPosition,0);
                            }
                        }
                    }
                }
            }
        }


        private void btnRenderStackedLevelShot_Click(object sender, RoutedEventArgs e)
        {

            try
            { // just in case of some invalid directory or whatever

                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "TIFF images (.tif;.tiff)|*.tif;*.tiff";

                //Directory.CreateDirectory(imagesSubDir);

                string imagesSubDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "images");
                if (Directory.Exists(imagesSubDir))
                {
                    ofd.InitialDirectory = imagesSubDir;
                }


                if (ofd.ShowDialog() == true)
                {
                    string filename = ofd.FileName;

                    TaskManager.TaskRun(() => {
                        try
                        {
                            LevelShotData lsData = LevelShotData.FromTiff(File.ReadAllBytes(filename));
                            string filenameString = System.IO.Path.GetFileNameWithoutExtension(filename) + "_RENDER_"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            string imagesSubDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename), "lsRender");
                            Directory.CreateDirectory(imagesSubDir);
                            Bitmap bmp = LevelShotData.ToBitmap(lsData.data,0);

                            if (bmp is null) {

                                Helpers.logToFile($"Error converting {filename} to levelshot: {e.ToString()}");
                                return;
                            }
                            filenameString = Helpers.MakeValidFileName(filenameString) + ".png";
                            filenameString = System.IO.Path.Combine(imagesSubDir, filenameString);
                            filenameString = Helpers.GetUnusedFilename(filenameString);

                            bmp.Save(filenameString);
                            bmp.Dispose();

                            // Open it maybe :)
                            using Process fileopener = new Process();
                            fileopener.StartInfo.FileName = "explorer";
                            fileopener.StartInfo.Arguments = $"\"{System.IO.Path.GetFullPath(filenameString)}\"";
                            fileopener.Start();

                        }
                        catch(Exception e)
                        {
                            Helpers.logToFile($"Error doing manual levelshot render: {e.ToString()}");
                        }
                    }, $"Manual levelshot renderer {filename}");
                }
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing manual levelshot render (outer): {e.ToString()}");
            }
        }

        private void btnRenderStackedMultipleLevelShot_Click(object sender, RoutedEventArgs e)
        {
            try
            { // just in case of some invalid directory or whatever

                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "TIFF images (.tif;.tiff)|*.tif;*.tiff";
                ofd.Multiselect = true;

                string imagesSubDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "images");
                if (Directory.Exists(imagesSubDir))
                {
                    ofd.InitialDirectory = imagesSubDir;
                }

                List<string> sourceFiles = new List<string>();

                while (ofd.ShowDialog() == true)
                {
                    sourceFiles.AddRange(ofd.FileNames);
                }

                if (sourceFiles.Count == 0) return;
                if (sourceFiles.Count == 1)
                {
                    MessageBox.Show("Please select at leaast 2 files for multistack. Aborting.");
                    return;
                }

                //Directory.CreateDirectory(imagesSubDir);
                List<string> finalFileParts = new List<string>();
                foreach(string sourceFile in sourceFiles)
                {
                    string clean = System.IO.Path.GetFileNameWithoutExtension(sourceFile);
                    if(clean.Length > 0)
                    {
                        int lastLetter = clean.Length - 1;
                        while(lastLetter > 0 && clean[lastLetter] >= '0' && clean[lastLetter] <= '9'|| clean[lastLetter] >= 'A' && clean[lastLetter] <= 'F'|| clean[lastLetter] >= 'a' && clean[lastLetter] <= 'f')
                        {
                            lastLetter--;
                        }
                        if(clean[lastLetter] == '_')
                        {
                            clean = clean.Substring(0,lastLetter);
                        }
                        finalFileParts.Add(clean);
                    }
                }

                string outFile = finalFileParts.Count == 0 ? "multiStack" : string.Join('_', finalFileParts);

                if(outFile.Length > 150)
                {
                    outFile = $"{outFile.Substring(0, 150)}[toolong]";
                }

                TaskManager.TaskRun(() => {
                    try
                    {
                        LevelShotData lsData = LevelShotData.FromTiff(File.ReadAllBytes(sourceFiles[0]));

                        for(int i=1;i<sourceFiles.Count;i++)
                        {
                            LevelShotData lsData2 = LevelShotData.FromTiff(File.ReadAllBytes(sourceFiles[i]));
                            LevelShotData.SumData(lsData.data, lsData2.data);
                        }

                        string filenameString = System.IO.Path.GetFileNameWithoutExtension(outFile) + "_RENDER_" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string imagesSubDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(sourceFiles[0]), "lsRender");
                        Directory.CreateDirectory(imagesSubDir);
                        Bitmap bmp = LevelShotData.ToBitmap(lsData.data, 0);

                        if (bmp is null)
                        {

                            Helpers.logToFile($"Error converting stacked levelshot {outFile} to bitmap: {e.ToString()}");
                            return;
                        }
                        filenameString = Helpers.MakeValidFileName(filenameString) + ".png";
                        filenameString = System.IO.Path.Combine(imagesSubDir, filenameString);
                        filenameString = Helpers.GetUnusedFilename(filenameString);

                        bmp.Save(filenameString);
                        bmp.Dispose();

                        // Open it maybe :)
                        using Process fileopener = new Process();
                        fileopener.StartInfo.FileName = "explorer";
                        fileopener.StartInfo.Arguments = $"\"{System.IO.Path.GetFullPath(filenameString)}\"";
                        fileopener.Start();

                    }
                    catch (Exception e)
                    {
                        Helpers.logToFile($"Error doing manual levelshot render: {e.ToString()}");
                    }
                }, $"Manual levelshot multi-stack renderer {outFile}");


            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing manual levelshot render (outer): {e.ToString()}");
            }
        }

        private void generateAnimationBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Animation.cfg (.cfg)|*.cfg";
            if (ofd.ShowDialog() == true)
            {
                string filename = ofd.FileName;

                byte[] jk2_102data = SaberAnimationStuff.GenerateAnimSaberData<JK2Anims102>(File.ReadAllLines(filename));
                byte[] jk2_104data = SaberAnimationStuff.GenerateAnimSaberData<JK2Anims104>(File.ReadAllLines(filename));

                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Animation binary (.abin)|*.abin";
                sfd.FileName = "jk2_102_anim.abin";
                sfd.Title = "Save jk2 1.02 animation binary";
                if(sfd.ShowDialog() == true)
                {
                    File.WriteAllBytes(sfd.FileName,jk2_102data);
                }
                sfd.FileName = "jk2_104_anim.abin";
                sfd.Title = "Save jk2 1.04 animation binary";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllBytes(sfd.FileName,jk2_104data);
                }

                /*TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, FindIntermissionInBsp);
                    zipRecursor.HandleFile(filename);
                    processConflictingIntermissionCamPositions();
                }, $"Intermission cam finding in file {filename}");*/
            }
        }

        private void generateAnimationBinaryJKABtn_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Animation.cfg (.cfg)|*.cfg";
            if (ofd.ShowDialog() == true)
            {
                string filename = ofd.FileName;

                byte[] jkadata = SaberAnimationStuff.GenerateAnimSaberData<JKAAnims>(File.ReadAllLines(filename));
                
                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Animation binary (.abin)|*.abin";
                sfd.FileName = "jka_anim.abin";
                sfd.Title = "Save jka animation binary";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllBytes(sfd.FileName, jkadata);
                }
                /*TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, FindIntermissionInBsp);
                    zipRecursor.HandleFile(filename);
                    processConflictingIntermissionCamPositions();
                }, $"Intermission cam finding in file {filename}");*/
            }
        }
        private void generateAnimationBinaryStringGeneralBtn_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Animation.cfg (.cfg)|*.cfg";
            if (ofd.ShowDialog() == true)
            {
                string filename = ofd.FileName;

                StringBuilder cStr = new StringBuilder();

                byte[] jkadata = SaberAnimationStuff.GenerateAnimSaberData<GeneralAnims>(File.ReadAllLines(filename), cStr);
                
                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Animation binary (.abin)|*.abin";
                sfd.FileName = "jka_anim.abin";
                sfd.Title = "Save jka animation binary";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllBytes(sfd.FileName, jkadata);
                }

                sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "C source file (.c)|*.c";
                sfd.FileName = "animationArray.c";
                sfd.Title = "Save general animation c array";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, cStr.ToString());
                }
                /*TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, FindIntermissionInBsp);
                    zipRecursor.HandleFile(filename);
                    processConflictingIntermissionCamPositions();
                }, $"Intermission cam finding in file {filename}");*/
            }
        }
        private void btnCreateMinimapsInFolderPath_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                string folder = fbd.SelectedPath;
                TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, MakeMinimapFromBSP);
                    zipRecursor.HandleFolder(folder);
                    processConflictingIntermissionCamPositions();
                }, $"Minimap creation in folder path {folder}");
            }
        }

        private void btnCreateMinimapsInFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "BSP, ZIP, RAR, PK3 files (.bsp;.zip;.pk3)|*.bsp;*.zip;*.rar;*.pk3";
            if (ofd.ShowDialog() == true)
            {
                string filename = ofd.FileName;

                TaskManager.TaskRun(() => {

                    ZipRecursor zipRecursor = new ZipRecursor(bspRegex, MakeMinimapFromBSP);
                    zipRecursor.HandleFile(filename);
                    processConflictingIntermissionCamPositions();
                }, $"Minimap creation in file {filename}");
            }
        }


        private void MakeMinimapFromBSP(string filename, byte[] fileData, string path)
        {
            Stack<string> pathStack = new Stack<string>();
            string[] pathPartsArr = path is null ? new string[0] : path.Split(new char[] { '\\', '/' });
            bool mapsFolderFound = false;
            for (int i = pathPartsArr.Length - 1; i >= 0; i--)
            {
                string pathPart = pathPartsArr[i];
                if (pathPart.Equals("maps", StringComparison.InvariantCultureIgnoreCase))
                {
                    mapsFolderFound = true;
                    break;
                }
                else if (pathPart.EndsWith(".pk3", StringComparison.InvariantCultureIgnoreCase) || pathPart.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    // this is weird. must be some weird isolated file
                    pathStack.Clear();
                    break;
                }
                else
                {
                    pathStack.Push(pathPart);
                }
            }
            if (!mapsFolderFound)
            {
                pathStack.Clear();
            }
            // TODO What if there is some maps folder really low in the hierarchy?
            StringBuilder sb = new StringBuilder();
            while (pathStack.Count > 0)
            {
                sb.Append($"{pathStack.Pop().ToLowerInvariant()}/");
            }
            sb.Append(System.IO.Path.GetFileNameWithoutExtension(filename).ToLowerInvariant());
            string mapname = sb.ToString();
            Debug.WriteLine($"Found {filename} ({mapname}) in {path}");
            mapname = mapname.ToLowerInvariant();

            BSPToMiniMap.MakeMiniMap(mapname, fileData, 0.1f, 500,500,10 );

            
        }

        
    }
}
