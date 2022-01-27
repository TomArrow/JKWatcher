using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JKClient;
using Client = JKClient.JKClient;

namespace JKWatcher
{
    class Connection
    {

        public Client client;
        private ConnectedServerWindow serverWindow;

        public Connection(ConnectedServerWindow serverWindowA, string ip, ProtocolVersion protocol)
        {
            serverWindow = serverWindowA;
            createConnection(ip, protocol);
        }
        public Connection(ConnectedServerWindow serverWindowA, ServerInfo serverInfo)
        {
            serverWindow = serverWindowA;
            createConnection(serverInfo.Address.ToString(), serverInfo.Protocol);
        }

        ~Connection()
        {
            disconnect();
        }

        private async void createConnection( string ip, ProtocolVersion protocol)
        {
            client = new Client();
            client.Name = "Padawan";

            client.ServerCommandExecuted += ServerCommandExecuted;
            client.ServerInfoChanged += Connection_ServerInfoChanged;
            client.Start(ExceptionCallback);
            await client.Connect(ip, protocol);

            serverWindow.playerListDataGrid.ItemsSource = client.ClientInfo;


            serverWindow.addToLog("New connection created.");
        }


        // Update player list
        private void Connection_ServerInfoChanged(ServerInfo obj)
        {

            serverWindow.Dispatcher.Invoke(() => {

                serverWindow.playerListDataGrid.ItemsSource = client.ClientInfo;
            });
        }

        public void disconnect()
        {

            client.Disconnect();
            client.ServerCommandExecuted -= ServerCommandExecuted;
            client.ServerInfoChanged -= Connection_ServerInfoChanged;
            client.Stop();
            client.Dispose();
            serverWindow.addToLog("Disconnected.");
        }

        private Task ExceptionCallback(JKClientException exception)
        {
            serverWindow.addToLog(exception.ToString());
            Debug.WriteLine(exception);
            return null;
        }
        void ServerCommandExecuted(CommandEventArgs commandEventArgs)
        {
            StringBuilder allArgs = new StringBuilder();
            for (int i = 0; i < commandEventArgs.Command.Argc; i++)
            {
                allArgs.Append(commandEventArgs.Command.Argv(i));
                allArgs.Append(" ");
            }
            serverWindow.addToLog(allArgs.ToString());
            //addToLog(commandEventArgs.Command.Argv(0)+" "+ commandEventArgs.Command.Argv(1)+" "+ commandEventArgs.Command.Argv(2)+" "+ commandEventArgs.Command.Argv(3));
            Debug.WriteLine(commandEventArgs.Command.Argv(0));
        }

        public async void startDemoRecord()
        {
            serverWindow.addToLog("Initializing demo recording...");
            string timeString = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(timeString + "-" + client.ServerInfo.MapName, client.ServerInfo.Protocol);
            bool success = await client.Record_f(unusedDemoFilename);

            if (success)
            {

                serverWindow.addToLog("Demo recording started.");
            }
            else
            {

                serverWindow.addToLog("Demo recording failed to start for some reason. Already running?");
            }
        }
        public void stopDemoRecord()
        {
            serverWindow.addToLog("Stopping demo recording...");
            client.StopRecord_f();
            serverWindow.addToLog("Demo recording stopped.");
        }

    }
}
