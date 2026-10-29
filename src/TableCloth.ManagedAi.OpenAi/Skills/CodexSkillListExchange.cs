using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record CodexSkillMetadata(string Name, string Description, string Path, string Scope, bool Enabled);
public sealed record CodexSkillError(string Path, string Message);
public sealed record CodexSkillCatalog(IReadOnlyList<CodexSkillMetadata> Skills, IReadOnlyList<CodexSkillError> Errors);

// Initializes the transport and reads skill metadata only. It never starts a thread or model turn.
public sealed class CodexSkillListExchange
{
    public const string Initialize = "{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"tablecloth\",\"title\":\"TableCloth\",\"version\":\"0.1.0\"},\"capabilities\":{\"experimentalApi\":true}}}\n";
    private readonly List<CodexSkillMetadata> _skills = [];
    private readonly List<CodexSkillError> _errors = [];
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private int _expectedId = 1;
    private bool _complete;

    public ProcessInputReply? Accept(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (!root.TryGetProperty("id", out var id) || !id.TryGetInt32(out var number) || number != _expectedId) return null;
            if (_complete || root.TryGetProperty("error", out _)) throw Failed();
            if (_expectedId == 1)
            {
                _expectedId = 2;
                return new("{\"method\":\"initialized\",\"params\":{}}\n" +
                    "{\"id\":2,\"method\":\"skills/list\",\"params\":{\"cwds\":[],\"forceReload\":true}}\n");
            }

            foreach (var entry in root.GetProperty("result").GetProperty("data").EnumerateArray())
            {
                foreach (var skill in entry.GetProperty("skills").EnumerateArray())
                {
                    var name = RequiredText(skill, "name", 128);
                    var description = RequiredText(skill, "description", 4096, allowWhitespace: true);
                    var path = RequiredPath(skill, "path");
                    var scope = RequiredText(skill, "scope", 16);
                    if (scope is not ("user" or "repo" or "system" or "admin")) throw Failed();
                    if (_paths.Add(path))
                        _skills.Add(new(name, description, path, scope, skill.GetProperty("enabled").GetBoolean()));
                    if (_skills.Count > 1024) throw Failed();
                }
                foreach (var error in entry.GetProperty("errors").EnumerateArray())
                {
                    _errors.Add(new(RequiredPath(error, "path"), RequiredText(error, "message", 4096, allowWhitespace: true)));
                    if (_errors.Count > 1024) throw Failed();
                }
            }
            _complete = true;
            return new(Close: true);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ManagedAiException)
        { throw Failed(); }
    }

    public CodexSkillCatalog Complete(int exitCode)
        => exitCode == 0 && _complete ? new(_skills.AsReadOnly(), _errors.AsReadOnly()) : throw Failed();

    private static string RequiredText(JsonElement parent, string property, int maxLength, bool allowWhitespace = false)
    {
        var value = parent.GetProperty(property).GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength ||
            value.Any(c => char.IsControl(c) && (!allowWhitespace || c is not ('\r' or '\n' or '\t')))) throw Failed();
        return value;
    }

    private static string RequiredPath(JsonElement parent, string property)
    {
        var value = RequiredText(parent, property, 1024);
        if (!Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal)) throw Failed();
        return Path.GetFullPath(value);
    }

    private static ManagedAiException Failed() => new(AiFailureCode.SkillIsolationFailed);
}
