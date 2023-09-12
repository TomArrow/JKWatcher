using JKClient;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Salaros.Configuration;
using System.Windows.Threading;
using System.ComponentModel;

// TODO: Javascripts that can be executed and interoperate with the program?
// Or if too hard, just .ini files that can be parsed for instructions on servers that must be connected etc.
// TODO Save server players info as json


namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private List<ConnectedServerWindow> connectedServerWindows = new List<ConnectedServerWindow>();


        static Dictionary<NetAddress,Tuple<DateTime,double>> lastTimeDisconnected = new Dictionary<NetAddress, Tuple<DateTime, double>>(new NetAddressComparer());
        static Random timeFromDisconnectedTimeRangeModifierRandom = new Random();

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





            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task.Factory.StartNew(() => { ctfAutoConnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted);
            backgroundTasks.Add(tokenSource);

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task.Factory.StartNew(() => { fastDelayedConnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted);
            backgroundTasks.Add(tokenSource);

            //Timeline.DesiredFrameRateProperty.OverrideMetadata(
            //    typeof(Timeline),
            //    new FrameworkPropertyMetadata { DefaultValue = 165 }
            //);
            HiResTimerSetter.UnlockTimerResolution();
            WindowPositionManager.Activate();

            executingTxt.DataContext = this;
            SpawnArchiveScript();
        }

        ~MainWindow()
        {
            CloseDown();
        }

        private void CloseDown()
        {
            foreach (CancellationTokenSource backgroundTask in backgroundTasks)
            {
                backgroundTask.Cancel();
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
                string ffaAutoJoinExclude = null;
                int ctfMinPlayersForJoin = 4;
                int ffaMinPlayersForJoin = 2;
                bool jkaMode = false;
                bool mohMode = false;
                bool allJK2Versions = false;
                bool delayedConnecterActive = false;
                Dispatcher.Invoke(()=> {
                    ctfAutoJoinActive = ctfAutoJoin.IsChecked == true;
                    ctfAutoJoinWithStrobeActive = ctfAutoJoinWithStrobe.IsChecked == true;
                    ffaAutoJoinActive = ffaAutoJoin.IsChecked == true;
                    ffaAutoJoinSilentActive = ffaAutoJoinSilent.IsChecked == true;
                    ffaAutoJoinExclude = ffaAutoJoinExcludeTxt.Text;
                    jkaMode = jkaModeCheck.IsChecked == true;
                    mohMode = mohModeCheck.IsChecked == true;
                    allJK2Versions = allJK2VersionsCheck.IsChecked == true;
                    delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true;
                    if (!int.TryParse(ctfAutoJoinMinPlayersTxt.Text, out ctfMinPlayersForJoin))
                    {
                        ctfMinPlayersForJoin = 4;
                    }
                    if (!int.TryParse(ffaAutoJoinMinPlayersTxt.Text, out ffaMinPlayersForJoin))
                    {
                        ffaMinPlayersForJoin = 4;
                    }
                });

                string[] ffaAutoJoinExcludeList = ffaAutoJoinExclude == null ? null : ffaAutoJoinExclude.Split(",",StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);

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

                    try
                    {

                        serverBrowser.Start(ExceptionCallback);
                        servers = await serverBrowser.GetNewList();
                        //servers = await serverBrowser.RefreshList();
                    }
                    catch (Exception e)
                    {
                        // Just in case getting servers crashes or sth.
                        continue;
                    }

                    if (servers == null) continue;

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
                            if (autoConnectRecentlyClosedBlockList.Contains(serverInfo.Address))
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
                            if(!alreadyConnected && !serverInfo.NeedPassword) {

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
                                                if (!srvTC.pollingInterval.HasValue || fastDelayedConnecterBroken)
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
                                                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName);
                                                    connectedServerWindows.Add(newWindow);
                                                    newWindow.Loaded += NewWindow_Loaded;
                                                    newWindow.Closed += NewWindow_Closed;
                                                    newWindow.ShowActivated = false;
                                                    newWindow.Show();
                                                    newWindow.createCTFOperator();
                                                    if (ctfAutoJoinWithStrobeActive)
                                                    {
                                                        newWindow.createStrobeOperator();
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
                                    } else if (ffaAutoJoinActive && !(serverInfo.GameType == GameType.CTF || serverInfo.GameType == GameType.CTY))
                                    {
                                        // Check for exclude filter
                                        if(ffaAutoJoinExcludeList != null)
                                        {
                                            bool serverIsExcluded = false;
                                            foreach(string excludeString in ffaAutoJoinExcludeList)
                                            {
                                                if(serverInfo.HostName.Contains(excludeString,StringComparison.OrdinalIgnoreCase) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(excludeString, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    serverIsExcluded = true;
                                                    break;
                                                }
                                            }
                                            if (serverIsExcluded) continue; // Skip this one.
                                        }

                                        if (serverInfo.RealClients >= ffaMinPlayersForJoin && statusReceived && !serverInfo.NoBots)
                                        {

                                            Dispatcher.Invoke(() => {

                                                lock (connectedServerWindows)
                                                {
                                                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,null,new ConnectedServerWindow.ConnectionOptions(){ autoUpgradeToCTF = true, autoUpgradeToCTFWithStrobe = ctfAutoJoinWithStrobeActive, attachClientNumToName=false, demoTimeColorNames = false, silentMode = ffaAutoJoinSilentActive });
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
                    serverBrowser.Dispose();

                }
            }
        }

        class DelayConnecterData
        {
            public Task<ServerInfo> pollTask = null;
            public DateTime lastTimePolled = DateTime.Now - new TimeSpan(240000, 0, 0);
            public DateTime lastTimeGotAnswer = DateTime.Now;
        };

        bool fastDelayedConnecterBroken = false;

        private async void fastDelayedConnecter(CancellationToken ct)
        {
            bool nextCheckFast = false;

            ServerBrowser serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, true));

            try
            {

                serverBrowser.Start(ExceptionCallback);
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
                bool delayedConnecterActive = true;
                Dispatcher.Invoke(() => {
                    delayedConnecterActive = delayedConnecterActiveCheck.IsChecked == true;
                });

                if (!delayedConnecterActive) continue;

                lock (serversToConnectDelayed)
                {
                    foreach (ServerToConnect srvTC in serversToConnectDelayed)
                    {
                        if (!srvTC.pollingInterval.HasValue || srvTC.ip == null) continue; // Servers with custom polling interval are handled elsewhere.

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
                            serverPollingInfo[srvTC].lastTimePolled = DateTime.Now; // Avoid fast connecter being identified as broken just because we were connected and then disconnected.
                            continue;
                        }

                        if (!serverPollingInfo.ContainsKey(srvTC))
                        {
                            serverPollingInfo[srvTC] = new DelayConnecterData();
                        }

                        if(serverPollingInfo[srvTC].pollTask == null && (DateTime.Now-serverPollingInfo[srvTC].lastTimePolled).TotalMilliseconds > srvTC.pollingInterval.Value)
                        {
                            serverPollingInfo[srvTC].pollTask = serverBrowser.GetFullServerInfo(srvTC.ip, srvTC.minRealPlayers > 0, true, srvTC.pollingInterval.Value); // Only need status if we have a min real player requirement.
                            serverPollingInfo[srvTC].lastTimePolled = DateTime.Now;
                        }

                        double millisecondsSinceLastAnswer = (DateTime.Now - serverPollingInfo[srvTC].lastTimeGotAnswer).TotalMilliseconds;
                        if (millisecondsSinceLastAnswer > Math.Max(srvTC.pollingInterval.Value*10,60000))
                        {
                            if (!fastDelayedConnecterBroken)
                            {
                                fastDelayedConnecterBroken = true;
                                // Let main loop take care of it then.
                                Helpers.logToFile($"ERROR: fastDelayedConnecter seems to be broken. No answer received in a long time ({millisecondsSinceLastAnswer}ms).");
                            }
                        } else
                        {
                            if (fastDelayedConnecterBroken)
                            {
                                fastDelayedConnecterBroken = false;
                                // Let main loop take care of it then.
                                Helpers.logToFile($"ERROR: fastDelayedConnecter unbroken again. Last answer recent ({millisecondsSinceLastAnswer}ms).");
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

            try
            {

                serverBrowser.Start(ExceptionCallback);
                servers = await serverBrowser.GetNewList();
                //servers = await serverBrowser.RefreshList();
            } catch(Exception e)
            {
                // Just in case getting servers crashes or sth.
                return;
            }

            if (servers == null) return;

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
            serverBrowser.Dispose();
            serverListDataGrid.ItemsSource = filteredServers;
            if(filteredServers.Count == 0)
            {
                connectBtn.IsEnabled = false;
            }
            saveServerStats(servers);
        }

        Task ExceptionCallback(JKClientException exception)
        {
            Helpers.logToFile(new string[] { exception.ToString() });
            Debug.WriteLine(exception);
            return null;
        }

        private void connectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            string userinfoName = userInfoNameTxt.Text.Length > 0 ? userInfoNameTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                lock (connectedServerWindows)
                {
                    ConnectedServerWindow.ConnectionOptions connOpts = null;
                    if (userinfoName != null)
                    {
                        connOpts = new ConnectedServerWindow.ConnectionOptions();
                        if (serverInfo.Protocol >= ProtocolVersion.Protocol6 && serverInfo.Protocol <= ProtocolVersion.Protocol8)
                        {
                            connOpts.LoadMOHDefaults(); // MOH needs different defaults.
                        }
                        connOpts.userInfoName = userinfoName;
                    }
                    //ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName, pw, connOpts);
                    connectedServerWindows.Add(newWindow);
                    newWindow.Loaded += NewWindow_Loaded;
                    newWindow.Closed += NewWindow_Closed;
                    newWindow.ShowActivated = false;
                    newWindow.Show();
                }
            }
        }

        private void refreshBtn_Click(object sender, RoutedEventArgs e)
        {
            connectBtn.IsEnabled = false;
            getServers();
        }

        private void serverListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(serverListDataGrid.Items.Count > 0 && serverListDataGrid.SelectedItems.Count > 0)
            {
                connectBtn.IsEnabled = true;
            } else
            {
                connectBtn.IsEnabled = false;
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
                        if (protocol.Value >= ProtocolVersion.Protocol6 && protocol.Value <= ProtocolVersion.Protocol8)
                        {
                            connOpts.LoadMOHDefaults(); // MOH needs different defaults.
                        }
                        connOpts.userInfoName = userinfoName;
                    }
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
            public int minRealPlayers { get; init; } = 0;
            public int timeFromDisconnect { get; init; } = 0;
            public int timeFromDisconnectUpperRange { get; init; } = 0;
            public int? botSnaps { get; init; } = 5;
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
            public int gameTypes { get; init; } = 0;
            public bool attachClientNumToName { get; init; } = true;
            public bool demoTimeColorNames { get; init; } = true;
            public bool silentMode { get; init; } = false;
            public int? pollingInterval { get; init; } = null;

            public ServerInfo lastFittingServerInfo = null;

            public ServerToConnect(ConfigSection config)
            {
                sectionName = config.SectionName;

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
                if (hostName == null && ip==null) throw new Exception("ServerConnectConfig: hostName or ip must be provided");
                playerName = config["playerName"]?.Trim();
                password = config["password"]?.Trim();
                mapChangeCommands = config["mapChangeCommands"]?.Trim();
                quickCommands = config["quickCommands"]?.Trim();
                conditionalCommands = config["conditionalCommands"]?.Trim();
                disconnectTriggers = config["disconnectTriggers"]?.Trim();
                autoRecord = config["autoRecord"]?.Trim().Atoi()>0;
                retries = (config["retries"]?.Trim().Atoi()).GetValueOrDefault(5);
                delayPerWatcher = (config["delayPerWatcher"]?.Trim().Atoi()).GetValueOrDefault(0);
                botSnaps = config["botSnaps"]?.Trim().Atoi();
                pollingInterval = config["pollingInterval"]?.Trim().Atoi();
                watchers = config["watchers"]?.Trim().Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                mapNames = config["maps"]?.Trim().Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                minRealPlayers = Math.Max(0,(config["minPlayers"]?.Trim().Atoi()).GetValueOrDefault(0));
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

                string[] gameTypesStrings = config["gameTypes"]?.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
                                gameTypes |= (1 << (int)GameType.CTF);
                                break;
                            case "ctf":
                                gameTypes |= (1 << (int)GameType.CTY);
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
            }

            public bool FitsRequirements(ServerInfo serverInfo, ref bool matchesButMightNotMeetRequirements)
            {
                matchesButMightNotMeetRequirements = false;
                if (serverInfo.HostName == null) return false;
                if (serverInfo.Address == ip || hostName != null && (serverInfo.HostName.Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(Q3ColorFormatter.cleanupString(hostName)))) // Improve this to also find non-colorcoded terms etc
                {
                    lastFittingServerInfo = serverInfo;
                    matchesButMightNotMeetRequirements = true;
                    if (!this.active) return false;
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
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,serverToConnect.password, new ConnectedServerWindow.ConnectionOptions() { userInfoName= serverToConnect.playerName, mapChangeCommands=serverToConnect.mapChangeCommands, quickCommands=serverToConnect.quickCommands,conditionalCommands=serverToConnect.conditionalCommands,disconnectTriggers=serverToConnect.disconnectTriggers,attachClientNumToName=serverToConnect.attachClientNumToName,demoTimeColorNames=serverToConnect.demoTimeColorNames,silentMode=serverToConnect.silentMode });
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
                    if (serverToConnect.watchers != null)
                    {
                        int index = 0;
                        foreach (string camera in serverToConnect.watchers)
                        {
                            string cameraLocal = camera;
                            Action watcherCreate = new Action(()=> {
                                switch (cameraLocal)
                                {
                                    case "defrag":
                                    case "ocd":
                                        newWindow.createOCDefragOperator();
                                        break;
                                    case "ctf":
                                        newWindow.createCTFOperator();
                                        break;
                                    case "strobe":
                                        newWindow.createStrobeOperator();
                                        break;
                                    case "ffa":
                                        newWindow.createFFAOperator();
                                        break;
                                    default:
                                        break;
                                }
                            });

                            index++;
                            int delayHere = index * serverToConnect.delayPerWatcher;

                            if(delayHere > 0)
                            {
                                bool localAutoRecord = serverToConnect.autoRecord;
                                Task.Run(()=> { 
                                    System.Threading.Thread.Sleep(delayHere);
                                    Dispatcher.Invoke(()=> {
                                        watcherCreate();
                                        if (localAutoRecord)
                                        {
                                            newWindow.recordAll();
                                        }
                                    });
                                });
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
            string ffaAutoConnectExclude = cp.GetValue("__general__", "ffaAutoConnectExclude", "");
            if (ffaAutoConnectExclude != null)
            {
                ffaAutoConnectExclude = ffaAutoConnectExclude.Trim();
                ffaAutoJoinExcludeTxt.Text = ffaAutoConnectExclude;
            }
            string autoJoinCheckInterval = cp.GetValue("__general__", "autoJoinCheckInterval", "");
            if (autoJoinCheckInterval != null)
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

                    if (section["hostName"] != null || section["ip"] != null)
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

                Task.Run(async () => {

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

                        try
                        {

                            serverBrowser.Start(ExceptionCallback);
                            servers = await serverBrowser.GetNewList();
                            //servers = await serverBrowser.RefreshList();
                        }
                        catch (Exception e)
                        {
                            // Just in case getting servers crashes or sth.
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
                        serverBrowser.Dispose();

                        tries++;
                    }
                    executionInProgress = false;


                });
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

        private async void delayedForceConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerToConnect serverToConnect = (ServerToConnect)delayedConnectsList.SelectedItem;
            string errorString = null;
            if(serverToConnect != null)
            {
                NetAddress ip = serverToConnect.ip;
                if (serverToConnect.lastFittingServerInfo != null)
                {
                    ConnectFromConfig(serverToConnect.lastFittingServerInfo, serverToConnect);
                } else if (ip == null)
                {
                    errorString = ("Cannot force connect to a server without IP. Not implemented.");
                } else
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
                                errorString = ("Exception trying to get ServerInfo for forced connect (during await): " + e.ToString());
                                //continue;
                            }

                            if (serverInfo != null && (serverInfo.StatusResponseReceived || serverInfo.InfoPacketReceived))
                            {

                                ConnectFromConfig(serverInfo, serverToConnect);
                            } else if (serverInfo == null)
                            {
                                errorString = ("Unknown error trying to get ServerInfo for forced connect. serverInfo is null");
                            }
                            else
                            {
                                errorString = ($"Unknown error trying to get ServerInfo for forced connect. status received: {serverInfo.StatusResponseReceived}, info received: {serverInfo.InfoPacketReceived} ");
                            }

                            browser.Stop();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorString = ("Exception trying to get ServerInfo for forced connect (outer): " + ex.ToString());
                    }
                }
            }

            if(errorString != null)
            {
                MessageBox.Show(errorString);
                Helpers.logToFile(errorString);
            }
        }
    }
}
