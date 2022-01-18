using JKClient;
using System;
using System.Collections.Generic;
using System.Data;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            getServers();
        }

        public async void getServers()
        {
            var serverBrowser = new ServerBrowser();
            serverBrowser.Start(ExceptionCallback);
            var servers = await serverBrowser.GetNewList();
            servers = await serverBrowser.RefreshList();

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
        }

        Task ExceptionCallback(JKClientException exception)
        {
            Debug.WriteLine(exception);
            return null;
        }
    }
}
