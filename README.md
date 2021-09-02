# CMQTT
[![NuGet](https://img.shields.io/nuget/v/CMQTT.svg?style=flat)](https://www.nuget.org/packages/CMQTT/)

CMQTT - MQTT Broker and Client for Crestron S#Pro framework

This repository is port of [GnatMQ](https://github.com/gnatmq/gnatmq) for Crestron S#Pro framework

## Description

A broker (server) for the MQTT protocol, an M2M Internet-of-Things communication protocol based on .Net Framework. 

MQTT, short for Message Queue Telemetry Transport, is a light weight messaging protocol that enables embedded devices with limited resources to perform asynchronous communication on a constrained network.

Developed by IBM and Eurotech, the MQTT protocol is released as an open standard and being standardized by OASIS (Organization for the Advancement of Structured Information Standard), a non-profit consortium that drives the development, convergence and adoption of open standards for the global information society.

In general, the MQTT environment consists of multiple clients and a server, called broker.

This project is created to develop an MQTT broker.  While there are other MQTT broker project, this project is created to provide additional resources to learn and engage in developing useful resources for the MQTT protocol, in conjunction with the M2Mqtt project, a .NET client library for MQTT that supports .NET Framework, .NET Compact Framework and .NET Micro Framework.

## How to use:
```
nuget install CMQTT -OutputDirectory .\packages
```
Starting the server is simple:
```C#
using CMQTT;
namespace Server
{
    public class ControlSystem : CrestronControlSystem
    {
        MqttBroker broker;
        ...
        public override void InitializeSystem()
        {
            // create and start broker
            broker = new MqttBroker();
            broker.Start();
            //Once the broker is started, you applciaiton is free to do whatever it wants. 
        }
        ...
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Stopping):
                    broker.Stop();
                    break;
            }

        }
        ...
    }
}
```
Please refer to the Server project for the full example with local client running in the same program

## Supported Platforms: 
* Crestron 3-series controllers
* Crestron 4-series controllers


# 3-series comparability warning 
While it compiles with VS2008 for 3-series controllers further tests showed that it takes almost 100% CPU on AV3 with a small number of clients connected. It gets especially worse (stack overflow with consequent reboot) if you try to run a local client in a separate program connected tp 127.0.0.1, so far we were not able to resolve the issue and advice against using this code in production


# If this package saves you time consider donating via buttons below
[![Coinbase](https://img.shields.io/badge/Donate%20with-Crypto-red)](https://commerce.coinbase.com/checkout/68c42319-c494-47b5-8755-2fad731a3547)
[![Paypal](https://img.shields.io/badge/Donate%20with-PayPal-blue)](https://paypal.me/APEngineeringLLC?locale.x=en_US)

# Commercial support is available
    Drop us an email at hi[at]apes[dot]ge 
