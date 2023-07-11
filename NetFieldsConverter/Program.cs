using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NetFieldsConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                convertFile(arg);
            }
            Console.ReadKey();
        }

        //static Regex regexLine = new Regex(@"{\s*[A-Z]+\(([^\)]+)\)\s*,\s*(\d+)\s*}",RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        //static Regex regexLineAdvanced = new Regex(@"{\s*[A-Z]+\(([^\)^\[]+)\s*(?:\[([^,\]]+?)\])?\)\s*,\s*([-\d]+)\s*}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        //static Regex regexLineAdvanced = new Regex(@"{\s*[A-Z]+\(([^\)^\[]+)\s*(?:\[([^,\]]+?)\])?\)\s*,\s*([^}]+)\s*}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        static Regex regexLineAdvanced = new Regex(@"( *\/\/ *)?{\s*[A-Z]+\(([^\)^\[]+)\s*(?:\[([^,\]]+?)\])?\)\s*,\s*([^}]+)\s*}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        
        private static void convertFile(string path)
        {
            Console.WriteLine(path);

            string outputPath =  Path.ChangeExtension(path, ".convertedNEW.txt");

            string[] linesIn = File.ReadAllLines(path);
            string[] linesOut = new string[linesIn.Length];

            for(int i =0;i<linesIn.Length;i++)
            {
                string lineIn = linesIn[i];
                Match match = regexLineAdvanced.Match(lineIn);
                if (!match.Success)
                {
                    linesOut[i] = linesIn[i] + " // Not detected";
                }
                else
                {
                    string commentPrefix = match.Groups[1].Success ? match.Groups[1].Value : "";
                    string mainString = match.Groups[2].Success ? match.Groups[2].Value : null;
                    string arrayOffset = match.Groups[3].Success ? match.Groups[3].Value : null;
                    string bits = match.Groups[4].Success ? match.Groups[4].Value : null;

                    if (mainString == null || bits == null)
                    {
                        linesOut[i] = linesIn[i] + " // Weird error";
                        continue;
                    }

                    string[] mainStringParts = mainString.Split(".");

                    if (mainStringParts.Length > 2)
                    {
                        linesOut[i] = linesIn[i] + " // Not sure how to handle. Do manually";
                    }

                    StringBuilder sb = new StringBuilder();
                    StringBuilder commentSb = new StringBuilder();
                    sb.Append(commentPrefix);
                    sb.Append("\t{ ");
                    sb.Append("nameof(");
                    sb.Append("ReplaceWithMainClass.");
                    string titleCaseName = ToTitleCase(mainStringParts[0]);
                    sb.Append(titleCaseName);
                    sb.Append(")");
                    sb.Append(", ");
                    if (mainStringParts.Length > 1)
                    {
                        commentSb.Append(" Replace ");
                        commentSb.Append(titleCaseName);
                        commentSb.Append("Type with real type and double check. ");
                        sb.Append(" Marshal.OffsetOf(typeof(");
                        sb.Append(titleCaseName);
                        sb.Append("Type");
                        sb.Append("),nameof(");
                        sb.Append(titleCaseName);
                        sb.Append("Type");
                        sb.Append(".");
                        sb.Append(ToTitleCase(mainStringParts[1]));
                        sb.Append(")).ToInt32()");
                        //Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2

                        if(arrayOffset != null)
                        {
                            int arrayOffsetNumber = 0;
                            int.TryParse(arrayOffset.Trim(),out arrayOffsetNumber);

                            if(arrayOffsetNumber > 0)
                            {

                                commentSb.Append("Also replace sizeof type near end. ");
                                sb.Append(" + sizeof(");
                                sb.Append(ToTitleCase(mainStringParts[1]));
                                sb.Append("Type");
                                sb.Append(")*");
                                sb.Append(arrayOffsetNumber);
                            }

                        }

                        sb.Append(", ");
                    }
                    else
                    {
                        if (arrayOffset != null)
                        {
                            int arrayOffsetNumber = 0;
                            int.TryParse(arrayOffset.Trim(), out arrayOffsetNumber);

                            if (arrayOffsetNumber > 0)
                            {

                                commentSb.Append("Replace sizeof type. ");
                                sb.Append(" sizeof(");
                                sb.Append(ToTitleCase(mainStringParts[0]));
                                sb.Append("Type");
                                sb.Append(")*");
                                sb.Append(arrayOffsetNumber);

                                sb.Append(", ");
                            }

                        }
                    }
                    sb.Append(bits);
                    sb.Append(" },");
                    if(commentSb.Length > 0)
                    {
                        sb.Append(" // ");
                        sb.Append(commentSb);
                    }

                    linesOut[i] = sb.ToString();
                }

            }
            File.WriteAllLines(outputPath, linesOut);
        }

        private static string ToTitleCase(string input)
        {
            //return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.Replace('_', ' ')).Replace(" ", "");
            StringBuilder output = new StringBuilder();
            bool makeNextUpper = false;
            for(int i = 0; i < input.Length; i++)
            {
                if (input[i] == '_')
                {
                    makeNextUpper = true;
                } else
                {
                    if (i == 0 || makeNextUpper)
                    {
                        output.Append(input[i].ToString().ToUpper());
                        makeNextUpper = false;
                    }
                    else
                    {
                        output.Append(input[i].ToString());
                    }
                }
            }
            return output.ToString();
        }


    }
}
