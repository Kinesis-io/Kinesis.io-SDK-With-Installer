using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinect.Toolbox;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Timers;

namespace Kinesis.io_Service
{
    class KinesisSwipeGestureRecognizer
    {

        SwipeGestureDetector swipeGestureDetector;

        public Joint cursor { get; set; }

        int previousJoint = -1;
        int previousDirection = -1;

        Timer _timer;

        public KinesisSwipeGestureRecognizer()
        {
            swipeGestureDetector = new SwipeGestureDetector();
            swipeGestureDetector.OnGestureDetectedWithStartingPosition += new Action<string, Vector3>(swipeGestureDetector_OnGestureDetectedWithStartingPosition);
            //swipeGestureDetector.OnGestureDetected += new Action<string>(swipeGestureDetector_OnGestureDetected);
        }

        void swipeGestureDetector_OnGestureDetectedWithStartingPosition(string obj, Vector3 arg2)
        {
            int direction = 0;
            int joint = 0;

            Console.WriteLine(obj);
            Console.WriteLine(arg2.X + "," + arg2.Y + "," + arg2.Z);

            switch (obj)
            {
                case "SwipeToRight":
                    direction = 1;
                    break;
                case "SwipeToLeft":
                    direction = 0;
                    break;
                case "SwipeToDown":
                    direction = 3;
                    break;
                case "SwipeToUp":
                    direction = 2;
                    break;
                default:
                    direction = 0;
                    break;
            }

            if (cursor.JointType == JointType.HandLeft)
                joint = 8;

            SkeletonPoint originalPosition = new SkeletonPoint();
            originalPosition.X = arg2.X;
            originalPosition.Y = arg2.Y;
            originalPosition.Z = arg2.Z;

            Joint original = new Joint();
            original.Position = originalPosition;
            var scaledOrigin = original.ScaleTo(100, 100, 0.3f, 0.3f);


            var scaledCursor = cursor.ScaleTo(100, 100, 0.3f, 0.3f);
            previousDirection = direction;
            previousJoint = joint;
            Kinesis.gKinesis.server.SendToAll(String.Format("{{\"gestures\":[{{\"type\":0,\"direction\":{0},\"joints\":[{1}],\"origin\":{{\"x\":{2},\"y\":{3},\"z\":{4}}}}}],\"cursor\":{{\"x\":{5},\"y\":{6},\"z\":{7}}}}}", direction, joint, scaledOrigin.Position.X, scaledOrigin.Position.Y, scaledOrigin.Position.Z, scaledCursor.Position.X, scaledCursor.Position.Y, scaledCursor.Position.Z));
        }

        void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Enabled = false;
            previousDirection = -1;
            previousJoint = -1;
        }

        internal void Add(SkeletonPoint skeletonPoint, KinectSensor sensor)
        {
            swipeGestureDetector.Add(skeletonPoint, sensor);
        }

        internal void Update(Skeleton skeleton, KinectSensor sensor)
        {
            Joint HandRight = skeleton.Joints[JointType.HandRight];
            Joint ElbowRight = skeleton.Joints[JointType.ElbowRight];

            Joint HandLeft = skeleton.Joints[JointType.HandLeft];
            Joint ElbowLeft = skeleton.Joints[JointType.ElbowLeft];

            Joint Elbow;

            if (cursor.JointType == JointType.HandLeft)
                Elbow = ElbowLeft;
            else
                Elbow = ElbowRight;


            if (cursor.Position.Y >= Elbow.Position.Y)
            {
                swipeGestureDetector.Add(cursor.Position, sensor);
            }
        }
    }
}
