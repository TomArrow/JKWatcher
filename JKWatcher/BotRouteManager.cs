using RT.Dijkstra;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JKWatcher
{
    public class WayPoint
    {
        public ushort number;
        public int flags;
        public float weight;
        public Vector3 origin;
        public ushort[] neighborsNums = null;
        public float distToNext;
    }

    public class WayPointPath
    {
        public ushort[] pathElements;
        public float totalDistance;
    }

    public class PathFinder
    {
        public string mapname = "";
        public WayPoint[] wayPoints = null;
        public WayPointPath[,] paths = null;
        public DateTime lastAccess = DateTime.Now;
        private bool loaded = false;
        private bool loadable = true;
        private bool loading = false;
        private Mutex stateMutex = new Mutex();
        public void CheckLoaded()
        {
            lock (stateMutex)
            {
                if (loading) return;
                // Unload if not used for a long time.
                else if (loaded && (DateTime.Now - lastAccess).TotalMinutes > 30)
                {
                    Unload();
                }
            }
        }
        private void Unload()
        {
            lock (stateMutex)
            {
                if (loading) return;
                wayPoints = null;
                paths = null;
                loaded = false;
            }
        }

        Random rnd = new Random();

        public WayPoint getRandomWayPoint()
        {
            lock (stateMutex)
            {
                lastAccess = DateTime.Now;
                if (loading || !loadable)
                {
                    return null;
                }
                else if (!loaded && !loading && loadable)
                {
                    loading = true;
                    Task.Run(Load); // We don't wanna stop here while we load the data. Perfectly fine to return null until we have the data.
                    return null;
                } else if(loaded && wayPoints != null)
                {
                    lock (rnd)
                    {
                        return wayPoints[rnd.Next(0,wayPoints.Length)];
                    }
                }
                else
                {
                    return null;// idk
                }
            }
        }

        public WayPoint findClosestWayPoint(Vector3 position, IEnumerable<WayPoint> wayPointsToSkip, bool allowSearchUp, float maxDistance = 600)
        {
            lock (stateMutex)
            {
                lastAccess = DateTime.Now;
                if (loading || !loadable)
                {
                    return null;
                }
                else if (!loaded && !loading && loadable)
                {
                    loading = true;
                    Task.Run(Load); // We don't wanna stop here while we load the data. Perfectly fine to return null until we have the data.
                    return null;
                } else if(loaded && wayPoints != null)
                {
                    float verticalSearchScopeDown = 30.0f;
                    float verticalSearchScopeUp = 15.0f;
                    WayPoint closestWayPoint = null;
                    float closestDistance = float.PositiveInfinity;
                    while (closestWayPoint == null && verticalSearchScopeDown < 70.0f)
                    {
                        foreach(WayPoint wayPoint in wayPoints)
                        {
                            if (wayPointsToSkip != null && wayPointsToSkip.Contains(wayPoint)) continue;
                            if(wayPoint.origin.Z >= position.Z- verticalSearchScopeDown && wayPoint.origin.Z <= position.Z + verticalSearchScopeUp)
                            {
                                float hereDistance = (wayPoint.origin - position).Length();
                                if (hereDistance > maxDistance) continue;
                                if (hereDistance < closestDistance)
                                {
                                    closestWayPoint = wayPoint;
                                    closestDistance = hereDistance;
                                }
                            }
                        }
                        verticalSearchScopeDown += 30.0f;
                        verticalSearchScopeUp += allowSearchUp ? 30.0f : 0.0f;
                    }
                    return closestWayPoint;
                }
                else
                {
                    return null;// idk
                }
            }
        }
        
        public WayPoint[] getPath(WayPoint wp1, WayPoint wp2, ref float totalDistance,bool includeFirstLast= true)
        {
            lock (stateMutex)
            {
                lastAccess = DateTime.Now;
                if (loading || !loadable)
                {
                    return null;
                }
                else if (!loaded && !loading && loadable)
                {
                    loading = true;
                    Task.Run(Load); // We don't wanna stop here while we load the data. Perfectly fine to return null until we have the data.
                    return null;
                } else if(loaded && paths != null && wayPoints != null)
                {
                    int pathsSize = paths.GetLength(0);
                    if (pathsSize > wp1.number && pathsSize > wp2.number && pathsSize == paths.GetLength(1) && pathsSize == wayPoints.Length)
                    {
                        List<WayPoint> wayPointsOfPath = new List<WayPoint>();
                        if (includeFirstLast)
                        {
                            wayPointsOfPath.Add(wp1);
                        }
                        foreach (ushort pathElementNum in paths[wp1.number,wp2.number].pathElements)
                        {
                            wayPointsOfPath.Add(wayPoints[pathElementNum]);
                        }
                        if (includeFirstLast)
                        {
                            wayPointsOfPath.Add(wp2);
                        }
                        totalDistance = paths[wp1.number, wp2.number].totalDistance;
                        return wayPointsOfPath.ToArray();
                    } else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;// idk
                }
            }
        }
        
        public WayPoint[] getLongestPathFrom(WayPoint wp1, ref float totalDistance,bool includeFirstLast= true)
        {
            lock (stateMutex)
            {
                lastAccess = DateTime.Now;
                if (loading || !loadable)
                {
                    return null;
                }
                else if (!loaded && !loading && loadable)
                {
                    loading = true;
                    Task.Run(Load); // We don't wanna stop here while we load the data. Perfectly fine to return null until we have the data.
                    return null;
                } else if(loaded && paths != null && wayPoints != null)
                {
                    int pathsSize = paths.GetLength(0);

                    if (pathsSize > wp1.number && pathsSize == paths.GetLength(1) && pathsSize == wayPoints.Length)
                    {
                        float longestPathDist = 0;
                        WayPointPath longestPath = null;
                        int longestPathEndPoint = -1;
                        for(int to=0;to<pathsSize;to++)
                        {
                            if (paths[wp1.number,to].totalDistance > longestPathDist)
                            {
                                longestPathDist = paths[wp1.number, to].totalDistance;
                                longestPath = paths[wp1.number, to];
                                longestPathEndPoint = to;
                            }
                        }
                        if (longestPath != null)
                        {
                            List<WayPoint> wayPointsOfPath = new List<WayPoint>();
                            if (includeFirstLast)
                            {
                                wayPointsOfPath.Add(wp1);
                            }
                            foreach (ushort pathElementNum in longestPath.pathElements)
                            {
                                wayPointsOfPath.Add(wayPoints[pathElementNum]);
                            }
                            if (includeFirstLast)
                            {
                                wayPointsOfPath.Add(wayPoints[longestPathEndPoint]);
                            }
                            totalDistance = longestPath.totalDistance;
                            return wayPointsOfPath.ToArray();
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;// idk
                }
            }
        }

        public void ForceLoad()
        {
            lock (stateMutex)
            {
                lastAccess = DateTime.Now;
                if (loading || !loadable)
                {
                }
                else if (!loaded && !loading && loadable)
                {
                    loading = true;
                    Task.Run(Load); // We don't wanna stop here while we load the data. Perfectly fine to return null until we have the data.
                }
            }
        }
        private void Load()
        {
            WayPoint[] wayPointsTmp = BotRouteManager.GetWayPoints(this.mapname);
            WayPointPath[,] pathsTmp = BotRouteManager.GetPaths(this.mapname);
            lock (stateMutex)
            {
                if (pathsTmp == null || pathsTmp == null)
                {
                    loaded = false;
                    loadable = false;
                    loading = false;
                    Unload();
                }
                else
                {
                    this.wayPoints = wayPointsTmp;
                    this.paths = pathsTmp;
                    loaded = true;
                    loadable = true;
                    loading = false;
                }
            }
        }
    }

    

    static class BotRouteManager
    {
        static Regex botrouteDataRegex = new Regex(@"(?:\n|\r|^)\s*(?<num>\d+)\s*(?<flags>\d+)\s*(?<weight>[\d\.-]+)\s*\(\s*(?<originX>[\d\.-]+)\s*(?<originY>[\d\.-]+)\s*(?<originZ>[\d\.-]+)\s*\)\s*{(?<neighbors>(?:\s*[\d-]+)*)\s*}\s*(?<distToNext>[\d\.-]+)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
        static string botroutesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "botroutes");
        static string convertedBotroutesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "botroutes","autoConverted");
        static readonly string fileAccessMutexName = "JKWatcherBotRouteManagerFileAccessMutex";

        static Dictionary<string, PathFinder> pathFinders = new Dictionary<string, PathFinder>();

        public static PathFinder GetPathFinder(string mapname)
        {
            string lowerMapName = mapname.ToLower();
            lock (pathFinders)
            {
                foreach(var kvp in pathFinders)
                {
                    kvp.Value.CheckLoaded(); // Unload old unneeded path/waypoint data. This does not remove the item from the pathFinders array. Those things are permanent so I don't have to manage referneces. They only get set to an unloaded state.
                }
                if (!pathFinders.ContainsKey(lowerMapName))
                {
                    var newPF = new PathFinder();
                    newPF.mapname = lowerMapName;
                    pathFinders[lowerMapName] = newPF;
                    //newPF.ForceLoad(); // Dont force load because we may never need the data actually.
                    return newPF;
                }
                else
                {
                    return pathFinders[lowerMapName];
                }
            }
        }
        public static void Initialize()
        {
            try { 

                using (new GlobalMutexHelper(fileAccessMutexName))
                {
                    Directory.CreateDirectory(botroutesFolder);
                    Directory.CreateDirectory(convertedBotroutesFolder);
                    string[] botrouteFiles = Directory.GetFiles(botroutesFolder, "*.wnt");
                    ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount/2};
                    Parallel.ForEach(botrouteFiles, po, (string botrouteFile) =>
                    {
                        string basename = Path.GetFileNameWithoutExtension(botrouteFile);
                        string convertedPath = Path.Combine(convertedBotroutesFolder, basename + ".sddi"); // sddi = shortest dijkstra distance info
                        if (!File.Exists(convertedPath))
                        {
                            GenerateSDDI(botrouteFile, convertedPath);
                        }
                    });
                    /*foreach(string botrouteFile in botrouteFiles)
                    {
                        string basename = Path.GetFileNameWithoutExtension(botrouteFile);
                        string convertedPath = Path.Combine(convertedBotroutesFolder, basename + ".sddi"); // sddi = shortest dijkstra distance info
                        if (!File.Exists(convertedPath))
                        {
                            GenerateSDDI(botrouteFile, convertedPath);
                        }
                    }*/
                    Console.WriteLine($"{botrouteFiles.Length} botroutes initialized.");
                }

            } catch (Exception ex)
            {
                string errorMessage = $"Error initializing Botroutes: {ex.ToString()}";
                Helpers.logToFile(errorMessage);
                MessageBox.Show(errorMessage);
            }
        }

        // sddi = shortest dijkstra distance info
        private static void GenerateSDDI(string wntFile, string sddiFile)
        {
            try
            {

                WayPoint[] data = LoadWNT(wntFile);
                WayPointPath[,] paths = new WayPointPath[data.Length, data.Length];
                // Now calculate best route for each combination of waypoints
                for (ushort from = 0; from < data.Length; from++)
                {
                    for (ushort to = 0; to < data.Length; to++)
                    {
                        float totalDistance = 0;
                        var route = DijkstrasAlgorithm.Run(
                            // The start node to begin our search.
                            new WayPointNode(from, to, data),
                            // The initial value for the distance traveled.
                            0,
                            // How to add two distances.
                            (a, b) => a + b,
                            // The variable to receive the total distance traveled.
                            out totalDistance);

                        List<ushort> steps = new List<ushort>();
                        foreach(var step in route)
                        {
                            if(step.Label != from && step.Label != to)
                            {
                                steps.Add(step.Label);
                            }
                        }
                        paths[from, to] = new WayPointPath() { pathElements = steps.ToArray(), totalDistance = totalDistance };
                    }
                }

                Console.WriteLine($"SDDI data calculated for {Path.GetFileName(wntFile)}. Saving now.");
                using (MemoryStream ms = new MemoryStream())
                {
                    using (ZipArchive sddiZip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        ZipArchiveEntry fakeFile = sddiZip.CreateEntry("sddidata");
                        using (Stream fakeFileStream = fakeFile.Open())
                        {
                            using (BinaryWriter bw = new BinaryWriter(fakeFileStream))
                            {
                                bw.Write((ushort)data.Length);
                                for (int from = 0; from < data.Length; from++)
                                {
                                    for (int to = 0; to < data.Length; to++)
                                    {
                                        bw.Write((ushort)paths[from, to].pathElements.Length);
                                        if(paths[from, to].pathElements.Length > 0)
                                        {
                                            foreach(ushort pathElement in paths[from, to].pathElements)
                                            {
                                                bw.Write((ushort)pathElement);
                                            }
                                            bw.Write((float)paths[from, to].totalDistance);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    using (FileStream fs = new FileStream(sddiFile, FileMode.Create))
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        ms.CopyTo(fs);
                    }
                }
                Console.WriteLine($"SDDI data saved as {Path.GetFileName(sddiFile)}.");

            } catch(Exception ex)
            {
                string errorMessage = $"Botroutes: Error generating SDDI: {ex.ToString()}";
                Helpers.logToFile(errorMessage);
                MessageBox.Show(errorMessage);
            }

        }

        public static WayPoint[] GetWayPoints(string mapname)
        {
            try
            {
                using (new GlobalMutexHelper(fileAccessMutexName,20000))
                {
                    string wntPath = Path.Combine(botroutesFolder, mapname + ".wnt");
                    if (File.Exists(wntPath))
                    {
                        return LoadWNT(wntPath);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error getting waypoints: {ex.ToString()}";
                Helpers.logToFile(errorMessage);
                return null;
            }
        }
        public static WayPointPath[,] GetPaths(string mapname)
        {
            try
            {
                using (new GlobalMutexHelper(fileAccessMutexName, 20000))
                {
                    string sddiPath = Path.Combine(convertedBotroutesFolder, mapname + ".sddi");
                    if (File.Exists(sddiPath))
                    {
                        return LoadSDDI(sddiPath);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error getting paths: {ex.ToString()}";
                Helpers.logToFile(errorMessage);
                return null;
            }
        }

        private static WayPointPath[,] LoadSDDI(string filename)
        {
            WayPointPath[,] paths = null;
            using(FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                using(ZipArchive za = new ZipArchive(fs, ZipArchiveMode.Read, true))
                {
                    ZipArchiveEntry dataEntry = za.GetEntry("sddidata");
                    using (Stream dataStream = dataEntry.Open())
                    {
                        using(BinaryReader br = new BinaryReader(dataStream))
                        {
                            ushort countWayPoints = br.ReadUInt16();
                            paths = new WayPointPath[countWayPoints, countWayPoints];
                            for(ushort from = 0; from< countWayPoints; from++)
                            {
                                for (ushort to = 0; to< countWayPoints; to++)
                                {
                                    paths[from,to] = new WayPointPath();
                                    ushort countPathElements = br.ReadUInt16();
                                    paths[from, to].pathElements = new ushort[countPathElements];
                                    if(countPathElements > 0)
                                    {
                                        for(int i =0; i < countPathElements; i++)
                                        {
                                            paths[from, to].pathElements[i] = br.ReadUInt16();
                                        }
                                        paths[from, to].totalDistance = br.ReadSingle();
                                    }
                                    else
                                    {
                                        paths[from, to].totalDistance = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return paths;
        }
        
        private static WayPoint[] LoadWNT(string filename)
        {
            string wntData = File.ReadAllText(filename);
            MatchCollection matches = botrouteDataRegex.Matches(wntData);
            List<WayPoint> waypoints = new List<WayPoint>();
            int index = 0;
            foreach(Match match in matches)
            {
                WayPoint newWaypoint = new WayPoint();
                newWaypoint.number = ushort.Parse(match.Groups["num"].Value);
                if(newWaypoint.number != index)
                {
                    throw new Exception($"WNT parsing error: number {newWaypoint.number} != index {index}");
                }
                newWaypoint.flags = int.Parse(match.Groups["flags"].Value);
                newWaypoint.weight = float.Parse(match.Groups["weight"].Value);
                newWaypoint.origin = new Vector3();
                newWaypoint.origin.X = float.Parse(match.Groups["originX"].Value);
                newWaypoint.origin.Y = float.Parse(match.Groups["originY"].Value);
                newWaypoint.origin.Z = float.Parse(match.Groups["originZ"].Value);
                string neighborsText = match.Groups["neighbors"].Value;
                string[] neighborsTextSplit = neighborsText.Split(new char[] {' ','\t'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                newWaypoint.neighborsNums = new ushort[neighborsTextSplit.Length];
                for(int i=0;i<neighborsTextSplit.Length;i++)
                {
                    string[] elements = neighborsTextSplit[i].Split("-"); // Detected force jumps have this stuff
                    newWaypoint.neighborsNums[i] = ushort.Parse(elements[0]);
                }
                newWaypoint.distToNext = float.Parse(match.Groups["distToNext"].Value);
                waypoints.Add(newWaypoint);
                index++;
            }
            return waypoints.ToArray();
        }
    }

    // For Dijkstra algorithm library
    public sealed class WayPointNode : Node<float, ushort>
    {
        public ushort Number { get; private set; }
        public ushort Destination { get; private set; }
        private WayPoint[] wayPoints = null;
        public WayPointNode(ushort number, ushort destination, WayPoint[] wayPointsArray)
        {
            Number = number;
            Destination = destination;
            wayPoints = wayPointsArray;
        }
        public override bool IsFinal => Number == Destination;

        public override IEnumerable<Edge<float, ushort>> Edges
        {
            get
            {
                List<Edge<float, ushort>> edges = new List<Edge<float, ushort>>();
                foreach (ushort neighbor in wayPoints[Number].neighborsNums)
                {
                    edges.Add(new Edge<float, ushort>((wayPoints[Number].origin - wayPoints[neighbor].origin).Length(), neighbor, new WayPointNode(neighbor, Destination, wayPoints)));
                }
                if (Number > 0)
                {
                    edges.Add(new Edge<float, ushort>((wayPoints[Number].origin - wayPoints[Number - 1].origin).Length(), (ushort)(Number - 1), new WayPointNode((ushort)(Number - 1), Destination, wayPoints)));
                }
                if (Number < wayPoints.Length - 1)
                {
                    edges.Add(new Edge<float, ushort>((wayPoints[Number].origin - wayPoints[Number + 1].origin).Length(), (ushort)(Number + 1), new WayPointNode((ushort)(Number + 1), Destination, wayPoints)));
                }
                return edges.ToArray();
            }
        }

        public override bool Equals(Node<float, ushort> other)
        {
            return ((WayPointNode)other).Number == Number;
        }

        public override int GetHashCode()
        {
            return Number;
        }
    }
}
