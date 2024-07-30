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

namespace Tests
{
    [TestClass]
    public class ProjectionMatrixTests
    {

        private float above1To2SoftApproach(float value) // the result of this will approach 2 but never reach it. perfection
        {
            return (1.0f - 1.0f / (1f + (float)Math.Pow(value, 0.833333333333333333f))) * 2.0f; // dont ask me why exactly 0.83333. i just aligned the two derivatives in le graphing calculator :)
        }

        private const float invGamma = 1f / 2.4f;
        private const float invGamma5 = 1f / 5f;
        private const float invGamma10 = 1f / 10f;

        [TestMethod]
        public void TestFalloff()
        {
            //int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
            //int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
            // posx = ((modelposx + 1)/2)*levelshotwidth
            // posx/levelshotwidth = (modelposx+1)/2
            // 2*(posx+0.25)/levelshotwidth-1 = modelposx


            Matrix4x4 m = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 140);
            //Matrix4x4 mInvert = new Matrix4x4();
            Assert.IsTrue(Matrix4x4.Invert(m,out Matrix4x4 mInvert));
            float[,] compensationImage = new float[1920, 1080];
            float minMultiplier = float.PositiveInfinity;
            float maxMultiplier = float.NegativeInfinity;
            for (int x = 0; x < 1920; x++)
            {
                for (int y = 0; y < 1080; y++)
                {
                    float xProjected = 2.0f * ((float)x + 0.25f) / (float)1920.0f - 1.0f;
                    float yProjected = 2.0f * ((float)y + 0.25f) / (float)1080.0f - 1.0f;
                    Vector4 projectedPoint = new Vector4(xProjected, yProjected, 1.0f, 1.0f);
                    Vector4 modelPoint = Vector4.Transform(projectedPoint, mInvert);
                    float multiplier = ProjectionMatrixHelper.GetIlluminationMultiplierPureNoZ(new Vector3(-modelPoint.Z,modelPoint.X, modelPoint.Y));
                    compensationImage[x, y] = multiplier;
                    minMultiplier = Math.Min(minMultiplier,multiplier);
                    maxMultiplier = Math.Max(maxMultiplier, multiplier);
                }
            }
            Trace.WriteLine($"Range: {minMultiplier} to {maxMultiplier}");


            Bitmap bmp = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            ByteImage bi = Helpers.BitmapToByteArray(bmp);
            bmp.Dispose();

            for (int x = 0; x < 1920; x++)
            {
                for (int y = 0; y < 1080; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = compensationImage[x, y] * 0.01f;
                        //float gammaValue = valueHere > 1.0 ? (float)Math.Pow(valueHere * multiplier, invGamma5) : (float)Math.Pow(valueHere * multiplier, invGamma);
                        float gammaValue = valueHere > 1.0 ? above1To2SoftApproach(valueHere) : (float)Math.Pow(valueHere, invGamma);
                        byte byteValue = (byte)Math.Clamp(gammaValue * 255.0f * 0.5f, 0, 255.0f);
                        int yInv = 1080 - 1 - y;
                        int xInv = 1920 - 1 - x;
                        bi.imageData[bi.stride * yInv + xInv * 3 + c] = byteValue;
                    }
                }
            }
            bmp = Helpers.ByteArrayToBitmap(bi);
            bmp.Save("TestFalloff.png");
            bmp.Dispose();

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestFalloff2()
        {
            //int posY = (int)(((levelshotPos.Y + 1.0f) / 2.0f) * (float)LevelShotData.levelShotHeight);
            //int posX = (int)(((levelshotPos.X + 1.0f) / 2.0f) * (float)LevelShotData.levelShotWidth);
            // posx = ((modelposx + 1)/2)*levelshotwidth
            // posx/levelshotwidth = (modelposx+1)/2
            // 2*(posx+0.25)/levelshotwidth-1 = modelposx


            Matrix4x4 m = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 140);
            //Matrix4x4 mInvert = new Matrix4x4();
            Assert.IsTrue(Matrix4x4.Invert(m,out Matrix4x4 mInvert));
            float[,] compensationImage = new float[1920, 1080];
            float minMultiplier = float.PositiveInfinity;
            float maxMultiplier = float.NegativeInfinity;
            for (int x = 0; x < 1920; x++)
            {
                for (int y = 0; y < 1080; y++)
                {
                    float xProjected = 2.0f * ((float)x + 0.25f) / (float)1920.0f - 1.0f;
                    float yProjected = 2.0f * ((float)y + 0.25f) / (float)1080.0f - 1.0f;
                    Vector4 projectedPoint = new Vector4(xProjected, yProjected, 1.0f, 1.0f);
                    Vector4 modelPoint = Vector4.Transform(projectedPoint, mInvert);
                    float multiplier = ProjectionMatrixHelper.GetIlluminationMultiplier2(new Vector3(-modelPoint.Z,modelPoint.X, modelPoint.Y));
                    compensationImage[x, y] = multiplier;
                    minMultiplier = Math.Min(minMultiplier,multiplier);
                    maxMultiplier = Math.Max(maxMultiplier, multiplier);
                }
            }
            Trace.WriteLine($"Range: {minMultiplier} to {maxMultiplier}");


            Bitmap bmp = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            ByteImage bi = Helpers.BitmapToByteArray(bmp);
            bmp.Dispose();

            for (int x = 0; x < 1920; x++)
            {
                for (int y = 0; y < 1080; y++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float valueHere = compensationImage[x, y] * 0.01f;
                        //float gammaValue = valueHere > 1.0 ? (float)Math.Pow(valueHere * multiplier, invGamma5) : (float)Math.Pow(valueHere * multiplier, invGamma);
                        float gammaValue = valueHere > 1.0 ? above1To2SoftApproach(valueHere) : (float)Math.Pow(valueHere, invGamma);
                        byte byteValue = (byte)Math.Clamp(gammaValue * 255.0f * 0.5f, 0, 255.0f);
                        int yInv = 1080 - 1 - y;
                        int xInv = 1920 - 1 - x;
                        bi.imageData[bi.stride * yInv + xInv * 3 + c] = byteValue;
                    }
                }
            }
            bmp = Helpers.ByteArrayToBitmap(bi);
            bmp.Save("TestFalloff2.png");
            bmp.Dispose();

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrix()
        {
            Matrix4x4 m = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120);
            Trace.WriteLine($"{m.ToString()}");
            m = Matrix4x4.Transpose(m);
            Trace.WriteLine($"{m.ToString()}");
            Vector3 v1 = new Vector3() { X=2,Y=0,Z=5 };
            Vector3 v2 = new Vector3() { X=2,Y=0,Z=10 };
            Vector3 v1t = Vector3.Transform(v1, m);
            Vector3 v2t = Vector3.Transform(v2, m);
            Trace.WriteLine($"{(v1t).ToString()}");
            Trace.WriteLine($"{(v2t).ToString()}");

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrix2()
        {
            Matrix4x4 m1 = ProjectionMatrixHelper.createModelMatrix(new Vector3() { X=-100},new Vector3() { Y=0},true);
            Matrix4x4 m2 = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120);
            Trace.WriteLine($"{m1.ToString()}");
            Trace.WriteLine($"{m2.ToString()}");
            //m1 = Matrix4x4.Transpose(m1);
            //m2 = Matrix4x4.Transpose(m2);
            //Trace.WriteLine($"{m.ToString()}");
            Vector4 v1 = new Vector4() { X=5,Y=2,Z=1,W=1 };
            Vector4 v2 = new Vector4() { X=100,Y=2,Z=1,W=1 };
            v1 = Vector4.Transform(v1, m1);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 = Vector4.Transform(v1, m2);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 /= v1.W;
            Trace.WriteLine($"{(v1).ToString()}");
            v2 = Vector4.Transform(v2, m1);
            Trace.WriteLine($"{(v2).ToString()}");
            v2 = Vector4.Transform(v2, m2);
            Trace.WriteLine($"{(v2).ToString()}");
            v2 /= v2.W;
            Trace.WriteLine($"{(v2).ToString()}");

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrix3()
        {
            Matrix4x4 m1 = ProjectionMatrixHelper.createModelMatrix(new Vector3() { X=-100},new Vector3() { Y=0}, true);
            Matrix4x4 m2 = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120);
            Trace.WriteLine($"{m1.ToString()}");
            Trace.WriteLine($"{m2.ToString()}");
            //m1 = Matrix4x4.Transpose(m1);
            //m2 = Matrix4x4.Transpose(m2);
            Matrix4x4 m3 = Matrix4x4.Multiply(m1, m2);
            //Trace.WriteLine($"{m.ToString()}");
            Vector4 v1 = new Vector4() { X=5,Y=2,Z=1,W=1 };
            Vector4 v2 = new Vector4() { X=100,Y=2,Z=1,W=1 };
            v1 = Vector4.Transform(v1, m3);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 /= v1.W;
            Trace.WriteLine($"{(v1).ToString()}");
            v2 = Vector4.Transform(v2, m3);
            Trace.WriteLine($"{(v2).ToString()}");
            v2 /= v2.W;
            Trace.WriteLine($"{(v2).ToString()}");

            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrixYavinCamTarget()
        {
            Matrix4x4 m1 = ProjectionMatrixHelper.createModelMatrix(new Vector3() { X = 696.0f, Y = 2132, Z = 488 }, new Vector3() { X = 13.2550048828125f, Y = -163.970947265625f }, true);
            Matrix4x4 m2 = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120);
            Trace.WriteLine($"{m1.ToString()}");
            Trace.WriteLine($"{m2.ToString()}");
            //m1 = Matrix4x4.Transpose(m1);
            //m2 = Matrix4x4.Transpose(m2);
            Matrix4x4 m3 = Matrix4x4.Multiply(m1, m2);
            //Trace.WriteLine($"{m.ToString()}");
            Vector4 v1 = new Vector4() { X = -724, Y = 1724, Z = 140, W = 1 };
            v1 = Vector4.Transform(v1, m3);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 /= v1.W;
            Trace.WriteLine($"{(v1).ToString()}");

            Assert.IsTrue(v1.X < 0.01f);
            Assert.IsTrue(v1.X > -0.01f);
            Assert.IsTrue(v1.Y < 0.01f);
            Assert.IsTrue(v1.Y > -0.01f);
            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrixYavinCamTargetTooFar()
        {
            Matrix4x4 m1 = ProjectionMatrixHelper.createModelMatrix(new Vector3() { X = 696.0f, Y = 2132, Z = 488 }, new Vector3() { X = 13.2550048828125f, Y = -163.970947265625f }, true);
            Matrix4x4 m2 = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120, 100);
            Trace.WriteLine($"{m1.ToString()}");
            Trace.WriteLine($"{m2.ToString()}");
            //m1 = Matrix4x4.Transpose(m1);
            //m2 = Matrix4x4.Transpose(m2);
            Matrix4x4 m3 = Matrix4x4.Multiply(m1, m2);
            //Trace.WriteLine($"{m.ToString()}");
            Vector4 v1 = new Vector4() { X = -724, Y = 1724, Z = 140, W = 1 };
            v1 = Vector4.Transform(v1, m3);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 /= v1.W;
            Trace.WriteLine($"{(v1).ToString()}");

            Assert.IsTrue(v1.X < 0.01f);
            Assert.IsTrue(v1.X > -0.01f);
            Assert.IsTrue(v1.Y < 0.01f);
            Assert.IsTrue(v1.Y > -0.01f);
            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestProjectionMatrixYavinCamPositionBehind()
        {
            Matrix4x4 m1 = ProjectionMatrixHelper.createModelMatrix(new Vector3() { X = 696.0f, Y = 2132, Z = 488 }, new Vector3() { X = 13.2550048828125f, Y = -163.970947265625f }, true);
            Matrix4x4 m2 = ProjectionMatrixHelper.createProjectionMatrix(1920, 1080, 120);
            Trace.WriteLine($"{m1.ToString()}");
            Trace.WriteLine($"{m2.ToString()}");
            //m1 = Matrix4x4.Transpose(m1);
            //m2 = Matrix4x4.Transpose(m2);
            Matrix4x4 m3 = Matrix4x4.Multiply(m1, m2);
            //Trace.WriteLine($"{m.ToString()}");
            Vector4 v1 = new Vector4() { X = 924.0f, Y = 2190, Z = 544, W =1 };
            v1 = Vector4.Transform(v1, m3);
            Trace.WriteLine($"{(v1).ToString()}");
            v1 /= v1.W;
            Trace.WriteLine($"{(v1).ToString()}");

            Assert.IsTrue(v1.X < 0.1f);
            Assert.IsTrue(v1.X > -0.1f);
            Assert.IsTrue(v1.Y < 0.1f);
            Assert.IsTrue(v1.Y > -0.1f);
            Assert.IsTrue(true);
        }
    }
}
