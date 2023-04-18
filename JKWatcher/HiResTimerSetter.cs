/*
*MIT License
*
*Copyright (c) 2023 S Christison
*
*Permission is hereby granted, free of charge, to any person obtaining a copy
*of this software and associated documentation files (the "Software"), to deal
*in the Software without restriction, including without limitation the rights
*to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
*copies of the Software, and to permit persons to whom the Software is
*furnished to do so, subject to the following conditions:
*
*The above copyright notice and this permission notice shall be included in all
*copies or substantial portions of the Software.
*
*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
*SOFTWARE.
*/

// Largely based on https://github.com/HypsyNZ/Delay.NET/
// It's largely unchanged except I just turned it into a static class that's intended to be called once at the start and that's it
// Then I just use Thread.Sleep from thereon out.

using System.Runtime.InteropServices;
using System.Security;

namespace JKWatcher
{
    class HiResTimerSetter
    {
        internal const string windowsMultimediaAPIString = "winmm.dll";

        [DllImport(windowsMultimediaAPIString)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern int timeBeginPeriod(int period);

        [DllImport(windowsMultimediaAPIString)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern int timeEndPeriod(int period);

        [DllImport(windowsMultimediaAPIString)]
        internal static extern int timeGetDevCaps(ref TimerCapabilities caps, int sizeOfTimerCaps);

        internal static TimerCapabilities Capabilities;

        static HiResTimerSetter()
        {
            timeGetDevCaps(ref Capabilities, Marshal.SizeOf(Capabilities));

        }

        public static void UnlockTimerResolution(){
            timeBeginPeriod(Capabilities.PeriodMinimum);
        }
        public static void LockTimerResolution() {
            timeEndPeriod(Capabilities.PeriodMinimum);
        }
        /*
        /// <summary>
        /// Platform Dependent Wait
        /// Accurately wait down to 1ms if your platform will allow it
        /// Replacement for Thread.Sleep()
        /// </summary>
        /// <param name="delayMs"></param>
        public static void Wait(int delayMs)
        {
            timeBeginPeriod(Capabilities.PeriodMinimum);
            Thread.Sleep(delayMs);
            timeEndPeriod(Capabilities.PeriodMinimum);
        }
        */
        /// <summary>
        /// The Min/Max supported period in milliseconds
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TimerCapabilities
        {
            /// <summary>Minimum supported period in milliseconds.</summary>
            public int PeriodMinimum;

            /// <summary>Maximum supported period in milliseconds.</summary>
            public int PeriodMaximum;
        }
    }
}
