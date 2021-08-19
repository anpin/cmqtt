using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;
namespace CMQTT
{
    class TcpClient: TCPClient, ICrestronTcpClient
    {
        public bool Secure
        {
            get
            {
                return false;
            }
        }
        public void SetClientPrivateKey(byte[] key)
        {
            throw new NotSupportedException();
        }
        public void SetClientCertificate(X509Certificate cert)
        {
            throw new NotSupportedException();
        }
        public SocketErrorCodes ConnectToServerAsync(ClientConnectedCallback callback)
        {
            return base.ConnectToServerAsync((s) => { if (callback != null) callback(this); });
        }
        public SocketErrorCodes ReceiveDataAsync(ClientReceiveDataCallback callback)
        {
            return base.ReceiveDataAsync((s, i) => { if (callback != null) callback(this, i); });
        }
        public SocketErrorCodes SendDataAsync(byte[] buffer, int offset, int length, ClientSendDataCallback callback)
        {
            return base.SendDataAsync(buffer, offset, length, (s,n) => { if (callback != null) callback(this, n); });
        }
        public TcpClient(IPEndPoint endPointToConnectTo, int bufferSize):base(endPointToConnectTo,bufferSize)
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
