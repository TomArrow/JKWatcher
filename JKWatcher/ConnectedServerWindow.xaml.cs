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

        private List<Client> connections = new List<Client>();

        private const int maxLogLength = 10000;
        private string logString = "Begin of Log\n";

        public ConnectedServerWindow(ServerInfo serverInfoA)
        {
            serverInfo = serverInfoA;
            InitializeComponent();
            this.Title = serverInfo.Address.ToString() + " (" + serverInfo.HostName + ")";

            createNewConnection();

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

            foreach (JKClient.JKClient connection in connections)
            {
                stopDemoRecord(connection);
                disconnect(connection);
                break;
            }
        }

        private void addToLog(string someString)
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

        private async void createNewConnection()
        {
            Client connection = new Client();
            connection.Name = "Padawan";

            connection.ServerCommandExecuted += ServerCommandExecuted;
            connection.ServerInfoChanged += Connection_ServerInfoChanged;
            connection.Start(ExceptionCallback);
            await connection.Connect(serverInfo);

            playerListDataGrid.ItemsSource = connection.ClientInfo;


            connections.Add(connection);
            addToLog("New connection created.");
        }

        // Update player list
        private void Connection_ServerInfoChanged(ServerInfo obj)
        {
            
            if (connections.Count == 0) return;

            foreach (JKClient.JKClient connection in connections)
            {
                if (connection.Status == ConnectionStatus.Active)
                {
                    Dispatcher.Invoke(() => {

                        playerListDataGrid.ItemsSource = connection.ClientInfo;
                    });
                    break;
                }
            }
        }

        private void disconnect(JKClient.JKClient connection)
        {

            connection.Disconnect();
            connection.ServerCommandExecuted -= ServerCommandExecuted;
            connection.Stop();
            connection.Dispose();
            addToLog("Disconnected.");
        }

        private async void startDemoRecord(JKClient.JKClient connection)
        {
            addToLog("Initializing demo recording...");
            string timeString = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(timeString + "-" + connection.ServerInfo.MapName, connection.ServerInfo.Protocol);
            await connection.Record_f(unusedDemoFilename);

            addToLog("Demo recording started.");
        }
        private void stopDemoRecord(JKClient.JKClient connection)
        {
            addToLog("Stopping demo recording...");
            connection.StopRecord_f();
            addToLog("Demo recording stopped.");
        }

        private Task ExceptionCallback(JKClientException exception)
        {
            addToLog(exception.ToString());
            Debug.WriteLine(exception);
            return null;
        }
        void ServerCommandExecuted(CommandEventArgs commandEventArgs)
        {
            addToLog(commandEventArgs.Command.Argv(0)+" "+ commandEventArgs.Command.Argv(1)+" "+ commandEventArgs.Command.Argv(2)+" "+ commandEventArgs.Command.Argv(3));
            Debug.WriteLine(commandEventArgs.Command.Argv(0));
        }

        private void recordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            foreach(JKClient.JKClient connection in connections)
            {
                if(connection.Status == ConnectionStatus.Active)
                {

                    startDemoRecord(connection);
                    break;
                }
            }
        }

        private void stopRecordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (connections.Count == 0) return;

            foreach (JKClient.JKClient connection in connections)
            {
                if (connection.Demorecording)
                {

                    stopDemoRecord(connection);
                    break;
                }
            }
        }

        private void commandSendBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (JKClient.JKClient connection in connections)
            {
                if (connection.Status == ConnectionStatus.Active)
                {
                    string command = commandLine.Text;
                    commandLine.Text = "";
                    connection.ExecuteCommand(command);

                    break;
                }
            }
        }
    }
}
