using Newtonsoft.Json;

namespace RobotControl.ClassLibrary
{
    public class RobotCommunicationResult
    {
        [JsonProperty("dataType")]
        public string DataType { get; set; } = "NODATA";

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("l")]
        public float L { get; set; }

        [JsonProperty("r")]
        public float R { get; set; }

        [JsonProperty("accelX")]
        public float AccelX { get; set; }

        [JsonProperty("accelY")]
        public float AccelY { get; set; }

        [JsonProperty("accelZ")]
        public float AccelZ { get; set; }

        [JsonProperty("compass")]
        public float Compass { get; set; }

        [JsonProperty("distance")]
        public float Distance { get; set; }

        [JsonProperty("voltage")]
        public float Voltage { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}