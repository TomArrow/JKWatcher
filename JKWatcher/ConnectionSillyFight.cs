using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JKClient;
using Client = JKClient.JKClient;

namespace JKWatcher
{
    // Silly fighting when forced out of spec
    public partial class Connection
    {
        private bool amNotInSpec = false; // If not in spec for whatever reason, do funny things
        private bool isDuelMode = false; // If we are in duel mode, different behavior. Some servers don't like us attacking innocents but for duel we have to, to end it quick. But if someone attacks US, then all bets are off.

        private void DoSillyThings(ref UserCommand userCmd)
        {
            // Of course normally the priority is to get back in spec
            // But sometimes it might not be possible, OR we might not want it (for silly reasons)
            // So as long as we aren't in spec, let's do silly things.


        }
    }
}
