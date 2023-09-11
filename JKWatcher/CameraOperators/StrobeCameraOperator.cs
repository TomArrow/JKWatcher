using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher.CameraOperators
{
    internal class StrobeCameraOperator : CameraOperator
    {
        public override int getRequiredConnectionCount()
        {
            return 1;
        }

        public override string getTypeDisplayName()
        {
            return "Strobe";
        }

        public override void Initialize()
        {
            base.Initialize();
            this.connections[0].ClientUserCommandGenerated += StrobeCameraOperator_ClientUserCommandGenerated;
        }

        bool pressAttack = false;
        private void StrobeCameraOperator_ClientUserCommandGenerated(object sender, ref JKClient.UserCommand modifiableCommand, in JKClient.UserCommand previousCommand, ref List<JKClient.UserCommand> insertCommands)
        {
            if (pressAttack) // Just turn it on and off as quick as possible.
            {
                modifiableCommand.Buttons |= (int)JKClient.UserCommand.Button.Attack;
                modifiableCommand.Buttons |= (int)JKClient.UserCommand.Button.AnyJK2; // AnyJK2 simply means Any, but its the JK2 specific constant
            }
            pressAttack = !pressAttack;
        }


        protected bool isDestroyed = false;
        protected Mutex destructionMutex = new Mutex();

        ~StrobeCameraOperator()
        {
            Destroy();
        }

        public override void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;

                this.connections[0].ClientUserCommandGenerated -= StrobeCameraOperator_ClientUserCommandGenerated;
                base.Destroy();
                isDestroyed = true;
            }

        }
    }
}
