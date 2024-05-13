namespace Sponge.Models
{
    public class RemovePrivacyFilesRequest
    {
        public RemovePrivacyFilesRequest(int overwriteCount)
        {
            OverwriteCount = overwriteCount;
        }

        public int OverwriteCount { get; } = 0;
    }
}
