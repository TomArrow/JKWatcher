using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JKWatcher;
using JKWatcher.RandomHelpers;
using System.Numerics;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class AngleCoderTest
    {
        [TestMethod]
        public void TestAngleCoderSimple()
        {
            string message = "hello there xy424yRSDHY42";

            Vector2[] angleSequence = AngleEncoder.CreateAngleSequence(Encoding.UTF8.GetBytes(message));

            AngleDecoder decoder = new AngleDecoder();

            byte[] rsult = null;
            for (int i = 0; i < angleSequence.Length; i++)
            {
                Vector2 anglesHere = angleSequence[i];
                rsult = decoder.GiveAngleMaybeReturnResult(anglesHere.X, anglesHere.Y);
                if(rsult != null)
                {
                    break;
                }
            }

            Assert.IsTrue(rsult != null);
            if (rsult != null)
            {
                string decodedString = Encoding.UTF8.GetString(rsult);
                Trace.WriteLine(decodedString);
                Assert.AreEqual(decodedString, message);
            }
        }
        [TestMethod]
        public void TestAngleCoderDuplicates()
        {
            string message = "hello there xy424yRSDHY42";

            Vector2[] angleSequence = AngleEncoder.CreateAngleSequence(Encoding.UTF8.GetBytes(message));

            List<Vector2> jumbledSequence = new List<Vector2>();

            Random rnd = new Random();

            for (int i = 0; i < angleSequence.Length; i++)
            {
                int countHere = rnd.Next(1, 10);
                for(int a = 0; a < countHere; a++)
                {
                    jumbledSequence.Add(angleSequence[i]);
                }
            }

            AngleDecoder decoder = new AngleDecoder();

            byte[] rsult = null;
            for (int i = 0; i < jumbledSequence.Count; i++)
            {
                Vector2 anglesHere = jumbledSequence[i];
                rsult = decoder.GiveAngleMaybeReturnResult(anglesHere.X, anglesHere.Y);
                if(rsult != null)
                {
                    break;
                }
            }

            Assert.IsTrue(rsult != null);
            if (rsult != null)
            {
                string decodedString = Encoding.UTF8.GetString(rsult);
                Trace.WriteLine(decodedString);
                Assert.AreEqual(decodedString, message);
            }
        }
        [TestMethod]
        public void TestAngleCoderJumbled()
        {
            string message = "hello there xy424yRSDHY42";

            Vector2[] angleSequence = AngleEncoder.CreateAngleSequence(Encoding.UTF8.GetBytes(message));

            List<Vector2> jumbledSequence = new List<Vector2>();

            Random rnd = new Random();

            for (int i = 0; i < angleSequence.Length; i++)
            {
                int countHere = rnd.Next(1, 10);
                for(int a = 0; a < countHere; a++)
                {
                    jumbledSequence.Add(angleSequence[i]);
                }
            }
            for (int i = 0; i < jumbledSequence.Count - 1; i++)
            {
                bool doJumble = rnd.NextDouble() < 0.1 &&jumbledSequence[i].X != 45 &&jumbledSequence[i+1].X != 45;
                if (doJumble)
                {
                    Vector2 tmp = jumbledSequence[i];
                    jumbledSequence[i] = jumbledSequence[i + 1];
                    jumbledSequence[i+1] = tmp;
                }
                while(rnd.NextDouble() < 0.3)
                {
                    Vector2 tmp = jumbledSequence[i];
                    tmp.X += 360.0f;
                    jumbledSequence[i] = tmp;
                }
                while (rnd.NextDouble() < 0.3)
                {
                    Vector2 tmp = jumbledSequence[i];
                    tmp.X -= 360.0f;
                    jumbledSequence[i] = tmp;
                }
            }

            AngleDecoder decoder = new AngleDecoder();

            byte[] rsult = null;
            for (int i = 0; i < jumbledSequence.Count; i++)
            {
                Vector2 anglesHere = jumbledSequence[i];
                rsult = decoder.GiveAngleMaybeReturnResult(anglesHere.X, anglesHere.Y);
                if(rsult != null)
                {
                    break;
                }
            }

            Assert.IsTrue(rsult != null);
            if (rsult != null)
            {
                string decodedString = Encoding.UTF8.GetString(rsult);
                Trace.WriteLine(decodedString);
                Assert.AreEqual(decodedString, message);
            }
        }
        [TestMethod]
        public void TestAngleCoderJumbled2()
        {
            int successCount = 0;
            for(int i = 0; i < 100; i++)
            {
                if (jumbled2test())
                {
                    successCount++;
                }
            }
            Trace.WriteLine($"{successCount} out of 100 jumbled successful. 50 required.");
            Assert.IsTrue(successCount > 50);
        }
        private bool jumbled2test()
        {

            string message = "hello there xy424yRSDHY42";

            Vector2[] angleSequence = AngleEncoder.CreateAngleSequence(Encoding.UTF8.GetBytes(message));

            List<Vector2> jumbledSequence = new List<Vector2>();

            Random rnd = new Random();

            for (int i = 0; i < angleSequence.Length; i++)
            {
                int countHere = rnd.Next(1, 10);
                for (int a = 0; a < countHere; a++)
                {
                    jumbledSequence.Add(angleSequence[i]);
                }
                while(rnd.NextDouble() < 0.3)
                {
                    jumbledSequence.Add(new Vector2() { X=(float)rnd.NextDouble()*2000.0f-1000.0f, Y = (float)rnd.NextDouble() * 2000.0f - 1000.0f });
                }
            }
            for (int i = 0; i < jumbledSequence.Count - 1; i++)
            {
                bool doJumble = rnd.NextDouble() < 0.4;
                if (doJumble)
                {
                    Vector2 tmp = jumbledSequence[i];
                    jumbledSequence[i] = jumbledSequence[i + 1];
                    jumbledSequence[i + 1] = tmp;
                }
                while (rnd.NextDouble() < 0.3)
                {
                    Vector2 tmp = jumbledSequence[i];
                    tmp.X += 360.0f;
                    jumbledSequence[i] = tmp;
                }
                while (rnd.NextDouble() < 0.3)
                {
                    Vector2 tmp = jumbledSequence[i];
                    tmp.X -= 360.0f;
                    jumbledSequence[i] = tmp;
                }
            }

            AngleDecoder decoder = new AngleDecoder();

            byte[] rsult = null;
            for (int i = 0; i < jumbledSequence.Count; i++)
            {
                Vector2 anglesHere = jumbledSequence[i];
                rsult = decoder.GiveAngleMaybeReturnResult(anglesHere.X, anglesHere.Y);
                if (rsult != null)
                {
                    break;
                }
            }

            if (rsult != null)
            {
                string decodedString = Encoding.UTF8.GetString(rsult);
                return decodedString== message;
            } else
            {
                return false;
            }
        }
    }
}
