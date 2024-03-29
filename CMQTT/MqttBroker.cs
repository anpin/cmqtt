﻿/*
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
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using CMQTT.Messages;
using CMQTT.Exceptions;
using CMQTT.Managers;
using CMQTT.Communication;
using CMQTT.Session;
using MqttUtility = CMQTT.Utility;
using Crestron.SimplSharp.Cryptography.X509Certificates;

namespace CMQTT
{
    /// <summary>
    /// MQTT broker business logic
    /// </summary>
    public class MqttBroker
    {
        // MQTT broker settings
        private MqttSettings settings;

        // clients connected list
        private Dictionary<uint, MqttClient> clients;

        // reference to publisher manager
        private MqttPublisherManager publisherManager;

        // reference to subscriber manager
        private MqttSubscriberManager subscriberManager;

        // reference to session manager
        private MqttSessionManager sessionManager;

        // reference to User Access Control manager
        private MqttUacManager uacManager;

        // MQTT communication layer
        private IMqttCommunicationLayer commLayer;

        /// <summary>
        /// User authentication method
        /// </summary>
        public MqttUserAuthenticationDelegate UserAuth
        {
            get { return this.uacManager.UserAuth; }
            set { this.uacManager.UserAuth = value; }
        }

		// Notifications to application, client connected / disconnected
		public event Action<MqttClient> ClientConnected;
		public event Action<MqttClient> ClientDisconnected;
        public const int DefaultConnectTimeout = 500;
        public const int DefaultNumberOfClients = 10;
        /// <summary>
        /// Constructor (TCP/IP communication layer on port 1883 and default settings)
        /// </summary>
        public MqttBroker(int numberOfClients)
            : this(new MqttTcpCommunicationLayer(MqttSettings.MQTT_BROKER_DEFAULT_PORT,MqttSettings.MQTT_BROKER_DEFAULT_BUFFER_SIZE, DefaultConnectTimeout, numberOfClients == 0 ? DefaultNumberOfClients : numberOfClients), MqttSettings.Instance)
        {
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commLayer">Communication layer to use (TCP)</param>
        /// <param name="settings">Broker settings</param>
        public MqttBroker(IMqttCommunicationLayer commLayer, MqttSettings settings)
        {
            // MQTT broker settings
            this.settings = settings;

            // MQTT communication layer
            this.commLayer = commLayer;
            this.commLayer.ClientConnected += commLayer_ClientConnected;
            // create managers (publisher, subscriber, session and UAC)
            this.subscriberManager = new MqttSubscriberManager();
            this.sessionManager = new MqttSessionManager();
            this.publisherManager = new MqttPublisherManager(this.subscriberManager, this.sessionManager);
            this.uacManager = new MqttUacManager();

            this.clients = new Dictionary<uint, MqttClient>();
        }

        /// <summary>
        /// Start broker
        /// </summary>
        public void Start()
        {
            this.commLayer.Start();
            this.publisherManager.Start();
        }

        /// <summary>
        /// Stop broker
        /// </summary>
        public void Stop()
        {
#if TRACE
            MqttUtility.Trace.Debug("MqttBroker stop Was called");
#endif
            this.commLayer.Stop();
            this.publisherManager.Stop();

            // close connection with all clients

				foreach (MqttClient client in this.clients.Values)
                {
#if TRACE
                    MqttUtility.Trace.Debug("MqttBroker trying to stop [{0}]", client.ClientId);
#endif
                    CloseClient(client);
				}
        }

        /// <summary>
        /// Close a client
        /// </summary>
        /// <param name="client">Client to close</param>
        private void CloseClient(MqttClient client)
        {
#if TRACE
                MqttUtility.Trace.Debug("Broker> Closing client [{0}] [{1}]", client.SocketId, client.ClientId);
#endif
                // if client is connected and it has a will message
                if (client.IsConnected && client.WillFlag)
                {
                    // create the will PUBLISH message
                    MqttMsgPublish publish =
                        new MqttMsgPublish(client.WillTopic, Encoding.UTF8.GetBytes(client.WillMessage), false, client.WillQosLevel, false);

                    // publish message through publisher manager
                    this.publisherManager.Publish(publish);
                }

                // if not clean session
                if (!client.CleanSession)
                {
                    List<MqttSubscription> subscriptions = this.subscriberManager.GetSubscriptionsByClient(client.ClientId);

                    if ((subscriptions != null) && (subscriptions.Count > 0))
                    {
                        this.sessionManager.SaveSession(client.ClientId, client.Session, subscriptions);

                        // TODO : persist client session if broker close
                    }
                }

                // Waits end messages publication
                publisherManager.PublishMessagesEventEnd.Wait(this.settings.TimeoutOnReceiving);
                this.subscriberManager.Unsubscribe(client);

                // close the client
                if (!client.WasClosed)
                {
                    client.Stop();
                }
                else
                {

#if TRACE
                    MqttUtility.Trace.Debug("Broker> client was previously closed");
#endif
                }
            RemoveClient(client);
            
        

#if TRACE
            MqttUtility.Trace.Debug("Broker> Closed client");
#endif
        }
        void RemoveClient(MqttClient client)
        {
            // delete client from runtime subscription
            client.MqttMsgDisconnected -= Client_MqttMsgDisconnected;
            client.MqttMsgPublishReceived -= Client_MqttMsgPublishReceived;
            client.MqttMsgConnected -= Client_MqttMsgConnected;
            client.MqttMsgSubscribeReceived -= Client_MqttMsgSubscribeReceived;
            client.MqttMsgUnsubscribeReceived -= Client_MqttMsgUnsubscribeReceived;
            client.ConnectionClosed -= Client_ConnectionClosed;
            lock (clients)
            {
                if (clients.ContainsKey(client.SocketId))
                {
                    // remove client from the collection
                    this.clients.Remove(client.SocketId);
                }
            }
        }

    void commLayer_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            bool previousClientIsInTheList = false;
#if TRACE
            MqttUtility.Trace.Debug("Broker> commLayer_ClientConnected Was called [{0}]", e.Client.SocketId);
#endif
            lock(clients)
            {
               previousClientIsInTheList = this.clients.ContainsKey(e.Client.SocketId);
            }
            if (previousClientIsInTheList)
            {
                RemoveClient(this.clients[e.Client.SocketId]);
            }
            // register event handlers from client
            e.Client.MqttMsgDisconnected += Client_MqttMsgDisconnected;
            e.Client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            e.Client.MqttMsgConnected += Client_MqttMsgConnected;
            e.Client.MqttMsgSubscribeReceived += Client_MqttMsgSubscribeReceived;
            e.Client.MqttMsgUnsubscribeReceived += Client_MqttMsgUnsubscribeReceived;
            e.Client.ConnectionClosed += Client_ConnectionClosed;

			lock (clients)
			{
				// add client to the collection
				this.clients[e.Client.SocketId] = e.Client;
			}

            // start client threads
            e.Client.Open();
        }

        void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            MqttClient client = (MqttClient)sender;

            // create PUBLISH message to publish
            // [v3.1.1] DUP flag from an incoming PUBLISH message is not propagated to subscribers
            //          It should be set in the outgoing PUBLISH message based on transmission for each subscriber
            MqttMsgPublish publish = new MqttMsgPublish(e.Topic, e.Message, false, e.QosLevel, e.Retain);

            // publish message through publisher manager
            this.publisherManager.Publish(publish);
        }

        void Client_MqttMsgUnsubscribeReceived(object sender, MqttMsgUnsubscribeEventArgs e)
        {
            MqttClient client = (MqttClient)sender;

            for (int i = 0; i < e.Topics.Length; i++)
            {
                // unsubscribe client for each topic requested
                this.subscriberManager.Unsubscribe(e.Topics[i], client);
            }

            try
            {
                // send UNSUBACK message to the client
                client.Unsuback(e.MessageId);
            }
            catch (MqttCommunicationException)
            {
                this.CloseClient(client);
            }
        }

        void Client_MqttMsgSubscribeReceived(object sender, MqttMsgSubscribeEventArgs e)
        {
            MqttClient client = (MqttClient)sender;

            for (int i = 0; i < e.Topics.Length; i++)
            {
                // TODO : business logic to grant QoS levels based on some conditions ?
                //        now the broker granted the QoS levels requested by client

                // subscribe client for each topic and QoS level requested
                this.subscriberManager.Subscribe(e.Topics[i], e.QoSLevels[i], client);
            }

            try
            {
                // send SUBACK message to the client
                client.Suback(e.MessageId, e.QoSLevels);

                for (int i = 0; i < e.Topics.Length; i++)
                {
                    // publish retained message on the current subscription
                    this.publisherManager.PublishRetaind(e.Topics[i], client.ClientId);
                }
            }
            catch (MqttCommunicationException)
            {
                this.CloseClient(client);
            }
        }

        void Client_MqttMsgConnected(object sender, MqttMsgConnectEventArgs e)
        {
            // [v3.1.1] session present flag
            bool sessionPresent = false;
            // [v3.1.1] generated client id for client who provides client id zero bytes length
            string clientId = null;

            MqttClient client = (MqttClient)sender;

            // verify message to determine CONNACK message return code to the client
            byte returnCode = this.MqttConnectVerify(e.Message);

            // [v3.1.1] if client id is zero length, the broker assigns a unique identifier to it
            clientId = (e.Message.ClientId.Length != 0) ? e.Message.ClientId : Guid.NewGuid().ToString();

            // connection "could" be accepted
            if (returnCode == MqttMsgConnack.CONN_ACCEPTED)
            {
                // check if there is a client already connected with same client Id
                MqttClient clientConnected = this.GetClient(clientId);

                // force connection close to the existing client (MQTT protocol)
                if (clientConnected != null)
                {
                    this.CloseClient(clientConnected);
                }
            }

            try
            {
                // connection accepted, load (if exists) client session
                if (returnCode == MqttMsgConnack.CONN_ACCEPTED)
                {
                    // check if not clean session and try to recovery a session
                    if (!e.Message.CleanSession)
                    {
                        // create session for the client
                        MqttClientSession clientSession = new MqttClientSession(clientId);

                        // get session for the connected client
                        MqttBrokerSession session = this.sessionManager.GetSession(clientId);

                        // set inflight queue into the client session
                        if (session != null)
                        {
                            clientSession.InflightMessages = session.InflightMessages;
                            // [v3.1.1] session present flag
                            if (client.ProtocolVersion == MqttProtocolVersion.Version_3_1_1)
                                sessionPresent = true;
                        }

                        // send CONNACK message to the client
                        client.Connack(e.Message, returnCode, clientId, sessionPresent);

                        // load/inject session to the client
                        client.LoadSession(clientSession);

                        if (session != null)
                        {
                            // set reference to connected client into the session
                            session.Client = client;

                            // there are saved subscriptions
                            if (session.Subscriptions != null)
                            {
                                // register all subscriptions for the connected client
                                foreach (MqttSubscription subscription in session.Subscriptions)
                                {
                                    this.subscriberManager.Subscribe(subscription.Topic, subscription.QosLevel, client);

                                    // publish retained message on the current subscription
                                    this.publisherManager.PublishRetaind(subscription.Topic, clientId);
                                }
                            }

                            // there are saved outgoing messages
                            if (session.OutgoingMessages.Count > 0)
                            {
                                // publish outgoing messages for the session
                                this.publisherManager.PublishSession(session.ClientId);
                            }
                        }
                    }
                    // requested clean session
                    else
                    {
                        // send CONNACK message to the client
                        client.Connack(e.Message, returnCode, clientId, sessionPresent);

                        this.sessionManager.ClearSession(clientId);
                    }
                }
                else
                {
                    // send CONNACK message to the client
                    client.Connack(e.Message, returnCode, clientId, sessionPresent);
                }

				// Notify to application, client connected
				if(ClientConnected != null)
                    ClientConnected.Invoke(client);
            }
            catch (MqttCommunicationException)
            {
                this.CloseClient(client);
            }
        }

        void Client_MqttMsgDisconnected(object sender, EventArgs e)
        {
            MqttClient client = (MqttClient)sender;

            // close the client
            this.CloseClient(client);
			// Notify to application, client disconnected
			if(ClientDisconnected != null)
                ClientDisconnected.Invoke(client);
        }

        void Client_ConnectionClosed(object sender, EventArgs e)
        {
            MqttClient client = (MqttClient)sender;

            // close the client
            this.CloseClient(client);
            // Notify to application, client disconnected
            if (ClientDisconnected != null)
                ClientDisconnected.Invoke(client);
        }

        /// <summary>
        /// Check CONNECT message to accept or not the connection request 
        /// </summary>
        /// <param name="connect">CONNECT message received from client</param>
        /// <returns>Return code for CONNACK message</returns>
        private byte MqttConnectVerify(MqttMsgConnect connect)
        {
            byte returnCode = MqttMsgConnack.CONN_ACCEPTED;

            // unacceptable protocol version
            if ((connect.ProtocolVersion != MqttMsgConnect.PROTOCOL_VERSION_V3_1) &&
                (connect.ProtocolVersion != MqttMsgConnect.PROTOCOL_VERSION_V3_1_1))
                returnCode = MqttMsgConnack.CONN_REFUSED_PROT_VERS;
            else
            {
                // client id length exceeded (only for old MQTT 3.1)
                if ((connect.ProtocolVersion == MqttMsgConnect.PROTOCOL_VERSION_V3_1) &&
                     (connect.ClientId.Length > MqttMsgConnect.CLIENT_ID_MAX_LENGTH))
                    returnCode = MqttMsgConnack.CONN_REFUSED_IDENT_REJECTED;
                else
                {
                    // [v.3.1.1] client id zero length is allowed but clean session must be true
                    if ((connect.ClientId.Length == 0) && (!connect.CleanSession))
                        returnCode = MqttMsgConnack.CONN_REFUSED_IDENT_REJECTED;
                    else
                    {
                        // check user authentication
                        if (!this.uacManager.UserAuthentication(connect.Username, connect.Password))
                            returnCode = MqttMsgConnack.CONN_REFUSED_USERNAME_PASSWORD;
                        // server unavailable and not authorized ?
                        else
                        {
                            // TODO : other checks on CONNECT message
                        }
                    }
                }
            }

            return returnCode;
        }

        /// <summary>
        /// Return reference to a client with a specified Id is already connected
        /// </summary>
        /// <param name="clientId">Client Id to verify</param>
        /// <returns>Reference to client</returns>
        private MqttClient GetClient(string clientId)
        {
			lock (this.clients)
			{
				var query = from c in this.clients.Values
							where c.ClientId == clientId
							select c;

				return query.FirstOrDefault();
			}
		}

        /// <summary>
        /// Return reference to a client with a specified Id is already connected
        /// </summary>
        /// <param name="clientId">Client Id to verify</param>
        /// <returns>Reference to client</returns>
        private MqttClient GetClient(uint clientId)
        {
            lock (this.clients)
            {
                var query = from c in this.clients
                            where c.Key == clientId
                            select c.Value;

                return query.FirstOrDefault();
            }
        }
    }
}
