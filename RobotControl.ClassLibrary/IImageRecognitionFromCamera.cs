using System;
using System.Threading.Tasks;

namespace RobotControl.ClassLibrary
{
    public interface IImageRecognitionFromCamera : IDisposable
    {
        Task<ImageRecognitionFromCameraResult> GetAsync();
        ImageRecognitionFromCameraResult Get();
    }
}