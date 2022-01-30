using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.CameraOperators
{
    class CTFCameraOperatorRedBlue : CameraOperator
    {
        public override int getRequiredConnectionCount()
        {
            return 2;
        }
    }
}
