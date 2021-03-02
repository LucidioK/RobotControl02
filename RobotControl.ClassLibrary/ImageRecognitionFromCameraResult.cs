using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace RobotControl.ClassLibrary
{
    public class ImageRecognitionFromCameraResult
    {
        public bool HasData { get; set; } = false;
        public Bitmap Bitmap { get; set; } = new Bitmap(1, 1);
        public float XDeltaProportionFromBitmapCenter { get; set; }
    }
}
