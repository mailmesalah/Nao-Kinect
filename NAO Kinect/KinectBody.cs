/*
 * This file was created by Austin Hughes and Stetson Gafford
 * Last Modified: 2014-09-04 
 */

// System imports
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

// Microsoft imports
using Microsoft.Kinect;

namespace NAO_Kinect
{
    /// <summary>
    /// This class handles processing the Kinect Body frames
    /// and drawing the body skeleton on an image for display
    /// </summary>
    class KinectBody
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        private int displayWidth;
        private int displayHeight;

        private BodyFrameReader bodyFrameReader;
        private CoordinateMapper coordinateMapper;

        /// <summary>
        /// Arrays
        /// </summary>
        private Body[] bodies;
        
        /// <summary>
        /// Drawing variables
        /// </summary>
        private const double HandSize = 30;
        private const double JointThickness = 3;
        private const double ClipBoundsThickness = 10;
        private const float InferredZPositionClamp = 0.1f;
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));  
        private readonly Brush inferredJointBrush = Brushes.Yellow;   
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;

        /// <summary>
        /// Lists
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;
        private List<Pen> bodyColors;

        /// <summary>
        /// Event handler for new Kinect frames
        /// </summary>
        public event EventHandler NewFrame;

        /// <summary>
        /// Class constructor
        /// </summary>
        public KinectBody(KinectSensor kinect)
        {
            // Set kinect sensor
            sensor = kinect;

            // Get the coordinate mapper
            coordinateMapper = sensor.CoordinateMapper;

            // Get the depth (display) extents
            FrameDescription frameDescription = sensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            displayWidth = frameDescription.Width;
            displayHeight = frameDescription.Height;

            // A bone defined as a line between two joints
            bones = new List<Tuple<JointType, JointType>>
                {
                    // Torso
                    new Tuple<JointType, JointType>(JointType.Head, JointType.Neck),
                    new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder),
                    new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid),
                    new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase),
                    new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight),
                    new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft),
                    new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight),
                    new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft),

                    // Right Arm
                    new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight),
                    new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight),
                    new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight),
                    new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight),
                    new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight),

                    // Left Arm
                    new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft),
                    new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft),
                    new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft),
                    new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft),
                    new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft),

                    // Right Leg
                    new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight),
                    new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight),
                    new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight),

                    // Left Leg
                    new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft),
                    new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft),
                    new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft)
                };

            // Populate body colors, one for each BodyIndex
            bodyColors = new List<Pen>
            {
                new Pen(Brushes.Red, 6),
                new Pen(Brushes.Orange, 6),
                new Pen(Brushes.Green, 6),
                new Pen(Brushes.Blue, 6),
                new Pen(Brushes.Indigo, 6),
                new Pen(Brushes.Violet, 6)
            };

            // Create the drawing group we'll use for drawing
            drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            imageSource = new DrawingImage(drawingGroup);

            // Start the body reader
            startBodyReader();

            if (bodyFrameReader != null)
            {
                bodyFrameReader.FrameArrived += Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Starts the Skeleton Stream
        /// If audio was started, restart it after starting skeleton stream
        /// </summary>
        private void startBodyReader()
        {
            try
            {
                // Open the reader for the body frames
                bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            }
            catch (Exception)
            {
                MessageBox.Show("Error starting body reader.");
            }
        }

        /// <summary>
        /// Disables the skeleton stream
        /// </summary>
        public void stopBodyStream()
        {
            if (null != sensor)
            {
                bodyFrameReader.Dispose();
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// 
        /// This code was provided by Microsoft
        /// </summary>
        /// <param name="sender"> Object sending the event </param>
        /// <param name="e"> Event arguments </param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                    {
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, displayWidth, displayHeight));

                    int penIndex = 0;
                    foreach (Body body in bodies)
                    {
                        Pen drawPen = bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // Convert the joint points to depth (display) space
                            var jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // Sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            DrawBody(joints, jointPoints, dc, drawPen);

                            DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                    // Prevent drawing outside of our render area
                    drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, displayWidth, displayHeight));
                }
            }

            // Calls our event
            OnNewFrame();
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints"> Foints to draw </param>
        /// <param name="jointPoints"> Translated positions of joints to draw </param>
        /// <param name="drawingContext"> Drawing context to draw to </param>
        /// <param name="drawingPen"> Specifies color to draw a specific body </param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in bones)
            {
                DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints"> Foints to draw </param>
        /// <param name="jointPoints"> Translated positions of joints to draw </param>
        /// <param name="jointType0"> First joint of bone to draw </param>
        /// <param name="jointType1"> Second joint of bone to draw </param>
        /// <param name="drawingContext"> Drawing context to draw to </param>
        /// /// <param name="drawingPen"> Specifies color to draw a specific bone </param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState"> State of the hand </param>
        /// <param name="handPosition"> Position of the hand </param>
        /// <param name="drawingContext"> Drawing context to draw to </param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body"> Body to draw clipping information for </param>
        /// <param name="drawingContext"> Drawing context to draw to </param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, displayHeight - ClipBoundsThickness, displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, displayHeight));
            }
        }

        /// <summary>
        /// Triggers image updated event
        /// </summary>
        private void OnNewFrame()
        {
            var handler = NewFrame;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Used to get the current frame
        /// </summary>
        /// <returns> current frame </returns>
        public DrawingImage getImage()
        {
            return imageSource;
        }

        /// <summary>
        /// Returns the currently tracked skeleton
        /// </summary>
        /// <returns> Tracked skeleton </returns>
        public Body getBody()
        {
            try
            {
                foreach (Body body in bodies)
                {
                    if (body.IsTracked)
                    {
                        return body;
                    }
                }
            }
            catch
            { }
            return null;
        }
    }
}
