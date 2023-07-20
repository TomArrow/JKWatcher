using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace JKWatcher
{
    public interface ConnectionProvider
    {
        public Connection[] getUnboundConnections(int count);
        public bool requestConnectionDestruction(Connection conn);
    }

    public abstract class CameraOperator : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        //protected Connection[] connections = null;
        protected List<Connection> connections = new List<Connection>();
        protected ServerSharedInformationPool infoPool = null;
        protected ConnectionProvider connectionProvider = null;

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
                            //conn.CameraOperator = index;
                            conn.CameraOperator = this;
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
            //connections = connectionsA;
            connections.AddRange(connectionsA);
            foreach (Connection conn in connections) // Todo what if it gets updated?
            {
                //conn.CameraOperator = index;
                conn.CameraOperator = this;
            }
        }

        protected bool getMoreConnections(int count)
        {
            lock (connections)
            {

                if (!initialized)
                {
                    throw new Exception("Can't request extra connections before initialization.");
                    return false;
                }
                Connection[] newConns = null;
                Application.Current.Dispatcher.Invoke(() => {
                    newConns = connectionProvider.getUnboundConnections(count);
                });
                if (newConns == null)
                {
                    throw new Exception("Error requesting extra connections.");
                    return false;
                }
                if (newConns.Count() != count)
                {
                    throw new Exception("Not enough extra connections were provided to a camera operator.");
                    return false;
                }
                //connections = connectionsA;
                connections.AddRange(newConns);
                foreach (Connection conn in connections) // Todo what if it gets updated?
                {
                    //conn.CameraOperator = index;
                    conn.CameraOperator = this;
                    conn.startDemoRecord(); // TODO Make this more elegant. Optional or sth. To be checked in the server connection window. But this is safe at least.
                }
                return true;
            }
        } 
        
        protected bool destroyConnection(Connection conn)
        {
            lock (connections)
            {
                if (!connections.Contains(conn))
                {
                    throw new Exception("Camera operator requesting destruction of a connection it does not own?");
                    return false;
                }
                if (connections.Count <= getRequiredConnectionCount())
                {
                    throw new Exception("Camera operator requesting destruction of a connection that is required.");
                    return false;
                }
                conn.CameraOperator = null;
                connections.Remove(conn);
                bool retVal = false;
                Application.Current.Dispatcher.Invoke(()=> {
                    retVal = connectionProvider.requestConnectionDestruction(conn);
                });
                return retVal;
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

        public void provideConnectionProvider(ConnectionProvider connProviderA)
        {
            if (connProviderA == null)
            {
                throw new Exception("Need to provide a valid ServerSharedInformationPool, not a null.");
            }
            connectionProvider = connProviderA;
        }

        // This must be called after connections and the shared information pool is provided
        // It is the "true" constructor (from the outside perspective) if you will.
        virtual public void Initialize() {
            //if (connections == null)
            if (connections.Count() < getRequiredConnectionCount())
            {
                throw new Exception("Cannot initialize CameraOperator before providing connections.");
            }
            if (infoPool == null)
            {
                throw new Exception("Cannot initialize CameraOperator before providing ServerSharedInformationPool.");
            }
            if (connectionProvider == null)
            {
                throw new Exception("Cannot initialize CameraOperator before providing ConnectionProvider.");
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

        virtual public void OpenDialog()
        {
            // Camera Operators can implement this if they want, but they don't have to.
            // They can use it to show some kind of dialogue for configuration.
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
