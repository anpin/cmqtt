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
    public class MqttNetworkChannel : IMqttNetworkChannel
    {
       
        // socket for communication
        private ICrestronTcpServer socket;
        public SocketStatus Status
        {
            get
            {
                return socket.GetServerSocketStatusForSpecificClient(ClientId);
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

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="socket">Socket opened with the client</param>
        /// <param name="secure">Secure connection (SSL/TLS)</param>
        /// <param name="serverCert">Server X509 certificate for secure connection</param>
        /// <param name="sslProtocol">SSL/TLS protocol version</param>
        public MqttNetworkChannel(ICrestronTcpServer socket, uint clientIndex, int connectTimeout)
        {
            this.socket = socket;
            //this.RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            this.clientIndex = clientIndex;
            this.connectTimeout = connectTimeout;
            this.thread = Fx.StartThread(this.ListenerThread);
           
        }
        private int _totalBytesReceived = 0;
        //CMutex dataLock = new CMutex();
        List<byte> DataStream = new List<byte>();
        Thread thread;
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
        private object ListenerThread(object o)
        {
            // ...and start it
            while (!this._closed && socket.GetServerSocketStatusForSpecificClient(clientIndex) == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                try
                {
                    if (rxMutex)
                    {
                        Thread.Sleep(50);
                    }
                    else
                    {
                        receive(this.socket);
                    }

                }
                catch (Exception e)
                {
                    if (this._closed)
                        return null;
                    else
                        MqttUtility.Trace.Error("MqttNetworkChannel> Exception in the ListnerThread {0} {1}", e.Message, e.StackTrace);
                }
            }
            return null;
        }
        bool rxMutex = false;
        void receive(ICrestronTcpServer s)
        {
#if TRACE
            MqttUtility.Trace.Debug("receive is called client: [{0}] rxMutex is  [{1}]", clientIndex, rxMutex);
#endif
            if (rxMutex)
            {
                return;
            }
            rxMutex = true;
            try
            {
                if (s.GetServerSocketStatusForSpecificClient(clientIndex) == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    var error = s.ReceiveDataAsync(clientIndex, Server_RecieveDataCallBack);
#if TRACE
                    MqttUtility.Trace.Debug("ReceiveDataAsync: client: [{0}] returned [{1}]", clientIndex, error);
#endif
                    if (error != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    {
                        rxMutex = false;
                    }
                }
            }
            catch (Exception ex)
            {
                MqttUtility.Trace.Error("Error in receive thread {0} {1}", ex.Message, ex.StackTrace);
                rxMutex = false;
            }

        }
        private void Server_RecieveDataCallBack(ICrestronTcpServer s, uint newClientIndex, int numberOfBytesReceived)
        {
            SocketStatus st = SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST;
            try
            {
                st = s.GetServerSocketStatusForSpecificClient(newClientIndex);
#if TRACE
                MqttUtility.Trace.Debug("ReceiveDataCallBack: client: [{0}] length: [{1}] status: [{2}]", newClientIndex, numberOfBytesReceived, st);
#endif

                if (numberOfBytesReceived > 0)//&& st == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _totalBytesReceived += numberOfBytesReceived;
                    byte[] recvd_bytes = new byte[numberOfBytesReceived];
                    Array.Copy(s.GetIncomingDataBufferForSpecificClient(newClientIndex), recvd_bytes, numberOfBytesReceived);
                    lock (DataStream)
                    {
                        DataStream.AddRange(recvd_bytes);
                    }
                }
            }
            finally
            {
                rxMutex = false;
#if TRACE
                MqttUtility.Trace.Debug("ReceiveDataCallBack: client: [{0}] finished, buffer: [{1}], total: [{2}], status: [{3}]", newClientIndex, DataStream.Count, _totalBytesReceived, st);
#endif
            }
        }
        public byte[] Receive(int l, out int received)
        {
            lock(DataStream)
            {
                if (l > DataStream.Count || _closed)
                {
                    received = 0;
                    return new byte[0];
                }
                var bytes = new byte[l];
                var i = 0;
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
        /// Send data on the network channel
        /// </summary>
        /// <param name="buffer">Data buffer to send</param>
        /// <returns>Number of byte sent</returns>
        public SocketErrorCodes Send(byte[] buffer, Action<int> sentCallback)
        {
            if (_closed)
                return SocketErrorCodes.SOCKET_NOT_CONNECTED;
            return this.socket.SendDataAsync(this.clientIndex, buffer, 0, buffer.Length, (s, clientIndex, numberOfBytes) =>
                {
                    if(sentCallback != null)
                        sentCallback.Invoke(numberOfBytes);
                });
        }
        bool _closed;
        /// <summary>
        /// Close the network channel
        /// </summary>
        public void Close()
        {
#if TRACE
            MqttUtility.Trace.Debug("NetworkChannel [{0}] is closing", clientIndex);
#endif
            _closed = true;
            lock (DataStream)
            {
                DataStream.Clear();
            }
            thread.Join();
            //socket.Disconnect(clientIndex);
#if TRACE
            MqttUtility.Trace.Debug("NetworkChannel [{0}] was closed", clientIndex);
#endif
        }
        //public void Dispose()
        //{
        //    if (IsDisposed)
        //        return;
        //    lock (DataStream)
        //    {
        //        this.DataStream = null;
        //    }
        //    this.socket = null;
        //    this.thread.Abort();
        //    IsDisposed = true;
        //}
        //public bool IsDisposed { get; private set; }
    }

}