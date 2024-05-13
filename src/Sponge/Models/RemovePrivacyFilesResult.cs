namespace Sponge.Models
{
    public class RemovePrivacyFilesResult
    {
        public RemovePrivacyFilesResult(int succeedFileCount, int failedFileCount, string message)
        {
            SucceedFileCount = succeedFileCount;
            FailedFileCount = failedFileCount;
            Message = message;
        }

        public int SucceedFileCount { get; } = 0;
        public int FailedFileCount { get; } = 0;
        public int TotalFileCount => SucceedFileCount + FailedFileCount;
        public string Message { get; } = string.Empty;

        public override string ToString() => $"Succeed: {SucceedFileCount}, Failed: {FailedFileCount}";
    }
}
