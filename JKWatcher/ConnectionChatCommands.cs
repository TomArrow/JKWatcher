﻿using System;
using System.Collections.Concurrent;
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
using JKWatcher.RandomHelpers;
using JKClient;
using Client = JKClient.JKClient;
using ConditionalCommand = JKWatcher.ConnectedServerWindow.ConnectionOptions.ConditionalCommand;

namespace JKWatcher
{   
    // Chat command related stuff
    public partial class Connection
    {

        const string Glicko2Version = "(v1.4)";

        struct DemoRequestRateLimiter
        {
            public DateTime? lastRequest;
            public int overlapCount;
        }

        DemoRequestRateLimiter[] demoRateLimiters = new DemoRequestRateLimiter[64];
        bool[] clientInfoValid = new bool[64];
        static object infoPoolResetStuffLock = new object();

        // For various kinda text processisng stuff / memes
        string lastCrossServerChat = "";
        DateTime lastCrossServerChatTime = DateTime.Now;
        string lastPublicChat = "";
        DateTime lastPublicChatTime = DateTime.Now;
        string lastTeamChat = "";
        DateTime lastTeamChatTime = DateTime.Now;

        Queue<string> calmSayQueue = new Queue<string>();

        public static string MakeKillTypesString(Dictionary<KillType, int> killTypes, int lengthLimit = 150)
        {
            List<KeyValuePair<KillType, int>> killTypesData = killTypes.ToList();
            killTypesData.Sort((a, b) => { return -a.Value.CompareTo(b.Value); });

            StringBuilder killTypesString = new StringBuilder();
            int killTypeIndex = 0;
            foreach (KeyValuePair<KillType, int> killTypeInfo in killTypesData)
            {
                if (killTypeInfo.Value <= 0)
                {
                    continue;
                }
                if ((killTypesString.Length + killTypeInfo.Key.name.Length) > lengthLimit)
                {
                    break;
                }
                if (killTypeIndex != 0)
                {
                    killTypesString.Append(", ");
                }
                killTypesString.Append($"{killTypeInfo.Value}x{killTypeInfo.Key.name}");
                killTypeIndex++;
            }
            return killTypesString.ToString();
        }
        
        private string MakeKillsOnString(bool thisGame, int clientNum, bool victim, bool rets )
        {

            List<KeyValuePair<string, int>> otherPersonData = new List<KeyValuePair<string, int>>();


            /*KillTracker[,] kts = thisGame ? infoPool.killTrackersThisGame : infoPool.killTrackers;

            foreach(PlayerInfo pi in infoPool.playerInfo)
            {
                if (pi.infoValid && pi.clientNum != clientNum)
                {
                    string name = Q3ColorFormatter.cleanupString(pi.name);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        KillTracker theTracker = victim ? kts[pi.clientNum, clientNum] : kts[clientNum, pi.clientNum];
                        otherPersonData.Add(new KeyValuePair<string, int>(name, rets ? theTracker.returns : theTracker.kills));
                    }
                }
            }*/
            SessionPlayerInfo playerSession = infoPool.playerInfo[clientNum].session;
            ChatCommandTrackingStuff trackingStuff = thisGame ? playerSession.chatCommandTrackingStuffThisGame : playerSession.chatCommandTrackingStuff;
            ConcurrentDictionary<SessionPlayerInfo, KillTracker> trackers = victim ? trackingStuff.killTrackersOnMe : trackingStuff.killTrackersOnOthers;
            foreach(KeyValuePair<SessionPlayerInfo, KillTracker> kvp in trackers)
            {
                if (kvp.Key == playerSession) continue; // dont count stuff on self. 
                string name = Q3ColorFormatter.cleanupString(kvp.Key.GetNameOrLastNonPadaName());
                if (!string.IsNullOrWhiteSpace(name))
                {
                    KillTracker theTracker = kvp.Value;
                    otherPersonData.Add(new KeyValuePair<string, int>(name, rets ? theTracker.returns : theTracker.kills));
                }
            }

            otherPersonData.Sort((a, b) => { return -a.Value.CompareTo(b.Value); });

            StringBuilder otherPeopleString = new StringBuilder();
            int otherPersonIndex = 0;
            foreach (KeyValuePair<string, int> otherPersonInfo in otherPersonData)
            {
                if (otherPersonInfo.Value <= 0)
                {
                    continue;
                }
                if ((otherPeopleString.Length + otherPersonInfo.Key.Length) > 150)
                {
                    break;
                }
                if (otherPersonIndex != 0)
                {
                    otherPeopleString.Append(", ");
                }
                otherPeopleString.Append($"{otherPersonInfo.Value}x{otherPersonInfo.Key}");
                otherPersonIndex++;
            }
            return otherPeopleString.ToString();
        }

        private string MakeGlicko2RatingsString(bool thisGame, bool all)
        {
            // Make temporary ratings (so we can get up to date data without having to have rating periods so short they make the algorithm overall very imprecise)
            if (!thisGame) infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults, true);
            else infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame, true);

            List<KeyValuePair<SessionPlayerInfo, IdentifiedPlayerStats>> ratingData = (thisGame ? infoPool.ratingsAndNamesThisGame : infoPool.ratingsAndNames).ToList();
            ratingData.Sort((a, b) => { return -a.Value.rating.GetRating(true).CompareTo(b.Value.rating.GetRating(true)); });

            StringBuilder topRatingsString = new StringBuilder();
            int ratingsIndex = 0;
            foreach (KeyValuePair<SessionPlayerInfo, IdentifiedPlayerStats> thisRating in ratingData)
            {
                if (thisRating.Value.rating.GetNumberOfResults(true) <= 5) // dont count these, not relevant
                {
                    continue;
                }
                if((DateTime.Now-thisRating.Value.lastSeenActive).TotalSeconds > 60 && !all)
                {
                    continue;
                }
                string strippedName = Q3ColorFormatter.cleanupString(thisRating.Value.playerSessInfo.GetNameOrLastNonPadaName());
                if (strippedName is null) continue;
                if ((topRatingsString.Length + strippedName.Length) > 150)
                {
                    break;
                }
                if (ratingsIndex != 0)
                {
                    topRatingsString.Append(", ");
                }
                //topRatingsString.Append($"{strippedName} ({(int)thisRating.Key.GetRating()}+-{(int)thisRating.Key.GetRatingDeviation()})");
                topRatingsString.Append($"{strippedName} ({(int)thisRating.Value.rating.GetRating(true)}+-{(int)thisRating.Value.rating.GetRatingDeviation(true)})");
                ratingsIndex++;
            }
            return topRatingsString.ToString();
        }
        private string MakeDBSPercentagesString(bool thisGame, bool all)
        {
            // Make temporary ratings (so we can get up to date data without having to have rating periods so short they make the algorithm overall very imprecise)
            List<KeyValuePair<float, Tuple<string, int, int>>> entries = new List<KeyValuePair<float, Tuple<string, int, int>>>();

            foreach (PlayerInfo pi in infoPool.playerInfo)
            {
                if (!pi.infoValid) continue;
                ChatCommandTrackingStuff trackingStuff = thisGame ? pi.chatCommandTrackingStuffThisGame : pi.chatCommandTrackingStuff;
                int dbsCount = trackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_BACK_CR_GENERAL);
                if (dbsCount < 5) continue; // so it doesn't end up with one guy who did a single dbs on top of stats
                Dictionary<string, int> killtypes = trackingStuff.GetKillTypesShortname();
                int dbsKills = killtypes.ContainsKey("DBS") ? killtypes["DBS"] : 0;
                float percentage = dbsCount > 0 ? 100.0f * (float)dbsKills / (float)dbsCount : 0.0f;
                entries.Add(new KeyValuePair<float, Tuple<string, int, int>>(percentage,new Tuple<string, int, int>(pi.name, dbsKills, dbsCount))); // meh pi.session.GetNameOrLastNonPadaName()
            }

            entries.Sort((a, b) => { return -a.Key.CompareTo(b.Key); });

            StringBuilder dbsPercentagesString = new StringBuilder();
            int entryindex = 0;
            foreach (var thisRating in entries)
            {
                string strippedName = Q3ColorFormatter.cleanupString(thisRating.Value.Item1);
                if (strippedName is null) continue;
                if ((dbsPercentagesString.Length + strippedName.Length) > 150)
                {
                    break;
                }
                if (entryindex != 0)
                {
                    dbsPercentagesString.Append(", ");
                }
                string percentString = thisRating.Key.ToString("#.00");
                dbsPercentagesString.Append($"{strippedName} ({percentString}: {thisRating.Value.Item2}/{thisRating.Value.Item3})");
                entryindex++;
            }
            return dbsPercentagesString.ToString();
        }

        private static Regex numericCompareSearchRegex = new Regex(@"^\s*(?<operator>(?:>=)|(?:<=)|<|>)\s*(?<number>(?:-?\d*(?:[\.,]\d+))|-?\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static Regex numericRangeSearchRegex = new Regex(@"^\s*(?<number1>(?:-?\d*(?:[\.,]\d+))|-?\d+)\s*-\s*(?<number2>(?:-?\d*(?:[\.,]\d+))|-?\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private string MakeDefragWRMapsString(bool notwr, string searchParamsRaw, bool ignoreName) // ignorename: just search maps
        {
            int minLength = 0;
            int maxLength = int.MaxValue;
            int page = 0;
            string mapSearchString = null;
            string playername = null;
            string[] searchParams = searchParamsRaw?.Split(" ",StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if (searchParams is null || searchParams.Length == 0)
            {
                return null;
            }
            if (!ignoreName)
            {
                playername = searchParams[0];
            }
            int startIndex = ignoreName ? 0 : 1;
            Match match;
            for(int i = startIndex; i < searchParams.Length; i++)
            {
                if (isNumber(searchParams[i]))
                {
                    page = Math.Max(0, searchParams[i].Atoi()-1);
                }
                else if ((match=numericRangeSearchRegex.Match(searchParams[i])).Success)
                {
                    int number1 = match.Groups["number1"].Value.Atoi()*1000;
                    int number2 = match.Groups["number2"].Value.Atoi() * 1000;
                    minLength = Math.Min(number1, number2);
                    maxLength = Math.Max(number1, number2);
                }
                else if ((match= numericCompareSearchRegex.Match(searchParams[i])).Success)
                {
                    string op = match.Groups["operator"].Value.Trim();
                    int number = match.Groups["number"].Value.Atoi() * 1000;
                    if(op == ">")
                    {
                        minLength = number+1;
                    }
                    else if(op == ">=")
                    {
                        minLength = number;
                    }
                    else if(op == "<")
                    {
                        maxLength = number-1;
                    }
                    else if(op == "<=")
                    {
                        maxLength = number;
                    }
                }
                else
                {
                    mapSearchString = searchParams[i];
                }
            }

            DefragAverageMapTime[] defragItems = AsyncPersistentDataManager<DefragAverageMapTime>.getAllItems();


            List<KeyValuePair<DefragAverageMapTime, string>> mapData = new List<KeyValuePair<DefragAverageMapTime, string>>();

            foreach (DefragAverageMapTime mapTime in defragItems)
            {
                bool hasRecordHolder = !string.IsNullOrWhiteSpace(mapTime.recordHolder);
                // playername filtering
                if (!ignoreName)
                {
                    if (notwr && hasRecordHolder && mapTime.recordHolder.Equals(playername, StringComparison.InvariantCultureIgnoreCase)) continue;
                    else if (!notwr && (!hasRecordHolder || !mapTime.recordHolder.Equals(playername, StringComparison.InvariantCultureIgnoreCase))) continue;
                }

                // time filtering
                if (minLength > 0 && hasRecordHolder && mapTime.record < minLength) continue;
                if (maxLength < int.MaxValue && hasRecordHolder && mapTime.record > maxLength) continue;
                if ((minLength > 0 || maxLength < int.MaxValue) && !hasRecordHolder) continue;

                string cleanMapName = Q3ColorFormatter.cleanupString(mapTime.MapName.ToLowerInvariant());
                if (string.IsNullOrWhiteSpace(cleanMapName)) continue;

                // mapname filtering
                // treat - and _ as same, and non case sensitive
                if (!string.IsNullOrWhiteSpace(mapSearchString) && !cleanMapName.Replace('_','-').Contains(mapSearchString.Replace('_', '-'), StringComparison.InvariantCultureIgnoreCase)) continue;
                
                mapData.Add(new KeyValuePair<DefragAverageMapTime, string>(mapTime, cleanMapName));
            }

            if (mapData.Count == 0) {
                if (ignoreName)
                {
                    return $"No records match your map search '{mapSearchString}' and range {minLength} to {maxLength}.";
                }
                else
                {
                    return $"No records match your request for player '{playername}' with map search '{mapSearchString}' and range {minLength} to {maxLength}.";
                }
            } 

            mapData.Sort((a,b)=> { return a.Value.CompareTo(b.Value); });

            const int itemsPerPage = 4;
            int firstItem = itemsPerPage * page;
            if (firstItem >= mapData.Count && page > 0) return $"No records found on page {page}. Only {mapData.Count} items found ({itemsPerPage} per page).";

            int lastItem = Math.Min(mapData.Count-1,firstItem+itemsPerPage-1);
            List<KeyValuePair<DefragAverageMapTime, string>> items = mapData.GetRange(firstItem,lastItem-firstItem+1);
            StringBuilder recordsString = new StringBuilder();

            if(page > 0 || mapData.Count > itemsPerPage)
            {
                recordsString.Append($"({firstItem + 1}-{lastItem + 1}/{mapData.Count}) ");
            }

            int itemIndex = 0;
            foreach (KeyValuePair<DefragAverageMapTime, string> item in items)
            {
                if (itemIndex != 0)
                {
                    recordsString.Append(", ");
                }
                if (!string.IsNullOrWhiteSpace(item.Key.recordHolder))
                {
                    if (notwr || ignoreName)
                    {
                        recordsString.Append($"{item.Value} ({item.Key.recordHolder},{ScoreboardRenderer.FormatTime(item.Key.record)})");
                    } else
                    {
                        recordsString.Append($"{item.Value} ({ScoreboardRenderer.FormatTime(item.Key.record)})");
                    }
                } else
                {
                    recordsString.Append($"{item.Value} (no record)");
                }
                itemIndex++;
            }
            return recordsString.ToString();
        }

        private string MakeStrafeStyleString(UInt64[] strafeStyles)
        {

            List<KeyValuePair<MovementDir, UInt64>> strafeStylesData = new List<KeyValuePair<MovementDir, ulong>>();

            UInt64 totalDivider = 0;
            for (int i =0;i<strafeStyles.Length;i++)
            {
                totalDivider += strafeStyles[i];
                strafeStylesData.Add(new KeyValuePair<MovementDir, ulong>((MovementDir)i,strafeStyles[i]));
            }

            if (totalDivider == 0) return "";

            strafeStylesData.Sort((a, b) => { return -a.Value.CompareTo(b.Value); });

            StringBuilder strafeStylesString = new StringBuilder();
            int strafeStyleIndex = 0;
            foreach (KeyValuePair<MovementDir, UInt64> strafeStyleInfo in strafeStylesData)
            {
                if (strafeStyleInfo.Value <= 0)
                {
                    continue;
                }
                string movementName = Enum.GetName(typeof(MovementDir), strafeStyleInfo.Key); 
                if (movementName == null)
                {
                    movementName = "WEIRD";
                }
                else if ((strafeStylesString.Length + movementName.Length) > 150)
                {
                    break;
                }
                if (strafeStyleIndex != 0)
                {
                    strafeStylesString.Append(", ");
                }
                string percentageString = ((int)Math.Round((100.0 * (double)strafeStyleInfo.Value / (double)totalDivider)+0.5)).ToString();
                strafeStylesString.Append($"{percentageString}*/. {movementName}");
                strafeStyleIndex++;
            }
            return strafeStylesString.ToString();
        }

        [DelayProperty]
        public bool IsMainChatConnection { get; set; }= false;
        [DelayProperty]
        public int ChatMemeCommandsDelay { get; set; } = 4000;

        struct ParsedChatMessage
        {
            public string playerName;
            public int playerNum;
            public ChatType type;
            public string message;
            public bool commandComesFromJKWatcher;
            public bool crossServerBroadcastMessage;
        }
        Regex validSkinRegex = new Regex(@"^[a-zA-z0-9\^]+(?:\/[a-zA-z0-9\^]+)?$", RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
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

        // some mods may split messages and continue the old color..
        const string privateChatSeparatorNoColor = "^7]: ^";
        const string teamChatSeparatorNoColor = "^7): ^";
        const string publicChatSeparatorNoColor = "^7: ^";
        const string crossServerPrefix = "^d<^fto all servers^d<^7: ";
        ParsedChatMessage? ParseChatMessage(string nameChatSegmentA, string extraArgument = null, string extraArgument2 = null)
        {
            int sentNumber = -1;
            bool isCrossServerBroadcast = false;

            if (extraArgument2 != null && extraArgument2.Equals("crossServerBroadcast",StringComparison.InvariantCultureIgnoreCase))
            {
                isCrossServerBroadcast = true;
            }

            if (extraArgument != null) // This is jka only i think, sending the client num as an extra
            {
                if (extraArgument.Equals("crossServerBroadcast", StringComparison.InvariantCultureIgnoreCase))
                {
                    isCrossServerBroadcast = true;
                }
                else if (extraArgument.Equals("crossServer",StringComparison.InvariantCultureIgnoreCase))
                {
                    sentNumber = -2; // we can't possible find this player. he's not from this server.
                }
                else if (!isNumber(extraArgument))
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

            if(isCrossServerBroadcast && nameChatSegmentA.StartsWith(crossServerPrefix))
            {
                nameChatSegmentA = nameChatSegmentA.Substring(crossServerPrefix.Length);
            }

            string nameChatSegment = nameChatSegmentA;
            ChatType detectedChatType = ChatType.PUBLIC;
            if (nameChatSegment.Length < privateChatBegin.Length + 1)
            {
                return null; // Idk
            }

            string separator;
            string separatorNoColor;
            if (nameChatSegment.Substring(0, privateChatBegin.Length) == privateChatBegin)
            {
                detectedChatType = ChatType.PRIVATE;
                nameChatSegment = nameChatSegment.Substring(privateChatBegin.Length);
                separator = privateChatSeparator;
                separatorNoColor = privateChatSeparatorNoColor;
            }
            else if (nameChatSegment.Substring(0, teamChatBegin.Length) == teamChatBegin)
            {
                detectedChatType = ChatType.TEAM;
                nameChatSegment = nameChatSegment.Substring(teamChatBegin.Length);
                separator = teamChatSeparator;
                separatorNoColor = teamChatSeparatorNoColor;
            }
            else
            {
                separator = publicChatSeparator;
                separatorNoColor = publicChatSeparatorNoColor;
            }

            int separatorLength = separator.Length;

            string[] nameChatSplits = nameChatSegment.Split(new string[] { separator }, StringSplitOptions.None);

            if(nameChatSplits.Length == 1)
            {
                // try no color variants.
                string[] nameChatSplitsNoColor = nameChatSegment.Split(new string[] { separatorNoColor }, StringSplitOptions.None);
                if(nameChatSplitsNoColor.Length > 1)
                {
                    for(int i=1;i< nameChatSplitsNoColor.Length; i++)
                    {
                        if (nameChatSplitsNoColor[i].Length > 0)
                        {
                            nameChatSplitsNoColor[i] = nameChatSplitsNoColor[i].Substring(1);
                        }
                    }
                    nameChatSplits = nameChatSplitsNoColor;
                }
            }

            List<int> possiblePlayers = new List<int>();
            int botCount = 0;
            int ourselvesCount = 0;
            int[] ourNums = serverWindow.getJKWatcherClientNums();
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
                            if (playerInfo.confirmedBot) botCount++;
                            if (ourNums.Contains(playerInfo.clientNum)) ourselvesCount++; 
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
                        if (playerInfo.confirmedBot) botCount++;
                        if (ourNums.Contains(playerInfo.clientNum)) ourselvesCount++;
                    }
                }
            }


            int playerNum = -1;

            if (possiblePlayers.Count == 0)
            {
                if (sentNumber == -2)
                {
                    // from another server. cross-server
                    return null;
                }
                else if (sentNumber == -1)
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
                bool logErrors = possiblePlayers.Count > botCount && possiblePlayers.Count > ourselvesCount;
                if (sentNumber == -2)
                {
                    serverWindow.addToLog($"Could not reliably identify sender of (t)chat message, BUT THIS WAS A CROSS-SERVER MESSAGE. {possiblePlayers.Count} matches ({botCount} bots, {ourselvesCount} ours): {nameChatSegmentA}", true);
                    return null;
                }
                else if (sentNumber == -1)
                {
                    serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. {possiblePlayers.Count} matches ({botCount} bots, {ourselvesCount} ours): {nameChatSegmentA}", logErrors);
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
                        serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. {possiblePlayers.Count} matches ({botCount} bots, {ourselvesCount} ours): {nameChatSegmentA} and extra argument number {sentNumber} matched none.", logErrors);
                        return null;
                    }
                    else
                    {
                        serverWindow.addToLog($"Could not reliably identify sender of (t)chat message. {possiblePlayers.Count} matches ({botCount} bots, {ourselvesCount} ours): {nameChatSegmentA} but extra argument number {sentNumber} cleared it up.", logErrors);
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

            string messageRaw = nameChatSegment.Substring(playerName.Length + separatorLength);

            int[] ourClientNums = serverWindow.getJKWatcherClientNums();
            bool commandComesFromJKWatcher = ourClientNums.Contains(playerNum) || (messageRaw.StartsWith("   ") && detectedChatType == ChatType.PRIVATE);

            return new ParsedChatMessage() { message = messageRaw.Trim(), playerName = playerName, playerNum = playerNum, type = detectedChatType, commandComesFromJKWatcher= commandComesFromJKWatcher, crossServerBroadcastMessage=isCrossServerBroadcast };
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
        bool isNumberOrFloat(string str)
        {
            if (str.Length == 0) return false;
            foreach (char ch in str)
            {
                if (!(ch >= '0' && ch <= '9' || ch == '-' || ch == '.'))
                {
                    return false;
                }
            }
            return true;
        }

        // Maybe: taunt
        static string[] customSillyModeCommands = new string[0];

        static Connection()
        {
            byte[] customCommandData = Helpers.GetResourceData("customSillyModeCommands.txt");
            string customCommandDataString = Encoding.UTF8.GetString(customCommandData);
            customSillyModeCommands = customCommandDataString.Split(new char[] { '\n', '\r', ' ', ',' },StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        struct ConnectionDemoState
        {
            public string demoName;
            public int demoTime;
            public int myClientNum;
            public bool isStrobeOperator;
        }

        void EvaluateChat(CommandEventArgs commandEventArgs)
        {
            try
            {
                bool weAreRecordingDemo = client?.Demorecording == true;
                //if (client?.Demorecording != true) return; // Why mark demo times if we're not recording...
                if (commandEventArgs.Command.Argc >= 2)
                {

                    int? myClientNum = client?.clientNum;
                    if (!myClientNum.HasValue) return;
                    string nameChatSegment = commandEventArgs.Command.Argv(1);

                    ParsedChatMessage? pmMaybe = ParseChatMessage(nameChatSegment, commandEventArgs.Command.Argc > 2 ? commandEventArgs.Command.Argv(2) : null, commandEventArgs.Command.Argc > 3 ? commandEventArgs.Command.Argv(3) : null);

                    if (!pmMaybe.HasValue) return;

                    ParsedChatMessage pm = pmMaybe.Value;

                    if (pm.message == null) return; // Message from myself. Ignore.

                    string myName = infoPool.playerInfo[myClientNum.Value].name;
                    string myNameNoSpecialChars = myName==null ? null: playerNameSpecialCharsExceptRoofRegex.Replace(myName, "");
                    string myNameNoSpecialCharsCleanedUp = myName==null ? null: playerNameSpecialCharsExceptRoofRegex.Replace(Q3ColorFormatter.cleanupString(myName), "");
                    string myBasePlayerName = _connectionOptions.userInfoName;
                    if (_connectionOptions.userInfoName == null)
                    {
                        myBasePlayerName = "Padawan";
                    }
                    string myBasePlayerNameNoSpecialChars = myBasePlayerName == null ? null : playerNameSpecialCharsExceptRoofRegex.Replace(myBasePlayerName, "");
                    string myBasePlayerNameNoSpecialCharsCleanedUp = myBasePlayerName == null ? null : playerNameSpecialCharsExceptRoofRegex.Replace(Q3ColorFormatter.cleanupString(myBasePlayerName), "");

                    bool notifyIfMention = !pm.commandComesFromJKWatcher && !infoPool.playerInfo[pm.playerNum].confirmedBot && !infoPool.playerInfo[pm.playerNum].confirmedJKWatcherFightbot; // dont flash taskbar icon if its only some phrase from a bot or some response by us
                    if (myName != null && (pm.message.Contains(myName, StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(Q3ColorFormatter.cleanupString(myName), StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(myNameNoSpecialChars, StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(myNameNoSpecialCharsCleanedUp, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        serverWindow.addToLog($"CHAT MESSAGE POSSIBLY MENTIONS ME: {commandEventArgs.Command.Argv(1)}", false, 0, 0, notifyIfMention ? ConnectedServerWindow.MentionLevel.MentionNotify : ConnectedServerWindow.MentionLevel.Mention);
                    } else if (myBasePlayerName != null && (pm.message.Contains(myBasePlayerName, StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(Q3ColorFormatter.cleanupString(myBasePlayerName), StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(myBasePlayerNameNoSpecialChars, StringComparison.InvariantCultureIgnoreCase) || pm.message.Contains(myBasePlayerNameNoSpecialCharsCleanedUp, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        serverWindow.addToLog($"CHAT MESSAGE POSSIBLY MENTIONS ME (basename): {commandEventArgs.Command.Argv(1)}", false, 0, 0, notifyIfMention ? ConnectedServerWindow.MentionLevel.MentionNotify : ConnectedServerWindow.MentionLevel.Mention);
                    }
                    

                    if (this.HandleAutoCommands && !pm.commandComesFromJKWatcher)
                    {
                        ConditionalCommand[] conditionalCommands = _connectionOptions.conditionalCommandsParsed;
                        foreach (ConditionalCommand cmd in conditionalCommands) // TODO This seems inefficient, hmm
                        {
                            if ((!cmd.mainConnectionOnly || this.IsMainChatConnection) && cmd.type == ConditionalCommand.ConditionType.CHAT_CONTAINS && (cmd.conditionVariable1.Match(pm.message).Success || cmd.conditionVariable1.Match(Q3ColorFormatter.cleanupString(pm.message)).Success))
                            {
                                string commands = cmd.commands
                                    .Replace("$name", pm.playerName, StringComparison.OrdinalIgnoreCase)
                                    .Replace("$clientnum", pm.playerNum.ToString(), StringComparison.OrdinalIgnoreCase)
                                    .Replace("$myclientnum", this.ClientNum.GetValueOrDefault(-1).ToString(), StringComparison.OrdinalIgnoreCase);
                                string saySameCmd = "say";
                                if (pm.type == ChatType.PRIVATE)
                                {
                                    saySameCmd = $"tell {pm.playerNum}";
                                } else if (pm.type == ChatType.TEAM)
                                {
                                    saySameCmd = $"say_team";
                                } else if(pm.type == ChatType.PUBLIC && pm.crossServerBroadcastMessage && this.ttFlags.HasFlag(TommyTernalFlags.TTFLAGSSERVERINFO_HASCROSSSERVERCHAT))
                                {
                                    saySameCmd = $"say_cross";
                                }
                                ExecuteCommandList(commands, cmd.getRequestCategory(), cmd.GetSpamLevelAsRequestBehavior<string, RequestCategory>(), saySameCmd);
                            }
                        }
                    }

                    List<int> numberParams = new List<int>();
                    List<float> floatParams = new List<float>();
                    List<string> stringParams = new List<string>();

                    bool thisGameParamFound = false;

                    string demoNoteString = null;
                    { // Just putting this in its own scope to isolate the variables.
                        string[] messageBits = pm.message.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (messageBits.Length == 0) return;
                        StringBuilder demoNote = new StringBuilder();

                        int possibleNumbers = messageBits.Length > 0 && (messageBits[0].ToLowerInvariant().Contains("markas")
                            || messageBits[0].ToLowerInvariant().Contains("kills")
                            || messageBits[0].ToLowerInvariant().Contains("rets")
                            || messageBits[0].ToLowerInvariant().Contains("killtypes")
                            || messageBits[0].ToLowerInvariant().Contains("rettypes")
                            ) ? 
                            2 : 1;
                        if (messageBits[0].ToLowerInvariant().Contains("thots")
                            || messageBits[0].ToLowerInvariant().Contains("who")
                            || messageBits[0].ToLowerInvariant().Contains("choose")
                            || messageBits[0].ToLowerInvariant().Contains("botsay")
                            )
                        {
                            possibleNumbers = 0;
                        }
                        foreach (string bit in messageBits)
                        {
                            if (stringParams.Count == 1 && numberParams.Count < possibleNumbers && isNumber(bit)) // Number parameters can only occur after the initial command, for example !markas 2 3
                            {// todo make this work for float numbers. idk
                                float tmp2;
                                if (float.TryParse(bit, out tmp2)) 
                                {
                                    floatParams.Add(tmp2);
                                }
                                int tmp;
                                if (int.TryParse(bit, out tmp)) // dunno why it wouldnt be true but lets be safe
                                {
                                    numberParams.Add(tmp);
                                    continue;
                                }
                            }
                            else if (stringParams.Count >= 1)
                            {
                                if (bit.Equals("thisGame", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    thisGameParamFound = true;
                                }
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


                    //int[] ourClientNums = serverWindow.getJKWatcherClientNums();
                    bool fightBotChatHandlersExist = serverWindow.dedicatedFightBotChatHandlersExist();
                    bool weHandleFightBotCommands = (fightBotChatHandlersExist && this.HandlesFightBotChatCommands) || (!fightBotChatHandlersExist && this.IsMainChatConnection);

                    //bool commandComesFromJKWatcher = ourClientNums.Contains(pm.playerNum);
                    bool commandComesFromJKWatcher = pm.commandComesFromJKWatcher;

                    //StringBuilder response = new StringBuilder();
                    bool markRequested = false;
                    bool helpRequested = false;
                    bool reframeRequested = false;
                    bool reframeAborted = false;
                    const int maxReframeMinutes = 20;
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
                            ChatCommandAnswer(pm, "!mock !gaily !reverse !ruski !saymyname !agree !opinion !color !flipcoin !roulette", true, true, true, true);
                            ChatCommandAnswer(pm, "!who !choose !weaklegs !doomer !chicken !blocker !thots !names", true, true, true, true);
                            notDemoCommand = true;
                            // TODO Send list of meme commands
                            break;

                        // String manipulation:
                        // Private and team meme responses get their own category each so the rate limiting isn't confusing
                        case "!mock":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : (pm.crossServerBroadcastMessage ? lastCrossServerChat : lastPublicChat);
                            ChatCommandAnswer(pm, MockString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!gaily":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : (pm.crossServerBroadcastMessage ? lastCrossServerChat : lastPublicChat);
                            ChatCommandAnswer(pm, GailyString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!reverse":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : (pm.crossServerBroadcastMessage ? lastCrossServerChat : lastPublicChat);
                            ChatCommandAnswer(pm, new string(tmpString.Reverse().ToArray()), true, true, false);
                            notDemoCommand = true;
                            break;
                        case "!ruski":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            tmpString = pm.type == ChatType.TEAM ? lastTeamChat : (pm.crossServerBroadcastMessage ? lastCrossServerChat : lastPublicChat);
                            ChatCommandAnswer(pm, RuskiString(tmpString), true, true, false);
                            notDemoCommand = true;
                            break;

                        // Whatever
                        case "!saymyname":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            ChatCommandAnswer(pm, pm.playerName,true,true,true);
                            notDemoCommand = true;
                            break;
                        case "!agree":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            ChatCommandAnswer(pm, $"agreed, {pm.playerName}",true,true,true);
                            notDemoCommand = true;
                            break;
                        case "!thots":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            (string response, string reverseResponse) = Markov.GetAnyMarkovText(demoNoteString);
                            if(!string.IsNullOrWhiteSpace(response) || !string.IsNullOrWhiteSpace(reverseResponse))
                            {
                                StringBuilder sb = new StringBuilder();
                                if (!string.IsNullOrWhiteSpace(reverseResponse))
                                {
                                    sb.Append(reverseResponse);
                                    sb.Append(" ");
                                }
                                if (!string.IsNullOrWhiteSpace(demoNoteString))
                                {
                                    sb.Append(demoNoteString);
                                    sb.Append(" ");
                                }
                                if (!string.IsNullOrWhiteSpace(response))
                                {
                                    sb.Append(response);
                                }
                                ChatCommandAnswer(pm, sb.ToString(), true, true, true);
                            } else
                            {
                                ChatCommandAnswer(pm, "no thots on that", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!color":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if(numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !color with a valid client number (see /clientlist)", true, true, true,true);
                                return;
                            }
                            ChatCommandAnswer(pm, StringShowQ3Color(infoPool.playerInfo[numberParams[0]].name), true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!flipcoin":
                            if (_connectionOptions.silentMode || pm.type == ChatType.PRIVATE || !this.IsMainChatConnection) return;
                            if (stringParams.Count < 2)
                            {
                                ChatCommandAnswer(pm, "heads or tails?", true, true, false);
                            } else if(stringParams[1].ToLowerInvariant() != "heads" && stringParams[1].ToLowerInvariant() != "tails")
                            {
                                ChatCommandAnswer(pm, $"{stringParams[1]}? what's that?", true, true, false);
                            } else
                            {
                                string result = getNiceRandom(0, 2) == 1 ? "tails" : "heads";
                                if (result == stringParams[1].ToLowerInvariant())
                                {
                                    ChatCommandAnswer(pm, $"it's {result}, you win", true, true, false);
                                } else
                                {
                                    ChatCommandAnswer(pm, $"it's {result}, you lose", true, true, false);
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!opinion":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            int opinionNum = getNiceRandom(0, 11);
                            string opinion = "maybe";
                            if(opinionNum <= 4)
                            {
                                opinion = "yes";
                            }
                            else if(opinionNum <= 9)
                            {
                                opinion = "no";
                            }
                            ChatCommandAnswer(pm, opinion, true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!roulette":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            ChatCommandAnswer(pm, getNiceRandom(0, 6) == 0 ? "you played russian roulette and died" : "you played russian roulette and survived", true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!who":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (demoNoteString == null)
                            {
                                ChatCommandAnswer(pm, "who what?", true, true, true);
                            } else
                            {
                                int randomPlayer = PickRandomPlayer(maxClientsHere);
                                if (randomPlayer == -1) return;
                                ChatCommandAnswer(pm, $"{infoPool.playerInfo[randomPlayer].name} {demoNoteString}", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!choose":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (demoNoteString == null)
                            {
                                ChatCommandAnswer(pm, "choose between what?", true, true, true);
                            } else
                            {
                                string[] options = demoNoteString.Split(new string[] { " or ", " OR ", " oR ", " Or " },StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                                if(options.Length < 2)
                                {
                                    options = demoNoteString.Split(new string[] { "or", "OR", "oR", "Or" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                }
                                if(options.Length == 1)
                                {
                                    ChatCommandAnswer(pm, $"choose between {options[0]} and what?", true, true, true);
                                } else if(options.Length < 2)
                                {
                                    ChatCommandAnswer(pm, "choose between what?", true, true, true);
                                } else
                                {
                                    string answer = options[getNiceRandom(0,options.Length)];
                                    ChatCommandAnswer(pm, answer, true, true, true);
                                }

                            }
                            notDemoCommand = true;
                            break;
                        case "!weaklegs":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            double highestFallsPerMinute = 0;
                            PlayerInfo highestFallsPlayer = null;
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                if (!pi.infoValid) continue;
                                double fallsPerMinute = (double)pi.chatCommandTrackingStuff.falls / (DateTime.Now - pi.chatCommandTrackingStuff.onlineSince).TotalMinutes;
                                if (thisGameParamFound)
                                {
                                    fallsPerMinute = (double)pi.chatCommandTrackingStuffThisGame.falls / (DateTime.Now - pi.chatCommandTrackingStuffThisGame.onlineSince).TotalMinutes;
                                }
                                if (fallsPerMinute > highestFallsPerMinute)
                                {
                                    highestFallsPerMinute = fallsPerMinute;
                                    highestFallsPlayer = pi;
                                }
                            }
                            if(highestFallsPlayer == null)
                            {
                                ChatCommandAnswer(pm, $"Haven't seen anyone fall today.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"{highestFallsPlayer.name} has the weakest legs with {highestFallsPerMinute.ToString("0.##")} falls per minute.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!g2top":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            string glicko2RatingsString = MakeGlicko2RatingsString(thisGameParamFound, stringParams.Contains("all"));
                            ChatCommandAnswer(pm, $"^7^0^7Glicko2{Glicko2Version} top: {glicko2RatingsString}", true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!doomer":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            int mostDooms = 0;
                            PlayerInfo mostDoomsPlayer = null;
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                int doomKills = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.doomkills : pi.chatCommandTrackingStuff.doomkills;
                                if (pi.infoValid && doomKills > mostDooms)
                                {
                                    mostDooms = doomKills;
                                    mostDoomsPlayer = pi;
                                }
                            }
                            if(mostDoomsPlayer == null)
                            {
                                ChatCommandAnswer(pm, $"Haven't seen any dooms yet.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"{mostDoomsPlayer.name} is the biggest doomer with {mostDooms} dooms.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!chicken":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            PlayerInfo mostChickenRollsPlayer = null;
                            int mostChickenRollsPlayerTotalRolls = 0;
                            bool isCTF = currentGameType >= GameType.CTF && currentGameType <= GameType.CTY;
                            /*
                            int mostChickenRolls = 0;
                            foreach (PlayerInfo pi in infoPool.playerInfo)
                            {
                                int chickenrolls;
                                if (isCTF)
                                {
                                    chickenrolls = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.rollsWithFlag.value : pi.chatCommandTrackingStuff.rollsWithFlag.value;
                                } else
                                {
                                    chickenrolls = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.rolls.value : pi.chatCommandTrackingStuff.rolls.value;
                                }
                                if (pi.infoValid && chickenrolls > mostChickenRolls)
                                {
                                    mostChickenRolls = chickenrolls;
                                    mostChickenRollsPlayer = pi;
                                }
                            }*/
                            
                            double mostChickenRollsPerMinute = 0;
                            foreach (PlayerInfo pi in infoPool.playerInfo)
                            {
                                int chickenrollsTotal;
                                double chickenrollsPerMinute;
                                if (isCTF)
                                {
                                    chickenrollsTotal = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.rollsWithFlag.value : pi.chatCommandTrackingStuff.rollsWithFlag.value;
                                    chickenrollsPerMinute = chickenrollsTotal;
                                } else
                                {
                                    chickenrollsTotal = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.rolls.value : pi.chatCommandTrackingStuff.rolls.value;
                                    chickenrollsPerMinute = chickenrollsTotal;
                                }
                                chickenrollsPerMinute /= thisGameParamFound ? (DateTime.Now - pi.chatCommandTrackingStuffThisGame.onlineSince).TotalMinutes : (DateTime.Now - pi.chatCommandTrackingStuff.onlineSince).TotalMinutes;
                                if (pi.infoValid && chickenrollsPerMinute > mostChickenRollsPerMinute)
                                {
                                    mostChickenRollsPerMinute = chickenrollsPerMinute;
                                    mostChickenRollsPlayer = pi;
                                    mostChickenRollsPlayerTotalRolls = chickenrollsTotal;
                                }
                            }
                            if (mostChickenRollsPlayer == null)
                            {
                                ChatCommandAnswer(pm, $"Haven't seen any chicken rolls yet.", true, true, true);
                            }
                            else if(isCTF)
                            {
                                ChatCommandAnswer(pm, $"{mostChickenRollsPlayer.name} is the biggest chicken with {mostChickenRollsPerMinute.ToString("0.##")} chicken rolls per minute ({mostChickenRollsPlayerTotalRolls} total) with flag.", true, true, true);
                            }
                            else 
                            {
                                ChatCommandAnswer(pm, $"{mostChickenRollsPlayer.name} is the biggest chicken with {mostChickenRollsPerMinute.ToString("0.##")} chicken rolls per minute ({mostChickenRollsPlayerTotalRolls} total).", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!blocker":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            int mostTeamBlocks = 0;
                            int mostTeamCapperBlocks = 0;
                            int mostEnemyBlocks = 0;
                            int mostEnemyCapperBlocks = 0;
                            PlayerInfo mostFriendlyBlocksPlayer = null;
                            PlayerInfo mostEnemyBlocksPlayer = null;
                            PlayerInfo mostFriendlyCapperBlocksPlayer = null;
                            PlayerInfo mostEnemyCapperBlocksPlayer = null;
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                int blocks = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.blocksEnemy : pi.chatCommandTrackingStuff.blocksEnemy;
                                if (pi.infoValid && blocks > mostEnemyBlocks)
                                {
                                    mostEnemyBlocks = blocks;
                                    mostEnemyBlocksPlayer = pi;
                                }
                            }
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                int blocks = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.blocksFriendly : pi.chatCommandTrackingStuff.blocksFriendly;
                                if (pi.infoValid && blocks > mostTeamBlocks)
                                {
                                    mostTeamBlocks = blocks;
                                    mostFriendlyBlocksPlayer = pi;
                                }
                            }
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                int blocks = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.blocksFlagCarrierFriendly : pi.chatCommandTrackingStuff.blocksFlagCarrierFriendly;
                                if (pi.infoValid && blocks > mostTeamCapperBlocks)
                                {
                                    mostTeamCapperBlocks = blocks;
                                    mostFriendlyCapperBlocksPlayer = pi;
                                }
                            }
                            foreach(PlayerInfo pi in infoPool.playerInfo)
                            {
                                int blocks = thisGameParamFound ? pi.chatCommandTrackingStuffThisGame.blocksFlagCarrierEnemy : pi.chatCommandTrackingStuff.blocksFlagCarrierEnemy;
                                if (pi.infoValid && blocks > mostEnemyCapperBlocks)
                                {
                                    mostEnemyCapperBlocks = blocks;
                                    mostEnemyCapperBlocksPlayer = pi;
                                }
                            }

                            if (
                                 mostFriendlyBlocksPlayer == null &&
                                 mostEnemyBlocksPlayer == null &&
                                 mostFriendlyCapperBlocksPlayer == null &&
                                 mostEnemyCapperBlocksPlayer == null)
                            {
                                ChatCommandAnswer(pm, $"Haven't seen any blocks yet.", true, true, true);
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                bool hadOne = false;
                                if (mostFriendlyCapperBlocksPlayer != null)
                                {
                                    if (hadOne)
                                    {
                                        sb.Append(", ^7^0^7");
                                    }
                                    sb.Append($"own capper: {mostFriendlyCapperBlocksPlayer.name} ({mostTeamCapperBlocks})");
                                    hadOne = true;
                                }
                                if (mostFriendlyBlocksPlayer != null)
                                {
                                    if (hadOne)
                                    {
                                        sb.Append(", ^7^0^7");
                                    }
                                    sb.Append($"team: {mostFriendlyBlocksPlayer.name} ({mostTeamBlocks})");
                                    hadOne = true;
                                }
                                if (mostEnemyCapperBlocksPlayer != null)
                                {
                                    if (hadOne)
                                    {
                                        sb.Append(", ^7^0^7");
                                    }
                                    sb.Append($"enemy capper: {mostEnemyCapperBlocksPlayer.name} ({mostEnemyCapperBlocks})");
                                    hadOne = true;
                                }
                                if (mostEnemyBlocksPlayer != null)
                                {
                                    if (hadOne)
                                    {
                                        sb.Append(", ^7^0^7");
                                    }
                                    sb.Append($"enemy: {mostEnemyBlocksPlayer.name} ({mostEnemyBlocks})");
                                    hadOne = true;
                                }
                                ChatCommandAnswer(pm, $"^7^0^7Blockers: {sb.ToString()}", true, true, true);
                            }
                            notDemoCommand = true;
                            break;



                        // Defrag

                        case "defrag":
                        case "!defrag":
                            if (!this.IsMainChatConnection || (stringParams0Lower == "defrag" && pm.type != ChatType.PRIVATE)) return;
                            ChatCommandAnswer(pm, "!notwr !wr !mapwr", true, true, true, true); // todo !wr !wrholders
                            notDemoCommand = true;
                            // TODO Send list of meme commands
                            break;
                        case "!wr":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (string.IsNullOrWhiteSpace(demoNoteString))
                            {
                                ChatCommandAnswer(pm, "Usage: !wr playername [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                            }
                            else
                            {
                                string defragRecsString = MakeDefragWRMapsString(false, demoNoteString, false);
                                if(defragRecsString is null)
                                {
                                    ChatCommandAnswer(pm, "Usage: !wr playername [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7{defragRecsString}", true, true, true);
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!notwr":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (string.IsNullOrWhiteSpace(demoNoteString))
                            {
                                ChatCommandAnswer(pm, "Usage: !notwr playername [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                            }
                            else
                            {
                                string defragRecsString = MakeDefragWRMapsString(true, demoNoteString,false);
                                if(defragRecsString is null)
                                {
                                    ChatCommandAnswer(pm, "Usage: !notwr playername [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7{defragRecsString}", true, true, true);
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!mapwr":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (string.IsNullOrWhiteSpace(demoNoteString))
                            {
                                ChatCommandAnswer(pm, "Usage: !mapwr [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                            }
                            else
                            {
                                string defragRecsString = MakeDefragWRMapsString(true, demoNoteString,true);
                                if(defragRecsString is null)
                                {
                                    ChatCommandAnswer(pm, "Usage: !mapwr [page number] [timerange, e.g. 10-30 or <40] [map filter keyword]", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7{defragRecsString}", true, true, true);
                                }
                            }
                            notDemoCommand = true;
                            break;



                        // Fightbot
                        case "bot":
                        case "!bot":
                            if (!weHandleFightBotCommands || (stringParams0Lower == "bot" && pm.type != ChatType.PRIVATE)) return;
                            ChatCommandAnswer(pm, "!imscared = bot ignores you, !imveryscared = bot ignores even ppl around you", true, true, true, true);
                            ChatCommandAnswer(pm, "!botmode !imbrave !cowards !bigcowards !botsay !botsaycalm !berserker", true, true, true, true);
                            if (_connectionOptions.allowWayPointBotmode)
                            {  
                                if (_connectionOptions.allowWayPointBotmodeCommands)
                                {
                                    ChatCommandAnswer(pm, "!waypoint !waypointcommand !clearwaypoints", true, true, true, true);
                                } else
                                {
                                    ChatCommandAnswer(pm, "!waypoint !clearwaypoints", true, true, true, true);
                                }
                                ChatCommandAnswer(pm, "!waypoint !waypointcommand !clearwaypoints", true, true, true, true);
                            }
                            ChatCommandAnswer(pm, "!selfpredict !deluxepredict !fastdbs !bsdist !dbsdist !precision", true, true, true, true);
                            notDemoCommand = true;
                            break;
                        case "!cowards":
                            if (_connectionOptions.silentMode || !weHandleFightBotCommands) return;
                            else {
                                StringBuilder cowardsb = new StringBuilder();
                                int count = 0;
                                foreach (PlayerInfo pi in infoPool.playerInfo)
                                {
                                    if(pi.infoValid && pi.chatCommandTrackingStuff.fightBotIgnore)
                                    {
                                        if(count++ == 0)
                                        {
                                            cowardsb.Append($"{pi.name}");
                                        }
                                        else
                                        {
                                            cowardsb.Append($", {pi.name}");
                                        }
                                    }
                                }
                                ChatCommandAnswer(pm, cowardsb.ToString(), true, true, true);
                            }
                            break;
                        case "!bigcowards":
                            if (_connectionOptions.silentMode || !weHandleFightBotCommands) return;
                            else {
                                StringBuilder cowardsb = new StringBuilder();
                                int count = 0;
                                foreach (PlayerInfo pi in infoPool.playerInfo)
                                {
                                    if(pi.infoValid && pi.chatCommandTrackingStuff.fightBotStrongIgnore)
                                    {
                                        if(count++ == 0)
                                        {
                                            cowardsb.Append($"{pi.name}");
                                        }
                                        else
                                        {
                                            cowardsb.Append($", {pi.name}");
                                        }
                                    }
                                }
                                ChatCommandAnswer(pm, cowardsb.ToString(), true, true, true);
                            }
                            break;
                        case "!berserk":
                        case "!berserka":
                        case "!berserkah":
                        case "!berserker":
                            if (_connectionOptions.silentMode || !weHandleFightBotCommands || pm.type == ChatType.PRIVATE || commandComesFromJKWatcher) return;
                            else {
                                if(infoPool.playerInfo[pm.playerNum].team == Team.Spectator)
                                {
                                    ChatCommandAnswer(pm, "Wtf, I'm not going berserk for a spectator.", true, true, true);
                                    return;
                                }
                                else if((DateTime.Now-infoPool.lastBerserkerStarted).TotalMinutes < 10)
                                {
                                    ChatCommandAnswer(pm, "Already going berserk.", true, true, true);
                                    return;
                                }
                                else if((DateTime.Now-infoPool.lastBerserkerStarted).TotalMinutes < 60)
                                {
                                    ChatCommandAnswer(pm, "Can't go berserk this soon after the last time. Try again later.", true, true, true);
                                    return;
                                }
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.berserkerVote = true;
                                int countVotes = 0;
                                int countVotesNeeded = 0;
                                foreach (PlayerInfo pi in infoPool.playerInfo)
                                {
                                    if(pi.infoValid)
                                    {
                                        if(pi.team != Team.Spectator)
                                        {
                                            countVotesNeeded++;
                                            if (pi.chatCommandTrackingStuff.berserkerVote)
                                            {
                                                countVotes++;
                                            }
                                        }
                                    }
                                }
                                countVotesNeeded = Math.Clamp(countVotesNeeded, 1,4);
                                bool berserkStarted = false;
                                if(countVotes < countVotesNeeded)
                                {
                                    ChatCommandAnswer(pm, $"{countVotes} out of {countVotesNeeded} votes to go berserk.", true, true, true);
                                } else
                                {
                                    ChatCommandAnswer(pm, $"{countVotes} out of {countVotesNeeded} votes to go berserk, going berserk now for 10 minutes!", true, true, true);
                                    berserkStarted = true;
                                    infoPool.lastBerserkerStarted = DateTime.Now;
                                }
                                if (berserkStarted)
                                {
                                    foreach (PlayerInfo pi in infoPool.playerInfo)
                                    {
                                        pi.chatCommandTrackingStuff.berserkerVote = false;
                                    }
                                }
                            }
                            break;
                        case "!iamscared":
                        case "!imscared":
                            if (!weHandleFightBotCommands || pm.playerNum == myClientNum || commandComesFromJKWatcher) return;
                            if (_connectionOptions.noBotIgnore)
                            {
                                ChatCommandAnswer(pm, $"Can't oblige right now, my maker set me to attack everyone.", true, true, true);
                                return;
                            }
                            if (infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore)
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"Gotten a little braver, {pm.playerName}? I'm proud of you.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"Very good, {pm.playerName}. With time, you shall overcome your anxiety.", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"Do I see a hint of courage, {pm.playerName}? A good first step.", true, true, true);
                                        break;
                                }
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.wantsBotFight = false;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore = true;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore = false;
                            }
                            else if(infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore)
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"What is it, {pm.playerName}? I am already going easy on you.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"Huh? Haven't I been soft enough on you, {pm.playerName}?", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"Pardon, {pm.playerName}? Have I hit you by accident? I apologize.", true, true, true);
                                        break;
                                }
                            } else 
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"Ok, {pm.playerName}, I will spare you for this session.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"Ok, {pm.playerName}, I will go easy on you.", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"Ok, {pm.playerName}, your life shall be spared.", true, true, true);
                                        break;
                                }
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.wantsBotFight = false;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore = true;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore = false;
                            }
                            notDemoCommand = true;
                            break;
                        case "!iamveryscared":
                        case "!imveryscared":
                            if (!weHandleFightBotCommands || pm.playerNum == myClientNum || commandComesFromJKWatcher) return;
                            if (_connectionOptions.noBotIgnore)
                            {
                                ChatCommandAnswer(pm, $"Can't oblige right now, my maker set me to attack everyone.", true, true, true);
                                return;
                            }
                            if (infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore)
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"Come on now, {pm.playerName}. I'm as careful as can be around you.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"You're STILL scared, {pm.playerName}? How?!", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"You ran into that one on purpose, {pm.playerName}! Don't be so sensitive.", true, true, true);
                                        break;
                                }
                            } else
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"OK, {pm.playerName}, I'll walk on eggshells around you.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"'Fragile, Handle With Care' - {pm.playerName}. You got it.", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"That's what she said, {pm.playerName}. Ok I'll be gentle.", true, true, true);
                                        break;
                                }
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.wantsBotFight = false;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore = true;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore = true;
                            }
                            notDemoCommand = true;
                            break;
                        case "!iambrave":
                        case "!imbrave":
                            if (!weHandleFightBotCommands || pm.playerNum == myClientNum || commandComesFromJKWatcher) return;
                            if (_connectionOptions.noBotIgnore)
                            {
                                ChatCommandAnswer(pm, $"Can't oblige right now, my maker set me to attack everyone.", true, true, true);
                                return;
                            }
                            if (!infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore && !infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore && infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.wantsBotFight)
                            {
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"You are a ballsy one, {pm.playerName}, but I cannot prioritize you more.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"You have a stout heart, {pm.playerName}. But I treat each foe equally.", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"You have true courage, {pm.playerName}. If you're really looking for a fight... come closer.", true, true, true);
                                        break;
                                }
                            } else
                            {
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.wantsBotFight = true;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotIgnore = false;
                                infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.fightBotStrongIgnore = false;
                                switch (getNiceRandom(0, 3))
                                {
                                    case 0:
                                        ChatCommandAnswer(pm, $"I will come after you, {pm.playerName}.", true, true, true);
                                        break;
                                    case 1:
                                        ChatCommandAnswer(pm, $"Putting you on my kill list, {pm.playerName}.", true, true, true);
                                        break;
                                    case 2:
                                        ChatCommandAnswer(pm, $"We'll see about that, {pm.playerName}.", true, true, true);
                                        break;
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!botmode":
                            if (!weHandleFightBotCommands && !amNotInSpec || pm.type == ChatType.PRIVATE) return;
                            if (demoNoteString == null)
                            {
                                ChatCommandAnswer(pm, "Available modes: silly, dbs, grip, speed, speedrage, speedragebs, lover, custom,", true, true, true);
                                if (_connectionOptions.allowWayPointBotmode)
                                {
                                    ChatCommandAnswer(pm, "bluebs, gripbluebs, waypoints, speedabsorb, assassin", true, true, true);
                                } else
                                {
                                    ChatCommandAnswer(pm, "bluebs, gripbluebs, speedabsorb, assassin", true, true, true);
                                }
                            } else if (stringParams.Count < 2 || !(new string[] { "silly", "grip", "dbs", "speed", "speedrage", "speedragebs", "lover", "custom", "speedabsorb", "assassin", "waypoints", "bluebs", "gripbluebs" }).Contains(stringParams[1].ToLower()))
                            {
                                ChatCommandAnswer(pm, $"Unknown mode {stringParams[1]}", true, true, true);
                            }
                            else if (stringParams[1].ToLower() == "custom" && stringParams.Count < 3)
                            {
                                ChatCommandAnswer(pm, $"For custom botmode please choose a command to execute near players. Example: !botmode custom amhug", true, true, true, true);
                                ChatCommandAnswer(pm, $"You can also specify a skin as an extra parameter like: !botmode custom amhug reelo/default", true, true, true, true);
                            }
                            else if (stringParams[1].ToLower() == "custom" && !customSillyModeCommands.Contains(stringParams[2].ToLower()))
                            {
                                ChatCommandAnswer(pm, $"{stringParams[2]} command is not whitelisted for custom botmode.", true, true, true);
                            }
                            else if (stringParams[1].ToLower() == "custom" && stringParams.Count >= 4 && !validSkinRegex.Match(stringParams[3]).Success)
                            {
                                ChatCommandAnswer(pm, $"{stringParams[3]} is not a valid skin name.", true, true, true);
                            }
                            else
                            {
                                string skin = "kyle/default";
                                string forcePowers = Client.forcePowersAllDark;
                                bool modeDisallowed = false;
                                switch (stringParams[1].ToLower()) {
                                    case "silly":
                                        infoPool.sillyMode = SillyMode.SILLY;
                                        this.client.Skin = "kyle/default";
                                        break;
                                    case "dbs":
                                        infoPool.sillyMode = SillyMode.DBS;
                                        break;
                                    case "bluebs":
                                        infoPool.sillyMode = SillyMode.BLUBS;
                                        break;
                                    case "waypoints":
                                    case "walkwaypoints":
                                        if (_connectionOptions.allowWayPointBotmode)
                                        {
                                            infoPool.sillyMode = SillyMode.WALKWAYPOINTS;
                                            forcePowers = Client.forcePowersJKClientDefault;
                                        }
                                        else
                                        {
                                            modeDisallowed = true;
                                        }
                                        break;
                                    case "lover":
                                        infoPool.sillyMode = SillyMode.LOVER;
                                        skin = "ugnaught/default";
                                        if (getNiceRandom(0, 100) == 0) // From rarest to most common
                                        {
                                            skin = "jan";
                                        } else if (getNiceRandom(0, 20) == 0)
                                        {
                                            skin = "lando";
                                        } else if (getNiceRandom(0, 5) == 0)
                                        {
                                            skin = "morgan";
                                        }
                                        break;
                                    case "custom":
                                        if (stringParams.Count >= 3 && customSillyModeCommands.Contains(stringParams[2].ToLower())) { // I know we checked above but lets make sure. We're kinda allowing custom user input here. ALWAYS be very careful.
                                            infoPool.sillyMode = SillyMode.CUSTOM;
                                            infoPool.sillyModeCustomCommand = stringParams[2];
                                            if(stringParams.Count >= 4 && validSkinRegex.Match(stringParams[3]).Success)
                                            {
                                                skin = stringParams[3];
                                            }
                                        }
                                        break;
                                    case "grip":
                                        infoPool.sillyMode = SillyMode.GRIPKICKDBS;
                                        infoPool.gripDbsMode = GripKickDBSMode.VANILLA;
                                        break;
                                    case "gripbluebs":
                                        infoPool.sillyMode = SillyMode.GRIPKICKBLUBS;
                                        infoPool.gripDbsMode = GripKickDBSMode.VANILLA;
                                        break;
                                    case "speed":
                                        infoPool.sillyMode = SillyMode.GRIPKICKDBS;
                                        infoPool.gripDbsMode = GripKickDBSMode.SPEED;
                                        break;
                                    case "speedrage":
                                        infoPool.sillyMode = SillyMode.GRIPKICKDBS;
                                        infoPool.gripDbsMode = GripKickDBSMode.SPEEDRAGE;
                                        break;
                                    case "speedragebs":
                                        infoPool.sillyMode = SillyMode.GRIPKICKDBS;
                                        infoPool.gripDbsMode = GripKickDBSMode.SPEEDRAGEBS;
                                        break;
                                    case "speedabsorb":
                                        infoPool.sillyMode = SillyMode.ABSORBSPEED;
                                        infoPool.gripDbsMode = GripKickDBSMode.VANILLA;
                                        forcePowers = Client.forcePowersAllLight;
                                        break;
                                    case "assassin":
                                        infoPool.sillyMode = SillyMode.MINDTRICKSPEED;
                                        infoPool.gripDbsMode = GripKickDBSMode.VANILLA;
                                        forcePowers = Client.forcePowersAllLight;
                                        break;
                                }
                                if (!modeDisallowed)
                                {
                                    this.client.SkipUserInfoUpdatesAfterNextNChanges(1);
                                    bool doKill = false;
                                    if (this.client.GetUserInfoKeyValue("forcepowers", out _) != forcePowers)
                                    {
                                        doKill = true;
                                    }
                                    this.client.SetUserInfoKeyValue("forcepowers", forcePowers);
                                    this.client.Skin = skin;
                                    if (doKill)
                                    {
                                        leakyBucketRequester.requestExecution("kill", RequestCategory.FIGHTBOT_QUEUED, 5, 2000, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE);
                                    }

                                    ChatCommandAnswer(pm, $"{demoNoteString} mode activated.", true, true, true);
                                } else
                                {
                                    ChatCommandAnswer(pm, $"{demoNoteString} mode not allowed.", true, true, true);
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!botsay":
                        case "!botsaycalm":
                            if (!weHandleFightBotCommands && !amNotInSpec) return; // Bots that are ingame should always respond.
                            if (pm.playerNum == myClientNum) return; // Avoid long loops lol.
                            if (demoNoteString == null)
                            {
                                ChatCommandAnswer(pm, stringParams0Lower == "!botsaycalm" ? "What should I say while standing still?" : "What should I say?", true, true, true,true);
                            } 
                            else
                            {
                                if(stringParams0Lower == "!botsaycalm")
                                {
                                    if(pm.type == ChatType.PUBLIC)
                                    {
                                        if (pm.crossServerBroadcastMessage && this.ttFlags.HasFlag( TommyTernalFlags.TTFLAGSSERVERINFO_HASCROSSSERVERCHAT))
                                        {
                                            calmSayQueue.Enqueue($"say_cross {demoNoteString}");
                                        }
                                        else
                                        {
                                            calmSayQueue.Enqueue($"say {demoNoteString}");
                                        }
                                    } else if(pm.type == ChatType.TEAM)
                                    {
                                        calmSayQueue.Enqueue($"say_team {demoNoteString}");
                                    } else if(pm.type == ChatType.PRIVATE)
                                    {
                                        calmSayQueue.Enqueue($"say {pm.playerName} wants me to say: {demoNoteString}");
                                    }
                                }
                                else
                                {
                                    if(pm.type == ChatType.PRIVATE)
                                    {
                                        ChatCommandAnswer(pm, $"{pm.playerName} wants me to say: {demoNoteString}", true, true, true,false,ChatType.PUBLIC);
                                    } else
                                    {
                                        ChatCommandAnswer(pm, demoNoteString, true, true, true);
                                    }
                                }
                            }
                            notDemoCommand = true;
                            break;
                        case "!bsdist":
                        case "!dbsdist":
                            if (!weHandleFightBotCommands) return;
                            bool isBs = stringParams0Lower == "!bsdist";
                            if (numberParams.Count > 0)
                            {
                                int dbsDist = numberParams[0];
                                if(dbsDist < 32 || dbsDist > (128+16))
                                {
                                    ChatCommandAnswer(pm, (isBs? "" : "D")+"BS trigger distance can't be below 32 or above 144", true, true, true);
                                }
                                else
                                {
                                    if(isBs)
                                    {
                                        infoPool.bsTriggerDistance = dbsDist;
                                    }
                                    else
                                    {
                                        infoPool.dbsTriggerDistance = dbsDist;
                                    }
                                    ChatCommandAnswer(pm, "Gotcha, triggering " + (isBs ? "" : "d") + $"bs at {dbsDist} now.", true, true, true);
                                }
                            }
                            else
                            {
                                ChatCommandAnswer(pm, "What distance should "+ (isBs ? "" : "d") + "bs get triggered at?", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!selfpredict":
                            if (!weHandleFightBotCommands) return;
                            if (numberParams.Count > 0)
                            {
                                infoPool.selfPredict = numberParams[0] > 0;
                                ChatCommandAnswer(pm, $"Self-predict set to {infoPool.selfPredict}.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Self-predict is currently {infoPool.selfPredict}. Use 1/0 to enable/disable.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!deluxepredict":
                            if (!weHandleFightBotCommands) return;
                            if (floatParams.Count > 0 && floatParams[0] >= 0.0f && floatParams[0] <= 1.0f)
                            {
                                infoPool.deluxePredict = floatParams[0];
                                ChatCommandAnswer(pm, $"Deluxe predict set to {infoPool.deluxePredict}.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Deluxe predict is currently {infoPool.deluxePredict}. Use number from 0.0 to 1.0 to enable/disable.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!fastdbs":
                            if (!weHandleFightBotCommands) return;
                            if (numberParams.Count > 0)
                            {
                                infoPool.fastDbs = numberParams[0] > 0;
                                ChatCommandAnswer(pm, $"Fast DBS set to {infoPool.fastDbs}.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Fast DBS is currently {infoPool.fastDbs}. Use 1/0 to enable/disable.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!precision":
                            if (!weHandleFightBotCommands) return;
                            if (numberParams.Count > 0 && numberParams[0] >= -1 && numberParams[0] <= 1)
                            {
                                switch(numberParams[0])
                                {
                                    case 0:
                                        infoPool.precision = SaberPrecision.Legacy;
                                        break;
                                    case 1:
                                        infoPool.precision = SaberPrecision.Precise;
                                        break;
                                    case -1:
                                        infoPool.precision = SaberPrecision.Random;
                                        break;
                                }
                                ChatCommandAnswer(pm, $"Precision set to {Enum.GetName(infoPool.precision)}.", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Precision is currently {infoPool.precision}. Use 1/0 to enable/disable or -1 to set to random.", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!waypointcommand":
                            if (_connectionOptions.allowWayPointBotmode && _connectionOptions.allowWayPointBotmodeCommands) { 
                                if (!weHandleFightBotCommands) return;
                                if (demoNoteString == null)
                                {
                                    ChatCommandAnswer(pm, "!waypointcommand requires a command to be excuted.", true, true, true, true);
                                }
                                else if (!customSillyModeCommands.Contains(demoNoteString.ToLower().Trim()))
                                {
                                    ChatCommandAnswer(pm, $"{demoNoteString} command is not whitelisted.", true, true, true, true);
                                }
                                else
                                {
                                    if (infoPool.playerInfo[pm.playerNum].lastFullPositionUpdate.HasValue && (DateTime.Now - infoPool.playerInfo[pm.playerNum].lastFullPositionUpdate.Value).TotalMilliseconds < 500)
                                    {
                                        lock (infoPool.wayPoints)
                                        {
                                            infoPool.wayPoints.Add(new WayPoint() { command = demoNoteString });
                                        }
                                        ChatCommandAnswer(pm, $"Waypoint added.", true, true, true);
                                    }
                                    else
                                    {
                                        ChatCommandAnswer(pm, $"Can't set waypoint, I don't see you.", true, true, true);
                                    }
                                    notDemoCommand = true; 
                                }
                            }
                            break;
                        case "!waypoint":
                            if (_connectionOptions.allowWayPointBotmode) { 
                                if (!weHandleFightBotCommands) return;
                                if (infoPool.playerInfo[pm.playerNum].lastFullPositionUpdate.HasValue && (DateTime.Now-infoPool.playerInfo[pm.playerNum].lastFullPositionUpdate.Value).TotalMilliseconds < 500)
                                {
                                    Vector3 newWp = infoPool.playerInfo[pm.playerNum].position;
                                    lock (infoPool.wayPoints)
                                    {
                                        infoPool.wayPoints.Add(new WayPoint {origin= newWp });
                                    }
                                    ChatCommandAnswer(pm, $"Waypoint added.", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"Can't set waypoint, I don't see you.", true, true, true);
                                }
                                notDemoCommand = true;
                            }
                            break;
                        case "!clearwaypoints":
                            if (_connectionOptions.allowWayPointBotmode) { 
                                if (!weHandleFightBotCommands) return;
                                lock (infoPool.wayPoints)
                                {
                                    infoPool.wayPoints.Clear();
                                }
                                ChatCommandAnswer(pm, $"Waypoints cleared.", true, true, true);
                                notDemoCommand = true;
                            }
                            break;


                        // Tools
                        case "tools":
                        case "!tools":
                            if (!this.IsMainChatConnection || (stringParams0Lower == "tools" && pm.type != ChatType.PRIVATE)) return;
                            ChatCommandAnswer(pm, "!kills !killsOn !killedBy !kd !match !resetmatch !endmatch !matchstate", true, true, true, true);
                            ChatCommandAnswer(pm, "!rets !retsOn !retBy !retRatio !killTypes !retTypes !strafeStyle !g2 !g2top", true, true, true, true);
                            ChatCommandAnswer(pm, "(add 'thisgame' at end to get stats for current game)", true, true, true, true);
                            notDemoCommand = true;
                            break;

                        case "!kills":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                if(stringParams.Count > 1 && stringParams[1] == "all")
                                {
                                    if (thisGameParamFound)
                                    {
                                        ChatCommandAnswer(pm, $"Total witnessed K/D (this game) for {infoPool.playerInfo[pm.playerNum].name}: {infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuffThisGame.totalKills}/{infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuffThisGame.totalDeaths}", true, true, true);
                                    } else
                                    {
                                        ChatCommandAnswer(pm, $"Total witnessed K/D for {infoPool.playerInfo[pm.playerNum].name}: {infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.totalKills}/{infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.totalDeaths}", true, true, true);
                                    }
                                } else
                                {
                                    ChatCommandAnswer(pm, "Call !kills with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                }
                                return;
                            }
                            if(numberParams.Count == 2)
                            {
                                if(numberParams[1] < 0 || numberParams[1] >= maxClientsHere || !infoPool.playerInfo[numberParams[1]].infoValid)
                                {
                                    ChatCommandAnswer(pm, "Call !kills with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                    return;
                                }
                                if (thisGameParamFound)
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed kills (this game): {infoPool.playerInfo[numberParams[0]].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {infoPool.killTrackersThisGame[numberParams[0], numberParams[1]].kills}-{infoPool.killTrackersThisGame[numberParams[1], numberParams[0]].kills}", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed kills: {infoPool.playerInfo[numberParams[0]].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {infoPool.killTrackers[numberParams[0], numberParams[1]].kills}-{infoPool.killTrackers[numberParams[1], numberParams[0]].kills}", true, true, true);
                                }
                            } else
                            {
                                if (thisGameParamFound)
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed kills (this game): {infoPool.playerInfo[pm.playerNum].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {infoPool.killTrackersThisGame[pm.playerNum, numberParams[0]].kills}-{infoPool.killTrackersThisGame[numberParams[0], pm.playerNum].kills}", true, true, true);
                                } else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed kills: {infoPool.playerInfo[pm.playerNum].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {infoPool.killTrackers[pm.playerNum, numberParams[0]].kills}-{infoPool.killTrackers[numberParams[0], pm.playerNum].kills}", true, true, true);
                                }
                            }
                            
                            notDemoCommand = true;
                            break;
                        case "!dbs":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, $"^7^0^7DBS kill percentages: {MakeDBSPercentagesString(thisGameParamFound, false)}", true, true, true);
                                return;
                            } else if(numberParams.Count >= 1)
                            {
                                PlayerInfo player = infoPool.playerInfo[numberParams[0]];
                                ChatCommandTrackingStuff trackingStuff = thisGameParamFound ? player.chatCommandTrackingStuffThisGame : player.chatCommandTrackingStuff;
                                int dbsCount = trackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_BACK_CR_GENERAL);
                                Dictionary<string, int> killtypes = trackingStuff.GetKillTypesShortname();
                                int dbsKills = killtypes.ContainsKey("DBS") ? killtypes["DBS"] : 0;
                                float percentage = dbsCount > 0 ? 100.0f * (float)dbsKills / (float)dbsCount : 0.0f;
                                string percentString = percentage.ToString("#.00");
                                if (dbsCount > 0)
                                {
                                    ChatCommandAnswer(pm, $"DBS kill percentage for {player.name} is {percentString}% ({dbsKills} DBS kills out of {dbsCount} DBS)", true, true, true);
                                }
                            }
                            
                            notDemoCommand = true;
                            break;
                        case "!rets":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                if(stringParams.Count > 1 && stringParams[1] == "all")
                                {
                                    if (thisGameParamFound)
                                    {
                                        ChatCommandAnswer(pm, $"Total witnessed returns/returneds (this game) for {infoPool.playerInfo[pm.playerNum].name}: {infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuffThisGame.returns}/{infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuffThisGame.returned}", true, true, true);
                                    }
                                    else
                                    {
                                        ChatCommandAnswer(pm, $"Total witnessed returns/returneds for {infoPool.playerInfo[pm.playerNum].name}: {infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.returns}/{infoPool.playerInfo[pm.playerNum].chatCommandTrackingStuff.returned}", true, true, true);
                                    }
                                } else
                                {
                                    ChatCommandAnswer(pm, "Call !rets with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                }
                                return;
                            }
                            if(numberParams.Count == 2)
                            {
                                if(numberParams[1] < 0 || numberParams[1] >= maxClientsHere || !infoPool.playerInfo[numberParams[1]].infoValid)
                                {
                                    ChatCommandAnswer(pm, "Call !rets with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                    return;
                                }
                                if (thisGameParamFound)
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed returns (this game): {infoPool.playerInfo[numberParams[0]].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {infoPool.killTrackersThisGame[numberParams[0], numberParams[1]].returns}-{infoPool.killTrackersThisGame[numberParams[1], numberParams[0]].returns}", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed returns: {infoPool.playerInfo[numberParams[0]].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {infoPool.killTrackers[numberParams[0], numberParams[1]].returns}-{infoPool.killTrackers[numberParams[1], numberParams[0]].returns}", true, true, true);
                                }
                            } else
                            {
                                if (thisGameParamFound)
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed returns (this game): {infoPool.playerInfo[pm.playerNum].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {infoPool.killTrackersThisGame[pm.playerNum, numberParams[0]].returns}-{infoPool.killTrackersThisGame[numberParams[0], pm.playerNum].returns}", true, true, true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, $"^7^0^7Total witnessed returns: {infoPool.playerInfo[pm.playerNum].name} ^7^0^7vs. {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {infoPool.killTrackers[pm.playerNum, numberParams[0]].returns}-{infoPool.killTrackers[numberParams[0], pm.playerNum].returns}", true, true, true);
                                }
                            }
                            
                            notDemoCommand = true;
                            break;
                        case "!kd":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !kd with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if (thisGameParamFound)
                            {
                                ChatCommandAnswer(pm, $"Total witnessed K/D (this game) for {infoPool.playerInfo[numberParams[0]].name}: {infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.totalKills}/{infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.totalDeaths}", true, true, true);
                            } else
                            {
                                ChatCommandAnswer(pm, $"Total witnessed K/D for {infoPool.playerInfo[numberParams[0]].name}: {infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.totalKills}/{infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.totalDeaths}", true, true, true);
                            }

                            notDemoCommand = true;
                            break;
                        case "!names":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !names with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            string[] names = infoPool.playerInfo[numberParams[0]].session.GetUsedNames();
                            StringBuilder sb2 = new StringBuilder();
                            int namesAdded = 0;
                            string countQualifier = "";
                            foreach(string name in names)
                            {
                                if(name != infoPool.playerInfo[numberParams[0]].name)
                                {
                                    if ((sb2.Length + name.Length) > 200)
                                    {
                                        countQualifier = $"{namesAdded}/{names.Length} ";
                                        break;
                                    }
                                    sb2.Append(namesAdded > 0 ? " ," : "");
                                    sb2.Append(name);
                                    namesAdded++;
                                }
                            }
                            ChatCommandAnswer(pm, $"{countQualifier}{infoPool.playerInfo[numberParams[0]].name} other names: {sb2.ToString()}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!g2":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !g2 with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if (thisGameParamFound)
                            {
                                infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults, true, infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.rating);
                                ChatCommandAnswer(pm, $"^7^0^7Glicko2 rating (this game) for {infoPool.playerInfo[numberParams[0]].name}: {(int)infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.rating.GetRating(true)}+-{(int)infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.rating.GetRatingDeviation(true)}", true, true, true);
                            } else
                            {
                                infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame, true, infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.rating);
                                ChatCommandAnswer(pm, $"^7^0^7Glicko2 rating for {infoPool.playerInfo[numberParams[0]].name}: {(int)infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.rating.GetRating(true)}+-{(int)infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.rating.GetRatingDeviation(true)}", true, true, true);
                            }

                            notDemoCommand = true;
                            break;
                        case "!retratio":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !retRatio with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if (thisGameParamFound)
                            {
                                ChatCommandAnswer(pm, $"Total witnessed returns/returneds (this game) for {infoPool.playerInfo[numberParams[0]].name}: {infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.returns}/{infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.returned}", true, true, true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Total witnessed returns/returneds for {infoPool.playerInfo[numberParams[0]].name}: {infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.returns}/{infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.returned}", true, true, true);
                            }

                            notDemoCommand = true;
                            break;
                        case "!strafestyle":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !strafeStyle with a client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            string strafeStylesString = MakeStrafeStyleString(thisGameParamFound ? (UInt64[])infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.strafeStyleSamples.Clone() : (UInt64[])infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.strafeStyleSamples.Clone());

                            ChatCommandAnswer(pm, $"^7^0^7Witnessed strafe style for {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {strafeStylesString}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!killtypes":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !killTypes with one or two valid client numbers (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if (numberParams.Count == 2)
                            {
                                if (numberParams[1] < 0 || numberParams[1] >= maxClientsHere || !infoPool.playerInfo[numberParams[1]].infoValid)
                                {
                                    ChatCommandAnswer(pm, "Call !killTypes with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                    return;
                                }

                                string killTypesString = MakeKillTypesString(thisGameParamFound ? infoPool.killTrackersThisGame[numberParams[0], numberParams[1]].GetKillTypes() : infoPool.killTrackers[numberParams[0], numberParams[1]].GetKillTypes());

                                ChatCommandAnswer(pm, $"^7^0^7Total witnessed kill types: {infoPool.playerInfo[numberParams[0]].name} ^7^0^7on {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {killTypesString}", true, true, true);
                            }
                            else
                            {
                                string killTypesString = MakeKillTypesString(thisGameParamFound ? infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.GetKillTypes() : infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.GetKillTypes());
                                ChatCommandAnswer(pm, $"^7^0^7Total witnessed kill types for {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {killTypesString}", true, true, true);
                            }

                            notDemoCommand = true;
                            break;
                        case "!rettypes":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !retTypes with one or two valid client numbers (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if (numberParams.Count == 2)
                            {
                                if (numberParams[1] < 0 || numberParams[1] >= maxClientsHere || !infoPool.playerInfo[numberParams[1]].infoValid)
                                {
                                    ChatCommandAnswer(pm, "Call !retTypes with 1 or 2 valid client numbers (see /clientlist) or 'all' (for your K/D)", true, true, true, true);
                                    return;
                                }

                                string killTypesString = MakeKillTypesString(thisGameParamFound ? infoPool.killTrackersThisGame[numberParams[0], numberParams[1]].GetKillTypesReturns()  : infoPool.killTrackers[numberParams[0], numberParams[1]].GetKillTypesReturns());

                                ChatCommandAnswer(pm, $"^7^0^7Total witnessed return types: {infoPool.playerInfo[numberParams[0]].name} ^7^0^7on {infoPool.playerInfo[numberParams[1]].name}^7^0^7: {killTypesString}", true, true, true);
                            }
                            else
                            {
                                string killTypesString = MakeKillTypesString(thisGameParamFound ? infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuffThisGame.GetKillTypesReturns() : infoPool.playerInfo[numberParams[0]].chatCommandTrackingStuff.GetKillTypesReturns());
                                ChatCommandAnswer(pm, $"^7^0^7Total witnessed return types for {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {killTypesString}", true, true, true);
                            }

                            notDemoCommand = true;
                            break;
                        case "!killson":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !killson with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }

                            string killsOnString = MakeKillsOnString(thisGameParamFound,numberParams[0],false,false);
                            ChatCommandAnswer(pm, $"^7^0^7Victims of {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {killsOnString}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!killedby":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !killedby with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }

                            string killedByString = MakeKillsOnString(thisGameParamFound,numberParams[0],true,false);
                            ChatCommandAnswer(pm, $"^7^0^7Killers of {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {killedByString}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!retson":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !retson with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }

                            string retsOnString = MakeKillsOnString(thisGameParamFound,numberParams[0],false,true);
                            ChatCommandAnswer(pm, $"^7^0^7Returned cappers by {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {retsOnString}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!retby":
                            if (_connectionOptions.silentMode || !this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !retby with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }

                            string rutByString = MakeKillsOnString(thisGameParamFound,numberParams[0],true,true);
                            ChatCommandAnswer(pm, $"^7^0^7Returners of {infoPool.playerInfo[numberParams[0]].name}^7^0^7: {rutByString}", true, true, true);

                            notDemoCommand = true;
                            break;
                        case "!match":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !match with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                ChatCommandAnswer(pm, $"Already tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !resetmatch or !endmatch?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch = true;
                                }
                                ChatCommandAnswer(pm, $"Now tracking match against {infoPool.playerInfo[numberParams[0]].name}", true, true, true,true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!matchstate":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !matchstate with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                ChatCommandAnswer(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                ChatCommandAnswer(pm, $"Your match against {infoPool.playerInfo[numberParams[0]].name}: {infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills}-{infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths}", true, true, true);
                            }
                            notDemoCommand = true;
                            break;
                        case "!resetmatch":
                            if (!this.IsMainChatConnection) return;
                            if (numberParams.Count == 0 || numberParams[0] < 0 || numberParams[0] >= maxClientsHere || !infoPool.playerInfo[numberParams[0]].infoValid)
                            {
                                ChatCommandAnswer(pm, "Call !resetmatch with a valid client number (see /clientlist)", true, true, true, true);
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                ChatCommandAnswer(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills = 0;
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths = 0;
                                }
                                ChatCommandAnswer(pm, $"Match against {infoPool.playerInfo[numberParams[0]].name} reset to 0-0", true, true, true,true);
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
                                    ChatCommandAnswer(pm, $"Ended tracking of all matches. ({countEnded} affected)", true, true, true,true);
                                }
                                else
                                {
                                    ChatCommandAnswer(pm, "Call !endmatch with a valid client number (see /clientlist) or 'all'", true, true, true, true);
                                }
                                return;
                            }
                            if(!infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch)
                            {
                                ChatCommandAnswer(pm, $"Not tracking match against {infoPool.playerInfo[numberParams[0]].name}. Want !match?", true, true, true,true);
                            }
                            else
                            {
                                lock (infoPool.killTrackers)
                                {
                                    infoPool.killTrackers[pm.playerNum, numberParams[0]].trackingMatch = false;
                                    ChatCommandAnswer(pm, $"Match against {infoPool.playerInfo[numberParams[0]].name} ended at {infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchKills}-{infoPool.killTrackers[pm.playerNum, numberParams[0]].trackedMatchDeaths}.", true, true, true, true);
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
                                if (!_connectionOptions.silentMode && pm.playerNum != myClientNum && !pm.commandComesFromJKWatcher)
                                {
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Options: !demohelp (demo commands), !memes (meme commands), !tools (tool commands), !defrag (df commands)\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    /*leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   I don't understand your command. Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);*/
                                }
                                return;
                            } else if(pm.type == ChatType.PUBLIC && pm.message.Trim().Length > 0)
                            {
                                if (pm.crossServerBroadcastMessage)
                                {
                                    lastCrossServerChat = pm.message;
                                    lastCrossServerChatTime = DateTime.Now;
                                }
                                else
                                {
                                    lastPublicChat = pm.message;
                                    lastPublicChatTime = DateTime.Now;
                                }
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
                                if (!_connectionOptions.silentMode && pm.playerNum != myClientNum && !pm.commandComesFromJKWatcher)
                                {
                                    leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Options: !demohelp (demo commands), !memes (meme commands), !tools (tool commands), !defrag (df commands)\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
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

                            if (!weAreRecordingDemo) return;
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

                            if (!weAreRecordingDemo) return;
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

                    if (helpRequested && !pm.commandComesFromJKWatcher)
                    {
                        serverWindow.addToLog($"help requested by \"{pm.playerName}\" (clientNum {pm.playerNum})\n");
                        if (!_connectionOptions.silentMode && this.IsMainChatConnection)
                        {
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Here is your help: Type !demohelp for help or !mark to mark a time point for a demo.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   If you want a long demo, add the amount of past minutes after !mark, like this: !mark 10\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                            leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   If you also want the demo reframed to your perspective, use !markme instead.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                        }
                        return;
                    }

                    //int demoTime = (client?.DemoCurrentTime).GetValueOrDefault(0);
                    //int demoTime = (client?.DemoCurrentTimeApproximate).GetValueOrDefault(0);

                    string rateLimited = "";
                    string asClientNum = reframeClientNum != pm.playerNum ? $"(as {reframeClientNum})" : "";
                    if(reframeRequested && markMinutes > maxReframeMinutes)
                    {
                        reframeAborted = true;
                    }
                    string withReframe = reframeRequested ? $"+reframe{asClientNum}" : "";
                    int cutDemosCount = 0;
                    List<int> demoCutsTimes = new List<int>();
                    if (markRequested)
                    {

                        bool serverSendsAllEntities = infoPool.serverSendsAllEntities;
                        List<ConnectionDemoState> demoStates = new List<ConnectionDemoState>();
                        Connection[] allConns = serverWindow.getAllConnections();
                        Connection firstActiveConnection = null;
                        foreach(Connection oneConn in allConns)
                        {
                            if(firstActiveConnection == null && oneConn.client?.Status == ConnectionStatus.Active) firstActiveConnection = oneConn; 
                            ConnectionDemoState demoState = new ConnectionDemoState() { 
                                demoName = oneConn.client?.AbsoluteDemoName, 
                                demoTime = (oneConn.client?.DemoCurrentTimeApproximate).GetValueOrDefault(0), 
                                myClientNum=(oneConn.client?.clientNum).GetValueOrDefault(-1),
                                isStrobeOperator=(oneConn.CameraOperator is CameraOperators.StrobeCameraOperator) ||(oneConn.CameraOperator as CameraOperators.CTFCameraOperatorRedBlue)?.StrobeConnection == oneConn 
                            };
                            if (demoState.demoName != null && demoState.demoTime != 0) // Even if it's truly 0, what could someone possibly get out of a mark when the demo recording literally just started?
                            {
                                demoStates.Add(demoState);
                            }
                        }

                        // We only want to process demo requests from the first connection in the array that is active and connected, to deduplicate.
                        // we don't wanna use the mainchatconnection thing just for safety, in case the main chat connection happens to not be properly connected for whatever reason, cuz then it would never receive this demo request and mark would be totally lost.
                        // Exception for needing this to be the main connection: It's a private message. Then just process it whenever.
                        if (pm.type != ChatType.PRIVATE && ( firstActiveConnection == null || this != firstActiveConnection)) return; 

                        ServerInfo thisServerInfo = client?.ServerInfo;
                        if (thisServerInfo == null)
                        {
                            return;
                        }
                        StringBuilder demoCutCommand = new StringBuilder();
                        StringBuilder demoCutCommandReframes = new StringBuilder();
                        DateTime now = DateTime.Now;

                        // Rate limit multiple overlapping demos. It might not help against an informed griefer who understands the system
                        // but it will cut down the amount of space used for demos when people accidentally write nonsensical commmands or write long
                        // mark times because they don't know any better or they want to just capture everything and make sure they didn't miss anything
                        // This way they will end up with their session split into multiple demos but it will all be there and 
                        // not a lot of duplicate demo waste.
                        if (demoRateLimiters[pm.playerNum].lastRequest.HasValue)
                        {
                            double minutesSinceLast = (DateTime.Now - demoRateLimiters[pm.playerNum].lastRequest.Value).TotalMinutes;
                            if (minutesSinceLast < (double)markMinutes)
                            {
                                demoRateLimiters[pm.playerNum].overlapCount++;
                                if(demoRateLimiters[pm.playerNum].overlapCount > 1) 
                                {
                                    // We will allow ONE correction/overlap. After that we just cut it down minutes since last rounded up.
                                    // Just to prevent ppl who spam ridiculous times.
                                    int oldMarkMinutes = markMinutes;
                                    markMinutes = (int)Math.Ceiling(minutesSinceLast);
                                    rateLimited = $"(ratelimited from {oldMarkMinutes} minutes, last request {minutesSinceLast} minutes ago)";
                                } else
                                {
                                    rateLimited = $"(ratelimit overlap #{demoRateLimiters[pm.playerNum].overlapCount} allowed, last request {minutesSinceLast} minutes ago)";
                                }
                            } else
                            {
                                demoRateLimiters[pm.playerNum].overlapCount = 0;
                            }
                        }
                        demoRateLimiters[pm.playerNum].lastRequest = DateTime.Now;

                        string safePlayerName = Helpers.DemoCuttersanitizeFilename(pm.playerName,false);
                        demoCutCommand.Append($"# demo cut{withReframe} requested by \"{safePlayerName}\" (clientNum {pm.playerNum}) on {thisServerInfo.HostName} ({thisServerInfo.Address.ToString()}) at {now}, {markMinutes} minute(s) into the past {rateLimited}\n");
                        
                        string demoNoteStringFilenamePart = "";
                        if (demoNoteString != null)
                        {
                            string demoNoteStringSafe = Helpers.DemoCuttersanitizeFilename(demoNoteString, false);
                            demoCutCommand.Append($"# demo note: {demoNoteStringSafe}\n");
                            demoNoteStringFilenamePart = $"_{demoNoteString}";
                        }

                        demoCutCommand.Append("wait\n");

                        List<Tuple<string,int>> cutDemoNames = new List<Tuple<string, int>>();

                        foreach(ConnectionDemoState demoState in demoStates) 
                        {
                            cutDemosCount++;
                            demoCutsTimes.Add(demoState.demoTime);
                            demoCutCommand.Append("DemoCutter ");
                            string demoPath = demoState.demoName;// client?.AbsoluteDemoName;
                            if (demoPath == null)
                            {
                                return;
                            }
                            string relativeDemoPath = Path.GetRelativePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts"), demoPath).Replace("$", "\\$");
                            demoCutCommand.Append($"\"{relativeDemoPath}\" ");
                            string filename = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{pm.playerName}__{thisServerInfo.HostName}__{thisServerInfo.MapName}_{demoState.myClientNum}{demoNoteStringFilenamePart}";
                            if (filename.Length > 150) // Limit this if ppl do ridiculously long notes, or if server name is too long... or or or 
                            {
                                filename = filename.Substring(0, 150);
                            }
                            filename = Helpers.MakeValidFileName(filename);
                            filename = Helpers.DemoCuttersanitizeFilename(filename, false).Replace("$", "\\$");
                            demoCutCommand.Append($"\"{filename}\" ");
                            demoCutCommand.Append(Math.Max(0, demoState.demoTime - markMinutes * 60000).ToString());
                            demoCutCommand.Append(" ");
                            demoCutCommand.Append((demoState.demoTime + 60000).ToString());
                            demoCutCommand.Append(" & \n");

                            if (reframeRequested)
                            {
                                string demoExtension = mohMode ? ".dm3" : (".dm_" + ((int)this.protocol).ToString());

                                cutDemoNames.Add(new Tuple<string,int>( filename, demoState.myClientNum));

                                if (demoState.isStrobeOperator
                                    /*this.CameraOperator is CameraOperators.StrobeCameraOperator*/)
                                {
                                    // Strobe is a bit flickedy-flicky, needs special heavy duty tools to produce something that's remotely useful
                                    if (reframeAborted)
                                    {
                                        demoCutCommandReframes.Append("//");
                                    }
                                    demoCutCommandReframes.Append("DemoMerger ");
                                    demoCutCommandReframes.Append($"\"{filename}_reframedSTR{reframeClientNum}{demoExtension}\" ");
                                    demoCutCommandReframes.Append($"\"{filename}{demoExtension}\" ");
                                    demoCutCommandReframes.Append($" -r{reframeClientNum} -i -I"); // -i option for persisting and interpolating other entities. -I option for interpolating playerstate. Not great still, but better than a normal reframe.
                                    if (serverSendsAllEntities)
                                    {
                                        // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                                        // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                                        demoCutCommandReframes.Append(" --visall");
                                    }
                                    demoCutCommandReframes.Append(" & \n");
                                } else
                                {
                                    if (reframeAborted)
                                    {
                                        demoCutCommandReframes.Append("//");
                                    }
                                    demoCutCommandReframes.Append("DemoReframer ");
                                    demoCutCommandReframes.Append($"\"{filename}{demoExtension}\" ");
                                    demoCutCommandReframes.Append($"\"{filename}_reframed{reframeClientNum}{demoExtension}\" ");
                                    demoCutCommandReframes.Append(reframeClientNum);
                                    if (serverSendsAllEntities)
                                    {
                                        // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                                        // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                                        demoCutCommandReframes.Append(" visall");
                                    }
                                    demoCutCommandReframes.Append(" & \n");
                                }
                            }
                        }

                        demoCutCommand.Append("wait\n");
                        demoCutCommand.Append(demoCutCommandReframes);

                        if (reframeRequested && cutDemoNames.Count > 1) // Make a merged demo out of all with the reframed angle :)
                        {
                            string demoExtension = mohMode ? ".dm3" : (".dm_" + ((int)this.protocol).ToString());

                            StringBuilder numbersString = new StringBuilder();
                            foreach(Tuple<string,int> cutDemoName in cutDemoNames)
                            {
                                numbersString.Append($"_{cutDemoName.Item2}");
                            }

                            string filenameGeneric = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{pm.playerName}__{thisServerInfo.HostName}__{thisServerInfo.MapName}{numbersString.ToString()}{demoNoteStringFilenamePart}";
                            filenameGeneric = Helpers.MakeValidFileName(filenameGeneric);
                            filenameGeneric = Helpers.DemoCuttersanitizeFilename(filenameGeneric, false).Replace("$", "\\$");
                            if (filenameGeneric.Length > 150) // Limit this if ppl do ridiculously long notes, or if server name is too long... or or or 
                            {
                                filenameGeneric = filenameGeneric.Substring(0, 150);
                            }
                            if (reframeAborted)
                            {
                                demoCutCommand.Append("//");
                            }
                            demoCutCommand.Append("DemoMerger ");
                            demoCutCommand.Append($"\"{filenameGeneric}_reframedMERGE{reframeClientNum}{demoExtension}\" ");
                            foreach (Tuple<string, int> cutDemoName in cutDemoNames)
                            {
                                demoCutCommand.Append($"\"{cutDemoName.Item1}{demoExtension}\" ");
                            }
                            demoCutCommand.Append($" -r{reframeClientNum} -i -I"); // -i option for persisting and interpolating other entities. -I option for interpolating playerstate. Not great still, but better than a normal reframe.
                            if (serverSendsAllEntities)
                            {
                                // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                                // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                                demoCutCommand.Append(" --visall");
                            }
                            demoCutCommand.Append(" & \n");
                        }

                        demoCutCommand.Append("wait\n");

                        Helpers.logRequestedDemoCut(new string[] { demoCutCommand.ToString() });
                    }
                    else
                    {
                        return;
                    }
                    if (!_connectionOptions.silentMode && cutDemosCount > 1)
                    {
                        string currentDemoTime = demoCutsTimes.Count > 0 ? string.Join(",", demoCutsTimes) : "UNKNOWN WTF";
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Time was marked for demo cut{withReframe} from {cutDemosCount} demos, {markMinutes} min into past. Times {currentDemoTime}.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    } else if (!_connectionOptions.silentMode && cutDemosCount > 0)
                    {
                        string currentDemoTime = demoCutsTimes.Count > 0 ? demoCutsTimes[0].ToString() : "UNKNOWN WTF";
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Time was marked for demo cut{withReframe}, {markMinutes} min into past. Current demo time is {currentDemoTime}.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    }
                    if (reframeAborted)
                    {
                        leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   Reframe will NOT be automatically made due to demo cut being more than {maxReframeMinutes} minutes long.\"", RequestCategory.NONE, 0, 0, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    }
                    serverWindow.addToLog($"^1NOTE ({myClientNum}): demo cut{withReframe} requested by \"{pm.playerName}\" (clientNum {pm.playerNum}), {markMinutes} minute(s) into the past {rateLimited}\n");
                    return;
                }
            }
            catch (Exception e)
            {
                StringBuilder erroredChat = new StringBuilder();
                for(int i=0; i < commandEventArgs.Command.Argc; i++)
                {
                    if(i != 0)
                    {
                        erroredChat.Append(" ");
                    }
                    erroredChat.Append(commandEventArgs.Command.Argv(i));
                }
                serverWindow.addToLog($"Error evaluating chat ({erroredChat.ToString()}): " + e.ToString(), true);
            }
        }


        public string GetMapName()
        {
            return client?.ServerInfo?.MapName;
        }

        // Should kinda merge it with the above because cringe.. oh well
        public void MarkDemoManual(int markMinutes, bool reframeRequested, int reframeClientNum = -1, string playernameOverride=null, string demoNoteString = null)
        {
            bool reframeAborted = false;
            ServerInfo thisServerInfo = client?.ServerInfo;
            if (thisServerInfo == null)
            {
                return;
            }

            bool serverSendsAllEntities = infoPool.serverSendsAllEntities;

            if (reframeClientNum < 0 || reframeClientNum >= Math.Max(this.serverMaxClientsLimit, 32))
            {
                reframeRequested = false;
            }

            string asClientNum = $"(as {reframeClientNum})";
            string withReframe = reframeRequested ? $"+reframe{asClientNum}" : "";
            int cutDemosCount = 0;
            List<int> demoCutsTimes = new List<int>();

            StringBuilder demoCutCommand = new StringBuilder();
            StringBuilder demoCutCommandReframes = new StringBuilder();
            DateTime now = DateTime.Now;


            string safePlayerName = playernameOverride == null? "null": Helpers.DemoCuttersanitizeFilename(playernameOverride, false);
            demoCutCommand.Append($"# demo cut{withReframe} manually added (\"{safePlayerName}\") (clientNum {reframeClientNum}) on {thisServerInfo.HostName} ({thisServerInfo.Address.ToString()}) at {now}, {markMinutes} minute(s) into the past\n");

            string demoNoteStringFilenamePart = "";
            if (demoNoteString != null)
            {
                string demoNoteStringSafe = Helpers.DemoCuttersanitizeFilename(demoNoteString, false);
                demoCutCommand.Append($"# demo note: {demoNoteStringSafe}\n");
                demoNoteStringFilenamePart = $"_{demoNoteString}";
            }

            demoCutCommand.Append("wait\n");

            List<Tuple<string, int>> cutDemoNames = new List<Tuple<string, int>>();
            List<ConnectionDemoState> demoStates = new List<ConnectionDemoState>();

            Connection[] allConns = serverWindow.getAllConnections();

            foreach (Connection oneConn in allConns)
            {
                ConnectionDemoState demoState = new ConnectionDemoState()
                {
                    demoName = oneConn.client?.AbsoluteDemoName,
                    demoTime = (oneConn.client?.DemoCurrentTimeApproximate).GetValueOrDefault(0),
                    myClientNum = (oneConn.client?.clientNum).GetValueOrDefault(-1),
                    isStrobeOperator = (oneConn.CameraOperator is CameraOperators.StrobeCameraOperator) || (oneConn.CameraOperator as CameraOperators.CTFCameraOperatorRedBlue)?.StrobeConnection == oneConn
                };
                if (demoState.demoName != null && demoState.demoTime != 0) // Even if it's truly 0, what could someone possibly get out of a mark when the demo recording literally just started?
                {
                    demoStates.Add(demoState);
                }
            }

            foreach (ConnectionDemoState demoState in demoStates)
            {
                cutDemosCount++;
                demoCutsTimes.Add(demoState.demoTime);
                demoCutCommand.Append("DemoCutter ");
                string demoPath = demoState.demoName;// client?.AbsoluteDemoName;
                if (demoPath == null)
                {
                    return;
                }
                string relativeDemoPath = Path.GetRelativePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts"), demoPath).Replace("$", "\\$");
                demoCutCommand.Append($"\"{relativeDemoPath}\" ");
                string filename = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{playernameOverride}__{thisServerInfo.HostName}__{thisServerInfo.MapName}_{demoState.myClientNum}{demoNoteStringFilenamePart}";
                if (filename.Length > 150) // Limit this if ppl do ridiculously long notes, or if server name is too long... or or or 
                {
                    filename = filename.Substring(0, 150);
                }
                filename = Helpers.MakeValidFileName(filename);
                filename = Helpers.DemoCuttersanitizeFilename(filename, false).Replace("$", "\\$");
                demoCutCommand.Append($"\"{filename}\" ");
                demoCutCommand.Append(Math.Max(0, demoState.demoTime - markMinutes * 60000).ToString());
                demoCutCommand.Append(" ");
                demoCutCommand.Append((demoState.demoTime + 60000).ToString());
                demoCutCommand.Append(" & \n");

                if (reframeRequested)
                {
                    string demoExtension = mohMode ? ".dm3" : (".dm_" + ((int)this.protocol).ToString());

                    cutDemoNames.Add(new Tuple<string, int>(filename, demoState.myClientNum));

                    if (demoState.isStrobeOperator
                        /*this.CameraOperator is CameraOperators.StrobeCameraOperator*/)
                    {
                        // Strobe is a bit flickedy-flicky, needs special heavy duty tools to produce something that's remotely useful
                        if (reframeAborted)
                        {
                            demoCutCommandReframes.Append("//");
                        }
                        demoCutCommandReframes.Append("DemoMerger ");
                        demoCutCommandReframes.Append($"\"{filename}_reframedSTR{reframeClientNum}{demoExtension}\" ");
                        demoCutCommandReframes.Append($"\"{filename}{demoExtension}\" ");
                        demoCutCommandReframes.Append($" -r{reframeClientNum} -i -I"); // -i option for persisting and interpolating other entities. -I option for interpolating playerstate. Not great still, but better than a normal reframe.
                        if (serverSendsAllEntities)
                        {
                            // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                            // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                            demoCutCommandReframes.Append(" --visall");
                        }
                        demoCutCommandReframes.Append(" & \n");
                    }
                    else
                    {
                        if (reframeAborted)
                        {
                            demoCutCommandReframes.Append("//");
                        }
                        demoCutCommandReframes.Append("DemoReframer ");
                        demoCutCommandReframes.Append($"\"{filename}{demoExtension}\" ");
                        demoCutCommandReframes.Append($"\"{filename}_reframed{reframeClientNum}{demoExtension}\" ");
                        demoCutCommandReframes.Append(reframeClientNum);
                        if (serverSendsAllEntities)
                        {
                            // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                            // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                            demoCutCommandReframes.Append(" visall");
                        }
                        demoCutCommandReframes.Append(" & \n");
                    }
                }
            }

            demoCutCommand.Append("wait\n");
            demoCutCommand.Append(demoCutCommandReframes);

            if (reframeRequested && cutDemoNames.Count > 1) // Make a merged demo out of all with the reframed angle :)
            {
                string demoExtension = mohMode ? ".dm3" : (".dm_" + ((int)this.protocol).ToString());

                StringBuilder numbersString = new StringBuilder();
                foreach (Tuple<string, int> cutDemoName in cutDemoNames)
                {
                    numbersString.Append($"_{cutDemoName.Item2}");
                }

                string filenameGeneric = $"{now.ToString("yyyy-MM-dd_HH-mm-ss")}__{playernameOverride}__{thisServerInfo.HostName}__{thisServerInfo.MapName}{numbersString.ToString()}{demoNoteStringFilenamePart}";
                filenameGeneric = Helpers.MakeValidFileName(filenameGeneric);
                filenameGeneric = Helpers.DemoCuttersanitizeFilename(filenameGeneric, false).Replace("$", "\\$");
                if (filenameGeneric.Length > 150) // Limit this if ppl do ridiculously long notes, or if server name is too long... or or or 
                {
                    filenameGeneric = filenameGeneric.Substring(0, 150);
                }
                if (reframeAborted)
                {
                    demoCutCommand.Append("//");
                }
                demoCutCommand.Append("DemoMerger ");
                demoCutCommand.Append($"\"{filenameGeneric}_reframedMERGE{reframeClientNum}{demoExtension}\" ");
                foreach (Tuple<string, int> cutDemoName in cutDemoNames)
                {
                    demoCutCommand.Append($"\"{cutDemoName.Item1}{demoExtension}\" ");
                }
                demoCutCommand.Append($" -r{reframeClientNum} -i -I"); // -i option for persisting and interpolating other entities. -I option for interpolating playerstate. Not great still, but better than a normal reframe.
                if (serverSendsAllEntities)
                {
                    // Server sending all entities means we will be able to reframe even to players who would not be normally visible in either demo
                    // Thus a high likelihood of ending up with reframes where the level is disappearing due to areamask issues. This should fix it.
                    demoCutCommand.Append(" --visall");
                }
                demoCutCommand.Append(" & \n");
            }

            demoCutCommand.Append("wait\n");

            Helpers.logRequestedDemoCut(new string[] { demoCutCommand.ToString() });

        }




        Regex multipleSlashRegex = new Regex("/{2,}", RegexOptions.Compiled);
        void ChatCommandAnswer(ParsedChatMessage pm, string message, bool pub=true, bool team=true, bool priv=true, bool forcePrivateResponse = false, ChatType? chatTypeOverride = null)
        {
            ChatType chatType = chatTypeOverride.HasValue ? chatTypeOverride.Value : pm.type;
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
            if (pm.commandComesFromJKWatcher)
            {
                serverWindow.addToLog($"Silent response due to message coming from ourselves: In response to {pm.type} from {pm.playerName} ({pm.playerNum}): {message}");
            }
            else if (chatType == ChatType.TEAM && team)
            {
                if (forcePrivateResponse) forcingPrivateResponse = true;
                else leakyBucketRequester.requestExecution($"say_team \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
            }
            else if (chatType == ChatType.PUBLIC && pub)
            {
                if (forcePrivateResponse) forcingPrivateResponse = true;
                else
                {
                    if (pm.crossServerBroadcastMessage && this.ttFlags.HasFlag(TommyTernalFlags.TTFLAGSSERVERINFO_HASCROSSSERVERCHAT))
                    {
                        leakyBucketRequester.requestExecution($"say_cross \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    }
                    else
                    {
                        leakyBucketRequester.requestExecution($"say \"{message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
                    }
                }
            }
            if ((chatType == ChatType.PRIVATE && priv) || forcingPrivateResponse)
            {
                // In case of private messages, append 3 spaces before the message so we can safely recognize this message as coming from a demobot to avoid creating endless loops
                leakyBucketRequester.requestExecution($"tell {pm.playerNum} \"   {message}\"", RequestCategory.MEME, 0, ChatMemeCommandsDelay, LeakyBucketRequester<string, RequestCategory>.RequestBehavior.ENQUEUE, null);
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
        public static int getNiceRandom(int min, int max)
        {
            lock (randomMakerRandom)
            {
                return max<min ? randomMakerRandom.Next(max,min): randomMakerRandom.Next(min, max);
            }
        }

    }
}
