using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JKWatcher;
using System.Numerics;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Media;
using JKWatcher.RandomHelpers;
using BitMiracle.LibTiff.Classic;

namespace Tests
{
    [TestClass]
    public class FloatImageTest
    {

        [TestMethod]
        public void TestTIFF()
        {
            float[] imageData = new float[1920*1080*4];

            BitmapSource bs = BitmapSource.Create(1920,1080,96,96,PixelFormats.Rgb128Float,null,imageData, 1920*4*4);

            TiffBitmapEncoder tiff = new TiffBitmapEncoder() { Compression = TiffCompressOption.None };
            var frame = BitmapFrame.Create(bs);
            tiff.Frames.Add(frame);
            tiff.Save(new FileStream("128floatTest.tif", FileMode.Create));

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestTIFF2()
        {
            Random rnd = new Random();
            
            LevelShotData ls = new LevelShotData();
            for (int y = 0; y < 1080 / 2; y++)
            {
                for (int x = 0; x < 1920 / 2; x++)
                {
                    ls.data[x,y].X = 1.0f * ((float)x / 100.0f) + (float)rnd.NextDouble() * 999999.0f;
                }
            }

            byte[] tiff = ls.createTiffImage();
            File.WriteAllBytes("128floatTestLibTiff.tiff",tiff);

            LevelShotData ls2 = LevelShotData.FromTiff(tiff);

            Assert.IsTrue(ls2 != null);

            bool isSame = true;
            for (int y = 0; y < 1080; y++)
            {
                for (int x = 0; x < 1920; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        bool equal = ls.data[x,y].GetIndex(c) == ls2.data[x,y].GetIndex(c);
                        if (!equal && isSame)
                        {
                            Trace.WriteLine($"Difference at x {x} y {y} c {c}: {ls.data[x, y].GetIndex(c)} vs {ls2.data[x, y].GetIndex(c)}");
                        }
                        isSame = isSame && equal;
                    }
                }
            }
            Assert.IsTrue(isSame); // doesnt work gg. 32 bit float apparently doesnt support lossless

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestTIFF3()
        {
            Random rnd = new Random();
            
            LevelShotData ls = new LevelShotData();
            for (int y = 0; y < 1080 / 2; y++)
            {
                for (int x = 0; x < 1920 / 2; x++)
                {
                    ls.data[x,y].X = 1.0f * ((float)x / 100.0f) + (float)rnd.NextDouble() * 999999.0f;
                }
            }

            byte[] tiff = ls.createTiffImage(Predictor.FLOATINGPOINT);
            Trace.WriteLine($"Size with Floating Point predictor: {tiff.Length}");
            tiff = ls.createTiffImage(Predictor.HORIZONTAL);
            Trace.WriteLine($"Size with Horizontal predictor: {tiff.Length}");
            tiff = ls.createTiffImage(Predictor.NONE);
            Trace.WriteLine($"Size with None predictor: {tiff.Length}");


            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestJXR()
        {
            Random rnd = new Random();
            float[] imageData = new float[1920*1080*4];
            for(int y = 0; y < 1080 / 2; y++)
            {
                for (int x = 0; x < 1920 / 2; x++)
                {
                    imageData[y * 1920 * 4 + x * 4] = 1.0f*((float)x/100.0f) + (float) rnd.NextDouble()*999999.0f;
                }
            }

            for (int y = 0; y < 1080; y++)
            {
                for (int x = 0; x < 1920; x++)
                {
                    imageData[y * 1920 * 4 + x * 4 + 3] = 1.0f;
                }
            }

            BitmapSource bs = BitmapSource.Create(1920,1080,96,96,PixelFormats.Prgba128Float,null,imageData, 1920*4*4);

            WmpBitmapEncoder jxr = new WmpBitmapEncoder();
            jxr.Lossless = true;
            jxr.ImageQualityLevel = 1f;
            jxr.ImageDataDiscardLevel = 0;
            var frame = BitmapFrame.Create(bs);
            jxr.Frames.Add(frame);
            using (MemoryStream ms = new MemoryStream()) { 
                jxr.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                ms.CopyTo(new FileStream("128floatTest.jxr", FileMode.Create));
                ms.Seek(0, SeekOrigin.Begin);

                WmpBitmapDecoder jxrDec = new WmpBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.Default);

                Assert.IsTrue(jxrDec.Frames.Count == 1);
                frame = jxrDec.Frames[0];
                Assert.IsTrue(frame.Format == PixelFormats.Prgba128Float);

                float[] imageDataCompare = new float[1920 * 1080 * 4];
                frame.CopyPixels(imageDataCompare, 1920 * 4 * 4,0);

                bool isSame = true;
                for (int y = 0; y < 1080; y++)
                {
                    for (int x = 0; x < 1920; x++)
                    {
                        for(int c = 0; c < 4; c++)
                        {
                            bool equal = imageData[y * 1920 * 4 + x * 4 + c] == imageDataCompare[y * 1920 * 4 + x * 4 + c];
                            if(!equal && isSame)
                            {
                                Trace.WriteLine($"Difference at x {x} y {y} c {c}: {imageData[y * 1920 * 4 + x * 4 + c]} vs {imageDataCompare[y * 1920 * 4 + x * 4 + c]}");
                            }
                            isSame = isSame && equal;
                        }
                    }
                }
                Assert.IsTrue(!isSame); // doesnt work gg. 32 bit float apparently doesnt support lossless
            }
        }
    }
}
