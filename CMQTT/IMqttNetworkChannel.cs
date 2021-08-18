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
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.CrestronIO;
namespace CMQTT
{
    /// <summary>
    /// Interface for channel under MQTT library
    /// </summary>
    /// 

    /// <summary>
    /// Delegate that defines event handler for cliet/peer connection
    /// </summary>

    public interface IMqttClientNetworkChannel : IMqttNetworkChannel
    {
        void Connect(Action connnectedCallback);

    }
    public interface IMqttNetworkChannel
    {
#if TRACE
        byte[] Dump {get;}
#endif
        /// <summary>
        /// Data available on channel
        /// </summary>
        //bool DataAvailable { get; }
        byte[] Receive(int l, out int received);
        SocketStatus Status { get; }
        /// <summary>
        /// ICrestronTcpServer's clientIndex 
        /// </summary>
        uint ClientId { get; }

        /// <summary>
        /// Receive data from the network channel
        /// </summary>
        /// <param name="buffer">Data buffer for receiving data</param>
        /// <returns>Number of bytes received</returns>
        //int Receive(byte[] buffer);
        //List<byte> DataStream { get; }
        /// <summary>
        /// Send data on the network channel to the broker
        /// </summary>
        /// <param name="buffer">Data buffer to send</param>
        /// <returns>Number of byte sent</returns>
        SocketErrorCodes Send(byte[] buffer, Action<int> sentCallback);

        /// <summary>
        /// Close the network channel
        /// </summary>
        void Close();
    }
}
