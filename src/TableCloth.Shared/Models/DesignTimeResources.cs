using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Resources
{
    internal static class DesignTimeResources
    {
#pragma warning disable IDE0090 // Use 'new(...)'
        private static readonly Random randomizer = new Random();
#pragma warning restore IDE0090 // Use 'new(...)'

        public static readonly IList<X509CertPair> DesignTimeCertPairs = GenerateDesignTimeCertPairs().ToList();

        public static readonly CatalogDocument DesignTimeCatalogDocument = GenerateDesignTimeCatalogDocument();

        public static readonly IList<CatalogPackageInformation> DesignTimePackageInformations = GeneratePackageItems(3, 9).ToList();

        public const int DefaultMinimum = 3;

        public const int DefaultMaximum = 12;

        public static IEnumerable<X509CertPair> GenerateDesignTimeCertPairs()
        {
            var now = DateTime.Now;

            var expired = new X509CertPairForDesigner(
                "James", now.AddYears(-2), now.AddYears(-1), "ACME Corp.", "A Bank", "KR");
            var current = new X509CertPairForDesigner(
                "Mary", now.AddDays(-1d), now.AddYears(1), "ABCD Corp.", "B Bank", "KR");
            var future = new X509CertPairForDesigner(
                "William", now.AddYears(3), now.AddYears(10), "Future Corp.", "C Bank", "KR");

            return new[] { expired, current, future };
        }

        public static bool? ConvertToTriState(int index)
        {
#pragma warning disable IDE0066 // Convert switch statement to expression
            switch (Math.Abs(index) % 3)
            {
                case 0: return null;
                case 1: return true;
                default: return false;
            }
#pragma warning restore IDE0066 // Convert switch statement to expression
        }

        public static string GenerateRandomErrorMessage(int index)
            => (index % 3 == 2) ? "Design time error simulation" : string.Empty;

#pragma warning disable IDE0090 // Use 'new(...)'
        public static CatalogDocument GenerateDesignTimeCatalogDocument(int min = DefaultMinimum, int max = DefaultMaximum) =>
            new CatalogDocument()
            {
                Services = new List<CatalogInternetService>(GenerateInternetServices(min, max)),
                Companions = new List<CatalogCompanion>(GenerateCompanions(min, max)),
            };
#pragma warning restore IDE0090 // Use 'new(...)'

        public static IEnumerable<CatalogCompanion> GenerateCompanions(int min = DefaultMinimum, int max = DefaultMaximum)
            => Enumerable
                .Range(0, Math.Min(min, Math.Max(max, randomizer.Next(min, max))))
                .Select(x => new CatalogCompanion()
                {
                    Id = $"Site{x}",
                    DisplayName = $"Sample {x}",
                    Arguments = "/silent",
                    Url = $"https://www.example.com/site{x}",
                });

        public static IEnumerable<CatalogInternetService> GenerateInternetServices(int min = DefaultMinimum, int max = DefaultMaximum)
            => Enumerable
                .Range(0, Math.Min(min, Math.Max(max, randomizer.Next(min, max))))
                .Select(x => new CatalogInternetService()
                {
                    Id = $"Site{x}",
                    DisplayName = $"Sample {x}",
                    Category = CatalogInternetServiceCategory.Banking,
                    Url = $"https://www.example.com/site{x}",
                    SearchKeywords = string.Join(";", Enumerable
                        .Range(0, Math.Min(min, Math.Max(max, randomizer.Next(min, max))))
                        .Select(y => $"Hint{y}")),
                    CompatibilityNotes = @"This website's security policy is configured to force the security agent to kill RDP sessions, which is a prerequisite for Windows Sandbox. We recommend that you refer to https://yourtablecloth.app/troubleshoot.html and change the AST to allow remote connections before using the site.",
                    Packages = new List<CatalogPackageInformation>(GeneratePackageItems(min, max)),
                });

        public static IEnumerable<CatalogPackageInformation> GeneratePackageItems(int min = DefaultMinimum, int max = DefaultMaximum)
            => Enumerable
                .Range(0, Math.Min(min, Math.Max(max, randomizer.Next(min, max))))
                .Select(x => new CatalogPackageInformation()
                {
                    Name = $"Item{x}",
                    Url = $"https://www.example.com/item{x}/setup.exe",
                    Arguments = "/silent",
                });
    }
}
