using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
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
            _ = createConnection(ip, protocol);
        }
        public Connection(ConnectedServerWindow serverWindowA, ServerInfo serverInfo, ServerSharedInformationPool infoPoolA)
        {
            infoPool = infoPoolA;
            serverWindow = serverWindowA;
            _ = createConnection(serverInfo.Address.ToString(), serverInfo.Protocol);
        }

        ~Connection()
        {
            disconnect();
        }

        private string ip;
        private ProtocolVersion protocol;

        private async Task createConnection( string ipA, ProtocolVersion protocolA)
        {
            ip = ipA;
            protocol = protocolA;

            client = new Client();
            client.Name = "Padawan";

            client.ServerCommandExecuted += ServerCommandExecuted;
            client.ServerInfoChanged += Connection_ServerInfoChanged;
            client.SnapshotParsed += Client_SnapshotParsed;
            client.EntityEvent += Client_EntityEvent;
            client.Disconnected += Client_Disconnected;
            Status = client.Status;
            
            client.Start(ExceptionCallback);
            Status = client.Status;

            await client.Connect(ip, protocol);
            Status = client.Status;

            serverWindow.addToLog("New connection created.");
        }

        private async void Client_Disconnected(object sender, EventArgs e)
        {

            serverWindow.addToLog("Involuntary disconnect for some reason.");
            Status = client.Status;

            bool wasRecordingADemo = isRecordingADemo;
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord();
            }

            // Reconnect
            System.Threading.Thread.Sleep(1000);
            serverWindow.addToLog("Attempting to reconnect.");

            //client.Start(ExceptionCallback); // I think that's only necessary once?
            //Status = client.Status;

            await client.Connect(ip, protocol);
            Status = client.Status;

            serverWindow.addToLog("Reconnected.");

            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Attempting to resume demo recording.");
                startDemoRecord();
            }
        }

        private unsafe void Client_EntityEvent(object sender, EntityEventArgs e)
        {
            if (e.EventType == EntityEvent.Obituary)
            {
                // Todo do more elaborate logging. Death method etc. Detect multikills maybe
                int target = e.Entity.CurrentState.OtherEntityNum;
                int attacker = e.Entity.CurrentState.OtherEntityNum2;
                ClientEntity copyOfEntity = e.Entity; // This is necessary in order to read the fixed float arrays. Don't ask why, idk.
                Vector3 locationOfDeath;
                locationOfDeath.X = copyOfEntity.CurrentState.Position.Base[0];
                locationOfDeath.Y = copyOfEntity.CurrentState.Position.Base[1];
                locationOfDeath.Z = copyOfEntity.CurrentState.Position.Base[2];
                MeansOfDeath mod = (MeansOfDeath)e.Entity.CurrentState.EventParm;
                if (target < 0 || target >= JKClient.Common.MaxClients)
                {
                    serverWindow.addToLog("EntityEvent Obituary: value "+target+" is out of bounds.");
                }

                infoPool.playerInfo[target].lastDeath = DateTime.Now;
                infoPool.playerInfo[target].lastDeathPosition = locationOfDeath;
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

                PlayerInfo? pi = null;

                if(playerNum >= 0 && playerNum <= JKClient.Common.MaxClients)
                {
                    pi = infoPool.playerInfo[playerNum];
                }

                if(messageType == CtfMessageType.PlayerGotFlag && pi != null)
                {
                    if(team == Team.Red)
                    {
                        infoPool.lastRedFlagCarrier = playerNum;
                        infoPool.lastRedFlagCarrierUpdate = DateTime.Now;
                        infoPool.redFlag = FlagStatus.FLAG_TAKEN;
                        serverWindow.addToLog(pi.Value.name + " got the red flag.");
                    } else if (team == Team.Blue)
                    {
                        infoPool.lastBlueFlagCarrier = playerNum;
                        infoPool.lastBlueFlagCarrierUpdate = DateTime.Now; 
                        infoPool.blueFlag = FlagStatus.FLAG_TAKEN;
                        serverWindow.addToLog(pi.Value.name + " got the blue flag.");
                    }
                } else if (messageType == CtfMessageType.FraggedFlagCarrier && pi != null)
                {
                    if (team == Team.Blue) // Teams are inverted here because team is the team of the person who got killed
                    {
                        infoPool.redFlag = FlagStatus.FLAG_DROPPED;
                        serverWindow.addToLog(pi.Value.name + " killed carrier of red flag.");
                    }
                    else if (team == Team.Red)
                    {
                        infoPool.blueFlag = FlagStatus.FLAG_DROPPED;
                        serverWindow.addToLog(pi.Value.name + " killed carrier of blue flag.");
                    }
                } else if (messageType == CtfMessageType.FlagReturned)
                {
                    if (team == Team.Red)
                    {
                        infoPool.redFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog("Red flag was returned.");
                    }
                    else if (team == Team.Blue)
                    {
                        infoPool.blueFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog("Blue flag was returned.");
                    }
                }
                else if (messageType == CtfMessageType.PlayerCapturedFlag && pi != null)
                {
                    if (team == Team.Red)
                    {
                        infoPool.redFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog(pi.Value.name + " captured the red flag.");
                    }
                    else if (team == Team.Blue)
                    {
                        infoPool.blueFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog(pi.Value.name + " captured the blue flag.");
                    }
                }
                else if (messageType == CtfMessageType.PlayerReturnedFlag && pi != null)
                {
                    if (team == Team.Red)
                    {
                        infoPool.redFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog(pi.Value.name + " returned the red flag.");
                    }
                    else if (team == Team.Blue)
                    {
                        infoPool.blueFlag = FlagStatus.FLAG_ATBASE;
                        serverWindow.addToLog(pi.Value.name + " returned the blue flag.");
                    }
                }

            }
            // Todo: look into various sound events that are broarcast to everyone, also global item pickup,
            // then we immediately know who's carrying the flag
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

                if (entities[i].CurrentValid || entities[i].CurrentState.FilledFromPlayerState ) { 
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
                    infoPool.playerInfo[i].lastPositionUpdate = DateTime.Now;
                    
                    if((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0)
                    {
                        infoPool.lastRedFlagCarrier = i;
                        infoPool.lastRedFlagCarrierUpdate = DateTime.Now;
                    } else if ((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0)
                    {
                        infoPool.lastBlueFlagCarrier = i;
                        infoPool.lastBlueFlagCarrierUpdate = DateTime.Now;
                    }
                }
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
            for (int i = JKClient.Common.MaxClients-1; i < JKClient.Common.MaxGEntities; i++)
            {
                if (entities[i].CurrentValid)
                { 
                    // Flag bases
                    if(entities[i].CurrentState.EntityType == (int)entityType_t.ET_TEAM)
                    {
                        if(entities[i].CurrentState.ModelIndex == (int)Team.Blue)
                        {
                            // It's the blue flag base
                            infoPool.blueFlagBasePosition.X = entities[i].CurrentState.Position.Base[0];
                            infoPool.blueFlagBasePosition.Y = entities[i].CurrentState.Position.Base[1];
                            infoPool.blueFlagBasePosition.Z = entities[i].CurrentState.Position.Base[2];
                            infoPool.lastBlueFlagBasePositionUpdate = DateTime.Now;
                        } else if(entities[i].CurrentState.ModelIndex == (int)Team.Red)
                        {
                            // red flag base
                            infoPool.redFlagBasePosition.X = entities[i].CurrentState.Position.Base[0];
                            infoPool.redFlagBasePosition.Y = entities[i].CurrentState.Position.Base[1];
                            infoPool.redFlagBasePosition.Z = entities[i].CurrentState.Position.Base[2];
                            infoPool.lastRedFlagBasePositionUpdate = DateTime.Now;
                        }
                    }else if (entities[i].CurrentState.EntityType == (int)entityType_t.ET_ITEM)
                    {
                        if(entities[i].CurrentState.ModelIndex == (int)ItemList.ModelIndex.MODELINDEX_REDFLAG ||
                            entities[i].CurrentState.ModelIndex == (int)ItemList.ModelIndex.MODELINDEX_BLUEFLAG 
                            )
                        {
                            // Check if it's base flag item or dropped one
                            if ((entities[i].CurrentState.EntityFlags & (int)EntityFlags.EF_BOUNCE_HALF) != 0)
                            {
                                // This very likely is a dropped flag, as dropped flags get the EF_BOUNCE_HALF entity flag.
                                if(entities[i].CurrentState.ModelIndex == (int)ItemList.ModelIndex.MODELINDEX_REDFLAG)
                                {
                                    // Red flag
                                    infoPool.redFlagDroppedPosition.X = entities[i].CurrentState.Position.Base[0];
                                    infoPool.redFlagDroppedPosition.Y = entities[i].CurrentState.Position.Base[1];
                                    infoPool.redFlagDroppedPosition.Z = entities[i].CurrentState.Position.Base[2];
                                    infoPool.lastRedFlagDroppedPositionUpdate = DateTime.Now;
                                }
                                else
                                {
                                    // Blue flag
                                    infoPool.blueFlagDroppedPosition.X = entities[i].CurrentState.Position.Base[0];
                                    infoPool.blueFlagDroppedPosition.Y = entities[i].CurrentState.Position.Base[1];
                                    infoPool.blueFlagDroppedPosition.Z = entities[i].CurrentState.Position.Base[2];
                                    infoPool.lastBlueFlagDroppedPositionUpdate = DateTime.Now;
                                }

                            } else
                            {
                                // This very likely is a base flag item, as it doesn't have an EF_BOUNCE_HALF entity flag.
                                if (entities[i].CurrentState.ModelIndex == (int)ItemList.ModelIndex.MODELINDEX_REDFLAG)
                                {
                                    // Red flag
                                    infoPool.redFlagBaseItemPosition.X = entities[i].CurrentState.Position.Base[0];
                                    infoPool.redFlagBaseItemPosition.Y = entities[i].CurrentState.Position.Base[1];
                                    infoPool.redFlagBaseItemPosition.Z = entities[i].CurrentState.Position.Base[2];
                                    infoPool.lastRedFlagBaseItemPositionUpdate = DateTime.Now;
                                }
                                else
                                {
                                    // Blue flag
                                    infoPool.blueFlagBaseItemPosition.X = entities[i].CurrentState.Position.Base[0];
                                    infoPool.blueFlagBaseItemPosition.Y = entities[i].CurrentState.Position.Base[1];
                                    infoPool.blueFlagBaseItemPosition.Z = entities[i].CurrentState.Position.Base[2];
                                    infoPool.lastBlueFlagBaseItemPositionUpdate = DateTime.Now;
                                }
                            }
                        }
                    }
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

            client.Disconnected -= Client_Disconnected; // This only handles involuntary disconnects
            client.Disconnect();
            client.ServerCommandExecuted -= ServerCommandExecuted;
            client.ServerInfoChanged -= Connection_ServerInfoChanged;
            client.SnapshotParsed -= Client_SnapshotParsed;
            client.EntityEvent -= Client_EntityEvent;
            client.Stop();
            client.Dispose();
            serverWindow.addToLog("Disconnected.");
        }

        int ExceptionCallbackRecursion = 0;
        const int ExceptionCallbackRecursionLimit = 5;
        // Client crashed for some reason
        private async Task ExceptionCallback(JKClientException exception)
        {
            if(ExceptionCallbackRecursion++ > ExceptionCallbackRecursionLimit)
            {
                serverWindow.addToLog("Hit ExceptionCallback recursion limit trying to restart the connection. Giving up.");
                return;
            }
            serverWindow.addToLog("JKClient crashed: "+exception.ToString());
            Debug.WriteLine(exception);

            bool wasRecordingADemo = isRecordingADemo;
            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Was recording a demo. Stopping recording if not already stopped.");
                stopDemoRecord();
            }

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
            disconnect();
            await createConnection(ip, protocol);


            serverWindow.addToLog("Reconnected.");

            if (wasRecordingADemo)
            {
                serverWindow.addToLog("Attempting to resume demo recording.");
                startDemoRecord();
            }
            ExceptionCallbackRecursion = 0;
        }

        List<string> serverCommandsVerbosityLevel0WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect" };
        List<string> serverCommandsVerbosityLevel2WhiteList = new List<string>() {"chat","tchat","lchat","print","cp","disconnect","cs" };

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
                default:
                    break;
            }

            if (serverWindow.verboseOutput ==5 || 
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
                        infoPool.redFlag = (FlagStatus)int.Parse(str[0].ToString());
                        infoPool.blueFlag = (FlagStatus)int.Parse(str[1].ToString());
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
        
        void EvaluateTInfo(CommandEventArgs commandEventArgs)
        {
            int i;
            int client;

            int numSortedTeamPlayers = commandEventArgs.Command.Argv(1).Atoi();

            for (i = 0; i < numSortedTeamPlayers; i++)
            {
                client = commandEventArgs.Command.Argv(i * 6 + 2).Atoi();

                //sortedTeamPlayers[i] = client;

                if(client < 0 || client >= 32)
                {
                    serverWindow.addToLog("TeamInfo client weird number "+client.ToString());
                } else { 

                    infoPool.playerInfo[client].location = commandEventArgs.Command.Argv(i * 6 + 3).Atoi();
                    infoPool.playerInfo[client].health = commandEventArgs.Command.Argv(i * 6 + 4).Atoi();
                    infoPool.playerInfo[client].armor = commandEventArgs.Command.Argv(i * 6 + 5).Atoi();
                    infoPool.playerInfo[client].curWeapon = commandEventArgs.Command.Argv(i * 6 + 6).Atoi();
                    infoPool.playerInfo[client].powerUps = commandEventArgs.Command.Argv(i * 6 + 7).Atoi(); // 2/3 places where powerups is transmitted
                    if ((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0)
                    {
                        infoPool.lastRedFlagCarrier = i;
                        infoPool.lastRedFlagCarrierUpdate = DateTime.Now;
                    }
                    else if ((infoPool.playerInfo[i].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0)
                    {
                        infoPool.lastBlueFlagCarrier = i;
                        infoPool.lastBlueFlagCarrierUpdate = DateTime.Now;
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

                if ((infoPool.playerInfo[clientNum].powerUps & (1 << (int)ItemList.powerup_t.PW_REDFLAG)) != 0)
                {
                    infoPool.lastRedFlagCarrier = clientNum;
                    infoPool.lastRedFlagCarrierUpdate = DateTime.Now;
                }
                else if ((infoPool.playerInfo[clientNum].powerUps & (1 << (int)ItemList.powerup_t.PW_BLUEFLAG)) != 0)
                {
                    infoPool.lastBlueFlagCarrier = clientNum;
                    infoPool.lastBlueFlagCarrierUpdate = DateTime.Now;
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
        }

        bool isRecordingADemo = false;

        public async void startDemoRecord()
        {
            if (isRecordingADemo)
            {
                serverWindow.addToLog("Demo is already being recorded...");
                return;
            }

            serverWindow.addToLog("Initializing demo recording...");
            string timeString = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string unusedDemoFilename = Helpers.GetUnusedDemoFilename(timeString + "-" + client.ServerInfo.MapName, client.ServerInfo.Protocol);

            TaskCompletionSource<bool> firstPacketRecordedTCS = new TaskCompletionSource<bool>();

            firstPacketRecordedTCS.Task.ContinueWith((Task<bool> s) =>
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

                // NWH / CTFMod (?)
                client.ExecuteCommand("info");
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
                client.ExecuteCommand("clientlist");
            });

            bool success = await client.Record_f(unusedDemoFilename, firstPacketRecordedTCS);

            if (success)
            {

                serverWindow.addToLog("Demo recording started.");
                isRecordingADemo = true;
            }
            else
            {

                serverWindow.addToLog("Demo recording failed to start for some reason.");
                isRecordingADemo = false;
            }
        }
        public void stopDemoRecord()
        {
            serverWindow.addToLog("Stopping demo recording...");
            client.StopRecord_f();
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
