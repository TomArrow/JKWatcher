using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JKWatcher
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    struct WindowPositionData
    {
        public int isActive; // Removed windows just end up as free space.
        public double left;
        public double top;
        public double width;
        public double height;
        public Int64 dateTimeTicks;
    }
    struct WindowPositionsState
    {
        public int existingWindowCount;
        public int actualCount;
        public WindowPositionData[] windowPositions;
    }

    class WindowPositionManager
    {

        const int maxWindows = 100;
        static readonly int windowPositionStructSize = Marshal.SizeOf(typeof(WindowPositionData));
        static readonly int spaceRequired = sizeof(int) + windowPositionStructSize * maxWindows;

        private class AccessorGetter : IDisposable {

            GlobalMutexHelper gmh = null;
            public AccessorGetter()
            {
                gmh = new GlobalMutexHelper("JKWatcherWindowPositionManagerSharedMemoryV2Mutex");
                acc = mmf.CreateViewAccessor(0, spaceRequired, MemoryMappedFileAccess.ReadWrite);
            }
            public void Dispose()
            {
                gmh.Dispose();
                acc.Dispose();
                acc = null;
            }
        }


        static MemoryMappedFile mmf = null;
        static MemoryMappedViewAccessor acc = null;

        static Dictionary<Window,int> registeredWindows = new Dictionary<Window, int>(); // Window -> index in shared memory data

        static WindowPositionManager()
        {
            mmf = MemoryMappedFile.CreateOrOpen("JKWatcherWindowPositionManagerSharedMemoryV2.mmf", spaceRequired, MemoryMappedFileAccess.ReadWrite,MemoryMappedFileOptions.None,System.IO.HandleInheritability.Inheritable);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            SpawnBackgroundThread();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // Kinda destructor..
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            backgroundThreadCancelTokenSource.Cancel();
            if(backgroundThreadTask != null && backgroundThreadTask.Status == TaskStatus.Running)
            {
                backgroundThreadTask.Wait();
            }
        }

        static CancellationTokenSource backgroundThreadCancelTokenSource = new CancellationTokenSource();
        static CancellationToken backgroundThreadCancelToken = backgroundThreadCancelTokenSource.Token;
        static Task backgroundThreadTask = null;
        static void SpawnBackgroundThread()
        {
            backgroundThreadTask = Task.Factory.StartNew(() => { backgroundThread(); }, backgroundThreadCancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
                Task.Run(()=> {
                    System.Threading.Thread.Sleep(30000);
                    SpawnBackgroundThread();
                });
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        static void backgroundThread()
        {
            while (true)
            {
                /*for(int i = 0; i < 60; i++)
                {
                    System.Threading.Thread.Sleep(500);
                    if (backgroundThreadCancelToken.IsCancellationRequested)
                    {
                        return;
                    }
                }*/
                System.Threading.Thread.Sleep(30000);
                if (backgroundThreadCancelToken.IsCancellationRequested)
                {
                    return;
                }

                // What we do here: Update timestamps of existing entries and remove entries that haven't been updated for very long.
                // For example, if an instance of JKWatcher crashed, it would be unable to update the entries corresponding with its windows.
                // If we don't remove those entries, we will have basically "ghost windows" messing with future window positions forever.
                // So we check any entries that haven't been updated for very long and yeet them.
                HandleGhostWindows();
            }
        }


        static WindowPositionsState getCurrentState()
        {

            int[] data = new int[1];
            acc.ReadArray<int>(0, data, 0, 1);
            int windowCount = data[0];
            Debug.WriteLine($"WindowPositionManager: {windowCount} windows so far.");
            WindowPositionData[] windowData = new WindowPositionData[windowCount];
            acc.ReadArray<WindowPositionData>(sizeof(int),windowData,0,windowCount);

            int actualCount = 0;
            for (int i = 0; i < windowData.Length; i++)
            {
                if (windowData[i].isActive == 1)
                {
                    actualCount++;
                }
            }

            return new WindowPositionsState() { existingWindowCount = windowCount,windowPositions= windowData,actualCount= actualCount };

        }
        
        static void changeWindowData(WindowPositionData newPosData, int index)
        {
            newPosData.isActive = 1;
            newPosData.dateTimeTicks = DateTime.Now.Ticks;
            acc.Write<WindowPositionData>(sizeof(int) + windowPositionStructSize * index, ref newPosData);

            Debug.WriteLine($"WindowPositionManager: Window {index} updated: {newPosData.left},{newPosData.top},{newPosData.width},{newPosData.height}.");

        }
        static void removeWindowData(int index)
        {
            WindowPositionData newData = new WindowPositionData { isActive = 0 };
            acc.Write<WindowPositionData>(sizeof(int) + windowPositionStructSize * index, ref newData);

            Debug.WriteLine($"WindowPositionManager: Window {index} removed.");

        }
        static int addWindowData(WindowPositionData newPosData)
        {
            newPosData.isActive = 1;
            newPosData.dateTimeTicks = DateTime.Now.Ticks;
            WindowPositionsState curData = getCurrentState();

            int newIndex = curData.existingWindowCount;
            for(int i=0; i < curData.existingWindowCount; i++)
            {
                if (curData.windowPositions[i].isActive == 0)
                {
                    newIndex = i;
                    break;
                }
            }

            if(newIndex == curData.existingWindowCount)
            {
                // Increase counter by 1.
                int windowCount = curData.existingWindowCount +1;
                acc.Write<int>(0, ref windowCount);
            }

            acc.Write<WindowPositionData>(sizeof(int) + windowPositionStructSize * newIndex, ref newPosData);

            Debug.WriteLine($"WindowPositionManager: Window {newIndex} added: {newPosData.left},{newPosData.top},{newPosData.width},{newPosData.height}.");

            return newIndex;

        }

        public static bool RegisterWindow(Window wnd)
        {
            try { 
                using(new AccessorGetter())
                {
                    WindowPositionsState curState = getCurrentState();
                    if (curState.actualCount >= maxWindows)
                    {
                        // No reserved space left for more window data.
                        return false;
                    }

                    Rect workArea = SystemParameters.WorkArea;
                    double[] initialPosition = new double[4] { workArea.Left + 100.0, workArea.Top + 100, wnd.ActualWidth, wnd.ActualHeight };
                    double[] position = (double[])initialPosition.Clone();
                    bool overlaps = true;
                    bool positionIsLegal = true;
                    bool rightReached = false;
                    int safetyIndex = 0;
                    while (overlaps && positionIsLegal && safetyIndex++ < 1_000_000)
                    {
                        overlaps = false;
                        WindowPositionData overlappingWindow = new WindowPositionData();
                        for (int i = 0; i < curState.existingWindowCount; i++)
                        {
                            if (curState.windowPositions[i].isActive == 0) continue; // skip empty memory placecholders from closed windows.
                            if (position[0] < curState.windowPositions[i].left+curState.windowPositions[i].width
                                && position[1] < curState.windowPositions[i].top + curState.windowPositions[i].height
                                && position[0]+ position[2] > curState.windowPositions[i].left
                                && position[1]+ position[3] > curState.windowPositions[i].top
                                )
                            {
                                overlappingWindow = curState.windowPositions[i];
                                overlaps = true;
                                break;
                            }
                        }

                        if (overlaps)
                        {
                            // We always try to evade to the right first, then to the bottom.
                            // We also try to make sure windows aren't partially hidden by the desktop bounds, at least not to the right.
                            if (rightReached)
                            {
                                position[0] = overlappingWindow.left;
                                position[1] = overlappingWindow.top + overlappingWindow.height + 1;
                                rightReached = false;
                            }
                            else
                            {
                                position[0] = overlappingWindow.left + overlappingWindow.width + 1;
                                position[1] = overlappingWindow.top;
                                if (position[0] >= workArea.Left + workArea.Width || position[0] + position[2] >= workArea.Left + workArea.Width)
                                {
                                    rightReached = true;
                                    position[0] = initialPosition[0];

                                }
                            }
                            
                            if (position[1] >= workArea.Top + workArea.Height || position[0] >= workArea.Left + workArea.Width)
                            {
                                positionIsLegal = false;
                            }
                        }

                    }

                    if(safetyIndex >= 1_000_000)
                    {
                        Helpers.logToFile(new string[] { "Window position manager possible infinite loop detected." });
                    }

                    if (positionIsLegal)
                    {
                        wnd.Left = position[0];
                        wnd.Top = position[1];
                    }
                    else
                    {
                        position[0] = wnd.Left;
                        position[1] = wnd.Top;
                    }

                    WindowPositionData newPosData = new WindowPositionData() { left = position[0], top = position[1], width = initialPosition[2], height = initialPosition[3] };


                    int newIndex = addWindowData(newPosData);

                    lock(registeredWindows) registeredWindows.Add(wnd, newIndex);
                    wnd.Closed += Wnd_Closed;
                    wnd.LocationChanged += Wnd_LocationChanged;



                    return true;
                }
            } catch(Exception e)
            {
                // Can't help it...
                Helpers.logToFile(e.ToString());
                return false;
            }

        }


        static void HandleGhostWindows()
        {
            try
            {
                using (new AccessorGetter())
                {

                    WindowPositionsState curState = getCurrentState();

                    List<int> ourWindowIndizi = new List<int>();
                    lock (registeredWindows)
                    {
                        foreach (var win in registeredWindows)
                        {
                            ourWindowIndizi.Add(win.Value);
                        }
                    }

                    foreach(int windowIndex in ourWindowIndizi)
                    {
                        WindowPositionData windowData = curState.windowPositions[windowIndex];
                        changeWindowData(windowData, windowIndex); // This function call automatically updates the current dateTimeTicks in the windowData. 
                    }

                    for(int i = 0; i < curState.windowPositions.Length; i++)
                    {
                        double age = (DateTime.Now - new DateTime(curState.windowPositions[i].dateTimeTicks)).TotalMinutes;
                        if (!ourWindowIndizi.Contains(i) && curState.windowPositions[i].isActive > 0 && age > 10)
                        {
                            Helpers.logToFile($"WindowPositionManager: Removing ghost window {i} that hasn't gotten a heartbeat in {age} minutes.");
                            removeWindowData(i); // This window is likely a ghost window, aka a window entry from a crashed or abruptly ended JKWatcher instance. It hasn't gotten a heartbeat in over 10 minutes (normally gets one every 30 seconds). Remove it.
                        }
                    }


                    return;
                }
            }
            catch (Exception e)
            {
                // Can't help it...
                Helpers.logToFile(e.ToString());
                return;
            }
        }

        private static void Wnd_LocationChanged(object sender, EventArgs e)
        {
            Window wnd = (Window)sender;
            if (wnd.WindowState != WindowState.Normal)
            {
                return; // Maximized and minimized positions are nonsense values.
            }
            try
            {
                using (new AccessorGetter())
                {
                    int windowIndex = -1;
                    lock (registeredWindows)
                    {
                        windowIndex = registeredWindows[wnd];
                    }
                    WindowPositionData newPosData = new WindowPositionData() { left = wnd.Left, top = wnd.Top, width = wnd.ActualWidth, height = wnd.ActualHeight };

                    changeWindowData(newPosData, windowIndex);




                    return;
                }
            }
            catch (Exception ecx)
            {
                // Can't help it...
                Helpers.logToFile(e.ToString());
                return;
            }
        }

        private static void Wnd_Closed(object sender, EventArgs e)
        {
            Window wnd = (Window)sender;
            wnd.Closed -= Wnd_Closed;
            wnd.LocationChanged -= Wnd_LocationChanged;
            int indexToRemove = -1;
            lock (registeredWindows)
            {
                indexToRemove = registeredWindows[wnd];
                registeredWindows.Remove(wnd);
            }
            try
            {
                using (new AccessorGetter())
                {

                    removeWindowData(indexToRemove);




                    return;
                }
            }
            catch (Exception ecx)
            {
                // Can't help it...
                Helpers.logToFile(e.ToString());
                return;
            }
        }

        public static void Activate()
        {
            // Force calls static constructor, *shrug*
        }

    }
}
