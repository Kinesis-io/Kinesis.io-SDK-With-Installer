using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

using System.Threading;
using Microsoft.Kinect;

namespace Kinesis.io_Service
{
    public partial class Kinesis : ServiceBase
    {
        public KinectSensor _sensor;

        public Server server;
        public SkeletonTracker skeletonTracker;

        public static Kinesis gKinesis;

        public bool skeletalTrackingRunning { get; set; }

        public Kinesis()
        {
            gKinesis = this;
        }

        protected override void OnStart(string[] args)
        {
            KinectSensor.KinectSensors.StatusChanged += new EventHandler<StatusChangedEventArgs>(KinectSensors_StatusChanged);
            Console.WriteLine("Starting...");

            this.SetupWebServer();

            if (KinectSensor.KinectSensors.Count > 0)
            {
                _sensor = KinectSensor.KinectSensors[0];

                if (_sensor.Status == KinectStatus.Connected)
                {
                    this.SetupSkeletonTracker();
                }
            }
            else
            {
                server.SendToAll("{\"Kinect\":\"Disconnected\"}");
            }
        }

        protected override void OnStop()
        {
            this.StopSkeletonTracker();
            this.StopWebServer();
        }

         void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    this.SetupSkeletonTracker();
                    break;
                case KinectStatus.Disconnected:
                case KinectStatus.NotPowered:
                case KinectStatus.Error:
                    server.SendToAll(String.Format("{{\"Kinect\":\"{0}\"}}", e.Status.ToString()));
                    this.StopSkeletonTracker();
                    break;
            }
        }

        private void Initialize()
        {
            this.SetupSkeletonTracker();
            this.SetupWebServer();
        }

        private void SetupWebServer()
        {
            server = new Server();
            server.Start();
        }

        public void SetupSkeletonTracker()
        {
            if (skeletalTrackingRunning == true)
                return;

            if (server.m_Sessions.Count > 0 && skeletalTrackingRunning == false)
            {
                server.SendToAll("{\"Kinect\":\"Connected\"}");

                if(skeletonTracker == null)
                    skeletonTracker = new SkeletonTracker();

                if (_sensor == null && KinectSensor.KinectSensors.Count > 0)
                    _sensor = KinectSensor.KinectSensors[0];

                skeletonTracker.sensor = _sensor;
                skeletonTracker.Start();
                skeletalTrackingRunning = true;
            }

        }

        private void StopWebServer()
        {
            if (server != null)
            {
                server.Stop();
            }
        }

        public void StopSkeletonTracker(bool force = false)
        {
            if (!force && server.m_Sessions.Count > 0)
                return;

            if (skeletonTracker != null)
            {
                skeletalTrackingRunning = false;
                skeletonTracker.Stop();
            }
        }
    }
}
