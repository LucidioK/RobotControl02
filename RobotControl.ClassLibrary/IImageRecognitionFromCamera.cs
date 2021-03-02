using System.Threading.Tasks;

namespace RobotControl.ClassLibrary
{
    public interface IImageRecognitionFromCamera
    {
        Task<ImageRecognitionFromCameraResult> GetAsync();
        Task StartAsync();
    }
}