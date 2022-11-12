using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JKClient;

namespace JKWatcher
{
    static class JKClientExtensions
    {
        public static bool currentValidOrFilledFromPlayerState(this ClientEntity entity)
        {
            return entity.CurrentValid || entity.CurrentFilledFromPlayerState;
        }
    }
}
