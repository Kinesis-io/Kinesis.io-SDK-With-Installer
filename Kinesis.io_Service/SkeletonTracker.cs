using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System.Timers;
using System.Collections;
using Newtonsoft.Json;

namespace Kinesis.io_Service
{
    public class SkeletonTracker
    {
        public KinectSensor sensor { get; set; }

        Skeleton[] allSkeletons = new Skeleton[6];
        bool skeletonDetected = false;

        bool trackingStopping = false;
        bool trackingStopped = true;
        Timer stopTimer;

        internal void Start()
        {

            if (trackingStopping == true)
            {
                this.cancelStop();
            }

            if (trackingStopped == false)
            {
                return;
            }

            if (sensor != null)
            {
                TransformSmoothParameters parameters =
                    new TransformSmoothParameters();
                parameters.Smoothing = 0.7f;
                parameters.Correction = 0.3f;
                parameters.Prediction = 0.4f;
                parameters.JitterRadius = 0.5f;
                parameters.MaxDeviationRadius = 0.5f;

                //Initialize to do Skeletal Tracking
                sensor.ColorStream.Enable();
                sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                sensor.SkeletonStream.Enable(parameters);

                sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);
                sensor.Start();

                Console.WriteLine("Tracking...");

                trackingStopped = false;
            }
        }

        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                {
                    return;
                }

                skeletonFrame.CopySkeletonDataTo(allSkeletons);

                ArrayList players = new ArrayList();

                foreach (Skeleton s in allSkeletons)
                {
                    if (s.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        ArrayList player = new ArrayList();
                        Joint baseJoint;
                        foreach (Joint x in s.Joints)
                        {
                            
                            
                            
                            //[[[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z],[x,y,z]]]
                            
                            var scaledJoint = x.ScaleTo(1000, 1000);

                            if (x.JointType == JointType.HipCenter)
                            {
                                baseJoint = scaledJoint;
                                float[] joint = { scaledJoint.Position.X / 10, scaledJoint.Position.Y / 10, scaledJoint.Position.Z };
                                player.Add(joint);
                            }
                            else
                            {
                                float[] joint = { (scaledJoint.Position.X - baseJoint.Position.X) / 10, (scaledJoint.Position.Y - baseJoint.Position.Y) / 10, (scaledJoint.Position.Z - baseJoint.Position.Z)};
                                player.Add(joint);
                            }
                        }

                        players.Add(player);
                    }
                }

                if (!skeletonDetected && players.Count > 0)
                {
                    skeletonDetected = true;
                    Hashtable message = new Hashtable();
                    message.Add("message", "User Found");
                    Console.WriteLine(JsonConvert.SerializeObject(message));
                    Kinesis.gKinesis.server.SendToAll(JsonConvert.SerializeObject(message));
                }

                if (players.Count == 0 && skeletonDetected)
                {
                    skeletonDetected = false;
                    Hashtable message = new Hashtable();
                    message.Add("message", "User Lost");
                    Kinesis.gKinesis.server.SendToAll(JsonConvert.SerializeObject(message));
                    Console.WriteLine(JsonConvert.SerializeObject(message));
                    return;
                }

                if (players.Count == 0)
                    return;

                Hashtable message1 = new Hashtable();
                message1.Add("players", players);

                string jsonValue = JsonConvert.SerializeObject(message1);
                Kinesis.gKinesis.server.SendToAll(jsonValue);
            }   
        }

        private void cancelStop()
        {
            Console.WriteLine("Cancel stop");
            trackingStopping = false;
            trackingStopped = false;
            stopTimer.Stop();
        }

        internal void Stop()
        {

            if (trackingStopping == true || trackingStopped == true)
            {
                Console.WriteLine("already stopping or stopped");
                return;
            }

            if (trackingStopping == false)
            {
                Console.WriteLine("stopping in 4 seconds");
                trackingStopping = true;
                trackingStopped = false;

                stopTimer = new Timer(4000);
                stopTimer.Elapsed += new ElapsedEventHandler(stopTimer_Elapsed);
                stopTimer.Start();
            }
        }

        void stopTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            stopTimer.Stop();
            stopTimer = null;

            if (trackingStopping == false)
            {
                Console.WriteLine("Canceled stop");
                return;
            }

            trackingStopping = false;

            Console.WriteLine("stopping");

            if (sensor != null)
            {
                sensor.SkeletonFrameReady -= sensor_SkeletonFrameReady;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"Tracking Stopped\"}");
                Console.WriteLine("Stop Tracking...");
                sensor.Stop();
            }
            Console.WriteLine("Stopped");
            trackingStopped = true;
        }
    }
}
