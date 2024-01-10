using System;
using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Resources;

internal static class DesignTimeCatalog
{
    public static CatalogDocument DesignTimeCatalogDocument = new()
    {
        Services = new List<CatalogInternetService>(new CatalogInternetService[] {
          new() {
            Id = "Site1", DisplayName = "Sample 1", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.example.com/",
              CompatibilityNotes = @"This website's security policy is configured to force the security agent to kill RDP sessions, which is a prerequisite for Windows Sandbox. We recommend that you refer to https://yourtablecloth.app/troubleshoot.html and change the AST to allow remote connections before using the site.", Packages = new List<CatalogPackageInformation>(new CatalogPackageInformation[] {
                new() {
                  Name = "Item1", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item2", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item3", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item4", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item5", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }
              })
          }, new() {
            Id = "Site2", DisplayName = "Sample 2", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.example.com/",
              CompatibilityNotes = "", Packages = new List<CatalogPackageInformation>(new CatalogPackageInformation[] {
                new() {
                  Name = "Item1", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item2", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }              })
          }, new() {
            Id = "Site3", DisplayName = "Sample 3", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.example.com/",
              CompatibilityNotes = @"", Packages = new List<CatalogPackageInformation>(new CatalogPackageInformation[] {
                new() {
                  Name = "Item1", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item2", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item3", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item4", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item5", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }
              })
          }, new() {
            Id = "Site4", DisplayName = "Sample 4", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.example.com/",
              CompatibilityNotes = @"This website's security policy is configured to force the security agent to kill RDP sessions, which is a prerequisite for Windows Sandbox. We recommend that you refer to https://yourtablecloth.app/troubleshoot.html and change the AST to allow remote connections before using the site.", Packages = new List<CatalogPackageInformation>(new CatalogPackageInformation[] {
                new() {
                  Name = "Item1", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item2", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item3", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item4", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }
              })
          }, new() {
            Id = "Site5", DisplayName = "Sample 5", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.example.com/",
              CompatibilityNotes = "", Packages = new List<CatalogPackageInformation>(new CatalogPackageInformation[] {
                new() {
                  Name = "Item1", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item2", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item3", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item4", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }, new() {
                  Name = "Item5", Url = "https://www.example.com/setup.exe", Arguments = "/silent",
                }
              })
          },
        }),
        Companions = new List<CatalogCompanion>(Array.Empty<CatalogCompanion>())
    };
}
