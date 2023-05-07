using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace JKWatcher
{

    class GlobalMutexHelper : IDisposable
    {
        private Mutex mutex = null;
        private bool active = false;

        public GlobalMutexHelper(string mutexNameWithoutGlobal,int? timeout=5000)
        {
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
                    || (*input >= 34 && *input < 42)
                    || (*input >= 43 && *input < 46)
                    || (*input >= 48 && *input < 58)
                    || (*input >= 59 && *input < 60)
                    || (*input == 61)
                    || (*input >= 64 && *input < 92)
                    || (*input >= 93 && *input < 124)
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

        public static string requestedDemoCutLogFile = "demoCuts.bat";
        public static void logRequestedDemoCut(string[] texts)
        {
            try {

                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demoCuts"));
                //lock (forcedLogFileName)
                using(new GlobalMutexHelper("JKWatcherRequestedDemoCutLogMutex"))
                {
                    int retryTime = 0;
                    bool successfullyWritten = false;
                    while (!successfullyWritten && retryTime < logfileWriteTimeout)
                    {
                        try
                        {

                            File.AppendAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher","demoCuts", requestedDemoCutLogFile), texts);
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

        public static void logToSpecificDebugFile(byte[] data, string specificFileName)
        {

            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "debugLogsSpecific"));
            lock (specificDebugMutex)
            {

                string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "debugLogsSpecific", specificFileName);
                string fullPathUnique = Helpers.GetUnusedFilename(fullPath);

                int retryTime = 0;
                bool successfullyWritten = false;
                while (!successfullyWritten && retryTime < logfileWriteTimeout)
                {
                    try
                    {

                        File.WriteAllBytes(fullPathUnique,data);
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
}
