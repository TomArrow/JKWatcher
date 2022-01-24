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

        public ConnectedServerWindow(ServerInfo serverInfoA)
        {
            serverInfo = serverInfoA;
            InitializeComponent();
            this.Title = serverInfo.Address.ToString() + " (" + serverInfo.HostName + ")";

            createNewConnection();
        }

        private async void createNewConnection()
        {
            Client connection = new Client();
            connection.Name = "Padawan";

            connection.Start(ExceptionCallback);
            connection.ServerCommandExecuted += ServerCommandExecuted;
            await connection.Connect(serverInfo);

            playerListDataGrid.ItemsSource = connection.ClientInfo;

            connections.Add(connection);
        }

        private void disconnect(JKClient.JKClient connection)
        {

            connection.Disconnect();
            connection.ServerCommandExecuted -= ServerCommandExecuted;
            connection.Stop();
            connection.Dispose();
        }

        private void startDemoRecord(JKClient.JKClient connection)
        {
            string timeString = DateTime.Now.ToString("yyyy-MMMM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(timeString + "-" + connection.ServerInfo.MapName, connection.ServerInfo.Protocol);
            connection.Record_f(unusedDemoFilename);
        }
        private void stopDemoRecord(JKClient.JKClient connection)
        {
            connection.StopRecord_f();
        }

        private Task ExceptionCallback(JKClientException exception)
        {
            Debug.WriteLine(exception);
            return null;
        }
        void ServerCommandExecuted(CommandEventArgs commandEventArgs)
        {
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
