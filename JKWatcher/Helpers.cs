using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JKWatcher
{

    class GlobalMutexHelper : IDisposable
    {
        private Mutex mutex = null;
        private bool active = false;

        public GlobalMutexHelper(string mutexNameWithoutGlobal,int? timeout=5000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            mutex = new Mutex(false,$"Global\\{mutexNameWithoutGlobal}");
            MutexSecurity sec = new MutexSecurity();
            sec.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid,null),MutexRights.FullControl,AccessControlType.Allow));
            mutex.SetAccessControl(sec);

            try
            {
                active = mutex.WaitOne(timeout == null || timeout < 0 ? Timeout.Infinite : timeout.Value,false);
            } catch(AbandonedMutexException e)
            {
                active = true;
            }

            if (!active)
            {
                throw new Exception($"Failed to acquire acquire mutex \"{mutexNameWithoutGlobal}\".");
            }
            Int64 elapsed = sw.ElapsedMilliseconds;
            sw.Stop();
            if(elapsed > 500)
            {
                Helpers.logToFile($"WARNING: Acquiring global mutex '{mutexNameWithoutGlobal}' took {elapsed} milliseconds.\n");
            }
        }
        public void Dispose()
        {
            if(mutex != null)
            {
                if (active)
                {
                    mutex.ReleaseMutex();
                }
                mutex.Dispose();
            }
        }
    }

    static class Helpers
    {

        private static readonly Dictionary<string, string> cachedFileReadCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> cachedFileReadLastRead = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, DateTime> cachedFileReadLastDateModified = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, bool> cachedFileReadFileExists = new Dictionary<string, bool>();
        const int cachedFileReadTimeout = 5000;
        const int cachedFileReadRetryDelay = 100;
        public static string cachedFileRead(string path)
        {
            string data = null;
            DateTime? lastModified = null;
            bool fileExists = false;
            try
            {

                try
                {

                    lock (cachedFileReadCache)
                    {
                        if (cachedFileReadCache.ContainsKey(path) && cachedFileReadLastRead.ContainsKey(path) && cachedFileReadFileExists.ContainsKey(path))
                        {
                            TimeSpan delta = DateTime.Now - cachedFileReadLastRead[path];
                            if (delta.TotalSeconds < 10)
                            {
                                return cachedFileReadCache[path];
                            }
                            else if (delta.TotalMinutes < 10)
                            {
                                if (File.Exists(path))
                                {
                                    if (cachedFileReadFileExists[path])
                                    {
                                        DateTime lastWriteTime = File.GetLastWriteTime(path);
                                        if (cachedFileReadLastDateModified.ContainsKey(path) && lastWriteTime == cachedFileReadLastDateModified[path])
                                        {
                                            return cachedFileReadCache[path];
                                        }
                                    }
                                } else
                                {
                                    if (!cachedFileReadFileExists[path])
                                    {
                                        return null; // File didn't exist. Still doesn't.
                                    }
                                }
                            }
                        }
                    }
                } catch(IOException)
                {
                    /// whatever
                }


                if (!File.Exists(path))
                {
                    data = null;
                }
                else
                {
                    fileExists = true;

                    using (new GlobalMutexHelper("JKWatcherCachedFileReadMutex"))
                    {

                        int retryTime = 0;
                        bool successfullyRead = false;
                        while (!successfullyRead && retryTime < cachedFileReadTimeout)
                        {
                            try
                            {

                                data = File.ReadAllText(path);
                                lastModified = File.GetLastWriteTime(path);

                                successfullyRead = true;
                            }
                            catch (IOException)
                            {
                                // Wait 100 ms then try again. File is probably locked.
                                // This will probably lock up the thread a bit in some cases
                                // but the log display/write thread is separate from the rest of the 
                                // program anyway so it shouldn't have a terrible impact other than a delayed
                                // display.
                                System.Threading.Thread.Sleep(cachedFileReadRetryDelay);
                                retryTime += cachedFileReadRetryDelay;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Failed to get  mutex, weird...
            }

            lock (cachedFileReadCache)
            {
                cachedFileReadCache[path] = data;
                cachedFileReadLastRead[path] = DateTime.Now;
                cachedFileReadFileExists[path] = fileExists;
                if (lastModified.HasValue)
                {
                    cachedFileReadLastDateModified[path] = lastModified.Value;
                }
            }
                
            return data;
        }

        public static DateTime? ToEST(this DateTime dateTime)
        {
            try
            {
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, easternZone);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public static DateTime? ToCEST(this DateTime dateTime)
        {
            try
            {
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, easternZone);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /*public static string EndsWithReturnStart(this string inputString, string otherString)
        {
            if (inputString.Length > otherString.Length
                   && inputString.Substring(inputString.Length - otherString.Length).Equals(otherString, StringComparison.OrdinalIgnoreCase)
                   )
            {
                return inputString.Substring(0, inputString.Length - otherString.Length);
                
            } else
            {
                return null;
            }
        }*/
        public static string EndsWithReturnStart(this string inputString, params string[] otherStrings)
        {
            foreach(string otherString in otherStrings)
            {

                if (inputString.Length > otherString.Length
                       && inputString.Substring(inputString.Length - otherString.Length).Equals(otherString, StringComparison.OrdinalIgnoreCase))
                {
                    return inputString.Substring(0, inputString.Length - otherString.Length);

                }
            }
            return null;
        }

        public static float DistanceToLineSlow(this in Vector3 point, in Vector3 linePoint1, in Vector3 linePoint2)
        {
            Vector3 P1toPoint = linePoint2 - point;
            Vector3 P1ToP2 = linePoint2 - linePoint1;

            return Math.Abs(Vector3.Dot(Vector3.Normalize(Vector3.Cross(Vector3.Cross(P1toPoint, P1ToP2), P1ToP2)), P1toPoint));
        }
        public static float DistanceToLine(this in Vector3 point, in Vector3 linePoint1, in Vector3 linePoint2)
        {

            Vector3 P1toPoint = linePoint2 - point;
            Vector3 P1ToP2 = linePoint2 - linePoint1;

            return Vector3.Cross(P1toPoint, P1ToP2).Length() / P1ToP2.Length();
        }

        // Z-coordinate is nulled. For horizontal distance to a line only in q3/jk2 coordinates
        public static float DistanceToLineXY(this in Vector3 point, in Vector3 linePoint1, in Vector3 linePoint2)
        {
            Vector3 P1toPoint = linePoint2 - point;
            Vector3 P1ToP2 = linePoint2 - linePoint1;

            P1toPoint.Z = 0;
            P1ToP2.Z = 0;

            return Vector3.Cross(P1toPoint, P1ToP2).Length() / P1ToP2.Length();
        }

        public static float LengthXY( this in Vector3 vector){
            Vector3 copy = vector;
            copy.Z = 0;
            return copy.Length();
        }

        public static bool IsSpaceChar(this char character)
        {
            return character == ' ' || character == '\n' || character == '\r' || character == '\t';
        }
        public static int SpacelessStringLength(this string thestring)
        {
            int minus = 0;
            foreach (var character in thestring)
            {
                if (character.IsSpaceChar())
                {
                    minus++;
                }
            }
            return thestring.Length - minus;
        }

        static public byte[] GetResourceData(string path)
        {
            path = path.Replace("\\",".");
            path = path.Replace("/",".");
            var ass = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = ass.GetManifestResourceStream(typeof(Helpers),path);
            if(stream != null)
            {
                using (MemoryStream ms = new MemoryStream((int)stream.Length))
                {

                    stream.CopyTo(ms);
                    stream.Dispose();
                    return ms.ToArray();
                }
            }
            else
            {
                return null;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        private const UInt32 FLASHW_ALL = 3;
        private const UInt32 FLASHW_TIMERNOFG = 12;

        private static Mutex flashyMutex = new Mutex();

        public static void FlashTaskBarIcon(Window window = null)
        {
            if(window == null)
            {
                lock (flashyMutex)
                {
                    IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; // Idk why this doesnt care about owning threads
                    FLASHWINFO fInfo = new FLASHWINFO();
                    fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
                    fInfo.hwnd = hwnd;
                    fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                    fInfo.uCount = UInt32.MaxValue;
                    fInfo.dwTimeout = 0;
                    FlashWindowEx(ref fInfo);
                }
            } else if(window.Dispatcher != null)
            {
                window.Dispatcher.Invoke(() => {  // Idk why this does care about owning threads
                    lock (flashyMutex)
                    {
                        IntPtr hwnd = (new System.Windows.Interop.WindowInteropHelper(window).Handle);
                        FLASHWINFO fInfo = new FLASHWINFO();
                        fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
                        fInfo.hwnd = hwnd;
                        fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                        fInfo.uCount = UInt32.MaxValue;
                        fInfo.dwTimeout = 0;
                        FlashWindowEx(ref fInfo);
                    }
                });
            }
            
            
        }


        static public unsafe string DemoCuttersanitizeFilename(string input , bool allowExtension)
        {
            
            byte[] byteArray = new byte[input.Length + 1];
            byte[] byteArrayOut = new byte[input.Length + 1];
            for(int i = 0; i < input.Length; i++)
            {
                byteArray[i] = (byte)input[i];
            }
            byteArray[input.Length] = 0;

            int outLength = 0;
            fixed (byte* inP=byteArray, outP=byteArrayOut)
            {
                DemoCuttersanitizeFilenameReal(inP, outP, allowExtension,ref outLength);
            }
            return Encoding.ASCII.GetString(byteArrayOut, 0,Math.Min(input.Length, outLength));
        }
        static unsafe void DemoCuttersanitizeFilenameReal(byte* input, byte* output, bool allowExtension, ref int outLength)
        {
            byte* outStart = output;
            byte* lastDot = (byte*)0;
            byte* inputStart = input;
            while (*input != 0)
            {
                if (*input == '.' && input != inputStart)
                { // Even tho we allow extensions (dots), we don't allow the dot at the start of the filename.
                    lastDot = output;
                }
                if ((*input == 32) // Don't allow ! exclamation mark. Linux doesn't like that.
                    || (*input >= 35 && *input < 42)
                    || (*input >= 43 && *input < 46)
                    || (*input >= 48 && *input < 58)
                    || (*input >= 59 && *input < 60)
                    || (*input == 61)
                    || (*input >= 64 && *input < 92)
                    || (*input >= 93 && *input < 96) // Don't allow `. Linux doesn't like that either, at least not in shell scripts.
                    || (*input >= 97 && *input < 124)
                    || (*input >= 125 && *input < 127)
                    )
                {
                    *output++ = *input;
                }
                else if (*input == '|')
                {

                    *output++ = (byte)'I';
                }

                else
                {
                    *output++ = (byte)'-';
                }
                input++;
            }
            *output = 0;
            outLength = (int)(output - outStart);

            if (allowExtension && lastDot != (byte*)0)
            {
                *lastDot = (byte)'.';
            }
        }


        public static string GetUnusedFilename(string baseFilename)
        {
            if (!File.Exists(baseFilename))
            {
                return baseFilename;
            }
            string extension = Path.GetExtension(baseFilename);

            int index = 1;
            while (File.Exists(Path.ChangeExtension(baseFilename, "." + (++index) + extension))) ;

            return Path.ChangeExtension(baseFilename, "." + (index) + extension);
        }

        private static int logfileWriteRetryDelay = 100;
        private static int logfileWriteTimeout = 5000;
        public static string forcedLogFileName = "forcedLog.log";
        public static void logToFile(string[] texts)
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher"));
            foreach (string line in texts)
            {
                Debug.WriteLine(line);
            }
            try {

                //lock (forcedLogFileName)
                using(new GlobalMutexHelper("JKWatcherForcedLogMutex"))
                {
                    int retryTime = 0;
                    bool successfullyWritten = false;
                    while (!successfullyWritten && retryTime < logfileWriteTimeout)
                    {
                        try
                        {

                            File.AppendAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", forcedLogFileName), texts);
                            successfullyWritten = true;
                        }
                        catch (IOException)
                        {
                            // Wait 100 ms then try again. File is probably locked.
                            // This will probably lock up the thread a bit in some cases
                            // but the log display/write thread is separate from the rest of the 
                            // program anyway so it shouldn't have a terrible impact other than a delayed
                            // display.
                            System.Threading.Thread.Sleep(logfileWriteRetryDelay);
                            retryTime += logfileWriteRetryDelay;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                // Failed to get  mutex, weird...
            }
        }

        public static void logToFile(string text){
            Helpers.logToFile(new string[] { text });
        }

        public static string requestedDemoCutLogFile = "demoCuts.sh";
        public static void logRequestedDemoCut(string[] texts)
        {
            try {

                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts"));
                //lock (forcedLogFileName)
                using(new GlobalMutexHelper("JKWatcherRequestedDemoCutLogMutex"))
                {
                    string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts", requestedDemoCutLogFile);
                    bool headerAppended = false;

                    if (!File.Exists(fullPath) && !headerAppended)
                    {
                        texts = (new string[] { @"#!/bin/bash" }).Concat(texts).ToArray();
                        headerAppended = true;
                    }
                    int retryTime = 0;
                    bool successfullyWritten = false; 
                    while (!successfullyWritten && retryTime < logfileWriteTimeout)
                    {
                        try
                        {
                            File.AppendAllLines(fullPath, texts);
                            successfullyWritten = true;
                        }
                        catch (IOException)
                        {
                            // Wait 100 ms then try again. File is probably locked.
                            // This will probably lock up the thread a bit in some cases
                            // but the log display/write thread is separate from the rest of the 
                            // program anyway so it shouldn't have a terrible impact other than a delayed
                            // display.
                            System.Threading.Thread.Sleep(logfileWriteRetryDelay);
                            retryTime += logfileWriteRetryDelay;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                // Failed to get  mutex, weird...
            }
        }

        static Mutex specificDebugMutex = new Mutex();


        public static void logToSpecificDebugFile(string[] lines, string specificFileName, bool append = false)
        {
            StringBuilder sb = new StringBuilder();
            foreach(string line in lines)
            {
                if (line == null) continue;
                sb.AppendLine(line);
            }
            logToSpecificDebugFile(Encoding.UTF8.GetBytes(sb.ToString()),specificFileName,append);
        }
        public static void logToSpecificDebugFile(byte[] data, string specificFileName, bool append = false)
        {

            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "debugLogsSpecific"));
            lock (specificDebugMutex)
            {

                string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "debugLogsSpecific", specificFileName);
                string fullPathUnique = append ? fullPath : Helpers.GetUnusedFilename(fullPath);

                int retryTime = 0;
                bool successfullyWritten = false;
                while (!successfullyWritten && retryTime < logfileWriteTimeout)
                {
                    try
                    {
                        if (append)
                        {
                            using (FileStream fs = new FileStream(fullPath, FileMode.Append))
                            {
                                fs.Write(data);
                            }
                        }
                        else{

                            File.WriteAllBytes(fullPathUnique, data);
                        }
                        successfullyWritten = true;
                    }
                    catch (IOException)
                    {
                        // NOTE: I dont think this should even happen here. We're not appending. But let's be safe and catch any errors.
                        // Don't want the software to crash because of a failed log file write.

                        // Wait 100 ms then try again. File is probably locked.
                        // This will probably lock up the thread a bit in some cases
                        // but the log display/write thread is separate from the rest of the 
                        // program anyway so it shouldn't have a terrible impact other than a delayed
                        // display.
                        System.Threading.Thread.Sleep(logfileWriteRetryDelay);
                        retryTime += logfileWriteRetryDelay;
                    }
                }
            }
        }
        public static string downloadLogFileName = "pk3DownloadLinks.csv";
        public static void logDownloadLinks(string[] texts)
        {
            lock (downloadLogFileName)
            {
                int retryTime = 0;
                bool successfullyWritten = false;
                while (!successfullyWritten && retryTime < logfileWriteTimeout)
                {
                    try
                    {

                        File.AppendAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", downloadLogFileName), texts);
                        successfullyWritten = true;
                    }
                    catch (IOException)
                    {
                        // Wait 100 ms then try again. File is probably locked.
                        // This will probably lock up the thread a bit in some cases
                        // but the log display/write thread is separate from the rest of the 
                        // program anyway so it shouldn't have a terrible impact other than a delayed
                        // display.
                        System.Threading.Thread.Sleep(logfileWriteRetryDelay);
                        retryTime += logfileWriteRetryDelay;
                    }
                }
            }
        }

        public static Mutex logMutex = new Mutex();
        public static void debugLogToFile(string logPrefix,string[] texts)
        {
            string validizedName = MakeValidFileName(logPrefix);
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "debugLogs"));
            string fileName = "debugLogs/"+validizedName + ".log";

            try
            {
                //lock (logMutex)
                using (new GlobalMutexHelper($"JKWatcherDebugLog{validizedName.Replace('\\','_')}"))
                {
                    int retryTime = 0;
                    bool successfullyWritten = false;
                    while (!successfullyWritten && retryTime < logfileWriteTimeout)
                    {
                        try
                        {

                            File.AppendAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", fileName), texts);
                            successfullyWritten = true;
                        }
                        catch (IOException)
                        {
                            // Wait 100 ms then try again. File is probably locked.
                            // This will probably lock up the thread a bit in some cases
                            // but the log display/write thread is separate from the rest of the 
                            // program anyway so it shouldn't have a terrible impact other than a delayed
                            // display.
                            System.Threading.Thread.Sleep(logfileWriteRetryDelay);
                            retryTime += logfileWriteRetryDelay;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                // Weird.
            }
            
        }

        // from: https://stackoverflow.com/a/847251
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        // Takes array of strings and turns them into chunks of maxSize with a chosen separator.
        // If any input chunk is too big, it will be split, first using empty spaces and commas,
        // then just hard cutoffs if any individual word is still over maxSize
        public static string[] StringChunksOfMaxSize(string[] input,int maxSize,string separator=", ",string chunkStart = "")
        {
            if(chunkStart.Length > maxSize)
            {
                throw new Exception("wtf man, chunkstart cant be bigger than maxsize");
            }
            int chunkStartLength = chunkStart.Length;
            int separatorLength = separator.Length;
            int maxActualSize = maxSize - chunkStartLength;
            List<string> noChunksTooBig = new List<string>();
            List<string> output = new List<string>();

            foreach (string inputString in input)
            {
                if (inputString == null) continue;
                if (inputString.Length > maxActualSize)
                {

                    string[] inputStringParts = inputString.Split(new char[] { ' ', ',' });
                    foreach(string inputStringPart in inputStringParts)
                    {
                        if(inputStringPart.Length > maxActualSize)
                        {
                            string[] inputStringPartsLimited = ChunksUpto(inputStringPart, maxActualSize).ToArray();
                            noChunksTooBig.AddRange(inputStringPartsLimited);
                        } else
                        {
                            noChunksTooBig.Add(inputString);
                        }
                    }
                }
                else
                {
                    noChunksTooBig.Add(inputString);
                }
            }

            string tmp = chunkStart;
            int stringsAdded = 0;
            foreach(string inputString in noChunksTooBig)
            {
                if(stringsAdded == 0)
                {
                    tmp += inputString; // Will only happen if chunkStart is an empty string
                    stringsAdded++;
                    continue;
                }
                int newLengthWouldBe = tmp.Length + separatorLength + inputString.Length;
                if (newLengthWouldBe < maxSize) // Still leaves some room
                {
                    tmp += separator + inputString;
                    stringsAdded++;
                }
                else if (newLengthWouldBe == maxSize) // exactly hits the limit
                {
                    tmp += separator + inputString;
                    output.Add(tmp);
                    tmp = chunkStart; 
                    stringsAdded = 0;
                } else
                {
                    // Too big to fit in. Turn into new string
                    output.Add(tmp);
                    tmp = chunkStart + inputString;
                    stringsAdded = 1;
                }
            }
            if(stringsAdded > 0)
            {
                output.Add(tmp);
                tmp = "";
            }

            return output.ToArray();
        }

        // following function from: https://stackoverflow.com/a/1450889
        static IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }

        public static string GetUnusedDemoFilename(string baseFilename, JKClient.ProtocolVersion protocolVersion)
        {
            string extension = ".dm_" + ((int)protocolVersion).ToString();
            if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos/" +baseFilename+ extension)))
            {
                return baseFilename;
            }
            //string extension = Path.GetExtension(baseFilename);

            int index = 1;
            while (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos/" + baseFilename+ "("+ (++index).ToString()+")" + extension))) ;

            return baseFilename + "(" + (++index).ToString() + ")";
        }

        static public BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        public static ByteImage BitmapToByteArray(Bitmap bmp)
        {

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int stride = Math.Abs(bmpData.Stride);
            int bytes = stride * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            bmp.UnlockBits(bmpData);

            return new ByteImage(rgbValues, stride, bmp.Width, bmp.Height, bmp.PixelFormat);
        }

        public static Bitmap ByteArrayToBitmap(ByteImage byteImage)
        {
            Bitmap myBitmap = new Bitmap(byteImage.width, byteImage.height, byteImage.pixelFormat);
            Rectangle rect = new Rectangle(0, 0, myBitmap.Width, myBitmap.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                myBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                myBitmap.PixelFormat);

            bmpData.Stride = byteImage.stride;

            IntPtr ptr = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(byteImage.imageData, 0, ptr, byteImage.imageData.Length);

            myBitmap.UnlockBits(bmpData);
            return myBitmap;

        }




    }

    public static class ProjectionMatrixHelper {


        static float[] s_flipMatrix = {
	        // convert from our coordinate system (looking down X)
	        // to OpenGL's coordinate system (looking down -Z)
	        0, 0, -1, 0,
            -1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 0, 1
        };

        public static Matrix4x4 createModelProjectionMatrix(Vector3 origin, Vector3 angles,float fovX, float width, float height)
        {

            Matrix4x4 modelMatrtix = createModelMatrix(origin,angles);
            Matrix4x4 projectionMatrix = createProjectionMatrix(width,height,fovX);

            return Matrix4x4.Multiply(modelMatrtix,projectionMatrix);
        }

        public static float DEG2RAD(float a)
        {
            return ((a) * (float)(Math.PI / 180.0));
        }

        public static float CG_CalcFOVFromX(float fov_x, float width, float height)
        {
            float x;
            //	float	phase;
            //	float	v;
            //	int		contents;
            float fov_y;

            x = width / (float)Math.Tan(fov_x / 360.0f * Math.PI);
            fov_y = (float)Math.Atan2(height, x);
            fov_y = fov_y * 360.0f / (float)Math.PI;


            return fov_y;//(fov_x,fov_y);
        }

        public static Matrix4x4 createProjectionMatrix(float widthScr, float heightScr, float fovX, float zFar = 2000, float zNear = 4)
        {
            float[] projectionMatrix = new float[16];
            Vector3[] axis = new Vector3[3];

            float xmin, xmax, ymin, ymax;
            float width,height,depth;
            //float zNear, zFar;


            //
            // set up projection matrix
            //

            ymax = zNear * (float)Math.Tan(DEG2RAD(CG_CalcFOVFromX(fovX,widthScr,heightScr) * 0.5f));
            ymin = -ymax;

            xmax = zNear * (float)Math.Tan(DEG2RAD(fovX * 0.5f));
            xmin = -xmax;

            width = xmax - xmin;
            height = ymax - ymin;
            depth = zFar - zNear;

            projectionMatrix[0] = 2 * zNear / width;
            projectionMatrix[4] = 0;
            projectionMatrix[8] = (xmax + xmin) / width;   // normally 0
            projectionMatrix[12] = 0;

            projectionMatrix[1] = 0;
            projectionMatrix[5] = 2 * zNear / height;
            projectionMatrix[9] = (ymax + ymin) / height;  // normally 0
            projectionMatrix[13] = 0;

            projectionMatrix[2] = 0;
            projectionMatrix[6] = 0;
            projectionMatrix[10] = -(zFar + zNear) / depth;
            projectionMatrix[14] = -2 * zFar * zNear / depth;

            projectionMatrix[3] = 0;
            projectionMatrix[7] = 0;
            projectionMatrix[11] = -1;
            projectionMatrix[15] = 0;

            return floatArrayToMatrix(projectionMatrix);


        }
        public static Matrix4x4 createModelMatrix(Vector3 origin, Vector3 angles)
        {
            float[] viewerMatrix = new float[16];
            Vector3[] axis = new Vector3[3];
            AngleVectors(angles, out axis[0], out axis[1], out axis[2]);
            viewerMatrix[0] = axis[0].X;
            viewerMatrix[4] = axis[0].Y;
            viewerMatrix[8] = axis[0].Z;
            viewerMatrix[12] = -origin.X * viewerMatrix[0] + -origin.Y * viewerMatrix[4] + -origin.Z * viewerMatrix[8];

            viewerMatrix[1] = axis[1].X;
            viewerMatrix[5] = axis[1].Y;
            viewerMatrix[9] = axis[1].Z;
            viewerMatrix[13] = -origin.X * viewerMatrix[1] + -origin.Y * viewerMatrix[5] + -origin.Z * viewerMatrix[9];

            viewerMatrix[2] = axis[2].X;
            viewerMatrix[6] = axis[2].Y;
            viewerMatrix[10] = axis[2].Z;
            viewerMatrix[14] = -origin.X * viewerMatrix[2] + -origin.Y * viewerMatrix[6] + -origin.Z * viewerMatrix[10];

            viewerMatrix[3] = 0;
            viewerMatrix[7] = 0;
            viewerMatrix[11] = 0;
            viewerMatrix[15] = 1;


            viewerMatrix = myGlMultMatrix(viewerMatrix, s_flipMatrix);



            return floatArrayToMatrix(viewerMatrix);


        }

        static Matrix4x4 floatArrayToMatrix(float[] matrix)
        {
            return new Matrix4x4() {
                M11 = matrix[0], M12 = matrix[1], M13 = matrix[2], M14 = matrix[3], 
                M21 = matrix[4], M22 = matrix[5], M23 = matrix[6], M24 = matrix[7], 
                M31 = matrix[8], M32 = matrix[9], M33 = matrix[10], M34 = matrix[11], 
                M41 = matrix[12], M42 = matrix[13], M43 = matrix[14], M44 = matrix[15], 
            };
        }

        static float[] myGlMultMatrix(float[] a, float[] b)
        {
            int i, j;
            float[] retVal = new float[16];

            for (i = 0; i < 4; i++)
            {
                for (j = 0; j < 4; j++)
                {
                    retVal[i * 4 + j] =
                      a[i * 4 + 0] * b[0 * 4 + j]
                      + a[i * 4 + 1] * b[1 * 4 + j]
                      + a[i * 4 + 2] * b[2 * 4 + j]
                      + a[i * 4 + 3] * b[3 * 4 + j];
                }
            }
            return retVal;
        }

        public static void AngleVectors(Vector3 angles, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            float angle;
            float sr, sp, sy, cr, cp, cy;
            // static to help MS compiler fp bugs

            angle = angles.Y * ((float)Math.PI * 2f / 360f);
            sy = (float)Math.Sin(angle);
            cy = (float)Math.Cos(angle);
            angle = angles.X * ((float)Math.PI * 2f / 360f);
            sp = (float)Math.Sin(angle);
            cp = (float)Math.Cos(angle);
            angle = angles.Z * ((float)Math.PI * 2f / 360f);
            sr = (float)Math.Sin(angle);
            cr = (float)Math.Cos(angle);

            forward.X = cp * cy;
            forward.Y = cp * sy;
            forward.Z = -sp;
            right.X = (-1 * sr * sp * cy + -1 * cr * -sy);
            right.Y = (-1 * sr * sp * sy + -1 * cr * cy);
            right.Z = -1 * sr * cp;
            up.X = (cr * sp * cy + -sr * -sy);
            up.Y = (cr * sp * sy + -sr * cy);
            up.Z = cr * cp;
        }
    }

}
