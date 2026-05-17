using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;
using TableCloth.Models.WindowsSandbox;

namespace Spork.Components.Implementations
{
    public sealed class UserDataStore : IUserDataStore
    {
        public UserDataStore(ILogger<UserDataStore> logger)
        {
            _logger = logger;
        }

        private readonly ILogger _logger;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        public string UserDataFilePath
            => Path.Combine(SandboxMountPaths.DataDirectory, SporkUserData.FileName);

        public async Task<SporkUserData> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(UserDataFilePath))
                    return new SporkUserData();

                using (var stream = File.OpenRead(UserDataFilePath))
                {
                    var data = await JsonSerializer.DeserializeAsync<SporkUserData>(stream, Options, cancellationToken).ConfigureAwait(false);
                    return data ?? new SporkUserData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot deserialize user data file: {path}", UserDataFilePath);
                return new SporkUserData();
            }
        }

        public async Task SaveAsync(SporkUserData userData, CancellationToken cancellationToken = default)
        {
            if (userData == null)
                throw new ArgumentNullException(nameof(userData));

            try
            {
                var directory = Path.GetDirectoryName(UserDataFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = File.Create(UserDataFilePath))
                {
                    await JsonSerializer.SerializeAsync(stream, userData, Options, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot save user data to: {path}", UserDataFilePath);
            }
        }
    }
}
