/*
 * This file was created by Austin Hughes and Stetson Gafford
 * Last Modified: 2014-09-10
 */

// System Imports
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using System.Collections.Generic;

namespace NAO_Kinect
{
    /// <summary>
    /// This class handles all the UI logic for the application
    /// and provides communication between the other classes
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Classes
        /// </summary>
        private Processing processing;

        /// <summary>
        /// Data structures
        /// </summary>
        private Processing.BodyInfo info;
        private List<NaoMovement> nmList;

        /// <summary>
        /// Class constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// ********************************************************
        /// 
        ///                     UI EVENTS
        /// 
        /// ********************************************************

        /// <summary>
        /// Event handler for the main UI being loaded
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Creates the bodyProcessing class and sends to kinectSkeleton reference to it
            processing = new Processing();

            processing.pNewFrame += processing_imageUpdate;
            processing.pNewSpeech += processing_speechUpdate;
            processing.pNewTick += processing_angleUpdate;

            buttonDiscard.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Called when the window is unloaded, cleans up anything that needs to be cleaned before the program exits
        /// </summary>
        /// <param name="sender"> object that created the event </param>
        /// <param name="e"> any additional arguments </param>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (processing != null)
            {
                processing.cleanUp();
            }
        }

        /// <summary>
        /// Event handler for start button click, enables NAO connection
        /// </summary>
        /// <param name="sender"> Object that sent the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            processing.connect(ipBox.Text);

            naoStatus.Content = "NAO - CONNECTED";

            stopButton.IsEnabled = true;
            startButton.IsEnabled = false;
        }

        /// <summary>
        /// Event handler for stop button click, disables NAO connection
        /// </summary>
        /// <param name="sender"> Object that sent the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            processing.disconnect();

            naoStatus.Content = "NAO - DISCONNECTED";

            stopButton.IsEnabled = false;
            startButton.IsEnabled = true;
        }

        /// <summary>
        /// Event handler for invert check box being checked
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void invertCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (processing != null)
            {
                processing.setInvert(true);
            }
        }

        /// <summary>
        /// Event handler for invert check box being unchecked
        /// </summary>
        /// <param name="sender"> Object that generated the event </param>
        /// <param name="e"> Any additional arguments </param>
        private void invertCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (processing != null)
            {
                processing.setInvert(false);
            }
        }

        private void processing_imageUpdate(object sender, EventArgs e)
        {
            Image.Source = processing.getFrame();
        }

        private void processing_angleUpdate(object sender, EventArgs e)
        {
            info = processing.getBodyInfo();
            // This is pretty awful

            display_angle(info.angles[0], "RSR", rsrLabel, rsrSlider);
            display_angle(info.angles[1], "LSR", lsrLabel, lsrSlider);
            display_angle(info.angles[2], "RER", rerLabel, rerSlider);
            display_angle(info.angles[3], "LER", lerLabel, lerSlider);
            display_angle(info.angles[4], "RSP", rspLabel, rspSlider);
            display_angle(info.angles[5], "LSP", lspLabel, lspSlider);

        }

        private void display_angle(float angle, string name, System.Windows.Controls.Label label, System.Windows.Controls.ProgressBar slider){
            
            label.Content = name + " " + String.Format("{0:F1}", angle);
            slider.Value = angle;
            if (angle < slider.Minimum)
            {
                slider.Background = Brushes.Transparent;
            }
            else if (angle > slider.Maximum)
            {
                slider.Foreground = Brushes.Green;
            }
            else
            {
                slider.Foreground = Brushes.LimeGreen;
                slider.Background = Brushes.LightGray;
            }
        }

        private void processing_speechUpdate(object sender, EventArgs e)
        {
            audioStatus.Text = processing.getSpeechStatus();

            if (!processing.getSpeechResult() && stopButton.IsEnabled)
            {
                stopButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }

            if (processing.getSpeechResult() && startButton.IsEnabled)
            {
                startButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
        }

        private void lspSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void buttonRecord_Click(object sender, RoutedEventArgs e)
        {
            if (buttonRecord.Content.ToString().Equals("Record"))
            {
                buttonDiscard.Visibility = Visibility.Visible;
                buttonRecord.Content = "Save";
                //start recording
                processing.startRecording();
            }
            else
            {
                buttonDiscard.Visibility = Visibility.Hidden;
                buttonRecord.Content = "Record";
                //stop recording to save
                nmList=processing.stopRecording();
            }
        }

        private void buttonDiscard_Click(object sender, RoutedEventArgs e)
        {
            buttonDiscard.Visibility = Visibility.Hidden;
            buttonRecord.Content = "Record";
            //discard recording
            processing.discardRecording();            
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            processing.playMoveList(nmList);
        }
    }
}
