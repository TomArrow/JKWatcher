using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JKClient;
using Client = JKClient.JKClient;

namespace JKWatcher
{

    enum ConfigStringDefines
    {
        CS_MUSIC = 2,
        CS_MESSAGE = 3,     // from the map worldspawn's message field
        CS_MOTD = 4,        // g_motd string for server message of the day
        CS_WARMUP = 5,      // server time when the match will be restarted
        CS_SCORES1 = 6,
        CS_SCORES2 = 7,
        CS_VOTE_TIME = 8,
        CS_VOTE_STRING = 9,
        CS_VOTE_YES = 10,
        CS_VOTE_NO = 11,

        CS_TEAMVOTE_TIME = 12,
        CS_TEAMVOTE_STRING = 14,

        CS_TEAMVOTE_YES = 16,
        CS_TEAMVOTE_NO = 18,

        CS_GAME_VERSION = 20,
        CS_LEVEL_START_TIME = 21,       // so the timer only shows the current level
        CS_INTERMISSION = 22,       // when 1, fraglimit/timelimit has been hit and intermission will start in a second or two
        CS_FLAGSTATUS = 23,     // string indicating flag status in CTF
        CS_SHADERSTATE = 24,
        CS_BOTINFO = 25,

        CS_MVSDK = 26,      // CS for mvsdk specific configuration

        CS_ITEMS = 27,      // string of 0's and 1's that tell which items are present

        CS_CLIENT_JEDIMASTER = 28,      // current jedi master
        CS_CLIENT_DUELWINNER = 29,      // current duel round winner - needed for printing at top of scoreboard
        CS_CLIENT_DUELISTS = 30,        // client numbers for both current duelists. Needed for a number of client-side things.

        // these are also in be_aas_def.h - argh (rjr)
        CS_MODELS=32
    }

    enum PersistantEnum {
        PERS_SCORE,                     // !!! MUST NOT CHANGE, SERVER AND GAME BOTH REFERENCE !!!
        PERS_HITS,                      // total points damage inflicted so damage beeps can sound on change
        PERS_RANK,                      // player rank or team rank
        PERS_TEAM,                      // player team
        PERS_SPAWN_COUNT,               // incremented every respawn
        PERS_PLAYEREVENTS,              // 16 bits that can be flipped for events
        PERS_ATTACKER,                  // clientnum of last damage inflicter
        PERS_ATTACKEE_ARMOR,            // health/armor of last person we attacked
        PERS_KILLED,                    // count of the number of times you died
                                        // player awards tracking
        PERS_IMPRESSIVE_COUNT,          // two railgun hits in a row
        PERS_EXCELLENT_COUNT,           // two successive kills in a short amount of time
        PERS_DEFEND_COUNT,              // defend awards
        PERS_ASSIST_COUNT,              // assist awards
        PERS_GAUNTLET_FRAG_COUNT,       // kills with the guantlet
        PERS_CAPTURES                   // captures
    }

    public enum RequestCategory
    {
        NONE,
        SCOREBOARD,
        FOLLOW,
        INFOCOMMANDS
    }

    struct MvHttpDownloadInfo
    {
        public bool httpIsAvailable;
        public string urlPrefix;
    }

    public class SnapStatusInfo {
        const int averageWindow = 30; // Max samples
        const int averageWindowTime = 2000; // Max time window

        private int lastServerTime = 0;
        private int lastMessageNum = 0;
        private struct SnapStatusInfoSnippet
        {
            public int duration; // difference from last serverTime
            public int serverTime;
            public int snapNumIncrementSinceLast; // (skipped packets+1 basically)
        }

        private SnapStatusInfoSnippet[] infoSnippets = new SnapStatusInfoSnippet[averageWindow];
        private int index = 0;

        public void addDataPoint(int messageNum, int serverTime)
        {
            infoSnippets[index].duration = serverTime - lastServerTime;
            infoSnippets[index].snapNumIncrementSinceLast = messageNum - lastMessageNum;
            infoSnippets[index].serverTime = serverTime;
            lastServerTime = serverTime;
            lastMessageNum = messageNum;
            index = (index + 1) % averageWindow;
        }

        public override string ToString()
        {
            int timeTotal = 0;
            int packetCount = 0;
            int packetCountIncludingSkipped = 0;
            foreach(SnapStatusInfoSnippet snippet in infoSnippets)
            {
                if((lastServerTime- snippet.serverTime) < averageWindowTime)
                {
                    packetCount++;
                    packetCountIncludingSkipped += snippet.snapNumIncrementSinceLast;
                    timeTotal += snippet.duration;
                }
            }
            int receivedSnaps = timeTotal == 0 ? 0 : 1000 * packetCount / timeTotal;
            int totalSnaps = timeTotal == 0 ? 0 : 1000 * packetCountIncludingSkipped / timeTotal;
            return $"{receivedSnaps}/{totalSnaps}";
        }
    }

    /*
     * General notes regarding flood protection.
     * Old style servers only allow 1 command per second period. If you send a command within a second of another command,
     * it's game over. Newer ones apparently allow bursts of 3 commands, and then the 1 second limit is enforced again.
     * This tool more or less hopes for the latter. It would be hard to control command timing to the degree necessary
     * to avoid ever having less than 1 second delay between two commands.
     * 
     * We try to generally stay within the limits though naturally. Experience will show how well it will work.
     * 
     */
    public class Connection : INotifyPropertyChanged
    {
        // Setting it a bit higher than in the jk2 code itself, just to be safe. Internet delays etc. could cause issues.
        // Still not absolutely foolproof I guess but about as good as I can do.
        const int floodProtectPeriod = 1100; 

        public Client client;
        private ConnectedServerWindow serverWindow;

        public event PropertyChangedEventHandler PropertyChanged;

        public event UserCommandGeneratedEventHandler ClientUserCommandGenerated;
        internal void OnClientUserCommandGenerated(ref UserCommand cmd)
        {
            this.ClientUserCommandGenerated?.Invoke(this, ref cmd);
        }

        public JKClient.Statistics clientStatistics { get; private set; }
        
        // To detect changes.
        private string lastKnownPakNames = "";
        private string lastKnownPakChecksums = "";

        public int? ClientNum { get; set; } = null;
        public int? SpectatedPlayer { get; set; } = null;
        public PlayerMoveType? PlayerMoveType { get; set; } = null;
        public int? Index { get; set; } = null;
        public int? CameraOperator { get; set; } = null;

        private bool trulyDisconnected = true; // If we disconnected manually we want to stay disconnected.

        public SnapStatusInfo SnapStatus { get; private set; } = new SnapStatusInfo();

        public bool AlwaysFollowSomeone { get; set; } = true;

        //public ConnectionStatus Status => client != null ? client.Status : ConnectionStatus.Disconnected;
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        private ServerSharedInformationPool infoPool;

        public string GameTime { get; set; } = null;

        public bool isRecordingADemo { get; private set; } = false;

        public LeakyBucketRequester<string, RequestCategory> leakyBucketRequester = null;

        MvHttpDownloadInfo? mvHttpDownloadInfo = null;


        private List<CancellationTokenSource> backgroundTasks = new List<CancellationTokenSource>();


        /*public Connection(ConnectedServerWindow serverWindowA, string ip, ProtocolVersion protocol, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            _ = createConnection(ip, protocol);
        }
        public Connection(ServerInfo serverInfo, ConnectedServerWindow serverWindowA,ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            leakyBucketRequester = new LeakyBucketRequester<string, RequestCategory>(3, floodProtectPeriod); // Assuming default sv_floodcontrol 3, but will be adjusted once known
            leakyBucketRequester.CommandExecuting += LeakyBucketRequester_CommandExecuting; ;
            _ = createConnection(serverInfo.Address.ToString(), serverInfo.Protocol);
            createPeriodicReconnecter();
        }*/

        private string password = null;
        private string userInfoName = null;
        private bool demoTimeNameColors = false;
        private bool attachClientNumToName = false;

        SnapsSettings snapsSettings = null;

        public Connection( NetAddress addressA, ProtocolVersion protocolA, ConnectedServerWindow serverWindowA, ServerSharedInformationPool infoPoolA, string passwordA = null, string userInfoNameA = null, bool dateTimeColorNamesA = false, bool attachClientNumToNameA = false, SnapsSettings snapsSettingsA = null)
        {
            snapsSettings = snapsSettingsA;
            demoTimeNameColors = dateTimeColorNamesA;
            attachClientNumToName = attachClientNumToNameA;
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            userInfoName = userInfoNameA;
            password = passwordA;
            leakyBucketRequester = new LeakyBucketRequester<string, RequestCategory>(3, floodProtectPeriod); // Assuming default sv_floodcontrol 3, but will be adjusted once known
            leakyBucketRequester.CommandExecuting += LeakyBucketRequester_CommandExecuting; ;
            _ = createConnection(addressA.ToString(), protocolA);
            createPeriodicReconnecter();
        }

        public void SetPassword(string passwordA)
        {
            password = passwordA;
            if(client != null)
            {
                client.Password = password != null ? password : "";
            }
        }

        public void SetUserInfoName(string userInfoNameA)
        {
            userInfoName = userInfoNameA;
            updateName();
        }

        public void SetDemoTimeNameColors(bool doColor)
        {
            demoTimeNameColors = doColor;
            updateName();
        }
        public void SetClientNumNameAttach(bool doAttach)
        {
            attachClientNumToName = doAttach;
            updateName();
        }

        private void updateName()
        {
            if (client != null)
            {
                string nameToUse = userInfoName != null ? userInfoName : "Padawan";
                if (demoTimeNameColors && client.Demorecording && !nameToUse.Contains("^"))
                {
                    DemoName_t demoName = client.getDemoName();
                    if(demoName != null) // Pointless I guess, hmm
                    {
                        DateTime demoStartTime = demoName.time;
                        string colorCodes = Convert.ToString(((DateTimeOffset)demoStartTime.ToUniversalTime()).ToUnixTimeSeconds(), 8);
                        while(colorCodes.Length < 12)
                        {
                            colorCodes = "0" + colorCodes;
                        }
                        if(colorCodes.Length > 12)
                        {
                            serverWindow.addToLog("Datetime Colorcode for name is more than 12 letters! Weird.", true);
                        }
                        else
                        {
                            StringBuilder tmpName = new StringBuilder();
                            while (nameToUse.Length < 6)
                            {
                                nameToUse += ".";
                            }
                            for(int i = 0; i < 6; i++)
                            {
                                tmpName.Append("^");
                                tmpName.Append(colorCodes[i*2]);
                                tmpName.Append("^");
                                tmpName.Append(colorCodes[i*2+1]);
                                tmpName.Append("^");
                                tmpName.Append(colorCodes[i*2]);
                                tmpName.Append(nameToUse[i]);
                            }
                            if(nameToUse.Length > 6)
                            {
                                tmpName.Append(nameToUse.Substring(6));
                            }
                            nameToUse = tmpName.ToString();
                        }
                    }
                }
                if (attachClientNumToName)
                {
                    int clientNum = client.clientNum;
                    nameToUse += $" ^7(^2{clientNum}^7)";
                }
                client.Name = nameToUse;
            }
        }

        private void createPeriodicReconnecter()
        {
            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;
            Task.Factory.StartNew(() => { periodicReconnecter(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
               serverWindow.addToLog(t.Exception.ToString(), true);
            }, TaskContinuationOptions.OnlyOnFaulted);
            backgroundTasks.Add(tokenSource);
        }

        private void periodicReconnecter(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(5*60*1000); // 5 minutes
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;

                if (client.Status != ConnectionStatus.Active && !trulyDisconnected)
                {
                    Reconnect();
                }
                if (client.Status == ConnectionStatus.Active && shouldBeRecordingADemo && !isRecordingADemo)
                {
                    serverWindow.addToLog("periodicReconnecter: Attempting to start/resume demo recording. (shouldBeRecordingADemo = true)");
                    startDemoRecord();
                }
            }
        }

        private void LeakyBucketRequester_CommandExecuting(object sender, LeakyBucketRequester<string, RequestCategory>.CommandExecutingEventArgs e)
        {
            if (client.Status == ConnectionStatus.Active) // safety check
            {
                int unacked = client.GetUnacknowledgedReliableCommandCount();
                if(unacked < 5)
                {

                    client.ExecuteCommand(e.Command);
                }
                else
                {
                    // If there is more than 5 unacked commands, let's just chill.
                    // This may happen due to bad connections.
                    // Benefit is, this way we may send less overall if the connection is bad, because
                    // Leakybucketrequester overwrites former commands with later ones of the same type.
                    // It should also allow a lower delay when sending commands because otherwise the queue
                    // may fill up to a crazy degree.
                    e.Cancel = true;
                }
            } else
            {
                e.Cancel = true;
            }
        }

        ~Connection()
        {
            CloseDown();
        }

        bool closedDown = false;
        Mutex closeDownMutex = new Mutex();

        public void CloseDown()
        {
            lock (closeDownMutex)
            {
                if (closedDown) return;
                closedDown = true;
                foreach (CancellationTokenSource backgroundTask in backgroundTasks)
                {
                    backgroundTask.Cancel();
                }
                disconnect();
            }
        }

        private string ip;
        private ProtocolVersion protocol;

        private void afterConnect()
        {
            Status = client.Status;
            infoPool.MapName = client.ServerInfo.MapName;
            infoPool.teamInfo[(int)Team.Red].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
            infoPool.ScoreRed = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
            infoPool.teamInfo[(int)Team.Blue].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
            infoPool.ScoreBlue = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
        }

        private async Task<bool> createConnection( string ipA, ProtocolVersion protocolA,int timeOut = 30000)
        {
            if (closedDown) return false;

            trulyDisconnected = false;

            ip = ipA;
            protocol = protocolA;

            client = new Client(new JOClientHandler(ProtocolVersion.Protocol15,ClientVersion.JO_v1_02)); // Todo make more flexible
            //client.Name = "Padawan";
            client.Name = userInfoName == null ? "Padawan" : userInfoName;

            if(password != null)
            {
                client.Password = password;
            }

            client.ServerCommandExecuted += ServerCommandExecuted;
            client.ServerInfoChanged += Connection_ServerInfoChanged;
            client.SnapshotParsed += Client_SnapshotParsed;
            client.EntityEvent += Client_EntityEvent;
            client.Disconnected += Client_Disconnected;
            client.UserCommandGenerated += Client_UserCommandGenerated;
            client.DebugEventHappened += Client_DebugEventHappened;
            clientStatistics = client.Stats;
            Status = client.Status;
            
            client.Start(ExceptionCallback);
            Status = client.Status;

            try
            {

                //Task connectTask = client.Connect(ip, protocol);
                Task connectTask = client.Connect(ip);
                bool didConnect = false;
                await Task.Run(()=> {
                    try
                    {

                        didConnect = connectTask.Wait(timeOut);
                    } catch(TaskCanceledException e)
                    {
                        // Who cares.
                        didConnect = false;
                    }
                });
                if (!didConnect)
                {
                    Status = client.Status;
                    serverWindow.addToLog($"Failed to create connection. Timeout after {timeOut} milliseconds. May still connect who knows.", true);
                    connectTask.ContinueWith((a)=> {
                        Status = client.Status;
                        if (shouldBeRecordingADemo)
                        {
                            serverWindow.addToLog("createConnection: Attempting to start/resume demo recording after delayed connect. (shouldBeRecordingADemo = true)");
                            startDemoRecord();
                        }
                        afterConnect();
                    },TaskContinuationOptions.NotOnCanceled);
                    return false;
                }

            } catch(Exception e)
            {
                Status = client.Status;
                serverWindow.addToLog("Failed to create connection: "+e.ToString(),true);
                return false;
            }
            Status = client.Status;
            if (shouldBeRecordingADemo)
            {
                serverWindow.addToLog("createConnection: Attempting to start/resume demo recording. (shouldBeRecordingADemo = true)");
                startDemoRecord();
            }
            afterConnect();

            serverWindow.addToLog("New connection created.");
            return true;
        }

        private void Client_DebugEventHappened(object sender, object e)
        {
            if(e is ConfigStringMismatch)
            {
                ConfigStringMismatch info = (ConfigStringMismatch)e;
                serverWindow.addToLog($"DEBUG: Config string mismatch: \"{info.intendedString}\" became \"{info.actualString}\"",true);
                Task.Run(()=> {
                    MemoryStream ms = new MemoryStream();
                    ms.Write(Encoding.UTF8.GetBytes($"{info.intendedString}\n{info.actualString}\n"));
                    if(info.oldGsStringData != null)
                    {
                        ms.Write(info.oldGsStringData);
                        ms.Write(Encoding.UTF8.GetBytes($"\n"));
                    }
                    if(info.newGsStringData != null)
                    {
                        ms.Write(info.newGsStringData);
                        ms.Write(Encoding.UTF8.GetBytes($"\n"));
                    }
                    Helpers.logToSpecificDebugFile(ms.ToArray(),"configStringMismatch.data");
                });
            }
        }

        // We relay this so any potential watchers can latch on to this and do their own modifications if they want to.
        // It also means we don't have to have watchers subscribe directly to the client because then that would break
        // when we get disconnected/reconnected etc.
        private void Client_UserCommandGenerated(object sender, ref UserCommand modifiableCommand)
        {
            OnClientUserCommandGenerated(ref modifiableCommand);
        }

        int reconnectTriesCount = 0;
        const int reconnectMaxTries = 10;

        int DisconnectCallbackRecursion = 0;
        const int DisconnectCallbackRecursionLimit = 10;

        public async Task<bool> hardReconnect()
        {
            Status = client.Status;
            bool success = false;
            while (success == false)
            {
                int delay = 1000 + (int)(1000 * Math.Pow(2, reconnectTriesCount));
                System.Threading.Thread.Sleep(delay); // The more retries fail, the larger the delay between tries grows.
                serverWindow.addToLog($"Reconnect try {reconnectTriesCount+1}. Delay {delay} ms.");
                if (reconnectTriesCount >= reconnectMaxTries)
                {
                    serverWindow.addToLog($"Giving up on reconnect after {reconnectTriesCount} tries.", true);
                    break;
                }
                if (client.Status == ConnectionStatus.Active) // Don't try to reconnect if we somehow managed to already reconnect in some other way.
                {
                    break;
                }
                Status = client.Status;
                success = await Reconnect();
                Status = client.Status;
                if (!success)
                {
                    reconnectTriesCount++;
                } else
                {
                    reconnectTriesCount = 0;
                }
            }
            reconnectTriesCount = 0;
            Status = client.Status;
            return success;
        }

        public async Task<bool> Reconnect()
        {
            disconnect();
            return await createConnection(ip, protocol);
        }

        private async void Client_Disconnected(object sender, EventArgs e)
        {
            if (DisconnectCallbackRecursion++ > DisconnectCallbackRecursionLimit)
            {
                serverWindow.addToLog("[Client_Disconnected] Hit Disconnect recursion limit trying to restart the connection. Giving up.", true);
                return;
            }

            serverWindow.addToLog("Involuntary disconnect for some reason.", true);
            Status = client.Status;

            if (isRecordingADemo)
            {
                wasRecordingADemo = true;
            }
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord(true);
            }

            // Reconnect
            System.Threading.Thread.Sleep(1000);
            serverWindow.addToLog("Attempting to reconnect.");

            //client.Start(ExceptionCallback); // I think that's only necessary once?
            //Status = client.Status;

            /*await client.Connect(ip, protocol); // TODO This can get cancelled. In that case,  handle it somehow.
             */
            // Be safe and just reset everything
            if (await hardReconnect())
            {
                serverWindow.addToLog("Reconnected.");

                if (wasRecordingADemo || shouldBeRecordingADemo)
                {
                    serverWindow.addToLog("Attempting to resume demo recording.");
                    startDemoRecord();
                }
            }
            DisconnectCallbackRecursion--;

        }

        bool wasRecordingADemo = false;
        bool shouldBeRecordingADemo = false;

        // Client crashed for some reason
        private async Task ExceptionCallback(JKClientException exception)
        {
            if (DisconnectCallbackRecursion++ > DisconnectCallbackRecursionLimit)
            {
                serverWindow.addToLog("[ExceptionCallback] Hit Disconnect recursion limit trying to restart the connection. Giving up.", true);
                return;
            }
            serverWindow.addToLog("JKClient crashed: " + exception.ToString(),true);
            Debug.WriteLine(exception);

            if (isRecordingADemo)
            {
                wasRecordingADemo = true;
            }
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord(true);
            }

            if (closedDown) return;

            // Reconnect
            System.Threading.Thread.Sleep(1000);
            /*serverWindow.addToLog("Attempting to restart.");

            client.Start(ExceptionCallback); // I think that's only necessary once?
            Status = client.Status;

            serverWindow.addToLog("Attempting to reconnect.");
            await client.Connect(ip, protocol);
            Status = client.Status;*/

            serverWindow.addToLog("Attempting to reconnect.");

            // Be safe and just reset everything
            if (await hardReconnect())
            {
                serverWindow.addToLog("Reconnected.");

                if (wasRecordingADemo || shouldBeRecordingADemo)
                {
                    serverWindow.addToLog("Attempting to resume demo recording.");
                    startDemoRecord();
                }
            }
            DisconnectCallbackRecursion--;
        }

        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;

        int lastEventSnapshotNumber = 0;
        Dictionary<int, Vector3> thisSnapshotObituaryVictims = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> thisSnapshotObituaryAttackers = new Dictionary<int, Vector3>();
        private unsafe void Client_EntityEvent(object sender, EntityEventArgs e)
        {
            int snapshotNumber, serverTime;
            ((IJKClientImport)client).GetCurrentSnapshotNumber(out snapshotNumber, out serverTime);

            if(snapshotNumber != lastEventSnapshotNumber)
            {
                thisSnapshotObituaryVictims.Clear();
                thisSnapshotObituaryAttackers.Clear();
            }

            if (e.EventType == ClientGame.EntityEvent.Obituary)
            {
                // TODO Important. See if we can correlate death events to ctf frag events. That way we could know where
                //  the flag carrier was killed and thus where the flag is
                // We know the death event comes first. If we just pass the snapshotnumber, we can correlate them.
                // Todo do more elaborate logging. Death method etc. Detect multikills maybe
                int target = e.Entity.CurrentState.OtherEntityNum;
                int attacker = e.Entity.CurrentState.OtherEntityNum2;
                ClientEntity copyOfEntity = e.Entity; // This is necessary in order to read the fixed float arrays. Don't ask why, idk.
                Vector3 locationOfDeath;
                locationOfDeath.X = copyOfEntity.CurrentState.Position.Base[0];
                locationOfDeath.Y = copyOfEntity.CurrentState.Position.Base[1];
                locationOfDeath.Z = copyOfEntity.CurrentState.Position.Base[2];
                MeansOfDeath mod = (MeansOfDeath)e.Entity.CurrentState.EventParm;
                if (target < 0 || target >= client.ClientHandler.MaxClients)
                {
                    serverWindow.addToLog("EntityEvent Obituary: value "+target+" is out of bounds.");
                    return;
                }

                // Was it the flag carrier?
                foreach (int teamToCheck in Enum.GetValues(typeof(Team)))
                {
                    if (infoPool.teamInfo[teamToCheck].lastFlagCarrierUpdate != null && infoPool.teamInfo[teamToCheck].lastFlagCarrier == target && infoPool.teamInfo[teamToCheck].lastFlagCarrierValid)
                    {
                        infoPool.teamInfo[teamToCheck].flagDroppedPosition = locationOfDeath;
                        infoPool.teamInfo[teamToCheck].lastFlagDroppedPositionUpdate = DateTime.Now;
                        // Remmeber flag carrier deaths so we can keep the camera nearby for a bit longer if it was a relevant manner of death (everything that's not suicide)
                        if(attacker != target) // If it was suicide we don't care
                        {
                            if (attacker < 0 || attacker >= client.ClientHandler.MaxClients)
                            {
                                infoPool.teamInfo[teamToCheck].lastFlagCarrierWorldDeath = DateTime.Now; // This could be unintentional so still funny and interesting
                            }
                            else  // was a normal frag
                            {
                                infoPool.teamInfo[teamToCheck].lastFlagCarrierFragged = DateTime.Now;
                            }
                        }
                        
                            
                    }
                }

                thisSnapshotObituaryVictims.Add(target, locationOfDeath);

                infoPool.playerInfo[target].IsAlive = false;
                infoPool.playerInfo[target].lastDeathPosition = locationOfDeath;
                infoPool.playerInfo[target].lastDeath = DateTime.Now;
                infoPool.playerInfo[target].position = locationOfDeath;
                infoPool.playerInfo[target].lastPositionUpdate = DateTime.Now;
                string targetName = infoPool.playerInfo[target].name;

                string killString = null;
                bool generic = false;
                switch (mod)
                {
                    case MeansOfDeath.MOD_STUN_BATON:
                        killString = "stunned";
                        break;
                    case MeansOfDeath.MOD_MELEE:
                        killString = "beat down";
                        break;
                    case MeansOfDeath.MOD_SABER:
                        killString = "sabered";
                        break;
                    case MeansOfDeath.MOD_BRYAR_PISTOL:
                    case MeansOfDeath.MOD_BRYAR_PISTOL_ALT:
                    case MeansOfDeath.MOD_BLASTER:
                    case MeansOfDeath.MOD_BOWCASTER:
                    case MeansOfDeath.MOD_REPEATER:
                    case MeansOfDeath.MOD_REPEATER_ALT:
                    case MeansOfDeath.MOD_REPEATER_ALT_SPLASH:
                    case MeansOfDeath.MOD_DEMP2:
                    case MeansOfDeath.MOD_DEMP2_ALT:
                    case MeansOfDeath.MOD_FLECHETTE:
                    case MeansOfDeath.MOD_FLECHETTE_ALT_SPLASH:
                        killString = "shot";
                        generic = true;
                        break;
                    case MeansOfDeath.MOD_DISRUPTOR:
                    case MeansOfDeath.MOD_DISRUPTOR_SPLASH:
                    case MeansOfDeath.MOD_DISRUPTOR_SNIPER:
                        generic = true;
                        killString = "sniped";
                        break;
                    case MeansOfDeath.MOD_ROCKET:
                    case MeansOfDeath.MOD_ROCKET_SPLASH:
                    case MeansOfDeath.MOD_ROCKET_HOMING:
                    case MeansOfDeath.MOD_ROCKET_HOMING_SPLASH:
                        generic = true;
                        killString = "rocketed";
                        break;
                    case MeansOfDeath.MOD_THERMAL:
                    case MeansOfDeath.MOD_THERMAL_SPLASH:
                    case MeansOfDeath.MOD_DET_PACK_SPLASH:
                        generic = true;
                        killString = "detonated";
                        break;
                    case MeansOfDeath.MOD_TRIP_MINE_SPLASH:
                    case MeansOfDeath.MOD_TIMED_MINE_SPLASH:
                        generic = true;
                        killString = "tripped";
                        break;
                    case MeansOfDeath.MOD_FORCE_DARK:
                        killString = "annihilated";
                        break;
                    case MeansOfDeath.MOD_SENTRY:
                        killString = "sentry-killed";
                        break;
                    case MeansOfDeath.MOD_WATER:
                        killString = "drowned";
                        break;
                    case MeansOfDeath.MOD_SLIME:
                        killString = "slimed";
                        break;
                    case MeansOfDeath.MOD_LAVA:
                        killString = "lava-burned";
                        break;
                    case MeansOfDeath.MOD_CRUSH:
                        killString = "crushed";
                        break;
                    case MeansOfDeath.MOD_TELEFRAG:
                        killString = "admin-killed";
                        break;
                    case MeansOfDeath.MOD_FALLING:
                        killString = "doomed";
                        break;
                    case MeansOfDeath.MOD_SUICIDE:
                        killString = "anheroed";
                        break;
                    case MeansOfDeath.MOD_TARGET_LASER:
                        killString = "lasered";
                        break;
                    case MeansOfDeath.MOD_TRIGGER_HURT:
                        killString = "triggered";
                        break;
                    case MeansOfDeath.MOD_MAX:
                        break;
                    case MeansOfDeath.MOD_UNKNOWN:
                    default:
                        break;
                }

                if (attacker < 0 || attacker >= client.ClientHandler.MaxClients)
                {
                    serverWindow.addToLog(targetName + " was "+ (killString == null ? "killed" : killString) + (killString == null || generic ? " [" + mod.ToString() + "]" : ""));
                } else
                {
                    thisSnapshotObituaryAttackers.Add(attacker, locationOfDeath);
                    infoPool.playerInfo[attacker].position = locationOfDeath;
                    infoPool.playerInfo[attacker].lastPositionUpdate = DateTime.Now;
                    // Can we also set the setalive of the attacker here? he might have blown himself up too.
                    // Would his self blowup message come before or after this?
                    string attackerName = infoPool.playerInfo[attacker].name;
                    serverWindow.addToLog(attackerName + " "+(killString == null ? "killed" : killString)+" " +( (target==attacker)? "himself": targetName) + (killString == null || generic? " [" + mod.ToString() + "]" : ""));
                }
            } else if(e.EventType == ClientGame.EntityEvent.CtfMessage)
            {
                CtfMessageType messageType = (CtfMessageType)e.Entity.CurrentState.EventParm;
                int playerNum = e.Entity.CurrentState.TrickedEntityIndex;
                Team team = (Team)e.Entity.CurrentState.TrickedEntityIndex2;

                if (team != Team.Red && team != Team.Blue)
                {
                    // Some other team, weird.
                    return;
                }

                Team otherTeam = team == Team.Red ? Team.Blue : Team.Red;

                string teamAsString = "";
                string otherTeamAsString = "";
                switch (team)
                {
                    case Team.Blue:
                        teamAsString = "blue";
                        otherTeamAsString = "red";
                        break;
                    case Team.Red:
                        teamAsString = "red";
                        otherTeamAsString = "blue";
                        break;
                    default:break;
                }

                PlayerInfo? pi = null;

                if(playerNum >= 0 && playerNum <= client.ClientHandler.MaxClients)
                {
                    pi = infoPool.playerInfo[playerNum];
                }

                // If it was picked up or generally status changed, and it was at base before, remember this as the last time it was at base.
                if (infoPool.teamInfo[(int)team].flag == FlagStatus.FLAG_ATBASE)
                {
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                }

                if (messageType == CtfMessageType.PlayerGotFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].lastFlagCarrier = playerNum;
                    infoPool.teamInfo[(int)team].lastFlagCarrierUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_TAKEN;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    serverWindow.addToLog(pi.Value.name + " got the " + teamAsString + " flag.");

                } else if (messageType == CtfMessageType.FraggedFlagCarrier && pi != null)
                {
                    // Teams are inverted here because team is the team of the person who got killed
                    infoPool.teamInfo[(int)otherTeam].flag = FlagStatus.FLAG_DROPPED;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)otherTeam].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)otherTeam].lastFlagCarrierFragged = DateTime.Now;
                    if ((client.Entities?[playerNum].CurrentValid).GetValueOrDefault(false)) // Player who did kill is currently visible!
                    {
                        // We know where the flag is!
                        Vector3 locationOfFrag;
                        locationOfFrag.X = (client.Entities?[playerNum].CurrentState.Position.Base[0]).GetValueOrDefault(0.0f);
                        locationOfFrag.Y = (client.Entities?[playerNum].CurrentState.Position.Base[1]).GetValueOrDefault(0.0f);
                        locationOfFrag.Z = (client.Entities?[playerNum].CurrentState.Position.Base[2]).GetValueOrDefault(0.0f);
                        infoPool.teamInfo[(int)otherTeam].flagDroppedPosition = locationOfFrag;
                        infoPool.teamInfo[(int)otherTeam].lastFlagDroppedPositionUpdate = DateTime.Now;
                    } else if (thisSnapshotObituaryAttackers.ContainsKey(playerNum))
                    {
                        // We remember the death message. It had a position. We can use that. :)
                        infoPool.teamInfo[(int)otherTeam].flagDroppedPosition = thisSnapshotObituaryAttackers[playerNum];
                        infoPool.teamInfo[(int)otherTeam].lastFlagDroppedPositionUpdate = DateTime.Now;
                    } 
                    serverWindow.addToLog(pi.Value.name + " killed carrier of " + otherTeamAsString + " flag.");
                    
                } else if (messageType == CtfMessageType.FlagReturned)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(textInfo.ToTitleCase(teamAsString) + " flag was returned.");
                }
                else if (messageType == CtfMessageType.PlayerCapturedFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(pi.Value.name + " captured the "+teamAsString+" flag.");
                }
                else if (messageType == CtfMessageType.PlayerReturnedFlag && pi != null)
                {
                    infoPool.teamInfo[(int)team].flag = FlagStatus.FLAG_ATBASE;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValid = false;
                    infoPool.teamInfo[(int)team].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastFlagUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)team].lastTimeFlagWasSeenAtBase = DateTime.Now;
                    serverWindow.addToLog(pi.Value.name + " returned the " + teamAsString + " flag.");
                }

            }
            // Todo: look into various sound events that are broarcast to everyone, also global item pickup,
            // then we immediately know who's carrying the flag
        }

        private void snapsEnforcementUpdate()
        {

            if (snapsSettings != null)
            {
                if (snapsSettings.forceEmptySnaps && infoPool.NoActivePlayers)
                {
                    client.ClientForceSnaps = true;
                    client.DesiredSnaps = snapsSettings.emptySnaps;
                } else if (snapsSettings.forceBotOnlySnaps && infoPool.lastBotOnlyConfirmed != null && (DateTime.Now-infoPool.lastBotOnlyConfirmed.Value).TotalMilliseconds < 10000 )
                {
                    client.ClientForceSnaps = true;
                    client.DesiredSnaps = snapsSettings.botOnlySnaps;
                } else
                {
                    client.ClientForceSnaps = false;
                    client.DesiredSnaps = 1000;
                }
            }
            else
            {
                client.ClientForceSnaps = false;
                client.DesiredSnaps = 1000;
            }
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        private unsafe void Client_SnapshotParsed(object sender, EventArgs e)
        {

            snapsEnforcementUpdate();

            SnapStatus.addDataPoint(client.SnapNum,client.ServerTime);
            OnPropertyChanged(new PropertyChangedEventArgs("SnapStatus"));

            infoPool.setGameTime(client.gameTime);
            infoPool.isIntermission = client.IsInterMission;
            PlayerMoveType = client.PlayerMoveType;

            SpectatedPlayer = client.playerStateClientNum; // Might technically need a playerstate parsed event but ig this will do?

            ClientEntity[] entities = client.Entities;
            if (entities == null)
            {
                return;
            }
            for (int i = 0; i < client.ClientHandler.MaxClients; i++)
            {

                if (entities[i].CurrentValid || entities[i].CurrentFilledFromPlayerState ) { 

                    // TODO
                    // This isAlive thing sometimes evaluated wrongly in unpredictable ways. In one instance, it appears it might have 
                    // evaluated to false for a single frame, unless I mistraced the error and this isn't the source of the error at all.
                    // Weird thing is, EntityFlags was not being copied from PlayerState at all! So how come the value changed at all?! It doesn't really make sense.
                    infoPool.playerInfo[i].IsAlive = (entities[i].CurrentState.EntityFlags & (int)EntityFlags.EF_DEAD) == 0; // We do this so that if a player respawns but isn't visible, we don't use his (useless) position
                    infoPool.playerInfo[i].position.X = entities[i].CurrentState.Position.Base[0];
                    infoPool.playerInfo[i].position.Y = entities[i].CurrentState.Position.Base[1];
                    infoPool.playerInfo[i].position.Z = entities[i].CurrentState.Position.Base[2];
                    infoPool.playerInfo[i].velocity.X = entities[i].CurrentState.Position.Delta[0];
                    infoPool.playerInfo[i].velocity.Y = entities[i].CurrentState.Position.Delta[1];
                    infoPool.playerInfo[i].velocity.Z = entities[i].CurrentState.Position.Delta[2];
                    infoPool.playerInfo[i].angles.X = entities[i].CurrentState.AngularPosition.Base[0];
                    infoPool.playerInfo[i].angles.Y = entities[i].CurrentState.AngularPosition.Base[1];
                    infoPool.playerInfo[i].angles.Z = entities[i].CurrentState.AngularPosition.Base[2];
                    infoPool.playerInfo[i].powerUps = entities[i].CurrentState.Powerups; // 1/3 places where powerups is transmitted
                    infoPool.playerInfo[i].lastPositionUpdate = infoPool.playerInfo[i].lastFullPositionUpdate = DateTime.Now;
                    
                    if(((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                    } else if (((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)JKClient.Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                    }
                }
            }

            PlayerState currentPs = client.PlayerState;
            if(currentPs.ClientNum >= 0 && currentPs.ClientNum < client.ClientHandler.MaxClients) // Dunno why it shouldnt be but i dont want any crashes.
            {
                // Update the followed player's score in realtime.
                infoPool.playerInfo[currentPs.ClientNum].score.score = currentPs.Persistant[(int)PersistantEnum.PERS_SCORE];
            }

            // Save flag positions. 
            // Each flag (red & blue) has a base entity and a dropped entity (if dropped and not yet returned)
            // We want to save each for extra info. 
            // A currently picked up/carried/dropped flag's base entity is not sent to clients, but it still exists
            // as a separate game entity from the dropped flag. 
            // How can we tell if a flag entity is a dropped or a base flag?
            // The game entity has the FL_DROPPED_ITEM flag added, however this doesn't appear to be shared with the clients.
            // We can try and get some clues from other stuff though.
            // Dropped flags get an EF_BOUNCE_HALF eFlag (eFlag is different from flag, latter is server only). 
            // Dropped flags also have a pos.trType of TR_GRAVITY.
            // However pos.trType actually gets reset to TR_STATIONARY once the bouncing is over.
            // The bounce flag might however remain, I do not see it being deleted, so that's a way to go?
            // 
            // In theory we should be able to just check the flag item against the flag status (cs 23) but 
            // we might be (?) at a point in time where the new flag status has not yet been parsed, but the new 
            // entities have, so we might mistake a base flag for a dropped one or vice versa.
            for (int i = client.ClientHandler.MaxClients; i < JKClient.Common.MaxGEntities; i++)
            {
                if (entities[i].CurrentValid)
                { 
                    // Flag bases
                    if(entities[i].CurrentState.EntityType == (int)entityType_t.ET_TEAM)
                    {
                        Team team = (Team)entities[i].CurrentState.ModelIndex;
                        if (team == Team.Blue || team == Team.Red)
                        {
                            infoPool.teamInfo[(int)team].flagBasePosition.X = entities[i].CurrentState.Position.Base[0];
                            infoPool.teamInfo[(int)team].flagBasePosition.Y = entities[i].CurrentState.Position.Base[1];
                            infoPool.teamInfo[(int)team].flagBasePosition.Z = entities[i].CurrentState.Position.Base[2];
                            infoPool.teamInfo[(int)team].flagBaseEntityNumber = i;
                            infoPool.teamInfo[(int)team].lastFlagBasePositionUpdate = DateTime.Now;
                        }
                    } else if (entities[i].CurrentState.EntityType == (int)entityType_t.ET_ITEM)
                    {
                        if(entities[i].CurrentState.ModelIndex == infoPool.teamInfo[(int)Team.Red].flagItemNumber ||
                            entities[i].CurrentState.ModelIndex == infoPool.teamInfo[(int)Team.Blue].flagItemNumber
                            )
                        {

                            Team team = entities[i].CurrentState.ModelIndex == infoPool.teamInfo[(int)Team.Red].flagItemNumber ? Team.Red : Team.Blue;

                            // Check if it's base flag item or dropped one
                            if ((entities[i].CurrentState.EntityFlags & (int)EntityFlags.EF_BOUNCE_HALF) != 0)
                            {
                                // This very likely is a dropped flag, as dropped flags get the EF_BOUNCE_HALF entity flag.
                                infoPool.teamInfo[(int)team].flagDroppedPosition.X = entities[i].CurrentState.Position.Base[0];
                                infoPool.teamInfo[(int)team].flagDroppedPosition.Y = entities[i].CurrentState.Position.Base[1];
                                infoPool.teamInfo[(int)team].flagDroppedPosition.Z = entities[i].CurrentState.Position.Base[2];
                                infoPool.teamInfo[(int)team].droppedFlagEntityNumber = i;
                                infoPool.teamInfo[(int)team].lastFlagDroppedPositionUpdate = DateTime.Now;

                            } else
                            {
                                // This very likely is a base flag item, as it doesn't have an EF_BOUNCE_HALF entity flag.
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.X = entities[i].CurrentState.Position.Base[0];
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.Y = entities[i].CurrentState.Position.Base[1];
                                infoPool.teamInfo[(int)team].flagBaseItemPosition.Z = entities[i].CurrentState.Position.Base[2];
                                infoPool.teamInfo[(int)team].flagBaseItemEntityNumber = i;
                                infoPool.teamInfo[(int)team].lastFlagBaseItemPositionUpdate = DateTime.Now;
                                
                            }
                        }
                    }
                }
            }


            bool spectatedPlayerIsBot = SpectatedPlayer.HasValue && playerIsLikelyBot(SpectatedPlayer.Value);
            bool onlyBotsActive = infoPool.lastBotOnlyConfirmed.HasValue && (DateTime.Now - infoPool.lastBotOnlyConfirmed.Value).TotalMilliseconds < 10000;
            if (AlwaysFollowSomeone && (ClientNum == SpectatedPlayer || (!this.CameraOperator.HasValue && spectatedPlayerIsBot && !onlyBotsActive))) // Not following anyone. Let's follow someone.
            {
                int highestScore = int.MinValue;
                int highestScorePlayer = -1;
                // Pick player with highest score.
                foreach (PlayerInfo player in infoPool.playerInfo)
                {
                    if (player.infoValid && player.team != Team.Spectator && (player.score.score > highestScore || highestScorePlayer == -1) && (onlyBotsActive || !playerIsLikelyBot(player.clientNum)))
                    {
                        highestScore = player.score.score;
                        highestScorePlayer = player.clientNum;
                    }
                }
                if (highestScorePlayer != -1) // Assuming any players at all exist that are playing atm.
                {
                    leakyBucketRequester.requestExecution("follow " + highestScorePlayer, RequestCategory.FOLLOW, 1, 10000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                }
            }
        }

        private bool playerIsLikelyBot(int clientNumber)
        {
            return clientNumber >= 0 && clientNumber < client.ClientHandler.MaxClients && (!infoPool.playerInfo[clientNumber].score.lastNonZeroPing.HasValue || (DateTime.Now - infoPool.playerInfo[clientNumber].score.lastNonZeroPing.Value).TotalMilliseconds > 10000) && infoPool.playerInfo[clientNumber].score.pingUpdatesSinceLastNonZeroPing > 10;
        }


        // Update player list
        private void Connection_ServerInfoChanged(ServerInfo obj)
        {
            infoPool.lastBotOnlyConfirmed = null; // Because if a new player just entered, we have no idea if it's only a bot or  not until we get his ping via score command.
            string serverName = client.ServerInfo.HostName;
            if (serverName != "")
            {
                serverWindow.ServerName = obj.HostName;
            }

            // Check for referencedPaks
            InfoString systemInfo = new InfoString( client.GetMappedConfigstring(ClientGame.Configstring.SystemInfo));
            if(systemInfo["sv_referencedPakNames"] != lastKnownPakNames || systemInfo["sv_referencedPaks"] != lastKnownPakChecksums)
            {
                // Referenced paks changed:
                lastKnownPakNames = systemInfo["sv_referencedPakNames"];
                lastKnownPakChecksums = systemInfo["sv_referencedPaks"];

                string lastKnownPakNamesCaptured = lastKnownPakNames; // Capture for parallel thread
                string lastKnownPakChecksumsCaptured = lastKnownPakChecksums;
                if(mvHttpDownloadInfo == null || mvHttpDownloadInfo.Value.httpIsAvailable){
                    serverWindow.addToLog("Systeminfo: Referenced paks changed, trying to save to download list.");
                    Task.Run(async () => {
                        string[] pakNames = lastKnownPakNamesCaptured.Trim(' ').Split(" ");
                        string[] pakChecksums = lastKnownPakChecksumsCaptured.Trim(' ').Split(" ");
                        if(pakNames.Length != pakChecksums.Length)
                        {
                            serverWindow.addToLog("WARNING: Amount of pak names does not match amount of pak checksums. Weird. Aborting pak name logging this time.");
                            return;
                        } else if (pakNames.Length == 0)
                        {
                            serverWindow.addToLog("Referenced paks count is 0.");
                            return;
                        }

                        if(mvHttpDownloadInfo == null)
                        {
                            // Let's get server info packet.
                            using (ServerBrowser browser = new ServerBrowser(new JKClient.JOBrowserHandler(ProtocolVersion.Protocol15)))
                            {
                                browser.Start(async (JKClientException ex)=> {
                                    serverWindow.addToLog("Exception trying to get ServerInfo for mvHttp purposes: "+ex.ToString());
                                });
                                NetAddress serverAddress = NetAddress.FromString(ip);

                                InfoString infoString = null;
                                try { 
                                    infoString = await browser.GetServerInfoInfo(serverAddress);
                                }
                                catch (Exception e)
                                {
                                    serverWindow.addToLog("Exception trying to get ServerInfo for mvHttp purposes (during await): " + e.ToString());
                                    return;
                                }

                                MvHttpDownloadInfo tmpDLInfo = new MvHttpDownloadInfo();
                                tmpDLInfo.httpIsAvailable = false;
                                if (infoString.ContainsKey("mvhttp"))
                                {
                                    string serverAddressString = serverAddress.ToString();
                                    string[] serverAddressStringParts = serverAddressString.Split(":");
                                    if(serverAddressStringParts.Length > 0)
                                    {
                                        tmpDLInfo.httpIsAvailable = true;
                                        tmpDLInfo.urlPrefix = "http://" + serverAddressStringParts[0] + ":" + infoString["mvhttp"] + "/";
                                        serverWindow.addToLog($"Http downloads possible via port: {tmpDLInfo.urlPrefix}");
                                    }
                                } else if(infoString.ContainsKey("mvhttpurl"))
                                {
                                    tmpDLInfo.httpIsAvailable = true;
                                    tmpDLInfo.urlPrefix = infoString["mvhttpurl"];
                                    serverWindow.addToLog($"Http downloads possible via possibly external url: {tmpDLInfo.urlPrefix}");
                                } else
                                {
                                    tmpDLInfo.httpIsAvailable = false; 
                                    serverWindow.addToLog("Http downloads not available.");
                                }

                                mvHttpDownloadInfo = tmpDLInfo;

                                browser.Stop();
                            }
                        }

                        if(mvHttpDownloadInfo != null && mvHttpDownloadInfo.Value.httpIsAvailable)
                        {

                            List<string> downloadLinks = new List<string>();
                            for(int pkI = 0; pkI<pakNames.Length; pkI++)
                            {
                                string pakName = pakNames[pkI];
                                string pakChecksum = pakChecksums[pkI];
                                int pakChecksumInt;
                                if (int.TryParse(pakChecksum, out pakChecksumInt))
                                {
                                    string dlLink = mvHttpDownloadInfo.Value.urlPrefix + pakName + ".pk3";
                                    string hashString = Convert.ToHexString(BitConverter.GetBytes(pakChecksumInt));
                                    serverWindow.addToLog($"Logged pk3 download url: {dlLink}");
                                    downloadLinks.Add($"{pakName},{hashString},{dlLink}");
                                    PakDownloader.Enqueue(dlLink, pakChecksumInt);
                                } else
                                {
                                    serverWindow.addToLog("Could not parse checksum integer, strange. Discarding.");
                                }
                            }
                            string[] downloadLinksArray = downloadLinks.ToArray();
                            Dispatcher.CurrentDispatcher.Invoke(()=> {
                                Helpers.logDownloadLinks(downloadLinksArray);
                            });
                        }


                    });
                } else
                {
                    serverWindow.addToLog("Systeminfo: Referenced paks changed, but http downloads are disabled.");
                }
            }

            ClientNum = client.clientNum;
            SpectatedPlayer = client.playerStateClientNum;

            infoPool.MapName = client.ServerInfo.MapName;
            infoPool.teamInfo[(int)Team.Red].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
            infoPool.ScoreRed = client.GetMappedConfigstring(ClientGame.Configstring.Scores1).Atoi();
            infoPool.teamInfo[(int)Team.Blue].teamScore = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();
            infoPool.ScoreBlue = client.GetMappedConfigstring(ClientGame.Configstring.Scores2).Atoi();

            if (obj.FloodProtect >=-1)
            {
                // Don't trust a setting of 0. Could still have non-engine limiting that isn't captured by the sv_floodprotect cvar.
                // In short, assume the worst: only 1 per second.
                int burst = obj.FloodProtect > 0 ? obj.FloodProtect : 1; // 0 means flood protection is disabled. Let's still try to be somewhat gracious and just set burst to 10
                leakyBucketRequester.changeParameters(burst, floodProtectPeriod);
            } else if (obj.FloodProtect == -2)
            {
                // This server has not sent an sv_floodprotect variable. Might be a legacy server without the leaky bucket algo
                // Be safe and set burst to 1, or risk losing commands
                leakyBucketRequester.changeParameters(1, floodProtectPeriod);
            }

            if(client.ClientInfo == null)
            {
                return;
            }
            bool noActivePlayers = true;
            for(int i = 0; i < client.ClientHandler.MaxClients; i++)
            {
                if(client.ClientInfo[i].Team != Team.Spectator && client.ClientInfo[i].InfoValid)
                {
                    noActivePlayers = false;
                }
                infoPool.playerInfo[i].name = client.ClientInfo[i].Name;
                infoPool.playerInfo[i].team = client.ClientInfo[i].Team;
                infoPool.playerInfo[i].infoValid = client.ClientInfo[i].InfoValid;
                infoPool.playerInfo[i].clientNum = client.ClientInfo[i].ClientNum;

                infoPool.playerInfo[i].lastClientInfoUpdate = DateTime.Now;
            }
            infoPool.NoActivePlayers = noActivePlayers;
            serverWindow.Dispatcher.Invoke(() => {

                serverWindow.playerListDataGrid.ItemsSource = null;
                serverWindow.playerListDataGrid.ItemsSource = infoPool.playerInfo;
            });


            if (AlwaysFollowSomeone && ClientNum == SpectatedPlayer) // Not following anyone. Let's follow someone.
            {
                int highestScore = int.MinValue;
                int highestScorePlayer = -1;
                // Pick player with highest score.
                foreach(PlayerInfo player in infoPool.playerInfo)
                {
                    if(player.infoValid && player.team != Team.Spectator && (player.score.score > highestScore || highestScorePlayer == -1))
                    {
                        highestScore = player.score.score;
                        highestScorePlayer = player.clientNum;
                    }
                }
                if(highestScorePlayer != -1) // Assuming any players at all exist that are playing atm.
                {
                    leakyBucketRequester.requestExecution("follow " + highestScorePlayer, RequestCategory.FOLLOW, 1, 10000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS);
                }
            }

            snapsEnforcementUpdate();
        }

        public void disconnect()
        {
            // In very very rare cases (some bug?) a weird disconnect can happen
            // And it thinks demo is still recording or sth? So just be clean.
            client.StopRecord_f();
            updateName();
            isRecordingADemo = false;

            client.Disconnected -= Client_Disconnected; // This only handles involuntary disconnects
            Client oldClientForHandler = client; // Since maybe we reconnect straight after this.
            client.Disconnected += (a,b)=> { // Replace handler that auto-reconnects with handler that disposes of client.
                oldClientForHandler.Stop();
                oldClientForHandler.Dispose();
                oldClientForHandler.StopRecord_f();
                try // I'm putting in a try because maybe at this point we are destroying the Connection object so it might not exist anymore later and lead to errors?
                {
                    updateName();
                } catch(Exception e)
                {
                    try
                    {
                        serverWindow.addToLog("Error updating name after disconnect, coulda seen it coming I guess. "+e.ToString(),true);
                    } catch(Exception e2)
                    {
                        // Eh whatever.
                    }
                }
                serverWindow.addToLog("Disconnected.");
            };
            client.Disconnect();
            trulyDisconnected = true;
            client.ServerCommandExecuted -= ServerCommandExecuted;
            client.ServerInfoChanged -= Connection_ServerInfoChanged;
            client.SnapshotParsed -= Client_SnapshotParsed;
            client.EntityEvent -= Client_EntityEvent;
            client.UserCommandGenerated -= Client_UserCommandGenerated;
            client.DebugEventHappened -= Client_DebugEventHappened;
            clientStatistics = null;
        }

        

        List<string> serverCommandsVerbosityLevel0WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect" };
        List<string> serverCommandsVerbosityLevel2WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect","cs" };
        List<string> serverCommandsVerbosityLevel4BlackList = new List<string>() {"scores","tinfo" };

        void ServerCommandExecuted(CommandEventArgs commandEventArgs)
        {
            string command = commandEventArgs.Command.Argv(0);

            switch (command)
            {
                case "tinfo":
                    EvaluateTInfo(commandEventArgs);
                    break;
                case "scores":
                    EvaluateScore(commandEventArgs);
                    break;
                case "cs":
                    EvaluateCS(commandEventArgs);
                    break;
                case "print":
                    EvaluatePrint(commandEventArgs);
                    break;
                case "chat":
                case "tchat":
                    EvaluateChat(commandEventArgs);
                    break;
                default:
                    break;
            }

            if (serverWindow.verboseOutput ==5 || 
                (serverWindow.verboseOutput >=4 && !serverCommandsVerbosityLevel4BlackList.Contains(commandEventArgs.Command.Argv(0))) || 
                (serverWindow.verboseOutput >=2 && serverCommandsVerbosityLevel2WhiteList.Contains(commandEventArgs.Command.Argv(0))) || 
                (serverWindow.verboseOutput ==0 && serverCommandsVerbosityLevel0WhiteList.Contains(commandEventArgs.Command.Argv(0)))
                ) { 
                StringBuilder allArgs = new StringBuilder();
                for (int i = 0; i < commandEventArgs.Command.Argc; i++)
                {
                    allArgs.Append(commandEventArgs.Command.Argv(i));
                    allArgs.Append(" ");
                }
                serverWindow.addToLog(allArgs.ToString());
            }
            //addToLog(commandEventArgs.Command.Argv(0)+" "+ commandEventArgs.Command.Argv(1)+" "+ commandEventArgs.Command.Argv(2)+" "+ commandEventArgs.Command.Argv(3));
            Debug.WriteLine(commandEventArgs.Command.Argv(0));
        }

        void EvaluateCS(CommandEventArgs commandEventArgs)
        {
            int num = commandEventArgs.Command.Argv(1).Atoi();

            switch (num)
            {
                case (int)ConfigStringDefines.CS_FLAGSTATUS:
                    if (client.ServerInfo.GameType == GameType.CTF || client.ServerInfo.GameType == GameType.CTY)
                    {
                        string str = commandEventArgs.Command.Argv(2);
                        // format is rb where its red/blue, 0 is at base, 1 is taken, 2 is dropped
                        if(str.Length < 2)
                        {
                            // This happens sometimes, for example on NWH servers between 2 games
                            // Server will send cs 23 0 and cs 23 00 in succession, dunno why.
                            // The first one with the single zero is the obvious problem.
                            serverWindow.addToLog("Configstring weirdness, cs 23 had parameter "+str+"(Length "+str.Length+")");
                            if(str.Length == 1 && str == "0")
                            {
                                infoPool.teamInfo[(int)Team.Red].flag = 0;
                                infoPool.teamInfo[(int)Team.Red].lastFlagUpdate = DateTime.Now;
                                infoPool.teamInfo[(int)Team.Blue].flag = 0;
                                infoPool.teamInfo[(int)Team.Blue].lastFlagUpdate = DateTime.Now;
                            }
                        } else {
                            // If it was picked up or generally status changed, and it was at base before, remember this as the last time it was at base.
                            foreach (int teamToCheck in Enum.GetValues(typeof(Team))) { 
                                if (infoPool.teamInfo[teamToCheck].flag == FlagStatus.FLAG_ATBASE)
                                {
                                    infoPool.teamInfo[teamToCheck].lastTimeFlagWasSeenAtBase = DateTime.Now;
                                }
                            }
                            int tmp = int.Parse(str[0].ToString());
                            infoPool.teamInfo[(int)Team.Red].flag = tmp == 2 ? FlagStatus.FLAG_DROPPED : (FlagStatus)tmp;
                            infoPool.teamInfo[(int)Team.Red].lastFlagUpdate = DateTime.Now;
                            tmp = int.Parse(str[1].ToString());
                            infoPool.teamInfo[(int)Team.Blue].flag = tmp == 2 ? FlagStatus.FLAG_DROPPED : (FlagStatus)tmp;
                            infoPool.teamInfo[(int)Team.Blue].lastFlagUpdate = DateTime.Now;
                        }

                        // Reasoning: If a flag is dropped/in base, and then it is taken and our cam operator knows this, but the current flag carrier isn't updated *yet*, we will otherwise assume the wrong flag carrier.
                        // So assume the lastflagcarrier info invalid until it is actually set again anew.
                        if(infoPool.teamInfo[(int)Team.Red].flag != FlagStatus.FLAG_TAKEN)
                        {
                            infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = false;
                            infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        }
                        if(infoPool.teamInfo[(int)Team.Blue].flag != FlagStatus.FLAG_TAKEN)
                        {
                            infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = false;
                            infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        }
                        /*infoPool.redFlag = str[0] - '0';
                        infoPool.blueFlag = str[1] - '0';
                        if (cgs.isCTFMod && cgs.CTF3ModeActive)
                            cgs.yellowflag = str[2] - '0';
                        else
                            cgs.yellowflag = 0;*/
                    }
                    break;
                default:break;
            }
        }

        public bool ConnectionLimitReached { get; private set; } = false;

        // Parse specs sent through NWH "specs" command.
        // [1] = clientNumSpectator
        // [2] = nameSpectator (with trailing whitespaces)
        // [3] = nameSpectated (with trailing whitespaces)
        // [4] = clientNumSpectated
        Regex specsRegex = new Regex(@"^(\d+)\^3\) \^7 (.*?)\^7   (.*?)\((\d+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        bool skipSanityCheck = false;

        void EvaluatePrint(CommandEventArgs commandEventArgs)
        {
            Match specMatch;
            if (commandEventArgs.Command.Argc >= 2)
            {
                if(commandEventArgs.Command.Argv(1) == "Connection limit reached.\n")
                {
                    ConnectionLimitReached = true;
                } else if ((specMatch = specsRegex.Match(commandEventArgs.Command.Argv(1))).Success)
                {
                    // Is this info about who is spectating who?
                    if (specMatch.Groups.Count < 5) return;

                    int spectatingPlayer = -1;
                    int spectatedPlayer = -1;
                    int.TryParse(specMatch.Groups[1].Value, out spectatingPlayer);
                    int.TryParse(specMatch.Groups[4].Value, out spectatedPlayer);

                    //if(spectatingPlayer != -1 && spectatedPlayer != -1)
                    if(spectatingPlayer >= 0 && spectatingPlayer < 32 && spectatedPlayer >= 0 && spectatedPlayer < 32)
                    {
                        // Do sanity check that names match
                        // The regex match has trailing spaces so we just take a substring
                        string spectatingActualName = infoPool.playerInfo[spectatingPlayer].name;
                        string spectatingRegexName = specMatch.Groups[2].Value;
                        string spectatedActualName = infoPool.playerInfo[spectatedPlayer].name;
                        string spectatedRegexName = specMatch.Groups[3].Value;

                        int spectatingStringMaxLength = Math.Min(spectatingActualName.Length, spectatingRegexName.Length);
                        int spectatedStringMaxLength = Math.Min(spectatedActualName.Length, spectatedRegexName.Length);

                        spectatingActualName = spectatingActualName.Substring(0, spectatingStringMaxLength);
                        spectatingRegexName = spectatingRegexName.Substring(0, spectatingStringMaxLength);
                        spectatedActualName = spectatedActualName.Substring(0, spectatedStringMaxLength);
                        spectatedRegexName = spectatedRegexName.Substring(0, spectatedStringMaxLength);

                        if (skipSanityCheck || (spectatingActualName == spectatingRegexName && spectatedActualName == spectatedRegexName && spectatingStringMaxLength > 0 && spectatedStringMaxLength > 0))
                        {
                            infoPool.playerInfo[spectatingPlayer].nwhSpectatedPlayer = spectatedPlayer;
                            infoPool.playerInfo[spectatingPlayer].nwhSpectatedPlayerLastUpdate = DateTime.Now;
                        }
                    }
                }
            }
        }
        
        struct ParsedChatMessage
        {
            public string playerName;
            public int playerNum;
            public ChatType type;
            public string message;
        }
        enum ChatType
        {
            NONE,
            PRIVATE,
            TEAM,
            PUBLIC
        }
        const string privateChatBegin = "[";
        const string teamChatBegin = "(";
        const string privateChatSeparator = "^7]: ^6";
        const string teamChatSeparator = "^7): ^5";
        const string publicChatSeparator = "^7: ^2";
        ParsedChatMessage? ParseChatMessage(string nameChatSegmentA)
        {
            string nameChatSegment = nameChatSegmentA;
            ChatType detectedChatType = ChatType.PUBLIC;
            if (nameChatSegment.Length < privateChatBegin.Length + 1)
            {
                return null; // Idk
            }

            string separator;
            if (nameChatSegment.Substring(0, privateChatBegin.Length) == privateChatBegin)
            {
                detectedChatType = ChatType.PRIVATE;
                nameChatSegment = nameChatSegment.Substring(privateChatBegin.Length);
                separator = privateChatSeparator;
            } else if (nameChatSegment.Substring(0, teamChatBegin.Length) == teamChatBegin)
            {
                detectedChatType = ChatType.TEAM;
                nameChatSegment = nameChatSegment.Substring(teamChatBegin.Length);
                separator = teamChatSeparator;
            }
            else
            {
                separator = publicChatSeparator;
            }

            int separatorLength = separator.Length;

            string[] nameChatSplits = nameChatSegment.Split(new string[] { separator }, StringSplitOptions.None);

            List<int> possiblePlayers = new List<int>();
            if (nameChatSplits.Length > 2)
            {
                // WTf. Someone is messing around and having a complete meme name or meme message consisting of the separator sequence.
                // Let's TRY find out who it is.
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < nameChatSplits.Length - 1; i++)
                {
                    if (i != 0)
                    {
                        sb.Append(separator);
                    }
                    sb.Append(nameChatSplits[i]);
                    string possibleName = sb.ToString();
                    foreach (PlayerInfo playerInfo in infoPool.playerInfo)
                    {
                        if (playerInfo.infoValid && playerInfo.name == possibleName)
                        {
                            possiblePlayers.Add(playerInfo.clientNum);
                        }
                    }
                }
            }
            else
            {
                foreach (PlayerInfo playerInfo in infoPool.playerInfo)
                {
                    if (playerInfo.infoValid && playerInfo.name == nameChatSplits[0])
                    {
                        possiblePlayers.Add(playerInfo.clientNum);
                    }
                }
            }
            if (possiblePlayers.Count == 0)
            {
                serverWindow.addToLog($"Could not identify sender of (t)chat message. Zero matches: {nameChatSegmentA}", true);
                return null;
            }
            else if (possiblePlayers.Count > 1)
            {
                serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. More than 1 match: {nameChatSegmentA}", true);
                return null;
            }

            int playerNum = possiblePlayers[0];
            string playerName = infoPool.playerInfo[playerNum].name;
            if (playerName == null)
            {
                serverWindow.addToLog($"Received message from player whose name in infoPool is now null, wtf.", true);
                return null;
            }

            return new ParsedChatMessage() { message = nameChatSegment.Substring(playerName.Length + separatorLength).Trim(), playerName = playerName, playerNum = playerNum, type = detectedChatType };
        }

        void EvaluateChat(CommandEventArgs commandEventArgs)
        {
            try { 
                if (client?.Demorecording != true) return; // Why mark demo times if we're not recording...
                if (commandEventArgs.Command.Argc >= 2)
                {

                    int? myClientNum = client?.clientNum;
                    if (!myClientNum.HasValue) return;
                    string nameChatSegment = commandEventArgs.Command.Argv(1);

                    ParsedChatMessage? pmMaybe = ParseChatMessage(nameChatSegment);

                    if (!pmMaybe.HasValue) return;

                    ParsedChatMessage pm = pmMaybe.Value;
                    
                    if (pm.playerNum == myClientNum || pm.message == null) return; // Message from myself. Ignore.

                    string[] messageBits = pm.message.ToLower().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                    if (messageBits.Length == 0 || messageBits.Length > 2) return;

                    //StringBuilder response = new StringBuilder();
                    bool markRequested = false;
                    bool helpRequested = false;
                    bool reframeRequested = false;
                    int markMinutes = 1;
                    switch (messageBits[0])
                    {
                        default:
                            if(pm.type == ChatType.PRIVATE)
                            {
                                leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"I don't understand your command. Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);

                                return;
                            }
                            break;
                        case "!help":
                        case "help":
                        case "demohelp":
                            if (pm.type == ChatType.PRIVATE) {
                                helpRequested = true;
                            }
                            break;
                        case "!demohelp":
                            helpRequested = true;
                            break;
                        case "!mark":
                        case "!markme":
                            if(messageBits[0] == "!markme")
                            {
                                reframeRequested = true;
                            }
                            markRequested = true;
                            if (messageBits.Length > 1)
                            {
                                markMinutes = Math.Max(1, messageBits[1].Atoi());
                            }
                            break;
                        case "mark":
                        case "markme":
                            if (messageBits[0] == "markme")
                            {
                                reframeRequested = true;
                            }
                            if (pm.type == ChatType.PRIVATE)
                            {
                                markRequested = true;
                            }
                            if (messageBits.Length == 2)
                            {
                                markMinutes = Math.Max(1, messageBits[1].Atoi());
                            }
                            break;
                    }

                    if (helpRequested)
                    {
                        serverWindow.addToLog($"help requested by \"{pm.playerName}\" (clientNum {pm.playerNum})\n");
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"Here is your help: Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                        return;
                    }

                    int demoTime = (client?.DemoCurrentTime).GetValueOrDefault(0);

                    string withReframe = reframeRequested ? "+reframe" : "";
                    if (markRequested)
                    {
                        ServerInfo thisServerInfo = client?.ServerInfo;
                        if(thisServerInfo == null)
                        {
                            return;
                        }
                        StringBuilder demoCutCommand = new StringBuilder();
                        DateTime now = DateTime.Now;
                        demoCutCommand.Append($"rem demo cut{withReframe} requested by \"{pm.playerName}\" (clientNum {pm.playerNum}) on {thisServerInfo.HostName} ({thisServerInfo.Address.ToString()}) at {now}, {markMinutes} minute(s) into the past\n");
                        demoCutCommand.Append("DemoCutter ");
                        string demoPath = client?.AbsoluteDemoName;
                        if(demoPath == null)
                        {
                            return;
                        }
                        string relativeDemoPath = Path.GetRelativePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher","demoCuts"), demoPath);
                        demoCutCommand.Append($"\"{relativeDemoPath}\" ");
                        string filename = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{pm.playerName}__{thisServerInfo.HostName}__{thisServerInfo.MapName}";
                        filename = Helpers.MakeValidFileName(filename);
                        filename = Helpers.DemoCuttersanitizeFilename(filename,false);
                        demoCutCommand.Append($"\"{filename}\" ");
                        demoCutCommand.Append(Math.Max(0, demoTime- markMinutes*60000).ToString());
                        demoCutCommand.Append(" ");
                        demoCutCommand.Append((demoTime + 60000).ToString());
                        demoCutCommand.Append("\n");

                        if (reframeRequested)
                        {
                            demoCutCommand.Append("DemoReframer ");
                            demoCutCommand.Append($"\"{filename}.dm_15\" ");
                            demoCutCommand.Append($"\"{filename}_reframed.dm_15\" ");
                            demoCutCommand.Append(pm.playerNum);
                            demoCutCommand.Append("\n");
                        }

                        Helpers.logRequestedDemoCut(new string[] { demoCutCommand.ToString() });
                    }
                    else
                    {
                        return;
                    }
                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"Time was marked for demo cut{withReframe}, {markMinutes} min into past. Current demo time is {demoTime}.\"",RequestCategory.NONE,0,0,LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE,null);
                    serverWindow.addToLog($"^1NOTE ({myClientNum}): demo cut{withReframe} requested by \"{pm.playerName}\" (clientNum {pm.playerNum}), {markMinutes} minute(s) into the past\n");
                    return;
                }
            }
            catch (Exception e)
            {
                serverWindow.addToLog("Error evaluating chat: " + e.ToString(), true);
            }
        }
        
        void EvaluateTInfo(CommandEventArgs commandEventArgs)
        {
            int i;
            int theClient;

            int numSortedTeamPlayers = commandEventArgs.Command.Argv(1).Atoi();

            for (i = 0; i < numSortedTeamPlayers; i++)
            {
                theClient = commandEventArgs.Command.Argv(i * 6 + 2).Atoi();

                //sortedTeamPlayers[i] = client;

                if(theClient < 0 || theClient >= 32)
                {
                    serverWindow.addToLog("TeamInfo client weird number "+theClient.ToString());
                } else { 

                    infoPool.playerInfo[theClient].location = commandEventArgs.Command.Argv(i * 6 + 3).Atoi();
                    infoPool.playerInfo[theClient].health = commandEventArgs.Command.Argv(i * 6 + 4).Atoi();
                    infoPool.playerInfo[theClient].armor = commandEventArgs.Command.Argv(i * 6 + 5).Atoi();
                    infoPool.playerInfo[theClient].curWeapon = commandEventArgs.Command.Argv(i * 6 + 6).Atoi();
                    infoPool.playerInfo[theClient].powerUps = commandEventArgs.Command.Argv(i * 6 + 7).Atoi(); // 2/3 places where powerups is transmitted
                    if (((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                    }
                    else if (((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0) && infoPool.playerInfo[i].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                    {
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrier = i;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = true;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                        infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                    }
                }
            }
        }

        void EvaluateScore(CommandEventArgs commandEventArgs)
        {
            int i, powerups, readScores;

            readScores = commandEventArgs.Command.Argv(1).Atoi();

            if (readScores > JKClient.Common.MaxClientScoreSend)
            {
                readScores = JKClient.Common.MaxClientScoreSend;
            }

            /*if (cg.numScores > JKClient.Common.MaxClients)
            {
                cg.numScores = MAX_CLIENTS;
            }*/
            infoPool.teamInfo[(int)Team.Red].teamScore = commandEventArgs.Command.Argv(2).Atoi();
            infoPool.ScoreRed = commandEventArgs.Command.Argv(2).Atoi();
            infoPool.teamInfo[(int)Team.Blue].teamScore = commandEventArgs.Command.Argv(3).Atoi();
            infoPool.ScoreBlue = commandEventArgs.Command.Argv(3).Atoi();

            bool anyNonBotPlayerActive = false;
            bool anyPlayersActive = false;
            for (i = 0; i < readScores; i++)
            {
                //
                int clientNum = commandEventArgs.Command.Argv(i * 14 + 4).Atoi();
                if (clientNum < 0 || clientNum >= client.ClientHandler.MaxClients)
                {
                    continue;
                }
                infoPool.playerInfo[clientNum].score.client = commandEventArgs.Command.Argv(i * 14 + 4).Atoi();
                infoPool.playerInfo[clientNum].score.score = commandEventArgs.Command.Argv(i * 14 + 5).Atoi();
                infoPool.playerInfo[clientNum].score.ping = commandEventArgs.Command.Argv(i * 14 + 6).Atoi();
                infoPool.playerInfo[clientNum].score.time = commandEventArgs.Command.Argv(i * 14 + 7).Atoi();
                infoPool.playerInfo[clientNum].score.scoreFlags = commandEventArgs.Command.Argv(i * 14 + 8).Atoi();
                powerups = commandEventArgs.Command.Argv(i * 14 + 9).Atoi();
                infoPool.playerInfo[clientNum].score.powerUps = powerups; // duplicated from entities?
                infoPool.playerInfo[clientNum].powerUps = powerups; // 3/3 places where powerups is transmitted
                infoPool.playerInfo[clientNum].score.accuracy = commandEventArgs.Command.Argv(i * 14 + 10).Atoi();
                infoPool.playerInfo[clientNum].score.impressiveCount = commandEventArgs.Command.Argv(i * 14 + 11).Atoi();
                infoPool.playerInfo[clientNum].score.excellentCount = commandEventArgs.Command.Argv(i * 14 + 12).Atoi();
                infoPool.playerInfo[clientNum].score.guantletCount = commandEventArgs.Command.Argv(i * 14 + 13).Atoi();
                infoPool.playerInfo[clientNum].score.defendCount = commandEventArgs.Command.Argv(i * 14 + 14).Atoi();
                infoPool.playerInfo[clientNum].score.assistCount = commandEventArgs.Command.Argv(i * 14 + 15).Atoi();
                infoPool.playerInfo[clientNum].score.perfect = commandEventArgs.Command.Argv(i * 14 + 16).Atoi() == 0 ? false : true;
                infoPool.playerInfo[clientNum].score.captures = commandEventArgs.Command.Argv(i * 14 + 17).Atoi();
                infoPool.playerInfo[clientNum].lastScoreUpdated = DateTime.Now;

                if(infoPool.playerInfo[clientNum].score.ping != 0)
                {
                    infoPool.playerInfo[clientNum].score.lastNonZeroPing = DateTime.Now;
                    infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing = 0;
                } else
                {
                    infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing++;
                }
                if (infoPool.playerInfo[clientNum].team != Team.Spectator)
                {
                    anyPlayersActive = true;
                    if (infoPool.playerInfo[clientNum].score.ping != 0 || infoPool.playerInfo[clientNum].score.pingUpdatesSinceLastNonZeroPing < 4) // Be more safe. Anyone could have ping 0 by freak accident in theory.
                    {
                        anyNonBotPlayerActive = true;
                    }
                }

                if (((infoPool.playerInfo[clientNum].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0) && infoPool.playerInfo[clientNum].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                {
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrier = clientNum;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)Team.Red].lastFlagCarrierUpdate = DateTime.Now;
                }
                else if (((infoPool.playerInfo[clientNum].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0) && infoPool.playerInfo[clientNum].team != Team.Spectator) // Sometimes stuff seems to glitch and show spectators as having the flag
                {
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrier = clientNum;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValid = true;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierValidUpdate = DateTime.Now;
                    infoPool.teamInfo[(int)Team.Blue].lastFlagCarrierUpdate = DateTime.Now;
                }

                // Serverside:
                //cl->ps.persistant[PERS_SCORE], // score
                //ping, // ping
                //(level.time - cl->pers.enterTime)/60000, // time
                //scoreFlags, // scoreflags
                //g_entities[level.sortedClients[i]].s.powerups, // powerups
                //accuracy,  // accuracy
                //cl->ps.persistant[PERS_IMPRESSIVE_COUNT], // impressive
                //cl->ps.persistant[PERS_EXCELLENT_COUNT], // excellent
                //cl->ps.persistant[PERS_GAUNTLET_FRAG_COUNT],  // gauntlet frag count
                //cl->ps.persistant[PERS_DEFEND_COUNT],  // defend count
                //cl->ps.persistant[PERS_ASSIST_COUNT],  // assist count 
                //perfect, // perfect count 
                //cl->ps.persistant[PERS_CAPTURES] // captures


            }
            if (!anyNonBotPlayerActive && anyPlayersActive)
            {
                infoPool.lastBotOnlyConfirmed = DateTime.Now;
            } else
            {
                infoPool.lastBotOnlyConfirmed = null;
            }
            snapsEnforcementUpdate();
        }

        int lastDemoIterator = 0;


        public async void startDemoRecord(int iterator=0)
        {
            shouldBeRecordingADemo = true;
            if(client.Status != ConnectionStatus.Active)
            {
                serverWindow.addToLog("Can't record demo when disconnected. But trying to queue recording in case we connect.");
                return;
            }
            if (isRecordingADemo)
            {
                if (!client.Demorecording)
                {

                    serverWindow.addToLog("isRecordingADemo indicates demo is already being recorded, but client says otherwise? Shouldn't really happen, some bug I guess. Try record anyway...");
                    //isRecordingADemo = false;
                } else
                {

                    serverWindow.addToLog("Demo is already being recorded...");
                    return;
                }
            }

            lastDemoIterator = iterator;

            serverWindow.addToLog("Initializing demo recording...");
            DateTime nowTime = DateTime.Now;
            string timeString = nowTime.ToString("yyyy-MM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(Helpers.MakeValidFileName(timeString + "-" + client.ServerInfo.MapName+"_"+client.ServerInfo.HostName+(iterator==0 ? "" : "_"+(iterator+1).ToString())), client.ServerInfo.Protocol);

            TaskCompletionSource<bool> firstPacketRecordedTCS = new TaskCompletionSource<bool>();

            _ = firstPacketRecordedTCS.Task.ContinueWith((Task<bool> s) =>
            {
                // Send a few commands that give interesting outputs, nothing more to it.

                // Some or most of these commands won't do anything on many servers.
                // On some servers they might display something
                // clientlist seems to have been a servercommand once, but its now a client command
                // In short, this stuff might not do anything except throw a wrong cmd error
                // But on some servers it might give mildly interesting output.

                // Need a timeout because of flood protection which is roughly speaking 1 command per second
                // We already do a scoreboard command every 2 seconds, so we have about 1 command every 2 seconds left
                // Go 3 seconds here to be safe. We also still need room to make commands for changing the camera angle.
                const int timeoutBetweenCommands = 3000;

                string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss G\\MTzzz");
                ServerInfo curServerInfo = client.ServerInfo;
                string[] serverInfoParts = new string[] { "Recording demo",
                    time,
                    curServerInfo.Address.ToString(),
                    curServerInfo.HostName,
                    curServerInfo.ServerGameVersionString,
                    curServerInfo.Location,
                    curServerInfo.Game,
                    curServerInfo.GameType.ToString(),
                    curServerInfo.GameName,
                    curServerInfo.MapName,
                    curServerInfo.Protocol.ToString(),
                    curServerInfo.Version.ToString(),
                    "sv_floodProtect: "+curServerInfo.FloodProtect.ToString(),
                    time,}; // Just for the occasional lost chat message, send time a second time. Most useful from these infos.
                serverInfoParts = Helpers.StringChunksOfMaxSize(serverInfoParts,140,", ^7^0^7", "^7^0^7"); // 150 is max message length. We split to 140 size chunks just to be safe
                foreach (string serverInfoPart in serverInfoParts)
                {
                    // Tell some info about the server... to myself
                    // Convenience feature.
                    leakyBucketRequester.requestExecution("tell " + client.clientNum + " \""+ serverInfoPart + "\"", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // NWH / CTFMod (?)
                leakyBucketRequester.requestExecution("info", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("afk", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("specs", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                if (client.ServerInfo.GameType == GameType.FFA && client.ServerInfo.NWH == false)
                { // replace with more sophisticated detection
                    // doing a detection here to not annoy ctf players.
                    // will still annoy ffa players until better detection.
                    // Show top 10 scores at start of demo recording.
                    leakyBucketRequester.requestExecution("say_team !top", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // TwiMod (DARK etc)
                leakyBucketRequester.requestExecution("ammodinfo", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("ammodinfo_twitch", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                if (client.ServerInfo.GameType == GameType.FFA && client.ServerInfo.NWH == false) // Might not be accurate idk
                {
                    leakyBucketRequester.requestExecution("say_team !dimensions", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                    leakyBucketRequester.requestExecution("say_team !where", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                }

                // Whatever
                leakyBucketRequester.requestExecution("serverstatus", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientinfo", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                leakyBucketRequester.requestExecution("clientlist", RequestCategory.INFOCOMMANDS, 0, timeoutBetweenCommands, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);

                /*client.ExecuteCommand("info");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("afk");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("clientstatus");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("specs");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);

                // OC Defrag
                if(client.ServerInfo.GameType == GameType.FFA) { // replace with more sophisticated detection
                    // doing a detection here to not annoy ctf players.
                    // will still annoy ffa players until better detection.
                    client.ExecuteCommand("say_team !top"); // Show top 10 scores at start of demo recording.
                    System.Threading.Thread.Sleep(timeoutBetweenCommands);
                }

                // TwiMod (DARK etc)
                client.ExecuteCommand("ammodinfo"); 
                System.Threading.Thread.Sleep(timeoutBetweenCommands);

                // Whatever
                client.ExecuteCommand("clientinfo");
                System.Threading.Thread.Sleep(timeoutBetweenCommands);
                client.ExecuteCommand("clientlist");*/
            });

            bool success = await client.Record_f(new DemoName_t {name= unusedDemoFilename,time=nowTime }, firstPacketRecordedTCS);

            if (success)
            {

                serverWindow.addToLog("Demo recording started.");
                isRecordingADemo = true;
                if (demoTimeNameColors)
                {
                    updateName();
                }
            }
            else
            {

                serverWindow.addToLog("Demo recording failed to start for some reason.");
                isRecordingADemo = false;
            }
        }
        public void stopDemoRecord(bool afterInvoluntaryDisconnect = false)
        {
            if (!afterInvoluntaryDisconnect)
            {
                shouldBeRecordingADemo = false;
            }
            serverWindow.addToLog("Stopping demo recording...");
            client.StopRecord_f();
            updateName();
            isRecordingADemo = false;
            serverWindow.addToLog("Demo recording stopped.");
        }

    }

    // means of death
    enum MeansOfDeath {
        MOD_UNKNOWN,
        MOD_STUN_BATON,
        MOD_MELEE,
        MOD_SABER,
        MOD_BRYAR_PISTOL,
        MOD_BRYAR_PISTOL_ALT,
        MOD_BLASTER,
        MOD_DISRUPTOR,
        MOD_DISRUPTOR_SPLASH,
        MOD_DISRUPTOR_SNIPER,
        MOD_BOWCASTER,
        MOD_REPEATER,
        MOD_REPEATER_ALT,
        MOD_REPEATER_ALT_SPLASH,
        MOD_DEMP2,
        MOD_DEMP2_ALT,
        MOD_FLECHETTE,
        MOD_FLECHETTE_ALT_SPLASH,
        MOD_ROCKET,
        MOD_ROCKET_SPLASH,
        MOD_ROCKET_HOMING,
        MOD_ROCKET_HOMING_SPLASH,
        MOD_THERMAL,
        MOD_THERMAL_SPLASH,
        MOD_TRIP_MINE_SPLASH,
        MOD_TIMED_MINE_SPLASH,
        MOD_DET_PACK_SPLASH,
        MOD_FORCE_DARK,
        MOD_SENTRY,
        MOD_WATER,
        MOD_SLIME,
        MOD_LAVA,
        MOD_CRUSH,
        MOD_TELEFRAG,
        MOD_FALLING,
        MOD_SUICIDE,
        MOD_TARGET_LASER,
        MOD_TRIGGER_HURT,
        MOD_MAX
    }
}
