namespace RobotControl.ClassLibrary
{
    public class ClassFactory
    {
        public static IRobotCommunication CreateRobotCommunication(RobotCommunicationParameters parameters) => new RobotCommunication(parameters);
        public static IImageRecognitionFromCamera CreateImageRecognitionFromCamera(ImageRecognitionFromCameraParameters parameters) => new ImageRecognitionFromCamera(parameters);
    }
}
