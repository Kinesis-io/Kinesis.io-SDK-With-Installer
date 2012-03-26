using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;

namespace Kinesis.io_Service
{
    public class SkeletonTracker
    {
        public KinectSensor sensor { get; set; }

        Skeleton[] allSkeletons = new Skeleton[6];
        bool skeletonDetected = false;

        KinesisSwipeGestureRecognizer kSwipeGestureRecognizer;

        internal void Start()
        {
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
                sensor.DepthStream.Enable();
                sensor.SkeletonStream.Enable(parameters);

                sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);
                sensor.Start();

                Kinesis.gKinesis.server.SendToAll("{\"message\":\"Tracking Started\"}");
                Console.WriteLine("Tracking...");

                kSwipeGestureRecognizer = new KinesisSwipeGestureRecognizer();
            }
        }

        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton skeleton = this.GetValidSkeleton(e);

            if (!skeletonDetected && skeleton != null)
            {
                skeletonDetected = true;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"User Found\"}");
                Console.WriteLine("User");
            }

            if (skeleton == null && skeletonDetected)
            {
                skeletonDetected = false;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"User Lost\"}");
                Console.WriteLine("No User");
            }

            if (skeleton != null)
            {
                Joint Spine = skeleton.Joints[JointType.Spine];
                //To make sure that the person is in range
                if (Spine.Position.Z < 1.0 || Spine.Position.Z > 2.0)
                    return;

                Joint HandRight = skeleton.Joints[JointType.HandRight];
                Joint ElbowRight = skeleton.Joints[JointType.ElbowRight];

                Joint HandLeft = skeleton.Joints[JointType.HandLeft];
                Joint ElbowLeft = skeleton.Joints[JointType.ElbowLeft];

                //Set the default cursor to be associated with the right hand
                Joint cursor = HandRight;

                //No hand is up. Just return without sending any information
                if (HandLeft.Position.Y < ElbowLeft.Position.Y && HandRight.Position.Y < ElbowRight.Position.Y)
                    return;

                //Set cursor to left hand if right hand is down
                if (HandLeft.Position.Y > ElbowLeft.Position.Y && HandRight.Position.Y < ElbowRight.Position.Y)
                    cursor = HandLeft;


                var scaledCursor = cursor.ScaleTo(100, 100, 0.3f, 0.3f);

                Joint elbow = ElbowRight;
                Joint shoulder = skeleton.Joints[JointType.ShoulderRight];
                if (cursor == HandLeft)
                {
                    shoulder = skeleton.Joints[JointType.ShoulderLeft];
                    elbow = ElbowLeft;
                }

                Kinesis.gKinesis.server.SendToAll(String.Format("{{\"cursor\":{{\"x\":{0},\"y\":{1},\"z\":{2}}}}}", scaledCursor.Position.X, scaledCursor.Position.Y, cursor.Position.Z - Spine.Position.Z));

                kSwipeGestureRecognizer.cursor = cursor;

                if (cursor.TrackingState == JointTrackingState.Tracked)
                    kSwipeGestureRecognizer.Update(skeleton, sensor);
            }
        }

        private Skeleton GetValidSkeleton(SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                {
                    return null;
                }

                skeletonFrame.CopySkeletonDataTo(allSkeletons);
                Skeleton skeleton = (from s in allSkeletons
                                     where
                                         s.TrackingState == SkeletonTrackingState.Tracked
                                     select s).FirstOrDefault();
                return skeleton;
            }
        }

        internal void Stop()
        {
            if (sensor != null)
            {
                sensor.SkeletonFrameReady -= sensor_SkeletonFrameReady;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"Tracking Stopped\"}");
                Console.WriteLine("Stop Tracking...");
                sensor.Stop();
            }
        }
    }
}
