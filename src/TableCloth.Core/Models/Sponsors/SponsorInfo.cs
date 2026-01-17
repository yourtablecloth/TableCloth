using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TableCloth.Models.Sponsors
{
    public sealed class SponsorsDocument
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public int TotalCount { get; set; }
        public List<SponsorInfo> Sponsors { get; set; } = new List<SponsorInfo>();

        public static SponsorsDocument Parse(string json)
        {
            var result = new SponsorsDocument();
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "generatedAt":
                            if (reader.TokenType == JsonTokenType.String)
                                result.GeneratedAt = reader.GetDateTimeOffset();
                            break;
                        case "totalCount":
                            if (reader.TokenType == JsonTokenType.Number)
                                result.TotalCount = reader.GetInt32();
                            break;
                        case "sponsors":
                            result.Sponsors = ParseSponsors(ref reader);
                            break;
                    }
                }
            }

            return result;
        }

        private static List<SponsorInfo> ParseSponsors(ref Utf8JsonReader reader)
        {
            var sponsors = new List<SponsorInfo>();

            if (reader.TokenType != JsonTokenType.StartArray)
                return sponsors;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    sponsors.Add(SponsorInfo.Parse(ref reader));
                }
            }

            return sponsors;
        }
    }

    public sealed class SponsorInfo
    {
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public SponsorTier Tier { get; set; } = new SponsorTier();
        public DateTimeOffset Since { get; set; }

        internal static SponsorInfo Parse(ref Utf8JsonReader reader)
        {
            var result = new SponsorInfo();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "login":
                            result.Login = reader.GetString() ?? string.Empty;
                            break;
                        case "name":
                            result.Name = reader.GetString() ?? string.Empty;
                            break;
                        case "avatarUrl":
                            result.AvatarUrl = reader.GetString() ?? string.Empty;
                            break;
                        case "profileUrl":
                            result.ProfileUrl = reader.GetString() ?? string.Empty;
                            break;
                        case "tier":
                            result.Tier = SponsorTier.Parse(ref reader);
                            break;
                        case "since":
                            if (reader.TokenType == JsonTokenType.String)
                                result.Since = reader.GetDateTimeOffset();
                            break;
                    }
                }
            }

            return result;
        }
    }

    public sealed class SponsorTier
    {
        public string Name { get; set; } = string.Empty;
        public int MonthlyPrice { get; set; }
        public bool IsOneTime { get; set; }

        internal static SponsorTier Parse(ref Utf8JsonReader reader)
        {
            var result = new SponsorTier();

            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "name":
                            result.Name = reader.GetString() ?? string.Empty;
                            break;
                        case "monthlyPrice":
                            if (reader.TokenType == JsonTokenType.Number)
                                result.MonthlyPrice = reader.GetInt32();
                            break;
                        case "isOneTime":
                            if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                                result.IsOneTime = reader.GetBoolean();
                            break;
                    }
                }
            }

            return result;
        }
    }
}

