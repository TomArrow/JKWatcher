using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JKWatcher;
using System.Numerics;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class ProjectionMatrixTests
    {
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
