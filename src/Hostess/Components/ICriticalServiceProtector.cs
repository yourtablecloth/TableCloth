using System.ServiceProcess;

namespace Hostess.Components
{
    public interface ICriticalServiceProtector
    {
        int GetServiceProcessId(ServiceController sc);
        void PreventServiceProcessTermination(string service);
        void PreventServiceStop(string service, string username);
    }
}