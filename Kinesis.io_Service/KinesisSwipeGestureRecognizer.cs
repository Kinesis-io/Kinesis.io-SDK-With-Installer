using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinect.Toolbox;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Timers;
using System.Collections;
using Newtonsoft.Json;

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


            Hashtable gesture = new Hashtable();

            Hashtable origin = new Hashtable();
            origin.Add("x", scaledOrigin.Position.X);
            origin.Add("y", scaledOrigin.Position.Y);
            origin.Add("z", scaledOrigin.Position.Z);
            gesture.Add("origin", origin);

            Hashtable cursorHash = new Hashtable();
            cursorHash.Add("x", scaledCursor.Position.X);
            cursorHash.Add("y", scaledCursor.Position.Y);
            cursorHash.Add("z", scaledCursor.Position.Z);
            gesture.Add("cursor", cursorHash);

            ArrayList joints = new ArrayList();
            joints.Add(joint);
            gesture.Add("joints", joints);

            gesture.Add("direction", direction);

            gesture.Add("type", 0);


            Hashtable message = new Hashtable();

            ArrayList gestures = new ArrayList();
            gestures.Add(gesture);

            message.Add("gestures", gestures);

            Kinesis.gKinesis.server.SendToAll(JsonConvert.SerializeObject(message));
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
