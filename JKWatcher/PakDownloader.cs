using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{
    static class PakDownloader
    {
        public struct PakDownload
        {
            public string pak;
            public int hash;
        }
        private static ConcurrentQueue<PakDownload> filesToDownload = new ConcurrentQueue<PakDownload>();

        static Mutex isRunningMutex = new Mutex();
        static bool isRunning = false;

        static string[] fileNameIgnoreList = { "assets0", "assets1", "assets2", "assets5" }; // Don't download these, we have them. lol

        static public void Enqueue(string pakUrl, int hash)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(pakUrl);
            if(Array.IndexOf(fileNameIgnoreList,fileNameWithoutExt) > -1)
            {
                return;
            }

            filesToDownload.Enqueue(new PakDownload {hash=hash,pak=pakUrl });
            lock (isRunningMutex)
            {
                if (!isRunning)
                {
                    Task.Factory.StartNew(() => { Run(); }, TaskCreationOptions.LongRunning).ContinueWith((t) => {
                        lock (isRunningMutex)
                        {
                            isRunning = false;
                        }
                    });
                    isRunning = true;
                } else
                {
                    // Already running, nvm.
                }
            }
        }
        static private void Run()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(1000);

                PakDownload currentDownload;
                while (filesToDownload.TryDequeue(out currentDownload))
                {
                    string targetFilename = Path.GetFileNameWithoutExtension(currentDownload.pak) +"_"+Convert.ToHexString(BitConverter.GetBytes(currentDownload.hash)) + Path.GetExtension(currentDownload.pak);
                    string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher","pakDownloads", targetFilename);
                    if (!File.Exists(targetPath))
                    {
                        // Do the actual download
                        try
                        {
                            byte[] fileData = new WebClient().DownloadData(currentDownload.pak);
                            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "pakDownloads"));
                            File.WriteAllBytes(targetPath,fileData);
                        } catch(Exception e)
                        {
                            // Whatever. If it didnt work it didnt work, don't care. Downloading the pk3s is a nice-to-have, not a top priority.
                        }
                    }
                }
            }
        }

    }
}
