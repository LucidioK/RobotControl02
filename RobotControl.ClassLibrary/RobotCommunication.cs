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
                    serialPort = new SerialPort($"COM{j}", parameters.BaudRate);
                    // this seems to be important for Arduino:
                    serialPort.RtsEnable = true;
                    serialPort.ReadTimeout = 200;
                    bool shouldContinueTryingToOpen = true;
                    for (var k = 0; !serialPort.IsOpen && k < 8 && shouldContinueTryingToOpen; k++)
                    {
                        shouldContinueTryingToOpen = TryToOpenPort(j);
                    }

                    if (serialPort.IsOpen)
                    {
                        return;
                    }
                }

                Thread.Sleep(100);
            }

            throw new Exception($"Cannot find SmartRobot02 COM port. Aborting.");
        });

        private bool TryToOpenPort(int j)
        {
            bool shouldContinueTryingToOpen = true;
            try
            {
                serialPort.Open();
                serialPort.ReadExisting();
                shouldContinueTryingToOpen = false;
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine("-->SerialPortImpl.GetSerialPort UnauthorizedAccessException, retrying");
                Thread.Sleep(100);
            }
            catch (FileNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine($"-->SerialPortImpl.GetSerialPort COM{j} does not exist, next!");
                shouldContinueTryingToOpen = false;
            }

            return shouldContinueTryingToOpen;
        }

        public async Task<RobotCommunicationResult> ReadAsync() => await Task.Run(() =>
        {
            for (int i = 0; i < 8; i++)
            {
                serialPort.ReadExisting();
                serialPort.WriteLine("{'operation':'readsensors'}");
                string json = string.Empty;
                try
                {
                    json = serialPort.ReadLine();
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine("-->SerialPortImpl.SerialReadThread TIMEOUT");
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
                    System.Diagnostics.Debug.WriteLine($"-->SerialPortImpl.ReadAsync bad json, will try again: {json}");
                    Thread.Sleep(10);
                }
            }

            System.Diagnostics.Debug.WriteLine($"-->SerialPortImpl.ReadAsync could not get data, will return empty RobotCommunicationResult");

            return new RobotCommunicationResult();
        });

        public async Task SetMotorsAsync(int l, int r, int timeMiliseconds = -1) => await Task.Run(() =>
        {
            if (timeMiliseconds >= 0)
            {
                serialPort.Write($"{{'operation':'timedmotor','l':{l},'r':{r},'t':{timeMiliseconds}}}");
            }
            else
            {
                serialPort.Write($"{{'operation':'motor','l':{l},'r':{r}}}");
            }
        });

        public async Task StopMotorsAsync() => await Task.Run(() =>
        {
            serialPort.Write($"{{'operation':'stop'}}");
        });

        public void Dispose()
        {
            serialPort?.Close();
        }
    }
}
