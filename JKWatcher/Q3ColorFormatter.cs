using System;
using System.Collections.Generic;
using Drawing = System.Drawing;
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
        // Don't get confused. The "^" in the regex string actually just makes sure it's the beginning of the string. It's not literally matching the "^" even tho it seems that way
        static Regex hexColorRegex = new Regex("^x[0-9a-fA-F]{3}|^y[0-9a-fA-F]{4}|^X[0-9a-fA-F]{6}|^Y[0-9a-fA-F]{8}", RegexOptions.Compiled);

        static Vector4 v4DKGREY2 = new Vector4(0.15f, 0.15f, 0.15f, 1f);

        public struct ColoredChar
        {
            public char character;
            //public Drawing.Brush color;
            public Drawing.Color color;
            //public static implicit operator char(ColoredChar c) =>c.character;
        }
        public class ColoredString
        {
            public string text;
            public Drawing.Color color = Drawing.Color.White;
            public Drawing.Color backgroundColor = vectorToDrawingColor(v4DKGREY2);
        }


        // Cringe but what can you do :)
        public static Drawing.Region[] MeasureCharacterRangesUnlimited<T>(this Drawing.Graphics g, Drawing.CharacterRange[] allRanges, string text, Drawing.Font font, T? layoutRectangle, Drawing.StringFormat format)
        {
            List< Drawing.Region> regions = new List<Drawing.Region>();
            Queue<Drawing.CharacterRange> rangesQueue = new Queue<Drawing.CharacterRange>(allRanges);
            List<Drawing.CharacterRange> currentRanges = new List<Drawing.CharacterRange>();


            Drawing.RectangleF? rectangle = null;
            Drawing.PointF? point = null;
            if (layoutRectangle is Drawing.RectangleF)
            {
                rectangle = layoutRectangle as Drawing.RectangleF?;
            }
            else if (layoutRectangle is Drawing.PointF)
            {
                point = layoutRectangle as Drawing.PointF?;
            }
            if (rectangle is null && layoutRectangle is null) throw new InvalidOperationException("MeasureCharacterRangesUnlimited<T> only works with Drawing.RectangleF/Drawing.PointF");

            Drawing.RectangleF rectangleFinal = rectangle.HasValue ? rectangle.Value : new Drawing.RectangleF(point.Value.X,point.Value.Y,9999,9999);
            while (rangesQueue.Count>0 || currentRanges.Count > 0)
            {
                bool flush = false;
                if(rangesQueue.Count > 0)
                {
                    currentRanges.Add(rangesQueue.Dequeue());
                    if(currentRanges.Count == 32)
                    {
                        flush = true;
                    }
                }
                else
                {
                    flush = true;
                }
                if (flush)
                {
                    format.SetMeasurableCharacterRanges(currentRanges.ToArray());
                    regions.AddRange(g.MeasureCharacterRanges(text, font, rectangleFinal, format));
                    currentRanges.Clear();
                }
            }
            return regions.ToArray();
        }
        
        public static bool DrawStringQ3<T>(this Drawing.Graphics g, string? s, Drawing.Font font, T layoutRectangle, Drawing.StringFormat? format, bool hexSupport = true, bool contrastSafety = true)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;

            bool mustDisposeStringFormat = false;
            //string cleanString = cleanupString(s, hexSupport);
            (ColoredChar[] chars, ColoredChar[] charsBg) =  Q3StringToColoredCharArrays(s,hexSupport,contrastSafety);
            if (chars is null || charsBg is null || chars.Length != charsBg.Length) return false;

            List<char> charsRaw = new List<char>();

            string cleanString = new string(Array.ConvertAll<ColoredChar, char>(chars, (a) => { return a.character; }));

            if (cleanString is null) return false;

            List<Drawing.CharacterRange> ranges = new List<Drawing.CharacterRange>();
            for(int i = 0; i < cleanString.Length; i++)
            {
                ranges.Add(new Drawing.CharacterRange(i,1));
            }

            if(format is null)
            {
                format = new Drawing.StringFormat();
                mustDisposeStringFormat = true;
            }
            //format.SetMeasurableCharacterRanges(ranges.ToArray()); // don't do this. if more than 32, we get overflow exception

            Drawing.Region[] regions = g.MeasureCharacterRangesUnlimited(ranges.ToArray(),cleanString, font, layoutRectangle, format);

            if (regions is null || regions.Length != chars.Length) {
                if (mustDisposeStringFormat)
                {
                    format.Dispose();
                }
                return false;
            }

            var oldAlignment = format.Alignment;
            format.Alignment = Drawing.StringAlignment.Center;
            float shadowDist = 2f * font.Size / 14f;
            for (int i = 0; i < chars.Length; i++)
            {
                Drawing.RectangleF where = regions[i].GetBounds(g);
                Drawing.RectangleF whereBg = where;
                whereBg.X += shadowDist;
                whereBg.Y += shadowDist;

                using (Drawing.SolidBrush brushBg = new Drawing.SolidBrush(charsBg[i].color))
                {
                    g.DrawString(charsBg[i].character.ToString(), font, brushBg, whereBg, format);
                }
                using(Drawing.SolidBrush brushFg = new Drawing.SolidBrush(chars[i].color))
                {
                    g.DrawString(chars[i].character.ToString(), font, brushFg, where, format);
                }
            }
            format.Alignment = oldAlignment;

            if (mustDisposeStringFormat)
            {
                format.Dispose();
            }

            return true;
        }

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
                        if(colorsParsed %2 == 0)
                        {
                            foregroundColor = color;
                        } else if(colorsParsed % 2 == 1)
                        {
                            backgroundColor = color;
                        } // Todo: maybe make option to handle chat prepended ^6 more gracefully like CG_ChatBox_AddString in eternaljk2mv
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

        // Returns arrays of chars for fore- and background
        public static (ColoredChar[], ColoredChar[]) Q3StringToColoredCharArrays(string q3String, bool hexSupport = true, bool contrastSafety = true)
        {
            List<ColoredChar> chars = new List<ColoredChar>();
            List<ColoredChar> charsBg = new List<ColoredChar>();
            ColoredString[] strings = Q3StringToColoredStringArray(q3String, hexSupport, contrastSafety);
            foreach(ColoredString text in strings)
            {
                if (text.text is null) continue;
                foreach (char character in text.text)
                {
                    chars.Add(new ColoredChar() { character = character, color = text.color});
                    charsBg.Add(new ColoredChar() { character = character, color = text.backgroundColor});
                }
            }
            return (chars.ToArray(),charsBg.ToArray());
        }

        public static ColoredString[] Q3StringToColoredStringArray(string q3String, bool hexSupport = true, bool contrastSafety=true)
        {
            List<ColoredString> runs = new List<ColoredString>();
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
                                ColoredString newRun = new ColoredString() { text = sb.ToString() };
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
                                    newRun.color = vectorToDrawingColor(fgColor);
                                    newRun.backgroundColor = vectorToDrawingColor(bgColor);
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
                        if(colorsParsed %2 == 0)
                        {
                            foregroundColor = color;
                        } else if(colorsParsed % 2 == 1)
                        {
                            backgroundColor = color;
                        } // Todo: maybe make option to handle chat prepended ^6 more gracefully like CG_ChatBox_AddString in eternaljk2mv
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
                ColoredString newRun = new ColoredString() { text = sb.ToString() };
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
                    newRun.color = vectorToDrawingColor(fgColor);
                    newRun.backgroundColor = vectorToDrawingColor(bgColor);
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
            if (q3String is null) return null;
            //List<Run> runs = new List<Run>();
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

        // Make each word its own token. colors become their own token too. not ideal or anything but eh :)
        // TODO make it take into account whether there was a space before/after colors? idk.
        public static string[] tokenizeStringColors(string q3String, bool hexSupport = true)
        {
            if (q3String is null) return null;
            //List<Run> runs = new List<Run>();
            List<string> tokens = new List<string>();
            StringBuilder currentToken = new StringBuilder();

            for (int i = 0; i < q3String.Length; i++)
            {
                int charsLeft = q3String.Length - 1 - i;
                char curChar = q3String[i];
                if (curChar == '^')
                {
                    if (charsLeft == 0)
                    {
                        break; // Nuthing to do, just a lonely ^ at the end
                    }
                    char nextChar = q3String[i + 1];
                    if (nextChar == '^') // Just an escaped ^ we actually want to see
                    {
                        currentToken.Append('^');
                        i++;
                    } else
                    {

                        // Color is being specified. This
                        int length;
                        Vector4 color = parseColor(ref q3String, i + 1, out length, hexSupport);
                        if (currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                        tokens.Add(q3String.Substring(i, length + 1));
                        i += length;
                    }
                } else if (curChar == ' ') {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                } else if (!Char.IsLetterOrDigit(curChar)) {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    tokens.Add(curChar.ToString());
                } else
                {
                    currentToken.Append(curChar);
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }

            return tokens.ToArray();
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
        private static Drawing.Color vectorToDrawingColor(Vector4 input)
        {
            Drawing.Color retVal = Drawing.Color.FromArgb(floatColorToByte(input.W), floatColorToByte(input.X), floatColorToByte(input.Y), floatColorToByte(input.Z));
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
            if (hexSupport && hexColorStarters.ContainsKey(firstChar) && charsLeft > hexColorStarters[firstChar])
            {
                string range = inputString.Substring(startIndex, hexColorStarters[firstChar]+1);
                if (!hexColorRegex.IsMatch(range))
                {
                    // Interpret color as base jk2 color instead
                    length = 1;
                    //return g_color_table[ColorIndex_Extended(inputString[startIndex])];
                    return g_color_table[ColorIndex(inputString[startIndex])];
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
                //return g_color_table[ColorIndex_Extended(inputString[startIndex])];
                return g_color_table[ColorIndex(inputString[startIndex])];
            }
        }

        const int COLOR_EXT_AMOUNT = 16;
        private static int ColorIndex_Extended(char c) {
            return ((c) - '0') & (COLOR_EXT_AMOUNT - 1); // compatible with 1.02, 'a' & 15 = 1
        }
        private static int ColorIndex(char c) {
            return ((c) - '0') & 7;
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
