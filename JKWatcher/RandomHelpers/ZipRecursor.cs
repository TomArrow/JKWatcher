//#define SHARPZIPLIB
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
#if SHARPZIPLIB
using ICSharpCode.SharpZipLib.Zip;
#else
using System.IO.Compression;
#endif

namespace JKWatcher.RandomHelpers
{
    class ZipRecursor
    {

        Dictionary<string, Dictionary<int, List<string>>> versionsWhere = new Dictionary<string, Dictionary<int, List<string>>>();

        Dictionary<string, List<byte[]>> versions = new Dictionary<string, List<byte[]>>();
        Regex searchFile = null;
        bool _trackVersions = false;
        Action<string, byte[], string> _foundFileCallback = null;
        string pathRoot = "";

        public ZipRecursor(Regex fileSearchRegex, Action<string,byte[],string> foundFileCallback, bool trackVersions=false)
        {
            if (fileSearchRegex is null || foundFileCallback is null)
            {
                throw new InvalidOperationException("ZipRecursor: Regex can't be null");
            }
            searchFile = fileSearchRegex;
            _trackVersions = trackVersions;
            _foundFileCallback = foundFileCallback;
        }
        public void HandleFolder(string folderPath)
        {
            string mainFolderPath = folderPath;
            pathRoot = folderPath;
            AnalyzeFolder(mainFolderPath);

        }
        public void HandleFile(string file)
        {
            pathRoot = Path.GetDirectoryName(file);
            // TODO Try to make all the folder paths consistent across the class
            string folderPath = Path.GetDirectoryName(file);
            if (searchFile.Match(file).Success)
            {
                HandleMatchedFile(file, folderPath, File.ReadAllBytes(file));
            }
            else if (file.EndsWith($".zip", StringComparison.OrdinalIgnoreCase) || file.EndsWith($".pk3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
#if SHARPZIPLIB
                    using (ZipFile mainArchive = new ZipFile(file))
                    {
                        AnalyzeZipFile(Path.Combine(folderPath, file), mainArchive, searchFile, ref versionsWhere, ref versions);
                    }
#else
                    using (ZipArchive mainArchive = ZipFile.OpenRead(file))
                    {
                        AnalyzeZipFile(Path.Combine(folderPath, file), mainArchive, searchFile);
                    }
#endif
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening: {file}", ex.Message);
                }
            }

        }

        public string GetVersionsReport()
        {
            if (!_trackVersions) return null;

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in versions)
            {
                sb.Append($"\n{kvp.Key}:\n");
                for (int i = 0; i < kvp.Value.Count; i++)
                {

                    sb.Append($"Version {i} is in the following paths:\n");
                    foreach (string path in versionsWhere[kvp.Key][i])
                    {
                        sb.Append($"\t{path}\n");
                    }
                }
            }
            return sb.ToString();
        }

        void AnalyzeFolder(string folderPath)
        {
            string[] listOfFiles = null;
            string[] listOfFolders = null;
            try
            {
                listOfFiles = Directory.GetFiles(folderPath);
                listOfFolders = Directory.GetDirectories(folderPath);
            } catch(Exception e)
            {
                Helpers.logToFile(e.ToString());
                return; // maybe for some reason the folder was not accessible, like junction tahts not connected
            }

            foreach (string file in listOfFiles)
            {
                if (searchFile.Match(file).Success)
                {
                    HandleMatchedFile(file, folderPath, File.ReadAllBytes(file));
                }
                else if (file.EndsWith($".zip", StringComparison.OrdinalIgnoreCase) || file.EndsWith($".pk3", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
#if SHARPZIPLIB
                    using (ZipFile mainArchive = new ZipFile(file))
                    {
                        AnalyzeZipFile(Path.Combine(folderPath, file), mainArchive, searchFile, ref versionsWhere, ref versions);
                    }
#else
                        using (ZipArchive mainArchive = ZipFile.OpenRead(file))
                        {
                            AnalyzeZipFile(Path.Combine(folderPath, file), mainArchive, searchFile);
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error opening: {file}", ex.Message);
                    }
                }
            }

            foreach (string folder in listOfFolders)
            {
                AnalyzeFolder(folder);
            }
        }
#if SHARPZIPLIB
    static void AnalyzeZipFile(string path, ZipFile mainArchive, Regex searchFile, ref Dictionary<string, Dictionary<int, List<string>>> versionsWhere, ref Dictionary<string, List<byte[]>> versions)
#else
        void AnalyzeZipFile(string path, ZipArchive mainArchive, Regex searchFile)
#endif
        {
#if SHARPZIPLIB
        foreach (ZipEntry entry in mainArchive)
#else
            foreach (ZipArchiveEntry entry in mainArchive.Entries)
#endif
            {
                if (entry.Name.EndsWith($".zip", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith($".pk3", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
#if SHARPZIPLIB
                    using (Stream zipStream = mainArchive.GetInputStream(entry))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            zipStream.CopyTo(ms);
                            using (ZipFile subArchive = new ZipFile(ms))
                            {
                                AnalyzeZipFile(Path.Combine(path, entry.Name), subArchive, searchFile, ref versionsWhere, ref versions);
                            }
                        }
                    }
#else
                        using (Stream zipStream = entry.Open())
                        {
                            using (ZipArchive subArchive = new ZipArchive(zipStream))
                            {
                                AnalyzeZipFile(Path.Combine(path, entry.Name), subArchive, searchFile);
                            }
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error opening: {path}/{entry.Name}", ex.Message);
                    }
                }
                if (searchFile.Match(entry.Name).Success)
                {
#if SHARPZIPLIB
                byte[] version = ReadFileFromZipEntry(mainArchive,entry);
#else
                    byte[] version = ReadFileFromZipEntry(entry);
#endif
                    HandleMatchedFile(entry.FullName, path, version);

                }
            }
        }

        void HandleMatchedFile(string fileName, string path, byte[] version)
        {
            string entryNameLower = fileName.ToLower();
            entryNameLower = Path.GetFileName(entryNameLower);
            path = Path.GetDirectoryName(Path.Combine(path,fileName));
            bool doProcess = false;
            int index = 0;
            if (_trackVersions)
            {
                int indexHere = 0;
                if (!versions.ContainsKey(entryNameLower))
                {
                    versions[entryNameLower] = new List<byte[]>();
                }
                if (!versionsWhere.ContainsKey(entryNameLower))
                {
                    versionsWhere[entryNameLower] = new Dictionary<int, List<string>>();
                }
                if ((indexHere = findVersion(entryNameLower, ref version, ref versionsWhere, ref versions)) == -1)
                {
                    doProcess = true;
                    index = versions[entryNameLower].Count;
                    versions[entryNameLower].Add(version);
                    versionsWhere[entryNameLower][index] = new List<string>();
                    //string targetFileName = entryNameLower;
                    //if (indexHere != 0)
                    //{
                    //    targetFileName = $"{Path.GetFileNameWithoutExtension(entryNameLower)}_version{(indexHere + 1)}{Path.GetExtension(entryNameLower)}";
                    //}
                    //File.WriteAllBytes(targetFileName, version);
                    //File.SetLastWriteTime(targetFileName, entry.LastWriteTime.DateTime);
                }
                versionsWhere[entryNameLower][indexHere].Add(path);
            }
            else
            {
                doProcess = true;
            }
            if (doProcess)
            {
                string relPath = Path.GetRelativePath(pathRoot, path);
                _foundFileCallback(entryNameLower, version, relPath);
            }
        }


        static int findVersion(string fileName, ref byte[] version, ref Dictionary<string, Dictionary<int, List<string>>> versionsWhere, ref Dictionary<string, List<byte[]>> versions)
        {
            List<byte[]> versionsHere = versions[fileName];
            for (int i = 0; i < versionsHere.Count; i++)
            {
                if (versionsHere[i].SequenceEqual(version))
                {
                    return i;
                }
            }
            return -1;
        }

#if SHARPZIPLIB
    static byte[] ReadFileFromZipEntry(ZipFile mainFile, ZipEntry entry)
    {
        using (BinaryReader reader = new BinaryReader(mainFile.GetInputStream(entry)))
        {
            return reader.ReadBytes((int)entry.Size);
        }
    }
#else
        static byte[] ReadFileFromZipEntry(ZipArchiveEntry entry)
        {
            using (BinaryReader reader = new BinaryReader(entry.Open()))
            {
                return reader.ReadBytes((int)entry.Length);
            }
        }
#endif
    }
}
