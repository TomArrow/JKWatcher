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
    // Chat command related stuff
    public partial class Connection
    {

        
        // For various kinda text processisng stuff / memes
        string lastPublicChat = "";
        DateTime lastPublicChatTime = DateTime.Now;
        string lastTeamChat = "";
        DateTime lastTeamChatTime = DateTime.Now;


        public bool IsMainChatConnection { get; set; }= false;
        public int ChatMemeCommandsDelay { get; set; } = 4000;

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
        ParsedChatMessage? ParseChatMessage(string nameChatSegmentA, string extraArgument = null)
        {
            int sentNumber = -1;

            if (extraArgument != null) // This is jka only i think, sending the client num as an extra
            {
                if (!isNumber(extraArgument))
                {
                    serverWindow.addToLog($"Chat message parsing, received extra argument that is not a number: {extraArgument} ({nameChatSegmentA})", true);
                }
                else
                {
                    if (!int.TryParse(extraArgument, out sentNumber))
                    {
                        sentNumber = -1;
                    }
                }
            }

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
            }
            else if (nameChatSegment.Substring(0, teamChatBegin.Length) == teamChatBegin)
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


            int playerNum = -1;

            if (possiblePlayers.Count == 0)
            {
                if (sentNumber == -1)
                {
                    serverWindow.addToLog($"Could not identify sender of (t)chat message. Zero matches: {nameChatSegmentA}", true);
                    return null;
                }
                else
                {
                    serverWindow.addToLog($"Could not identify sender of (t)chat message ({nameChatSegmentA}), zero matches, but extra argument number {sentNumber} helped.", true);
                    playerNum = sentNumber;
                }
            }
            else if (possiblePlayers.Count > 1)
            {
                if (sentNumber == -1)
                {
                    serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. More than 1 match: {nameChatSegmentA}", true);
                    return null;
                }
                else
                {
                    int confirmedNumber = -1;
                    for (int i = 0; i < possiblePlayers.Count; i++)
                    {
                        if (possiblePlayers[i] == sentNumber)
                        {
                            confirmedNumber = sentNumber;
                        }
                    }
                    if (confirmedNumber == -1)
                    {
                        serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. More than 1 match: {nameChatSegmentA} and extra argument number {sentNumber} matched none.", true);
                        return null;
                    }
                    else
                    {
                        serverWindow.addToLog($"Could not reliably identify sender of (t)chat message ({nameChatSegmentA}), but extra argument number {sentNumber} cleared it up.", true);
                        playerNum = confirmedNumber;
                    }
                }
            }
            else
            {
                playerNum = possiblePlayers[0];
            }

            string playerName = infoPool.playerInfo[playerNum].name;
            if (playerName == null)
            {
                serverWindow.addToLog($"Received message from player whose name in infoPool is now null, wtf.", true);
                return null;
            }

            return new ParsedChatMessage() { message = nameChatSegment.Substring(playerName.Length + separatorLength).Trim(), playerName = playerName, playerNum = playerNum, type = detectedChatType };
        }

        bool isNumber(string str)
        {
            if (str.Length == 0) return false;
            foreach (char ch in str)
            {
                if (!(ch >= '0' && ch <= '9'))
                {
                    return false;
                }
            }
            return true;
        }

        void EvaluateChat(CommandEventArgs commandEventArgs)
        {
            try
            {
                if (client?.Demorecording != true) return; // Why mark demo times if we're not recording...
                if (commandEventArgs.Command.Argc >= 2)
                {

                    int? myClientNum = client?.clientNum;
                    if (!myClientNum.HasValue) return;
                    string nameChatSegment = commandEventArgs.Command.Argv(1);

                    ParsedChatMessage? pmMaybe = ParseChatMessage(nameChatSegment, commandEventArgs.Command.Argc > 2 ? commandEventArgs.Command.Argv(2) : null);

                    if (!pmMaybe.HasValue) return;

                    ParsedChatMessage pm = pmMaybe.Value;

                    if (pm.message == null) return; // Message from myself. Ignore.


                    List<int> numberParams = new List<int>();
                    List<string> stringParams = new List<string>();

                    string demoNoteString = null;
                    { // Just putting this in its own scope to isolate the variables.
                        string[] messageBits = pm.message.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (messageBits.Length == 0) return;
                        StringBuilder demoNote = new StringBuilder();

                        int possibleNumbers = messageBits.Length > 0 && (messageBits[0].ToLowerInvariant().Contains("markas") || messageBits[0].ToLowerInvariant().Contains("kills")) ? 2 : 1;
                        foreach (string bit in messageBits)
                        {
                            if (stringParams.Count == 1 && numberParams.Count < possibleNumbers && isNumber(bit)) // Number parameters can only occur after the initial command, for example !markas 2 3
                            {
                                int tmp;
                                if (int.TryParse(bit, out tmp)) // dunno why it wouldnt be true but lets be safe
                                {
                                    numberParams.Add(tmp);
                                    continue;
                                }
                            }
                            else if (stringParams.Count >= 1)
                            {
                                stringParams.Add(bit);
                                demoNote.Append(bit);
                                demoNote.Append(" ");
                            }
                            else
                            {
                                stringParams.Add(bit);
                            }
                        }
                        if (demoNote.Length > 0)
                        {
                            demoNoteString = demoNote.ToString().Trim();
                        }
                    }

                    //if (messageBits.Length == 0 || messageBits.Length > 3) return;
                    if (stringParams.Count == 0) return;

                    //StringBuilder response = new StringBuilder();
                    bool markRequested = false;
                    bool helpRequested = false;
                    bool reframeRequested = false;
                    int markMinutes = 1;
                    int reframeClientNum = pm.playerNum;
                    int requiredCommandNumbers = 0;
                    int maxClientsHere = (client?.ClientHandler?.MaxClients).GetValueOrDefault(32);
                    // Idea: let people define their own binds so they don't have to use markme? hm
                    bool notDemoCommand = false;
                    string stringParams0Lower = stringParams[0].ToLowerInvariant();
                    string tmpString;
                    switch (stringParams0Lower)
                    {
                        // Memes
                        case "memes":
                        case "!memes":
                            if (!this.IsMainChatConnection || (stringParams0Lower== "memes" && pm.type != ChatType.PRIVATE)) return;
                            MemeRequest(pm, "!mock !gaily !reverse !ruski !saymyname !agree !opinion !color !flipcoin !roulette !who", true, true, true, true);
                            notDemoCommand = true;
                            // TODO Send list of meme commands
                            break;

                        // String manipulation:
                        // Private and team meme responses get their own category each so the rate limiting isn't confusing
                        case "!mock":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : lastPublicChat;
                            MemeRequest(pm, MockString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!gaily":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : lastPublicChat;
                            MemeRequest(pm, GailyString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!reverse":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : lastPublicChat;
                            MemeRequest(pm, new string(tmpString.Reverse().ToArray()), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!ruski":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : lastPublicChat;
                            MemeRequest(pm, RuskiString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;

                        // Whatever
                        case "!saymyname":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            MemeRequest(pm, pm.playerName,true,true,true);
                            notDemoCommand = true;
                            break;
                        case "!agree":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            MemeRequest(pm, $"agreed, {pm.playerName}",true,true,true);
                            notDemoCommand = true;
                            break;
                        case "!color":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if(numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                MemeRequest(pm, "Call !color with a valid client number (see /clientlist)", true, true, true,true);
                                return;
                            }
                            MemeRequest(pm, StringShowQ3Color(infoPool.playerInfo[numberParams[0]].name), true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!flipcoin":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            if (stringParams.Count < 2)
                            {
                                MemeRequest(pm, "heads or tails?", true, true, false);
                            } else if(stringParams[1].ToLowerInvariant() != "heads" && stringParams[1].ToLowerInvariant() != "tails")
                            {
                                MemeRequest(pm, $"{stringParams[1]}? what's that?", true, true, false);
                            } else
                            {
                                string result = getNiceRandom(0, 2) == 1 ? "tails" : "heads";
                                if (result == stringParams[1].ToLowerInvariant())
                                {
                                    MemeRequest(pm, $"it's {result}, you win", true, true, false);
                                } else
                                {
                                    MemeRequest(pm, $"it's {result}, you lose", true, true, false);
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!opinion":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            int opinionNum = getNiceRandom(0, 3);
                            string opinion = "maybe";
                            if(opinionNum == 1)
                            {
                                opinion = "yes";
                            }
                            if(opinionNum == 2)
                            {
                                opinion = "no";
                            }
                            MemeRequest(pm, opinion, true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!roulette":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            MemeRequest(pm, getNiceRandom(0, 7) == 0 ? "you played russian roulette and died" : "you played russian roulette and survived", true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!who":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (demoNoteString == null)
                            {
                                MemeRequest(pm, "who what?", true, true, true);
                            } else
                            {
                                int randomPlayer = PickRandomPlayer(maxClientsHere);
                                if (randomPlayer == -1) return;
                                MemeRequest(pm, $"{infoPool.playerInfo[randomPlayer].name} {demoNoteString}", true, true, true);
                            }
                            notDemoCommand = true;
                            break;




                        // Memes
                        case "tools":
                        case "!tools":
                            if (!this.IsMainChatConnection || (stringParams0Lower == "tools" && pm.type != ChatType.PRIVATE)) return;
                            MemeRequest(pm, "!kills !kd !match !resetmatch !endmatch !matchstate", true, true, true, true);
                            notDemoCommand = true;
                            // TODO Send list of meme commands
                            break;

                        case "!kills":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                if(stringParams.Count > 1 && stringParams[1] == "all")
                                {
                                    MemeRequest(pm, $"Total witnessed K/D for {infoPool.playerInfo[pm.playerNum].name}: {infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.totalKills}/{infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.totalDeaths}", true, true, true);
                                } else
                                {
                                    MemeRequest(pm, "Call !kills with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                }
                                return;
                            }
                            if(numberParams.Count == 2)
                            {
                                if(numberParams[1] < 0 || numberParams[1] >= maxClientsHere || !infoPool.playerInfo[numberParams[1]].infoValid)
                                {
                                    MemeRequest(pm, "Call !kills with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                    return;
                                }
                                MemeRequest(pm, $"^7^7^7Total witnessed kills: {infoPool.playerInfo[numberParams[0]].name} ^7^7^7vs. {infoPool.playerInfo[numberParams[1]].name}^7^7^7: {infoPool.killTrackers[numberParams[0], numberParams[1]].kills}-{infoPool.killTrackers[numberParams[1], numberParams[0]].kills}", true, true, true);
                            } else
                            {
                                MemeRequest(pm, $"^7^7^7Total witnessed kills: {infoPool.playerInfo[pm.playerNum].name} ^7^7^7vs. {infoPool.playerInfo[numberParams[0]].name}^7^7^7: {infoPool.killTrackers[pm.playerNum, numberParams[0]].kills}-{infoPool.killTrackers[numberParams[0], pm.playerNum].kills}", true, true, true);
                            }
                            
                            notDemoCommand = true;
                            break;
                        case "!kd":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                MemeRequest(pm, "Call !kd with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            MemeRequest(pm, $"Total witnessed K/D for {infoPool.playerInfo[numberParams[0]].name}: {infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.totalKills}/{infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.totalDeaths}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!match":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                MemeRequest(pm, "Call !match with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                MemeRequest(pm, $"Already tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !resetmatch or !endmatch?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch = true;
                                }
                                MemeRequest(pm, $"Now tracking match against {infoPool.playerInfo[numberParams[0]].name}", true, true, true,true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!matchstate":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                MemeRequest(pm, "Call !matchstate with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                MemeRequest(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                MemeRequest(pm, $"Your match against {infoPool.playerInfo[numberParams[0]].name}: {infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills}-{infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths}", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!resetmatch":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                MemeRequest(pm, "Call !resetmatch with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                MemeRequest(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths = 0;
                                }
                                MemeRequest(pm, $"Match against {infoPool.playerInfo[numberParams[0]].name} reset to 0-0", true, true, true,true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!endmatch":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                if (stringParams.Count > 1 && stringParams[1] == "all")
                                {
                                    int countEnded = 0;
                                    lock (infoPool.killTrackers)
                                    {
                                        for (int otherpl = 0; otherpl < maxClientsHere; otherpl++)
                                        {
                                            infoPool.killTrackers[pm.playerNum, otherpl].trackedMatchKills = 0;
                                            infoPool.killTrackers[pm.playerNum, otherpl].trackedMatchDeaths = 0;
                                            if (infoPool.killTrackers[pm.playerNum, otherpl].trackingMatch)
                                            {
                                                countEnded++;
                                                infoPool.killTrackers[pm.playerNum, otherpl].trackingMatch = false;
                                            }
                                        }
                                    }
                                    MemeRequest(pm, $"Ended tracking of all matches. ({countEnded} affected)", true, true, true,true);
                                }
                                else
                                {
                                    MemeRequest(pm, "Call !endmatch with a valid client number (see /clientlist) or 'all'", true, true, true, true);
                                }
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                MemeRequest(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch = false;
                                    MemeRequest(pm, $"Match against {infoPool.playerInfo[numberParams[0]].name} ended at {infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills}-{infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths}.", true, true, true, true);
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths = 0;
                                }
                            }
                            notDemoCommand = true;
                            break;


                        // Demo related
                        default:
                            if (pm.type == ChatType.PRIVATE)
                            {
                                if (!_connectionOptions.silentMode && pm.playerNum != myClientNum)
                                {
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"!demohelp (demo commands), !memes (meme commands), !tools (tool commands)\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    /*leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"I don't understand your command. Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);*/
                                }
                                return;
                            } else if(pm.type == ChatType.PUBLIC && pm.message.Trim().Length > 0)
                            {
                                lastPublicChat = pm.message;
                                lastPublicChatTime = DateTime.Now;
                            } else if(pm.type == ChatType.TEAM && pm.message.Trim().Length > 0)
                            {
                                lastTeamChat = pm.message;
                                lastTeamChatTime = DateTime.Now;
                            }
                            break;
                        case "!help":
                        case "help":
                            if (pm.type == ChatType.PRIVATE)
                            {
                                if (!_connectionOptions.silentMode && pm.playerNum != myClientNum)
                                {
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"!demohelp (demo commands), !memes (meme commands), !tools (tool commands)\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                }
                            }
                            break;
                        case "demohelp":
                            if (pm.type == ChatType.PRIVATE)
                            {
                                helpRequested = true;
                            }
                            break;
                        case "!demohelp":
                            helpRequested = true;
                            break;
                        case "!mark":
                        case "!markme":
                        case "!markas":

                            if (stringParams0Lower == "!markas")
                            {
                                reframeRequested = true;
                                requiredCommandNumbers = 1;
                                if (numberParams.Count > 0 && numberParams[0] >= 0 && numberParams[0] < maxClientsHere)
                                {
                                    reframeClientNum = numberParams[0];
                                }
                            }
                            else if (stringParams0Lower == "!markme")
                            {
                                reframeRequested = true;
                            }
                            markRequested = true;
                            if (numberParams.Count > requiredCommandNumbers)
                            {
                                markMinutes = Math.Max(1, numberParams[requiredCommandNumbers]);
                            }
                            break;
                        case "mark":
                        case "markme":
                        case "markas":
                            if (pm.type == ChatType.PRIVATE)
                            {
                                markRequested = true;

                                if (stringParams0Lower == "markas")
                                {
                                    reframeRequested = true;
                                    requiredCommandNumbers = 1;
                                    if (numberParams.Count > 0 && numberParams[0] >= 0 && numberParams[0] < maxClientsHere)
                                    {
                                        reframeClientNum = numberParams[0];
                                    }
                                }
                                else if (stringParams0Lower == "markme")
                                {
                                    reframeRequested = true;
                                }
                                if (numberParams.Count > requiredCommandNumbers)
                                {
                                    markMinutes = Math.Max(1, numberParams[requiredCommandNumbers]);
                                }
                            }
                            break;
                    }

                    if (notDemoCommand)
                    {
                        return;
                    }

                    if (helpRequested)
                    {
                        serverWindow.addToLog($"help requested by \"{pm.playerName}\" (clientNum {pm.playerNum})\n");
                        if (!_connectionOptions.silentMode)
                        {
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"Here is your help: Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                        }
                        return;
                    }

                    int demoTime = (client?.DemoCurrentTime).GetValueOrDefault(0);

                    string asClientNum = reframeClientNum != pm.playerNum ? $"(as {reframeClientNum})" : "";
                    string withReframe = reframeRequested ? $"+reframe{asClientNum}" : "";
                    if (markRequested)
                    {
                        ServerInfo thisServerInfo = client?.ServerInfo;
                        if (thisServerInfo == null)
                        {
                            return;
                        }
                        StringBuilder demoCutCommand = new StringBuilder();
                        DateTime now = DateTime.Now;
                        demoCutCommand.Append($"rem demo cut{withReframe} requested by \"{pm.playerName}\" (clientNum {pm.playerNum}) on {thisServerInfo.HostName} ({thisServerInfo.Address.ToString()}) at {now}, {markMinutes} minute(s) into the past\n");
                        string demoNoteStringFilenamePart = "";
                        if (demoNoteString != null)
                        {
                            demoCutCommand.Append($"rem demo note: {demoNoteString}\n");
                            demoNoteStringFilenamePart = $"_{demoNoteString}";
                        }
                        demoCutCommand.Append("DemoCutter ");
                        string demoPath = client?.AbsoluteDemoName;
                        if (demoPath == null)
                        {
                            return;
                        }
                        string relativeDemoPath = Path.GetRelativePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts"), demoPath);
                        demoCutCommand.Append($"\"{relativeDemoPath}\" ");
                        string filename = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{pm.playerName}__{thisServerInfo.HostName}__{thisServerInfo.MapName}_{myClientNum}{demoNoteStringFilenamePart}";
                        if (filename.Length > 150) // Limit this if ppl do ridiculously long notes, or if server name is too long... or or or 
                        {
                            filename = filename.Substring(0, 150);
                        }
                        filename = Helpers.MakeValidFileName(filename);
                        filename = Helpers.DemoCuttersanitizeFilename(filename, false);
                        demoCutCommand.Append($"\"{filename}\" ");
                        demoCutCommand.Append(Math.Max(0, demoTime - markMinutes * 60000).ToString());
                        demoCutCommand.Append(" ");
                        demoCutCommand.Append((demoTime + 60000).ToString());
                        demoCutCommand.Append("\n");

                        if (reframeRequested)
                        {
                            demoCutCommand.Append("DemoReframer ");
                            demoCutCommand.Append($"\"{filename}.dm_{(int)this.protocol}\" ");
                            demoCutCommand.Append($"\"{filename}_reframed{reframeClientNum}.dm_{(int)this.protocol}\" ");
                            demoCutCommand.Append(reframeClientNum);
                            demoCutCommand.Append("\n");
                        }

                        Helpers.logRequestedDemoCut(new string[] { demoCutCommand.ToString() });
                    }
                    else
                    {
                        return;
                    }
                    if (!_connectionOptions.silentMode)
                    {
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"Time was marked for demo cut{withReframe}, {markMinutes} min into past. Current demo time is {demoTime}.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    }
                    serverWindow.addToLog($"^1NOTE ({myClientNum}): demo cut{withReframe} requested by \"{pm.playerName}\" (clientNum {pm.playerNum}), {markMinutes} minute(s) into the past\n");
                    return;
                }
            }
            catch (Exception e)
            {
                serverWindow.addToLog("Error evaluating chat: " + e.ToString(), true);
            }
        }








        Regex multipleSlashRegex = new Regex("/{2,}", RegexOptions.Compiled);
        void MemeRequest(ParsedChatMessage pm, string message, bool pub=true, bool team=true, bool priv=true, bool forcePrivateResponse = false)
        {

            if (_connectionOptions.silentMode) {
                serverWindow.addToLog($"Silent mode: MemeRequest sending suppressed in response to {pm.type} from {pm.playerName} ({pm.playerNum}): {message}");
                return;
            }
            if(getNiceRandom(0,500) == 250)
            {
                message = "STFU AND PLAY";
            } else
            {
                message = multipleSlashRegex.Replace(message, "/"); // Idk why this is needed. double // seems to cut off messages.
            }
            bool forcingPrivateResponse = false;
            if (pm.type == ChatType.TEAM && team)
            {
                if (forcePrivateResponse) forcingPrivateResponse = true;
                else leakyBucketRequester.requestExecution($"say_team \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
            }
            else if (pm.type == ChatType.PUBLIC && pub)
            {
                if (forcePrivateResponse) forcingPrivateResponse = true;
                else leakyBucketRequester.requestExecution($"say \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
            }
            if ((pm.type == ChatType.PRIVATE && priv) || forcingPrivateResponse)
            {
                leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
            }
        }












        string MockString(string str)
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < str.Length; i++)
            {
                if(getNiceRandom(0, 2) == 0)
                {
                    sb.Append(str[i].ToString().ToLowerInvariant());
                }
                else
                {
                    sb.Append(str[i].ToString().ToUpperInvariant());
                }
            }
            return sb.ToString();
        }

        string GailyString(string strA)
        {
            StringBuilder sb = new StringBuilder();
            string str = Q3ColorFormatter.cleanupString(strA);
            Random rnd = new Random();
            for(int i = 0; i < str.Length; i++)
            {
                sb.Append($"^{getNiceRandom(1, 7)}{str[i]}");
            }
            return sb.ToString();
        }
        string StringShowQ3Color(string strA)
        {
            string replaced = strA.Replace("^", "^^7");
            return $"^7{replaced}";
        }

        string RuskiString(string strA)
        {
            string replaced = strA.Replace(" the ", " ").Replace(" a ", " ").Replace("is", "are");
            return $"{replaced} )))";
        }

        // This is the factor by which real players are more likely to get returned by !who command or similar commands that ask for a random player.
        const int whoRandomPlayerRealPlayerChanceMultiplier = 10;

        int PickRandomPlayer(int maxClientsHere)
        {
            int[] ourClientNums = serverWindow.getJKWatcherClientNums();
            List<int> possiblePlayers = new List<int>();
            List<int> possiblePlayersBots = new List<int>();
            for(int i = 0; i < maxClientsHere; i++)
            {
                if (infoPool.playerInfo[i].infoValid)
                {
                    if(infoPool.playerInfo[i].confirmedBot || /*serverWindow.clientNumIsJKWatcherInstance(i)*/ourClientNums.Contains(i))
                    {
                        possiblePlayersBots.Add(i);
                    } else
                    {
                        possiblePlayers.Add(i);
                    }
                }
            }
            if(possiblePlayers.Count == 0 && possiblePlayersBots.Count == 0)
            {
                return -1;
            }
            int totalGuessCount = possiblePlayers.Count * whoRandomPlayerRealPlayerChanceMultiplier + possiblePlayersBots.Count;
            int randomValue = getNiceRandom(0, totalGuessCount);
            if(randomValue >= possiblePlayers.Count * whoRandomPlayerRealPlayerChanceMultiplier)
            {
                return possiblePlayersBots[randomValue - possiblePlayers.Count * whoRandomPlayerRealPlayerChanceMultiplier];
            } else
            {
                return possiblePlayers[randomValue % possiblePlayers.Count];
            }
        }



        static Random randomMakerRandom = new Random();
        int getNiceRandom(int min, int max)
        {
            lock (randomMakerRandom)
            {
                return randomMakerRandom.Next(min, max);
            }
        }

    }
}
