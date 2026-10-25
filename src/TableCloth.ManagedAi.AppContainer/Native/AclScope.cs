using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

internal sealed class AclScope(OwnedPaths paths, nint packageSid, string profileName, CancellationToken cancellationToken,
    string? journalPath = null)
{
    private sealed record Snapshot(string Path, string Sddl, Win32.FileInformation Identity)
    {
        internal bool DaclChanged, LabelChanged, MayChangeLabel;
    }
    private readonly List<Snapshot> _snapshots = [];
    private readonly HashSet<string> _captured = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _journal = journalPath ?? paths.Check(Path.Combine(paths.Root, ".acl-journal-" + Guid.NewGuid().ToString("N") + ".json"));
    private bool _journalDirty;

    internal void GrantRootTraversal() => GrantOne(paths.Root, 0x100020, writable: false, inherit: false);

    internal void GrantDirectory(string directory, bool writable)
    {
        directory = paths.Check(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException();
        var entries = new List<string> { directory };
        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];
            paths.Identity(entry);
            if (Directory.Exists(entry)) entries.AddRange(Directory.EnumerateFileSystemEntries(entry));
            if (entries.Count > 20000) throw new IOException("The grant exceeds the owned-entry budget.");
        }
        // Capture every original descriptor before an inheritable grant can propagate to children.
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(entry);
            if (writable) _snapshots.Single(x => x.Path == entry).MayChangeLabel = true;
        }
        _journalDirty = true;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GrantOne(entry, writable ? Win32.Modify : Win32.ReadExecute, writable, inherit: writable && Directory.Exists(entry));
        }
    }

    private void Capture(string path)
    {
        if (!_captured.Add(path)) return;
        var identity = paths.Identity(path);
        uint status = Win32.GetNamedSecurityInfo(path, 1, Win32.SecurityInformation,
            out _, out _, out _, out _, out var descriptor);
        CheckAt(status, "GetNamedSecurityInfo:DACL+LABEL", path);
        try
        {
            Win32.Check(Win32.DescriptorToString(descriptor, 1, Win32.SecurityInformation, out var text, out _));
            try { _snapshots.Add(new(path, Marshal.PtrToStringUni(text)!, identity)); _journalDirty = true; }
            finally { Win32.LocalFree(text); }
        }
        finally { Win32.LocalFree(descriptor); }
    }

    private void GrantOne(string path, uint access, bool writable, bool inherit)
    {
        Capture(path);
        SaveJournal();
        var snapshot = _snapshots.Single(x => x.Path == path);
        CheckAt(Win32.GetNamedSecurityInfo(path, 1, Win32.DaclInformation | 1,
            out var owner, out _, out var oldDacl, out _, out var descriptor), "GetNamedSecurityInfo:DACL+OWNER", path);
        try
        {
            var entry = new Win32.ExplicitAccess
            {
                Permissions = access, Mode = 1, Inheritance = inherit ? 3u : 0,
                Trustee = new() { Form = 0, Type = 1, Name = packageSid }
            };
            Win32.CheckError(Win32.SetEntriesInAcl(1, ref entry, oldDacl, out var dacl));
            try
            {
                snapshot.DaclChanged = true;
                CheckAt(Win32.SetNamedSecurityInfo(path, 1, Win32.DaclInformation, 0, 0, dacl, 0), "SetNamedSecurityInfo:DACL", path);
            }
            finally { Win32.LocalFree(dacl); }
            if (writable)
            {
                // Changing the mandatory label requires WRITE_OWNER. Grant only the existing owner,
                // only on these explicitly writable owned targets, and restore the original descriptor.
                var ownerEntry = new Win32.ExplicitAccess
                {
                    Permissions = 0x80000, Mode = 1,
                    Trustee = new() { Form = 0, Type = 1, Name = owner }
                };
                CheckAt(Win32.GetNamedSecurityInfo(path, 1, Win32.DaclInformation,
                    out _, out _, out var currentDacl, out _, out var currentDescriptor), "GetNamedSecurityInfo:owner-grant", path);
                try
                {
                    Win32.CheckError(Win32.SetEntriesInAcl(1, ref ownerEntry, currentDacl, out var withOwner));
                    try { CheckAt(Win32.SetNamedSecurityInfo(path, 1, Win32.DaclInformation, 0, 0, withOwner, 0), "SetNamedSecurityInfo:owner-WRITE_OWNER", path); }
                    finally { Win32.LocalFree(withOwner); }
                }
                finally { Win32.LocalFree(currentDescriptor); }
            }
        }
        finally { Win32.LocalFree(descriptor); }
        if (!writable) return;
        Win32.Check(Win32.StringToDescriptor(inherit ? "S:(ML;OICI;NW;;;LW)" : "S:(ML;;NW;;;LW)", 1, out var label, out _));
        try
        {
            Win32.Check(Win32.GetSecurityDescriptorSacl(label, out _, out var sacl, out _));
            CheckAt(Win32.SetNamedSecurityInfo(path, 1, Win32.LabelInformation, 0, 0, 0, sacl), "SetNamedSecurityInfo:low-label", path);
            snapshot.LabelChanged = true;
        }
        finally { Win32.LocalFree(label); }
    }

    internal string? Restore()
    {
        string? failure = null;
        // Restore ancestors first so their propagation cannot overwrite the final child descriptors.
        foreach (var snapshot in _snapshots.OrderBy(x => x.Path.Length))
        {
            try
            {
                if (!File.Exists(snapshot.Path) && !Directory.Exists(snapshot.Path)) continue;
                var identity = paths.Identity(snapshot.Path);
                if (identity.VolumeSerial != snapshot.Identity.VolumeSerial || identity.IndexHigh != snapshot.Identity.IndexHigh ||
                    identity.IndexLow != snapshot.Identity.IndexLow)
                    throw new IOException("An ACL target changed identity during execution.");
                Win32.Check(Win32.StringToDescriptor(snapshot.Sddl, 1, out var descriptor, out _));
                try
                {
                    Win32.Check(Win32.GetSecurityDescriptorDacl(descriptor, out _, out var dacl, out _));
                    Win32.Check(Win32.GetSecurityDescriptorSacl(descriptor, out _, out var sacl, out _));
                    Win32.Check(Win32.GetSecurityDescriptorControl(descriptor, out var control, out _));
                    // Restore a changed label while the temporary owner WRITE_OWNER grant still exists.
                    if (snapshot.LabelChanged)
                    {
                        EnsureOwnerLabelRight(snapshot.Path);
                        snapshot.DaclChanged = true;
                        var error = RestoreLabel(snapshot.Path, sacl);
                        if (error != 0) failure ??= "acl-restore:label:" + error + ":" + Relative(snapshot.Path);
                    }
                    if (snapshot.DaclChanged)
                    {
                        var protection = (control & 0x1000) != 0 ? 0x80000000u : 0x20000000u;
                        CheckAt(Win32.SetNamedSecurityInfo(snapshot.Path, 1, Win32.DaclInformation | protection,
                            0, 0, dacl, 0), "restore-DACL", snapshot.Path);
                    }
                }
                finally { Win32.LocalFree(descriptor); }
                CheckAt(Win32.GetNamedSecurityInfo(snapshot.Path, 1, Win32.SecurityInformation,
                    out _, out _, out _, out _, out var restored), "verify-restoration:GetNamedSecurityInfo", snapshot.Path);
                try
                {
                    Win32.Check(Win32.DescriptorToString(restored, 1, Win32.SecurityInformation, out var value, out _));
                    try
                    {
                        if (!string.Equals(SecurityFingerprint(snapshot.Sddl),
                            SecurityFingerprint(Marshal.PtrToStringUni(value)!), StringComparison.Ordinal))
                            failure ??= "acl-restore:descriptor-mismatch:" + Relative(snapshot.Path);
                    }
                    finally { Win32.LocalFree(value); }
                }
                finally { Win32.LocalFree(restored); }
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or ArgumentException)
            {
                failure ??= "acl-restore:" + Describe(error);
            }
        }
        return failure;
    }

    private static unsafe uint RestoreLabel(string path, nint originalSacl)
    {
        // NULL means no label update to this API. A present empty ACL removes an
        // explicit label when the original descriptor contained no mandatory ACE.
        var empty = stackalloc byte[8];
        new Span<byte>(empty, 8).Clear();
        empty[0] = 2; // ACL_REVISION
        empty[2] = 8; // sizeof(ACL), little endian
        return Win32.SetNamedSecurityInfo(path, 1, Win32.LabelInformation, 0, 0, 0,
            originalSacl == 0 ? (nint)empty : originalSacl);
    }

    private static string SecurityFingerprint(string sddl)
    {
        var descriptor = new RawSecurityDescriptor(sddl);
        return descriptor.GetSddlForm(AccessControlSections.Access) + "|ML:" + MandatoryLabels(descriptor);
    }

    private static string MandatoryLabels(RawSecurityDescriptor descriptor)
    {
        var labels = new List<string>();
        if (descriptor.SystemAcl is { } sacl)
            foreach (GenericAce ace in sacl)
            {
                if ((byte)ace.AceType != 17) continue;
                var bytes = new byte[ace.BinaryLength];
                ace.GetBinaryForm(bytes, 0);
                labels.Add(Convert.ToHexString(bytes));
            }
        return string.Join(';', labels);
    }

    private void EnsureOwnerLabelRight(string path)
    {
        CheckAt(Win32.GetNamedSecurityInfo(path, 1, Win32.DaclInformation | 1,
            out var owner, out _, out var dacl, out _, out var descriptor), "restore-owner:GetNamedSecurityInfo", path);
        try
        {
            var entry = new Win32.ExplicitAccess
            {
                Permissions = 0x80000, Mode = 1,
                Trustee = new() { Form = 0, Type = 1, Name = owner }
            };
            Win32.CheckError(Win32.SetEntriesInAcl(1, ref entry, dacl, out var restored));
            try { CheckAt(Win32.SetNamedSecurityInfo(path, 1, Win32.DaclInformation, 0, 0, restored, 0), "restore-owner:WRITE_OWNER", path); }
            finally { Win32.LocalFree(restored); }
        }
        finally { Win32.LocalFree(descriptor); }
    }

    private string Relative(string path) => Path.GetRelativePath(paths.Root, path);

    private void CheckAt(uint status, string operation, string path)
    {
        if (status == 0) return;
        var error = new System.ComponentModel.Win32Exception((int)status);
        error.Data["PocOperation"] = operation + ":" + Relative(path);
        throw error;
    }

    internal static string Describe(Exception error) =>
        (error.Data["PocOperation"] is string operation ? operation + ":" : "") +
        (error is System.ComponentModel.Win32Exception win32 ? "win32-" + win32.NativeErrorCode : error.GetType().Name);

    private void SaveJournal()
    {
        if (!_journalDirty) return;
        var temporary = _journal + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var writer = new Utf8JsonWriter(file))
            {
                writer.WriteStartObject(); writer.WriteNumber("version", 1);
                writer.WriteString("root", paths.Root); writer.WriteString("profile", profileName);
                writer.WriteStartArray("entries");
                foreach (var snapshot in _snapshots)
                {
                    writer.WriteStartObject(); writer.WriteString("path", snapshot.Path); writer.WriteString("sddl", snapshot.Sddl);
                    writer.WriteNumber("volume", snapshot.Identity.VolumeSerial);
                    writer.WriteNumber("indexHigh", snapshot.Identity.IndexHigh); writer.WriteNumber("indexLow", snapshot.Identity.IndexLow);
                    writer.WriteBoolean("labelMayChange", snapshot.MayChangeLabel); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
            }
            file.Flush(flushToDisk: true);
        }
        File.Move(temporary, _journal, overwrite: true);
        _journalDirty = false;
    }

    internal void Complete()
    {
        if (File.Exists(_journal)) File.Delete(_journal);
        if (File.Exists(_journal + ".tmp")) File.Delete(_journal + ".tmp");
    }

    internal static int RecoverLegacyPoCGrants(OwnedPaths paths)
    {
        static string ReadDacl(string path)
        {
            Win32.CheckError(Win32.GetNamedSecurityInfo(path, 1, Win32.DaclInformation, out _, out _, out _, out _, out var descriptor));
            try
            {
                Win32.Check(Win32.DescriptorToString(descriptor, 1, Win32.DaclInformation, out var value, out _));
                try { return Marshal.PtrToStringUni(value)!; }
                finally { Win32.LocalFree(value); }
            }
            finally { Win32.LocalFree(descriptor); }
        }
        var rootDescriptor = new RawSecurityDescriptor(ReadDacl(paths.Root));
        var sids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ace in rootDescriptor.DiscretionaryAcl!)
            if (ace is CommonAce common && common.AceQualifier == AceQualifier.AccessAllowed &&
                common.AccessMask == 0x100020 && common.AceFlags == AceFlags.None &&
                common.SecurityIdentifier.Value.StartsWith("S-1-15-2-", StringComparison.Ordinal) &&
                common.SecurityIdentifier.Value.Split('-').Length == 11)
                sids.Add(common.SecurityIdentifier.Value);
        if (sids.Count == 0) return 0;
        var entries = new List<string> { paths.Root };
        for (var index = 0; index < entries.Count; index++)
        {
            paths.Identity(entries[index]);
            if (Directory.Exists(entries[index])) entries.AddRange(Directory.EnumerateFileSystemEntries(entries[index]));
            if (entries.Count > 25000) throw new IOException("Recovery exceeds the owned-entry budget.");
        }
        var changes = new List<(string Path, string Sddl)>();
        var removed = 0;
        foreach (var path in entries)
        {
            var descriptor = new RawSecurityDescriptor(ReadDacl(path));
            var dacl = descriptor.DiscretionaryAcl!;
            var changed = false;
            for (var index = dacl.Count - 1; index >= 0; index--)
                if (dacl[index] is CommonAce common && common.AceQualifier == AceQualifier.AccessAllowed &&
                    sids.Contains(common.SecurityIdentifier.Value) && common.AccessMask is 0x100020 or 0x1200A9 or 0x1301BF)
                { dacl.RemoveAce(index); removed++; changed = true; }
            if (changed) changes.Add((path, descriptor.GetSddlForm(AccessControlSections.Access)));
        }
        // Descendants first: retain the exact SID set at the root until their ACEs are removed.
        foreach (var (path, sddl) in changes.OrderByDescending(x => x.Path.Length))
        {
            paths.Identity(path);
            Win32.Check(Win32.StringToDescriptor(sddl, 1, out var descriptor, out _));
            try
            {
                Win32.Check(Win32.GetSecurityDescriptorDacl(descriptor, out _, out var dacl, out _));
                Win32.CheckError(Win32.SetNamedSecurityInfo(path, 1, Win32.DaclInformation, 0, 0, dacl, 0));
            }
            finally { Win32.LocalFree(descriptor); }
        }
        return removed;
    }

    internal static string? RecoverPendingJournals(OwnedPaths paths)
    {
        foreach (var journal in Directory.EnumerateFiles(paths.Root, ".acl-journal-*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                paths.Identity(journal);
                if (new FileInfo(journal).Length > 67108864) throw new IOException("The ACL journal exceeds its budget.");
                using var document = JsonDocument.Parse(File.ReadAllBytes(journal), new JsonDocumentOptions { MaxDepth = 8 });
                var root = document.RootElement;
                if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("root").GetString() != paths.Root)
                    throw new IOException("The ACL journal has a different owner root.");
                var profile = root.GetProperty("profile").GetString() ?? "";
                const string prefix = "TableCloth.SkillPoc.";
                if (!profile.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(profile[prefix.Length..], "N", out _))
                    throw new IOException("The ACL journal profile is invalid.");
                var entries = root.GetProperty("entries");
                if (entries.GetArrayLength() > 20001) throw new IOException("The ACL journal exceeds its entry budget.");
                var scope = new AclScope(paths, 0, profile, CancellationToken.None, journal);
                foreach (var entry in entries.EnumerateArray())
                {
                    var path = paths.Check(entry.GetProperty("path").GetString()!, allowRoot: true);
                    if (!scope._captured.Add(path)) throw new IOException("The ACL journal contains duplicate targets.");
                    if (!File.Exists(path) && !Directory.Exists(path)) continue;
                    var identity = paths.Identity(path);
                    if (identity.VolumeSerial != entry.GetProperty("volume").GetUInt32() ||
                        identity.IndexHigh != entry.GetProperty("indexHigh").GetUInt32() || identity.IndexLow != entry.GetProperty("indexLow").GetUInt32())
                        throw new IOException("The ACL journal target changed identity.");
                    var sddl = entry.GetProperty("sddl").GetString()!;
                    var labelChanged = false;
                    if (entry.GetProperty("labelMayChange").GetBoolean())
                    {
                        Win32.CheckError(Win32.GetNamedSecurityInfo(path, 1, Win32.LabelInformation,
                            out _, out _, out _, out _, out var descriptor));
                        try
                        {
                            Win32.Check(Win32.DescriptorToString(descriptor, 1, Win32.LabelInformation, out var text, out _));
                            try
                            {
                                labelChanged = MandatoryLabels(new RawSecurityDescriptor(Marshal.PtrToStringUni(text) ?? "")) !=
                                    MandatoryLabels(new RawSecurityDescriptor(sddl));
                            }
                            finally { Win32.LocalFree(text); }
                        }
                        finally { Win32.LocalFree(descriptor); }
                    }
                    scope._snapshots.Add(new(path, sddl, identity) { DaclChanged = true, LabelChanged = labelChanged });
                }
                var failure = scope.Restore();
                var status = Win32.DeleteAppContainerProfile(profile);
                if (failure is not null) return failure;
                if (status != 0 && unchecked((uint)status) is not (0x80070002 or 0x80070003 or 0x80070490))
                    return "profile-recovery:hresult-" + unchecked((uint)status).ToString("X8");
                var runRoot = Path.Combine(paths.Root, ".appcontainer-runs");
                foreach (var snapshot in scope._snapshots)
                    if (Path.GetDirectoryName(snapshot.Path)?.Equals(runRoot, StringComparison.OrdinalIgnoreCase) == true &&
                        Guid.TryParseExact(Path.GetFileName(snapshot.Path), "N", out _))
                        paths.DeleteRunDirectory(snapshot.Path);
                scope.Complete();
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or
                UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException)
            { return "journal-recovery:" + Describe(error); }
        }
        return null;
    }
}
