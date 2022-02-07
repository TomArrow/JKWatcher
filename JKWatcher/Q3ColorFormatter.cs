using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace JKWatcher
{
    static class Q3ColorFormatter
    {



        static Dictionary<char,int> hexColorStarters = new Dictionary<char,int> { { 'x', 3 },{ 'X',6 },{ 'y', 4 },{ 'Y',8 } };
        static Regex hexColorRegex = new Regex("x[0-9a-f]{3}|y[0-9a-f]{4}|X[0-9a-f]{6}|Y[0-9a-f]{8}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static Vector4 v4DKGREY2 = new Vector4(0.15f, 0.15f, 0.15f, 1f);

        public static Run[] Q3StringToInlineArray(string q3String, bool hexSupport = true, bool contrastSafety=true)
        {
            List<Run> runs = new List<Run>();
            StringBuilder sb = new StringBuilder();
            int colorsParsed = 0;
            Vector4? foregroundColor = null;
            Vector4? backgroundColor = null;

            for (int i = 0; i < q3String.Length; i++)
            {
                int charsLeft = q3String.Length - 1 - i;
                char curChar = q3String[i];
                if (curChar == '^')
                {
                    if(charsLeft == 0)
                    {
                        break; // Nuthing to do, just a lonely ^ at the end
                    }
                    char nextChar = q3String[i + 1];
                    if (nextChar == '^') // Just an escaped ^ we actually want to see
                    {
                        sb.Append('^');
                        i++;
                        colorsParsed = 0;
                    } else 
                    {
                        if(colorsParsed == 0)
                        {
                            if(sb.Length == 0)
                            {
                                // First time, do nothing.
                            } else
                            {
                                // String is finished. Write it out.
                                Run newRun = new Run(sb.ToString());
                                if (foregroundColor.HasValue)
                                {
                                    // set a default background color in case none was specified
                                    // Because default background is too bright for most names probably.
                                    if (!backgroundColor.HasValue)
                                    {
                                        backgroundColor = v4DKGREY2;
                                    }
                                    Vector4 fgColor = foregroundColor.Value, bgColor = backgroundColor.Value;

                                    if (contrastSafety)
                                    {
                                        ensureContrast(ref fgColor, ref bgColor);
                                    }
                                    newRun.Foreground = new SolidColorBrush(vectorToColor(fgColor));
                                    newRun.Background = new SolidColorBrush(vectorToColor(bgColor));
                                }

                                runs.Add(newRun);
                                sb.Clear();
                                foregroundColor = null;
                                backgroundColor = null;
                            }
                        }

                        // Color is being specified. This
                        int length;
                        Vector4 color = parseColor(ref q3String,i+1,out length,hexSupport);
                        i += length;
                        if(colorsParsed == 0 || colorsParsed == 2)
                        {
                            foregroundColor = color;
                        } else if(colorsParsed == 1)
                        {
                            backgroundColor = color;
                        } // We don't handle 4 colors in a row. What is that even supposed to be?
                        colorsParsed++;
                    }
                } else
                {
                    sb.Append(curChar);
                    colorsParsed = 0;
                }
            }
            // We're done. Repeat what we did above
            // Sad to duplicate the code but not sure what else to do.
            if (sb.Length == 0)
            {
                // Hmm guess nothing came anymore
            }
            else
            {
                // String is finished. Write it out.
                Run newRun = new Run(sb.ToString());
                if (foregroundColor.HasValue)
                {
                    // set a default background color in case none was specified
                    // Because default background is too bright for most names probably.
                    if (!backgroundColor.HasValue)
                    {
                        backgroundColor = v4DKGREY2;
                    }
                    Vector4 fgColor = foregroundColor.Value, bgColor = backgroundColor.Value;

                    if (contrastSafety)
                    {
                        ensureContrast(ref fgColor, ref bgColor);
                    }
                    newRun.Foreground = new SolidColorBrush(vectorToColor(fgColor));
                    newRun.Background = new SolidColorBrush(vectorToColor(bgColor));
                }
                runs.Add(newRun);
                sb.Clear();
                foregroundColor = null;
                backgroundColor = null;
            }
            return runs.ToArray();
        }
        
        public static string cleanupString(string q3String, bool hexSupport = true)
        {
            List<Run> runs = new List<Run>();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < q3String.Length; i++)
            {
                int charsLeft = q3String.Length - 1 - i;
                char curChar = q3String[i];
                if (curChar == '^')
                {
                    if(charsLeft == 0)
                    {
                        break; // Nuthing to do, just a lonely ^ at the end
                    }
                    char nextChar = q3String[i + 1];
                    if (nextChar == '^') // Just an escaped ^ we actually want to see
                    {
                        sb.Append('^');
                        i++;
                    } else 
                    {
                        
                        // Color is being specified. This
                        int length;
                        Vector4 color = parseColor(ref q3String,i+1,out length,hexSupport);
                        i += length;
                    }
                } else
                {
                    sb.Append(curChar);
                }
            }
            
            return sb.ToString();
        }

        const float contrastMinBrightnessFactor = 4.0f;
        private static void ensureContrast(ref Vector4 foregroundColor,ref Vector4 backgroundColor)
        {
            bool isSameColor = false;
            if (foregroundColor.X == backgroundColor.X && foregroundColor.Y == backgroundColor.Y && foregroundColor.Z == backgroundColor.Z)
            {
                // Same color. Nope.
                isSameColor = true;
            }
            float fgAlpha = foregroundColor.W;
            float bgAlpha = backgroundColor.W;
            float fgBrightness = vec4brightness(ref foregroundColor);
            float bgBrightness = vec4brightness(ref backgroundColor);

            if (isSameColor) { 
                if( fgBrightness >= 0.5)
                {
                    backgroundColor *= 0.5f;
                    backgroundColor.W = bgAlpha;
                } else
                {
                    backgroundColor *= 2f;
                    backgroundColor.W = bgAlpha;
                } 
                if(backgroundColor.X == 0f && backgroundColor.Y == 0f && backgroundColor.Z == 0f)
                {
                    // Literally black. Let's just set some constant for BG then
                    backgroundColor.X = 0.25f;
                    backgroundColor.Y = 0.25f;
                    backgroundColor.Z = 0.25f;
                }
            }

            float fgPeak = Math.Max(foregroundColor.X, Math.Max(foregroundColor.Y, foregroundColor.Z));
            float bgPeak = Math.Max(backgroundColor.X, Math.Max(backgroundColor.Y, backgroundColor.Z));

            float factor = fgBrightness / bgBrightness;
            if (factor == 1f)
            {
                if(fgBrightness >= 0.5f)
                {
                    backgroundColor /= 2f;
                    backgroundColor.W = bgAlpha;
                    factor = 2f;
                } else
                {
                    backgroundColor *= 2f;
                    backgroundColor.W = bgAlpha;
                    factor = 0.5f;
                }
            }
            if(factor > 1f)
            {
                if(factor < contrastMinBrightnessFactor)
                {
                    float ratio = contrastMinBrightnessFactor / factor;
                    // We need to either divide background by ratio or multiply foreground with ratio
                    // Or we darken bg and brighten fg equally. But fg is limited. Can't go above 1.
                    float equallyDividedRatio = (float)Math.Sqrt(ratio);
                    float maximumBrightenRatio = 1f / fgPeak;
                    float fgRatio = equallyDividedRatio,bgRatio = 1f/equallyDividedRatio;
                    if(maximumBrightenRatio < fgRatio)
                    {
                        fgRatio = maximumBrightenRatio;
                        bgRatio = 1f/(ratio / fgRatio);
                    }
                    foregroundColor *= fgRatio;
                    foregroundColor.W = fgAlpha;
                    backgroundColor *= bgRatio;
                    backgroundColor.W = bgAlpha;
                }
            } else
            {
                float inverseFactor = 1f / factor;
                if (inverseFactor < contrastMinBrightnessFactor)
                {
                    float ratio = contrastMinBrightnessFactor / inverseFactor;
                    // We need to either divide background by ratio or multiply foreground with ratio
                    // Or we darken bg and brighten fg equally. But fg is limited. Can't go above 1.
                    float equallyDividedRatio = (float)Math.Sqrt(ratio);
                    float maximumBrightenRatio = 1f / bgPeak;
                    float bgRatio = equallyDividedRatio, fgRatio = 1f / equallyDividedRatio;
                    if (maximumBrightenRatio < bgRatio)
                    {
                        bgRatio = maximumBrightenRatio;
                        fgRatio = 1 / (ratio / bgRatio);
                    }
                    foregroundColor *= fgRatio;
                    foregroundColor.W = fgAlpha;
                    backgroundColor *= bgRatio;
                    backgroundColor.W = bgAlpha;
                }
            }

        }

        private static float vec4brightness(ref Vector4 color) // This is VERY rough. Should technically take into account gamma. But fuck it, speed is more important.
        {
            return 0.2126f * color.X + 0.7152f * color.Y + 0.0722f * color.Z;
        }

        private static Color vectorToColor(Vector4 input)
        {
            Color retVal = new Color();
            retVal.R = floatColorToByte(input.X);
            retVal.G = floatColorToByte(input.Y);
            retVal.B = floatColorToByte(input.Z);
            retVal.A = floatColorToByte(input.W);
            return retVal;
        }
        private static byte floatColorToByte(float color)
        {
            return (byte)Math.Clamp(color*255f,0f,255f);
        }

        private static Vector4 parseColor(ref string inputString,int startIndex, out int length, bool hexSupport)
        {
            int charsLeft = inputString.Length - startIndex;
            char firstChar = inputString[startIndex];
            if (hexSupport && hexColorStarters.ContainsKey(firstChar) && charsLeft >= hexColorStarters[firstChar])
            {
                string range = inputString.Substring(startIndex, hexColorStarters[firstChar]);
                if (!hexColorRegex.IsMatch(range))
                {
                    // Interpret color as base jk2 color instead
                    length = 1;
                    return g_color_table[ColorIndex_Extended(inputString[startIndex])];
                }

                Vector4 color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

                length = hexColorStarters[firstChar] + 1;

                // Hex color parsing
                switch (firstChar)
                {
                    case 'y':
                        color.X = int.Parse(inputString.Substring(startIndex + 1, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        color.Y = int.Parse(inputString.Substring(startIndex + 2, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        color.Z = int.Parse(inputString.Substring(startIndex + 3, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        color.W = int.Parse(inputString.Substring(startIndex + 4, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        break;
                    case 'x':
                        color.X = int.Parse(inputString.Substring(startIndex + 1, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        color.Y = int.Parse(inputString.Substring(startIndex + 2, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        color.Z = int.Parse(inputString.Substring(startIndex + 3, 1), System.Globalization.NumberStyles.HexNumber) / 15f;
                        break;
                    case 'Y':
                        color.X = int.Parse(inputString.Substring(startIndex + 1, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        color.Y = int.Parse(inputString.Substring(startIndex + 3, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        color.Z = int.Parse(inputString.Substring(startIndex + 5, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        color.W = int.Parse(inputString.Substring(startIndex + 7, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        break;
                    case 'X':
                        color.X = int.Parse(inputString.Substring(startIndex + 1, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        color.Y = int.Parse(inputString.Substring(startIndex + 3, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        color.Z = int.Parse(inputString.Substring(startIndex + 5, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                        break;
                }
                return color;

            } else
            {
                // Normal JK2 color
                length = 1;
                return g_color_table[ColorIndex_Extended(inputString[startIndex])];
            }
        }

        const int COLOR_EXT_AMOUNT = 16;
        private static int ColorIndex_Extended(char c) {
            return ((c) - '0') & (COLOR_EXT_AMOUNT - 1); // compatible with 1.02, 'a' & 15 = 1
        }


        static List<Vector4> g_color_table = new List<Vector4> { 
	        // Default colorTable
	        new Vector4(0.0f, 0.0f, 0.0f, 1.0f),           // ^0 -> black
	        new Vector4(1.0f, 0.0f, 0.0f, 1.0f),           // ^1 -> red
	        new Vector4(0.0f, 1.0f, 0.0f, 1.0f),           // ^2 -> green
	        new Vector4(1.0f, 1.0f, 0.0f, 1.0f),           // ^3 -> yellow
	        new Vector4(0.0f, 0.0f, 1.0f, 1.0f),           // ^4 -> blue
	        new Vector4(0.0f, 1.0f, 1.0f, 1.0f),           // ^5 -> cyan
	        new Vector4(1.0f, 0.0f, 1.0f, 1.0f),           // ^6 -> magenta
	        new Vector4(1.0f, 1.0f, 1.0f, 1.0f),           // ^7 -> white

	        // Extended colorTable
	        new Vector4( 1.0f, 0.5f, 0.0f, 1.0f ),         // ^8 -> orange
	        new Vector4( 0.5f, 0.5f, 0.5f, 1.0f ),         // ^9 -> md. grey
	        new Vector4(0.75f, 0.75f, 0.75f, 1f),          // ^j -> lt. rey
	        new Vector4(0.25f, 0.25f, 0.25f, 1f),          // ^k -> dk. grey
	        new Vector4(0.367f, 0.261f, 0.722f, 1f),    // ^l -> lt. blue
	        new Vector4(0.199f, 0.0f, 0.398f, 1f),      // ^m -> dk. blue
        #if DEBUG
	        new Vector4(.70f, 0f, 0f, 1f),                // ^n -> jk2mv-color [red]
        #else
	        new Vector4( 0.509f, 0.609f, 0.847f, 1.0f),     // ^n -> jk2mv-color [blue]
        #endif
	        new Vector4(1.0f, 1.0f, 1.0f, 0.75f),          // ^o -> lt. transparent
        };
    }


    // WPF
    public class Q3StringToPlaintextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            return Q3ColorFormatter.cleanupString((string)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
