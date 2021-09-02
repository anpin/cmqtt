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
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography.X509Certificates;
using Crestron.SimplSharpPro.CrestronThread;
using MqttUtility = CMQTT.Utility;
namespace CMQTT.Communication
{
    /// <summary>
    /// MQTT communication layer
    /// </summary>
    public class MqttTcpCommunicationLayer : IMqttCommunicationLayer
    {
        #region Constants ...

        // name for listener thread
        private const string LISTENER_THREAD_NAME = "MqttListenerThread";
        // option to accept only connection from IPv6 (or IPv4 too)
        private const int IPV6_V6ONLY = 27;

        #endregion

        #region Properties ...

        /// <summary>
        /// TCP listening port
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// Listner buffer size
        /// </summary>
        public int BufferSize { get; private set; }
        /// <summary>
        /// NumberOfClients Allowed
        /// </summary>
        public int NumberOfConnections { get; private set; }

        /// <summary>
        /// Secure connection (SSL/TLS)
        /// </summary>
        public bool Secure { get; private set; }

        /// <summary>
        /// X509 Server certificate
        /// </summary>
        public X509Certificate ServerCert { get; private set; }
        #endregion

        // TCP listener for incoming connection requests
        private ICrestronTcpServer listener;

        // TCP listener thread
        private Thread thread;
        private bool isRunning;

        // Connection timeout for ssl authentication
        private int connectTimeout;
        private bool _waiting;
        private bool _chilling;
        readonly Dictionary<uint, MqttClient> _clientList = new Dictionary<uint, MqttClient>();
        readonly Dictionary<uint, SocketStatus> _clientStatusList = new Dictionary<uint, SocketStatus>();



        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="port">TCP listening port</param>
        /// <param name="connectTimeout">connection timeout in ms</param>
        /// <param name="numberOfConnections">Max number of clients</param>
        public MqttTcpCommunicationLayer(int port, int bufferSize, int connectTimeout, int numberOfConnections) : this(port, bufferSize, connectTimeout, numberOfConnections, false, null, null)
        {
        }
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="port">TCP listening port</param>
            /// <param name="connectTimeout">connection timeout in ms</param>
            /// <param name="numberOfConnections">Max number of clients</param>
            /// <param name="secure">Pass true to indicate that we are using SSL</param>
            /// <param name="serverCert">X509 server certificate</param>
            /// <param name="privateKey">Private Key corresponding to the Server Certificate in binary (DER) format</param>
            public MqttTcpCommunicationLayer(int port, int bufferSize, int connectTimeout, int numberOfConnections, bool secure, X509Certificate serverCert, byte[] privateKey)
        {
            if (secure && serverCert == null)
                throw new ArgumentException("Secure connection requested but no server certificate provided");
            if (secure && serverCert == null)
                throw new ArgumentException("Secure connection requested but no private key provided");
            this.Secure = secure;
            this.Port = port;
            this.connectTimeout = connectTimeout;
            this.NumberOfConnections = numberOfConnections;
            this.BufferSize = bufferSize * NumberOfConnections;
            // With IPAddress.IPv6Any it doesn't works correctly on WinCE

#if TRACE
            MqttUtility.Trace.Debug("Server> Init with  {0} {1} {2} {3} {4}", "0.0.0.0", this.Port, ushort.MaxValue, EthernetAdapterType.EthernetUnknownAdapter, this.NumberOfConnections);
#endif
            if (Secure)
            {
                this.listener = new SecureTcpServer("0.0.0.0", this.Port, this.BufferSize, EthernetAdapterType.EthernetUnknownAdapter, this.NumberOfConnections);
                this.listener.SetServerCertificate(serverCert);
                this.listener.SetServerPrivateKey(privateKey);
            }
            else
            {
                this.listener = new TcpServer("0.0.0.0", this.Port, this.BufferSize, EthernetAdapterType.EthernetUnknownAdapter, this.NumberOfConnections);
            }
            this.listener.ClientStatusChange += _server_SocketStatusChange;
        }
        void updateClientStatus(uint clientIndex, SocketStatus status)
        {
            _clientStatusList[clientIndex] = status;
        }
        bool checkIfChangeIsDuplicate(uint clientIndex, SocketStatus status)
        {
            return _clientStatusList.ContainsKey(clientIndex) && _clientStatusList[clientIndex] == status;
        }
        void _server_SocketStatusChange(ICrestronTcpServer myICrestronTcpServer, uint clientIndex, SocketStatus serverSocketStatus)
        {
            try
            {
#if TRACE
                MqttUtility.Trace.Debug("Server> Socket status change [{0}|{1}/{2}] :: {3}", clientIndex, listener.NumberOfClientsConnected, listener.MaxNumberOfClientSupported, serverSocketStatus.ToString());
#endif
                if(checkIfChangeIsDuplicate(clientIndex, serverSocketStatus))
                {
#if TRACE
                    MqttUtility.Trace.Debug("Server> Socket status change is duplicate skip processing  [{0}|{1}/{2}] :: {3}", clientIndex, listener.NumberOfClientsConnected, listener.MaxNumberOfClientSupported, serverSocketStatus.ToString());
#endif
                    return;
                }
                updateClientStatus(clientIndex, serverSocketStatus);
                if (serverSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED )
                {
//#if TRACE

                    
//                    MqttUtility.Trace.Debug("Server> Socket status change trying to disconnect [{0}]", clientIndex);
//#endif
//                    var derror =  myICrestronTcpServer.Disconnect(clientIndex);
//#if TRACE
//                    MqttUtility.Trace.Debug("Server> Socket status change disconnect returned [{0}] [{1}]", clientIndex, derror);
//#endif

#if TRACE
                    MqttUtility.Trace.Debug("Server> Socket status change trying to clean [{0}]", clientIndex);
#endif
                    OnClientDisconnected(clientIndex, serverSocketStatus);
                    if (!_waiting)
                        this.CheckForWaitingConnection();
                }
            }
            catch(Exception ex)
            {
                MqttUtility.Trace.Error("Server> Exception in SocketStatusChange handler {0} {1}", ex.Message, ex.StackTrace);
            }
            //_serverSocketStatus(null, new ServerTcpSocketStatusEventArgs(clientIndex, serverSocketStatus, _numberOfClientsConnected));
        }

        #region IMqttCommunicationLayer ...

        // client connected event
        public event MqttClientConnectedEventHandler ClientConnected;
        public event MqttClientDisconnectedEventHandler ClientDisconnected;

        /// <summary>
        /// Start communication layer listening
        /// </summary>
        public void Start()
        {
            this.isRunning = true;

            // create and start listener thread
            this.thread = Fx.StartThread(this.ListenerThread);
        }

        /// <summary>
        /// Stop communication layer listening
        /// </summary>
        public void Stop()
        {
            this.isRunning = false;

            this.listener.Stop();
            this.listener.DisconnectAll();

            // wait for thread
            this.thread.Join();
        }

        #endregion

        /// <summary>
        /// Listener thread for incoming connection requests
        /// </summary>
        private object ListenerThread(object o)
        {
            // ...and start it
            while (this.isRunning)
            {
                try
                {
                    
                    if (_waiting || _chilling)
                    {
                        Thread.Sleep(100);
                    }
                    else
                    {
                        CheckForWaitingConnection();
                    }

                }
                catch (Exception e)
                {
                    if (!this.isRunning)
                        return null;
                    else
                        MqttUtility.Trace.Error("Server> Exception in the ListnerThread {0} {1}", e.Message, e.StackTrace);
                }
            }
            return null;
        }
        void connectedCallback(ICrestronTcpServer s, uint clientIndex)
        {
            if (!isRunning)
                return;
            try
            {
                _waiting = false;
                this.CheckForWaitingConnection();
//                if (clientIndex == 0)
//                {
//#if TRACE
//                    MqttUtility.Trace.Debug("Server> Incoming Connection: [{0}|{1}/{2}] :: skipping zero", clientIndex, s.NumberOfClientsConnected, s.MaxNumberOfClientSupported);
//#endif
//                    return;
//                }
                var status = s.GetServerSocketStatusForSpecificClient(clientIndex);
                //int waitttt = 10;
                //while (status == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST && --waitttt > 0)
                //{
                //    Thread.Sleep(500);
                //    status = s.GetServerSocketStatusForSpecificClient(clientIndex);
                //}
#if TRACE
                MqttUtility.Trace.Debug("Server> Incoming Connection: [{0}|{1}/{2}] :: SocketStatus: {3}", clientIndex, s.NumberOfClientsConnected, s.MaxNumberOfClientSupported, status);
#endif
                // manage socket client connected
                if (status == SocketStatus.SOCKET_STATUS_CONNECTED)
                {

                    // create network channel to accept connection request
                    IMqttNetworkChannel channel = new MqttNetworkChannel(s, clientIndex, this.connectTimeout);
                    
                    // handling channel for connected client
                    MqttClient client = new MqttClient(channel);
                    // raise client raw connection event

                    this.OnClientConnected(client);

                }
                else
                {
#if TRACE
                    MqttUtility.Trace.Debug("Server> Incoming Connection: [{0}|{1}/{2}] :: ClientConnected returned false", clientIndex, s.NumberOfClientsConnected, s.MaxNumberOfClientSupported);
#endif
                    //    s.Disconnect(clientIndex);
                    //    if (_clientList.ContainsKey(clientIndex))
                    //    {
                    //        var con = _clientList[clientIndex] as MqttClient;
                    //        con.Close();
                    //        _clientList.Remove(clientIndex);
                    //    }
                }
            }
            catch(Exception ex)
            {
                MqttUtility.Trace.Error("Server> Exception is connectedCallback {0} {1}", ex.Message, ex.StackTrace);
            }
        }
        int inProgressCounter = 0;
        private void CheckForWaitingConnection()
        {

            
#if TRACE
            MqttUtility.Trace.Debug("Server> CheckForWaitingConnection :: Waiting: {0} Running {1} Chilling {2}", _waiting, isRunning, _chilling);
#endif
            if (_waiting || _chilling || !isRunning)
                return;
            if (listener.NumberOfClientsConnected < listener.MaxNumberOfClientSupported)
            {
                //listener.Stop();
                //Thread.Sleep(100);
                SocketErrorCodes error = listener.WaitForConnectionAsync(IPAddress.Parse("0.0.0.0"), connectedCallback);
#if TRACE
                MqttUtility.Trace.Debug("Server> WaitForConnectionAsync: [{0}/{1}] :: returned: {2}", listener.NumberOfClientsConnected, listener.MaxNumberOfClientSupported, error.ToString());
#endif
                _waiting = error == SocketErrorCodes.SOCKET_OPERATION_PENDING;
                if (error == SocketErrorCodes.SOCKET_CONNECTION_IN_PROGRESS)
                {
                    inProgressCounter++;
                    if(inProgressCounter > 20)
                    {
#if TRACE
                        MqttUtility.Trace.Debug("Server> stopping listner");
#endif
                        listener.Stop();
                        inProgressCounter = 0;
                    }
                    Thread.Sleep(300);
                }
                else
                {
                    inProgressCounter = 0;
                }

            }
        }
        /// <summary>
        /// Raise client connected event
        /// </summary>
        /// <param name="e">Event args</param>
        private void OnClientConnected(MqttClient client)
        {
            if (this.ClientConnected != null)
                this.ClientConnected(this, new MqttClientConnectedEventArgs(client));
        }
        /// <summary>
        /// Raise client connected event
        /// </summary>
        /// <param name="e">Event args</param>
        private void OnClientDisconnected(uint client, SocketStatus status)
        {
            if (this.ClientDisconnected != null)
                this.ClientDisconnected(this, new MqttClientDisconnectedEventArgs(client, status));
        }
    }
}