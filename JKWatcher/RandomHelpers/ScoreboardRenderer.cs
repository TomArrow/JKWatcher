﻿using JKClient;
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
    // TODO parse g_printstats for extra thisgame info

    // TODO highlight top time in defrag
    // TODO what a re values when connecting/laoding map?

    // TODO on map change: prioritize players who have left before the previous mapchange?

    // TODO defrag: after WR and maybe top10, order by best average times on maps? or best best times?
    // TODO flaghold/longest flaghold. 
    // TODO ctf pauses fuck up time

    // TODO Defrag: runs: total, top 10, #1, total run time. map WRs, map top 10s. average deviation + per map

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
    //using RunsTop10WR = Tuple<int, int, int>;

    class RunsTop10WR {
        public int runs;
        public int top10s;
        public int wrs;
        public RunsTop10WR(int runsA,int top10sA,int wrsA)
        {
            runs = runsA;
            top10s = top10sA;
            wrs = wrsA;
        }
        public static int Comparer(RunsTop10WR a, RunsTop10WR b)
        {
            // WR most important. Top10 then. Then run count.
            if (a.wrs != b.wrs)
            {
                return b.wrs.CompareTo(a.wrs);
            }
            if (a.top10s != b.top10s)
            {
                return b.top10s.CompareTo(a.top10s);
            }
            return b.runs.CompareTo(a.runs);
        }
        public static int Comparer<T>(KeyValuePair<T,RunsTop10WR> a, KeyValuePair<T, RunsTop10WR> b)
        {
            // WR most important. Top10 then. Then run count.
            if (a.Value.wrs != b.Value.wrs)
            {
                return b.Value.wrs.CompareTo(a.Value.wrs);
            }
            if (a.Value.top10s != b.Value.top10s)
            {
                return b.Value.top10s.CompareTo(a.Value.top10s);
            }
            return b.Value.runs.CompareTo(a.Value.runs);
        }
    }

    class ScoreboardEntry
    {
        public IdentifiedPlayerStats stats;

        // we copy a few properties to here to sort by.
        // since the player session data could be changed while we are sorting,
        // sorting might throw an exception, so we first create copies of the sort-relevant data.
        public bool isStillActivePlayer;
        public bool activeSinceLastThisGameReset;
        public Team team;
        public Team realTeam;
        public int score;
        public DateTime lastSeen;
        public int teamScore;
        public Dictionary<string, int> killTypes = new Dictionary<string, int>();
        public Dictionary<string, int> killTypesRets = new Dictionary<string, int>();

        public bool fightBot;
        public bool isBot;

        public int runs;
        public int top10s;
        public int wrs;

        public int returns;// This is just for convenience.
        public int returnsOldSum;

        //public int dfas;
        //public int ydfas;
        //public int bses;
        //public int blubses;
        //public int dbses;
        public static Dictionary<string, int> slashTypeIndex = new Dictionary<string, int>()
        {
            { "DBS",0 },
            { "BS",1 },
            { "BLUBS",2 },
            { "DFA",3 },
            { "YDFA",4 },
        };
        public int[] slashCounts = new int[5];
        public int[] mineGrabs = new int[4];
        public int mineGrabsTotal;

        public PlayerScore scoreCopy;

        public static int Comparer(ScoreboardEntry a, ScoreboardEntry b)
        {
            // In case of defrag
            if (a.wrs != b.wrs)
            {
                return b.wrs.CompareTo(a.wrs);
            }
            if (a.top10s != b.top10s)
            {
                return b.top10s.CompareTo(a.top10s);
            }
            if (a.runs != b.runs)
            {
                return b.runs.CompareTo(a.runs);
            }

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
            // In case of defrag
            if (a.wrs != b.wrs)
            {
                return b.wrs.CompareTo(a.wrs);
            }
            if (a.top10s != b.top10s)
            {
                return b.top10s.CompareTo(a.top10s);
            }
            if (a.runs != b.runs)
            {
                return b.runs.CompareTo(a.runs);
            }

            bool aSpec = a.team == Team.Spectator;
            bool bSpec = b.team == Team.Spectator;

            if(aSpec != bSpec)
            {
                return aSpec.CompareTo(bSpec); // spectators always last
            }

            if(a.isStillActivePlayer != b.isStillActivePlayer)
            {
                return b.isStillActivePlayer.CompareTo(a.isStillActivePlayer); // disconnected players also gonna have lower rank
            }

            if(a.activeSinceLastThisGameReset != b.activeSinceLastThisGameReset)
            {
                return b.activeSinceLastThisGameReset.CompareTo(a.activeSinceLastThisGameReset); // players who disconnected before last thisgame reset (like map_restart/gamestate/mapchange) gonna have lower rank
            }

            int aScore = a.score;
            int bScore = b.score;
            return bScore.CompareTo(aScore);
        }
        public static int CountReductionComparerIgnoreActivity(ScoreboardEntry a, ScoreboardEntry b) // this sorts by which players we most want to keep, not neccessarily by which order they should be displayed in.
        {
            // In case of defrag
            if (a.wrs != b.wrs)
            {
                return b.wrs.CompareTo(a.wrs);
            }
            if (a.top10s != b.top10s)
            {
                return b.top10s.CompareTo(a.top10s);
            }
            if (a.runs != b.runs)
            {
                return b.runs.CompareTo(a.runs);
            }

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

    class CSVColumnInfo
    {
        Func<ScoreboardEntry, string> fetcher = null;
        string name = null;
        public CSVColumnInfo(string nameA, Func<ScoreboardEntry, string> fetchFunc)
        {
            if (nameA is null || fetchFunc is null)
            {
                throw new InvalidOperationException("CSVColumnInfo must be created with non null name and fetchFunc");
            }
            name = nameA;
            fetcher = fetchFunc;
        }
        public static string EscapeValue(string input)
        {
            input = input.Replace("\"", "\"\"");
            return $"\"{input}\"";
        }
        public string WriteHeaderColumn()
        {
            return EscapeValue(name);
        }
        public void WriteHeaderColumn(StringBuilder csv)
        {
            csv.Append(EscapeValue(name));
        }
        public string WriteDataColumn(ScoreboardEntry entry)
        {
            return EscapeValue(fetcher(entry));
        }
        public void WriteDataColumn(StringBuilder csv, ScoreboardEntry entry)
        {
            string data = fetcher(entry);
            if(data == null)
            {
                Helpers.logToFile($"CSV column {name} data is null wtf",true);
                data = "";
            }
            csv.Append(EscapeValue(data));
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
        public OverflowMode overflowModeHeader = OverflowMode.AllowOverflow;
        //public bool autoScale = false;
        //public bool allowWrap = true;
        public bool angledHeader = false;
        public bool needsLeftLine = false;
        public bool rightAlign = false;
        public float headerYOffset = 10.0f;
        public bool noAdvanceAfter = false; // if u want to add a second line or such
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

            if (header)
            {
                y += headerYOffset;
            }

            OverflowMode overflowModeHere = header ? overflowModeHeader : overflowMode;

            //StringFormat formatToUse = (header || (overflowMode != OverflowMode.WrapClip)) ? (rightAlign ? noWrapStringFormatRightAlign: noWrapStringFormat) : ( rightAlign ? defaultStringFormatRightAlign: defaultStringFormat);
            StringFormat formatToUse = (overflowModeHere != OverflowMode.WrapClip) ? (rightAlign ? noWrapStringFormatRightAlign: noWrapStringFormat) : ( rightAlign ? defaultStringFormatRightAlign: defaultStringFormat);

            string cleanString = null;
            bool fontWasReplaced = false;
            const float reductionDecrements = 0.5f;
            //if (overflowMode == OverflowMode.AutoScale && !header)
            if (overflowModeHere == OverflowMode.AutoScale)
            {
                cleanString = Q3ColorFormatter.cleanupString(theString);
                float trySize = fontToUse.Size;
                bool tooBig = false;
                while ((tooBig=g.MeasureString(cleanString, fontToUse, new PointF(x,y),formatToUse).Width > width) && trySize >= 5)
                {
                    trySize -= reductionDecrements;
                    if (fontWasReplaced)
                    {
                        fontToUse.Dispose();
                    }
                    fontToUse = new Font(fontToUse.FontFamily, trySize, fontToUse.Style, fontToUse.Unit);
                    fontWasReplaced = true;
                }
                if (tooBig)
                {
                    // nvm this doesnt work. it doesnt wrap on individual characters
                    // still too big. wrap.
                    //overflowModeHere = OverflowMode.WrapClip;
                    //if (header)
                    //{
                    //    y -= headerYOffset;
                    //}
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

            //if (overflowMode == OverflowMode.AllowOverflow || angledHeader)
            if (overflowModeHere == OverflowMode.AllowOverflow || angledHeader && header)
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
        private static readonly Font headerFont = new Font("Segoe UI", 18,FontStyle.Bold);
        private static readonly Font nameFont = new Font("Segoe UI", 14,FontStyle.Bold);
        private static readonly Font normalFont = new Font("Segoe UI", 10);
        private static readonly Font tinyFont = new Font("Segoe UI", 8);
        private static readonly string[] killTypesAlways = new string[] {string.Intern("DBS"), /*"DFA", "BS", "DOOM",*/ string.Intern("MINE") }; // these will always get a column if at least 1 was found.
        private static readonly string[] killTypesCSV = new string[] {string.Intern("DFA"),string.Intern("RED"),string.Intern("YEL"),string.Intern("BLU"),string.Intern("DBS"),string.Intern("BS"),string.Intern("MINE"), string.Intern("UPCUT"), string.Intern("YDFA"), string.Intern("BLUBS"), string.Intern("DOOM"), string.Intern("TUR"), string.Intern("UNKN"), string.Intern("IDLE") }; // these will always get a column if at least 1 was found.

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
            TOTALKILLS,
            DEFRAG,
            DEFRAGTOP10,
            DEFRAGWR,
            GAUNTLETCOUNT,
            //DBSPERCENT,
            MINEGRABS
        }

        const int fgAlpha = (int)((float)255 * 0.8f);
        const int bgAlpha = (int)((float)255 * 0.33f);
        const int color0_2 = (int)((float)255 * 0.2f);
        const int color0_8 = (int)((float)255 * 0.8f);
        static readonly Brush redFlagBrush = new SolidBrush(Color.FromArgb(fgAlpha, 255, color0_2, color0_2));
        static readonly Brush blueFlagBrush = new SolidBrush(Color.FromArgb(fgAlpha, color0_2, 192, 255));
        static readonly Brush dominanceBrush = new SolidBrush(Color.FromArgb(fgAlpha, 255, 255, 0));
        static readonly Pen redFlagPen = new Pen(redFlagBrush,1.0f);
        static readonly Pen blueFlagPen = new Pen(blueFlagBrush, 1.0f);
        static readonly Pen dominancePen = new Pen(dominanceBrush, 0.5f);
        static readonly Brush redTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, color0_2, color0_2));
        static readonly Brush blueTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_2, color0_2, 255));
        static readonly Brush freeTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, color0_8, color0_8, 0));
        static readonly Brush spectatorTeamBrush = new SolidBrush(Color.FromArgb(bgAlpha, 128, 128, 128));
        static readonly Brush brighterBGBrush = new SolidBrush(Color.FromArgb(bgAlpha / 4, 128, 128, 128));
        static readonly Brush gameTimeBrush = new SolidBrush(Color.FromArgb(bgAlpha, 64, 64, 64));
        static readonly Brush gamePausedBrush = new SolidBrush(Color.FromArgb(bgAlpha, 128, 128, 0));
        static readonly Brush darkenBgBrush = new SolidBrush(Color.FromArgb(bgAlpha, 0, 0, 0));
        static readonly Pen linePen = new Pen(Color.FromArgb(bgAlpha/2, 128, 128, 128),0.5f);

        private static string block0(string input, string prefixIfUnblocked="")
        {
            return input == "0" ? "" : $"{prefixIfUnblocked}{input}";
        }

        private static float exaggerate0to1scale(float input)
        {
            input = input.ValueOrDefault(0.5f) * 2.0f - 1.0f;
            input = (float)Math.Sign(input) * (float)Math.Sqrt(Math.Abs(input));
            return (input + 1.0f) * 0.5f;
        }

        public static void DrawScoreboard(
            Bitmap bmp,
            bool thisGame,
            ConcurrentDictionary<SessionPlayerInfo, IdentifiedPlayerStats> ratingsAndNames, 
            ServerSharedInformationPool infoPool,
            bool all,
            GameType gameType,
            StringBuilder csvData
            )
        {

            bool anyKillsLogged = false;
            bool anyValidGlicko2 = false;

            if (!thisGame) infoPool.ratingCalculator.UpdateRatings(infoPool.ratingPeriodResults, true);
            else infoPool.ratingCalculatorThisGame.UpdateRatings(infoPool.ratingPeriodResultsThisGame, true);

            string whenString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            bool isIntermission = infoPool.isIntermission;
            GameStats gameStats = infoPool.gameStatsThisGame;
            string serverName = infoPool.ServerName;
            string mapName = infoPool.MapName;
            int redScore = infoPool.ScoreRed;
            int blueScore = infoPool.ScoreBlue;
            List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
            Dictionary<string, int> killTypesCounts = new Dictionary<string, int>();
            Dictionary<string, int> killTypesCountsMax = new Dictionary<string, int>();
            Dictionary<string, int> killTypesCountsRetsMax = new Dictionary<string, int>();
            Dictionary<string, RunsTop10WR> defragBestTimesPlayerCount = new Dictionary<string, RunsTop10WR>(StringComparer.InvariantCultureIgnoreCase);
            bool serverSendsRets = infoPool.serverSeemsToSupportRetsCountScoreboard;
            Int64 foundFields = 0;
            foreach(var kvp in ratingsAndNames)
            {
                ScoreboardEntry entry   = new ScoreboardEntry();
                entry.stats = kvp.Value;
                entry.team = kvp.Value.chatCommandTrackingStuff.LastNonSpectatorTeam;
                entry.realTeam = kvp.Value.playerSessInfo.team;
                entry.score = kvp.Value.chatCommandTrackingStuff.score.score + kvp.Value.chatCommandTrackingStuff.score.oldScoreSum;
                entry.runs = kvp.Value.chatCommandTrackingStuff.defragRunsFinished;
                entry.top10s = kvp.Value.chatCommandTrackingStuff.defragTop10RunCount;
                entry.wrs = kvp.Value.chatCommandTrackingStuff.defragWRCount;
                entry.slashCounts[ScoreboardEntry.slashTypeIndex["DBS"]] = kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_BACK_CR_GENERAL);
                entry.slashCounts[ScoreboardEntry.slashTypeIndex["BS"]] = kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_BACK_GENERAL);
                entry.slashCounts[ScoreboardEntry.slashTypeIndex["BLUBS"]] = kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_BACKSTAB_GENERAL);
                entry.slashCounts[ScoreboardEntry.slashTypeIndex["DFA"]] = kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_JUMP_T__B__GENERAL);
                entry.slashCounts[ScoreboardEntry.slashTypeIndex["YDFA"]] = kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_FLIP_STAB_GENERAL) + kvp.Value.chatCommandTrackingStuff.slashTypeCounter.GetValue((int)SaberMovesGeneral.LS_A_FLIP_SLASH_GENERAL);
                entry.mineGrabsTotal = 0;
                for(int i = 0; i < 4; i++)
                {
                    entry.mineGrabsTotal += kvp.Value.chatCommandTrackingStuff.minePickupCounter[i].value;
                    entry.mineGrabs[i] = kvp.Value.chatCommandTrackingStuff.minePickupCounter[i].value;
                }
                entry.isBot = kvp.Value.playerSessInfo.confirmedBot || kvp.Value.playerSessInfo.confirmedJKWatcherFightbot;
                entry.fightBot = kvp.Value.playerSessInfo.confirmedJKWatcherFightbot;
                entry.scoreCopy = kvp.Value.chatCommandTrackingStuff.score.Clone() as PlayerScore; // make copy because creating screenshot may take a few seconds and data might change drastically
                if (entry.scoreCopy is null) entry.scoreCopy = kvp.Value.chatCommandTrackingStuff.score; // idk why this should happen but lets be safe
                anyValidGlicko2 = anyValidGlicko2 || kvp.Value.chatCommandTrackingStuff.rating.GetNumberOfResults(true) > 0;
                anyKillsLogged = anyKillsLogged || kvp.Value.chatCommandTrackingStuff.totalKills > 0 || kvp.Value.chatCommandTrackingStuff.totalDeaths > 0;
                if (entry.team != Team.Spectator && kvp.Value.playerSessInfo.team == Team.Spectator && (entry.score <= 0 || kvp.Value.chatCommandTrackingStuff.score.time == 0))
                {
                    // we do want to show anyone who played, but if someone just showed up as player while connecting or just auto-joined quickly at the start and promptly
                    // went spec, we don't wanna list him as a player of that match.
                    // TODO maybe also always do if bot? or naw?
                    entry.team = Team.Spectator;
                }
                entry.teamScore = 0;
                bool returnScoreValue = kvp.Value.chatCommandTrackingStuff.returns < kvp.Value.chatCommandTrackingStuff.score.impressiveCount;
                entry.returns = returnScoreValue ? kvp.Value.chatCommandTrackingStuff.score.impressiveCount : kvp.Value.chatCommandTrackingStuff.returns;
                entry.returnsOldSum = returnScoreValue ? kvp.Value.chatCommandTrackingStuff.score.impressiveCount.oldSum : 0;
                if(entry.returns > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.RETURNS);
                }
                //if(entry.dbses >= 5)
                //{
                //    foundFields |= (1 << (int)ScoreFields.DBSPERCENT);
                //}
                if(entry.mineGrabsTotal > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.MINEGRABS);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.captures > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.CAPTURES);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.defendCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFEND);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.assistCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.ASSIST);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.guantletCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.GAUNTLETCOUNT);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.excellentCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.SPREEKILLS);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.accuracy > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.ACCURACY);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.deaths > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEATHS);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.kills > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.KILLS);
                }
                if(kvp.Value.chatCommandTrackingStuff.score.totalKills > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.TOTALKILLS);
                }
                if(kvp.Value.chatCommandTrackingStuff.defragRunsFinished > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFRAG);
                }
                if(kvp.Value.chatCommandTrackingStuff.defragTop10RunCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFRAGTOP10);
                }
                if(kvp.Value.chatCommandTrackingStuff.defragWRCount > 0)
                {
                    foundFields |= (1 << (int)ScoreFields.DEFRAGWR);
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
                    if(pi.session == kvp.Key && pi.infoValid)
                    {
                        entry.isStillActivePlayer = true;
                        break;
                    }
                }
                entry.lastSeen = kvp.Value.lastSeenActive;
                entry.activeSinceLastThisGameReset = entry.lastSeen > infoPool.lastThisGameReset;

                // keep track of all defrag maps that we have runs of
                foreach (var dt in kvp.Value.chatCommandTrackingStuff.defragMapsRun)
                {
                    if (!defragBestTimesPlayerCount.ContainsKey(dt.Key))
                    {
                        defragBestTimesPlayerCount[dt.Key] = new RunsTop10WR(1,0,0);
                    }
                    else
                    {
                        defragBestTimesPlayerCount[dt.Key].runs++; // we are not counting the amount of runs. just how many players ran it (or got top10 etc)
                    }
                }
                foreach(var dt in kvp.Value.chatCommandTrackingStuff.defragMapsTop10)
                {
                    if (!defragBestTimesPlayerCount.ContainsKey(dt.Key))
                    {
                        defragBestTimesPlayerCount[dt.Key] = new RunsTop10WR(0,1,0);
                    }
                    else
                    {
                        defragBestTimesPlayerCount[dt.Key].top10s++; // we are not counting the amount of runs. just how many players ran it (or got top10 etc)
                    }
                }
                foreach(var dt in kvp.Value.chatCommandTrackingStuff.defragMapsWR)
                {
                    if (!defragBestTimesPlayerCount.ContainsKey(dt.Key))
                    {
                        defragBestTimesPlayerCount[dt.Key] = new RunsTop10WR(0,0,1);
                    }
                    else
                    {
                        defragBestTimesPlayerCount[dt.Key].wrs++; // we are not counting the amount of runs. just how many players ran it (or got top10 etc)
                    }
                }

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

            // Sort defrag maps (by how many players have a logged best time in them)
            List<KeyValuePair<string, RunsTop10WR>> defragMapsList = defragBestTimesPlayerCount.ToList();
            defragMapsList.Sort(RunsTop10WR.Comparer); // columns with most players that got wr first. most players that got top10 then. etc. to see most relevant columns.
            const int mapTimesColumnCount = 8;

            List<string> mapTimeColumns = new List<string>();
            HashSet<string> mapsWithoutTimeColumns = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var dt in defragMapsList)
            {
                if (mapTimeColumns.Count >= mapTimesColumnCount)
                {
                    mapsWithoutTimeColumns.Add(dt.Key);
                }
                else
                {
                    mapTimeColumns.Add(dt.Key);
                }
            }


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
            List<CSVColumnInfo> csvColumns = new List<CSVColumnInfo>();


            columns.Add(new ColumnInfo("CL", 0, 25, normalFont, (a) => { return a.stats.playerSessInfo.clientNum.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("CL", (a) => { return a.stats.playerSessInfo.clientNum.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("LAST-NONSPEC-TEAM", (a) => { return a.team.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("CURRENT-TEAM", (a) => { return a.realTeam.ToString(); }));
            columns.Add(new ColumnInfo("NAME", 0, 220, nameFont, (a) => { return a.stats.playerSessInfo.GetNameOrLastNonPadaName(); }) { overflowMode= ColumnInfo.OverflowMode.AutoScale});
            csvColumns.Add(new CSVColumnInfo("NAME-CLEAN", (a) => { return Q3ColorFormatter.cleanupString(a.stats.playerSessInfo.GetNameOrLastNonPadaName()); }) );
            csvColumns.Add(new CSVColumnInfo("NAME-RAW", (a) => { return a.stats.playerSessInfo.GetNameOrLastNonPadaName(); }) );
            columns.Add(new ColumnInfo("SCORE", 0, 50, normalFont, (a) => { 
                if(a.scoreCopy.oldScoreSum > 0)
                {
                    return a.scoreCopy.score == -9999 ? $"^yfff8∑^7{a.scoreCopy.oldScoreSum}" : $"^yfff8∑^7{a.scoreCopy.oldScoreSum+a.scoreCopy.score}";
                }
                else
                {
                    return a.scoreCopy.score == -9999 ? "--" : a.scoreCopy.score.ToString();
                }
            }) { noAdvanceAfter=true});
            csvColumns.Add(new CSVColumnInfo("SCORE-CURRENT", (a) => { return a.scoreCopy.score == -9999 ? "" : $"{a.scoreCopy.score}"; }));
            csvColumns.Add(new CSVColumnInfo("SCORE-SUM", (a) => { return a.scoreCopy.score == -9999 ? $"{a.scoreCopy.oldScoreSum}" : $"{a.scoreCopy.oldScoreSum + a.scoreCopy.score}"; }));

            columns.Add(new ColumnInfo("", 15, 50, tinyFont, (a) => { var oldsum = a.scoreCopy.oldScoreSum; return oldsum > 0 ? (a.scoreCopy.score == -9999 ? "^yfff8--" : $"^yfff8{a.scoreCopy.score}") : ""; }));
            if ((foundFields & (1 << (int)ScoreFields.CAPTURES)) >0)
            {
                columns.Add(new ColumnInfo("C", 0, 20, normalFont, (a) => { return block0(a.scoreCopy.captures.GetString1()); }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 20, tinyFont, (a) => { return block0(a.scoreCopy.captures.GetString2(), "^yfff8"); }));
            }
            csvColumns.Add(new CSVColumnInfo("CAPTURES-CURRENT", (a) => { return a.scoreCopy.captures.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("CAPTURES-SUM", (a) => { return (a.scoreCopy.captures.value + a.scoreCopy.captures.oldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.RETURNS)) >0)
            {
                columns.Add(new ColumnInfo("R", 0, 25, normalFont, (a) => { 
                    if (a.returnsOldSum > 0)
                    {
                        return $"^yfff8∑^7{a.returnsOldSum + a.returns}";
                    }
                    else
                    {
                        return block0(a.returns.ToString());
                    }
                }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 25, tinyFont, (a) => {  return a.returnsOldSum > 0 ? block0(a.returns.ToString()) : ""; }));
            }
            csvColumns.Add(new CSVColumnInfo("RETURNS-CURRENT", (a) => { return a.returns.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("RETURNS-SUM", (a) => { return (a.returns + a.returnsOldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.DEFEND)) >0)
            {
                columns.Add(new ColumnInfo("BC", 0, 30, normalFont, (a) => { return block0(a.scoreCopy.defendCount.GetString1());  }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 30, tinyFont, (a) => { return block0(a.scoreCopy.defendCount.GetString2(), "^yfff8"); ; }));
            }
            csvColumns.Add(new CSVColumnInfo("BC-CURRENT", (a) => { return a.scoreCopy.defendCount.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("BC-SUM", (a) => { return (a.scoreCopy.defendCount.value + a.scoreCopy.defendCount.oldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.ASSIST)) >0)
            {
                columns.Add(new ColumnInfo("A", 0, 23, normalFont, (a) => { return block0(a.scoreCopy.assistCount.GetString1()); }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 23, tinyFont, (a) => { return block0(a.scoreCopy.assistCount.GetString2(), "^yfff8"); }));
            }
            csvColumns.Add(new CSVColumnInfo("ASSISTS-CURRENT", (a) => { return a.scoreCopy.assistCount.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("ASSISTS-SUM", (a) => { return (a.scoreCopy.assistCount.value + a.scoreCopy.assistCount.oldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.GAUNTLETCOUNT)) >0)
            {
                if (infoPool.NWHDetected)
                {
                    columns.Add(new ColumnInfo("FH", 0, 70, normalFont, (a) => { return a.scoreCopy.guantletCount.GetString1(true, null, (a) => { return FormatTime(a); }); }) { noAdvanceAfter = true });
                    columns.Add(new ColumnInfo("", 15, 70, tinyFont, (a) => { return a.scoreCopy.guantletCount.GetString2(true, "^yfff8", (a) => { return FormatTime(a); }); }));
                }
                else
                {
                    columns.Add(new ColumnInfo("G", 0, 20, normalFont, (a) => { return block0(a.scoreCopy.guantletCount.GetString1()); }) { noAdvanceAfter = true });
                    columns.Add(new ColumnInfo("", 15, 20, tinyFont, (a) => { return block0(a.scoreCopy.guantletCount.GetString2(), "^yfff8"); }));
                }
            }
            csvColumns.Add(new CSVColumnInfo(infoPool.NWHDetected ? "FLAGHOLD-CURRENT" : "GAUNTLET-CURRENT", (a) => { return a.scoreCopy.guantletCount.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo(infoPool.NWHDetected ? "FLAGHOLD-SUM" : "GAUNTLET-SUM", (a) => { return (a.scoreCopy.guantletCount.value + a.scoreCopy.guantletCount.oldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.SPREEKILLS)) >0)
            {
                columns.Add(new ColumnInfo(infoPool.NWHDetected ? "FG" : "»", 0, 25, normalFont, (a) => { return block0(a.scoreCopy.excellentCount.GetString1()); }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 25, tinyFont, (a) => { return block0(a.scoreCopy.excellentCount.GetString2(), "^yfff8"); }));
            }
            csvColumns.Add(new CSVColumnInfo(infoPool.NWHDetected ? "FLAGGRABS-CURRENT" : "SPREEKILLS-CURRENT", (a) => { return a.scoreCopy.excellentCount.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo(infoPool.NWHDetected ? "FLAGGRABS-SUM" : "SPREEKILLS-SUM", (a) => { return (a.scoreCopy.excellentCount.value + a.scoreCopy.excellentCount.oldSum).ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.MINEGRABS)) >0)
            {
                columns.Add(new ColumnInfo("MG", 0, 40, normalFont, (a) => {
                    if (a.mineGrabsTotal == 0) return "";
                    if(gameType != GameType.CTF && gameType != GameType.CTY)
                    {
                        return $"{a.mineGrabsTotal}";
                    }
                    else if(a.mineGrabs[(int)Team.Red] > 0 && a.mineGrabs[(int)Team.Blue] > 0)
                    {
                        return $"^1{a.mineGrabs[(int)Team.Red]}^7/^x0af{a.mineGrabs[(int)Team.Blue]}";
                    } else if (a.mineGrabs[(int)Team.Red] > 0)
                    {
                        return $"^1{a.mineGrabs[(int)Team.Red]}";
                    }
                    else if(a.mineGrabs[(int)Team.Blue] > 0)
                    {
                        return $"^x0af{a.mineGrabs[(int)Team.Blue]}";
                    }
                    else
                    {
                        return $"";
                    }
                }) {noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 40, tinyFont, (a) => {
                    if (gameType != GameType.CTF && gameType != GameType.CTY || a.mineGrabs[(int)Team.Free] <= 0)
                    {
                        return "";
                    }

                    return $"^yfff8{a.mineGrabs[(int)Team.Free]}";
                }));
            }
            csvColumns.Add(new CSVColumnInfo("MINEGRABS-TOTAL", (a) => { return a.mineGrabsTotal.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("MINEGRABS-REDBASE", (a) => { return a.mineGrabs[(int)Team.Red].ToString(); }));
            csvColumns.Add(new CSVColumnInfo("MINEGRABS-BLUEBASE", (a) => { return a.mineGrabs[(int)Team.Blue].ToString(); }));
            csvColumns.Add(new CSVColumnInfo("MINEGRABS-NEUTRAL", (a) => { return a.mineGrabs[(int)Team.Free].ToString(); }));

            if ((foundFields & (1 << (int)ScoreFields.ACCURACY)) >0)
            {
                columns.Add(new ColumnInfo("%", 0, infoPool.NWHDetected ? 40: 20, normalFont, (a) => { return block0(a.scoreCopy.accuracy.ToString()); }));
            }
            csvColumns.Add(new CSVColumnInfo("ACCURACY-WEIRD", (a) => { return a.scoreCopy.accuracy.ToString(); }));
            if ((foundFields & (1 << (int)ScoreFields.DEFRAG)) >0)
            {
                columns.Add(new ColumnInfo("DEFRAG", 0, 90, normalFont, (a) => {
                    if (a.stats.chatCommandTrackingStuff.defragRunsFinished <= 0) {
                        return "";
                    }
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"^yfff8{a.stats.chatCommandTrackingStuff.defragRunsFinished.ToString()}");
                    if (a.stats.chatCommandTrackingStuff.defragTop10RunCount > 0)
                    {
                        sb.Append($"^7/{a.stats.chatCommandTrackingStuff.defragTop10RunCount.ToString()}");
                    }
                    if (a.stats.chatCommandTrackingStuff.defragWRCount > 0)
                    {
                        sb.Append($"^7/^2{a.stats.chatCommandTrackingStuff.defragWRCount.ToString()}");
                    }
                    sb.Append(GetAverageDefragDeviation(a));
                    return sb.ToString(); 
                }));
                foreach(string map in mapTimeColumns)
                {
                    columns.Add(new ColumnInfo($"^3{map.ToUpperInvariant()}", 0, 80, normalFont, (a) =>
                    {
                        if(a.stats.chatCommandTrackingStuff.defragBestTimes.TryGetValue(map,out int besttime))
                        {
                            DefragAverageMapTime mapInfo = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(map);
                            string color = "";
                            if(mapInfo!= null)
                            {
                                // TODO make sure someone who has unlogged best time with same number doesnt accidentally get green color?
                                if(mapInfo.recordHolder != null && mapInfo.record == besttime)
                                {
                                    color = "^2";
                                } else if(mapInfo.unloggedRecordHolder != null && mapInfo.unloggedRecord == besttime)
                                {
                                    color = "^3";
                                }
                            }
                            return $"{color}{FormatTime(besttime)}";
                        }
                        return "";
                    })
                    {  overflowModeHeader = ColumnInfo.OverflowMode.AutoScale, noAdvanceAfter = true });
                    // second row: runs/top10/wr/% relative to average
                    columns.Add(new ColumnInfo($"", 15, 80, tinyFont, (a) =>
                    {
                        var tracker = a.stats.chatCommandTrackingStuff;
                        if (!tracker.defragMapsRun.ContainsKey(map))
                        {
                            return "";
                        }
                        StringBuilder sb = new StringBuilder();
                        sb.Append($"^yfff8{tracker.defragMapsRun[map].ToString()}");
                        if (tracker.defragMapsTop10.TryGetValue(map,out int val))
                        {
                            sb.Append($"^7/{val}");
                        }
                        if (tracker.defragMapsWR.TryGetValue(map,out int val2))
                        {
                            sb.Append($"^7/^2{val2}");
                        }
                        sb.Append(GetAverageDefragDeviation(a,map));
                        return sb.ToString();
                    }));
                }
                columns.Add(new ColumnInfo("SUM RUNS", 0, 80, normalFont, (a) => {
                    Int64 totalRunTime = a.stats.chatCommandTrackingStuff.defragTotalRunTime;
                    if (totalRunTime <= 0) return "";
                    return FormatTime(totalRunTime);
                }));
                if(mapsWithoutTimeColumns.Count > 0)
                {
                    columns.Add(new ColumnInfo("MAPS", 0, 350, tinyFont, (a) =>
                    {
                        return MakeDefragMapsString(a, mapsWithoutTimeColumns);
                    })
                    { overflowMode = ColumnInfo.OverflowMode.WrapClip });
                }
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
            csvColumns.Add(new CSVColumnInfo("KILLS", (a) => {
                if (anyKillsLogged)
                {
                    return a.stats.chatCommandTrackingStuff.totalKills.ToString();
                }
                else if((foundFields & (1 << (int)ScoreFields.KILLS)) > 0)
                {
                    return a.scoreCopy.kills.ToString();
                }
                else
                {
                    return "";
                }
            }));
            csvColumns.Add(new CSVColumnInfo("DEATHS", (a) => {
                if (anyKillsLogged)
                {
                    return a.stats.chatCommandTrackingStuff.totalDeaths.ToString();
                }
                else if((foundFields & (1 << (int)ScoreFields.DEATHS)) > 0)
                {
                    return a.scoreCopy.deaths.ToString();
                }
                else
                {
                    return "";
                }
            }));
            csvColumns.Add(new CSVColumnInfo("TOTALKILLS-MOH", (a) => {
                if((foundFields & (1 << (int)ScoreFields.TOTALKILLS)) > 0)
                {
                    return a.scoreCopy.totalKills.ToString();
                }
                else
                {
                    return "";
                }
            }));

            columns.Add(new ColumnInfo("PING", 0, 50, normalFont, (a) => { return a.scoreCopy.ping.ToString(); }) { noAdvanceAfter=true });
            columns.Add(new ColumnInfo("", 15, 50, tinyFont, (a) => { string meansd = a.scoreCopy.ping.GetMeanSDString(); return !string.IsNullOrEmpty(meansd) ? $"^yfff8{meansd}" : ""; }));

            csvColumns.Add(new CSVColumnInfo("PING-CURRENT", (a) => { return a.scoreCopy.ping.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("PING-MEAN", (a) => { double? avg = a.scoreCopy.ping.GetPreciseAverage(); return (!avg.HasValue || double.IsNaN(avg.Value)) ? "": avg.Value.ToString();}));
            csvColumns.Add(new CSVColumnInfo("PING-MEAN-DEVIATION", (a) => { double? dev = a.scoreCopy.ping.GetStandardDeviation(); return (!dev.HasValue || double.IsNaN(dev.Value)) ? "": dev.Value.ToString();}));

            if (infoPool.mohMode)
            {
                // reset on 0 too unreliable in MOH because they have seconds, not minutes)
                columns.Add(new ColumnInfo("TIME", 0, 40, normalFont, (a) => { return a.scoreCopy.time.GetString1(); }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 40, tinyFont, (a) => { return $"^yfff8{a.scoreCopy.time.GetString2()}"; }));
            }
            else
            {
                // need reset on 0 because pauses are a thing.... sad
                columns.Add(new ColumnInfo("TIME", 0, 40, normalFont, (a) => { return a.scoreCopy.timeResetOn0.GetString1(); }) { noAdvanceAfter = true });
                columns.Add(new ColumnInfo("", 15, 40, tinyFont, (a) => { return $"^yfff8{a.scoreCopy.timeResetOn0.GetString2()}"; }));
            }

            csvColumns.Add(new CSVColumnInfo("TIME-CURRENT", (a) => { return infoPool.mohMode ? a.scoreCopy.time.value.ToString() : a.scoreCopy.timeResetOn0.value.ToString(); }));
            csvColumns.Add(new CSVColumnInfo("TIME-SUM", (a) => { return infoPool.mohMode ? (a.scoreCopy.time.value + a.scoreCopy.time.oldSum).ToString() : (a.scoreCopy.timeResetOn0.value + a.scoreCopy.timeResetOn0.oldSum).ToString(); }));


            if (anyValidGlicko2)
            {
                //columns.Add(new ColumnInfo("GLICKO2", 0, 70, normalFont, (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRating(true)}^yfff8±{(int)a.stats.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}"; }));
                columns.Add(new ColumnInfo("G2", 0, 35, normalFont, (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRating(true)}"; }) { noAdvanceAfter=true});
                columns.Add(new ColumnInfo("", 15, 35, tinyFont, (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"^yfff8±{(int)a.stats.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}"; }));
            }

            csvColumns.Add(new CSVColumnInfo("GLICKO2-RATING", (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRating(true)}"; }));
            csvColumns.Add(new CSVColumnInfo("GLICKO2-DEVIATION", (a) => { if (a.stats.chatCommandTrackingStuff.rating.GetNumberOfResults(true) <= 0) { return ""; } return $"{(int)a.stats.chatCommandTrackingStuff.rating.GetRatingDeviation(true)}"; }));

            //if ((foundFields & (1 << (int)ScoreFields.DBSPERCENT)) > 0)
            //{
            //    columns.Add(new ColumnInfo("DBS%", 0, 30, normalFont, (a) => { return block0(a.scoreCopy.defendCount.GetString1()); }) { noAdvanceAfter = true });
            //    columns.Add(new ColumnInfo("", 15, 30, tinyFont, (a) => { return block0(a.scoreCopy.defendCount.GetString2(), "^yfff8"); ; }));
            //}

            string[] columnizedKillTypes = killTypesColumns.ToArray();

            bool firstKillType = true;
            foreach (string killTypeColumn in columnizedKillTypes)
            {
                string stringLocal = killTypeColumn;
                string measureString = block0(killTypesCountsMax.GetValueOrDefault(stringLocal, 0).ToString() + (killTypesCountsRetsMax.ContainsKey(stringLocal) ? $"/{killTypesCountsRetsMax[stringLocal]}" : ""));
                float width = Math.Max(g.MeasureString(measureString, normalFont).Width+2f,20); // Sorta kinda make sure the text will fit.
                bool showTryCount = ScoreboardEntry.slashTypeIndex.ContainsKey(stringLocal);
                int localAttemptIndex = -1;
                if (showTryCount)
                {
                    localAttemptIndex = ScoreboardEntry.slashTypeIndex[stringLocal];
                }
                columns.Add(new ColumnInfo(stringLocal, 0, width, normalFont, (a) =>
                {
                    return block0(a.killTypes.GetValueOrDefault(stringLocal, 0).ToString() + (a.killTypesRets.ContainsKey(stringLocal) ? $"/^1{a.killTypesRets[stringLocal]}" : ""));
                })
                {
                    angledHeader = true,
                    needsLeftLine = !firstKillType,
                    noAdvanceAfter = showTryCount
                });
                if (showTryCount)
                {
                    columns.Add(new ColumnInfo("", 15, width, tinyFont, (a) => { return a.slashCounts[localAttemptIndex] > 0 ? $"^yff08/{a.slashCounts[localAttemptIndex]}" : ""; }));
                }
                firstKillType = false;
            }

            if (anyKillsLogged) { 
                if(killTypesCounts.Count > 0 && !killTypesAllCovered)
                {
                    string referenceMaxString = MakeKillTypesString(killTypesCountsMax, killTypesCountsRetsMax, columnizedKillTypes);
                    float width = Math.Max(100,Math.Min(g.MeasureString(referenceMaxString, tinyFont).Width/2f + 2f, 180));// Sorta kinda make sure the column isn't much bigger than needed
                    columns.Add(new ColumnInfo("OTHER KILLTYPES", 0, width, tinyFont, (a) => { return MakeKillTypesString(a.killTypes, a.killTypesRets, columnizedKillTypes); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
                }

                columns.Add(new ColumnInfo("KILLED PLAYERS", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,false); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
                columns.Add(new ColumnInfo("KILLED BY", 0, 270, tinyFont, (a) => { return MakeKillsOnString(a,true); }) { overflowMode = ColumnInfo.OverflowMode.WrapClip });
            }


            int dbsIndex = ScoreboardEntry.slashTypeIndex["DBS"];
            foreach (string killtype in killTypesCSV)
            {
                string stringLocal = killtype;
                csvColumns.Add(new CSVColumnInfo($"{stringLocal}-KILLS", (a) =>
                {
                    return a.killTypes.GetValueOrDefault(stringLocal, 0).ToString();
                }));
                csvColumns.Add(new CSVColumnInfo($"{stringLocal}-RETURNS", (a) =>
                {
                    return a.killTypesRets.GetValueOrDefault(stringLocal, 0).ToString();
                }));
                if (stringLocal == "DBS")
                {
                    csvColumns.Add(new CSVColumnInfo($"{stringLocal}-ATTEMPTS", (a) =>
                    {
                        return a.slashCounts[dbsIndex].ToString();
                    }));
                }
            }

            const float sidePadding = 90;
            const float totalWidth = 1920 - sidePadding - sidePadding;
            float neededWidth = 0;

            // Check that it all fits in
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].noAdvanceAfter) continue;
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
                    if (timeSince.TotalDays > 1.0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalDays}D";
                    }
                    else if (timeSince.TotalHours > 1.0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalHours}H";
                    }
                    else if (timeSince.TotalMinutes > 1.0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalMinutes}M";
                    }
                    else if (timeSince.TotalSeconds > 1.0)
                    {
                        lastSeenString = $"{(int)timeSince.TotalSeconds}S";
                    }
                    else if (timeSince.TotalMilliseconds > 1.0)
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
            float posY = 20; // was 20. now fitting more stuff on top


            if (thisGame && (gameType == GameType.CTF || gameType == GameType.CTY))
            {
                string winString;
                if(redScore == blueScore)
                {
                    winString = $"Teams are tied at {redScore}-{blueScore}";
                }
                else if (isIntermission)
                {
                    winString = redScore > blueScore ? $"Red wins {redScore}-{blueScore}" : $"Blue wins {blueScore}-{redScore}";
                }
                else
                {
                    winString = redScore > blueScore ? $"Red leads {redScore}-{blueScore}" : $"Blue leads {blueScore}-{redScore}";
                }
                ColumnInfo fakeColumn = new ColumnInfo("Score", 0, 1800, headerFont, (a) => { return winString; });
                fakeColumn.DrawString(g, false, posXStart, posY, new ScoreboardEntry()); // bit awkward but the drawing part is already nicely coded in this... too lazy to redo it, too lazy to abstract it.
                posY += 30;
                fakeColumn = new ColumnInfo("Playtime", 0, 1800, normalFont, (a) => { return $"Playtime: {FormatTime(gameStats.timeTotal-gameStats.pausedTime)}, Pausetime: {FormatTime(gameStats.pausedTime)}, Total: {FormatTime(gameStats.timeTotal)}, Date/Time: {whenString}"; });
                fakeColumn.DrawString(g, false, posXStart, posY, new ScoreboardEntry());
                posY += 15;
                fakeColumn = new ColumnInfo("Mapname(fake column)", 0, 1800, nameFont, (a) => { return $"{mapName} ^7( {serverName} ^7)"; });
                fakeColumn.DrawString(g, false, posXStart, posY, new ScoreboardEntry());
                posY += 30;

                GameStatsFrame[] frames = gameStats.getFrames();
                // draw a bit of stats of how the game went
                if (frames.Length > 1) // must be > 1 so we dont divide by zero and stuff.
                {
                    float statusheight = 100.0f;
                    float bgWidth = 1920.0f - sidePadding - sidePadding;
                    GameStatsFrame startFrame = frames[0];
                    // first do background. to see pauses and normal gameplay.
                    for(int i = 1; i < frames.Length; i++)
                    {
                        if(startFrame.paused != frames[i].paused)
                        {
                            Brush brush = startFrame.paused ? gamePausedBrush : gameTimeBrush;
                            float newposX = posXStart+bgWidth * ((float)i / (float)(frames.Length - 1)); // wont divide by 0 cuz if (frames.Length > 1)
                            g.FillRectangle(brush, new RectangleF(posX, posY, newposX - posX, statusheight));
                            posX = newposX;
                            startFrame = frames[i];
                        }
                    }
                    if(startFrame != frames[frames.Length - 1])
                    {
                        Brush brush = startFrame.paused ? gamePausedBrush : gameTimeBrush;
                        float newposX = posXStart + bgWidth;
                        g.FillRectangle(brush, new RectangleF(posX, posY, newposX - posX, statusheight));
                        posX = newposX;
                    }
                    posX = posXStart;

                    // k now draw flag position n shit.
                    int skip = frames.Length/(int)bgWidth;
                    if (skip <= 0) skip = 1;
                    if (skip > 3) skip = 3; // dont do bigger gaps than 3 seconds, gonna start looking weird i think.
                    //int firstFrame = 0;
                    //while (firstFrame < frames.Length -2 && ( !float.IsFinite(frames[firstFrame].redFlagRatio) || !float.IsFinite(frames[firstFrame].blueFlagRatio)))
                    //{
                    //    firstFrame++;
                    //}
                    startFrame = frames[0];
                    float oldDominance = 0.5f;
                    for (int i = 1; i < frames.Length; i+= skip)
                    {
                        int averageMax = Math.Min(i + 10+1, frames.Length); // for dominance we wanna show an average of a 20 second range to smooth it out and show a trend rather than a precise crisp line with lots of detail.
                        float avgTotal = 0.0f;
                        int sampleCount = 0;
                        for(int j = Math.Max(i - 10,0); j < averageMax; j++) // todo sliding average here for better perf?
                        {
                            avgTotal += frames[j].dominance;
                            sampleCount++;
                        }
                        float dominanceHere = exaggerate0to1scale(avgTotal / (float)sampleCount);
                        float newposX = posXStart + bgWidth * ((float)i / (float)(frames.Length - 1)); // wont divide by 0 cuz if (frames.Length > 1)
                        g.DrawLine(dominancePen, new PointF(posX,posY + oldDominance.ValueOrDefault(0.5f) * statusheight), new PointF(newposX, posY + dominanceHere.ValueOrDefault(0.5f) * statusheight));
                        g.DrawLine(redFlagPen, new PointF(posX,posY + startFrame.redFlagRatio.ValueOrDefault(0.0f) * statusheight), new PointF(newposX, posY + frames[i].redFlagRatio.ValueOrDefault(0.0f) * statusheight));
                        g.DrawLine(blueFlagPen, new PointF(posX,posY + (1.0f-startFrame.blueFlagRatio.ValueOrDefault(0.0f)) * statusheight), new PointF(newposX, posY + (1.0f-frames[i].blueFlagRatio.ValueOrDefault(0.0f)) * statusheight));
                        posX = newposX;
                        startFrame = frames[i];
                        oldDominance = dominanceHere;
                    }
                    if (startFrame != frames[frames.Length - 1])
                    {
                        float avgTotal = 0.0f;
                        int sampleCount = 0;
                        for (int j = Math.Max(frames.Length-1 - 10, 0); j < frames.Length; j++) // todo sliding average here for better perf?
                        {
                            avgTotal += frames[j].dominance;
                            sampleCount++;
                        }
                        float dominanceHere = exaggerate0to1scale(avgTotal / (float)sampleCount);
                        float newposX = posXStart + bgWidth;
                        g.DrawLine(dominancePen, new PointF(posX, posY + oldDominance.ValueOrDefault(0.5f) * statusheight), new PointF(newposX, posY + dominanceHere.ValueOrDefault(0.5f) * statusheight));
                        g.DrawLine(redFlagPen, new PointF(posX, posY + startFrame.redFlagRatio.ValueOrDefault(0.0f) * statusheight), new PointF(newposX, posY + frames[frames.Length-1].redFlagRatio.ValueOrDefault(0.0f) * statusheight));
                        g.DrawLine(blueFlagPen, new PointF(posX, posY + (1.0f - startFrame.blueFlagRatio.ValueOrDefault(0.0f)) * statusheight), new PointF(newposX, posY + (1.0f - frames[frames.Length - 1].blueFlagRatio.ValueOrDefault(0.0f)) * statusheight));
                    }

                    // now draw events (captures/ returns etc)
                    for (int i = 0; i < frames.Length; i++)
                    {
                        if (frames[i].flags.HasFlag(GameEventFlags.Flags.BlueCapture))
                        {// maybe these need i-1 for the pos?
                            float newposX = posXStart + bgWidth * ((float)i / (float)(frames.Length - 1)); // wont divide by 0 cuz if (frames.Length > 1)
                            g.FillRectangle(redFlagBrush, new RectangleF(newposX-5, posY-5, 10, 10));
                        }
                        if (frames[i].flags.HasFlag(GameEventFlags.Flags.RedCapture))
                        {
                            float newposX = posXStart + bgWidth * ((float)i / (float)(frames.Length - 1)); // wont divide by 0 cuz if (frames.Length > 1)
                            g.FillRectangle(blueFlagBrush, new RectangleF(newposX-5, posY-5+statusheight, 10, 10));
                        }
                    }

                    posY += 110; // we'll make a lil chart of how the game went.
                }


            }
            else
            {
                ColumnInfo fakeColumn = new ColumnInfo("Mapname(fake column)", 0, 1800, nameFont, (a) => { return $"{mapName} ^7( {serverName} ^7)"; });
                fakeColumn.DrawString(g, false, posXStart, posY, new ScoreboardEntry());
                posY += 20;
            }

            posX = posXStart;

            foreach (var column in columns)
            {
                column.DrawString(g,true,posX,posY);
                if (!column.noAdvanceAfter)
                {
                    posX += column.width + horzPadding;
                }
            }

            int csvIndex = 0;
            foreach(var column in csvColumns)
            {
                if(csvIndex++ != 0)
                {
                    csvData.Append(",");
                }
                column.WriteHeaderColumn(csvData);
            }
            csvData.Append("\n");

            foreach (ScoreboardEntry entry in entries)
            {
                posY += recordHeight+ vertPadding;
                posX = posXStart;

                preColumn.DrawString(g,false,posX- preColumn.width-horzPadding, posY, entry);

                g.FillRectangle(darkenBgBrush, new RectangleF(posX, posY, 1920.0f - sidePadding - sidePadding, 30.0f)); // darken the bg a bit so it doesnt make the text unreadable

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

                    if (column.noAdvanceAfter)
                    {
                        continue;
                    }
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
                    if (!column.noAdvanceAfter)
                    {
                        posX += column.width + horzPadding;
                    }
                }

                csvIndex = 0;
                foreach (var column in csvColumns)
                {
                    if (csvIndex++ != 0)
                    {
                        csvData.Append(",");
                    }
                    column.WriteDataColumn(csvData, entry);
                }
                csvData.Append("\n");
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

        private static string GetAverageDefragDeviation(ScoreboardEntry entry)
        {
            AverageHelper averageHelper = new AverageHelper();
            foreach (KeyValuePair<string, AverageHelper> kvp in entry.stats.chatCommandTrackingStuff.defragAverageMapTimes)
            {
                double? mapAveragePlayer = kvp.Value.GetAverage();
                if (mapAveragePlayer.HasValue)
                {
                    DefragAverageMapTime mapData = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(kvp.Key);
                    double? mapAverageAll = mapData?.CurrentAverage;
                    if (mapAverageAll.HasValue)
                    {
                        double percentageDiff = 100.0 * (mapAveragePlayer.Value - mapAverageAll.Value) / mapAverageAll.Value;
                        averageHelper.AddValue(percentageDiff);
                    }
                }
            }
            double? avg = averageHelper.GetAverage();
            if (!avg.HasValue)
            {
                return "";
            }

            return $"^7(^3{(int)avg.Value}%^7)";
        }
        private static string GetAverageDefragDeviation(ScoreboardEntry entry, string map)
        {
            AverageHelper averageHelper = new AverageHelper();
            if(entry.stats.chatCommandTrackingStuff.defragAverageMapTimes.TryGetValue(map,out AverageHelper helper))
            //foreach (KeyValuePair<string, AverageHelper> kvp in entry.stats.chatCommandTrackingStuff.defragAverageMapTimes)
            {
                double? mapAveragePlayer = helper.GetAverage();
                if (mapAveragePlayer.HasValue)
                {
                    DefragAverageMapTime mapData = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(map);
                    double? mapAverageAll = mapData?.CurrentAverage;
                    if (mapAverageAll.HasValue)
                    {
                        double percentageDiff = 100.0 * (mapAveragePlayer.Value - mapAverageAll.Value) / mapAverageAll.Value;

                        return $"^7(^3{(int)percentageDiff}%^7)";
                    }
                }
            }
            return "";
        }
        private static string MakeDefragMapsString(ScoreboardEntry entry, HashSet<string> requiredMaps, int lengthLimit = 99999)
        {

            List<KeyValuePair<string, RunsTop10WR>> mapsRun = new List<KeyValuePair<string, RunsTop10WR>>();

            SessionPlayerInfo playerSession = entry.stats.playerSessInfo;
            ChatCommandTrackingStuff trackingStuff = entry.stats.chatCommandTrackingStuff;
            foreach (KeyValuePair<string, int> kvp in trackingStuff.defragMapsRun)
            {
                int runs = kvp.Value;
                int top10 = 0;
                int wr = 0;

                if (trackingStuff.defragMapsTop10.TryGetValue(kvp.Key,out int value))
                {
                    top10 = value;
                }
                if (trackingStuff.defragMapsWR.TryGetValue(kvp.Key,out int value2))
                {
                    wr = value2;
                }

                mapsRun.Add(new KeyValuePair<string, RunsTop10WR>(kvp.Key, new RunsTop10WR(runs,top10,wr)));
            }

            mapsRun.Sort(RunsTop10WR.Comparer);

            StringBuilder mapsString = new StringBuilder();
            int mapIndex = 0;
            foreach (KeyValuePair<string, RunsTop10WR> mapInfo in mapsRun)
            {
                if (!requiredMaps.Contains(mapInfo.Key))
                {
                    continue;
                }
                if (mapInfo.Value.runs <= 0 && mapInfo.Value.top10s <= 0 && mapInfo.Value.wrs <= 0)
                {
                    continue;
                }
                if ((mapsString.Length + mapInfo.Key.Length) > lengthLimit)
                {
                    break;
                }
                if (mapIndex != 0)
                {
                    mapsString.Append(", ");
                }
                mapsString.Append($"^yfff8{mapInfo.Value.runs}");
                if(mapInfo.Value.top10s > 0)
                {
                    mapsString.Append($"^7/{mapInfo.Value.top10s}");
                }
                if(mapInfo.Value.wrs > 0)
                {
                    mapsString.Append($"^7/^2{mapInfo.Value.wrs}");
                }

                mapsString.Append($"^7x{mapInfo.Key}");

                // See if we can calculate average deviation
                if (entry.stats.chatCommandTrackingStuff.defragAverageMapTimes.TryGetValue(mapInfo.Key, out AverageHelper avg))
                {
                    double? mapAveragePlayer = avg.GetAverage();
                    if (mapAveragePlayer.HasValue)
                    {
                        DefragAverageMapTime mapData = AsyncPersistentDataManager<DefragAverageMapTime>.getByPrimaryKey(mapInfo.Key);
                        double? mapAverageAll = mapData?.CurrentAverage;
                        if(mapAverageAll.HasValue)
                        {
                            double percentageDiff = 100.0* (mapAveragePlayer.Value - mapAverageAll.Value)/ mapAverageAll.Value;

                            mapsString.Append($"^7(^3{(int)percentageDiff}%^7)");
                        }
                    }
                }

                
                {
                    if (entry.stats.chatCommandTrackingStuff.defragBestTimes.TryGetValue(mapInfo.Key, out int besttime))
                    {
                        mapsString.Append($"^7({FormatTime(besttime)})");
                    }
                }

                mapIndex++;
            }
            return mapsString.ToString();
        }

        public static string FormatTime(Int64 milliseconds)
        {
            const Int64 hour = 1000 * 60 * 60;
            const Int64 minute = 1000 * 60;
            Int64 hours = 0;
            Int64 minutes = 0;
            Int64 seconds = 0;

            bool must = false;
            StringBuilder sb = new StringBuilder();
            if(milliseconds > hour )
            {
                hours = milliseconds / hour;
                milliseconds -= hours * hour;
                sb.Append(hours.ToString("00"));
                sb.Append(":");
                must = true;
            }
            if(must || milliseconds > minute)
            {
                minutes = milliseconds / minute;
                milliseconds -= minutes * minute;
                sb.Append(minutes.ToString("00"));
                sb.Append(":");
            }
            seconds = milliseconds / 1000;
            milliseconds -= seconds * 1000;
            sb.Append(seconds.ToString("00"));
            sb.Append(".");
            sb.Append(milliseconds.ToString("000"));
            return sb.ToString();

        }

    }
}
