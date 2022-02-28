using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{
    abstract class CameraOperator : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected Connection[] connections = null;
        protected ServerSharedInformationPool infoPool = null;

        public bool HasErrored {get;protected set;} = false;

        public event EventHandler<ErroredEventArgs> Errored;
        protected void OnErrored(ErroredEventArgs eventArgs)
        {
            this.Errored?.Invoke(this, eventArgs);
        }


        private bool initialized = false;

        private int index = -1;
        public int Index { 
            get {
                return index;
            } 
            set { 
                if(value != index)
                {
                    index = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Index"));
                    if(connections != null)
                    {
                        foreach(Connection conn in connections)
                        {
                            conn.CameraOperator = index;
                        }
                    }
                }
            } 
        }

        public string Type { get {
                return getTypeDisplayName();
            } }

        public CameraOperator() { }

        abstract public int getRequiredConnectionCount();

        abstract public string getTypeDisplayName();

        public void provideConnections(Connection[] connectionsA)
        {
            if(connectionsA.Count() != getRequiredConnectionCount())
            {
                throw new Exception("Not enough connections were provided to a camera operator.");
            }
            connections = connectionsA;
            foreach (Connection conn in connections)
            {
                conn.CameraOperator = index;
            }
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

        protected bool isDestroyed = false;
        protected Mutex destructionMutex = new Mutex();

        virtual public void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;
                if(connections != null)
                {
                    foreach (Connection conn in connections)
                    {
                        conn.CameraOperator = null;
                    }
                }
                connections = null;
                infoPool = null;
                isDestroyed = true;
            }
        }

        // Use this at the start of any function that requires everything to be initialized.
        protected void ThrowErrorIfNotInitialized()
        {
            if (!initialized)
            {
                throw new Exception("CameraOperator is not initialized.");
            }
        }

        public class ErroredEventArgs : EventArgs
        {

            public Exception Exception { get; private set; }

            public ErroredEventArgs(Exception exception)
            {
                Exception = exception;
            }
        }
    }

    
}
