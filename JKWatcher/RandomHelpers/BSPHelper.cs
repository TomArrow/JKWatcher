using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    //using EntityAndCount = Tuple<EntityProperties, int, int>;

    class EntityAndCount
    {
        public EntityProperties props;
        public int count;
        public float coolMedian;
        public int originalIndex;
        public float[] visMatrix;
        public EntityAndCount(EntityProperties propsA, int whateverA)
        {
            props = propsA;
            originalIndex = whateverA;
        }
        public override string ToString()
        {
            return $"visMatrix = {Helpers.printArray(visMatrix)}, coolMedian = {coolMedian}, count = {count}, originalIndex = {originalIndex}, props = {props}";
        }
    }

    static class BSPHelper
    {
        public const int nonIntermissionEntityAlgorithmVersion = 4;

        // \{(\s*"[^"]+"\s*){2,}\s*\}
        // gets stuck in weird inexplicable infinite loop on some maps
        // e.g. bMOHAA24 NS +2000 Mapas (64 bits) v2024.rar\MOHAA24 NS +2000 Mapas (64 bits) v2024\MOHAA\main\mohaa24ns_1_byDJ.pk3\maps\test_normandy.bsp
        static Regex entitiesRegex = new Regex(@"\{(\s*""[^""]+""\s*){2,}\s*\}", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase); 

        // return values: origin, angles, from intermission ent (bool)
        public static (Vector3?,Vector3?,bool) GetIntermissionCamFromBSPData(byte[] data)
        {
            string dataAsString = Encoding.Latin1.GetString(data); // really cringe, i know :)
            string[] potentialEntityBlocks = GetEntityBlocks(dataAsString);
            //MatchCollection matches = entitiesRegex.Matches(dataAsString);
            Dictionary<string, EntityProperties> entitiesByTargetname = new Dictionary<string, EntityProperties>(StringComparer.InvariantCultureIgnoreCase);
            List<EntityProperties> allEnts = new List<EntityProperties>();
            List<EntityProperties> playerSpots = new List<EntityProperties>();
            List<EntityProperties> allPlayerSpots = new List<EntityProperties>(); // maybe if no info_player_deathmatch is found we do other stuff, like ctf spawns. idk
            EntityProperties intermissionEnt = null;
            //foreach (Match m in matches)
            foreach (string entityBlock in potentialEntityBlocks)
            {
                //if (m.Success)
                {
                    //EntityProperties props = EntityProperties.FromString(m.Value);
                    EntityProperties props = EntityProperties.FromString(entityBlock);
                    if (props is null) continue;
                    allEnts.Add(props);
                    if(props != null)
                    {
                        if (props.ContainsKey("classname"))
                        {
                            if(props["classname"].Equals("info_player_deathmatch", StringComparison.InvariantCultureIgnoreCase))
                            {
                                playerSpots.Add(props);
                            }
                            else if(props["classname"].Equals("info_player_intermission", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if(intermissionEnt is null)
                                {
                                    intermissionEnt = props;
                                } else
                                {
                                    Debug.WriteLine($"map contains multiple intermisson ents. using first one.");
                                }
                            }
                            else if(
                                props["classname"].Equals("info_player_start", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("info_player_allied", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("info_player_axis", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("info_player_rebel", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("info_player_imperial", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("team_CTF_redplayer", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("team_CTF_blueplayer", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("team_CTF_redspawn", StringComparison.InvariantCultureIgnoreCase)
                                || props["classname"].Equals("team_CTF_bluespawn", StringComparison.InvariantCultureIgnoreCase)
                                )
                            {
                                allPlayerSpots.Add(props);
                            }
                        }
                        if (props.ContainsKey("targetname"))
                        {
                            entitiesByTargetname[props["targetname"]] = props;
                        }
                    }
                }
            }

            if(intermissionEnt != null)
            {
                Debug.WriteLine("Found intermission cam in bsp data");
                Debug.WriteLine($"intermission ent is {intermissionEnt.ToString()}");
                if (intermissionEnt.ContainsKey("target"))
                {
                    if (entitiesByTargetname.ContainsKey(intermissionEnt["target"]))
                    {
                        EntityProperties target = entitiesByTargetname[intermissionEnt["target"]];
                        Debug.WriteLine($"target is {target.ToString()}");
                        Vector3 dir = target.origin - intermissionEnt.origin;
                        Vector3 angles = new Vector3();
                        Q3MathStuff.vectoangles(dir,ref angles);
                        Debug.WriteLine($"target found, using angle to target {angles} with intermission origin {intermissionEnt.origin}");
                        return (intermissionEnt.origin, angles,true);
                    }
                    else
                    {
                        string key = intermissionEnt["target"];
                        Debug.WriteLine($"target {key} not found, using ent values {intermissionEnt.origin} {intermissionEnt.angles}");
                        return (intermissionEnt.origin, intermissionEnt.angles, true);
                    }
                }
                else
                {
                    Debug.WriteLine($"no target specified, using ent values {intermissionEnt.origin} {intermissionEnt.angles}");
                    return (intermissionEnt.origin, intermissionEnt.angles, true);
                }
            } else
            {
                Vector3 origin = new Vector3();
                Vector3 angles = new Vector3();
                EntityProperties relevantEntity = SelectRandomFurthestSpawnPoint(Vector3.Zero,playerSpots.Count == 0? allPlayerSpots : playerSpots,ref origin, ref angles, allEnts);
                if(relevantEntity != null)
                {
                    Debug.WriteLine($"no intermission ent, using furthest spawnpoint {origin} {angles}");
                    return (origin, angles,false);
                } else
                {
                    Debug.WriteLine($"no intermission ent, no spawn point found. giving up.");
                }
            }

            return (null, null,false);
        }

        static EntityProperties SelectRandomFurthestSpawnPoint(Vector3 avoidPoint,List<EntityProperties> playerSpots, ref Vector3 origin, ref Vector3 angles, List<EntityProperties> allEnts)
        {
            EntityProperties spot;
            Vector3 delta;
            float dist;
            float[] list_dist = new float[64];
            EntityProperties[] list_spot = new EntityProperties[64];
            int numSpots, rnd, i, j;

            numSpots = 0;
            spot = null;

            Queue<EntityProperties> spotsToCheck = new Queue<EntityProperties>(playerSpots);

            while (spotsToCheck.Count > 0)
            {
                spot = spotsToCheck.Dequeue();
                //if (SpotWouldTelefrag(spot))
                //{
                //    continue;
                //}
                delta = spot.origin - avoidPoint;
                //VectorSubtract(spot->s.origin, avoidPoint, delta);
                dist = delta.Length();
                for (i = 0; i < numSpots; i++)
                {
                    if (dist > list_dist[i])
                    {
                        if (numSpots >= 64)
                            numSpots = 64 - 1;
                        for (j = numSpots; j > i; j--)
                        {
                            list_dist[j] = list_dist[j - 1];
                            list_spot[j] = list_spot[j - 1];
                        }
                        list_dist[i] = dist;
                        list_spot[i] = spot;
                        numSpots++;
                        if (numSpots > 64)
                            numSpots = 64;
                        break;
                    }
                }
                if (i >= numSpots && numSpots < 64)
                {
                    list_dist[numSpots] = dist;
                    list_spot[numSpots] = spot;
                    numSpots++;
                }
            }
            if (numSpots == 0)
            {
                spot = playerSpots.Count > 0 ? playerSpots[0] : null;
                if (spot is null)
                {
                    //G_Error("Couldn't find a spawn point");
                    return null;
                }

                origin = spot.origin;
                origin.Y += 9;
                angles = spot.angles;
                return spot;
            }

            // classic q3:
            // select a random spot from the spawn points furthest away
            //rnd = random() * (numSpots / 2);
            //Random random = new Random(); // actually nah, let it be deterministic
            //rnd = (int)((float)random.NextDouble() * (float)(numSpots / 2));

            // v0/1:
            //rnd = 0;
            //
            //origin = list_spot[rnd].origin;
            //origin.Y += 9;
            //angles = list_spot[rnd].angles;

            //return list_spot[rnd];

            // v3
            // highestPossibleIndex is numSpots/2 in classic q3
            // out of all these (which could feasibly happen in a real game), we will pick the one 
            // that has sight of most of the other entities
            int feasibleCount = (numSpots / 2) +1;
            EntityAndCount[] feasibleSpots = new EntityAndCount[feasibleCount];
            for(i=0;i< feasibleCount; i++)
            {
                feasibleSpots[i] = new EntityAndCount(list_spot[i],i);
            }

            // Ok now, for each feasible spot, check how many other entities it sees
            Vector3 forward = new Vector3();
            Vector3 right = new Vector3();
            Vector3 up = new Vector3();
            for (i = 0; i < feasibleCount; i++)
            {
                const int rows = 3;
                const int cols = 3;
                float[] visibleItemsTable = new float[rows * cols];
                int visibleTotal = 0;
                Vector3 optionOri = feasibleSpots[i].props.origin;
                Vector3 optionAngles = feasibleSpots[i].props.angles;
                Matrix4x4 m = ProjectionMatrixHelper.createModelProjectionMatrix(optionOri,optionAngles,LevelShotData.levelShotFov,LevelShotData.levelShotWidth,LevelShotData.levelShotHeight);
                for (j = 0; j < allEnts.Count; j++)
                {
                    if (!allEnts[j].originSet) continue;
                    Vector4 projected= Vector4.Transform(new Vector4(allEnts[j].origin, 1), m); 
                    float theZ = projected.Z;
                    projected /= projected.W;
                    if (theZ > 0 && projected.X >= -1.0f && projected.X <= 1.0f && projected.Y >= -1.0f && projected.Y <= 1.0f)
                    {
                        int row = (int)(((projected.X + 1.0f) / 2.0f) * (float)rows);
                        int col = (int)(((projected.Y + 1.0f) / 2.0f) * (float)cols);
                        if (row >= 0 && row < rows && col >= 0 && col < cols)
                        {
                            visibleItemsTable[row * cols + col] += 1.0f;
                            visibleTotal++;
                        }
                    }
                    
                }

                feasibleSpots[i].count = visibleTotal;
                feasibleSpots[i].visMatrix = visibleItemsTable;
                feasibleSpots[i].coolMedian = Helpers.CoolMedian(visibleItemsTable);
                /*Vector3 optionOri = feasibleSpots[i].Item1.origin;
                Connection.AngleVectors(feasibleSpots[i].Item1.angles,out forward, out right, out up);
                for (j = 0; j < allEnts.Count; j++)
                {
                    if (!allEnts[j].originSet) continue;
                    Vector3 dir = Vector3.Normalize(allEnts[j].origin-optionOri);
                    float dot = Vector3.Dot(dir,forward);
                    if(dot > 0.5f) // 60 degrees
                    {
                        feasibleSpots[i] = new EntityAndCount(feasibleSpots[i].Item1, feasibleSpots[i].Item2 + 1, feasibleSpots[i].Item3);
                    }
                }*/
            }

            // Sort by amount of visible entities. If count same, favor the smaller original index.
            Array.Sort(feasibleSpots, (a, b) => { return a.coolMedian == b.coolMedian ?( a.count==b.count ? a.originalIndex.CompareTo(b.originalIndex) : a.count.CompareTo(b.count)) : b.coolMedian.CompareTo(a.coolMedian); });


            origin = feasibleSpots[0].props.origin;
            origin.Y += 9;
            angles = feasibleSpots[0].props.angles;
            return feasibleSpots[0].props;

        }


        readonly static char[] spaceChars = new char[] { ' ', '\n', '\r', '\t'};
        // Because Regex gets stuck in infinite loop.... SIGH
        static string[] GetEntityBlocks(string input)
        {
            List<string> potentialEntities = new List<string>();
            List<int> braceOpens = new List<int>();
            for(int i = 0; i < input.Length; i++)
            {
                if(input[i] == '{')
                {
                    braceOpens.Add(i);
                }
            }
            foreach(int braceOpen in braceOpens)
            {
                bool inQuote = false;
                int quotedStrings = 0;
                for(int i = braceOpen+1; i < input.Length; i++)
                {
                    if (inQuote && input[i] == 0)
                    {
                        break;
                    }
                    else if (input[i] == '"')
                    {
                        inQuote = !inQuote;
                        if (inQuote)
                        {
                            quotedStrings++;
                        }
                    } else if (!inQuote && input[i] == '}' && quotedStrings >= 2)
                    {
                        potentialEntities.Add(input.Substring(braceOpen,i-braceOpen+1));
                        break;
                    } else if (!inQuote && !spaceChars.Contains(input[i]))
                    {
                        break;
                    }
                }
            }
            return potentialEntities.ToArray();
        }
    }

    public class EntityProperties : Dictionary<string, string>, INotifyPropertyChanged
    {
        public Vector3 origin = new Vector3(0,0,0);
        public Vector3 angles = new Vector3(0,0,0);
        public bool originSet = false;

        public EntityProperties() : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in this)
            {
                sb.Append($"\"{(kvp.Key + '\"').PadRight(10)} \"{kvp.Value}\"\n");
            }
            return sb.ToString();
        }

        static Regex singleEntityParseRegex = new Regex(@"(\s*""([^""]+)""[ \t]+""([^""]+)"")+\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static EntityProperties FromString(string propertiesString)
        {
            MatchCollection matches = singleEntityParseRegex.Matches(propertiesString);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    EntityProperties props = new EntityProperties();

                    int lineCount = match.Groups[2].Captures.Count;
                    for (int c = 0; c < lineCount; c++)
                    {
                        //Trace.WriteLine($"{match.Groups[2].Captures[c].Value}:{match.Groups[3].Captures[c].Value}");
                        props[match.Groups[2].Captures[c].Value] = match.Groups[3].Captures[c].Value;
                        if(match.Groups[2].Captures[c].Value.Equals("angles",StringComparison.InvariantCultureIgnoreCase))
                        {
                            Vector3? entangles = parseVector3(match.Groups[3].Captures[c].Value);
                            if(entangles != null)
                            {
                                props.angles = entangles.Value;
                            }
                        } else if(match.Groups[2].Captures[c].Value.Equals("origin",StringComparison.InvariantCultureIgnoreCase))
                        {
                            Vector3? entorigin = parseVector3(match.Groups[3].Captures[c].Value);
                            if(entorigin != null)
                            {
                                props.origin = entorigin.Value;
                                props.originSet = true;
                            }
                        } else if(match.Groups[2].Captures[c].Value.Equals("angle",StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (float.TryParse(match.Groups[3].Captures[c].Value, out float entangle))
                            {
                                props.angles.X = 0; 
                                props.angles.Y = entangle; 
                                props.angles.Z = 0; 
                            }
                        }
                    }
                    return props;
                }
            }
            return null;
        }

        public string String => this.ToString();


        public override bool Equals(object obj)
        {
            EntityProperties other = obj as EntityProperties;
            if (!(other is null))
            {
                if (this.Count == other.Count)
                {
                    foreach (var kvp in this)
                    {
                        if (!other.ContainsKey(kvp.Key) || other[kvp.Key] != kvp.Value)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            bool firstDone = false;

            string[] keys = this.Keys.ToArray();
            Array.Sort(keys, StringComparer.InvariantCultureIgnoreCase);

            foreach (var key in keys)
            {

                int hereHash = HashCode.Combine(key.GetHashCode(StringComparison.InvariantCultureIgnoreCase), this[key].GetHashCode(StringComparison.InvariantCultureIgnoreCase));
                if (!firstDone)
                {
                    hash = hereHash;
                }
                else
                {
                    hash = HashCode.Combine(hereHash, hash);
                    firstDone = true;
                }
            }
            return hash;
        }

        static Regex emptySpaceRegex = new Regex(@"\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static Vector3? parseVector3(string colorString)
        {
            string prefilteredColor = emptySpaceRegex.Replace(colorString, " ");
            string[] components = prefilteredColor.Split(' ');

            //if (components.Length < 3)
            //{
            //    Debug.WriteLine("Vector3 with less than 3 components, skipping, weird.");
            //    return null;
            //}

            Vector3 parsedColor = new Vector3();

            bool parseSuccess = true;
            if (components.Length > 0) parseSuccess = parseSuccess && float.TryParse(components[0], out parsedColor.X);
            if (components.Length > 1) parseSuccess = parseSuccess && float.TryParse(components[1], out parsedColor.Y);
            if (components.Length > 2) parseSuccess = parseSuccess && float.TryParse(components[2], out parsedColor.Z);

            if (!parseSuccess) return null;

            return parsedColor;
        }
    }
}
