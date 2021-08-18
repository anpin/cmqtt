using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Cryptography.X509Certificates;
namespace CMQTT
{
    /// <summary>
    /// Interface to abstarct SSL and non-SSL servers 
    /// </summary>
    public interface ICrestronTcpServer
    {
        void Stop();
        int MaxNumberOfClientSupported { get; }
        bool Nagle { get; set; }
        int NumberOfClientsConnected { get; }
        int SocketSendOrReceiveTimeOutInMs { get; set; }
        bool Secure { get; }
        void SetServerPrivateKey(byte[] key);
        void SetServerCertificate(X509Certificate cert);
        SocketErrorCodes Disconnect(uint id);
        void DisconnectAll();
        byte[] GetIncomingDataBufferForSpecificClient(uint id);
        SocketStatus GetServerSocketStatusForSpecificClient(uint id);
        SocketErrorCodes WaitForConnectionAsync(IPAddress ip, ServerWaitForConnectionCallback callback);
        SocketErrorCodes ReceiveDataAsync(uint clientIndex, ServerRecieveDataCallback callback);
        SocketErrorCodes SendDataAsync(uint clientIndex, byte[] buffer, int offset, int length, ServerSendDataCallBack callback);
        event ServerSocketStatusChangeEventHandler ClientStatusChange;
    }
    
    public delegate void ServerWaitForConnectionCallback(ICrestronTcpServer server, uint clientIndex);
    public delegate void ServerRecieveDataCallback(ICrestronTcpServer s, uint newClientIndex, int numberOfBytesReceived);
    public delegate void ServerSendDataCallBack(ICrestronTcpServer s, uint newClientIndex, int numberOfBytesSent);
    public delegate void ServerSocketStatusChangeEventHandler(ICrestronTcpServer s, uint clientindex, SocketStatus status);
}
