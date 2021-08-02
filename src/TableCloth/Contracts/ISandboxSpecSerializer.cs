using TableCloth.Implementations.WindowsSandbox;

namespace TableCloth.Contracts
{
    public interface ISandboxSpecSerializer
    {
        string SerializeSandboxSpec(SandboxConfiguration configuration);
    }
}
