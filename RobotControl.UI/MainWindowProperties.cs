using Newtonsoft.Json;

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace RobotControl.UI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private float  accelX;
        private float  accelY;
        private float  accelZ;
        private string cameraIp = "10.0.0.67";
        private float  compass;
        private float  compassHeading;
        private string compassPointingTo;
        private Configuration configuration;
        private int    currentL;
        private int    currentR;
        private float  distance;
        private float  lMult;
        private int    lPower;
        private int    lurchPower;
        private bool   pleaseLurch;
        private float  rMult;
        private int    rPower;
        private int    scanPower;
        private int    timeToRun;
        private float  voltage;

        public Configuration Configuration
        {
            get
            {
                if (configuration == null)
                {
                    if (File.Exists(this.configurationPath))
                    {
                        string config = File.ReadAllText(this.configurationPath);
                        this.configuration = JsonConvert.DeserializeObject<Configuration>(config);
                        var configurationProperties = new HashSet<string>(GetPropertyNames(configuration));
                        GetPropertyNames(this)
                            .Where(pn => configurationProperties.Contains(pn)).ToList()
                            .ForEach(pn => this.GetType().GetProperty(pn)?.SetValue(this, Configuration.GetType().GetProperty(pn).GetValue(configuration)));
                        this.GetType().GetProperties().Select(p => p.Name).ToList().ForEach(pn => NotifyPropertyChanged(pn));
                    }
                    else
                    {
                        this.configuration = new Configuration();
                    }
                }

                return configuration;
            }
            set => configuration = value;
        }

        public float  AccelX            { get => accelX;            set => SetAndNotify(ref accelX,            value, nameof(AccelX));            }
        public float  AccelY            { get => accelY;            set => SetAndNotify(ref accelY,            value, nameof(AccelY));            }
        public float  AccelZ            { get => accelZ;            set => SetAndNotify(ref accelZ,            value, nameof(AccelZ));            }
        public string CameraIp          { get => cameraIp;          set => SetAndNotify(ref cameraIp,          value, nameof(CameraIp));          }
        public float  Compass           { get => compass;           set => SetAndNotify(ref compass,           value, nameof(Compass));           }
        public float  CompassHeading    { get => compassHeading;    set => SetAndNotify(ref compassHeading,    value, nameof(CompassHeading));    }
        public string CompassPointingTo { get => compassPointingTo; set => SetAndNotify(ref compassPointingTo, value, nameof(CompassPointingTo)); }
        public int    CurrentL          { get => currentL;          set => SetAndNotify(ref currentL,          value, nameof(CurrentL));          }
        public int    CurrentR          { get => currentR;          set => SetAndNotify(ref currentR,          value, nameof(CurrentR));          }
        public float  Distance          { get => distance;          set => SetAndNotify(ref distance,          value, nameof(Distance));          }
        public float  LMult             { get => lMult;             set => SetAndNotify(ref lMult,             value, nameof(LMult));             }
        public int    LPower            { get => lPower;            set => SetAndNotify(ref lPower,            value, nameof(LPower));            }
        public int    LurchPower        { get => lurchPower;        set => SetAndNotify(ref lurchPower,        value, nameof(LurchPower));        }
        public bool   PleaseLurch       { get => pleaseLurch;       set => SetAndNotify(ref pleaseLurch,       value, nameof(PleaseLurch));       }
        public float  RMult             { get => rMult;             set => SetAndNotify(ref rMult,             value, nameof(RMult));             }
        public int    RPower            { get => rPower;            set => SetAndNotify(ref rPower,            value, nameof(RPower));            }
        public int    ScanPower         { get => scanPower;         set => SetAndNotify(ref scanPower,         value, nameof(ScanPower));         }
        public int    TimeToRun         { get => timeToRun;         set => SetAndNotify(ref timeToRun,         value, nameof(TimeToRun));         }
        public float  Voltage           { get => voltage;           set => SetAndNotify(ref voltage,           value, nameof(Voltage));           }

        private void SetAndNotify(ref float field, float value, string propertyName)
        {
            field = value;
            NotifyPropertyChanged(propertyName);
        }

        private void SetAndNotify(ref int field, int value, string propertyName)
        {
            field = value;
            NotifyPropertyChanged(propertyName);
        }

        private void SetAndNotify(ref string field, string value, string propertyName)
        {
            field = value;
            NotifyPropertyChanged(propertyName);
        }

        private void SetAndNotify(ref bool field, bool value, string propertyName)
        {
            field = value;
            NotifyPropertyChanged(propertyName);
        }
    }
}
