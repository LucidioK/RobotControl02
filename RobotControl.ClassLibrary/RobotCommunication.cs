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
        private readonly RobotCommunicationParameters parameters;
        private SerialPort serialPort = null;
        private int arduinoSerialPortNumber = -1;

        public RobotCommunication(RobotCommunicationParameters parameters)
        {
            this.parameters = parameters;
        }

        public async Task StartAsync() => await Task.Run(() =>
        {
            bool foundPort = false;
            for (int i = 0; i < 4 && !foundPort; i++)
            {
                for (int j = 1; j < 32 && !foundPort; j++)
                {
                    if (OpenPort(j))
                    {

                        System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.StartAsync Opened port {j} <--");
                        arduinoSerialPortNumber = j;
                        return;
                    }
                }

                Thread.Sleep(100);
            }

            throw new Exception($"Cannot find SmartRobot02 COM port. Check if the robot is connected to a USB port. Check if Arduino IDE or other app is using the port. Aborting.");
        });

        private bool OpenPort(int portNumber)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }

            serialPort = new SerialPort($"COM{portNumber}", parameters.BaudRate);
            // this seems to be important for Arduino:
            serialPort.RtsEnable = true;
            serialPort.ReadTimeout = 200;
            bool shouldContinueTryingToOpen = true;
            for (var k = 0; !serialPort.IsOpen && k < 8 && shouldContinueTryingToOpen; k++)
            {
                try
                {
                    serialPort.Open();
                    serialPort.ReadExisting();
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

        private void ReopenPort()
        {
            if (arduinoSerialPortNumber >= 0)
            {
                OpenPort(arduinoSerialPortNumber);
            }
            else
            {
                throw new Exception("Trying to reopen port without first setting arduinoSerialPortNumber");
            }
        }

        public async Task<RobotCommunicationResult> ReadAsync() => await Task.Run(async () =>
        {
            System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync trying to read port {arduinoSerialPortNumber}.");
            for (int i = 0; i < 8; i++)
            {
                serialPort.ReadExisting();
                await WriteAsync("{'operation':'readsensors'}");
                string json = string.Empty;
                try
                {
                    json = serialPort.ReadLine();
                    System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync read {json}");
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine("-->RobotCommunication.ReadAsync TIMEOUT, will reopen port.");
                    ReopenPort();
                    Thread.Sleep(10);
                    continue;
                }

                try
                {
                    var result = JsonConvert.DeserializeObject<RobotCommunicationResult>(json);
                    // To compensate that the sensor is upside down...
                    result.AccelX *= -1;
                    result.AccelY *= -1;
                    result.AccelZ *= -1;
                    result.RobotCommunication = this;
                    return result;
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync bad json, will try again: {json}");
                    Thread.Sleep(10);
                }
            }

            System.Diagnostics.Debug.WriteLine($"-->RobotCommunication.ReadAsync could not get data, will return empty RobotCommunicationResult");

            return new RobotCommunicationResult();
        });

        public async Task SetMotorsAsync(int l, int r, int timeMiliseconds = -1) => await Task.Run(async () =>
        {
            if (timeMiliseconds >= 0)
            {
                await WriteAsync($"{{'operation':'timedmotor','l':{l},'r':{r},'t':{timeMiliseconds}}}");
            }
            else
            {
                await WriteAsync($"{{'operation':'motor','l':{l},'r':{r}}}");
            }
        });

        public async Task WriteAsync(string s) => await Task.Run(() =>
        {
            serialPort.WriteLine(s);
        });

        private int AbsolutePower(int pwr) => pwr == 0 ? 0 : (pwr > 0 ? 254 : -254);
        public async Task StopMotorsAsync() => await Task.Run(async () =>
        {
            await WriteAsync($"{{'operation':'stop'}}");
        });

        public void Dispose()
        {
            serialPort?.Close();
        }
    }
}
