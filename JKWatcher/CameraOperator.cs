using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{
    abstract class CameraOperator : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected Connection[] connections = null;
        protected ServerSharedInformationPool infoPool = null;

        private bool initialized = false;

        public int Index { get; set; } = -1;

        public CameraOperator() { }

        abstract public int getRequiredConnectionCount();

        public void provideConnections(Connection[] connectionsA)
        {
            if(connectionsA.Count() != getRequiredConnectionCount())
            {
                throw new Exception("Not enough connections were provided to a camera operator.");
            }
            connections = connectionsA;
        }

        public void provideServerSharedInformationPool(ServerSharedInformationPool pool)
        {
            if (pool == null)
            {
                throw new Exception("Need to provide a valid ServerSharedInformationPool, not a null.");
            }
            infoPool = pool;
        }

        // This must be called after connections and the shared information pool is provided
        // It is the "true" constructor (from the outside perspective) if you will.
        virtual public void Initialize() {
            if (connections == null)
            {
                throw new Exception("Cannot initialize CameraOperator before providing connections.");
            }
            if (infoPool == null)
            {
                throw new Exception("Cannot initialize CameraOperator before providing ServerSharedInformationPool.");
            }
            initialized = true;
        }

        // Use this at the start of any function that requires everything to be initialized.
        protected void ThrowErrorIfNotInitialized()
        {
            if (!initialized)
            {
                throw new Exception("CameraOperator is not initialized.");
            }
        }
    }
}
