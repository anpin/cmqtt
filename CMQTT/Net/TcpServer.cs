using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;
using System;
namespace CMQTT
{
    class TcpServer : TCPServer, ICrestronTcpServer
    {
        public bool Secure
        {
            get
            {
                return false;
            }
        }
        public void SetServerPrivateKey(byte[] key)
        {
            throw new NotSupportedException();
        }
        public void SetServerCertificate(X509Certificate cert)
        {
            throw new NotSupportedException();
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
        public TcpServer(string addressToAcceptConnectionFrom, int portNumber, int bufferSize, EthernetAdapterType ethernetAdapterToBindTo, int numberOfConnections)
            : base(addressToAcceptConnectionFrom,portNumber,bufferSize,ethernetAdapterToBindTo, numberOfConnections)
        {
            this.SocketStatusChange += SecureTcpServer_SocketStatusChange;
        }

        private void SecureTcpServer_SocketStatusChange(TCPServer mySecureTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            if (ClientStatusChange != null)
                ClientStatusChange.Invoke(this, clientIndex, serverSocketStatus);
        }
    }
}
