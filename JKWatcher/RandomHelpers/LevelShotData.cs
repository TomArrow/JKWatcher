using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    public class LevelShotData
    {
        public const float levelShotFov = 140;
        public const int levelShotWidth = 1920;
        public const int levelShotHeight = 1080;
        public float[,,] data = new float[levelShotWidth, levelShotHeight, 3];
        public DateTime lastSaved = DateTime.Now;
        public object lastSavedLock = new object();
        public UInt64 changesSinceLastSaved = 0;

        public static float[,] compensationMultipliers = null;
        static LevelShotData()
        {
            CreateCompensationMultipliers();
        }
        private static void CreateCompensationMultipliers()
        {
            Matrix4x4 m = ProjectionMatrixHelper.createProjectionMatrix(levelShotWidth, levelShotHeight, levelShotFov);
            Matrix4x4.Invert(m, out Matrix4x4 mInvert);
            compensationMultipliers = new float[levelShotWidth, levelShotHeight];
            float minMultiplier = float.PositiveInfinity;
            float maxMultiplier = float.NegativeInfinity;
            for (int x = 0; x < levelShotWidth; x++)
            {
                for (int y = 0; y < levelShotHeight; y++)
                {
                    float xProjected = 2.0f * ((float)x + 0.25f) / (float)levelShotWidth - 1.0f;
                    float yProjected = 2.0f * ((float)y + 0.25f) / (float)levelShotHeight - 1.0f;
                    Vector4 projectedPoint = new Vector4(xProjected, yProjected, 1.0f, 1.0f);
                    Vector4 modelPoint = Vector4.Transform(projectedPoint, mInvert);
                    float multiplier = ProjectionMatrixHelper.GetIlluminationMultiplierPureNoZ(new Vector3(-modelPoint.Z, modelPoint.X, modelPoint.Y));
                    compensationMultipliers[x, y] = multiplier;
                    minMultiplier = Math.Min(minMultiplier, multiplier);
                    maxMultiplier = Math.Max(maxMultiplier, multiplier);
                }
            }
            Debug.WriteLine($"Levelshot compensation multipliers calculated. Value range from {minMultiplier} to {maxMultiplier}");
        }

        public static LevelShotData FromTiff(byte[] tiffData)
        {
            try { 
                using (MemoryStream ms = new MemoryStream(tiffData))
                {
                    using (Tiff tiff = Tiff.ClientOpen("blahblah", "r", ms, new TiffStream()))
                    {
                        FieldValue[] field = tiff.GetFieldDefaulted(TiffTag.IMAGEWIDTH);
                        if(field[0].ToInt() != levelShotWidth)
                        {
                            return null;
                        }
                        field = tiff.GetFieldDefaulted(TiffTag.IMAGELENGTH);
                        if(field[0].ToInt() != levelShotHeight)
                        {
                            return null;
                        }
                        field = tiff.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL);
                        if(field[0].ToInt() != 3)
                        {
                            return null;
                        }
                        field = tiff.GetFieldDefaulted(TiffTag.BITSPERSAMPLE);
                        if(field[0].ToInt() != 32)
                        {
                            return null;
                        }
                        field = tiff.GetFieldDefaulted(TiffTag.SAMPLEFORMAT);
                        if(field[0].ToInt() != 3)
                        {
                            return null;
                        }
                        field = tiff.GetFieldDefaulted(TiffTag.ROWSPERSTRIP);
                        if(field[0].ToInt() != levelShotHeight)
                        {
                            return null;
                        }
                        
                        LevelShotData retVal = new LevelShotData();


                        byte[] rowBuffer = new byte[1920 * 4 * 3];
                        for (int y = 0; y < levelShotHeight; y++)
                        {
                            tiff.ReadScanline(rowBuffer, y);
                            using (MemoryStream ms2 = new MemoryStream(rowBuffer))
                            {
                                using (BinaryReader br = new BinaryReader(ms2))
                                {
                                    for (int x = 0; x < levelShotWidth; x++)
                                    {
                                        retVal.data[x, y, 0] = br.ReadSingle();
                                        retVal.data[x, y, 1] = br.ReadSingle();
                                        retVal.data[x, y, 2] = br.ReadSingle();
                                    }
                                }
                            }
                        }
                        return retVal;
                    }
                }
            } catch(Exception e)
            {
                Helpers.logToFile(e.ToString());
                return null;
            }
        }

        public byte[] createTiffImage()
        {
            return createTiffImage(this.data);
        }
        public static byte[] createTiffImage(float[,,] imageData)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Tiff tiff = Tiff.ClientOpen("blah", "w", ms, new TiffStream()))
                {
                    tiff.SetField(TiffTag.IMAGEWIDTH, levelShotWidth);
                    tiff.SetField(TiffTag.IMAGELENGTH, levelShotHeight);
                    tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
                    tiff.SetField(TiffTag.BITSPERSAMPLE, 32);
                    tiff.SetField(TiffTag.SAMPLEFORMAT, 3);
                    tiff.SetField(TiffTag.ORIENTATION, Orientation.BOTRIGHT);
                    tiff.SetField(TiffTag.ROWSPERSTRIP, levelShotHeight);
                    tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                    tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                    tiff.SetField(TiffTag.COMPRESSION, Compression.DEFLATE);
                    tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);

                    for (int y = 0; y < levelShotHeight; y++)
                    {
                        using (MemoryStream ms2 = new MemoryStream(1920 * 4 * 3))
                        {
                            using (BinaryWriter bw = new BinaryWriter(ms2))
                            {
                                for (int x = 0; x < levelShotWidth; x++)
                                {
                                    bw.Write(imageData[x, y, 0]);
                                    bw.Write(imageData[x, y, 1]);
                                    bw.Write(imageData[x, y, 2]);
                                }
                            }
                            tiff.WriteScanline(ms2.ToArray(), y);
                        }
                    }
                }
                return ms.ToArray();
            }
        }
    }
}
