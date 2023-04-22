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

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private List<ConnectedServerWindow> connectedServerWindows = new List<ConnectedServerWindow>();




        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();

        public MainWindow()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher"));

            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            protocols.ItemsSource = System.Enum.GetValues(typeof(ProtocolVersion));
            protocols.SelectedItem = ProtocolVersion.Protocol15;
            //getServers();

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

        private async void ctfAutoConnecter(CancellationToken ct)
        {
            bool nextCheckFast = false;
            while (true)
            {
                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                System.Threading.Thread.Sleep(nextCheckFast ? 60000 :  60000 *2); // every 2 min or 1 min if fast recheck requested (see code below)

                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                nextCheckFast = false;

                bool ctfAutoJoinActive = false;
                bool ctfAutoJoinWithStrobeActive = false;
                int minPlayersForJoin = 3;
                Dispatcher.Invoke(()=> {
                    ctfAutoJoinActive = ctfAutoJoin.IsChecked == true;
                    ctfAutoJoinWithStrobeActive = ctfAutoJoinWithStrobe.IsChecked == true;
                    if(!int.TryParse(ctfAutoJoinMinPlayersTxt.Text, out minPlayersForJoin))
                    {
                        minPlayersForJoin = 3;
                    }
                });

                NetAddress[] manualServers = getManualServers();
                ServerBrowser.SetHiddenServers(manualServers);

                if (ctfAutoJoinActive)
                {
                    IEnumerable<ServerInfo> servers = null;

                    ServerBrowser serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15)) { RefreshTimeout = 30000L }; // The autojoin gets a nice long refresh time out to avoid wrong client numbers being reported.

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
                        if (serverInfo.Version == ClientVersion.JO_v1_02)
                        {
                            bool alreadyConnected = false;
                            foreach (ConnectedServerWindow window in connectedServerWindows)
                            {
                                if (window.netAddress == serverInfo.Address && window.protocol == serverInfo.Protocol)
                                {
                                    alreadyConnected = true;
                                }
                            }
                            // We want to be speccing/recording this.
                            // Check if we are already connected. If so, do nothing.
                            if (!alreadyConnected && !serverInfo.NeedPassword && (serverInfo.Clients >= minPlayersForJoin && serverInfo.StatusResponseReceived) && (serverInfo.GameType == GameType.CTF || serverInfo.GameType == GameType.CTY))
                            {
                                
                                Dispatcher.Invoke(()=> {

                                    ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName);
                                    connectedServerWindows.Add(newWindow);
                                    newWindow.Closed += (a, b) => { connectedServerWindows.Remove(newWindow); };
                                    newWindow.Show();
                                    newWindow.createCTFOperator();
                                    if (ctfAutoJoinWithStrobeActive)
                                    {
                                        newWindow.createStrobeOperator();
                                    }
                                    newWindow.recordAll();
                                });
                            } else if (!alreadyConnected && serverInfo.Clients >= minPlayersForJoin && !serverInfo.StatusResponseReceived)
                            {
                                // If there's a potential candidate but we haven't received info about whether the players are real players, make next refresh with less waiting time. It's possible the StatusResponse just didn't
                                // arrive for some reason
                                nextCheckFast = true;
                            }

                        }


                    }
                    saveServerStats(servers);
                    serverBrowser.Stop();
                    serverBrowser.Dispose();

                }
            }
        }


        public async void getServers()
        {
            IEnumerable<ServerInfo> servers = null;
            connectBtn.IsEnabled = false;

            NetAddress[] manualServers = getManualServers();
            ServerBrowser.SetHiddenServers(manualServers);

            ServerBrowser serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15));

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
                if(serverInfo.Version == ClientVersion.JO_v1_02)
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
                //ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address,serverInfo.Protocol,serverInfo.HostName, pw);
                connectedServerWindows.Add(newWindow);
                newWindow.Closed += (a,b)=> { connectedServerWindows.Remove(newWindow); };
                newWindow.Show();
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
                //ConnectedServerWindow newWindow = new ConnectedServerWindow(ip, protocol.Value);
                ConnectedServerWindow newWindow = new ConnectedServerWindow(NetAddress.FromString(ip), protocol.Value,null,pw);
                connectedServerWindows.Add(newWindow);
                newWindow.Closed += (a, b) => { connectedServerWindows.Remove(newWindow); };
                newWindow.Show();
            }
            /*ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            //MessageBox.Show(serverInfo.HostName);
            if (serverInfo != null)
            {
                ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                connectedServerWindows.Add(newWindow);
                newWindow.Closed += (a, b) => { connectedServerWindows.Remove(newWindow); };
                newWindow.Show();
            }*/
        }

        private void colorDecoderBtn_Click(object sender, RoutedEventArgs e)
        {
            var colorDecoderWindow = new ColorTimeDecoder();
            colorDecoderWindow.Show();
        }
    }
}
