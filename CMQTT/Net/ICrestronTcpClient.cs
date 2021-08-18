using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;

namespace CMQTT
{
    public interface ICrestronTcpClient 
    {
        SocketStatus ClientStatus { get; }
  
        bool Secure { get; }
        void SetClientPrivateKey(byte[] key);
        void SetClientCertificate(X509Certificate cert);
        SocketErrorCodes SendDataAsync(byte[] buffer, int offset, int length, ClientSendDataCallback callback);
        SocketErrorCodes ConnectToServerAsync(ClientConnectedCallback callback);
        SocketErrorCodes ReceiveDataAsync(ClientReceiveDataCallback callback);
        byte[] IncomingDataBuffer { get; }
    }
    public delegate void ClientSendDataCallback(ICrestronTcpClient s, int numberOfBytesSent);
    public delegate void ClientConnectedCallback(ICrestronTcpClient s);
    public delegate void ClientReceiveDataCallback(ICrestronTcpClient s, int numberOfBytesReceived);
}
