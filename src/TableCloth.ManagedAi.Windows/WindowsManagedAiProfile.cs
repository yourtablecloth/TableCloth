using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace TableCloth.ManagedAi.Windows;

public sealed class WindowsManagedAiProfile(ManagedAiPaths paths) : IManagedAiProfile
{
    public const string EmptySkillConfiguration = "# Managed by TableCloth. Changes are replaced before the next operation.\n";
    public const string Configuration = """
        forced_login_method = "chatgpt"
        cli_auth_credentials_store = "file"
        check_for_update_on_startup = false
        hide_agent_reasoning = true
        web_search = "live"
        feedback.enabled = false
        project_doc_max_bytes = 0
        [history]
        persistence = "none"
        [otel]
        exporter = "none"
        metrics_exporter = "none"
        trace_exporter = "none"
        log_user_prompt = false
        [features]
        shell_tool = false
        unified_exec = false
        shell_snapshot = false
        multi_agent = false
        hooks = false
        remote_plugin = false
        skill_mcp_dependency_install = false
        """;

    public void Prepare()
    {
        var drive = new DriveInfo(Path.GetPathRoot(paths.Root)!);
        if (drive.DriveType != DriveType.Fixed) throw new ManagedAiException(AiFailureCode.InvalidPath);
        SecureDirectory(paths.Root);
        SecureDirectory(paths.Profile);
        SecureDirectory(paths.Skills);
        SecureDirectory(paths.Logs);
        var marker = paths.Under(".tablecloth-managed-ai");
        if (!File.Exists(marker)) File.WriteAllText(marker, "1", new UTF8Encoding(false));
        var config = paths.Under("profiles", "openai-codex", "codex-home", "config.toml");
        // This profile is app-owned. Fail closed on edits that could enable tools or another authentication provider.
        if (File.Exists(config))
        {
            if (new FileInfo(config).Length > 8192 || File.ReadAllText(config) != Configuration)
                throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        }
        else File.WriteAllText(config, Configuration, new UTF8Encoding(false));
        // Skill policy is generated after native discovery. Reset the overlay first so hand edits never reach Codex.
        File.WriteAllText(paths.SkillConfiguration, EmptySkillConfiguration, new UTF8Encoding(false));
    }

    private static void SecureDirectory(string path)
    {
        ManagedAiPaths.RejectReparsePoints(path);
        var directory = Directory.CreateDirectory(path);
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User ?? throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        var security = new DirectorySecurity();
        security.SetOwner(sid);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        SecurityIdentifier[] principals = [sid, new(WellKnownSidType.LocalSystemSid, null),
            new(WellKnownSidType.BuiltinAdministratorsSid, null)];
        foreach (var principal in principals)
            security.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
    }
}
