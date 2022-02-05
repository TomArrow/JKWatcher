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
using System.Drawing;
using System.Threading;
using System.Collections.Concurrent;

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for ConnectedServerWindow.xaml
    /// </summary>
    public partial class ConnectedServerWindow : Window
    {

        private ServerInfo serverInfo;

        private FullyObservableCollection<Connection> connections = new FullyObservableCollection<Connection>();
        private FullyObservableCollection<CameraOperator> cameraOperators = new FullyObservableCollection<CameraOperator>();

        private const int maxLogLength = 10000;
        private string logString = "Begin of Log\n";

        private ServerSharedInformationPool infoPool;

        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();

        //public bool verboseOutput { get; private set; } = false;
        public int verboseOutput { get; private set; } = 0;

        // TODO: Send "score" command to server every second or 2 so we always have up to date scoreboards. will eat a bit more space maybe but should be cool. make it possible to disable this via some option, or to set interval

        public ConnectedServerWindow(ServerInfo serverInfoA)
        {
            serverInfo = serverInfoA;
            InitializeComponent();
            this.Title = serverInfo.Address.ToString() + " (" + serverInfo.HostName + ")";

            connectionsDataGrid.ItemsSource = connections;
            cameraOperatorsDataGrid.ItemsSource = cameraOperators;

            infoPool = new ServerSharedInformationPool();

            lock (connections)
            {
                connections.Add(new Connection(this, serverInfo, infoPool));
            }
            updateIndices();

            logTxt.Text = logString;

            this.Closed += ConnectedServerWindow_Closed;

            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task.Factory.StartNew(() => { miniMapUpdater(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default);
            backgroundTasks.Add(tokenSource);

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task.Factory.StartNew(() => { scoreBoardRequester(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default);
            backgroundTasks.Add(tokenSource);

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task.Factory.StartNew(() => { logStringUpdater(ct); }, ct, TaskCreationOptions.LongRunning,TaskScheduler.Default);
            backgroundTasks.Add(tokenSource);

        }

        private void updateIndices()
        {
            lock (connections)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    connections[i].Index = i;
                }
            }
            lock (cameraOperators)
            {
                for (int i = 0; i < cameraOperators.Count; i++)
                {
                    cameraOperators[i].Index = i;
                }
            }
        }

        ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();

        private void logStringUpdater(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(10);
                ct.ThrowIfCancellationRequested();

                string stringToAdd;
                
                while(logQueue.TryDequeue(out stringToAdd))
                {
                    lock (logString)
                    {
                        stringToAdd += "\n";
                        int newLogLength = logString.Length + stringToAdd.Length;
                        if (newLogLength <= maxLogLength)
                        {
                            logString += stringToAdd;
                        }
                        else
                        {
                            int cutAway = newLogLength - maxLogLength;
                            logString = logString.Substring(cutAway) + stringToAdd;
                        }
                    }
                }
                
                Dispatcher.Invoke(() => {
                    lock (logString)
                    {
                        logTxt.Text = logString;
                    }
                });
            }
            
        }

        public void addToLog(string someString)
        {
            logQueue.Enqueue(someString);
        }


        private unsafe void scoreBoardRequester(CancellationToken ct)
        {
            while (true)
            {
                //System.Threading.Thread.Sleep(1000); // wanted to do 1 every second but alas, it triggers rate limit that is 1 per second apparently, if i want to execute any other commands.
                System.Threading.Thread.Sleep(2000);
                ct.ThrowIfCancellationRequested();

                foreach(Connection connection in connections)
                {
                    if(connection.client.Status == ConnectionStatus.Active)
                    {
                        //connection.client.ExecuteCommand("score");
                        connection.leakyBucketRequester.requestExecution("score",RequestCategory.SCOREBOARD,0,2000,LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE);
                    }
                }
            }
        }

        const float miniMapOutdatedDrawTime = 1; // in seconds.

        private unsafe void miniMapUpdater(CancellationToken ct)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            while (true) {

                System.Threading.Thread.Sleep(100);
                ct.ThrowIfCancellationRequested();

                if (infoPool.playerInfo == null) continue;

                int imageWidth = (int)miniMapContainer.ActualWidth;
                int imageHeight = (int)miniMapContainer.ActualHeight;

                if (imageWidth < 5 || imageHeight < 5) continue; // avoid crashes and shit

                // We flip imageHeight and imageWidth because it's more efficient to work on rows than on columns. We later rotate the image into the proper position
                ByteImage miniMapImage = Helpers.BitmapToByteArray(new Bitmap(imageWidth, imageHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb));
                int stride = miniMapImage.stride;

                // Pass 1: Get bounds of all player entities
                /*ClientEntity[] entities = connections[0].client.Entities;
                if(entities == null)
                {
                    continue;
                }*/
                
                for(int i = 0; i < JKClient.Common.MaxClients(ProtocolVersion.Protocol15); i++)
                {
                    minX = Math.Min(minX, -infoPool.playerInfo[i].position.X);
                    maxX = Math.Max(maxX, -infoPool.playerInfo[i].position.X);
                    minY = Math.Min(minY, infoPool.playerInfo[i].position.Y);
                    maxY = Math.Max(maxY, infoPool.playerInfo[i].position.Y);
                }

                // Pass 2: Draw players as pixels
                float xRange = maxX - minX, yRange = maxY-minY;
                float x, y;
                int imageX, imageY;
                int byteOffset;
                for (int i = 0; i < JKClient.Common.MaxClients(ProtocolVersion.Protocol15); i++)
                {
                    if(infoPool.playerInfo[i].lastPositionUpdate == null)
                    {
                        continue; // don't have any position data
                    }
                    if ((DateTime.Now - infoPool.playerInfo[i].lastPositionUpdate.Value).TotalSeconds > miniMapOutdatedDrawTime)
                    {
                        continue; // data too old (probably bc out of sight)
                    }
                    if(infoPool.playerInfo[i].lastClientInfoUpdate == null)
                    {
                        continue; // don't have any client info.
                    }
                    x = -infoPool.playerInfo[i].position.X;
                    y = infoPool.playerInfo[i].position.Y;
                    imageX = Math.Clamp((int)( (x - minX) / xRange *(float)(imageWidth-1f)),0,imageWidth-1);
                    imageY = Math.Clamp((int)((y - minY) / yRange *(float)(imageHeight-1f)),0,imageHeight-1);
                    byteOffset = imageY * stride + imageX * 3;
                    if(infoPool.playerInfo[i].team == Team.Red)
                    {
                        byteOffset += 2; // red pixel. blue is just 0.
                    } else if (infoPool.playerInfo[i].team != Team.Blue)
                    {
                        byteOffset += 1; // Just make it green then, not sure what it is.
                    }

                    miniMapImage.imageData[byteOffset] = 255;
                }

                
                //statsImageBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                Dispatcher.Invoke(()=> {
                    Bitmap miniMapImageBitmap = Helpers.ByteArrayToBitmap(miniMapImage);
                    miniMap.Source = Helpers.BitmapToImageSource(miniMapImageBitmap);
                    miniMapImageBitmap.Dispose();
                });
            }
        }

        private void createCameraOperator<T>() where T:CameraOperator, new()
        {
            T camOperator = new T();
            int requiredConnectionCount = camOperator.getRequiredConnectionCount();
            Connection[] connectionsForCamOperator = getUnboundConnections(requiredConnectionCount);
            camOperator.provideConnections(connectionsForCamOperator);
            camOperator.provideServerSharedInformationPool(infoPool);
            camOperator.Initialize();
            lock (cameraOperators)
            {
                cameraOperators.Add(camOperator);
            }
            updateIndices();
        }

        private Connection[] getUnboundConnections(int count)
        {
            List<Connection> retVal = new List<Connection>();

            foreach(Connection connection in connections)
            {
                if(connection.CameraOperator == null)
                {
                    retVal.Add(connection);
                    if(retVal.Count == count)
                    {
                        break;
                    }
                }
            }

            while(retVal.Count < count)
            {
                Connection newConnection = new Connection(this,connections[0].client.ServerInfo,infoPool);
                lock (connections)
                {
                    connections.Add(newConnection);
                }
                updateIndices();
                retVal.Add(newConnection);
            }

            return retVal.ToArray();
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
            foreach(CancellationTokenSource backgroundTask in backgroundTasks)
            {
                backgroundTask.Cancel();
            }

            if (connections.Count == 0) return;

            foreach (Connection connection in connections)
            {
                connection.stopDemoRecord();
                connection.disconnect();
                break;
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
                    //commandLine.Text = "";
                    //connection.client.ExecuteCommand(command);
                    connection.leakyBucketRequester.requestExecution(command, RequestCategory.NONE,1,0,LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                    addToLog("Command \"" + command + "\" sent.");

                    break;
                }
            }
        }

        private void addCtfWatcherBtn_Click(object sender, RoutedEventArgs e)
        {
            createCameraOperator<CameraOperators.CTFCameraOperatorRedBlue>();
        }

        private void verbosityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int verbosity = 0;
            if( int.TryParse(((ComboBoxItem)verbosityComboBox.SelectedItem).Tag.ToString(), out verbosity))
            {
                verboseOutput = verbosity;
            }
        }
        /*
        private void verboseOutputCheck_Checked(object sender, RoutedEventArgs e)
        {
        verboseOutput = true;
        }

        private void verboseOutputCheck_Unchecked(object sender, RoutedEventArgs e)
        {

        verboseOutput = false;
        }*/
    }
}
