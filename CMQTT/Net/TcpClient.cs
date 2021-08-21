using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;
using trace = CMQTT.Utility.Trace;
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
            return base.ConnectToServerAsync((s) => {
                try
                {
                    if (callback != null) callback(this);
                }
                catch (Exception ex)
                {
                    trace.Error("ConnectToServerAsync callback", ex);
                }
            });
        }
        public SocketErrorCodes ReceiveDataAsync(ClientReceiveDataCallback callback)
        {
            return base.ReceiveDataAsync((s, i) =>
            {
                try { if (callback != null) callback(this, i); }
                catch (Exception ex)
                {
                    trace.Error("ReceiveDataAsync callback", ex);
                }
            });
        }
        public SocketErrorCodes SendDataAsync(byte[] buffer, int offset, int length, ClientSendDataCallback callback)
        {
            return base.SendDataAsync(buffer, offset, length, (s, n) =>
            {
                try { if (callback != null) callback(this, n); }
                catch (Exception ex)
                {
                    trace.Error("SendDataAsync callback", ex);
                }
            });
        }
        public TcpClient(IPEndPoint endPointToConnectTo, int bufferSize):base(endPointToConnectTo,bufferSize)
        {
            this.Nagle = true;
            SocketStatusChange += new TCPClientSocketStatusChangeEventHandler(TcpClient_SocketStatusChange);
            CrestronEnvironment.EthernetEventHandler += EthernetEventHandler;
        }

        public event ClientSocketStatusChangeEventHandler OnSocketStatusChange;
        void TcpClient_SocketStatusChange(TCPClient myTCPClient, SocketStatus clientSocketStatus)
        {
#if TRACE 
            trace.WriteLine(CMQTT.Utility.TraceLevel.Information, "TcpClient {0}:{1} socket status change {2}", this.AddressClientConnectedTo, this.PortNumber, clientSocketStatus);
#endif
            if (OnSocketStatusChange != null)
            {
                OnSocketStatusChange.Invoke(this, clientSocketStatus);
            }
        }
        void EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            trace.WriteLine(CMQTT.Utility.TraceLevel.Information, "TcpCLient [{0:1}] Ethernet addapter status changed to {2}", this.AddressClientConnectedTo, this.PortNumber, ethernetEventArgs);
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
