namespace Sponge
{
    public class RemovePrivacyFilesResult
    {
        public RemovePrivacyFilesResult(int succeedFileCount, int failedFileCount)
        {
            SucceedFileCount = succeedFileCount;
            FailedFileCount = failedFileCount;
        }

        public int SucceedFileCount { get; } = 0;
        public int FailedFileCount { get; } = 0;
        public int TotalFileCount => SucceedFileCount + FailedFileCount;

        public override string ToString() => $"Succeed: {SucceedFileCount}, Failed: {FailedFileCount}";
    }
}
