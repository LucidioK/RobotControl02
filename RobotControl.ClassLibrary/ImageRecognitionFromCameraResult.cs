using Newtonsoft.Json;

using System.Drawing;

namespace RobotControl.ClassLibrary
{
    public class ImageRecognitionFromCameraResult
    {
        public bool HasData { get; set; } = false;
        public Bitmap Bitmap { get; set; } = new Bitmap(1, 1);
        public float XDeltaProportionFromBitmapCenter { get; set; }
        public string Label { get; set; }

        [JsonIgnore]
        public IImageRecognitionFromCamera ImageRecognitionFromCamera { get; set; }
    }
}
