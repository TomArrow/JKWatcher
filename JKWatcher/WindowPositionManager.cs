using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
                gmh = new GlobalMutexHelper("JKWatcherWindowPositionManagerSharedMemoryMutex");
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
            mmf = MemoryMappedFile.CreateOrOpen("JKWatcherWindowPositionManagerSharedMemory.mmf", spaceRequired, MemoryMappedFileAccess.ReadWrite,MemoryMappedFileOptions.None,System.IO.HandleInheritability.Inheritable);

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
                    while (overlaps && positionIsLegal)
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

                    WindowPositionData newPosData = new WindowPositionData() {left=position[0],top=position[1],width=initialPosition[2],height=initialPosition[3] };


                    int newIndex = addWindowData(newPosData);

                    registeredWindows.Add(wnd, newIndex);
                    wnd.Closed += Wnd_Closed;
                    wnd.LocationChanged += Wnd_LocationChanged;



                    return true;
                }
            } catch(Exception e)
            {
                // Can't help it...
                return false;
            }

        }

        private static void Wnd_LocationChanged(object sender, EventArgs e)
        {
            try
            {
                using (new AccessorGetter())
                {


                    Window wnd = (Window)sender;
                    int windowIndex = registeredWindows[wnd];
                    WindowPositionData newPosData = new WindowPositionData() { left = wnd.Left, top = wnd.Top, width = wnd.ActualWidth, height = wnd.ActualHeight };

                    changeWindowData(newPosData, windowIndex);




                    return;
                }
            }
            catch (Exception ecx)
            {
                // Can't help it...
                return;
            }
        }

        private static void Wnd_Closed(object sender, EventArgs e)
        {
            Window wnd = (Window)sender;
            wnd.Closed -= Wnd_Closed;
            wnd.LocationChanged -= Wnd_LocationChanged;
            int indexToRemove = registeredWindows[wnd];
            registeredWindows.Remove(wnd);
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
                return;
            }
        }

        public static void Activate()
        {
            // Force calls static constructor, *shrug*
        }

    }
}
