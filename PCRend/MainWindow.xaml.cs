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

namespace PCRend
{
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

        public bool SaveTiff(float[,,] lsData, string filenameString)
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
                        levelShot.data[posX, posY, 0] += color.Z;
                        levelShot.data[posX, posY, 1] += color.Y;
                        levelShot.data[posX, posY, 2] += color.X;
                    }
                    else
                    {
                        // bgr ordering.
                        levelShot.data[posX, posY, 0] += color.Z;
                        levelShot.data[posX, posY, 1] += color.Y;
                        levelShot.data[posX, posY, 2] += color.X;
                    }

                }
            }
        }

        const float divideby255 = 1.0f / 255.0f;

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
                                        for (Int64 i = 0; i < count; i++)
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
                                            color.X = (float)((a & 240) | 15) * divideby255;
                                            color.Y = (float)(((a & 15) << 4) | 15) * divideby255;
                                            color.Z = (float)((b & 240) | 15) * divideby255;
                                            PrintPositionToImage(pos, color, modelMatrix, camTransform, lsData, false);
                                        }
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
        public static extern unsafe char* freeVideoMetaString(char* metaString);

        private unsafe void videoTestBtn_Click(object sender, RoutedEventArgs e)
        {

            try
            { // just in case of some invalid directory or whatever


                var ofd = new Microsoft.Win32.OpenFileDialog();
                //ofd.Filter = "Point cloud (.bin)|*.bin";
                string test = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-x86", "native"); // todo make this nicer... cross-platform? oh well wpf is windumb anyway
                ffmpeg.RootPath = test;
                if (ofd.ShowDialog() == true)
                {
                    using (VideoStreamDecoder decoder = new VideoStreamDecoder(ofd.FileName))
                    {
                        using (VideoConverter converter = new VideoConverter(decoder.FrameSize, decoder.PixelFormat, decoder.FrameSize, AVPixelFormat.AV_PIX_FMT_RGB24))
                        {
                            decoder.TryDecodeNextFrame(out AVFrame frame);
                            if (decoder.PixelFormat != AVPixelFormat.AV_PIX_FMT_RGB24)
                            {
                                frame = converter.Convert(frame);
                            }
                            int stride = frame.linesize[0];
                            byte* data = frame.data[0];
                            //byte[] toppixel = frame.getpixel(0, 0);
                            //byte[] pixel = frame.getpixel(0, frame.height - 1);
                            //UIntPtr[] rgbOffsets = new UIntPtr[4] {(UIntPtr)0, (UIntPtr)1, (UIntPtr)2, (UIntPtr)3 };
                            UIntPtr[] rgbOffsets = new UIntPtr[4] {(UIntPtr)0, (UIntPtr)1, (UIntPtr)2, (UIntPtr)3 };

                            byte* json = (byte*)0;
                            fixed (UIntPtr* rgboff = rgbOffsets)
                            {
                                json =(byte*)parseVideoMetaToString((byte*)frame.data[0] + frame.linesize[0] * 720, 1280, 720, 720, frame.linesize[0], 3, rgboff);

                                string jsonStr = charPtrToString(json); 

                                //Debug.WriteLine(pixel);
                                Debug.WriteLine(frame.width);
                            }
                        }
                    }
                }
            }
            catch (Exception e2)
            {
                Helpers.logToFile($"Error doing pointcloud video test (outer): {e2.ToString()}");
            }
        }
    }
}
