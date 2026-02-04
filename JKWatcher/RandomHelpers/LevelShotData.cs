using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct LevelShotAccumType : IEquatable<LevelShotAccumType>
    {
        public bool isRealValue;
        public int zCompensationVersion;
        public Vector3 pos;
        public Vector3 angles;

        public override int GetHashCode()
        {
            return HashCode.Combine(this.zCompensationVersion.GetHashCode(),this.pos.GetHashCode(),this.angles.GetHashCode(),this.isRealValue.GetHashCode());
        }
        public bool Equals(LevelShotAccumType other)
        {
            return zCompensationVersion == other.zCompensationVersion && pos == other.pos && angles == other.angles && isRealValue == other.isRealValue;
        }
        public override bool Equals(object obj)
        {
            return obj is LevelShotAccumType && Equals((LevelShotAccumType)obj);
        }
        public static bool operator == (LevelShotAccumType me,LevelShotAccumType other)
        {
            return me.Equals(other);
        }
        public static bool operator != (LevelShotAccumType me,LevelShotAccumType other)
        {
            return !me.Equals(other);
        }
        public string GetIdentifierString(bool withZ)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                using(BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(pos.X);
                    bw.Write(pos.Y);
                    bw.Write(pos.Z);
                    bw.Write(angles.X);
                    bw.Write(angles.Y);
                    bw.Write(angles.Z);
                }

                using (SHA512 sha512 = new SHA512Managed())
                {
                    string hashString = BitConverter.ToString(sha512.ComputeHash(ms.ToArray())).Replace("-", "");
                    if (withZ)
                    {
                        hashString = $"z{zCompensationVersion}_{hashString}";
                    }
                    else
                    {
                        hashString = $"noZ_{hashString}";
                    }
                    return hashString.Length > 12 ? hashString.Substring(0, 12) : hashString;
                }
            }
        }

    }

    public static class LevelShotDataHelpers {
        public static float GetIndex(this Vector3 me, int index)
        {
            switch (index) {
                case 0:
                    return me.X;
                case 1:
                    return me.Y;
                case 2:
                    return me.Z;
            }
            throw new Exception($"Invalid Index requested from Vector3: {index}");
        }
    }


    public class LevelShotData
    {
        public const float levelShotFov = 140;
        public const int levelShotWidth = 1920;
        public const int levelShotHeight = 1080;
        public Vector3[,] data = new Vector3[levelShotWidth, levelShotHeight];

        public object lastSavedAndAccumTypeLock = new object();

        public DateTime lastSaved = DateTime.Now;
        public UInt64 changesSinceLastSaved = 0;
        public DateTime lastSavedAccum = DateTime.Now;
        public UInt64 changesSinceLastSavedAccum = 0;

        public LevelShotAccumType accumType = new LevelShotAccumType() { zCompensationVersion = ProjectionMatrixHelper.ZCompensationVersion };
        public DateTime lastReferenceAccumTypeReset = DateTime.Now - new TimeSpan(100,0,0);
        public DateTime lastRealAccumTypeReset = DateTime.Now - new TimeSpan(100,0,0);
        public int accumTypeResetsSinceLastReset = 0;
        public string mapname = null;
        // this sounds very strange. basically, first reset we log the time. then we allow up to 5 new resets in whatever timespan, and then we lock it down.
        // that way we can have a lot of changes when needed during start/ level change but if different connections conflict we won't get endless loops of allocation.


        public static float[,] compensationMultipliers = null;
        static LevelShotData()
        {
            CreateCompensationMultipliers();
        }


        public LevelShotData MakeSoftCopy()
        {
            LevelShotData lsNew = new LevelShotData();
            lsNew.data = this.data;
            lock (this.lastSavedAndAccumTypeLock)
            {
                lsNew.changesSinceLastSaved = this.changesSinceLastSaved;
                lsNew.lastSaved = this.lastSaved;
                lsNew.accumType = this.accumType;
                lsNew.mapname = this.mapname;
            }
            return lsNew;
        }
        // This is only really used for the z-compensated one that keeps getting combined. Idk if it will work. I hope. Really dirty tho.
        // Basically we really must make sure that the camera angles and z compensation algorithm haven't changed, otherwise combining makes little sense
        public bool IsAccumTypeOkayMaybeReset(in LevelShotAccumType accumTypeNew, string currentMapName, out LevelShotData preResetLevelShot)
        {
            preResetLevelShot = null;
            lock (lastSavedAndAccumTypeLock)
            {
                if(string.IsNullOrWhiteSpace(currentMapName))
                {
                    return false;
                }
                else if(accumTypeNew == accumType)
                {
                    return true;
                }
                else if(!accumType.isRealValue || string.IsNullOrWhiteSpace(this.mapname))
                {
                    mapname = currentMapName;
                    accumType = accumTypeNew;
                    //preResetLevelShot = this.MakeSoftCopy(); // no need since it was useless anyway
                    accumTypeResetsSinceLastReset = 0;
                    Reset();
                    return true;
                }
                else if((DateTime.Now- lastReferenceAccumTypeReset).TotalSeconds > 120 || !currentMapName.Equals(this.mapname,StringComparison.InvariantCultureIgnoreCase))
                {
                    mapname = currentMapName;
                    accumType = accumTypeNew;
                    accumTypeResetsSinceLastReset = 0;
                    lastReferenceAccumTypeReset = DateTime.Now;
                    preResetLevelShot = this.MakeSoftCopy();
                    Reset();
                    return true;
                }
                else if(accumTypeResetsSinceLastReset < 5 && (DateTime.Now - lastRealAccumTypeReset).TotalSeconds > 5)
                {
                    mapname = currentMapName;
                    accumTypeResetsSinceLastReset++;
                    accumType = accumTypeNew;
                    lastRealAccumTypeReset = DateTime.Now;
                    preResetLevelShot = this.MakeSoftCopy();
                    Reset();
                    return true;
                }
                else
                {
                    return false; // keep old
                }
            }
        }
        public void Reset()
        {
            lock (lastSavedAndAccumTypeLock)
            {
                data = new Vector3[levelShotWidth, levelShotHeight];
                changesSinceLastSaved = 0;
            }
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

        public static void SumData(Vector3[,] main, Vector3[,] add)
        {
            for (int x = 0; x < levelShotWidth; x++)
            {
                for (int y = 0; y < levelShotHeight; y++)
                {
                    main[x, y] += add[x, y];
                    //main[x, y, 0] += add[x, y, 0];
                    //main[x, y, 1] += add[x, y, 1];
                    //main[x, y, 2] += add[x, y, 2];
                }
            }
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
                                        retVal.data[x, y].X = br.ReadSingle();
                                        retVal.data[x, y].Y = br.ReadSingle();
                                        retVal.data[x, y].Z = br.ReadSingle();
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

        public byte[] createTiffImage(Predictor predictor)
        {
            return createTiffImage(this.data, predictor);
        }
        public byte[] createTiffImage()
        {
            return createTiffImage(this.data, null);
        }
        public static byte[] createTiffImage(Vector3[,] imageData, Predictor? predictor = null)
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
                    if (predictor.HasValue)
                    {
                        tiff.SetField(TiffTag.PREDICTOR, predictor.Value);
                    }

                    for (int y = 0; y < levelShotHeight; y++)
                    {
                        using (MemoryStream ms2 = new MemoryStream(1920 * 4 * 3))
                        {
                            using (BinaryWriter bw = new BinaryWriter(ms2))
                            {
                                for (int x = 0; x < levelShotWidth; x++)
                                {
                                    bw.Write(imageData[x, y].X);
                                    bw.Write(imageData[x, y].Y);
                                    bw.Write(imageData[x, y].Z);
                                }
                            }
                            tiff.WriteScanline(ms2.ToArray(), y);
                        }
                    }
                }
                return ms.ToArray();
            }
        }




        private const float invGamma = 1f / 2.4f;
        private const float invGamma5 = 1f / 5f;
        private const float invGamma10 = 1f / 10f;
        private static float above1To2SoftApproach(float value) // the result of this will approach 2 but never reach it. perfection
        {
            return (1.0f - 1.0f / (1f + (float)Math.Pow(value, 0.833333333333333333f))) * 2.0f; // dont ask me why exactly 0.83333. i just aligned the two derivatives in le graphing calculator :)
        }

        public static Bitmap ToBitmap(Vector3[,] levelshotDataLocal, uint skipLessThanPixelCount)
        {
            int width = levelshotDataLocal.GetLength(0);
            int height = levelshotDataLocal.GetLength(1);

            List<float> brightnessValuesList = new List<float>();

            //double totalUsedPixelBrightness = 0;
            double divider = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = levelshotDataLocal[x, y].GetIndex(c);
                        if (valueHere > 0.0f)
                        {
                            //totalUsedPixelBrightness += valueHere;
                            divider++;
                            brightnessValuesList.Add(valueHere);
                        }
                    }
                }
            }

            if (divider < (double)skipLessThanPixelCount) return null;

            //double averageBrightness = totalUsedPixelBrightness / divider;
            //float multiplier = 1.0f/ (float)averageBrightness;
            brightnessValuesList.Sort();
            float roughMedianBrightness = brightnessValuesList.Count > 0 ? brightnessValuesList[brightnessValuesList.Count / 2] : 1.0f;
            float multiplier = 1.0f / (float)roughMedianBrightness;

            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            ByteImage bi = Helpers.BitmapToByteArray(bmp);
            bmp.Dispose();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = levelshotDataLocal[x, y].GetIndex(c) * multiplier;
                        //float gammaValue = valueHere > 1.0 ? (float)Math.Pow(valueHere * multiplier, invGamma5) : (float)Math.Pow(valueHere * multiplier, invGamma);
                        float gammaValue = valueHere > 1.0 ? above1To2SoftApproach(valueHere) : (float)Math.Pow(valueHere, invGamma);
                        byte byteValue = (byte)Math.Clamp(gammaValue * 255.0f * 0.5f, 0, 255.0f);
                        int yInv = height - 1 - y;
                        int xInv = width - 1 - x;
                        bi.imageData[bi.stride * yInv + xInv * 3 + c] = byteValue;
                    }
                }
            }
            bmp = Helpers.ByteArrayToBitmap(bi);
            return bmp;
        }



    }
}
