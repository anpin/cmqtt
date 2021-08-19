using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace CMQTT
{
    class SecureTcpServer : SecureTCPServer, ICrestronTcpServer
    {
        public bool Secure
        {
            get
            {
                return true;
            }
        }
        public event ServerSocketStatusChangeEventHandler ClientStatusChange;

        public SocketErrorCodes WaitForConnectionAsync(IPAddress ip, ServerWaitForConnectionCallback callback)
        {
            return base.WaitForConnectionAsync(ip, (s, i) => { if (callback != null) callback(this, i); });
        }
        public SocketErrorCodes ReceiveDataAsync(uint clientIndex, ServerRecieveDataCallback callback)
        {
            return base.ReceiveDataAsync(clientIndex, (s, i, n) => { if (callback != null) callback(this, i, n); });
        }
        public SocketErrorCodes SendDataAsync(uint clientIndex, byte[] buffer, int offset, int length, ServerSendDataCallBack callback)
        {
            return base.SendDataAsync(clientIndex, buffer, offset, length, (s, i, n) => { if (callback != null) callback(this, i, n); });
        }
        public SecureTcpServer(string addressToAcceptConnectionFrom, int portNumber, int bufferSize, EthernetAdapterType ethernetAdapterToBindTo, int numberOfConnections)
            : base(addressToAcceptConnectionFrom,portNumber,bufferSize,ethernetAdapterToBindTo, numberOfConnections)
        {
            this.SocketStatusChange += SecureTcpServer_SocketStatusChange;
            CrestronEnvironment.EthernetEventHandler += EthernetEventHandler;
        }
        void EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {
                case (eEthernetEventType.LinkDown):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        HandleLinkLoss();
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        HandleLinkUp();
                    }
                    break;
            }
        }

        private void SecureTcpServer_SocketStatusChange(SecureTCPServer mySecureTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            if (ClientStatusChange != null)
                ClientStatusChange.Invoke(this, clientIndex, serverSocketStatus);
        }
    }
}
