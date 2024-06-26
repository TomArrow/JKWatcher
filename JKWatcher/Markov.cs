using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markov;
using System.IO;
using System.Threading.Tasks;

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

            MarkovChain<string> chain = new MarkovChain<string>(2);

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

        public static string GetAnyMarkovText(string startString = null)
        {
            MarkovChain<string> chain = null;

            lock (chains)
            {
                string[] keys = chains.Keys.ToArray();
                if (keys.Length == 0) return null;

                chain = chains[keys[rnd.Next(0,chains.Count)]];
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
                }
            }

            string[] tokens = null;
            lock (chain)
            {
                if(previous is null)
                {
                    tokens = chain.Chain(rnd).ToArray();
                } else
                {
                    tokens = chain.Chain(previous,rnd).ToArray();
                }
            }

            return previous is null ? string.Join(' ', tokens) : startString.Trim()+" "+ string.Join(' ', tokens);
        }
    }
}
