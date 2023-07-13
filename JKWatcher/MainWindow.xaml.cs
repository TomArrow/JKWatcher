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

            // Check botroutes
            BotRouteManager.Initialize();

            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                System.Threading.Thread.Sleep(nextCheckFast ? 60000 :  60000 *2); // every 2 min or 1 min if fast recheck requested (see code below)

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
                bool allJK2Versions = false;
                Dispatcher.Invoke(()=> {
                    ctfAutoJoinActive = ctfAutoJoin.IsChecked == true;
                    ctfAutoJoinWithStrobeActive = ctfAutoJoinWithStrobe.IsChecked == true;
                    ffaAutoJoinActive = ffaAutoJoin.IsChecked == true;
                    ffaAutoJoinSilentActive = ffaAutoJoinSilent.IsChecked == true;
                    ffaAutoJoinExclude = ffaAutoJoinExcludeTxt.Text;
                    jkaMode = jkaModeCheck.IsChecked == true;
                    allJK2Versions = allJK2VersionsCheck.IsChecked == true;
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
                ServerBrowser.SetHiddenServers(manualServers);


                int delayedConnectServersCount = 0;
                lock (serversToConnectDelayed)
                {
                    delayedConnectServersCount = serversToConnectDelayed.Count;
                }

                if (ctfAutoJoinActive || ffaAutoJoinActive || delayedConnectServersCount > 0)
                {
                    IEnumerable<ServerInfo> servers = null;


                    ServerBrowser serverBrowser = jkaMode ? new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { RefreshTimeout = 30000L,ForceStatus=true }: new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15,allJK2Versions || delayedConnectServersCount > 0)) { RefreshTimeout = 30000L, ForceStatus=true }; // The autojoin gets a nice long refresh time out to avoid wrong client numbers being reported.

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
                        if (serverInfo.Version == ClientVersion.JO_v1_02 || jkaMode || allJK2Versions || delayedConnectServersCount > 0)
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

                                if(delayedConnectServersCount > 0)
                                {
                                    ServerToConnect srvTCChosen = null;
                                    lock (serversToConnectDelayed) { 
                                        foreach (ServerToConnect srvTC in serversToConnectDelayed)
                                        {
                                            if (srvTC.FitsRequirements(serverInfo))
                                            {
                                                srvTCChosen = srvTC;
                                                break;
                                            }
                                        }
                                        if(srvTCChosen != null)
                                        {
                                            ConnectFromConfig(serverInfo, srvTCChosen);
                                            continue;
                                            //serversToConnectDelayed.Remove(srvTCChosen); // actually dont delete it.
                                        }
                                    }
                                }

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

                                    if (serverInfo.RealClients >= ffaMinPlayersForJoin && statusReceived)
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
                                    }
                                }
                            }

                        }


                    }
                    saveServerStats(baselineFilteredServers);
                    serverBrowser.Stop();
                    serverBrowser.Dispose();

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

            NetAddress[] manualServers = getManualServers();
            ServerBrowser.SetHiddenServers(manualServers);


            bool jkaMode = false;
            bool allJK2Versions = false;
            Dispatcher.Invoke(() => {
                jkaMode = jkaModeCheck.IsChecked == true;
                allJK2Versions = allJK2VersionsCheck.IsChecked == true;
            });

            ServerBrowser serverBrowser = jkaMode ? new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { ForceStatus = true } : new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, allJK2Versions)) { ForceStatus = true};

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

            foreach(ServerInfo serverInfo in servers)
            {
                if(serverInfo.Version == ClientVersion.JO_v1_02 || jkaMode || allJK2Versions)
                {
                    filteredServers.Add(serverInfo);
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
            Debug.WriteLine(exception);
            return null;
        }

        private void connectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            string pw = pwListTxt.Text.Length > 0 ? pwListTxt.Text : null;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                lock (connectedServerWindows)
                {
                    //ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName, pw);
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
            ProtocolVersion? protocol = protocols.SelectedItem != null ? (ProtocolVersion)protocols.SelectedItem : null;
            if(ip.Length > 0 && protocol != null)
            {
                lock (connectedServerWindows)
                {
                    //ConnectedServerWindow newWindow = new ConnectedServerWindow(ip, protocol.Value);
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(NetAddress.FromString(ip), protocol.Value, null, pw);
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
            public string hostName = null;
            public string playerName = null;
            public string password = null;
            public bool autoRecord = false;
            public bool delayed = false;
            public int retries = 5;
            public int minRealPlayers = 1;
            public int? botSnaps = 5;
            public string[] watchers = null;

            public ServerToConnect(ConfigSection config)
            {
                hostName = config["hostName"]?.Trim();
                if (hostName == null || hostName.Length == 0) throw new Exception("ServerConnectConfig: hostName must be provided");
                playerName = config["playerName"]?.Trim();
                password = config["password"]?.Trim();
                autoRecord = config["autoRecord"]?.Trim().Atoi()>0;
                retries = (config["retries"]?.Trim().Atoi()).GetValueOrDefault(5);
                botSnaps = config["botSnaps"]?.Trim().Atoi();
                watchers = config["watchers"]?.Trim().Split(',');
                minRealPlayers = Math.Max(0,(config["minPlayers"]?.Trim().Atoi()).GetValueOrDefault(0));
                delayed = config["delayed"]?.Trim().Atoi() > 0;

                if(minRealPlayers>0 && !delayed)
                {
                    throw new Exception("ServerConnectConfig: minPlayers value > 0 requires delayed=1");
                }
            }

            public bool FitsRequirements(ServerInfo serverInfo)
            {
                if (serverInfo.HostName == null) return false;
                if (serverInfo.HostName.Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(hostName) || Q3ColorFormatter.cleanupString(serverInfo.HostName).Contains(Q3ColorFormatter.cleanupString(hostName))) // Improve this to also find non-colorcoded terms etc
                {
                    return (serverInfo.RealClients >= minRealPlayers || minRealPlayers == 0) && (!serverInfo.NeedPassword || serverInfo.NeedPassword && password != null && password.Length > 0);
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
                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName,serverToConnect.password, new ConnectedServerWindow.ConnectionOptions() { userInfoName= serverToConnect.playerName });
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
                        foreach (string camera in serverToConnect.watchers)
                        {
                            switch (camera)
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
                                default:
                                    break;
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
            if (cp.GetValue("__general__", "jkaMode", 0) == 1)
            {
                jkaModeCheck.IsChecked = true;
            }
            if (cp.GetValue("__general__", "allJK2Versions", 0) == 1)
            {
                allJK2VersionsCheck.IsChecked = true;
            }
            bool jkaMode = jkaModeCheck.IsChecked == true;
            List<ServerToConnect> serversToConnect = new List<ServerToConnect>();
            lock (serversToConnectDelayed)
            {
                serversToConnectDelayed.Clear();
                foreach (ConfigSection section in cp.Sections)
                {
                    if (section.SectionName == "__general__") continue;

                    if (section["hostName"] != null)
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
                            serversToConnect.Remove(elementToRemove);
                        }
                        elementsToRemove.Clear();


                        IEnumerable<ServerInfo> servers = null;

                        ServerBrowser serverBrowser = jkaMode ? new ServerBrowser(new JABrowserHandler(ProtocolVersion.Protocol26)) { RefreshTimeout = 10000L,ForceStatus=true } : new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15, true)) { RefreshTimeout = 10000L,ForceStatus=true }; // The autojoin gets a nice long refresh time out to avoid wrong client numbers being reported.

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
                                    if (serverToConnect.FitsRequirements(serverInfo))
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
            lock (executionInProgressMutex) { 
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



    }
}
