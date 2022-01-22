using Newtonsoft.Json;

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace RobotControl.ClassLibrary
{
    internal class RobotCommunication : IRobotCommunication
    {
        private const int ReadTimeOut = 200;
        private readonly RobotCommunicationParameters parameters;
        private SerialPort serialPort = null;

        private int LFromRobot = -1;
        private int RFromRobot = -1;
        private string latestStringFromSerial = "";
        private object serialLock = new object();

        public RobotCommunication(RobotCommunicationParameters parameters)
        {
            this.parameters = parameters;
        }

        public void Start()
        {
            bool foundPort = false;
            for (int i = 0; i < 4 && !foundPort; i++)
            {
                for (int j = 1; j < 32 && !foundPort; j++)
                {
                    if (OpenPort(j))
                    {
                        System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.StartAsync Opened port {j} <--");
                        return;
                    }
                }

                Thread.Sleep(100);
            }

            throw new Exception($"Cannot find SmartRobot02 COM port. Check if the robot is connected to a USB port. Check if Arduino IDE or other app is using the port. Aborting.");
        }

        public RobotCommunicationResult Read()
        {
            string json = string.Empty;
            lock (serialLock)
            {
                json = latestStringFromSerial;
                latestStringFromSerial = string.Empty;
            }

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<RobotCommunicationResult>(json);
                    // To compensate that the sensor is upside down...
                    result.AccelX *= -1;
                    result.AccelY *= -1;
                    result.AccelZ *= -1;
                    result.RobotCommunication = this;
                    LFromRobot = (int)result.L;
                    RFromRobot = (int)result.R;
                    return result;
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync bad json, will try again: {json}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync could not get data, will return empty RobotCommunicationResult");

            return new RobotCommunicationResult();
        }

        public void SetMotors(int l, int r, int timeMiliseconds = -1)
        {
            if (LFromRobot == l && RFromRobot == r)
            {
                return;
            }

            if (timeMiliseconds >= 0)
            {
                Write($"{{'operation':'timedmotor','l':{l},'r':{r},'t':{timeMiliseconds}}}");
            }
            else
            {
                Write($"{{'operation':'motor','l':{l},'r':{r}}}");
            }
        }


        public void Write(string s) => serialPort.WriteLine(s);

        public void StopMotors() => Write($"{{'operation':'stop'}}");

        public async Task<RobotCommunicationResult> ReadAsync() => await Task.Run(() => Read());
        public async Task SetMotorsAsync(int l, int r, int timeMiliseconds = -1) => await Task.Run(() => SetMotors(l, r, timeMiliseconds));
        public async Task StopMotorsAsync() => await Task.Run(() => StopMotors());
        public async Task StartAsync() => await Task.Run(() => Start());
        public async Task WriteAsync(string s) => await Task.Run(() => Write(s));


        public void Dispose()
        {
            serialPort?.Close();
        }

        private bool OpenPort(int portNumber)
        {
            ClosePortIfNeeded();

            serialPort = new SerialPort($"COM{portNumber}", parameters.BaudRate);
            // this seems to be important for Arduino:
            serialPort.RtsEnable = true;
            serialPort.ReadTimeout = ReadTimeOut;
            bool shouldContinueTryingToOpen = true;
            for (var k = 0; !serialPort.IsOpen && k < 8 && shouldContinueTryingToOpen; k++)
            {
                try
                {
                    serialPort.Close();
                    serialPort.Open();
                    serialPort.ReadExisting();
                    serialPort.WriteLine("{'operation':'id'}");
                    Thread.Sleep(ReadTimeOut / 2);
                    if (!ReadLineIfPossible().StartsWith("SmartRobot02"))
                    {
                        ClosePortIfNeeded();
                        shouldContinueTryingToOpen = false;
                        continue;
                    }

                    serialPort.DataReceived += OnSerialDataReceived;
                    serialPort.WriteLine("{'operation':'constantreadsensors'}");
                    shouldContinueTryingToOpen = false;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine("-->RobotCommunication.TryToOpenPort UnauthorizedAccessException, retrying");
                    Thread.Sleep(100);
                }
                catch (FileNotFoundException)
                {
                    System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.TryToOpenPort COM{portNumber} does not exist, next!");
                    shouldContinueTryingToOpen = false;
                }
            }

            return serialPort.IsOpen;
        }

        private void ClosePortIfNeeded()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }

        private string ReadLineIfPossible()
        {
            try
            {
                return serialPort.ReadLine();
            }
            catch (TimeoutException) { }
            catch (IOException) { }

            return string.Empty;
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (serialLock)
            {
                latestStringFromSerial = ReadLineIfPossible();
            }
        }

    }
}
