using Newtonsoft.Json;

using RobotControl.ClassLibrary;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RobotControl.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken = cancellationTokenSource.Token;
        private readonly object buttonLock = new object();
        private readonly string configurationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RobotControlConfiguration.json");

        //private          IImageRecognitionFromCamera imageRecognitionFromCamera;
        private          IRobotCommunication         robotCommunication;
        private string[] labelsOfObjectsToDetect;
        private TimeChart accelXTimeChart;
        private TimeChart accelYTimeChart;
        private TimeChart accelZTimeChart;
        private SpeechSynthesizer speechSynthesizer = null;
        private Thread robotThread;

        private Dictionary<string, DateTime> latestSay = new Dictionary<string, DateTime>();

        private bool alreadyFoundScanPower = true;

        public event PropertyChangedEventHandler PropertyChanged;
        public Random Random = new Random(DateTime.Now.Millisecond);



        private string[] CompassPointingToValues = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"};

        public MainWindow()
        {
            InitializeComponent();
            accelXTimeChart = new TimeChart(accelXChart, -10, 10, TimeSpan.FromMilliseconds(100));
            accelYTimeChart = new TimeChart(accelYChart, -10, 10, TimeSpan.FromMilliseconds(100));
            accelZTimeChart = new TimeChart(accelZChart, -10, 10, TimeSpan.FromMilliseconds(100));
            DataContext = this;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Dispatcher.Invoke(() =>
            {
                cameraComboBox.Items.Clear();
                GetAllConnectedCameras().ForEach(c => cameraComboBox.Items.Add(c));
                speechSynthesizer = new SpeechSynthesizer();
                HandleConfigurationData();
            });
        }

        public static List<string> GetAllConnectedCameras()
        {
            var cameraNames = new List<string>();
            var captionRegex = new Regex("camera|webcam", RegexOptions.IgnoreCase);
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
            {
                foreach (var device in searcher.Get())
                {
                    if (device == null || device["Caption"] == null || !captionRegex.IsMatch((string)device["Caption"]))
                    {
                        continue;
                    }

                    string caption = device["Caption"].ToString();
                    if (!cameraNames.Contains(caption))
                    {
                        cameraNames.Add(caption);
                    }
                }
            }

            return cameraNames;
        }

        private void NotifyPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private async void startStop_ClickAsync(object sender, RoutedEventArgs e)
        {
            if ((string)startStop.Content == "Start")
            {
                var rc = new RobotCommunicationParameters
                {
                    BaudRate = int.Parse(this.baudRateComboBox.Text)
                };
                robotCommunication = ClassFactory.CreateRobotCommunication(rc);
                await robotCommunication.StartAsync();
            }

            LockedExec(() =>
            {
                btnCalibrateCompass.IsEnabled = false;
                if ((string)startStop.Content == "Start")
                {
                    PleaseSay($"Started seeking for {string.Join(", ", labelsOfObjectsToDetect)}");
                    SaveConfigurationData();

                    robotThread = new Thread(WorkerThreadProc);
                    robotThread.Start(this);
                    startStop.Content = "Stop";
                }
                else
                {
                    PleaseSay($"Stopping...");
                    cancellationTokenSource.Cancel();
                    robotThread.Join();
                    startStop.Content = "Start";
                    PleaseSay($"Stopped.");
                    this.Close();
                }
            });
        }

        private void LockedExec(Action action)
        {
            lock (buttonLock)
            {
                var start = DateTime.Now;
                var previousCursor = this.Cursor;
                this.Cursor = Cursors.Wait;

                action.Invoke();

                var waitMilliseconds = 2000 - (int)(DateTime.Now - start).TotalMilliseconds;
                if (waitMilliseconds > 0)
                {
                    Thread.Sleep(waitMilliseconds);
                }
                this.Cursor = previousCursor;
            }
        }

        private void InvokeLockedExec(Action action)
        {
            lock (buttonLock)
            {
                Dispatcher.Invoke(() =>
                {
                    var start = DateTime.Now;
                    var previousCursor = this.Cursor;
                    this.Cursor = Cursors.Wait;

                    action.Invoke();

                    var waitMilliseconds = 2000 - (int)(DateTime.Now - start).TotalMilliseconds;
                    if (waitMilliseconds > 0)
                    {
                        Thread.Sleep(waitMilliseconds);
                    }
                    this.Cursor = previousCursor;
                });
            }
        }

        private void SaveConfigurationData() =>
            LockedExec(() =>
                {
                    this.Configuration.EnableAudio          = IsChecked(this.enableAudioCheckBox);
                    this.Configuration.ScanForObjects       = IsChecked(this.scanForObjects);
                    this.Configuration.LeftMotorMultiplier  = this.LMult;
                    this.Configuration.RightMotorMultiplier = this.RMult;
                    this.Configuration.SerialPortBaudrate   = int.Parse(this.baudRateComboBox.Text);
                    this.Configuration.ScanPower            = this.ScanPower;
                    this.Configuration.LurchPower           = this.LurchPower;

                    this.Configuration.ObjectsToDetect.Clear();
                    for (int i = 0; i < this.objectsToDetectComboBox.Items.Count; i++)
                    {
                        CheckBox checkBox = (CheckBox)this.objectsToDetectComboBox.Items[i];
                        if (IsChecked(checkBox))
                        {
                            this.Configuration.ObjectsToDetect.Add((string)checkBox.Content);
                        }
                    }

                    File.WriteAllText(this.configurationPath, JsonConvert.SerializeObject(this.configuration));
                });


        private void HandleConfigurationData()
        {
            this.enableAudioCheckBox.IsChecked  = this.Configuration.EnableAudio;
            this.scanForObjects.IsChecked       = this.Configuration.ScanForObjects;
            this.LMult                          = this.Configuration.LeftMotorMultiplier;
            this.RMult                          = this.Configuration.RightMotorMultiplier;
            this.baudRateComboBox.SelectedValue = this.Configuration.SerialPortBaudrate;
            this.baudRateComboBox.Text          = this.Configuration.SerialPortBaudrate.ToString();
            this.ScanPower                      = this.Configuration.ScanPower;
            this.LurchPower                     = this.Configuration.LurchPower;

            for (int i = 0; i < this.objectsToDetectComboBox.Items.Count; i++)
            {
                CheckBox checkBox = (CheckBox)this.objectsToDetectComboBox.Items[i];
                checkBox.IsChecked = this.Configuration.ObjectsToDetect.Contains((string)checkBox.Content);
            }
        }

        private void scanForObjects_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void testMotors_Click(object sender, RoutedEventArgs e)
        {

        }

        private static async void WorkerThreadProc(object obj)
        {
            var thisWindow = (MainWindow)obj;
            int cameraId = 0;
            await thisWindow.Dispatcher.InvokeAsync(() =>
            {
                cameraId = Math.Max(0, thisWindow.cameraComboBox.SelectedIndex);
            });

            ImageRecognitionFromCameraParameters ip = new ImageRecognitionFromCameraParameters
            {
                OnnxFilePath = "TinyYolo2_model.onnx",
                LabelsOfObjectsToDetect = thisWindow.labelsOfObjectsToDetect,
                CameraId = cameraId,
            };

            using (var imageRecognitionFromCamera = ClassFactory.CreateImageRecognitionFromCamera(ip))
            {
                await imageRecognitionFromCamera.StartAsync();

                await thisWindow.HandleImageRecognitionFromCameraResultAsync(
                    await imageRecognitionFromCamera.GetAsync(), thisWindow.robotCommunication);

                if (thisWindow.CurrentL != thisWindow.LurchPower)
                {
                    await thisWindow.ScanRightAsync(thisWindow.robotCommunication);
                }

                int previousScanPower = thisWindow.ScanPower;
                while (!thisWindow.cancellationToken.IsCancellationRequested)
                {
                    var start = DateTime.Now;
                    try
                    {
                        var robotData = await thisWindow.robotCommunication.ReadAsync();
                        await thisWindow.HandleRobotCommuniationResultAsync(robotData);

                        var imageData = await imageRecognitionFromCamera.GetAsync();
                        await thisWindow.HandleImageRecognitionFromCameraResultAsync(imageData, thisWindow.robotCommunication);
                        var elapsed = (DateTime.Now - start).TotalMilliseconds;
                        await thisWindow.Dispatcher.InvokeAsync(() => thisWindow.lblObjectData.Content = elapsed.ToString());

                        if (thisWindow.ScanPower != previousScanPower)
                        {
                            await thisWindow.robotCommunication.StopMotorsAsync();
                            if (thisWindow.Configuration.ScanPower > 0)
                            {
                                await thisWindow.ScanRightAsync(thisWindow.robotCommunication);
                            }
                            previousScanPower = thisWindow.ScanPower;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        thisWindow.PleaseSay("Scanning cancelled.");
                    }
                }
            }
        }

        private async Task HandleImageRecognitionFromCameraResultAsync(
            ImageRecognitionFromCameraResult imageData,
            IRobotCommunication robotCommunication)
        {
            if (imageData.HasData)
            {

                var objectPosition = imageData.XDeltaProportionFromBitmapCenter * 100;
                if (objectPosition < -5) // object is to the left
                {
                    await SetMotorsAsync(robotCommunication, -ScanPower, ScanPower);
                }
                else if (objectPosition > 5) // object is to the right
                {
                    await SetMotorsAsync(robotCommunication, ScanPower, -ScanPower);
                }
                else // object is straight ahead, CHARGE!
                {
                    PleaseSayButOnlyIfNotSaidInThePast30Seconds($"Ha! {imageData.Label}, I found you!");
                    using (var gr = Graphics.FromImage(imageData.Bitmap))
                    {
                        gr.DrawRectangle(new Pen(Color.Yellow, 10), 5, 5, imageData.Bitmap.Width - 5, imageData.Bitmap.Height - 5);
                    }

                    if (PleaseLurch)
                    {
                        PleaseSayButOnlyIfNotSaidInThePast30Seconds($"Charge!");
                        await SetMotorsAsync(robotCommunication, LurchPower, LurchPower);
                    }
                }
            }
            else
            {
                await ScanRightAsync(robotCommunication);
            }

            await Dispatcher.InvokeAsync(() =>
                objectDetectionImage.Source = Utilities.BitmapToBitmapImage(imageData.Bitmap));
        }

        private async Task ScanRightAsync(IRobotCommunication robotCommunication)
        {
            if (!alreadyFoundScanPower)
            {
                int power = 0;
                float accelY;
                for (power = 90; power < 255 && Math.Abs(accelY = (await robotCommunication.ReadAsync()).AccelY) < 3; power += 5)
                {
                    System.Diagnostics.Debug.WriteLine($"--> ScanRightAsync accelY {accelY} power {power}");
                    await SetMotorsAsync(robotCommunication, power, -power);
                }

                System.Diagnostics.Debug.WriteLine($"--> ScanRightAsync settled with power {power}");
                await Dispatcher.InvokeAsync(() =>
                {
                    ScanPower = power;
                });

                alreadyFoundScanPower = true;
            }
            else
            {
                await SetMotorsAsync(robotCommunication, ScanPower, -ScanPower);
            }
        }

        private async Task SetMotorsAsync(IRobotCommunication robotCommunication, int l, int r)
        {
            await robotCommunication.SetMotorsAsync((int)(l * LMult), (int)(r * RMult));
            CurrentL = l;
            CurrentR = r;
        }

        private void PleaseSayButOnlyIfNotSaidInThePast30Seconds(string s)
        {
            if (!latestSay.ContainsKey(s) || (DateTime.Now - latestSay[s]).TotalSeconds >= 30)
            {
                PleaseSay(s);
                latestSay[s] = DateTime.Now;
            }
        }

        private async Task HandleRobotCommuniationResultAsync(RobotCommunicationResult robotData)
        {
            if (robotData.DataType == RobotCommunicationResult.NoData)
            {
                return;
            }

            if (robotData.Distance <= 10)
            {
                await robotData.RobotCommunication.StopMotorsAsync();
            }

            await this.Dispatcher.InvokeAsync(() =>
            {
                this.AccelX   = robotData.AccelX;
                this.AccelY   = robotData.AccelY;
                this.AccelZ   = robotData.AccelZ;
                this.Distance = robotData.Distance;
                this.Voltage  = robotData.Voltage;
                this.Compass  = robotData.Compass;

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

        private void DisplayCompass(System.Windows.Controls.Image compassImage, float heading)
        {
            CompassHeading = heading / (180 / (Configuration.CompassReadingSouth - Configuration.CompassReadingNorth));
            var bitmap = new Bitmap((int)compassImage.Width, (int)compassImage.Height);
            var backgroundColor = new System.Drawing.SolidBrush(System.Drawing.Color.LightYellow);

            System.Drawing.Pen blackPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1);

            using (var gr = Graphics.FromImage(bitmap))
            {
                gr.FillRectangle(backgroundColor, 0, 0, bitmap.Width, bitmap.Height);
                float hw = (float)(bitmap.Width / 2.0);
                float hh = (float)(bitmap.Height / 2.0);
                float x1 = hw;
                float y1 = hh;
                float si = (float)Math.Sin(CompassHeading) * -1;
                float co = (float)Math.Cos(CompassHeading) * -1;
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

        private async void btnCalibrateCompass_Click(object sender, RoutedEventArgs e) =>
            await Task.Run(() => InvokeLockedExec(async () =>
            {
                var rc = new RobotCommunicationParameters { BaudRate = int.Parse(baudRateComboBox.Text) };

                using (var robotCommunication = ClassFactory.CreateRobotCommunication(rc))
                {
                    await robotCommunication.StartAsync();
                    switch (btnCalibrateCompass.Content)
                    {
                        case "Calibrate compass":
                            btnCalibrateCompass.Content = "Point North, then click here";
                            break;
                        case "Point North, then click here":
                            Configuration.CompassReadingNorth = (await robotCommunication.ReadAsync()).Compass;
                            btnCalibrateCompass.Content = "Now point South, then click here";
                            break;
                        case "Now point South, then click here":
                            Configuration.CompassReadingSouth = (await robotCommunication.ReadAsync()).Compass;
                            btnCalibrateCompass.Content = "Done calibrating compass";
                            btnCalibrateCompass.IsEnabled = false;
                            SaveConfigurationData();
                            break;
                    }
                }
            }));

        private void btnTurnRightOneDegree_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnTurnLeftOneDegree_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btnStopMotors_Click(object sender, RoutedEventArgs e) => await this.robotCommunication?.StopMotorsAsync();
        private void PleaseSay(string s) => new Thread(() => new SpeechSynthesizer().Speak(s)).Start();
        private bool IsChecked(CheckBox checkBox) => checkBox.IsChecked.HasValue ? checkBox.IsChecked.Value : false;
        private List<string> GetPropertyNames(object o) => o.GetType().GetProperties().Select(p => p.Name).ToList();
        private void saveConfiguration_Click(object sender, RoutedEventArgs e) => SaveConfigurationData();
        private float RadiansToDegrees(float radians) => (float)((((double)radians + 90) % 360) * Math.PI / 180.0);

    }
}
