using TableCloth.AppContainerPoc;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
try
{
    if (args is ["prepare", var workspace]) return await Preparation.RunAsync(workspace, stop.Token);
    if (args is ["mcp", var manifest]) return await SkillMcpBroker.RunAsync(manifest, stop.Token);
    if (args is ["selftest", var root]) return await Evaluation.RunAsync(root, includePublicApi: false, stop.Token);
    if (args is ["evaluate-public-api", var root2]) return await Evaluation.RunAsync(root2, includePublicApi: true, stop.Token);
    if (args is ["codex-probe", var root3]) return await CodexProbe.RunAsync(root3, stop.Token);
    if (args is ["recover-legacy-acl", var root4])
    {
        Console.WriteLine(AppContainerProcessRunner.RecoverLegacyPoCGrants(root4));
        return 0;
    }
    if (args is ["recover-acl", var root5])
    {
        var failure = AppContainerProcessRunner.RecoverPendingAclJournals(root5);
        Console.WriteLine(failure ?? "ACL journals recovered.");
        return failure is null ? 0 : 1;
    }
    Console.Error.WriteLine("Commands: prepare <workspace>, mcp <manifest>, selftest <owned-root>, evaluate-public-api <owned-root>, codex-probe <owned-root>");
    return args.Length == 0 || args[0] == "--help" ? 0 : 2;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Canceled"); return 130; }
catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }
