using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher.CameraOperators
{
    // Doesn't really do anything except stop trying to get back into spec (which is default behavior) if we are not in spec
    internal class SillyCameraOperator : CameraOperator
    {
        public override int getRequiredConnectionCount()
        {
            return 1;
        }

        public override string getTypeDisplayName()
        {
            return "Silly";
        }

        public override void Initialize()
        {
            base.Initialize();
        }



        protected bool isDestroyed = false;
        protected Mutex destructionMutex = new Mutex();

        ~SillyCameraOperator()
        {
            Destroy();
        }

        public override void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;

                base.Destroy();
                isDestroyed = true;
            }

        }
    }
}
