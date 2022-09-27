/*
 * This file was created by Austin Hughes and Stetson Gafford
 * Last Modified: 2014-09-04
 */

// System imports
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

// Microsoft imports
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using Microsoft.Kinect;

namespace NAO_Kinect
{
    /// <summary>
    /// This class handles processing voice events from the Kinect
    /// </summary>
    class KinectVoice
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Speech engine initialization
        /// </summary>
        private SpeechRecognitionEngine sre;

        /// <summary>
        /// Event handler for updated speech events
        /// </summary>
        public event EventHandler SpeechEvent;

        /// <summary>
        /// Stream for 32b-16b conversion.
        /// </summary>
        private KinectAudioStream convertStream = null;

        /// <summary>
        /// Variables to hold results of speech
        /// </summary>
        string result;
        string semanticResult;
        float confidence;

        /// <summary>
        /// Class constructor, sets Kinect sensor
        /// </summary>
        /// <param name="kinect"></param>
        public KinectVoice(KinectSensor kinect)
        {
            sensor = kinect;
        }

        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// 
        /// This code is provided by Microsoft
        /// 
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// Starts the voice recognition engine
        /// </summary>
        public void startVoiceRecognition()
        {
            // Start voice recongintion
            try
            {
                var ri = GetKinectRecognizer();  // Initializes Kinect Recognizer for speech
                sre = new SpeechRecognitionEngine(ri.Id);   // Initializes Speech Recognition Engine

                // Create simple string array that contains speech recognition data and interpreted values
                string[] valuesHeard = { "computer start", "computer stop" };
                string[] valuesInterpreted = { "on", "off"};

                var commands = new Choices(); // Initializes Choices for engine

                // Adds all values in string arrays to commands for engine
                for (var i = 0; i < valuesHeard.Length; i++)
                {
                    commands.Add(new SemanticResultValue(valuesHeard[i], valuesInterpreted[i]));
                }

                // Submits commands to Grammar Builder for engine
                var gb = new GrammarBuilder {Culture = ri.Culture};
                gb.Append(commands);

                var g = new Grammar(gb);

                sre.LoadGrammar(g);

                // Set event handler for when speech is recognized
                sre.SpeechRecognized += SpeechRecognized;

                IReadOnlyList<AudioBeam> audioBeamList = sensor.AudioSource.AudioBeams;
                Stream audioStream = audioBeamList[0].OpenInputStream();

                // create the convert stream
                convertStream = new KinectAudioStream(audioStream);

                convertStream.SpeechActive = true;

                // Tells the speech engine where to find the audio stream
                sre.SetInputToAudioStream(convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                sre.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception e) // Catch to make sure if no Kinect is found program does not crash
            {
                MessageBox.Show("Error starting audio stream: " + e.Message);
            }
        }

        public void end()
        {
            if (null != this.convertStream)
            {
                convertStream.SpeechActive = false;
                convertStream.Dispose();
            }

            if (null != sre)
            {
                sre.SpeechRecognized -= this.SpeechRecognized;
                sre.RecognizeAsyncStop();
                sre.Dispose();
            }
        }

        /// <summary>
        /// Determines confidence level of voice command and launches the voice command.
        /// Also puts debugging text into the main window.
        /// </summary>
        /// <param name="sender"> Encapsulated calling method sending the event. </param>
        /// <param name="e"> The recognized argument. </param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            result = e.Result.Text;
            semanticResult = e.Result.Semantics.Value.ToString();
            confidence = e.Result.Confidence;

            OnSpeechEvent();
        }

        /// <summary>
        /// Triggers speech recognized
        /// </summary>
        private void OnSpeechEvent()
        {
            var handler = SpeechEvent;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Returns the most recent recognized word
        /// </summary>
        /// <returns> result </returns>
        public string getResult()
        {
            return result;
        }

        /// <summary>
        /// Returns the semeantic result of the most recent regcognized word
        /// </summary>
        /// <returns> semanticResult </returns>
        public string getSemanticResult()
        {
            return semanticResult;
        }

        /// <summary>
        /// Returns the most recent confidence
        /// </summary>
        /// <returns> confidence </returns>
        public float getConfidence()
        {
            return confidence;
        }

    }
}
