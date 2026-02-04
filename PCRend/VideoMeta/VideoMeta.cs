
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCRend.VideoMeta {


    public class VideoMeta
    {
        public Invalues inValues { get; set; }
        public int[] version { get; set; }
        public Camera camera { get; set; }
        public int psClientNum { get; set; }
        public Playermeta[] playerMeta { get; set; }
        public Consoleline[] consoleLines { get; set; }
        public Centerprint centerPrint { get; set; }
        public ConsolelineSimple[] getSimpleConsoleLines(int timelimit, bool convertToSRGB)
        {
            List<ConsolelineSimple> newLines = new List<ConsolelineSimple>();
            if(consoleLines != null)
            {
                foreach(Consoleline line in consoleLines)
                {
                    if(line.ageMilliseconds <= timelimit)
                    {
                        newLines.Add(line.getSimpleLine(convertToSRGB));
                    }
                }
            }
            return newLines.ToArray();
        }
        public bool versionAtLeast(int a,int b, int c, int d)
        {
            return ((((a)) < ((version))[(0)] || (((a)) == ((version))[(0)] && ((((b)) < ((version))[(1)] || (((b)) == ((version))[(1)] && ((((c)) < ((version))[(2)] || (((c)) == ((version))[(2)] && (((d) <= (version)[3])))))))))));
        }
    }

    public class Invalues
    {
        public int width { get; set; }
        public int height { get; set; }
        public int totalHeight { get; set; }
        public int stride { get; set; }
        public int multiplier { get; set; }
        public int[] rgbOffsets { get; set; }
    }

    public class Camera
    {
        public float[] pos { get; set; }
        public float[] ang { get; set; }
        public float[][] viewAxes { get; set; }
        public int blendFrames { get; set; }
        public float fov { get; set; }
        public int fisheyeMode { get; set; }
        public float fishEyeNormalBlend { get; set; }
    }

    public class Centerprint : IEquatable<Centerprint>
    {
        public string plaintext { get; set; }
        public Letter[] letters { get; set; }

        bool IEquatable<Centerprint>.Equals(Centerprint other)
        {
            return (other != null || this == null) && plaintext == other.plaintext && letters.SequenceEqualSafe(other.letters);
        }
        public bool Equals(Centerprint other)
        {
            return (this as IEquatable<Centerprint>).Equals(other);
        }
        public Centerprint getColorCorrectedCenterPrint(bool convertToSRGB)
        {
            if (convertToSRGB)
            {
                List<Letter> converted = new List<Letter>();
                foreach (Letter letter in letters)
                {
                    converted.Add(new Letter()
                    {
                        letter = letter.letter,
                        color = new float[] { letter.color[0].linearToSRGB(), letter.color[1].linearToSRGB(), letter.color[2].linearToSRGB(), letter.color[3] },
                        bgColor = letter.bgColor == null ? null : new float[] { letter.bgColor[0].linearToSRGB(), letter.bgColor[1].linearToSRGB(), letter.bgColor[2].linearToSRGB(), letter.bgColor[3] },
                    });
                }
                return new Centerprint() { letters = converted.ToArray(), plaintext = plaintext };
            }
            else
            {
                return new Centerprint() { letters = letters, plaintext = plaintext };
            }
        }
    }

    public class Playermeta
    {
        public float[] light { get; set; }
        public float[] lightDirect { get; set; }
        public float[] lightDir { get; set; }
        public float[] pos { get; set; }
        public float[] headPos { get; set; }
        public float[] vel { get; set; }
        public float[] ang { get; set; }
    }

    public class Consoleline : IEquatable<Consoleline>
    {
        public int ageMilliseconds { get; set; }
        public string plaintext { get; set; }
        public Letter[] letters { get; set; }
        public ConsolelineSimple getSimpleLine(bool convertToSRGB)
        {
            if (convertToSRGB)
            {
                List<Letter> converted = new List<Letter>();
                foreach(Letter letter in letters)
                {
                    converted.Add(new Letter() { 
                        letter = letter.letter,
                        color = new float[] { letter.color[0].linearToSRGB(), letter.color[1].linearToSRGB(), letter.color[2].linearToSRGB(), letter.color[3] },
                        bgColor = letter.bgColor == null ? null : new float[] { letter.bgColor[0].linearToSRGB(), letter.bgColor[1].linearToSRGB(), letter.bgColor[2].linearToSRGB(), letter.bgColor[3] },
                    });
                }
                return new ConsolelineSimple() { letters = converted.ToArray(), plaintext = plaintext };
            }
            else
            {
                return new ConsolelineSimple() { letters = letters, plaintext = plaintext };
            }
        }
        bool IEquatable<Consoleline>.Equals(Consoleline other)
        {
            return (other != null || this == null) && ageMilliseconds == other.ageMilliseconds  && plaintext == other.plaintext && letters.SequenceEqualSafe(other.letters);
        }
        public bool Equals(Consoleline other)
        {
            return (this as IEquatable<Consoleline>).Equals(other);
        }
    }

    public class Letter : IEquatable<Letter>
    {
        public char letter { get; set; }
        public float[] color { get; set; }
        public float[] bgColor { get; set; }

        bool IEquatable<Letter>.Equals(Letter other)
        {
            return (other != null || this == null) && letter == other.letter && color.SequenceEqualSafe(other.color) && bgColor.SequenceEqualSafe(other.bgColor);
        }
        public bool Equals(Letter other)
        {
            return (this as IEquatable<Letter>).Equals(other);
        }
    }



    // Not JSON, but convenience
    public class ConsoleState
    {
        public long startTime = 0;
        public ConsolelineSimple[] lines = null;
        public string getASSString(long currentTime, bool hasBgColors)
        {
            //long timeInterval = currentTime - startTime;
            StringBuilder sb = new StringBuilder();
            sb.Append("Dialogue: 1,"); // layer 1
            sb.Append($"{startTime.ASSTimeStamp()},");
            sb.Append($"{currentTime.ASSTimeStamp()},");
            sb.Append($"Console,Console,0,0,0,,");
            foreach(ConsolelineSimple line in lines)
            {
                line.letters.ASSFormatting(hasBgColors, sb);
                sb.Append("\\N");
            }
            return sb.ToString();
        }
    }
    public class CenterPrintState
    {
        public long startTime = 0;
        public Centerprint cprint = null;
        public string getASSString(long currentTime)
        {
            //long timeInterval = currentTime - startTime;
            StringBuilder sb = new StringBuilder();
            sb.Append("Dialogue: 0,"); // layer 0
            sb.Append($"{startTime.ASSTimeStamp()},");
            sb.Append($"{currentTime.ASSTimeStamp()},");
            sb.Append($"Centerprint,Centerprint,0,0,0,,");
            cprint.letters.ASSFormatting(true, sb);
            return sb.ToString();
        }
    }


    public class ConsolelineSimple : IEquatable<ConsolelineSimple>
    {
        public string plaintext { get; set; }
        public Letter[] letters { get; set; }

        bool IEquatable<ConsolelineSimple>.Equals(ConsolelineSimple other)
        {
            return (other != null || this == null) && plaintext == other.plaintext && letters.SequenceEqualSafe(other.letters);
        }
        public bool Equals(ConsolelineSimple other)
        {
            return (this as IEquatable<ConsolelineSimple>).Equals(other);
        }
    }

    static class VideoMetaHelpers { 
        public static bool SequenceEqualSafe<T>(this IEnumerable<T> a, IEnumerable<T> b)
        {
            return a == null && b == null || a != null && b != null && a.SequenceEqual(b);
        }

        public static string Color4ToHex(this float[] color4, bool alphaInvert)
        {
            byte[] byteColors = new byte[4] {
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color4[0] * 255.0f)) + 0.5f),
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color4[1] * 255.0f)) + 0.5f),
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color4[2] * 255.0f)) + 0.5f),
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color4[3] * 255.0f)) + 0.5f),
            };
            if (alphaInvert)
            {
                byteColors[3] = (byte)(255 - byteColors[3]);
            }

            return byteColors[0].ToString("X2") + byteColors[1].ToString("X2") + byteColors[2].ToString("X2") + byteColors[3].ToString("X2");
        }
        public static string Color3ToHex(this float[] color3, bool bgr)
        {
            byte[] byteColors = new byte[3] {
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color3[0] * 255.0f)) + 0.5f),
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color3[1] * 255.0f)) + 0.5f),
                (byte)(Math.Max(0.0f, Math.Min(255.0f, color3[2] * 255.0f)) + 0.5f),
            };

            if (bgr)
            {
                byte tmp = byteColors[2];
                byteColors[2] = byteColors[0];
                byteColors[0] = tmp;
            }

            return byteColors[0].ToString("X2") + byteColors[1].ToString("X2") + byteColors[2].ToString("X2");
        }
        public static string ColorChannelToHex(this float color, bool invert)
        {
            byte byteColor = (byte)(Math.Max(0.0f, Math.Min(255.0f, color * 255.0f)) + 0.5f);
            if (invert)
            {
                byteColor = (byte)(255-byteColor);
            }
            return byteColor.ToString("X2");
        }
        public static float linearToSRGB(this float color)
        {
            if(color <= 0.0031308f)
            {
                return 12.92f * color;
            }
            else
            {
                return 1.055f * (float)Math.Pow((double)color, 1.0 / 2.4)-0.055f;
            }
        }

        public static string ASSTimeStamp(this long hundredths)
        {
            long realHundredths = hundredths % 100;
            long seconds = hundredths / 100;
            long realSeconds = seconds % 60;
            long minutes = seconds / 60;
            long realMinutes = minutes % 60;
            long hours = minutes / 60;
            return hours.ToString("0") + ":" + realMinutes.ToString("00") + ":" + realSeconds.ToString("00") + ":" + realHundredths.ToString("00");
        }
        public static void ASSFormatting(this Letter[] letters, bool bgColors, StringBuilder sb)
        {
            if (bgColors)
            {
                sb.Append(@"{\4a0}");
            }
            else
            {
                sb.Append(@"{\4a&HFF&}");
            }
            float[] oldColor = new float[4] { 1f,1f,1f,1f};
            float[] oldBGColor = new float[4] { 0f,0f,0f,0f};
            foreach (Letter letter in letters)
            {
                if (!letter.color.SequenceEqualSafe(oldColor))
                {
                    sb.Append($"{{\\c&H{letter.color.Color3ToHex(true)}&\\1a&H{letter.color[3].ColorChannelToHex(true)}&}}");
                    oldColor = letter.color;
                }
                if (bgColors && !letter.bgColor.SequenceEqualSafe(oldBGColor))
                {
                    sb.Append($"{{\\4c&H{letter.bgColor.Color3ToHex(true)}&\\4a&H{letter.bgColor[3].ColorChannelToHex(true)}&}}");
                    oldBGColor = letter.bgColor;
                }
                if(letter.letter == '\n')
                {
                    sb.Append("\\N");
                }
                else
                {
                    sb.Append(letter.letter);
                }
            }
        }
        public static string ASSFormatting(this Letter[] letters, bool bgColors)
        {
            StringBuilder sb = new StringBuilder();
            letters.ASSFormatting(bgColors,sb);
            return sb.ToString();
        }
    }


}
