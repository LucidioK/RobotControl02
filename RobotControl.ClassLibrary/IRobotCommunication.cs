using System.Threading.Tasks;

namespace RobotControl.ClassLibrary
{
    public interface IRobotCommunication
    {
        Task<RobotCommunicationResult> ReadAsync();
        Task SetMotorsAsync(int l, int r, int timeMiliseconds = -1);
        Task StartAsync();
        Task StopMotorsAsync();
    }
}