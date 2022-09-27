/*
 * This file was created by Austin Hughes
 * Last Modified: 2014-09-04
 */

// System Imports
using System;
using System.Windows.Media;

// Microsoft imports
using Microsoft.Kinect;
using Microsoft.Speech.Synthesis;
using Microsoft.Speech.Recognition;
using System.Globalization;
using System.Windows;

namespace NAO_Kinect
{

    public delegate void NewFrameEventHandler(object sender, EventArgs e);
    public delegate void NewSpeechEventHandler(object sender, EventArgs e);

    /// <summary>
    /// Class to allow Kinect to be threaded
    /// </summary>
    class KinectInterface
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Classes
        /// </summary>
        private KinectVoice kinectVoice;
        private KinectBody kinectBody;

        /// <summary>
        /// Variables
        /// </summary>
        private string result;
        private string semanticResult;
        private float confidence;
        private ImageSource image;

        /// <summary>
        /// Events
        /// </summary>
        public event NewFrameEventHandler NewFrame;
        public event NewSpeechEventHandler NewSpeech;

        static SpeechSynthesizer ss = new SpeechSynthesizer();
        static SpeechRecognitionEngine sre;

        ~KinectInterface()
        {
            end();
        }

        public void start()
        {
            // Get the Kinect Sensor
            sensor = KinectSensor.GetDefault();

            try
            {
                sensor.Open();
                sensor.IsAvailableChanged += Sensor_IsAvailableChanged;
            }
            catch (Exception)
            {
                sensor = null;
            }

            // Send the sensor to the voice class and setup the event handler
            //kinectVoice = new KinectVoice(sensor);
            //kinectVoice.SpeechEvent += kinectVoice_NewSpeech;

            // Send the sensor to the skeleton class and setup the event handler
            kinectBody = new KinectBody(sensor);
            kinectBody.NewFrame += kinectBody_NewFrame;

            // Enables voice reconginition
            //kinectVoice.startVoiceRecognition();

            ss.SetOutputToDefaultAudioDevice();
            CultureInfo ci = new CultureInfo("en-us");
            sre = new SpeechRecognitionEngine(ci);
            sre.SetInputToDefaultAudioDevice();
            sre.SpeechRecognized += sre_SpeechRecognized;

            //voice commands
            Choices vCommands = new Choices();
            vCommands.Add("Start Recording");
            vCommands.Add("Stop Recording");
            vCommands.Add("Discard Recording");

            GrammarBuilder gb_Commands = new GrammarBuilder();
            gb_Commands.Append(vCommands);
            Grammar g_Commands = new Grammar(gb_Commands);
            sre.LoadGrammarAsync(g_Commands);
            sre.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string txt = e.Result.Text;
            float confidence = e.Result.Confidence;
            MessageBox.Show("\nRecognized: " + txt +" "+confidence );
            if (confidence < 0.60) return;
            /*if (txt.IndexOf("speech on") >= 0)
            {
                Console.WriteLine("Speech is now ON");
            }
            if (txt.IndexOf("speech off") >= 0)
            {
                Console.WriteLine("Speech is now OFF");
            }*/
        }

        public void end()
        {
            if (sensor != null)
            {
                //sensor.Close();
            }

            kinectVoice.end();
        }

        public Body getBody()
        {
            return kinectBody.getBody();
        }

        public ImageSource getImage()
        {
            return image;
        }

        public string getResult()
        {
            return result;
        }

        public string getSemanticResult()
        {
            return semanticResult;
        }

        public float getConfidence()
        {
            return confidence;
        }

        /// <summary>
        /// Event handler for new frames created by the kinectSkeleton class
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void kinectBody_NewFrame(object sender, EventArgs e)
        {
            image = kinectBody.getImage();

            if (NewFrame != null)
            {
                NewFrame(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Event handler for speech events
        /// </summary>
        /// <param name="sender"> Object that sent the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void kinectVoice_NewSpeech(object sender, EventArgs e)
        {
            // Variables for recongized speech, final speech result, and confidence
            result = kinectVoice.getResult();
            semanticResult = kinectVoice.getSemanticResult();
            confidence = kinectVoice.getConfidence();

            if (NewSpeech != null)
            {
                NewSpeech(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender"> Object sending the event </param>
        /// <param name="e"> Event arguments </param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // Do nothing for now
        }
    }
}
