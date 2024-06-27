using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markov;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace JKWatcher
{
    public static class Markov
    {

        private static Dictionary<string, MarkovChain<string>> chains = new Dictionary<string, MarkovChain<string>>();

        static Random rnd = new Random();
        public static bool RegisterMarkovChain(string filename, Action<Int64,Int64> trainProgressCallback = null)
        {
            string[] lines = null;
            try
            {
                lines = File.ReadAllLines(filename);
            } catch(Exception ex)
            {
                Helpers.logToFile(ex.ToString());
                return false;
            }

            MarkovChain<string> chain = new MarkovChain<string>(2, KeyTransformer);

            Int64 index = 0;
            foreach(string line in lines)
            {
                string[] tokens = Q3ColorFormatter.tokenizeStringColors(line, true);
                if(tokens != null)
                {
                    chain.Add(tokens);
                }
                index++;
                if (!(trainProgressCallback is null))
                {
                    trainProgressCallback(index, lines.Length);
                }
            }

            lock (chains)
            {
                chains[filename] = chain;
            }
            return true;
        }

        static Regex sSounds = new Regex("[zx]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex kSounds = new Regex("[cgq]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex vocals = new Regex("[aeiou]",RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex repeats = new Regex(@"(.)\1{1,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string KeyTransformer(string input)
        {
            return sSounds.Replace(vocals.Replace(kSounds.Replace(repeats.Replace(input.ToLowerInvariant(),@"$1"),"k"),"o").Replace("ph","f").Replace('b','p'),"s").Replace('m', 'n');
        }

        public static string GetAnyMarkovText(string startString = null)
        {
            MarkovChain<string> chain = null;

            lock (chains)
            {
                string[] keys = chains.Keys.ToArray();
                if (keys.Length == 0) return null;

                lock (rnd)
                {
                    chain = chains[keys[rnd.Next(0,chains.Count)]];
                }
            }

            if(chain is null)
            {
                return null;
            }

            string[] previous = null;
            if(!string.IsNullOrWhiteSpace(startString))
            {
                string[] previousTokens = Q3ColorFormatter.tokenizeStringColors(startString,true);
                if(previousTokens != null && previousTokens.Length > 0)
                {
                    if (previousTokens.Length > 2)
                    {
                        previous = new string[] { previousTokens[previousTokens.Length - 2], previousTokens[previousTokens.Length - 1] };
                    } else
                    {
                        previous = previousTokens;
                    }
                    for(int i = 0; i < previous.Length; i++)
                    {
                        previous[i] = KeyTransformer(previous[i]);
                    }
                }
            }

            string[] tokens = null;
            lock (chain)
            {
                lock (rnd) { 
                    if(previous is null)
                    {
                        tokens = chain.Chain(rnd,10).ToArray();
                    } else
                    {
                        tokens = chain.Chain(previous,rnd,10).ToArray();
                    }
                }
            }

            //return previous is null ? string.Join(' ', tokens) : startString.Trim()+" "+ string.Join(' ', tokens);
            return tokenReassembler(tokens);// string.Join(' ', tokens);
        }
        static readonly char[] punctuation = {'.',',',';',':' };
        private static string tokenReassembler(string[] tokens)
        {
            if (tokens is null) return null;
            StringBuilder sb= new StringBuilder();
            string lastToken = null;
            lock (rnd)
            {
                foreach (string token in tokens)
                {
                    if (lastToken == null || token.Length == 1 && lastToken.Length == 1 || lastToken == "(" || token == ")" || lastToken == "[" || token == "]")
                    {

                    }
                    else if (token.Length == 1 && (token == "," || token == "." || token == "!" || token == "?") && rnd.NextDouble() > 0.1)
                    {

                    }
                    else if (token.Length == 1 && (token == ";" || token == ":") && rnd.NextDouble() > 0.8)
                    {

                    }
                    else if (token.Length == 1 && rnd.NextDouble() > 0.9)
                    {

                    } else
                    {
                        sb.Append(" ");
                    }

                    sb.Append(token);
                    lastToken = token;
                }
            }
            return sb.ToString();
        }

    }
}
