using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{
    static class Helpers
    {
        public static string GetUnusedDemoFilename(string baseFilename, JKClient.ProtocolVersion protocolVersion)
        {
            string extension = ".dm_" + ((int)protocolVersion).ToString();
            if (!File.Exists("demos/"+baseFilename+ extension))
            {
                return baseFilename;
            }
            //string extension = Path.GetExtension(baseFilename);

            int index = 1;
            while (File.Exists("demos/" + baseFilename+ "("+ (++index).ToString()+")" + extension)) ;

            return baseFilename + "(" + (++index).ToString() + ")";
        }
    }
}
