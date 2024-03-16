using System.ServiceProcess;

namespace Spork.Components
{
    public interface ICriticalServiceProtector
    {
        int GetServiceProcessId(ServiceController sc);
        void PreventServiceProcessTermination(string service);
        void PreventServiceStop(string service, string username);
    }
}