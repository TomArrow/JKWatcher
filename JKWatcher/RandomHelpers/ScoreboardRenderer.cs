using JKClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    using KillsRets = Tuple<int, int>;
    class ScoreboardEntry
    {
        public IdentifiedPlayerStats stats;

        // we copy a few properties to here to sort by.
        // since the player session data could be changed while we are sorting,
        // sorting might throw an exception, so we first create copies of the sort-relevant data.
        public bool isStillActivePlayer;
        public Team team;
        public int score;
        public DateTime lastSeen;
        public int teamScore;
        public Dictionary<string, int> killTypes = new Dictionary<string, int>();
        public Dictionary<string, int> killTypesRets = new Dictionary<string, int>();

        public bool isBot;

        public int returns;// This is just for convenience.

        public static int Comparer(ScoreboardEntry a, ScoreboardEntry b)
        {
            if (a.team == b.team)
            {
                if(a.isBot != b.isBot)
                {
                    return a.isBot.CompareTo(b.isBot); // bots come last, no matter what.
                }
                return b.score.CompareTo(a.score); // highest to lowest
            }

            if ((a.team == Team.Red || a.team == Team.Blue ) && (b.team == Team.Red || b.team == Team.Blue) && a.teamScore != b.teamScore)
            {
                return b.teamScore.CompareTo(a.teamScore); // highest to lowest
            }

            int aTeam = normalizedTeamNumber(a.team);
            int bTeam = normalizedTeamNumber(b.team);
            return aTeam.CompareTo(bTeam);
        }
        public static int CountReductionComparer(ScoreboardEntry a, ScoreboardEntry b) // this sorts by which players we most want to keep, not neccessarily by which order they should be displayed in.
        {
            bool aSpec = a.team == Team.Spectator;
            bool bSpec = b.team == Team.Spectator;

            if(aSpec != bSpec)
            {
                return aSpec.CompareTo(bSpec); // spectators always last
            }

            if(a.isStillActivePlayer != b.isStillActivePlayer)
            {
                return bSpec.CompareTo(aSpec); // disconnected players also gonna have lower rank
            }

            int aScore = a.score;
            int bScore = b.score;
            return bScore.CompareTo(aScore);
        }
        public static int CountReductionComparerIgnoreActivity(ScoreboardEntry a, ScoreboardEntry b) // this sorts by which players we most want to keep, not neccessarily by which order they should be displayed in.
        {
            bool aSpec = a.team == Team.Spectator;
            bool bSpec = b.team == Team.Spectator;

            if(aSpec != bSpec)
            {
                return aSpec.CompareTo(bSpec); // spectators always last
            }

            int aScore = a.score;
            int bScore = b.score;
            return bScore.CompareTo(aScore);
        }
        private static int normalizedTeamNumber(Team team)
        {
            switch(team){
                case Team.Red:
                    return 0;
                case Team.Blue:
                    return 1;
                case Team.Free:
                    return 2;
                default:
                    return (int)team;
            }
        }
    }

    class ColumnInfo
    {
        public static readonly Font headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
        Func<ScoreboardEntry, string> fetcher = null;
        string name = null;
        float topOffset = 0;
        public float width = 0;
        Font font = null;
        public bool autoScale = false;
        public bool allowWrap = true;
        public ColumnInfo(string nameA, float topOffsetA, float widthA, Font fontA ,Func<ScoreboardEntry, string> fetchFunc)
        {
            if(nameA is null || fetchFunc is null || fontA is null)
            {
                throw new InvalidOperationException("ColumnInfo must be created with non null name and fetchFunc and font");
            }
            name = nameA;
            fetcher = fetchFunc;
            font = fontA;
            topOffset = topOffsetA;
            width = widthA;
        }
        public string GetValueString(ScoreboardEntry session)
        {
            if (session is null) return null;
            return fetcher(session);
        }
        StringFormat defaultStringFormat = new StringFormat() { };
        StringFormat noWrapStringFormat = new StringFormat() { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip};


        private static byte floatColorToByte(float color)
        {
            return (byte)Math.Clamp(color * 255f, 0f, 255f);
        }
        private static Color vectorToDrawingColor(Vector4 input)
        {
            Color retVal = Color.FromArgb(floatColorToByte(input.W), floatColorToByte(input.X), floatColorToByte(input.Y), floatColorToByte(input.Z));
            return retVal;
        }

        static Vector4 v4DKGREY2 = new Vector4(0.15f, 0.15f, 0.15f, 1f);
        static readonly Brush bgBrush = new SolidBrush(vectorToDrawingColor(v4DKGREY2));

        public void DrawString(Graphics g, bool header, float x, float y, ScoreboardEntry entry=  null )
        {
            string theString = header ? this.name : GetValueString(entry);
            if (theString is null) return;
            Font fontToUse = header ? headerFont : font;

            StringFormat formatToUse = (header || !allowWrap) ? noWrapStringFormat : defaultStringFormat;

            string cleanString = null;
            const float reductionDecrements = 0.5f;
            if (autoScale)
            {
                cleanString = Q3ColorFormatter.cleanupString(theString);
                float trySize = fontToUse.Size;
                while (g.MeasureString(cleanString, fontToUse, new PointF(x,y),formatToUse).Width > width && trySize >= 8)
                {
                    trySize -= reductionDecrements;
                    fontToUse = new Font(fontToUse.FontFamily, trySize, fontToUse.Style, fontToUse.Unit);
                }
            }

            if (!theString.Contains('^') || !g.DrawStringQ3(theString, fontToUse,  new RectangleF(x, y + topOffset, width, ScoreboardRenderer.recordHeight), formatToUse, true,false/*entry?.team == Team.Spectator ? true : false*/))
            {
                theString = cleanString is null ? Q3ColorFormatter.cleanupString(theString) : cleanString;
                if (theString is null) return;
                g.DrawString(theString, fontToUse, bgBrush, new RectangleF(2f + x, 2f + y + topOffset, width, ScoreboardRenderer.recordHeight), formatToUse);
                g.DrawString(theString, fontToUse, System.Drawing.Brushes.White, new RectangleF(x, y + topOffset, width, ScoreboardRenderer.recordHeight), formatToUse);
            }
        }
    }

    class ScoreboardRenderer
    {
        private static readonly Font nameFont = new Font("Segoe UI", 14,FontStyle.Bold);
        private static readonly Font normalFont = new Font("Segoe UI", 10);
        private static readonly Font tinyFont = new Font("Segoe UI", 8);
        private static readonly string[] killTypesAlways = new string[] {string.Intern("DBS"), /*"DFA", "BS", "DOOM",*/ string.Intern("MINE") }; // these will always get a column if at least 1 was found.

        public const float recordHeight = 30;
        public const float horzPadding = 5;
        public const float vertPadding = 1;

        enum ScoreFields
        {
            RETURNS,
            DEFEND,
            ASSIST,
        }

        const int bgAlpha = (int)((float)255 * 0.33f);
        const int color0_2 = (int)((float)255 * 0.2f);
        const int color0_8 = (int)((float)255 * 0.8f);
        static readonly Brush redTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, color0_2, color0_2));
        static readonly Brush blueTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_2, color0_2, 255));
        static readonly Brush freeTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_8, color0_8, 0));
        static readonly Brush spectatorTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 128, 128, 128));

        public static void DrawScoreboard(
            Bitmap bmp,
            bool thisGame,
            ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats> ratingsAndNames, 
            ServerSharedInformationPool infoPool,
            bool all
            )
        {

            if (!thisGame) infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults, true);
            else infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame, true);

            int redScore = infoPool.ScoreRed;
            int blueScore = infoPool.ScoreBlue;
            List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
            Dictionary<string, int> killTypesCounts = new Dictionary<string, int>();
            bool serverSendsRets = infoPool.serverSeemsToSupportRetsCountScoreboard;
            Int64 foundFields = 0;
            foreach(var kvp in ratingsAndNames)
            {
                ScoreboardEntry entry   = new ScoreboardEntry();
                entry.stats = kvp.Value;
                entry.team = kvp.Value.playerSessInfo.LastNonSpectatorTeam;
                entry.score = kvp.Value.playerSessInfo.score.score;
                entry.teamScore = 0;
                entry.returns = kvp.Value.chatCommandTrackingStuff.returns < kvp.Value.playerSessInfo.score.impressiveCount ? kvp.Value.playerSessInfo.score.impressiveCount : kvp.Value.chatCommandTrackingStuff.returns;
                if(entry.returns > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.RETURNS);
                }
                if(kvp.Value.playerSessInfo.score.defendCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFEND);
                }
                if(kvp.Value.playerSessInfo.score.assistCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.ASSIST);
                }
                entry.isBot = kvp.Value.playerSessInfo.confirmedBot || kvp.Value.playerSessInfo.confirmedJKWatcherFightbot;
                switch (entry.team)
                {
                    case Team.Red:
                        entry.teamScore = redScore;
                        break;
                    case Team.Blue:
                        entry.teamScore = blueScore;
                        break;
                }
                entry.isStillActivePlayer = false;
                foreach (PlayerInfo pi in infoPool.playerInfo)
                {
                    if(pi.session == kvp.Key)
                    {
                        entry.isStillActivePlayer = true;
                        break;
                    }
                }
                entry.lastSeen = kvp.Value.lastSeenActive;

                Dictionary<KillType, int> killTypes = kvp.Value.chatCommandTrackingStuff.GetKillTypes();
                Dictionary<KillType, int> killTypesRets = kvp.Value.chatCommandTrackingStuff.GetKillTypesReturns();
                foreach (var kt in killTypes)
                {
                    string keyName = kt.Key.shortname; // for scoreboard we concatenate/abbreviate by using the shortname to not have too much stuff.
                    if (!killTypesCounts.ContainsKey(keyName))
                    {
                        killTypesCounts[keyName] = 0;
                    }
                    killTypesCounts[keyName]++;
                    if (!entry.killTypes.ContainsKey(keyName))
                    {
                        entry.killTypes[keyName] = 0;
                    }
                    entry.killTypes[keyName]++;
                }
                foreach (var kt in killTypesRets)
                {
                    string keyName = kt.Key.shortname; // for scoreboard we concatenate/abbreviate by using the shortname to not have too much stuff.
                    if (!entry.killTypesRets.ContainsKey(keyName))
                    {
                        entry.killTypesRets[keyName] = 0;
                    }
                    entry.killTypesRets[keyName]++;
                }

                entries.Add(entry);
            }

            const int maxScoreEntries = 32;

            ScoreboardEntry[] removedEntries = null;
            if (entries.Count > maxScoreEntries)
            {
                // If we have too many, only keep those with highest scores
                entries.Sort(ScoreboardEntry.CountReductionComparer);
                removedEntries = entries.GetRange(maxScoreEntries, entries.Count - maxScoreEntries).ToArray(); // maybe we'll just give them a "honorable mention" text at the bottom with names.
                entries.RemoveRange(30, entries.Count - maxScoreEntries);
            }

            // Sort entries, including by team
            entries.Sort(ScoreboardEntry.Comparer);

            // Sort kill types
            List<KeyValuePair<string, int>> killTypesList = killTypesCounts.ToList();
            killTypesList.Sort((a,b)=> { return b.Value.CompareTo(a.Value); });

            // decide which killtypes should get their own column
            // a few that should always, and then just the ones that happened the most often.
            const int killTypeColumnCount = 8;
            List<string> killTypesColumns = new List<string>();
            foreach(string column in killTypesAlways)
            {
                if (killTypesCounts.ContainsKey(column))
                {
                    killTypesColumns.Add(column);
                }
            }
            foreach(KeyValuePair<string, int> column in killTypesList)
            {
                if (killTypesColumns.Count >= killTypeColumnCount)
                {
                    break;
                }
                if (!killTypesColumns.Contains(column.Key))
                {
                    killTypesColumns.Add(column.Key);
                }
            }



            Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;



            List<ColumnInfo> columns = new List<ColumnInfo>();


            columns.Add(new ColumnInfo("CL", 0, 25, normalFont, (a) => { return a.stats.playerSessInfo.clientNum.ToString(); }));
            columns.Add(new ColumnInfo("NAME", 0, 270, nameFont, (a) => { return a.stats.playerSessInfo.GetNameOrLastNonPadaName(); }) {  autoScale = true, allowWrap =false});
            columns.Add(new ColumnInfo("SCORE",0,60,normalFont,(a)=> { return a.stats.score.score.ToString(); }));
            columns.Add(new ColumnInfo("C",0, 25, normalFont,(a)=> { return a.stats.score.captures.ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.RETURNS)) >0)
            {
                columns.Add(new ColumnInfo("R", 0, 25, normalFont, (a) => { return a.returns.ToString(); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.DEFEND)) >0)
            {
                columns.Add(new ColumnInfo("BC", 0, 25, normalFont, (a) => { return a.stats.score.defendCount.ToString(); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.ASSIST)) >0)
            {
                columns.Add(new ColumnInfo("A", 0, 25, normalFont, (a) => { return a.stats.score.assistCount.ToString(); }));
            }
            columns.Add(new ColumnInfo("K/D", 0, 40, normalFont, (a) => { return $"{a.stats.chatCommandTrackingStuff.totalKills}/{a.stats.chatCommandTrackingStuff.totalDeaths}"; }));
            columns.Add(new ColumnInfo("PING", 0, 40, normalFont, (a) => { return a.stats.score.ping.ToString(); }));
            columns.Add(new ColumnInfo("TIME", 0, 40, normalFont, (a) => { return a.stats.score.time.ToString(); }));
            columns.Add(new ColumnInfo("GLICKO2", 0, 70, normalFont, (a) => { return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRating(true)}±{(int)a.stats.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}"; }));

            string[] columnizedKillTypes = killTypesColumns.ToArray();

            foreach (string killTypeColumn in columnizedKillTypes)
            {
                string stringLocal = killTypeColumn;
                float width = Math.Max(g.MeasureString(stringLocal, ColumnInfo.headerFont).Width+2f,20);
                columns.Add(new ColumnInfo(stringLocal, 0, width, normalFont, (a) => { return a.killTypes.GetValueOrDefault(stringLocal,0).ToString() + (a.killTypesRets.ContainsKey(stringLocal) ? $"/^1{a.killTypesRets[stringLocal]}" : ""); }));
            }

            if(killTypesCounts.Count > 0)
            {
                columns.Add(new ColumnInfo("OTHER KILLTYPES", 0, 180, tinyFont, (a) => { return MakeKillTypesString(a.killTypes, a.killTypesRets, columnizedKillTypes); }));
            }

            columns.Add(new ColumnInfo("KILLED PLAYERS", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,false); }));
            columns.Add(new ColumnInfo("KILLED BY", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,true); }));


            const float sidePadding = 90;
            const float totalWidth = 1920 - sidePadding - sidePadding;
            float neededWidth = 0;

            // Check that it all fits in
            for (int i = 0; i < columns.Count; i++)
            {
                float thisWidth = columns[i].width;
                if(neededWidth + thisWidth > totalWidth)
                {
                    // overflow. truncate this column and ditch rest.
                    columns[i].width = totalWidth - neededWidth;
                    if(columns.Count > (i + 1))
                    {
                        columns.RemoveRange(i + 1, columns.Count - i - 1);
                    }
                    break;
                }
                neededWidth += thisWidth + horzPadding;
            }


            float posXStart = sidePadding;
            float posX = posXStart;
            float posY = 20;
            foreach (var column in columns)
            {
                column.DrawString(g,true,posX,posY);
                posX += column.width+ horzPadding;
            }
            foreach (var entry in entries)
            {
                posY += recordHeight+ vertPadding;
                posX = posXStart;

                Brush brush = null;
                switch (entry.team) {
                    case Team.Red:
                        brush = redTeamBrush;
                        break;
                    case Team.Blue:
                        brush = blueTeamBrush;
                        break;
                    case Team.Free:
                        brush = freeTeamBrush;
                        break;
                    case Team.Spectator:
                        brush = spectatorTeamBrush;
                        break;
                }

                if(brush != null)
                {
                    g.FillRectangle(brush,new RectangleF(posX,posY,1920.0f- sidePadding - sidePadding,30.0f));
                }


                foreach (var column in columns)
                {
                    column.DrawString(g, false, posX, posY, entry);
                    posX += column.width + horzPadding;
                }
            }
            g.Flush();
            g.Dispose();

        }


        public static string MakeKillTypesString(Dictionary<string, int> killTypes,Dictionary<string, int> retTypes, string[] excludedKillTypes, int lengthLimit = 99999)
        {
            List<KeyValuePair<string, int>> killTypesData = killTypes.ToList();
            killTypesData.Sort((a, b) => { return -a.Value.CompareTo(b.Value); });

            StringBuilder killTypesString = new StringBuilder();
            int killTypeIndex = 0;
            foreach (KeyValuePair<string, int> killTypeInfo in killTypesData)
            {
                if (excludedKillTypes.Contains(killTypeInfo.Key))
                {
                    continue;
                }
                if (killTypeInfo.Value <= 0)
                {
                    continue;
                }
                if ((killTypesString.Length + killTypeInfo.Key.Length) > lengthLimit)
                {
                    break;
                }
                if (killTypeIndex != 0)
                {
                    killTypesString.Append(", ");
                }
                int retCount = retTypes.GetValueOrDefault(killTypeInfo.Key,0);
                if(retCount > 0)
                {
                    killTypesString.Append($"{killTypeInfo.Value}/^1{retCount}^7x{killTypeInfo.Key}");
                }
                else
                {
                    killTypesString.Append($"{killTypeInfo.Value}x{killTypeInfo.Key}");
                }
                killTypeIndex++;
            }
            return killTypesString.ToString();
        }

        private static string MakeKillsOnString(ScoreboardEntry entry, bool victim, int lengthLimit = 99999)
        {

            List<KeyValuePair<string, KillsRets>> otherPersonData = new List<KeyValuePair<string, KillsRets>>();

            SessionPlayerInfo playerSession = entry.stats.playerSessInfo;
            ChatCommandTrackingStuff trackingStuff = entry.stats.chatCommandTrackingStuff;
            ConcurrentDictionary<SessionPlayerInfo, KillTracker> trackers = victim ? trackingStuff.killTrackersOnMe : trackingStuff.killTrackersOnOthers;
            foreach (KeyValuePair<SessionPlayerInfo, KillTracker> kvp in trackers)
            {
                if (kvp.Key == playerSession) continue; // dont count stuff on self. 
                string name = kvp.Key.GetNameOrLastNonPadaName();//Q3ColorFormatter.cleanupString(kvp.Key.GetNameOrLastNonPadaName());
                if (!string.IsNullOrWhiteSpace(name))
                {
                    KillTracker theTracker = kvp.Value;
                    otherPersonData.Add(new KeyValuePair<string, KillsRets>(name, new KillsRets(theTracker.kills,theTracker.returns)));
                }
            }

            otherPersonData.Sort((a, b) => { return -a.Value.Item1.CompareTo(b.Value.Item1); });

            StringBuilder otherPeopleString = new StringBuilder();
            int otherPersonIndex = 0;
            foreach (KeyValuePair<string, KillsRets> otherPersonInfo in otherPersonData)
            {
                if (otherPersonInfo.Value.Item1 <= 0)
                {
                    continue;
                }
                if ((otherPeopleString.Length + otherPersonInfo.Key.Length) > lengthLimit)
                {
                    break;
                }
                if (otherPersonIndex != 0)
                {
                    otherPeopleString.Append(", ");
                }
                if(otherPersonInfo.Value.Item2 > 0)
                {
                    otherPeopleString.Append($"^7{otherPersonInfo.Value.Item1}/^1{otherPersonInfo.Value.Item2}^7x{otherPersonInfo.Key}");
                }
                else
                {
                    otherPeopleString.Append($"^7{otherPersonInfo.Value.Item1}x{otherPersonInfo.Key}");
                }
                otherPersonIndex++;
            }
            return otherPeopleString.ToString();
        }

    }
}
