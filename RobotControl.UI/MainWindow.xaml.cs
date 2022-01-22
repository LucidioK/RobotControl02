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
        class TurnParam
        {
            public MainWindow MainWindow { get; set; }
            public float DesiredDirection { get; set; }
        }

        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken = cancellationTokenSource.Token;
        private readonly object buttonLock = new object();
        private readonly string configurationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RobotControlConfiguration.json");
        private readonly object handleImageRecognitionFromCameraResultAsyncLock = new object();
        //private          IImageRecognitionFromCamera imageRecognitionFromCamera;
        private          IRobotCommunication         robotCommunication;
        private string[] labelsOfObjectsToDetect;

        private SpeechSynthesizer speechSynthesizer = null;

        private Dictionary<string, DateTime> latestSay = new Dictionary<string, DateTime>();

        private bool alreadyFoundScanPower = true;

        public event PropertyChangedEventHandler PropertyChanged;
        public Random Random = new Random(DateTime.Now.Millisecond);

        private Thread scanThread;
        private Thread updateRobotDataThread;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Dispatcher.Invoke(() =>
            {
                speechSynthesizer = new SpeechSynthesizer();
                HandleConfigurationData();
                RobotMode = "N";
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


        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var previousCursor = this.Cursor;
            this.Cursor        = Cursors.Wait;
            var rc             = new RobotCommunicationParameters
            {
                BaudRate       = int.Parse(this.baudRateComboBox.Text)
            };
            robotCommunication = ClassFactory.CreateRobotCommunication(rc);
            await robotCommunication.StartAsync();
            this.Cursor        = previousCursor;

            updateRobotDataThread = new Thread(UpdateRobotDataThreadProc);
            updateRobotDataThread.Start(this);
            Dispatcher.Invoke(() =>
            {
                btnConnect.IsEnabled             = false;
                btnScan.IsEnabled                = true;
                runMotors.IsEnabled              = true;
                RobotMode                        = "C";
            });
        }

        private void btnScanStop_Click(object sender, RoutedEventArgs e) =>
            LockedExec(() =>
            {
                if ((string)btnScan.Content == "Scan")
                {
                    PleaseSay($"Started seeking for {string.Join(", ", labelsOfObjectsToDetect)}");
                    SaveConfigurationData();

                    scanThread = new Thread(ScanThreadProc);
                    scanThread.Start(this);
                    btnScan.Content = "Stop";
                }
                else
                {
                    PleaseSay($"Stopping...");
                    cancellationTokenSource.Cancel();
                    scanThread.Join();
                    btnScan.Content = "Scan";
                    PleaseSay($"Stopped.");
                }
            });

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

        private static void UpdateRobotDataThreadProc(object obj)
        {
            var thisWindow = (MainWindow)obj;
            while (!thisWindow.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var robotData = thisWindow.robotCommunication.Read();
                    thisWindow.HandleRobotCommunicationResult(robotData);
                }
                catch (TaskCanceledException)
                {
                    thisWindow.PleaseSay("Update Robot Data cancelled.");
                }
            }
        }

        private static void ScanThreadProc(object obj)
        {
            var thisWindow = (MainWindow)obj;

            ImageRecognitionFromCameraParameters ip = new ImageRecognitionFromCameraParameters
            {
                OnnxFilePath = "TinyYolo2_model.onnx",
                LabelsOfObjectsToDetect = thisWindow.labelsOfObjectsToDetect,
                CameraId = 0, // Always the default camera...
            };

            using (var imageRecognitionFromCamera = ClassFactory.CreateImageRecognitionFromCamera(ip))
            {
                //await thisWindow.HandleImageRecognitionFromCameraResultAsync(
                //    await imageRecognitionFromCamera.GetAsync(), thisWindow.robotCommunication);

                int previousScanPower = thisWindow.ScanPower;
                while (!thisWindow.cancellationToken.IsCancellationRequested)
                {
                    var start = DateTime.Now;
                    try
                    {
                        thisWindow.SetMotors(thisWindow.robotCommunication, 0, 0, thisWindow.TimeToRun / 4);
                        var imageData = imageRecognitionFromCamera.Get();
                        var elapsed = (DateTime.Now - start).TotalMilliseconds;
                        thisWindow.Dispatcher.Invoke(() => thisWindow.ObjectData = elapsed.ToString());
                        thisWindow.HandleImageRecognitionFromCameraResult(imageData, thisWindow.robotCommunication);

                    }
                    catch (TaskCanceledException)
                    {
                        thisWindow.PleaseSay("Scanning cancelled.");
                    }
                }
            }
        }

        private void HandleImageRecognitionFromCameraResult(
            ImageRecognitionFromCameraResult imageData,
            IRobotCommunication robotCommunication)
        {
            if (imageData.HasData)
            {
                var objectPosition = imageData.XDeltaProportionFromBitmapCenter * 100;
                if (objectPosition < -5) // object is to the left
                {
                    Dispatcher.Invoke(() => RobotMode = "L");
                    SetMotors(robotCommunication, 0, ScanPower, TimeToRun);
                }
                else if (objectPosition > 5) // object is to the right
                {
                    Dispatcher.Invoke(() => RobotMode = "R");
                    SetMotors(robotCommunication, ScanPower, 0, TimeToRun);
                }
                else // object is straight ahead, CHARGE!
                {
                    Dispatcher.Invoke(() => RobotMode = "!");
                    PleaseSayButOnlyIfNotSaidInThePast30Seconds($"Ha! {imageData.Label}, I found you!");
                    using (var gr = Graphics.FromImage(imageData.Bitmap))
                    {
                        gr.DrawRectangle(new Pen(Color.Yellow, 10), 5, 5, imageData.Bitmap.Width - 5, imageData.Bitmap.Height - 5);
                    }

                    if (PleaseLurch)
                    {
                        PleaseSayButOnlyIfNotSaidInThePast30Seconds($"Charge!");
                        SetMotors(robotCommunication, LurchPower, LurchPower, TimeToRun * 20);
                    }
                }
            }
            else
            {
                Dispatcher.Invoke(() => RobotMode = "S");
                SetMotors(robotCommunication, 0, ScanPower, TimeToRun * 2);
                SetMotors(robotCommunication, 0, ScanPower * 3 / 2, TimeToRun);
            }

            Dispatcher.Invoke(() => objectDetectionImage.Source = Utilities.BitmapToBitmapImage(imageData.Bitmap));
        }

        private void SetMotors(IRobotCommunication robotCommunication, int l, int r, int timeMiliseconds = -1)
        {
            robotCommunication.SetMotors((int)(l * LMult), (int)(r * RMult), timeMiliseconds);
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

        private void HandleRobotCommunicationResult(RobotCommunicationResult robotData)
        {
            if (robotData.DataType == RobotCommunicationResult.NoData)
            {
                return;
            }

            if (robotData.Distance <= 10)
            {
                robotData.RobotCommunication.StopMotors();
            }

            Dispatcher.Invoke(() =>
            {
                AccelX   = robotData.AccelX;
                AccelY   = robotData.AccelY;
                AccelZ   = robotData.AccelZ;
                Distance = robotData.Distance;
                Voltage  = robotData.Voltage;
                Compass  = robotData.Compass;
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
            btnScan.IsEnabled = labels.Count > 0 && btnScan.Content.ToString() == "Scan";
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
            btnScan.IsEnabled = labels.Count > 0 && btnScan.Content.ToString() == "Start";
        }

        private async void runMotors_Click(object sender, RoutedEventArgs e)
        {
            await robotCommunication.SetMotorsAsync((int)(LPower * LMult), (int)(RPower * RMult), TimeToRun);
        }

        private void PleaseSay(string s) => new Thread(() => new SpeechSynthesizer().Speak(SayThis = s)).Start();
        private bool IsChecked(CheckBox checkBox) => checkBox.IsChecked.HasValue ? checkBox.IsChecked.Value : false;
        private List<string> GetPropertyNames(object o) => o.GetType().GetProperties().Select(p => p.Name).ToList();
        private void saveConfiguration_Click(object sender, RoutedEventArgs e) => SaveConfigurationData();
    }
}
