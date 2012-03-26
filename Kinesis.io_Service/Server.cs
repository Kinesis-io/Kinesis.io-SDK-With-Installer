using System;
using System.Collections.Generic;

using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketEngine;
using SuperWebSocket;
using System.Timers;
using System.Diagnostics;
using Microsoft.Kinect;

namespace Kinesis.io_Service
{
    public class Server
    {
        public List<WebSocketSession> m_Sessions = new List<WebSocketSession>();
        private object m_SessionSyncRoot = new object();
        private object m_SecureSessionSyncRoot = new object();
        private Timer m_SecureSocketPushTimer;

        private static WebSocketServer socketServer;

        public Server()
        {
            socketServer = new WebSocketServer();
        }

        internal void Start()
        {
            socketServer.Setup(new RootConfig(),
                new ServerConfig
                {
                    Name = "SuperWebSocket",
                    Ip = "Any",
                    Port = 2011,
                    Mode = SocketMode.Sync
                }, SocketServerFactory.Instance);

            socketServer.NewMessageReceived += new SessionEventHandler<WebSocketSession, string>(socketServer_NewMessageReceived);
            socketServer.NewSessionConnected += new SessionEventHandler<WebSocketSession>(socketServer_NewSessionConnected);
            socketServer.SessionClosed += new SessionEventHandler<WebSocketSession, CloseReason>(socketServer_SessionClosed);

            Trace.WriteLine(String.Format("Status: {0}", socketServer.Start()));
        }

        internal void Stop()
        {
            if (socketServer.IsRunning)
            {
                socketServer.NewMessageReceived -= socketServer_NewMessageReceived;
                socketServer.NewSessionConnected -= socketServer_NewSessionConnected;
                socketServer.SessionClosed -= socketServer_SessionClosed;
                socketServer.Stop();
            }
        }

        void socketServer_NewMessageReceived(WebSocketSession session, string e)
        {
            SendToAll(session.Cookies["name"] + ": " + e);
        }

        void socketServer_NewSessionConnected(WebSocketSession session)
        {
            lock (m_SessionSyncRoot)
                m_Sessions.Add(session);

            if (Kinesis.gKinesis._sensor != null && Kinesis.gKinesis._sensor.Status == KinectStatus.Connected)
            {
                session.SendResponse("{\"Kinect\":\"Connected\"}");
            }

            Kinesis.gKinesis.SetupSkeletonTracker();
        }

        void socketServer_SessionClosed(WebSocketSession session, CloseReason reason)
        {
            lock (m_SessionSyncRoot)
                m_Sessions.Remove(session);

            Kinesis.gKinesis.StopSkeletonTracker();

            if (reason == CloseReason.ServerShutdown)
                return;
        }

        public void SendToAll(string message)
        {
            lock (m_SessionSyncRoot)
            {
                foreach (var s in m_Sessions)
                {
                    s.SendResponse(message);
                }
            }
        }
    }    
}
