using JKClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    // TODO average value for ping?
    // TODO other used names
    // TODO glicko 2 lines to save space?
    // TODO sum up time column?
    // TODO what about VERY old scoreboard entries? treat them same?
    // TODO maybe: blocks, rolls, strafestyle
    // TODO Suicide count?
    // TODO highlight best number in each column?
    // TODO max/avg speed, sd, blocks?
    // TODO keystroke speeds, jump held durations, dbs speed
    // TODO dynamic sizes for all fields?
    // TODO decision on which fields based on prefiltered (when count too high) not on all
    // TODO icons for special stuff like perfect or whatever? idk wanted it for something else too but forgot.

    // TODO more possible values that are mod specific?
    // TODO Show team score in team modes?
    // TODO do sth different for defrag? maybe max speed? Do count of defrag runs for sorting. average/max speed. maps that were run and how often. how many top 10 runs. or sth like that. 

    // TODO dynamic killtypes size DONE
    // TODO make K/D a bit bigger DONE
    // TODO sort in dbs according to counts while making sure its used at all? DONE
    // TODO slightly alternating text color for columns? DONE (bg color)
    // TODO Mark Bot/fightbot on scoreboard DONE
    // TODO icons for special stuff like perfect or whatever? idk wanted it for something else too but forgot. like for BOT etc. Bot, fightbot, disconnected, wentspec DONE
    // TODO make all overflow by default unless specified otherwise? or all autoscale? DONE
    // TODO angled top columns to make them closer together DONE
    // TODO dont show ppl as member of team who were shortly in tem at start of game but never really did much playing. maybe only count non spec team if score > 0? MAYBE DONE
    // TODO MOH has no score and such KINDA DONE?
    // TODO scale shadow distance with font size MABE DONE
    // TODO correct MOD for jka and mb2? DONE
    // TODO MOH special fields and deal with K/D or whatever DONE
    // TODO Only cap column if any caps DONE
    // TODO only other kill types if any DONE 
    // TODO dont show kills on who in moh (since we dont have that info) or if there are no logged kills DONE
    // TODO dont count non-spec team of connecting clients? -- not sure how MAYBE DONE
    // TODO LastNonSpecTeam: make that also specifically thisgame DONE
    // TODO killtypes not being counted properly for some reason, very low numbers in columns - DONE
    // TODO spectators are getting same score as players. EW and then if last team is remembered they have the wrong thing dispalyed. can we only save score if not spec team? DONE


    using KillsRets = Tuple<int, int>;
    class ScoreboardEntry
    {
        public IdentifiedPlayerStats stats;

        // we copy a few properties to here to sort by.
        // since the player session data could be changed while we are sorting,
        // sorting might throw an exception, so we first create copies of the sort-relevant data.
        public bool isStillActivePlayer;
        public Team team;
        public Team realTeam;
        public int score;
        public DateTime lastSeen;
        public int teamScore;
        public Dictionary<string, int> killTypes = new Dictionary<string, int>();
        public Dictionary<string, int> killTypesRets = new Dictionary<string, int>();

        public bool fightBot;
        public bool isBot;

        public int returns;// This is just for convenience.

        public PlayerScore scoreCopy;

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
        public enum OverflowMode
        {
            WrapClip,
            AutoScale,
            AllowOverflow
        }

        public static readonly Font headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
        Func<ScoreboardEntry, string> fetcher = null;
        string name = null;
        float topOffset = 0;
        public float width = 0;
        Font font = null;
        public OverflowMode overflowMode = OverflowMode.AllowOverflow;
        //public bool autoScale = false;
        //public bool allowWrap = true;
        public bool angledHeader = false;
        public bool needsLeftLine = false;
        public bool rightAlign = false;
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
        StringFormat defaultStringFormatRightAlign = new StringFormat() { Alignment = StringAlignment.Far };
        StringFormat noWrapStringFormat = new StringFormat() { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip};
        StringFormat noWrapStringFormatRightAlign = new StringFormat() { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,Alignment = StringAlignment.Far };


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

            StringFormat formatToUse = (header || (overflowMode != OverflowMode.WrapClip)) ? (rightAlign ? noWrapStringFormatRightAlign: noWrapStringFormat) : ( rightAlign ? defaultStringFormatRightAlign: defaultStringFormat);

            string cleanString = null;
            bool fontWasReplaced = false;
            const float reductionDecrements = 0.5f;
            if (overflowMode == OverflowMode.AutoScale && !header)
            {
                cleanString = Q3ColorFormatter.cleanupString(theString);
                float trySize = fontToUse.Size;
                while (g.MeasureString(cleanString, fontToUse, new PointF(x,y),formatToUse).Width > width && trySize >= 8)
                {
                    trySize -= reductionDecrements;
                    if (fontWasReplaced)
                    {
                        fontToUse.Dispose();
                    }
                    fontToUse = new Font(fontToUse.FontFamily, trySize, fontToUse.Style, fontToUse.Unit);
                    fontWasReplaced = true;
                }
            }

            float yOffset = header ? 0 : topOffset;

            if (header && angledHeader)
            {
                float maxHeight = 45f-5f;
                float maxWidth = (float)Math.Sqrt(2f * maxHeight * maxHeight);
                cleanString = cleanString is null ? Q3ColorFormatter.cleanupString(theString) : cleanString;
                float trySize = fontToUse.Size;
                while (g.MeasureString(cleanString, fontToUse, new PointF(x, y), formatToUse).Width > maxWidth && trySize >= 8)
                {
                    trySize -= reductionDecrements;
                    if (fontWasReplaced)
                    {
                        fontToUse.Dispose();
                    }
                    fontToUse = new Font(fontToUse.FontFamily, trySize, fontToUse.Style, fontToUse.Unit);
                    fontWasReplaced = true;
                }
                SizeF measure = g.MeasureString(cleanString, fontToUse, new PointF(x, y), formatToUse);
                float realHeight = (float)Math.Sqrt(0.5f*measure.Height * measure.Height);

                g.TranslateTransform(x-5f, y+ measure.Height - realHeight + yOffset);
                g.RotateTransform(-45,MatrixOrder.Prepend);
            }
            else
            {
                g.TranslateTransform(x, y + yOffset);
            }

            float shadowDist = 2f * fontToUse.Size / 14f;

            if (overflowMode == OverflowMode.AllowOverflow || angledHeader)
            {
                if (!theString.Contains('^') || !g.DrawStringQ3(theString, fontToUse, new PointF(0, 0), formatToUse, true, false/*entry?.team == Team.Spectator ? true : false*/))
                {
                    theString = cleanString is null ? Q3ColorFormatter.cleanupString(theString) : cleanString;
                    if (theString is null) goto cleanup;
                    g.DrawString(theString, fontToUse, bgBrush, new PointF(shadowDist + 0, shadowDist + 0), formatToUse);
                    g.DrawString(theString, fontToUse, System.Drawing.Brushes.White, new PointF(0, 0), formatToUse);
                }
            }
            else
            {
                if (!theString.Contains('^') || !g.DrawStringQ3(theString, fontToUse, new RectangleF(0, 0, width, ScoreboardRenderer.recordHeight), formatToUse, true, false/*entry?.team == Team.Spectator ? true : false*/))
                {
                    theString = cleanString is null ? Q3ColorFormatter.cleanupString(theString) : cleanString;
                    if (theString is null) goto cleanup;
                    g.DrawString(theString, fontToUse, bgBrush, new RectangleF(shadowDist + 0, shadowDist + 0, width, ScoreboardRenderer.recordHeight), formatToUse);
                    g.DrawString(theString, fontToUse, System.Drawing.Brushes.White, new RectangleF(0, 0, width, ScoreboardRenderer.recordHeight), formatToUse);
                }
            }

        cleanup:
            g.ResetTransform();
            if (fontWasReplaced)
            {
                fontToUse.Dispose();
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
        public const float horzPadding = 3;
        public const float vertPadding = 1;

        enum ScoreFields
        {
            CAPTURES,
            RETURNS,
            DEFEND,
            ASSIST,
            SPREEKILLS,
            ACCURACY,
            DEATHS,
            KILLS,
            TOTALKILLS
        }

        const int bgAlpha = (int)((float)255 * 0.33f);
        const int color0_2 = (int)((float)255 * 0.2f);
        const int color0_8 = (int)((float)255 * 0.8f);
        static readonly Brush redTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, color0_2, color0_2));
        static readonly Brush blueTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_2, color0_2, 255));
        static readonly Brush freeTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_8, color0_8, 0));
        static readonly Brush spectatorTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 128, 128, 128));
        static readonly Brush brighterBGBrush = new SolidBrush(Color.FromArgb(bgAlpha / 4, 128, 128, 128));
        static readonly Pen linePen = new Pen(Color.FromArgb(bgAlpha/2, 128, 128, 128),0.5f);

        private static string block0(string input)
        {
            return input == "0" ? "" : input;
        }

        public static void DrawScoreboard(
            Bitmap bmp,
            bool thisGame,
            ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats> ratingsAndNames, 
            ServerSharedInformationPool infoPool,
            bool all,
            GameType gameType
            )
        {

            bool anyKillsLogged = false;
            bool anyValidGlicko2 = false;

            if (!thisGame) infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults, true);
            else infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame, true);

            int redScore = infoPool.ScoreRed;
            int blueScore = infoPool.ScoreBlue;
            List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
            Dictionary<string, int> killTypesCounts = new Dictionary<string, int>();
            Dictionary<string, int> killTypesCountsMax = new Dictionary<string, int>();
            Dictionary<string, int> killTypesCountsRetsMax = new Dictionary<string, int>();
            bool serverSendsRets = infoPool.serverSeemsToSupportRetsCountScoreboard;
            Int64 foundFields = 0;
            foreach(var kvp in ratingsAndNames)
            {
                ScoreboardEntry entry   = new ScoreboardEntry();
                entry.stats = kvp.Value;
                entry.team = kvp.Value.chatCommandTrackingStuff.LastNonSpectatorTeam;
                entry.realTeam = kvp.Value.playerSessInfo.team;
                entry.score = kvp.Value.playerSessInfo.score.score;
                entry.isBot = kvp.Value.playerSessInfo.confirmedBot || kvp.Value.playerSessInfo.confirmedJKWatcherFightbot;
                entry.fightBot = kvp.Value.playerSessInfo.confirmedJKWatcherFightbot;
                entry.scoreCopy = kvp.Value.playerSessInfo.score.Clone() as PlayerScore; // make copy because creating screenshot may take a few seconds and data might change drastically
                if (entry.scoreCopy is null) entry.scoreCopy = kvp.Value.playerSessInfo.score; // idk why this should happen but lets be safe
                anyValidGlicko2 = anyValidGlicko2 || kvp.Value.chatCommandTrackingStuff.rating.GetNumberOfResults(true) > 0;
                anyKillsLogged = anyKillsLogged || kvp.Value.chatCommandTrackingStuff.totalKills > 0 || kvp.Value.chatCommandTrackingStuff.totalDeaths > 0;
                if (entry.team != Team.Spectator && kvp.Value.playerSessInfo.team == Team.Spectator && (entry.score <= 0 || kvp.Value.playerSessInfo.score.time == 0))
                {
                    // we do want to show anyone who played, but if someone just showed up as player while connecting or just auto-joined quickly at the start and promptly
                    // went spec, we don't wanna list him as a player of that match.
                    // TODO maybe also always do if bot? or naw?
                    entry.team = Team.Spectator;
                }
                entry.teamScore = 0;
                entry.returns = kvp.Value.chatCommandTrackingStuff.returns < kvp.Value.playerSessInfo.score.impressiveCount ? kvp.Value.playerSessInfo.score.impressiveCount : kvp.Value.chatCommandTrackingStuff.returns;
                if(entry.returns > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.RETURNS);
                }
                if(kvp.Value.playerSessInfo.score.captures > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.CAPTURES);
                }
                if(kvp.Value.playerSessInfo.score.defendCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFEND);
                }
                if(kvp.Value.playerSessInfo.score.assistCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.ASSIST);
                }
                if(kvp.Value.playerSessInfo.score.excellentCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.SPREEKILLS);
                }
                if(kvp.Value.playerSessInfo.score.accuracy > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.ACCURACY);
                }
                if(kvp.Value.playerSessInfo.score.deaths > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEATHS);
                }
                if(kvp.Value.playerSessInfo.score.kills > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.KILLS);
                }
                if(kvp.Value.playerSessInfo.score.totalKills > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.TOTALKILLS);
                }
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
                    killTypesCounts[keyName] += kt.Value;
                    if (!killTypesCountsMax.ContainsKey(keyName))
                    {
                        killTypesCountsMax[keyName] = 0;
                    }
                    killTypesCountsMax[keyName] = Math.Max(kt.Value, killTypesCountsMax[keyName]);
                    if (!entry.killTypes.ContainsKey(keyName))
                    {
                        entry.killTypes[keyName] = 0;
                    }
                    entry.killTypes[keyName] += kt.Value;
                }
                foreach (var kt in killTypesRets)
                {
                    string keyName = kt.Key.shortname; // for scoreboard we concatenate/abbreviate by using the shortname to not have too much stuff.
                    if (!killTypesCountsRetsMax.ContainsKey(keyName))
                    {
                        killTypesCountsRetsMax[keyName] = 0;
                    }
                    killTypesCountsRetsMax[keyName] = Math.Max(kt.Value, killTypesCountsRetsMax[keyName]);
                    if (!entry.killTypesRets.ContainsKey(keyName))
                    {
                        entry.killTypesRets[keyName] = 0;
                    }
                    entry.killTypesRets[keyName]+= kt.Value;
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

            const int killTypeColumnCount = 8;

            int removeNeeded = killTypesList.Count() > killTypeColumnCount ? killTypesList.Count()- killTypeColumnCount : 0;
            bool killTypesAllCovered = true;
            if (removeNeeded > 0)
            {
                int removed = 0;
                for (int i = killTypesList.Count-1; i >= 0; i--)
                {
                    if (removed >= removeNeeded) break;
                    if (!killTypesAlways.Contains(killTypesList[i].Key))
                    {
                        killTypesList.RemoveAt(i);
                        removed++;
                        killTypesAllCovered = false;
                    }
                }
            }

            // decide which killtypes should get their own column
            // a few that should always, and then just the ones that happened the most often.
            List<string> killTypesColumns = new List<string>();
            foreach(KeyValuePair<string, int> column in killTypesList)
            {
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
            columns.Add(new ColumnInfo("NAME", 0, 220, nameFont, (a) => { return a.stats.playerSessInfo.GetNameOrLastNonPadaName(); }) { overflowMode= ColumnInfo.OverflowMode.AutoScale});
            columns.Add(new ColumnInfo("SCORE",0,50,normalFont,(a)=> { return a.scoreCopy.score == -9999 ? "--" : a.scoreCopy.score.ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.CAPTURES)) >0)
            {
                columns.Add(new ColumnInfo("C", 0, 15, normalFont, (a) => { return block0(a.scoreCopy.captures.ToString()); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.RETURNS)) >0)
            {
                columns.Add(new ColumnInfo("R", 0, 25, normalFont, (a) => { return block0(a.returns.ToString()); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.DEFEND)) >0)
            {
                columns.Add(new ColumnInfo("BC", 0, 25, normalFont, (a) => { return block0(a.scoreCopy.defendCount.ToString()); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.ASSIST)) >0)
            {
                columns.Add(new ColumnInfo("A", 0, 20, normalFont, (a) => { return block0(a.scoreCopy.assistCount.ToString()); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.SPREEKILLS)) >0)
            {
                columns.Add(new ColumnInfo("»", 0, 20, normalFont, (a) => { return block0(a.scoreCopy.excellentCount.ToString()); }));
            }
            if ((foundFields & (1 << (int)ScoreFields.ACCURACY)) >0)
            {
                columns.Add(new ColumnInfo("%", 0, 20, normalFont, (a) => { return block0(a.scoreCopy.accuracy.ToString()); }));
            }
            if (anyKillsLogged)
            {
                columns.Add(new ColumnInfo("K/D", 0, 55, normalFont, (a) => { if (a.stats.chatCommandTrackingStuff.totalKills == 0 && a.stats.chatCommandTrackingStuff.totalDeaths == 0) { return ""; } return $"{a.stats.chatCommandTrackingStuff.totalKills}/{a.stats.chatCommandTrackingStuff.totalDeaths}"; }));
            } else if(gameType > GameType.Team && ((foundFields & (1 << (int)ScoreFields.KILLS)) > 0 || (foundFields & (1 << (int)ScoreFields.TOTALKILLS)) > 0))
            {
                // MOH
                if ((foundFields & (1 << (int)ScoreFields.TOTALKILLS)) > 0)
                {
                    columns.Add(new ColumnInfo("K/T", 0, 40, normalFont, (a) => { if (a.scoreCopy.kills == 0 && a.scoreCopy.totalKills == 0) {return ""; } return $"{a.scoreCopy.kills}/{a.scoreCopy.totalKills}"; }));
                }
                else
                {
                    columns.Add(new ColumnInfo("K", 0, 40, normalFont, (a) => { return block0(a.scoreCopy.kills.ToString()); }));
                }
            } else if(gameType > GameType.Team && ((foundFields & (1 << (int)ScoreFields.KILLS)) > 0 || (foundFields & (1 << (int)ScoreFields.DEATHS)) > 0))
            {
                // MOH
                if((foundFields & (1 << (int)ScoreFields.DEATHS)) > 0)
                {
                    columns.Add(new ColumnInfo("K/D", 0, 40, normalFont, (a) => { if (a.scoreCopy.kills == 0 && a.scoreCopy.deaths == 0) { return ""; } return $"{a.scoreCopy.kills}/{a.scoreCopy.deaths}"; }));
                }
                else
                {
                    columns.Add(new ColumnInfo("K", 0, 40, normalFont, (a) => { return block0(a.scoreCopy.kills.ToString()); }));
                }
            }

            columns.Add(new ColumnInfo("PING", 0, 35, normalFont, (a) => { return a.scoreCopy.ping.ToString(); }));
            columns.Add(new ColumnInfo("TIME", 0, 40, normalFont, (a) => { return a.scoreCopy.time.ToString(); }));
            if (anyValidGlicko2)
            {
                columns.Add(new ColumnInfo("GLICKO2", 0, 70, normalFont, (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRating(true)}^yfff8±{(int)a.stats.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}"; }));
            }
            string[] columnizedKillTypes = killTypesColumns.ToArray();

            bool firstKillType = true;
            foreach (string killTypeColumn in columnizedKillTypes)
            {
                string stringLocal = killTypeColumn;
                string measureString = block0(killTypesCountsMax.GetValueOrDefault(stringLocal, 0).ToString() + (killTypesCountsRetsMax.ContainsKey(stringLocal) ? $"/{killTypesCountsRetsMax[stringLocal]}" : ""));
                float width = Math.Max(g.MeasureString(measureString, normalFont).Width+2f,20); // Sorta kinda make sure the text will fit.
                columns.Add(new ColumnInfo(stringLocal, 0, width, normalFont, (a) =>
                {
                    return block0(a.killTypes.GetValueOrDefault(stringLocal, 0).ToString() + (a.killTypesRets.ContainsKey(stringLocal) ? $"/^1{a.killTypesRets[stringLocal]}" : ""));
                })
                {
                    angledHeader = true,
                    needsLeftLine = !firstKillType
                });
                firstKillType = false;
            }

            if (anyKillsLogged) { 
                if(killTypesCounts.Count > 0 && !killTypesAllCovered)
                {
                    string referenceMaxString = MakeKillTypesString(killTypesCountsMax, killTypesCountsRetsMax, columnizedKillTypes);
                    float width = Math.Min(g.MeasureString(referenceMaxString, tinyFont).Width/2f + 2f, 180);// Sorta kinda make sure the column isn't much bigger than needed
                    columns.Add(new ColumnInfo("OTHER KILLTYPES", 0, width, tinyFont, (a) => { return MakeKillTypesString(a.killTypes, a.killTypesRets, columnizedKillTypes); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
                }

                columns.Add(new ColumnInfo("KILLED PLAYERS", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,false); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
                columns.Add(new ColumnInfo("KILLED BY", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,true); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
            }


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

            // for little optional notes like DISCONNECTED or such
            ColumnInfo preColumn = new ColumnInfo("", 0, 270, tinyFont, (a) =>
            {
                List<string> markers = new List<string>();
                if (a.fightBot) markers.Add("FIGHTBOT");
                else if (a.isBot) markers.Add("BOT");
                if (a.realTeam == Team.Spectator && a.team != Team.Spectator) markers.Add("WENTSPEC");
                if (!a.isStillActivePlayer)
                {
                    string lastSeenString = "";
                    TimeSpan timeSince = DateTime.Now - a.lastSeen;
                    if (timeSince.TotalDays > 0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalDays}D";
                    }
                    else if (timeSince.TotalHours > 0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalHours}H";
                    }
                    else if (timeSince.TotalMinutes > 0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalMinutes}M";
                    }
                    else if (timeSince.TotalSeconds > 0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalSeconds}S";
                    }
                    else if (timeSince.TotalMilliseconds > 0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalMilliseconds}MS";
                    }
                    markers.Add($"DISC{lastSeenString}");
                }
                if (markers.Count == 0) return "";
                int halfMarkers = markers.Count / 2;
                string[] row1 = markers.GetRange(0, halfMarkers).ToArray();
                string[] row2 = markers.GetRange(halfMarkers, markers.Count - halfMarkers).ToArray();
                if (row1 is null || row1.Length == 0) {
                    row1 = row2;
                    row2 = null;
                } 
                if (row1 is null) row1 = new string[0];
                if (row2 is null) row2 = new string[0];
                return $"{string.Join(',', row1)}\r\n{string.Join(',', row2)}";
            })
            {  overflowMode= ColumnInfo.OverflowMode.WrapClip, rightAlign = true};

            float posXStart = sidePadding;
            float posX = posXStart;
            float posY = 20;
            foreach (var column in columns)
            {
                column.DrawString(g,true,posX,posY+10);
                posX += column.width+ horzPadding;
            }
            foreach (ScoreboardEntry entry in entries)
            {
                posY += recordHeight+ vertPadding;
                posX = posXStart;

                preColumn.DrawString(g,false,posX- preColumn.width-horzPadding, posY, entry);

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

                bool brighterBg = true;
                foreach (var column in columns)
                {
                    if (brighterBg)
                    {
                        g.FillRectangle(brighterBGBrush, new RectangleF(posX- 0.5f * horzPadding, posY, column.width + horzPadding, 30.0f));
                    }
                    if (column.needsLeftLine)
                    {
                        //g.DrawLine(linePen, posX-0.5f* horzPadding, posY, posX- 0.5f * horzPadding, posY + 30f);
                        //g.FillRectangle(Brushes.White32fg, new RectangleF(posX, posY, 0.5f, 30.0f));
                    }
                    posX += column.width + horzPadding;
                    brighterBg = !brighterBg;
                }
                posX = posXStart;

                if (brush != null)
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
