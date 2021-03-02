namespace RobotControl.ClassLibrary
{
    public class ImageRecognitionFromCameraParameters
    {
        public string   OnnxFilePath            { get; set; }
        public string[] LabelsOfObjectsToDetect { get; set; }
        public int      CameraId                { get; set; } = 0;
    }
}
