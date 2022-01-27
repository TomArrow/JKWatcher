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

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for ConnectedServerWindow.xaml
    /// </summary>
    public partial class ConnectedServerWindow : Window
    {

        private ServerInfo serverInfo;

        private List<Connection> connections = new List<Connection>();

        private const int maxLogLength = 10000;
        private string logString = "Begin of Log\n";

        // TODO: Send "score" command to server every second or 2 so we always have up to date scoreboards. will eat a bit more space maybe but should be cool. make it possible to disable this via some option, or to set interval

        public ConnectedServerWindow(ServerInfo serverInfoA)
        {
            serverInfo = serverInfoA;
            InitializeComponent();
            this.Title = serverInfo.Address.ToString() + " (" + serverInfo.HostName + ")";

            connections.Add(new Connection(this,serverInfo));

            logTxt.Text = logString;

            this.Closed += ConnectedServerWindow_Closed;
        }

        private void ConnectedServerWindow_Closed(object sender, EventArgs e)
        {
            CloseDown();
        }

        ~ConnectedServerWindow() // Not sure if needed tbh
        {
            CloseDown();
        }

        private void CloseDown()
        {
            if (connections.Count == 0) return;

            foreach (Connection connection in connections)
            {
                connection.stopDemoRecord();
                connection.disconnect();
                break;
            }
        }

        public void addToLog(string someString)
        {
            lock (logString) { 
                someString += "\n";
                int newLogLength = logString.Length + someString.Length;
                if (newLogLength <= maxLogLength)
                {
                    logString += someString;
                } else
                {
                    int cutAway = newLogLength -maxLogLength;
                    logString = logString.Substring(cutAway) + someString;
                }
                Dispatcher.Invoke(()=> {

                    logTxt.Text = logString;
                });
            }
        }

        
        private void recordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            foreach(Connection connection in connections)
            {
                if(connection.client.Status == ConnectionStatus.Active)
                {

                    connection.startDemoRecord();
                    break;
                }
            }
        }

        private void stopRecordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            foreach (Connection connection in connections)
            {
                if (connection.client.Demorecording)
                {

                    connection.stopDemoRecord();
                    break;
                }
            }
        }

        private void commandSendBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (Connection connection in connections)
            {
                if (connection.client.Status == ConnectionStatus.Active)
                {
                    string command = commandLine.Text;
                    commandLine.Text = "";
                    connection.client.ExecuteCommand(command);

                    break;
                }
            }
        }
    }
}
