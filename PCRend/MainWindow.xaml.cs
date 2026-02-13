using JKWatcher;
using JKWatcher.RandomHelpers;
//using LibVLCSharp.Shared;
using FFmpeg.AutoGen;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PCRend.FFmpegStuff;
using System.Text.Json;
using PCRend.VideoMeta;
using System.Collections.Concurrent;

namespace PCRend
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct fp32Point
    {
        public Vector3 pos;
        public byte a;
        public byte b;
    };
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct fp16Point
    {
        public Int16 X;
        public Int16 Y;
        public Int16 Z;
        public byte a;
        public byte b;
    };

    /// <summary>
    /// Interaction logic for PointCloudRenderer.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitializeComponent();
            //quickTest();
        }

        public unsafe void quickTest()
        {

           // char* test = testString();
            char* test = parseVideoMetaToString((byte*)0,0,0,0,0,0,(UIntPtr*)0);
            int strlen = 0;
            byte* s = (byte*)test;
            while (*s != '\0')
            {
                strlen++;
                s++;
            }
            string testReadString = Encoding.GetEncoding("Windows-1252")?.GetString((byte*)test, strlen);
            Debug.WriteLine(testReadString);    
            freeVideoMetaString(test);

        }

        public unsafe string charPtrToString(byte* test)
        {
            int strlen = 0;
            byte* s = (byte*)test;
            while (*s != '\0')
            {
                strlen++;
                s++;
            }
            string testReadString = Encoding.GetEncoding("Windows-1252")?.GetString((byte*)test, strlen);
            return testReadString;
        }

        public bool SaveTiff(Vector3[,] lsData, string filenameString)
        {

            filenameString = Helpers.GetUnusedFilename(filenameString);
            LevelShotData oldTiff = null;
            if (File.Exists(filenameString))
            {
                oldTiff = LevelShotData.FromTiff(File.ReadAllBytes(filenameString));
                if (oldTiff == null) return false;
            }
            if (oldTiff != null)
            {
                LevelShotData.SumData(lsData, oldTiff.data);
            }
            byte[] tiff = LevelShotData.createTiffImage(lsData);
            if (tiff is null) return false;
            File.WriteAllBytes(filenameString, tiff);
            return true;
        }

        private void PrintPositionToImage(Vector3 pos, Vector3 color, Matrix4x4 modelMatrix, Matrix4x4 camTransform, LevelShotData levelShot, bool zComp = false)
        {
            Vector4 levelshotPos = Vector4.Transform(pos, camTransform);
            float theZ = levelshotPos.Z;
            levelshotPos /= levelshotPos.W;
            if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
            {
                int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                {
                    color *= LevelShotData.compensationMultipliers[posX, posY];

                    if (zComp)
                    {

                        // z compensated stuff that's stacked for infinity, adding state to a HDR tiff file etc.
                        // z compensation only looks good with ridiculously high amount of samples
                        Vector4 modelSpaceOrigin = Vector4.Transform(pos, modelMatrix);
                        float z1 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Y * modelSpaceOrigin.Y);
                        float z2 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Z * modelSpaceOrigin.Z);
                        color /= z1 * z2;
                        levelShot.data[posX, posY].X += color.Z;
                        levelShot.data[posX, posY].Y += color.Y;
                        levelShot.data[posX, posY].Z += color.X;
                    }
                    else
                    {
                        // bgr ordering.
                        levelShot.data[posX, posY].X += color.Z;
                        levelShot.data[posX, posY].Y += color.Y;
                        levelShot.data[posX, posY].Z += color.X;
                    }

                }
            }
        }
        
        private void PrintPositionsToImage(List<Tuple<Vector3,Vector3>> posColors, Matrix4x4 modelMatrix, Matrix4x4 camTransform, LevelShotData levelShot, bool zComp = false)
        {
            ConcurrentBag<Tuple<int, int, Vector3>> transformed = new ConcurrentBag<Tuple<int, int, Vector3>>();
            //List<Tuple<int,int, Vector3>> transformed = new List<Tuple<int,int, Vector3>>();
            //foreach (var posColor in posColors)
            Parallel.ForEach(posColors,(posColor)=> { 
                Vector4 levelshotPos = Vector4.Transform(posColor.Item1, camTransform);
                float theZ = levelshotPos.Z;
                levelshotPos /= levelshotPos.W;
                if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
                {
                    int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                    int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                    Vector3 color = new Vector3(posColor.Item2.Z, posColor.Item2.Y, posColor.Item2.X);
                    if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                    {
                        transformed.Add(new Tuple<int, int, Vector3>(posX,posY, color * LevelShotData.compensationMultipliers[posX, posY]));
                    }
                }
            });
            posColors.Clear();

            foreach(var point in transformed)
            {
                levelShot.data[point.Item1, point.Item2] += point.Item3;
            }

            /*if (zComp)
            {

                // z compensated stuff that's stacked for infinity, adding state to a HDR tiff file etc.
                // z compensation only looks good with ridiculously high amount of samples
                Vector4 modelSpaceOrigin = Vector4.Transform(pos, modelMatrix);
                float z1 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Y * modelSpaceOrigin.Y);
                float z2 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Z * modelSpaceOrigin.Z);
                color /= z1 * z2;
                levelShot.data[posX, posY].X += color.Z;
                levelShot.data[posX, posY].Y += color.Y;
                levelShot.data[posX, posY].Z += color.X;
            }
            else
            {
                // bgr ordering.
                levelShot.data[posX, posY].X += color.Z;
                levelShot.data[posX, posY].Y += color.Y;
                levelShot.data[posX, posY].Z += color.X;
            }*/

            
        }
        private void PrintPositionsToImage(List<Tuple<byte,byte,Vector3>> posColors, Matrix4x4 modelMatrix, Matrix4x4 camTransform, LevelShotData levelShot, bool zComp = false)
        {
            ConcurrentBag<Tuple<int, int, Vector3>> transformed = new ConcurrentBag<Tuple<int, int, Vector3>>();
            //List<Tuple<int,int, Vector3>> transformed = new List<Tuple<int,int, Vector3>>();
            //foreach (var posColor in posColors)
            Parallel.ForEach(posColors,(posColor)=> { 
                Vector4 levelshotPos = Vector4.Transform(posColor.Item3, camTransform);
                float theZ = levelshotPos.Z;
                levelshotPos /= levelshotPos.W;
                if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
                {
                    int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                    int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                    Vector3 color = new Vector3();

                    color.Z = (float)((posColor.Item1 & 240) | 15);
                    color.Y = (float)(((posColor.Item1 & 15) << 4) | 15);
                    color.X = (float)((posColor.Item2 & 240) | 15);
                    color *= divideby255;

                    if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                    {
                        transformed.Add(new Tuple<int, int, Vector3>(posX,posY, color * LevelShotData.compensationMultipliers[posX, posY]));
                    }
                }
            });
            posColors.Clear();

            foreach(var point in transformed)
            {
                levelShot.data[point.Item1, point.Item2] += point.Item3;
            }


            
        }
        private void PrintPositionsToImage(List<fp32Point> posColors, Matrix4x4 modelMatrix, Matrix4x4 camTransform, LevelShotData levelShot, bool zComp = false)
        {
            ConcurrentBag<Tuple<int, int, Vector3>> transformed = new ConcurrentBag<Tuple<int, int, Vector3>>();
            //List<Tuple<int,int, Vector3>> transformed = new List<Tuple<int,int, Vector3>>();
            //foreach (var posColor in posColors)
            Parallel.ForEach(posColors,(posColor)=> { 
                Vector4 levelshotPos = Vector4.Transform(posColor.pos, camTransform);
                float theZ = levelshotPos.Z;
                levelshotPos /= levelshotPos.W;
                if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
                {
                    int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                    int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                    Vector3 color = new Vector3();

                    color.Z = (float)((posColor.a & 240) | 15);
                    color.Y = (float)(((posColor.a & 15) << 4) | 15);
                    color.X = (float)((posColor.b & 240) | 15);
                    color *= divideby255;

                    if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                    {
                        transformed.Add(new Tuple<int, int, Vector3>(posX,posY, color * LevelShotData.compensationMultipliers[posX, posY]));
                    }
                }
            });
            posColors.Clear();

            foreach(var point in transformed)
            {
                levelShot.data[point.Item1, point.Item2] += point.Item3;
            }


            
        }
        private void PrintPositionsToImage(IEnumerable<fp32Point> posColors, Matrix4x4 modelMatrix, Matrix4x4 camTransform, LevelShotData levelShot, ref long ticks1,ref long ticks2, bool zComp = false)
        {
            Stopwatch sw= new Stopwatch();
            sw.Restart();
            ConcurrentBag<Tuple<int, int, Vector3>> transformed = new ConcurrentBag<Tuple<int, int, Vector3>>();
            //List<Tuple<int,int, Vector3>> transformed = new List<Tuple<int,int, Vector3>>();
            //foreach (var posColor in posColors)
            Parallel.ForEach(posColors,(posColor)=> { 
                Vector4 levelshotPos = Vector4.Transform(posColor.pos, camTransform);
                float theZ = levelshotPos.Z;
                levelshotPos /= levelshotPos.W;
                if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
                {
                    int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                    int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                    Vector3 color = new Vector3();

                    color.Z = (float)((posColor.a & 240) | 15);
                    color.Y = (float)(((posColor.a & 15) << 4) | 15);
                    color.X = (float)((posColor.b & 240) | 15);
                    color *= divideby255;

                    if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                    {
                        transformed.Add(new Tuple<int, int, Vector3>(posX,posY, color * LevelShotData.compensationMultipliers[posX, posY]));
                    }
                }
            });
            //posColors.Clear();

            ticks1 += sw.ElapsedTicks;

            sw.Restart();
            foreach (var point in transformed)
            {
                levelShot.data[point.Item1, point.Item2] += point.Item3;
            }

            ticks2 += sw.ElapsedTicks;


        }
        private unsafe void EncodeFrames(IEnumerable<frameRenderInfo> frames, MagicYUVVideoStreamEncoder enc, ref AVFrame frame)
        {
            foreach (frameRenderInfo frameInfo in frames)
            {
                byte[] data = LevelShotData.ToByteArray(frameInfo.lsData.data, true);
                fixed (byte* datap = data)
                {
                    frame.data = new byte_ptrArray8();
                    frame.data[0] = datap;
                    frame.data[1] = datap + frame.width * frame.height;
                    frame.data[2] = datap + frame.width * frame.height * 2;

                    enc.Encode(frame);
                }
                frame.pts++;
            }
        }

        private void PrintPositionsToImages(IReadOnlyList<frameRenderInfo> frames,IEnumerable<fp32Point> posColors, ref long ticks1,ref long ticks2, bool zComp = false)
        {
            object ticksLock = new object();
            long ticks1local = 0;
            long ticks2local = 0;
            Parallel.ForEach(frames,(frame)=> {
                Stopwatch sw = new Stopwatch();
                sw.Restart();
                if (float.IsNaN(frame.fov) || float.IsNaN(frame.pos.X) || float.IsNaN(frame.pos.Y) || float.IsNaN(frame.pos.Z) || float.IsNaN(frame.angles.X) || float.IsNaN(frame.angles.Y) || float.IsNaN(frame.angles.Z))
                {
                    return;
                }

                //List<Tuple<int, int, Vector3>> transformed = new List<Tuple<int, int, Vector3>>();

                foreach (var posColor in posColors)
                {
                    Vector4 levelshotPos = Vector4.Transform(posColor.pos, frame.camTransform);
                    float theZ = levelshotPos.Z;
                    levelshotPos /= levelshotPos.W;
                    if (theZ > 0 && levelshotPos.X >= -1.0f && levelshotPos.X <= 1.0f && levelshotPos.Y >= -1.0f && levelshotPos.Y <= 1.0f)
                    {
                        int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
                        int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
                        Vector3 color = new Vector3();

                        color.Z = (float)((posColor.a & 240) | 15);
                        color.Y = (float)(((posColor.a & 15) << 4) | 15);
                        color.X = (float)((posColor.b & 240) | 15);
                        color *= divideby255;

                        if (posX >= 0 && posX < LevelShotData.levelShotWidth && posY >= 0 && posY < LevelShotData.levelShotHeight)
                        {

                            frame.lsData.data[posX, posY] += color * LevelShotData.compensationMultipliers[posX, posY];
                            //transformed.Add(new Tuple<int, int, Vector3>(posX, posY, color * LevelShotData.compensationMultipliers[posX, posY]));
                        }
                    }
                }

                lock (ticksLock)
                {
                    ticks1local += sw.ElapsedTicks;
                }

                sw.Restart();
                //foreach (var point in transformed)
                //{
                //    frame.lsData.data[point.Item1, point.Item2] += point.Item3;
                //}

                lock (ticksLock)
                {
                    ticks2local += sw.ElapsedTicks;
                }
            });

            ticks1 += ticks1local;
            ticks2 += ticks2local;

        }

        const float divideby255 = 1.0f / 255.0f;

        class frameRenderInfo
        {
            public Vector3 pos;
            public Vector3 angles;
            public float fov;
            public Matrix4x4 modelMatrix;
            public Matrix4x4 camTransform;
            public LevelShotData lsData = null;
        }

        private void RenderFrames(IReadOnlyList<frameRenderInfo> frames, MagicYUVVideoStreamEncoder enc, ref AVFrame frame, string pointCloudFile, bool fp32)
        {
            try
            { // just in case of some invalid directory or whatever


                

                string filename = pointCloudFile;

                for(int i = 0; i < frames.Count(); i++)
                {
                    frameRenderInfo frameData = frames[i];
                    frameData.modelMatrix = ProjectionMatrixHelper.createModelMatrix(frameData.pos, frameData.angles, false);
                    frameData.camTransform = ProjectionMatrixHelper.createModelProjectionMatrix(frameData.pos, frameData.angles, frameData.fov, LevelShotData.levelShotWidth, LevelShotData.levelShotHeight);
                    frameData.lsData = new LevelShotData();
                }

                try
                {
                    // do the drawing

                    int itemLength = fp32 ? 14 : 8;

                    using (FileStream fs = new FileStream(filename, FileMode.Open))
                    {
                        fs.Seek(0, SeekOrigin.End);
                        Int64 count = fs.Position / (Int64)itemLength;
                        fs.Seek(0, SeekOrigin.Begin);
                        using (BinaryReader br = new BinaryReader(fs))
                        {
                            //List<Tuple<byte,byte, Vector3>> items = new List<Tuple<byte, byte, Vector3>>();
                            List<fp32Point> items = new List<fp32Point>();
                            fp32Point dummy;
                            items.Capacity = 10000000;
                            Stopwatch sw = new Stopwatch();
                            long ticksread = 0;
                            long tickstoarr = 0;
                            long ticksprocess1 = 0;
                            long ticksprocess2 = 0;
                            if (fp32)
                            {
                                Task printTask = null;
                                while (count > 0)
                                {
                                    Int64 batch = Math.Min(10000000, count);
                                    sw.Restart();
                                    var points = Helpers.ReadBytesAsTypeArray<fp32Point>(br, (int)batch);
                                    ticksread += sw.ElapsedTicks; sw.Restart();
                                    count -= batch;
                                    fp32Point[] arr = points.ToArray();
                                    tickstoarr += sw.ElapsedTicks;
                                    if (printTask != null)
                                    {
                                        printTask.Wait();
                                    }
                                    printTask = Task.Run(()=> {
                                        PrintPositionsToImages(frames, arr, ref ticksprocess1, ref ticksprocess2, false);
                                    });
                                }
                                if(printTask != null)
                                {
                                    printTask.Wait();
                                }

                                Console.WriteLine($"{(double)ticksread/(double)Stopwatch.Frequency}s reading, {(double)tickstoarr/(double)Stopwatch.Frequency}s making array, {(double)ticksprocess1/(double)Stopwatch.Frequency}s processing 1, {(double)ticksprocess2/(double)Stopwatch.Frequency}s processing 2");

                            }/*
                            else
                            {
                                // only s16 supported for nowfor (Int64 i = 0; i < count; i++)
                                for (Int64 i = 0; i < count; i++) { 
                                    Vector3 pos = new Vector3();
                                    Vector3 color = new Vector3();
                                    pos.X = br.ReadInt16();
                                    pos.Y = br.ReadInt16();
                                    pos.Z = br.ReadInt16();
                                    byte a = br.ReadByte();
                                    byte b = br.ReadByte();
                                    //items.Add(new Tuple<byte, byte, Vector3>(a, b, pos));
                                    items.Add(new fp32Point() {a=a,b=b,pos=pos });
                                    if (items.Count >= 10000000 || i == count - 1)
                                    {
                                        PrintPositionsToImage(items, modelMatrix, camTransform, lsData, false);
                                        items.Clear();
                                        items.Capacity = 10000000;
                                    }
                                }
                            }*/

                        }
                    }

                    EncodeFrames(frames, enc, ref frame);

                }
                catch (Exception ex)
                {
                    Helpers.logToFile($"Error doing pointcloud render render: {ex.ToString()}");
                }
                    
                    

                
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing pointcloud render (outer): {e2.ToString()}");
            }

        }
        private void renderBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            { // just in case of some invalid directory or whatever

                bool fp32 = pointCloudRadio_fp32?.IsChecked == true;
                string posanglestxt = posAnglesTxt.Text;
                string fovtxt = fovTxt.Text;
                if (string.IsNullOrWhiteSpace(posanglestxt))
                {
                    return;
                }
                if (string.IsNullOrWhiteSpace(fovtxt))
                {
                    return;
                }
                string[] posAnglesPieces = posanglestxt.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (posAnglesPieces.Length < 5)
                {
                    return;
                }

                Vector3 pos = new Vector3();
                Vector3 angles = new Vector3();

                float.TryParse(posAnglesPieces[0], out pos.X);
                float.TryParse(posAnglesPieces[1], out pos.Y);
                float.TryParse(posAnglesPieces[2], out pos.Z);
                float.TryParse(posAnglesPieces[3], out angles.X);
                float.TryParse(posAnglesPieces[4], out angles.Y);

                if (posAnglesPieces.Length >= 6)
                {
                    float.TryParse(posAnglesPieces[5], out angles.Z);
                }

                float.TryParse(fovtxt, out float fov);

                if (float.IsNaN(fov) || float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) || float.IsNaN(angles.X) || float.IsNaN(angles.Y) || float.IsNaN(angles.Z))
                {
                    return;
                }

                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "Point cloud (.bin)|*.bin";

                //Directory.CreateDirectory(imagesSubDir);

                //string imagesSubDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "images");
                //if (Directory.Exists(imagesSubDir))
                //{
                //    ofd.InitialDirectory = imagesSubDir;
                //}


                if (ofd.ShowDialog() == true)
                {
                    string filename = ofd.FileName;

                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "TIFF images (.tif;.tiff)|*.tif;*.tiff";
                    sfd.FileName = Path.ChangeExtension(Path.GetFileName(filename), ".tif");
                    sfd.InitialDirectory = Path.GetDirectoryName(filename);

                    if (sfd.ShowDialog() == true)
                    {
                        string filenameOut = sfd.FileName;
                        Task.Run(() => {
                            try
                            {
                                // do the drawing

                                Matrix4x4 modelMatrix = ProjectionMatrixHelper.createModelMatrix(pos, angles, false);
                                Matrix4x4 camTransform = ProjectionMatrixHelper.createModelProjectionMatrix(pos, angles, fov, LevelShotData.levelShotWidth, LevelShotData.levelShotHeight);
                                LevelShotData lsData = new LevelShotData();
                                //LevelShotData lsData = LevelShotData.FromTiff(File.ReadAllBytes(filename));

                                int itemLength = fp32 ? 14 : 8;

                                using (FileStream fs = new FileStream(filename, FileMode.Open))
                                {
                                    fs.Seek(0, SeekOrigin.End);
                                    Int64 count = fs.Position / (Int64)itemLength;
                                    fs.Seek(0, SeekOrigin.Begin);
                                    using (BinaryReader br = new BinaryReader(fs))
                                    {
                                        //List<Tuple<byte,byte, Vector3>> items = new List<Tuple<byte, byte, Vector3>>();
                                        List<fp32Point> items = new List<fp32Point>();
                                        fp32Point dummy;
                                        items.Capacity = 10000000;
                                        Stopwatch sw = new Stopwatch();
                                        long ticksread = 0;
                                        long tickstoarr = 0;
                                        long ticksprocess1 = 0;
                                        long ticksprocess2 = 0;
                                        if (fp32)
                                        {
                                            Task printTask = null;
                                            while (count > 0)
                                            {
                                                Int64 batch = Math.Min(10000000, count);
                                                //for (Int64 i = 0; i < batch; i++)
                                                //{
                                                //    items.Add(Helpers.ReadBytesAsType<fp32Point>(br));
                                                //}
                                                sw.Restart();
                                                var points = Helpers.ReadBytesAsTypeArray<fp32Point>(br, (int)batch);
                                                ticksread += sw.ElapsedTicks; sw.Restart();
                                                count -= batch;
                                                fp32Point[] arr = points.ToArray();
                                                tickstoarr += sw.ElapsedTicks;
                                                //PrintPositionsToImage(points.ToArray(), modelMatrix, camTransform, lsData, false);
                                                if (printTask != null)
                                                {
                                                    printTask.Wait();
                                                }
                                                printTask = Task.Run(()=> {
                                                    PrintPositionsToImage(arr, modelMatrix, camTransform, lsData, ref ticksprocess1, ref ticksprocess2, false);
                                                });
                                                //items.Clear();
                                                //items.Capacity = 10000000;
                                            }
                                            if(printTask != null)
                                            {
                                                printTask.Wait();
                                            }

                                            Console.WriteLine($"{(double)ticksread/(double)Stopwatch.Frequency}s reading, {(double)tickstoarr/(double)Stopwatch.Frequency}s making array, {(double)ticksprocess1/(double)Stopwatch.Frequency}s processing 1, {(double)ticksprocess2/(double)Stopwatch.Frequency}s processing 2");

                                            //for (Int64 i = 0; i < count; i++)
                                            //{
                                                //Vector3 pos = new Vector3();
                                                //Vector3 color = new Vector3();
                                                //pos.X = br.ReadSingle();
                                                //pos.Y = br.ReadSingle();
                                                //pos.Z = br.ReadSingle();
                                                //byte a = br.ReadByte();
                                                //byte b = br.ReadByte();
                                                //dummy = Helpers.ReadBytesAsType<fp32Point>(br);
                                                //items.Add(Helpers.ReadBytesAsType<fp32Point>(br));
                                                //items.Add(new Tuple<byte, byte, Vector3>(dummy.a, dummy.b, dummy.pos));
                                                //if (items.Count >= 10000000 || i == count - 1)
                                                //{
                                                //}
                                            //}
                                        }
                                        else
                                        {
                                            // only s16 supported for nowfor (Int64 i = 0; i < count; i++)
                                            for (Int64 i = 0; i < count; i++) { 
                                                Vector3 pos = new Vector3();
                                                Vector3 color = new Vector3();
                                                pos.X = br.ReadInt16();
                                                pos.Y = br.ReadInt16();
                                                pos.Z = br.ReadInt16();
                                                byte a = br.ReadByte();
                                                byte b = br.ReadByte();
                                                //items.Add(new Tuple<byte, byte, Vector3>(a, b, pos));
                                                items.Add(new fp32Point() {a=a,b=b,pos=pos });
                                                if (items.Count >= 10000000 || i == count - 1)
                                                {
                                                    PrintPositionsToImage(items, modelMatrix, camTransform, lsData, false);
                                                    items.Clear();
                                                    items.Capacity = 10000000;
                                                }
                                            }
                                        }

                                        /*for (Int64 i = 0; i < count; i++)
                                        {
                                            Vector3 pos = new Vector3();
                                            Vector3 color = new Vector3();
                                            if (fp32)
                                            {
                                                pos.X = br.ReadSingle();
                                                pos.Y = br.ReadSingle();
                                                pos.Z = br.ReadSingle();
                                            }
                                            else
                                            {
                                                // only s16 supported for now
                                                pos.X = br.ReadInt16();
                                                pos.Y = br.ReadInt16();
                                                pos.Z = br.ReadInt16();
                                            }
                                            byte a = br.ReadByte();
                                            byte b = br.ReadByte();
                                            //color.X = (float)((a & 240) | 15) * divideby255;
                                            //color.Y = (float)(((a & 15) << 4) | 15) * divideby255;
                                            //color.Z = (float)((b & 240) | 15) * divideby255;
                                            items.Add(new Tuple<byte,byte, Vector3>(a,b,color));
                                            //PrintPositionToImage(pos, color, modelMatrix, camTransform, lsData, false);
                                            if (items.Count > 100000 || i == count - 1)
                                            {
                                                PrintPositionsToImage(items,modelMatrix, camTransform, lsData, false);
                                                items.Clear();
                                            }
                                        }*/
                                    }
                                }


                                string filenameTiffString = filenameOut;

                                SaveTiff(lsData.data, filenameTiffString);

                                string filenameString = System.IO.Path.GetFileNameWithoutExtension(filenameTiffString) + "_RENDER_" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                string imagesSubDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filenameOut), "lsRender");
                                Directory.CreateDirectory(imagesSubDir);
                                Bitmap bmp = LevelShotData.ToBitmap(lsData.data, 0);

                                if (bmp is null)
                                {

                                    Helpers.logToFile($"Error rendering {filename} as point cloud: {e.ToString()}");
                                    return;
                                }
                                filenameString = Helpers.MakeValidFileName(filenameString) + ".png";
                                filenameString = System.IO.Path.Combine(imagesSubDir, filenameString);
                                filenameString = Helpers.GetUnusedFilename(filenameString);

                                bmp.Save(filenameString);
                                bmp.Dispose();

                                // Open it maybe :)
                                using Process fileopener = new Process();
                                fileopener.StartInfo.FileName = "explorer";
                                fileopener.StartInfo.Arguments = $"\"{System.IO.Path.GetFullPath(filenameString)}\"";
                                fileopener.Start();

                            }
                            catch (Exception ex)
                            {
                                Helpers.logToFile($"Error doing pointcloud render render: {ex.ToString()}");
                            }
                        });
                    }

                }
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing pointcloud render (outer): {e2.ToString()}");
            }

        }

        [DllImport("videoMetaLib.dll",CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe char* testString();
        [DllImport("videoMetaLib.dll",CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe char* parseVideoMetaToString(byte* buf, int width, int height, int totalHeight, int stride, int multiplier, UIntPtr* rgboffsets3Array);
        [DllImport("videoMetaLib.dll",CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void freeVideoMetaString(char* metaString);

        private unsafe void videoTestBtn_Click(object sender, RoutedEventArgs e)
        {

            try
            { // just in case of some invalid directory or whatever

                bool makeSubtitles = makeSubtitlesCheck.IsChecked == true;
                bool makePointCloudVideo = makePointCloudVidCheck.IsChecked == true;
                bool fp32 = pointCloudRadio_fp32?.IsChecked == true;

                string videoFilename = null;
                string pointcloudFilename = null;
                var sfd0 = new Microsoft.Win32.SaveFileDialog();
                sfd0.Filter = "AVI video file (.avi)|*.avi";
                sfd0.Title = "Where to save video file";
                if (makePointCloudVideo && sfd0.ShowDialog() == true)
                {
                    videoFilename = sfd0.FileName;
                    var ofd0 = new Microsoft.Win32.OpenFileDialog();
                    ofd0.Title = "Find pointcloud file";
                    ofd0.Filter = "Point cloud (.bin)|*.bin";

                    if (ofd0.ShowDialog() == true)
                    {
                        pointcloudFilename = ofd0.FileName;
                    }
                }

                var ofd = new Microsoft.Win32.OpenFileDialog();
                //ofd.Filter = "Point cloud (.bin)|*.bin";
                //string test = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-x86", "native"); // todo make this nicer... cross-platform? oh well wpf is windumb anyway
                string test = FFmpegHelper.findFFmpegLibs();
                ffmpeg.RootPath = test;
                //MessageBox.Show($"ffmpeg dir: {test}");
                ofd.Title = "Select Source video";
                if (ofd.ShowDialog() == true)
                {
                    StringBuilder subtitlesString = new StringBuilder();
                    UIntPtr[] rgbOffsets = new UIntPtr[4] { (UIntPtr)0, (UIntPtr)1, (UIntPtr)2, (UIntPtr)3 };

                    ConsoleState oldConsole = new ConsoleState();
                    CenterPrintState oldCenterPrint = new CenterPrintState();
                    List<Tuple<long,long,string>> subtitleEntries = new List<Tuple<long,long, string>>();

                    using (VideoStreamDecoder decoder = new VideoStreamDecoder(ofd.FileName))
                    {
                        using (VideoConverter converter = new VideoConverter(decoder.FrameSize, decoder.PixelFormat, decoder.FrameSize, AVPixelFormat.AV_PIX_FMT_RGB24))
                        {
                            Task encoderTask = null;
                            MagicYUVVideoStreamEncoder enc = null;
                            AVFrame outFrame = new AVFrame();
                            List<frameRenderInfo> renderFrames = null;
                            System.Drawing.Size outVideoSize = new System.Drawing.Size(1920, 1080);
                            if (!string.IsNullOrWhiteSpace(videoFilename) && !string.IsNullOrWhiteSpace(pointcloudFilename))
                            {
                                enc = new MagicYUVVideoStreamEncoder(videoFilename,decoder.TimeBase, outVideoSize);
                                outFrame.linesize = new int_array8();
                                outFrame.linesize[0] = outVideoSize.Width;
                                outFrame.linesize[1] = outVideoSize.Width;
                                outFrame.linesize[2] = outVideoSize.Width;
                                outFrame.pts = outFrame.best_effort_timestamp = 0;
                                outFrame.width = outVideoSize.Width;
                                outFrame.height = outVideoSize.Height;
                                outFrame.format = (int)AVPixelFormat.AV_PIX_FMT_GBRP;
                                outFrame.time_base = decoder.TimeBase; 
                                renderFrames = new List<frameRenderInfo>();
                            }

                            while(decoder.TryDecodeNextFrame(out AVFrame frame))
                            {
                                double exactTime = (double)frame.best_effort_timestamp * decoder.TimeBaseDouble;
                                long assTimeStamp = 100 * frame.best_effort_timestamp * decoder.TimeBase.num / decoder.TimeBase.den;
                                var originalFrame = frame;
                                if (decoder.PixelFormat != AVPixelFormat.AV_PIX_FMT_RGB24)
                                {
                                    frame = converter.Convert(frame);
                                }
                                int stride = frame.linesize[0];
                                byte* data = frame.data[0];

                                byte* json = (byte*)0;
                                fixed (UIntPtr* rgboff = rgbOffsets)
                                {
                                    json = (byte*)parseVideoMetaToString((byte*)frame.data[0], originalFrame.width, originalFrame.height, originalFrame.height, frame.linesize[0], 3, rgboff);

                                    string jsonStr = charPtrToString(json);

                                    freeVideoMetaString((char*)json);

                                    try
                                    {
                                        JsonSerializerOptions opts = new JsonSerializerOptions() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
                                        VideoMeta.VideoMeta meta = JsonSerializer.Deserialize<VideoMeta.VideoMeta>(jsonStr, opts);

                                        if (renderFrames != null)
                                        {
                                            if (meta != null)
                                            {
                                                renderFrames.Add(new frameRenderInfo() { pos = new Vector3(meta.camera.pos[0], meta.camera.pos[1], meta.camera.pos[2]), angles = new Vector3(meta.camera.ang[0], meta.camera.ang[1], meta.camera.ang[2]), fov = meta.camera.fov });
                                            }
                                            else
                                            {
                                                renderFrames.Add(new frameRenderInfo() { fov = 120 });
                                            }
                                        }

                                        ConsolelineSimple[] newLines = meta.getSimpleConsoleLines(3000,true);
                                        if (!oldConsole.lines.SequenceEqualSafe(newLines))
                                        {

                                            Debug.WriteLine($"new console state at {exactTime}s");
                                            if(oldConsole.lines != null && oldConsole.lines.Length > 0)
                                            {
                                                subtitleEntries.Add(new Tuple<long,long, string>(oldConsole.startTime,1,oldConsole.getASSString(assTimeStamp,meta.versionAtLeast(0,0,0,3))));
                                            }
                                            if(newLines != null)
                                            {
                                                foreach (ConsolelineSimple line in newLines)
                                                {
                                                    Debug.WriteLine($"{line.plaintext}");
                                                }
                                            }
                                            oldConsole.lines = newLines;
                                            oldConsole.startTime = assTimeStamp;
                                        }

                                        Centerprint newCenterPrint = meta.centerPrint == null ? null : meta.centerPrint.getColorCorrectedCenterPrint(true);
                                        if ((newCenterPrint is null) != (oldCenterPrint.cprint is null) || !oldCenterPrint.cprint.Equals(newCenterPrint))
                                        {

                                            Debug.WriteLine($"new centerprint state at {exactTime}s");
                                            if(oldCenterPrint.cprint != null && oldCenterPrint.cprint.plaintext.Length > 0)
                                            {
                                                subtitleEntries.Add(new Tuple<long,long, string>(oldCenterPrint.startTime,0,oldCenterPrint.getASSString(assTimeStamp)));
                                            }
                                            if(newCenterPrint != null)
                                            {
                                                Debug.WriteLine($"{newCenterPrint.plaintext}");
                                            }
                                            oldCenterPrint.cprint = newCenterPrint;
                                            oldCenterPrint.startTime = assTimeStamp;
                                        }

                                        Debug.WriteLine(meta);
                                    }
                                    catch (Exception exe)
                                    {
                                        Debug.WriteLine(exe.ToString());
                                    }

                                    //Debug.WriteLine(pixel);
                                    Debug.WriteLine(frame.width);
                                }
                                if(renderFrames != null && renderFrames.Count >= 100)
                                {
                                    if(encoderTask != null)
                                    {
                                        encoderTask.Wait();
                                    }
                                    frameRenderInfo[] frameInfos = renderFrames.ToArray();
                                    renderFrames.Clear();
                                    encoderTask = Task.Run(()=> {
                                        RenderFrames(frameInfos, enc, ref outFrame, pointcloudFilename, fp32);
                                    });
                                }
                            }

                            if (encoderTask != null)
                            {
                                encoderTask.Wait();
                            }
                            if (renderFrames != null && renderFrames.Count >= 0)
                            {
                                RenderFrames(renderFrames, enc, ref outFrame, pointcloudFilename, fp32);
                                renderFrames.Clear();
                            }
                            if(enc != null)
                            {
                                enc.Drain();
                                enc.Dispose();
                            }
                        }
                    }

                    subtitleEntries.Sort();
                    Debug.WriteLine($"{subtitleEntries.Count} subtitle entries.");
                    subtitlesString.Append(Encoding.ASCII.GetString(Helpers.GetResourceData("files/skeleton.ass",true,typeof(MainWindow))));
                    foreach(var entry in subtitleEntries)
                    {
                        subtitlesString.AppendLine(entry.Item3);
                    }
                    if (makeSubtitles)
                    {
                        SaveFileDialog sfd = new SaveFileDialog();
                        sfd.Filter = "ASSA subtitle (.ass)|*.ass";
                        if(sfd.ShowDialog() == true)
                        {
                            File.WriteAllText(sfd.FileName, subtitlesString.ToString());
                        }
                    }
                }
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing pointcloud video test (outer): {e2.ToString()}");
                MessageBox.Show($"Error doing pointcloud video test (outer): {e2.ToString()}");
            }
        }


        private unsafe void videoTest2Btn_Click(object sender, RoutedEventArgs e)
        {

            try
            { // just in case of some invalid directory or whatever

                bool makeSubtitles = makeSubtitlesCheck.IsChecked == true;

                var sfd = new Microsoft.Win32.SaveFileDialog();
                //ofd.Filter = "Point cloud (.bin)|*.bin";
                string test = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-x86", "native"); // todo make this nicer... cross-platform? oh well wpf is windumb anyway
                ffmpeg.RootPath = test;
                if (sfd.ShowDialog() == true)
                {
                    //using (FileStream fs = File.OpenWrite(sfd.FileName))
                    {
                        System.Drawing.Size size = new System.Drawing.Size(640, 360);
                        MagicYUVVideoStreamEncoder enc = new MagicYUVVideoStreamEncoder(sfd.FileName, new AVRational() { num = 2, den = 1}, size);
                        //AVFrame* frameP = ffmpeg.av_frame_alloc();
                        //AVFrame frame = *frameP;// new AVFrame();
                        AVFrame frame = new AVFrame();
                        frame.width = size.Width;
                        frame.height = size.Height;
                        frame.format = (int)AVPixelFormat.AV_PIX_FMT_GBRP;
                        frame.time_base = new AVRational { num = 1, den = 2 }; 
                        //ffmpeg.av_frame_get_buffer(&frame, 24).ThrowExceptionIfError(); ;
                        byte[] data = new byte[size.Width * size.Height * 3];
                        //ffmpeg.av_frame_make_writable(&frame).ThrowExceptionIfError(); ;
                        fixed (byte* datap = data)
                        {
                            frame.data = new byte_ptrArray8();
                            frame.data[0] = datap;
                            frame.data[1] = datap+size.Width * size.Height;
                            frame.data[2] = datap+size.Width * size.Height * 2;
                            frame.linesize = new int_array8();
                            frame.linesize[0] = size.Width;
                            frame.linesize[1] = size.Width;
                            frame.linesize[2] = size.Width;
                            //frame.data[0] = datap;
                            frame.pts = frame.best_effort_timestamp = 0;
                            enc.Encode(frame);
                            frame.pts++;
                            enc.Encode(frame);
                            frame.pts++;
                            enc.Encode(frame);
                        }
                        enc.Drain();
                        enc.Dispose();
                    }
                }
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing pointcloud video test 2 (outer): {e2.ToString()}");
            }
        }
    }
}
