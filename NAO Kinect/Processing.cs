/*
 * This file was created by Austin Hughes and Stetson Gafford
 * Last Modified: 2014-09-10
 */

// System imports
using System;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D; // For 3D vectors

// Microsoft imports
using Microsoft.Kinect;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace NAO_Kinect
{
    public delegate void ProcessingNewFrameEventHandler(object sender, EventArgs e);
    public delegate void ProcessingNewSpeechEventHandler(object sender, EventArgs e);
    public delegate void ProcessingNewTickEventHandler(object sender, EventArgs e);

    /// <summary>
    /// This class takes a tracked body and generates useful data from it
    /// </summary>
    class Processing
    {
        /// <summary>
        /// Struct to return all relevant data to other classes
        /// </summary>
        /// 
        [Serializable]
        internal struct BodyInfo
        {
            public float[] angles;
            public bool RHandOpen;
            public bool LHandOpen;
            public bool noTrackedBody;
        };

        /// <summary>
        /// Variables
        /// </summary>
        private bool allowNaoUpdates = false;
        private bool invert = true;
        private string rHandStatus = "unknown";
        private string lHandStatus = "unkown";
        private readonly string[] invertedJointNames = { "LShoulderRoll", "RShoulderRoll", "LElbowRoll", "RElbowRoll", "LShoulderPitch", "RShoulderPitch" };
        private readonly string[] jointNames = { "RShoulderRoll", "LShoulderRoll", "RElbowRoll", "LElbowRoll", "RShoulderPitch", "LShoulderPitch" };
        private float[] offset = { 0.8f, 0.8f, -2.5f, -2.5f, -2.65f, -2.65f };
        private float[] oldAngles = new float[6];
        private static KinectInterface kinectInterface;
        private static Body trackedBody;
        BodyInfo bodyInfo;
        BodyInfo info;
        private BodyInfo UIinfo;
        private Motion naoMotion;
        ImageSource currentFrame;
        string speechStatus;
        bool speechResult;

        //for recording
        private bool isRecording = false;
        List<NaoMovement> moveList = new List<NaoMovement>();
        Stopwatch stopwatch = new Stopwatch();
        private bool isPlaying = false;

        public event ProcessingNewFrameEventHandler pNewFrame;
        public event ProcessingNewSpeechEventHandler pNewSpeech;
        public event ProcessingNewSpeechEventHandler pNewTick;

        /// <summary>
        /// Timer 
        /// </summary>
        private DispatcherTimer motionTimer = new DispatcherTimer();

        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="interfaceClass"> Reference to current kinect interface </param>
        public Processing()
        {
            bodyInfo = new BodyInfo();
            bodyInfo.angles = new float[6];

            // Call the motion constructor
            naoMotion = new Motion();

            // Creates the kinectInterface class and registers event handlers
            kinectInterface = new KinectInterface();
            kinectInterface.start();

            kinectInterface.NewFrame += kinectInterface_NewFrame;
            kinectInterface.NewSpeech += kinectInterface_NewSpeech;

            // Create a timer for event based NAO update. 
            motionTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)Math.Ceiling(1000.0 / 7));
            motionTimer.Start();

            motionTimer.Tick += motionTimer_Tick;
        }

        /// <summary>
        /// Record and Plays
        /// </summary>

        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite)
        {
            using (Stream stream = File.Open(filePath, FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }


        public void startRecording()
        {
            isRecording = true;
            moveList = new List<NaoMovement>();
            stopwatch.Reset();
            stopwatch.Start();
        }

        public List<NaoMovement> stopRecording()
        {
            isRecording = false;
            stopwatch.Stop();
                        
            if (moveList.Count > 0)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Nao Movement files (*.nmv)|*.nmv";
                if (saveFileDialog.ShowDialog() == true)
                {
                    WriteToBinaryFile(saveFileDialog.FileName, moveList);
                }
            }

            return moveList;
        }

        public void discardRecording()
        {
            isRecording = false;
            moveList = new List<NaoMovement>();
            stopwatch.Reset();
        }

        public List<NaoMovement> getMoveList()
        {
            return moveList;
        }

        public void playMoveList(List<NaoMovement> mList)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Nao Movement files (*.nmv)|*.nmv";
            if (openFileDialog.ShowDialog() == true)
            {
                mList=ReadFromBinaryFile<List<NaoMovement>>(openFileDialog.FileName);
                isPlaying = true;
                stopwatch.Reset();
                stopwatch.Start();
                if (mList != null)
                {
                    foreach (NaoMovement nm in mList)
                    {
                        while (nm.timeDifference < stopwatch.ElapsedMilliseconds) ;
                        moveNao(nm.bodyInfo);
                    }
                }
                stopwatch.Stop();
                isPlaying = false;
            }
        }

        private void moveNao(BodyInfo bodyInf)
        {
            // Generate calibrated angles
            for (var x = 0; x < 6; x++)
            {
                bodyInf.angles[x] = bodyInf.angles[x] - offset[x]; // adjustment to work with NAO robot angles
            }

            if (bodyInf.angles[4] < -2.0f) bodyInf.angles[4] = -2.0f;
            if (bodyInf.angles[5] < -2.0f) bodyInf.angles[5] = -2.0f;

            if (bodyInf.angles[4] > 2.0f) bodyInf.angles[4] = 2.0f;
            if (bodyInf.angles[5] > 2.0f) bodyInf.angles[5] = 2.0f;



            // Check if updates should be sent to NAO
            if (allowNaoUpdates)
            {
                // Check to make sure that angle has changed enough to send new angle and update angle if it has
                for (var x = 0; x < 6; x++)
                {
                    if ((Math.Abs(oldAngles[x] - bodyInf.angles[x]) > .1 || Math.Abs(bodyInf.angles[x] - bodyInf.angles[x]) < .1))
                    {
                        oldAngles[x] = bodyInf.angles[x];
                        updateNAO(bodyInf.angles[x], invert ? invertedJointNames[x] : jointNames[x]);
                    }
                }

                // update right hand
                switch (bodyInf.RHandOpen)
                {
                    case true:
                        if (rHandStatus == "open")
                        {
                            break;
                        }
                        rHandStatus = "open";
                        if (invert)
                        {
                            naoMotion.openHand("LHand");
                            break;
                        }
                        naoMotion.openHand("RHand");
                        break;
                    case false:
                        if (rHandStatus == "closed")
                        {
                            break;
                        }
                        rHandStatus = "closed";
                        if (invert)
                        {
                            naoMotion.closeHand("LHand");

                            break;
                        }
                        naoMotion.closeHand("RHand");
                        break;
                }

                // update left hand
                switch (bodyInf.LHandOpen)
                {
                    case true:
                        if (lHandStatus == "open")
                        {
                            break;
                        }
                        lHandStatus = "open";
                        if (invert)
                        {
                            naoMotion.openHand("RHand");
                            break;
                        }
                        naoMotion.openHand("LHand");
                        break;
                    case false:
                        if (lHandStatus == "closed")
                        {
                            break;
                        }
                        lHandStatus = "closed";
                        if (invert)
                        {
                            naoMotion.closeHand("RHand");

                            break;
                        }
                        naoMotion.closeHand("LHand");
                        break;
                }
            }
        }

        //Record and Play
        //////////////////////////

        public void connect(string ip)
        {
            naoMotion.connect(ip);

            allowNaoUpdates = true;
        }

        public void disconnect()
        {
            allowNaoUpdates = false;
        }

        public void setInvert(bool set)
        {
            invert = set;
        }

        public string getSpeechStatus()
        {
            return speechStatus;
        }

        public bool getSpeechResult()
        {
            return speechResult;
        }

        public BodyInfo getBodyInfo()
        {
            return UIinfo;
        }

        public ImageSource getFrame()
        {
            return currentFrame;
        }

        public void cleanUp()
        {
            naoMotion.removeStiffness();
        }

        /// <summary>
        /// Gets the usable angles of joints for sending to NAO
        /// </summary>
        /// <returns> struct of tupe BodyInfo </returns>
        private BodyInfo calculateAngles()
        {
            trackedBody = kinectInterface.getBody();

            if (trackedBody != null)
            {
                bodyInfo.noTrackedBody = false;

                var shoulderCenter = trackedBody.Joints[JointType.SpineShoulder].Position;

                var wristLeft = trackedBody.Joints[JointType.WristLeft].Position;
                var wristRight = trackedBody.Joints[JointType.WristRight].Position;

                //var spineShoulder = trackedBody.Joints[JointType.SpineShoulder].Position;
                //var spineBase = trackedBody.Joints[JointType.SpineBase].Position;

                var elbowLeft = trackedBody.Joints[JointType.ElbowLeft].Position;
                var elbowRight = trackedBody.Joints[JointType.ElbowRight].Position;

                var shoulderLeft = trackedBody.Joints[JointType.ShoulderLeft].Position;
                var shoulderLeftNorm = trackedBody.JointOrientations[JointType.ShoulderLeft].Orientation;
                var shoulderRight = trackedBody.Joints[JointType.ShoulderRight].Position;
                var shoulderRightNorm = trackedBody.JointOrientations[JointType.ShoulderRight].Orientation;

                var hipLeft = trackedBody.Joints[JointType.HipLeft].Position;
                var hipRight = trackedBody.Joints[JointType.HipRight].Position;

                switch (trackedBody.HandRightState)
                {
                    case HandState.Open:
                        bodyInfo.RHandOpen = true;
                        break;
                    case HandState.Closed:
                        bodyInfo.RHandOpen = false;
                        break;
                    case HandState.Lasso:
                        bodyInfo.RHandOpen = false;
                        break;
                }

                switch (trackedBody.HandLeftState)
                {
                    case HandState.Open:
                        bodyInfo.LHandOpen = true;
                        break;
                    case HandState.Closed:
                        bodyInfo.LHandOpen = false;
                        break;
                    case HandState.Lasso:
                        bodyInfo.LHandOpen = false;
                        break;
                }

                var rollRefRight = new CameraSpacePoint();
                rollRefRight.X = shoulderRight.X;
                rollRefRight.Y = elbowRight.Y;
                rollRefRight.Z = elbowRight.Z;

                var rollRefLeft = new CameraSpacePoint();
                rollRefLeft.X = shoulderLeft.X;
                rollRefLeft.Y = elbowLeft.Y;
                rollRefLeft.Z = elbowLeft.Z;

                /*if (elbowRight.Y < shoulderRight.Y)
                {
                    // Stores the right shoulder roll in radians
                    bodyInfo.angles[0] = angleCalc3D(rollRefRight, shoulderRight, elbowRight);
                }

                if (elbowLeft.Y < elbowRight.Y)
                {
                    // Stores the left shoulder roll in radians
                    bodyInfo.angles[1] = angleCalc3D(rollRefLeft, shoulderLeft, elbowLeft);
                }*/


                bodyInfo.angles[0] = getShoulderRoll(shoulderRight, elbowRight, hipRight);
                bodyInfo.angles[1] = getShoulderRoll(shoulderLeft, elbowLeft, hipLeft);

                // Stores the right elbow roll in radians
                bodyInfo.angles[2] = 0 - angleCalc3D(shoulderRight, elbowRight, wristRight);
                // Stores the left elbow roll in radians
                bodyInfo.angles[3] = 0 - angleCalc3D(shoulderLeft, elbowLeft, wristLeft);

                // Shoulder pitch should be same as shoulder roll but with angleCalcYZ
                // Stores the right shoulder pitch in radians
                bodyInfo.angles[4] = 0 - angleCalcYZ(hipRight, shoulderRight, elbowRight) * 1.5f;
                // Stores the left shoulder pitch in radians
                bodyInfo.angles[5] = 0 - angleCalcYZ(hipLeft, shoulderLeft, elbowLeft) * 1.5f;
            }
            else
            {
                bodyInfo.noTrackedBody = true;
            }
            return bodyInfo;
        }

        // Calculates angle between a<-b and b->c for situation a->b->c
        private static float getShoulderRoll(CameraSpacePoint shoulder, CameraSpacePoint elbow, CameraSpacePoint hip)
        {
            CameraSpacePoint xzRef = new CameraSpacePoint();
            xzRef.X = shoulder.X;
            xzRef.Y = elbow.Y;
            xzRef.Z = elbow.Z;

            var anglexz = angleCalcXZ(xzRef, shoulder, elbow);
            var anglexy = angleCalcXY(hip, shoulder, elbow);

            return (float)(anglexz * anglexy) / 1.2f;
        }

        // Calculates angle between a<-b and b->c for situation a->b->c
        private static float angleCalc3D(CameraSpacePoint a, CameraSpacePoint b, CameraSpacePoint c)
        {
            var ba = new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            var bc = new Vector3D(c.X - b.X, c.Y - b.Y, c.Z - b.Z);

            var angle = (float)Vector3D.AngleBetween(ba, bc); // degrees
            return (float)(Math.PI / 180) * angle; // radians
        }



        // Calculates angle between a<-b and b->c for situation a->b->c
        private static float angleCalcXZ(CameraSpacePoint a, CameraSpacePoint b, CameraSpacePoint c)
        {
            var ba = new Vector3D(a.X - b.X, 0, a.Z - b.Z);
            var bc = new Vector3D(c.X - b.X, 0, c.Z - b.Z);

            var angle = (float)Vector3D.AngleBetween(ba, bc); // degrees
            return (float)(Math.PI / 180) * angle; // radians
        }

        // Calculates angle between a<-b and b->c for situation a->b->c on XY plane only
        private static float angleCalcXY(CameraSpacePoint a, CameraSpacePoint b, CameraSpacePoint c)
        {
            var ba = new Vector3D(a.X - b.X, a.Y - b.Y, 0);
            var bc = new Vector3D(c.X - b.X, c.Y - b.Y, 0);

            var angle = (float)Vector3D.AngleBetween(ba, bc); // degrees

            return (float)(Math.PI / 180) * angle; // radians
        }

        // Calculates angle between a<-b and b->c for situation a->b->c on YZ plane only
        private static float angleCalcYZ(CameraSpacePoint a, CameraSpacePoint b, CameraSpacePoint c)
        {
            var ba = new Vector3D(0, a.Y - b.Y, a.Z - b.Z);
            var bc = new Vector3D(0, c.Y - b.Y, c.Z - b.Z);

            var angle = (float)Vector3D.AngleBetween(ba, bc); // degrees
            return (float)(Math.PI / 180) * angle; // radians
        }

        /// ********************************************************
        /// 
        ///                     TIMER
        /// 
        /// ********************************************************

        /// <summary>
        /// Timer to rate limit NAO joint updates
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void motionTimer_Tick(object sender, EventArgs e)
        {
            // Gets array of info from bodyProcessing
            info = calculateAngles();

            if (!info.noTrackedBody && !isPlaying)
            {
                //Recording
                if (isRecording)
                {
                    moveList.Add(new NaoMovement() { bodyInfo = info, timeDifference = stopwatch.ElapsedMilliseconds });
                }

                // Generate calibrated angles
                for (var x = 0; x < 6; x++)
                {
                    info.angles[x] = info.angles[x] - offset[x]; // adjustment to work with NAO robot angles
                }

                if (info.angles[4] < -2.0f) info.angles[4] = -2.0f;
                if (info.angles[5] < -2.0f) info.angles[5] = -2.0f;

                if (info.angles[4] > 2.0f) info.angles[4] = 2.0f;
                if (info.angles[5] > 2.0f) info.angles[5] = 2.0f;



                // Check if updates should be sent to NAO
                if (allowNaoUpdates)
                {
                    // Check to make sure that angle has changed enough to send new angle and update angle if it has
                    for (var x = 0; x < 6; x++)
                    {
                        if ((Math.Abs(oldAngles[x] - info.angles[x]) > .1 || Math.Abs(info.angles[x] - info.angles[x]) < .1))
                        {
                            oldAngles[x] = info.angles[x];
                            updateNAO(info.angles[x], invert ? invertedJointNames[x] : jointNames[x]);
                        }
                    }

                    // update right hand
                    switch (info.RHandOpen)
                    {
                        case true:
                            if (rHandStatus == "open")
                            {
                                break;
                            }
                            rHandStatus = "open";
                            if (invert)
                            {
                                naoMotion.openHand("LHand");
                                break;
                            }
                            naoMotion.openHand("RHand");
                            break;
                        case false:
                            if (rHandStatus == "closed")
                            {
                                break;
                            }
                            rHandStatus = "closed";
                            if (invert)
                            {
                                naoMotion.closeHand("LHand");

                                break;
                            }
                            naoMotion.closeHand("RHand");
                            break;
                    }

                    // update left hand
                    switch (info.LHandOpen)
                    {
                        case true:
                            if (lHandStatus == "open")
                            {
                                break;
                            }
                            lHandStatus = "open";
                            if (invert)
                            {
                                naoMotion.openHand("RHand");
                                break;
                            }
                            naoMotion.openHand("LHand");
                            break;
                        case false:
                            if (lHandStatus == "closed")
                            {
                                break;
                            }
                            lHandStatus = "closed";
                            if (invert)
                            {
                                naoMotion.closeHand("RHand");

                                break;
                            }
                            naoMotion.closeHand("LHand");
                            break;
                    }
                }
            }

            if (pNewTick != null)
            {
                UIinfo = info;
                pNewTick(this, EventArgs.Empty);
            }
        }

        /// ********************************************************
        /// 
        ///                     KINECT EVENTS
        /// 
        /// ********************************************************

        /// <summary>
        /// Event handler for new frames created by the kinectBody class
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void kinectInterface_NewFrame(object sender, EventArgs e)
        {
            // Gets the image from kinectInterface class and updates the image in the UI
            currentFrame = kinectInterface.getImage();

            if (pNewFrame != null)
            {
                pNewFrame(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Event handler for new frames created by the kinectBody class
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void kinectInterface_NewSpeech(object sender, EventArgs e)
        {
            var result = kinectInterface.getResult();
            var semanticResult = kinectInterface.getSemanticResult();
            var confidence = kinectInterface.getConfidence();
            MessageBox.Show(result + " "+ semanticResult+" ");
            // If confidence of recognized speech is greater than 60%
            if (confidence > 0.6)
            {
                // Debug output, tells what phrase was recongnized and the confidence
                speechStatus = "Recognized: " + result + " \nConfidence: " + confidence;
                MessageBox.Show(speechStatus);
                if (semanticResult == "on")
                {
                    speechResult = true;
                }

                if (semanticResult == "off")
                {
                    speechResult = false;
                }
            }
            else // Else say that it was rejected and confidence
            {
                speechStatus = "Rejected " + " \nConfidence: " + confidence;
            }

            if (pNewSpeech != null)
            {
                pNewSpeech(this, EventArgs.Empty);
            }
        }


        /// ********************************************************
        /// 
        ///                    NAO METHODS
        /// 
        /// ********************************************************

        private void updateNAO(float angle, string joint)
        {
            // RShoulderRoll and RElbowRoll require inverted angles
            if (joint == "RShoulderRoll" || joint == "LElbowRoll")
            {
                // Invert the angle
                angle = (0 - angle);
            }

            // Check for error when moving joint
            if (!naoMotion.moveJoint(angle, joint))
            {
                //debug3.Text = "Exception occured when communicating with NAO check C:\\NAO Motion\\ for details";
            }

            if (joint == "RElbowRoll")
            {
                Console.WriteLine(joint + ": " + angle);
            }
        }
    }
}
