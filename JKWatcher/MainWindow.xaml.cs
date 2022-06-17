using JKClient;
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

        private async void ctfAutoConnecter(CancellationToken ct)
        {
            while (true)
            {
                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                System.Threading.Thread.Sleep(60000*5); // every 5 min
                ct.ThrowIfCancellationRequested();

                bool ctfAutoJoinActive = false;
                Dispatcher.Invoke(()=> {
                    ctfAutoJoinActive = ctfAutoJoin.IsChecked == true;
                });

                NetAddress[] manualServers = getManualServers();
                ServerBrowser.SetHiddenServers(manualServers);

                if (ctfAutoJoinActive)
                {
                    IEnumerable<ServerInfo> servers = null;

                    ServerBrowser serverBrowser = new ServerBrowser(new JOBrowserHandler(ProtocolVersion.Protocol15));

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
                            if(!serverInfo.NeedPassword && serverInfo.Clients >= 6 && (serverInfo.GameType == GameType.CTF || serverInfo.GameType == GameType.CTY))
                            {
                                // We want to be speccing/recording this.
                                // Check if we are already connected. If so, do nothing.
                                bool alreadyConnected = false;
                                foreach(ConnectedServerWindow window in connectedServerWindows)
                                {
                                    if (window.netAddress == serverInfo.Address && window.protocol == serverInfo.Protocol)
                                    {
                                        alreadyConnected = true;
                                    }
                                }
                                if (!alreadyConnected)
                                {
                                    Dispatcher.Invoke(()=> {

                                        ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address, serverInfo.Protocol, serverInfo.HostName);
                                        connectedServerWindows.Add(newWindow);
                                        newWindow.Closed += (a, b) => { connectedServerWindows.Remove(newWindow); };
                                        newWindow.Show();
                                        newWindow.createCTFOperator();
                                        newWindow.recordAll();
                                    });
                                }
                            }

                        }
                    }

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
        }

        Task ExceptionCallback(JKClientException exception)
        {
            Debug.WriteLine(exception);
            return null;
        }

        private void connectBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerInfo serverInfo = (ServerInfo)serverListDataGrid.SelectedItem;
            //MessageBox.Show(serverInfo.HostName);
            if(serverInfo != null)
            {
                //ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo);
                ConnectedServerWindow newWindow = new ConnectedServerWindow(serverInfo.Address,serverInfo.Protocol,serverInfo.HostName);
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
            ProtocolVersion? protocol = protocols.SelectedItem != null ? (ProtocolVersion)protocols.SelectedItem : null;
            if(ip.Length > 0 && protocol != null)
            {
                //ConnectedServerWindow newWindow = new ConnectedServerWindow(ip, protocol.Value);
                ConnectedServerWindow newWindow = new ConnectedServerWindow(NetAddress.FromString(ip), protocol.Value);
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
    }
}
