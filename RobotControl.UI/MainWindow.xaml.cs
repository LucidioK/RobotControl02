using RobotControl.ClassLibrary;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RobotControl.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken = cancellationTokenSource.Token;
        private readonly Thread robotThread = new Thread(WorkerThreadProcAsync);
        private IImageRecognitionFromCamera imageRecognitionFromCamera;
        private IRobotCommunication robotCommunication;
        private string[] labelsOfObjectsToDetect;
        private TimeChart accelXTimeChart;
        private TimeChart accelYTimeChart;
        private TimeChart accelZTimeChart;

        public MainWindow()
        {
            InitializeComponent();
            accelXTimeChart = new TimeChart(accelXChart, -10, 10, TimeSpan.FromMilliseconds(100));
            accelYTimeChart = new TimeChart(accelYChart, -10, 10, TimeSpan.FromMilliseconds(100));
            accelZTimeChart = new TimeChart(accelZChart, -10, 10, TimeSpan.FromMilliseconds(100));
        }

        private void startStop_ClickAsync(object sender, RoutedEventArgs e)
        {
            imageRecognitionFromCamera = ClassFactory.CreateImageRecognitionFromCamera(new ImageRecognitionFromCameraParameters
                {
                    OnnxFilePath = "TinyYolo2_model.onnx",
                    LabelsOfObjectsToDetect = labelsOfObjectsToDetect,
                    CameraId = 0,
                });


            robotCommunication = ClassFactory.CreateRobotCommunication(new RobotCommunicationParameters
                {
                    BaudRate = int.Parse(baudRateComboBox.Text)
                });

            robotThread.Start(this);
         }

        private void scanForObjects_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void testMotors_Click(object sender, RoutedEventArgs e)
        {

        }

        private void saveConfiguration_Click(object sender, RoutedEventArgs e)
        {

        }

        private static async void WorkerThreadProcAsync(object obj)
        {
            var thisWindow = (MainWindow)obj;

            await thisWindow.robotCommunication.StartAsync();
            await thisWindow.imageRecognitionFromCamera.StartAsync();

            while (!thisWindow.cancellationToken.IsCancellationRequested)
            {
                var robotData = await thisWindow.robotCommunication.ReadAsync();
                await thisWindow.HandleRobotCommuniationResultAsync(robotData);

                var imageData = await thisWindow.imageRecognitionFromCamera.GetAsync();
                await thisWindow.HandleImageRecognitionFromCameraResultAsync(imageData);
            }
        }

        private async Task HandleImageRecognitionFromCameraResultAsync(ImageRecognitionFromCameraResult imageData) => await 
            this.Dispatcher.InvokeAsync(() =>
            {
                //this.lblObjectData.Content = eventDescriptor.Detail;
                this.objectDetectionImage.Source = Utilities.BitmapToBitmapImage(imageData.Bitmap);
                //this.speaker.OnEvent(new EventDescriptor { Name = EventName.PleaseSay, Detail = $"Ha! {eventDescriptor.Detail}, I will go after you!" });
            });

        private async Task HandleRobotCommuniationResultAsync(RobotCommunicationResult robotData)
        {
            if (robotData.Distance <= 10)
            {
                await robotCommunication.StopMotorsAsync();
            }

            await this.Dispatcher.InvokeAsync(() =>
            {
                this.lblAccelX.Content   = robotData.AccelX.ToString("0.0");
                this.lblAccelY.Content   = robotData.AccelY.ToString("0.0");
                this.lblAccelZ.Content   = robotData.AccelZ.ToString("0.0");
                this.lblDistance.Content = robotData.Distance.ToString("0.0");
                this.lblVoltage.Content  = robotData.Voltage.ToString("0.0");
                this.lblCompass.Content  = robotData.Compass.ToString("0.0");

                DisplayAcceleration(this.accelXImage, this.accelXTimeChart, robotData.AccelX, 1f, 2f, 3.5f);
                DisplayAcceleration(this.accelYImage, this.accelYTimeChart, robotData.AccelY, 1f, 2f, 3.5f);
                DisplayAcceleration(this.accelZImage, this.accelZTimeChart, (robotData.AccelZ - 10) % 10, 1f, 2f, 3.5f);
                DisplayCompass(this.CompassImage, robotData.Compass);
            });
        }

        private void objectsToDetectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var labels = new List<string>();
            foreach (var item in objectsToDetectComboBox.Items)
            {
                var cb = item as CheckBox;
                if (cb != null && cb.IsChecked.GetValueOrDefault())
                {
                    labels.Add(cb.Content.ToString());
                }

            }

            labelsOfObjectsToDetect = labels.ToArray();
            startStop.IsEnabled = labels.Count > 0 && startStop.Content.ToString() == "Start";
        }

        private void DisplayCompass(System.Windows.Controls.Image compassImage, float compassHeading)
        {
            var bitmap = new Bitmap((int)compassImage.Width, (int)compassImage.Height);
            var backgroundColor = new System.Drawing.SolidBrush(System.Drawing.Color.LightYellow);

            // We receive 0 as the top quadrant, while Math considers 0 as the right quadrant.
            compassHeading = (float)((((double)compassHeading + 90) % 360) * Math.PI / 180.0);
            System.Drawing.Pen blackPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1);

            using (var gr = Graphics.FromImage(bitmap))
            {
                gr.FillRectangle(backgroundColor, 0, 0, bitmap.Width, bitmap.Height);
                float hw = (float)(bitmap.Width / 2.0);
                float hh = (float)(bitmap.Height / 2.0);
                float x1 = hw;
                float y1 = hh;
                float si = (float)Math.Sin(compassHeading) * -1;
                float co = (float)Math.Cos(compassHeading) * -1;
                float x2 = x1 + (hw * co);
                float y2 = y1 + (hh * si);
                gr.DrawLine(blackPen, x1, y1, x2, y2);
            }

            compassImage.Source = Utilities.BitmapToBitmapImage(bitmap);
        }

        private void DisplayAcceleration(
            System.Windows.Controls.Image accelDisplay,
            TimeChart timeChart,
            float acceleration,
            float greenThreshold,
            float yellowThreshold,
            float redThreshold)
        {
            var bitmap = new Bitmap((int)accelDisplay.Width, (int)accelDisplay.Height);
            System.Drawing.Color backgroundColor = System.Drawing.Color.Green;
            if (Math.Abs(acceleration) >= redThreshold) backgroundColor = System.Drawing.Color.Red;
            else if (Math.Abs(acceleration) >= yellowThreshold) backgroundColor = System.Drawing.Color.Yellow;
            var backgroundBrush = new System.Drawing.SolidBrush(backgroundColor);
            using (var gr = Graphics.FromImage(bitmap))
            {
                gr.FillRectangle(backgroundBrush, 0, 0, bitmap.Width, bitmap.Height);
                float x1 = (float)(bitmap.Width / 2.0);
                float y1 = bitmap.Height - 1;
                float x2 = x1 + bitmap.Width * (acceleration / 10);
                float y2 = (acceleration / 10) * bitmap.Height;
                gr.DrawLine(new System.Drawing.Pen(System.Drawing.Color.Black, 1), x1, y1, x2, y2);
            }

            accelDisplay.Source = Utilities.BitmapToBitmapImage(bitmap);

            timeChart?.Post(acceleration, backgroundColor);
        }

        private void objectsToDetectSelectionChanged(object sender, RoutedEventArgs e)
        {
            var labels = new List<string>();
            foreach (var item in objectsToDetectComboBox.Items)
            {
                var cb = item as CheckBox;
                if (cb != null && cb.IsChecked.GetValueOrDefault())
                {
                    labels.Add(cb.Content.ToString());
                }

            }

            labelsOfObjectsToDetect = labels.ToArray();
            startStop.IsEnabled = labels.Count > 0 && startStop.Content.ToString() == "Start";
        }

        private void runMotors_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
