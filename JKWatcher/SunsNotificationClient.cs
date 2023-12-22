using JKClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{
    class NotificationEventArgs : EventArgs
    {
        public NetAddress Address { get; init; }
        public string Key { get; init; }

        public NotificationEventArgs(NetAddress address, string key)
        {
            Address = address;
            Key = key;
        }
    }

    static class SunsNotificationClient
    {
        public static event EventHandler<NotificationEventArgs> sunsNotificationReceived;

        private static void OnSunsNotificationReceived(NetAddress address, string key)
        {
            sunsNotificationReceived?.Invoke(null,new NotificationEventArgs(address,key));
        }

        static Socket socket = null;
        static IPEndPoint endPoint = null;
        private static void InitSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false, EnableBroadcast = true };
            try
            {
                endPoint = new IPEndPoint(IPAddress.Any, 0);
                socket.Bind(endPoint);
                Debug.WriteLine($"NOTE: SUNS notification client bound to port {endPoint.Port}.");
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.AddressAlreadyInUse:
                        Debug.WriteLine($"ERROR: Unable to bind SUNS notification client.");
                        break;
                }

            }

        }

        public static unsafe void Subscribe(NetAddress sunsServer, string key)
        {
            bool tryForMore = true;
            while (tryForMore)
            {
                try
                {
                    byte[] messageData = Encoding.Latin1.GetBytes(key);
                    int sentWhat = socket.SendTo(messageData, sunsServer.ToIPEndPoint());
                    tryForMore = false;
                    if (sentWhat < messageData.Length)
                    {
                        Debug.WriteLine($"Tried to send subscription message to {sunsServer.ToString()} but only sent {sentWhat} out of {messageData.Length} bytes");
                    }
                    else
                    {
                        Debug.WriteLine($"Sent subscription message to {sunsServer.ToString()}");
                    }
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        default:
                            Debug.WriteLine($"Cannot send subscription message: {ex.ToString()}");
                            tryForMore = false;
                            break;
                        case SocketError.NotConnected:
                        case SocketError.Shutdown:
                            Debug.WriteLine($"ERROR: Suns notification socket is not connected/shut down. Attempting to bind again.");
                            InitSocket();
                            tryForMore = true;
                            break;
                    }

                }
            }
        }


        static unsafe void CheckForNewNotifications()
        {
            bool tryForMore = true;
            while (tryForMore)
            {
                tryForMore = false;
                try
                {
                    if (!socket.Poll(0, SelectMode.SelectRead))
                    {
                        return;
                    }
                    byte[] messageData = new byte[5000];
                    EndPoint fromWho = new IPEndPoint(0, 0);
                    int receivedWhat = socket.ReceiveFrom(messageData, SocketFlags.None, ref fromWho);
                    tryForMore = true;
                    string dataString = null;
                    fixed (byte* data = messageData)
                    {
                        dataString = Encoding.Latin1.GetString(data, receivedWhat);
                    }
                    Debug.WriteLine($"Received {receivedWhat} bytes from {fromWho.ToString()}");
                    IPEndPoint fromWhoIP = fromWho as IPEndPoint;
                    OnSunsNotificationReceived(new NetAddress(fromWhoIP.Address.GetAddressBytes(),(ushort)fromWhoIP.Port), dataString);
                }
                catch (SocketException e)
                {
                    switch (e.SocketErrorCode)
                    {
                        case SocketError.NotConnected:
                        case SocketError.Shutdown:
                            Debug.WriteLine($"ERROR (SUNS notification client): Socket is not connected/shut down. Attempting to bind again.");
                            InitSocket();
                            tryForMore = true;
                            break;
                    }

                }
            }
        }

        static SunsNotificationClient()
        {
            InitSocket();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            TaskManager.RegisterTask(Task.Factory.StartNew(() => { sunsMessageChecker(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {
                Helpers.logToFile(new string[] { t.Exception.ToString() });
            }, TaskContinuationOptions.OnlyOnFaulted), "CTF Auto Connecter");
        }

        static void sunsMessageChecker(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                CheckForNewNotifications();
            }
        }
    }
}
