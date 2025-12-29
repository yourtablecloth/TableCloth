using System;
using System.Collections.Generic;
using System.Xml;

namespace TableCloth.Models.Catalog;

public static class XmlCatalogParser
{
    public static CatalogDocument ParseCatalogDocument(XmlReader reader)
    {
        var document = new CatalogDocument();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "Companions":
                        document.Companions = ParseCompanions(reader);
                        break;
                    case "InternetServices":
                        document.Services = ParseServices(reader);
                        break;
                }
            }
        }

        return document;
    }

    private static List<CatalogCompanion> ParseCompanions(XmlReader reader)
    {
        var companions = new List<CatalogCompanion>();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Companion")
            {
                var companion = new CatalogCompanion
                {
                    Id = reader.GetAttribute("Id"),
                    DisplayName = reader.GetAttribute("DisplayName"),
                    Url = reader.GetAttribute("Url"),
                    Arguments = reader.GetAttribute("Arguments"),
                };
                companions.Add(companion);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Companions")
            {
                break;
            }
        }

        return companions;
    }

    private static List<CatalogInternetService> ParseServices(XmlReader reader)
    {
        var services = new List<CatalogInternetService>();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Service")
            {
                var service = ParseService(reader);
                if (service != null)
                {
                    services.Add(service);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "InternetServices")
            {
                break;
            }
        }

        return services;
    }

    private static CatalogInternetService ParseService(XmlReader reader)
    {
        var service = new CatalogInternetService
        {
            Id = reader.GetAttribute("Id") ?? string.Empty,
            DisplayName = reader.GetAttribute("DisplayName") ?? string.Empty,
            Category = Enum.TryParse<CatalogInternetServiceCategory>(reader.GetAttribute("Category"), out var category) ? category : CatalogInternetServiceCategory.Other,
            Url = reader.GetAttribute("Url") ?? string.Empty,
        };

        var depth = reader.Depth;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "CompatNotes":
                        service.CompatibilityNotes = reader.ReadElementContentAsString();
                        break;
                    case "Package":
                        if (service.Packages == null)
                        {
                            service.Packages = new List<CatalogPackageInformation>();
                        }
                        service.Packages.Add(ParsePackage(reader));
                        break;
                    case "EdgeExtension":
                        if (service.EdgeExtensions == null)
                        {
                            service.EdgeExtensions = new List<CatalogEdgeExtensionInformation>();
                        }
                        service.EdgeExtensions.Add(ParseEdgeExtension(reader));
                        break;
                    case "CustomBootstrap":
                        service.CustomBootstrap = reader.ReadElementContentAsString();
                        break;
                    case "SearchKeywords":
                        service.SearchKeywords = reader.ReadElementContentAsString();
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
            {
                break;
            }
        }

        return service;
    }

    private static CatalogPackageInformation ParsePackage(XmlReader reader)
    {
        return new CatalogPackageInformation
        {
            Name = reader.GetAttribute("Name") ?? string.Empty,
            Url = reader.GetAttribute("Url") ?? string.Empty,
            Arguments = reader.GetAttribute("Arguments") ?? string.Empty,
        };
    }

    private static CatalogEdgeExtensionInformation ParseEdgeExtension(XmlReader reader)
    {
        return new CatalogEdgeExtensionInformation
        {
            Name = reader.GetAttribute("Name") ?? string.Empty,
            CrxUrl = reader.GetAttribute("CrxUrl") ?? string.Empty,
            ExtensionId = reader.GetAttribute("ExtensionId") ?? string.Empty,
        };
    }
}