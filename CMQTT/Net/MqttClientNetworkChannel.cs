/*
Copyright (c) 2013, 2014 Paolo Patierno

All rights reserved. This program and the accompanying materials
are made available under the terms of the Eclipse Public License v1.0
and Eclipse Distribution License v1.0 which accompany this distribution. 

The Eclipse Public License is available at 
   http://www.eclipse.org/legal/epl-v10.html
and the Eclipse Distribution License is available at 
   http://www.eclipse.org/org/documents/edl-v10.php.

Contributors:
   Paolo Patierno - initial API and implementation and/or initial documentation
   Pavel Anpin - port to Crestron SIMPL# framework
*/

using System;
using System.Linq;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharp.Cryptography.X509Certificates;
using Crestron.SimplSharp.Cryptography;
using Crestron.SimplSharp.CrestronIO;
using CMQTT.Utility;

// alias needed due to Microsoft.SPOT.Trace in .Net Micro Framework
// (it's ambiguos with CMQTT.Utility.Trace)
using MqttUtility = CMQTT.Utility;

namespace CMQTT
{
    /// <summary>
    /// Channel to communicate over the network
    /// </summary>
    public class MqttClientNetworkChannel : IMqttClientNetworkChannel
    {
        
        // socket for communication
        private ICrestronTcpClient socket;
        public SocketStatus Status
        {
            get
            {
                return socket != null ? socket.ClientStatus : SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST;
            }
        }
		// IP Address of the client connected to Broker
		//public IPEndPoint RemoteEndPoint;
        private uint clientIndex;
        public uint ClientId
        {
            get
            {
                return clientIndex;
            }
        }

        // Connection timeout for ssl authentication
        private int connectTimeout;

        /// <summary>
        /// Data available on the channel
        /// </summary>
        //public bool DataAvailable
        //{
        //    get
        //    {
        //        return this.socket.GetServerSocketStatusForSpecificClient(clientIndex) == SocketStatus.SOCKET_STATUS_CONNECTED && 
        //                this.socket.GetIfDataAvailableForSpecificClient(clientIndex);
        //    }
        //}
        readonly IPEndPoint RemoteEndPoint;
        readonly int bufSize;
        private int _totalBytesReceived = 0;
        List<byte> DataStream = new List<byte>();
        readonly bool secure;
        readonly X509Certificate cert;
        readonly byte[] pk;
        bool isRunning = false;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="socket">Socket opened with the client</param>
        /// <param name="secure">Secure connection (SSL/TLS)</param>
        /// <param name="serverCert">Server X509 certificate for secure connection</param>
        /// <param name="privateKey">Private Key corresponding to the Server Certificate in binary (DER) format</param>
        public MqttClientNetworkChannel(IPEndPoint endpoint, uint clientIndex, int connectTimeout, int bufSize, bool secure, X509Certificate cert, byte[] privateKey)
        {
           
            this.RemoteEndPoint = endpoint;
            this.bufSize = bufSize;
            this.clientIndex = clientIndex;
            this.connectTimeout = connectTimeout;
            this.secure = secure;
            if (secure)
            {
                this.cert = cert;
                this.pk = privateKey;
            }
        }
#if TRACE
        public byte[] Dump
        {
            get
            {
                lock(DataStream)
                {
                    return DataStream.ToArray();
                }
                
            }
        }
#endif
        void receive(ICrestronTcpClient s)
        {
            var error = s.ReceiveDataAsync(RecieveDataCallBack);
#if TRACE
            MqttUtility.Trace.Debug("ReceiveDataAsync: client: [{0}] returned [{1}]", clientIndex, error);
#endif

        }
        private void RecieveDataCallBack(ICrestronTcpClient s, int numberOfBytesReceived)
        {
            try
            {
                //var a = DataAvailable;
#if TRACE
                MqttUtility.Trace.Debug("ReceiveDataCallBack: client: [{0}] length: [{1}] status: [{2}]", clientIndex, numberOfBytesReceived, Status);
#endif
                
                if (numberOfBytesReceived > 0 && Status == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _totalBytesReceived += numberOfBytesReceived;
                    byte[] recvd_bytes = new byte[numberOfBytesReceived];
                    Array.Copy(s.IncomingDataBuffer, recvd_bytes, numberOfBytesReceived);

                    lock(DataStream)
                    {
                        DataStream.AddRange(recvd_bytes);
                    }
                }
            }
            finally
            {
#if TRACE
                MqttUtility.Trace.Debug("ReceiveDataCallBack: client: [{0}] finished, buffer: [{1}], total: [{2}]", clientIndex, DataStream.Count, _totalBytesReceived);
#endif    
                if (Status == SocketStatus.SOCKET_STATUS_CONNECTED || Status == SocketStatus.SOCKET_STATUS_WAITING)
                    receive(s);

            }
        }
        public byte[] Receive(int l, out int received)
        {
            lock (DataStream)
            {
                if (l > DataStream.Count)
                {
                    received = 0;
                    return new byte[0];
                }
                var bytes = new byte[l];
                var i = 0;
                //channel.DataStream.Seek(0, SeekOrigin.Begin);
                for (i = 0; i < l; i++)
                {
                    var b = DataStream[0];
                    DataStream.RemoveAt(0);
                    bytes[i] = (byte)b;
                }
                received = i;
                return bytes;
            }
        }

        /// <summary>
        /// Connect to remote server
        /// </summary>
        /// 
        
        public void Connect(Action callback)
        {
#if TRACE 
            MqttUtility.Trace.WriteLine(TraceLevel.Verbose, "LocalClient [{0}] is trying to connect", clientIndex);
#endif
            if (Status == SocketStatus.SOCKET_STATUS_CONNECTED && isRunning)
            {
                Close();
            }
            // try connection to the broker
            if (this.secure)
            {
                this.socket = new SecureTcpClient(RemoteEndPoint, bufSize);
                this.socket.SetClientCertificate(cert);
                this.socket.SetClientPrivateKey(pk);
            }
            else
            {
                this.socket = new TcpClient(RemoteEndPoint, bufSize);
            }
            isRunning = true;
            var error = this.socket.ConnectToServerAsync((s) =>
            {
                if (s.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    callback.BeginInvoke((r) => { }, null);
                    receive(this.socket);
                }
                else
                {
                    MqttUtility.Trace.WriteLine(TraceLevel.Error, "MqttClientNetworkChannel> Connection to {0}:{1} failed {2}", RemoteEndPoint.Address.ToString(), RemoteEndPoint.Port, s.ClientStatus.ToString());
                }
                
            });
#if TRACE
            MqttUtility.Trace.Debug("MqttClientNetworkChannel ConnectToServerAsync to {0}:{1} returned {2}", RemoteEndPoint.Address.ToString(), RemoteEndPoint.Port, error);
#endif           
        }
        /// <summary>
        /// Send data on the network channel
        /// </summary>
        /// <param name="buffer">Data buffer to send</param>
        /// <returns>Number of byte sent</returns>
        public SocketErrorCodes Send(byte[] buffer, Action<int> sentCallback)
        {
            return this.socket.SendDataAsync(buffer, 0, buffer.Length, (s, numberOfBytes) =>
                {
#if TRACE
            MqttUtility.Trace.Debug("MqttClientNetworkChannel> sent {0} bytes", numberOfBytes);
#endif     
                    if(sentCallback != null)
                        sentCallback.Invoke(numberOfBytes);
                });
        }
        /// <summary>
        /// Receive data from the network
        /// </summary>
        /// <param name="buffer">Data buffer for receiving data</param>
        /// <returns>Number of bytes received</returns>
        //public int Receive(byte[] buffer)
        //{

        //    if (_closed)
        //        return 0;
        //    if(this.socket.GetServerSocketStatusForSpecificClient(this.clientIndex) != SocketStatus.SOCKET_STATUS_CONNECTED)
        //        return 0;
        //    if (this.socket.GetIfDataAvailableForSpecificClient(this.clientIndex) == false)
        //        return 0;
        //    // read all data needed (until fill buffer)
        //    int idx = 0, read = 0;
        //    while (idx < buffer.Length)
        //    {
        //        // fixed scenario with socket closed gracefully by peer/broker and
        //        // Read return 0. Avoid infinite loop.

        //        read = this.ms.ReadByte();
        //        if (read == -1)
        //            return idx;
        //        System.Buffer.BlockCopy(new byte[] {(byte)read}, 0, buffer, idx, 1);
        //        idx += 1;
        //    }
        //    return buffer.Length;
        //}
        /// <summary>
        /// Close the network channel
        /// </summary>
        public void Close()
        {
#if TRACE
            MqttUtility.Trace.Debug("MqttClientNetworkChannel> [{0}] Close was called", ClientId);
#endif
            isRunning = false;
            lock (DataStream)
            {
                DataStream.Clear();
            }
            socket.DisconnectFromServer();
            socket.Dispose();
            
        }
    }

}