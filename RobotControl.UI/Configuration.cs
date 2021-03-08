using RobotControl.ClassLibrary;

using System.Collections.Generic;

namespace RobotControl.UI
{
    public class Configuration
    {
        public HashSet<string>
                        ObjectsToDetect      { get; set; } = new HashSet<string>(){ "person" };
        public float    LeftMotorMultiplier  { get; set; } = 1.0f;
        public float    RightMotorMultiplier { get; set; } = 1.0f;
        public int      SerialPortBaudrate   { get; set; } = 115200;
        public int      ScanPower            { get; set; } = 100;
        public int      LurchPower           { get; set; } = 100;
        public bool     EnableAudio          { get; set; } = true;
        public bool     ScanForObjects       { get; set; } = true;
        public bool     PleaseLurch          { get; set; } = true;
        public float CompassReadingNorth { get; internal set; } = 0;
        public float CompassReadingSouth { get; internal set; } = 180;
    }
}
