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
    static class BSPHelper
    {
        static Regex entitiesRegex = new Regex(@"\{(\s*""[^""]+""\s*){2,}\}", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public static (Vector3?,Vector3?) GetIntermissionCamFromBSPData(byte[] data)
        {
            string dataAsString = Encoding.Latin1.GetString(data); // really cringe, i know :)
            MatchCollection matches = entitiesRegex.Matches(dataAsString);
            Dictionary<string, EntityProperties> entitiesByTargetname = new Dictionary<string, EntityProperties>(StringComparer.InvariantCultureIgnoreCase);
            List<EntityProperties> playerSpots = new List<EntityProperties>();
            List<EntityProperties> allPlayerSpots = new List<EntityProperties>(); // maybe if no info_player_deathmatch is found we do other stuff, like ctf spawns. idk
            EntityProperties intermissionEnt = null;
            foreach (Match m in matches)
            {
                if (m.Success)
                {
                    EntityProperties props = EntityProperties.FromString(m.Value);
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
                                intermissionEnt = props;
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
                        return (intermissionEnt.origin, angles);
                    }
                    else
                    {
                        string key = intermissionEnt["target"];
                        Debug.WriteLine($"target {key} not found, using ent values {intermissionEnt.origin} {intermissionEnt.angles}");
                        return (intermissionEnt.origin, intermissionEnt.angles);
                    }
                }
                else
                {
                    Debug.WriteLine($"no target specified, using ent values {intermissionEnt.origin} {intermissionEnt.angles}");
                    return (intermissionEnt.origin, intermissionEnt.angles);
                }
            } else
            {
                Vector3 origin = new Vector3();
                Vector3 angles = new Vector3();
                EntityProperties relevantEntity = SelectRandomFurthestSpawnPoint(Vector3.Zero,playerSpots.Count == 0? allPlayerSpots : playerSpots,ref origin, ref angles);
                if(relevantEntity != null)
                {
                    Debug.WriteLine($"no intermission ent, using furthest spawnpoint {origin} {angles}");
                    return (origin, angles);
                } else
                {
                    Debug.WriteLine($"no intermission ent, no spawn point found. giving up.");
                }
            }

            return (null, null);
        }

        static EntityProperties SelectRandomFurthestSpawnPoint(Vector3 avoidPoint,List<EntityProperties> playerSpots, ref Vector3 origin, ref Vector3 angles)
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

            // select a random spot from the spawn points furthest away
            //rnd = random() * (numSpots / 2);
            //Random random = new Random(); // actually nah, let it be deterministic
            //rnd = (int)((float)random.NextDouble() * (float)(numSpots / 2));
            rnd = 0;

            origin = list_spot[rnd].origin;
            origin.Y += 9;
            angles = list_spot[rnd].angles;

            return list_spot[rnd];
        }

    }

    public class EntityProperties : Dictionary<string, string>, INotifyPropertyChanged
    {
        public Vector3 origin = new Vector3(0,0,0);
        public Vector3 angles = new Vector3(0,0,0);

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
