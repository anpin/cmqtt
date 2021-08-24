//#define LOCALCLIENT
using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using CMQTT;
#if TRACE
// alias needed due to Microsoft.SPOT.Trace in .Net Micro Framework
// (it's ambiguos with CMQTT.Utility.Trace)
using MqttUtility = CMQTT.Utility;
using CMQTT.Communication;
#endif
namespace CMQTTServer
{
    public class ControlSystem : CrestronControlSystem
    {
        MqttBroker broker;
#if LOCALCLIENT
        MqttLocalClient client;
        Thread clientThread;
#endif
        bool running = true;
        int numberOfClients = 20;
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20 + numberOfClients * 6;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
            }
            catch (Exception e)
            {
                ErrorLog.Exception("Error in the constructor:", e);
            }
        }
        public override void InitializeSystem()
        {
            try
            {

#if TRACE
                MqttUtility.Trace.TraceLevel = MqttUtility.TraceLevel.Verbose | MqttUtility.TraceLevel.Frame | MqttUtility.TraceLevel.Queuing;
                MqttUtility.Trace.TraceListener = (format, args) => ErrorLog.Warn(format, args);
#endif

                // create and start broker
                broker = new MqttBroker(numberOfClients);
                broker.Start();
                broker.ClientConnected += new Action<MqttClient>(broker_ClientConnected);
                broker.ClientDisconnected += new Action<MqttClient>(broker_ClientDisconnected);
#if LOCALCLIENT
                client = new MqttLocalClient(new IPEndPoint(IPAddress.Parse("10.254.254.237"), 1883), 100, 30000, 10000);
                client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                client.ConnectionClosed += Client_ConnectionClosed; ;
                clientThread = Fx.StartThread((o) =>
                {
                    while (running)
                    {
                        Thread.Sleep(60000);
                        try
                        {
                            if (!client.IsConnected)
                            {
                                client.Connect("local-client", (c) =>
                                {
                                    ErrorLog.Warn("local-client connect returned {0:X}", c);
                                    var m = client.Subscribe(new[] { "#", "abc/defa", "abc/def" }, new[] { (byte)0, (byte)0, (byte)0 });
                                    ErrorLog.Warn("local-client subscribe returned {0}", m);
                                });
                            }
                        }
                        catch(Exception ex)
                        {
                            ErrorLog.Exception("Local Client Thread>", ex);
                        }
                        
                    }
                    return null;
                });
                
#endif

            }
            catch (Exception e)
            {
                ErrorLog.Exception("Error in InitializeSystem:", e);
            }
        }

        private void Client_MqttMsgDisconnected(object sender, EventArgs e)
        {
            ErrorLog.Warn("local-client disconnected");
        }

        private void Client_MqttMsgSubscribeReceived(object sender, CMQTT.Messages.MqttMsgSubscribeEventArgs e)
        {
            ErrorLog.Warn("local-client subscribed {0}", string.Join(",", e.Topics));
        }

        private void Client_MqttMsgUnsubscribeReceived(object sender, CMQTT.Messages.MqttMsgUnsubscribeEventArgs e)
        {
            ErrorLog.Warn("local-client unsubscribed {0}", string.Join(",", e.Topics));
        }

        private void Client_ConnectionClosed(object sender, EventArgs e)
        {
            ErrorLog.Warn("local-client connection closed");
        }

        private void Client_MqttMsgConnected(object sender, CMQTT.Messages.MqttMsgConnectEventArgs e)
        {
            ErrorLog.Warn("local-client connected");
        }

        private void Client_MqttMsgPublishReceived(object sender, CMQTT.Messages.MqttMsgPublishEventArgs e)
        {
            try
            {
                ErrorLog.Warn("Localclient received topic {0} message {1}", e.Topic, e.Message.ToHex());
            }
            catch(Exception ex)
            {
                ErrorLog.Exception("Error in Client_MqttMsgPublishReceived:", ex);

            }
        }

        void broker_ClientDisconnected(MqttClient obj)
        {
            ErrorLog.Notice("MQTT CLIENT {0} WENT OFFLINE", obj.ClientId);
        }

        void broker_ClientConnected(MqttClient obj)
        {
            ErrorLog.Notice("MQTT CLIENT {0} WENT ONLINE", obj.ClientId);
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    //client.Disconnect();
                    running = false;
#if LOCALCLIENT
                    client.Disconnect();
#endif
                    broker.Stop();
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}