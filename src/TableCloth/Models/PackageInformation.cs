using System;

namespace TableCloth.Models
{
    [Serializable]
    public sealed class PackageInformation
    {
        public string Name { get; set; }
        public Uri PackageDownloadUrl { get; set; }
        public string Arguments { get; set; }

        public override string ToString()
            => $"{Name} - {PackageDownloadUrl}";
    }
}
