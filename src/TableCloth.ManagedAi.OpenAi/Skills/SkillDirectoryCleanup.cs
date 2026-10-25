namespace TableCloth.ManagedAi.OpenAi;

internal static class SkillDirectoryCleanup
{
    // Only for final cleanup of a unique directory owned by this operation. File contention
    // must not replace a committed result or an earlier validation/cancellation exception.
    // Path-policy and other unexpected errors still propagate. Rollback itself remains strict.
    public static void DeleteOwnedBestEffort(ManagedAiPaths paths, string directory)
    {
        try { paths.DeleteOwnedDirectory(directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
