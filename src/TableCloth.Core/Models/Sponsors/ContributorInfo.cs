using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TableCloth.Models.Sponsors
{
    /// <summary>
    /// yourtablecloth.app/contributors.json 의 파싱 결과.
    /// 생성기(yourtablecloth.github.io)가 REST /contributors?anon=true 로 TableCloth +
    /// TableClothCatalog 기여자를 합산해 만든다. 이메일만 있는 익명 기여자는 신원 없이
    /// <see cref="AnonymousCount"/> 로만 집계된다.
    /// </summary>
    public sealed class ContributorsDocument
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public int TotalCount { get; set; }
        public int AnonymousCount { get; set; }
        public List<ContributorInfo> Contributors { get; set; } = new List<ContributorInfo>();

        public static ContributorsDocument Parse(string json)
        {
            var result = new ContributorsDocument();
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
                        case "anonymousCount":
                            if (reader.TokenType == JsonTokenType.Number)
                                result.AnonymousCount = reader.GetInt32();
                            break;
                        case "contributors":
                            result.Contributors = ParseContributors(ref reader);
                            break;
                    }
                }
            }

            return result;
        }

        private static List<ContributorInfo> ParseContributors(ref Utf8JsonReader reader)
        {
            var contributors = new List<ContributorInfo>();

            if (reader.TokenType != JsonTokenType.StartArray)
                return contributors;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    contributors.Add(ContributorInfo.Parse(ref reader));
                }
            }

            return contributors;
        }
    }

    public sealed class ContributorInfo
    {
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public int Contributions { get; set; }

        internal static ContributorInfo Parse(ref Utf8JsonReader reader)
        {
            var result = new ContributorInfo();

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
                        case "contributions":
                            if (reader.TokenType == JsonTokenType.Number)
                                result.Contributions = reader.GetInt32();
                            break;
                    }
                }
            }

            return result;
        }
    }
}
