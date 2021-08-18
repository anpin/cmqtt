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
using Crestron.SimplSharp.CrestronSockets;
namespace CMQTT.Communication
{
    /// <summary>
    /// MQTT client connected event args
    /// </summary>
    public class MqttClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Disconnected client
        /// </summary>
        public uint ClientIndex { get; private set; }
        public SocketStatus Status { get; private set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client">Connected client</param>
        public MqttClientDisconnectedEventArgs(uint  clientIndex, SocketStatus status)
        {
            this.ClientIndex = clientIndex;
            this.Status = status;
        }
    }
}
