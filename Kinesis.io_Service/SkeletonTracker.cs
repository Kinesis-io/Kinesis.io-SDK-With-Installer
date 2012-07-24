﻿using System;
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

        KinesisSwipeGestureRecognizer kSwipeGestureRecognizer;

        String depthImage;

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

                sensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(sensor_DepthFrameReady);
                sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);
                sensor.Start();

                Kinesis.gKinesis.server.SendToAll("{\"message\":\"Tracking Started\"}");
                Console.WriteLine("Tracking...");

                kSwipeGestureRecognizer = new KinesisSwipeGestureRecognizer();

                trackingStopped = false;
            }
        }

        void sensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            return;
            //using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            //{
            //    if (depthFrame == null)
            //    {
            //        return;
            //    }

            //    //generateDataForWeb(depthFrame);
            //    byte[] pixels = GenerateColoredBytes(depthFrame);

            //    //number of bytes per row width * 4 (B,G,R,Empty)
            //    int stride = depthFrame.Width * 4;

            //    //create image
            //    BitmapSource src = BitmapSource.Create(depthFrame.Width, depthFrame.Height,
            //        96, 96, PixelFormats.Bgr32, null, pixels, stride);
            //    depthImage = BitmapSourceToBase64(src);
            //}
        }

        public String BitmapSourceToBase64(BitmapSource bitmap)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            BitmapFrame frame = BitmapFrame.Create(bitmap);
            encoder.Frames.Add(frame);

            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {

            //get the raw data from kinect with the depth for every pixel
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData);

            //use depthFrame to create the image to display on-screen
            //depthFrame contains color information for all pixels in image
            //Height x Width x 4 (Red, Green, Blue, empty byte)
            Byte[] pixels = new byte[depthFrame.Height * depthFrame.Width * 4];

            //Bgr32  - Blue, Green, Red, empty byte
            //Bgra32 - Blue, Green, Red, transparency 
            //You must set transparency for Bgra as .NET defaults a byte to 0 = fully transparent

            //hardcoded locations to Blue, Green, Red (BGR) index positions       
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;


            //loop through all distances
            //pick a RGB color based on distance
            for (int depthIndex = 0, colorIndex = 0;
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length;
                depthIndex++, colorIndex += 4)
            {
                //get the player (requires skeleton tracking enabled for values)
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;

                //gets the depth value
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                byte intensity = CalculateIntensityFromDepth(depth);

                //.9M or 2.95'
                if (depth <= 900)
                {
                    //we are very close
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;

                }
                // .9M - 2M or 2.95' - 6.56'
                else if (depth > 900 && depth < 2000)
                {
                    //we are a bit further away
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;
                }
                // 2M+ or 6.56'+
                else if (depth > 2000)
                {
                    //we are the farthest
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;
                }

                //Color all players "gold"
                if (player > 0)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = intensity;
                    pixels[colorIndex + RedIndex] = 0;

                }
            }
            return pixels;
        }

        public static byte CalculateIntensityFromDepth(int distance)
        {
            //formula for calculating monochrome intensity for histogram
            return (byte)(255 - (255 * Math.Max(distance - 100, 0)
                / (800)));
        }

        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton skeleton = this.GetValidSkeleton(e);

            if (!skeletonDetected && skeleton != null)
            {
                skeletonDetected = true;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"User Found\"}");
            }

            if (skeleton == null && skeletonDetected)
            {
                skeletonDetected = false;
                Kinesis.gKinesis.server.SendToAll("{\"message\":\"User Lost\"}");
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
                //if (HandLeft.Position.Y < ElbowLeft.Position.Y && HandRight.Position.Y < ElbowRight.Position.Y)
                //    return;

                //Set cursor to left hand if right hand is down
                if (HandLeft.Position.Y > HandRight.Position.Y && HandRight.Position.Y < ElbowRight.Position.Y)
                    cursor = HandLeft;


                var scaledCursor = cursor.ScaleTo(100, 100, 0.3f, 0.3f);

                Joint elbow = ElbowRight;
                Joint shoulder = skeleton.Joints[JointType.ShoulderRight];
                if (cursor == HandLeft)
                {
                    shoulder = skeleton.Joints[JointType.ShoulderLeft];
                    elbow = ElbowLeft;
                }

                Hashtable position = new Hashtable();
                position.Add("x", scaledCursor.Position.X);
                position.Add("y", scaledCursor.Position.Y);
                position.Add("z", cursor.Position.Z - Spine.Position.Z);

                Hashtable message = new Hashtable();
                message.Add("cursor", position);

                Kinesis.gKinesis.server.SendToAll(JsonConvert.SerializeObject(message));

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
