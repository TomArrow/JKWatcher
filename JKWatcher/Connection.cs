using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    class Connection : INotifyPropertyChanged
    {

        public Client client;
        private ConnectedServerWindow serverWindow;

        public event PropertyChangedEventHandler PropertyChanged;

        public int? Index { get; set; } = null;
        public int? CameraOperator { get; set; } = null;
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        private ServerSharedInformationPool infoPool;

        public Connection(ConnectedServerWindow serverWindowA, string ip, ProtocolVersion protocol, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            createConnection(ip, protocol);
        }
        public Connection(ConnectedServerWindow serverWindowA, ServerInfo serverInfo, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
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
            client.SnapshotParsed += Client_SnapshotParsed;
            client.EntityEvent += Client_EntityEvent;
            Status = client.Status;
            
            client.Start(ExceptionCallback);
            Status = client.Status;

            await client.Connect(ip, protocol);
            Status = client.Status;

            serverWindow.addToLog("New connection created.");
        }

        private void Client_EntityEvent(object sender, EntityEventArgs e)
        {
            if (e.EventType == EntityEvent.Obituary)
            {
                int target = e.Entity.CurrentState.OtherEntityNum;
                int attacker = e.Entity.CurrentState.OtherEntityNum2;
                MeansOfDeath mod = (MeansOfDeath)e.Entity.CurrentState.EventParm;
                if (target < 0 || target >= JKClient.Common.MaxClients)
                {
                    serverWindow.addToLog("EntityEvent Obituary: value "+target+" is out of bounds.");
                }

                infoPool.playerInfo[target].lastDeath = DateTime.Now;
                string targetName = infoPool.playerInfo[target].name;
                if (attacker < 0 || attacker >= JKClient.Common.MaxClients)
                {
                    serverWindow.addToLog(targetName + " died");
                } else
                {
                    string attackerName = infoPool.playerInfo[attacker].name;
                    serverWindow.addToLog(attackerName + " killed " + targetName);
                }
            } else if(e.EventType == EntityEvent.CtfMessage)
            {
                CtfMessageType messageType = (CtfMessageType)e.Entity.CurrentState.EventParm;
                int playerNum = e.Entity.CurrentState.TrickedEntityIndex;
                Team team = (Team)e.Entity.CurrentState.TrickedEntityIndex2;

            }
        }

        private unsafe void Client_SnapshotParsed(object sender, EventArgs e)
        {
            ClientEntity[] entities = client.Entities;
            if (entities == null)
            {
                return;
            }
            for (int i = 0; i < JKClient.Common.MaxClients; i++)
            {

                if (entities[i].CurrentValid) { 
                    infoPool.playerInfo[i].position.X = entities[i].CurrentState.Position.Base[0];
                    infoPool.playerInfo[i].position.Y = entities[i].CurrentState.Position.Base[1];
                    infoPool.playerInfo[i].position.Z = entities[i].CurrentState.Position.Base[2];
                    infoPool.playerInfo[i].velocity.X = entities[i].CurrentState.Position.Delta[0];
                    infoPool.playerInfo[i].velocity.Y = entities[i].CurrentState.Position.Delta[1];
                    infoPool.playerInfo[i].velocity.Z = entities[i].CurrentState.Position.Delta[2];
                    infoPool.playerInfo[i].angles.X = entities[i].CurrentState.AngularPosition.Base[0];
                    infoPool.playerInfo[i].angles.Y = entities[i].CurrentState.AngularPosition.Base[1];
                    infoPool.playerInfo[i].angles.Z = entities[i].CurrentState.AngularPosition.Base[2];
                    infoPool.playerInfo[i].lastPositionUpdate = DateTime.Now;
                }
            }
        }


        // Update player list
        private void Connection_ServerInfoChanged(ServerInfo obj)
        {
            if(client.ClientInfo == null)
            {
                return;
            }
            for(int i = 0; i < JKClient.Common.MaxClients; i++)
            {
                infoPool.playerInfo[i].name = client.ClientInfo[i].Name;
                infoPool.playerInfo[i].team = client.ClientInfo[i].Team;
                infoPool.playerInfo[i].infoValid = client.ClientInfo[i].InfoValid;
                infoPool.playerInfo[i].clientNum = client.ClientInfo[i].ClientNum;

                infoPool.playerInfo[i].lastClientInfoUpdate = DateTime.Now;
            }
            serverWindow.Dispatcher.Invoke(() => {

                serverWindow.playerListDataGrid.ItemsSource = null;
                serverWindow.playerListDataGrid.ItemsSource = infoPool.playerInfo;
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

        List<string> serverCommandsNonVerboseOutputWhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect" };

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
                default:
                    break;
            }

            if (serverWindow.verboseOutput || serverCommandsNonVerboseOutputWhiteList.Contains(commandEventArgs.Command.Argv(0))) { 
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

        void EvaluateTInfo(CommandEventArgs commandEventArgs)
        {
            int i;
            int client;

            int numSortedTeamPlayers = commandEventArgs.Command.Argv(1).Atoi();

            for (i = 0; i < numSortedTeamPlayers; i++)
            {
                client = commandEventArgs.Command.Argv(i * 6 + 2).Atoi();

                //sortedTeamPlayers[i] = client;

                infoPool.playerInfo[client].location = commandEventArgs.Command.Argv(i * 6 + 3).Atoi();
                infoPool.playerInfo[client].health = commandEventArgs.Command.Argv(i * 6 + 4).Atoi();
                infoPool.playerInfo[client].armor = commandEventArgs.Command.Argv(i * 6 + 5).Atoi();
                infoPool.playerInfo[client].curWeapon = commandEventArgs.Command.Argv(i * 6 + 6).Atoi();
                infoPool.playerInfo[client].powerups = commandEventArgs.Command.Argv(i * 6 + 7).Atoi();
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
            infoPool.teamScores[0] = commandEventArgs.Command.Argv(2).Atoi();
            infoPool.teamScores[1] = commandEventArgs.Command.Argv(3).Atoi();

            for (i = 0; i < readScores; i++)
            {
                //
                int clientNum = commandEventArgs.Command.Argv(i * 14 + 4).Atoi();
                if (clientNum < 0 || clientNum >= JKClient.Common.MaxClients)
                {
                    continue;
                }
                infoPool.playerInfo[clientNum].score.client = commandEventArgs.Command.Argv(i * 14 + 4).Atoi();
                infoPool.playerInfo[clientNum].score.score = commandEventArgs.Command.Argv(i * 14 + 5).Atoi();
                infoPool.playerInfo[clientNum].score.ping = commandEventArgs.Command.Argv(i * 14 + 6).Atoi();
                infoPool.playerInfo[clientNum].score.time = commandEventArgs.Command.Argv(i * 14 + 7).Atoi();
                infoPool.playerInfo[clientNum].score.scoreFlags = commandEventArgs.Command.Argv(i * 14 + 8).Atoi();
                powerups = commandEventArgs.Command.Argv(i * 14 + 9).Atoi();
                infoPool.playerInfo[clientNum].score.powerUps = powerups;
                infoPool.playerInfo[clientNum].powerups = powerups;
                infoPool.playerInfo[clientNum].score.accuracy = commandEventArgs.Command.Argv(i * 14 + 10).Atoi();
                infoPool.playerInfo[clientNum].score.impressiveCount = commandEventArgs.Command.Argv(i * 14 + 11).Atoi();
                infoPool.playerInfo[clientNum].score.excellentCount = commandEventArgs.Command.Argv(i * 14 + 12).Atoi();
                infoPool.playerInfo[clientNum].score.guantletCount = commandEventArgs.Command.Argv(i * 14 + 13).Atoi();
                infoPool.playerInfo[clientNum].score.defendCount = commandEventArgs.Command.Argv(i * 14 + 14).Atoi();
                infoPool.playerInfo[clientNum].score.assistCount = commandEventArgs.Command.Argv(i * 14 + 15).Atoi();
                infoPool.playerInfo[clientNum].score.perfect = commandEventArgs.Command.Argv(i * 14 + 16).Atoi() == 0 ? false : true;
                infoPool.playerInfo[clientNum].score.captures = commandEventArgs.Command.Argv(i * 14 + 17).Atoi();

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
