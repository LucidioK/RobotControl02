using System;
using System.Threading;
using System.IO.Ports;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;

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
                    serialPort.ReadTimeout = 5000;
                    bool shouldContinueTryingToOpen = true;
                    for (var k = 0; !serialPort.IsOpen && k < 8 && shouldContinueTryingToOpen; k++)
                    {
                        try
                        {
                            serialPort.Open();
                            serialPort.ReadExisting();
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

        public async Task<RobotCommunicationResult> ReadAsync() => await Task.Run(() =>
        {
            for (int i = 0; i < 8; i++)
            {
                serialPort.ReadExisting();
                serialPort.WriteLine("{'operation':'readsensors'}");
                var json = serialPort.ReadLine();
                try
                {
                    return JsonConvert.DeserializeObject<RobotCommunicationResult>(json);
                }
                catch(JsonReaderException)
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
    }
}
