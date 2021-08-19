using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;
namespace CMQTT
{
    class SecureTcpClient: SecureTCPClient, ICrestronTcpClient
    {
        public bool Secure
        {
            get
            {
                return true;
            }
        }
        public SocketErrorCodes ConnectToServerAsync(ClientConnectedCallback callback)
        {
            return this.ConnectToServerAsync((s) => callback(this));
        }
        public SocketErrorCodes ReceiveDataAsync(ClientReceiveDataCallback callback)
        {
            return this.ReceiveDataAsync((s, i) => callback(this, i));
        }
        public SocketErrorCodes SendDataAsync(byte[] buffer, int offset, int length, ClientSendDataCallback callback)
        {
            return this.SendDataAsync(buffer, offset, length, (s,n) => callback(this, n));
        }
        public SecureTcpClient(IPEndPoint endPointToConnectTo, int bufferSize):base(endPointToConnectTo,bufferSize)
        {

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
    }
}
