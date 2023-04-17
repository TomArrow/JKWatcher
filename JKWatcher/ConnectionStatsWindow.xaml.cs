using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for ConnectionStatsWindow.xaml
    /// </summary>
    public partial class ConnectionStatsWindow : Window
    {
        // Stats to do:
        // Pending commands and last sent command(s) with timestamp
        // Demo data rate at KB/s, KB/minute, KB/hour, KB/day
        // Current demo size in KB
        // Packet reduction stats (how many were dropped, and/or the reason they were dropped/not dropped (non-delta, not starting with svc_snapshot etc) with count for each)
        // The above should be total amount as well as recent.
        public ConnectionStatsWindow(Connection connA)
        {
            InitializeComponent();

            conn = connA;
            //Dispatcher.Invoke(()=> {
            //    commandQueueGrid.ItemsSource = conn.leakyBucketRequester.ReadOnlyRequestList;
            //    recentCommandQueueGrid.ItemsSource = conn.leakyBucketRequester.RecentExecutedCommandsReadOnly;
            //});
            clientStatsPanel.DataContext = conn.clientStatistics;
            clientStatsPanel2.DataContext = conn.clientStatistics;
            conn.leakyBucketRequester.RequestListUpdated += LeakyBucketRequester_RequestListUpdated;

            conn.PropertyChanged += Conn_PropertyChanged;

            this.Closed += ConnectionStatsWindow_Closed;
        }

        private void Conn_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "clientStatistics")
            {
                Dispatcher.Invoke(() =>
                {
                    clientStatsPanel.DataContext = conn.clientStatistics;
                    clientStatsPanel2.DataContext = conn.clientStatistics;
                });
            }
        }

        Connection conn = null;

        private void ConnectionStatsWindow_Closed(object sender, EventArgs e)
        {
            conn.leakyBucketRequester.RequestListUpdated -= LeakyBucketRequester_RequestListUpdated;
            conn.PropertyChanged -= Conn_PropertyChanged;
            conn = null;
        }

        private void LeakyBucketRequester_RequestListUpdated(object sender, Tuple<LeakyBucketRequester<string, RequestCategory>.Request[], LeakyBucketRequester<string, RequestCategory>.FinishedRequest[]> e)
        {


            Dispatcher.Invoke(() =>
            {
                // Terrible solution tbh but it seems to work ok.
                // Other ways don't properly update,
                // or through ArgumentOutOfRangeException and other bs.
                commandQueueGrid.ItemsSource = e.Item1;
                recentCommandQueueGrid.ItemsSource = e.Item2;
                // This does not work sadly:
                //ICollectionView view = CollectionViewSource.GetDefaultView(commandQueueGrid.ItemsSource);
                //view.Refresh();
                //view = CollectionViewSource.GetDefaultView(recentCommandQueueGrid.ItemsSource);
                //view.Refresh();
                //commandQueueGrid.ItemsSource = null;
                //commandQueueGrid.ItemsSource = conn.leakyBucketRequester.ReadOnlyRequestList;
                //recentCommandQueueGrid.ItemsSource = null;
                //recentCommandQueueGrid.ItemsSource = conn.leakyBucketRequester.RecentExecutedCommandsReadOnly;
            });
        }


    }
}
