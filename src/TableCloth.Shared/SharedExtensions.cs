using Microsoft.Win32;

namespace TableCloth
{
    internal static class SharedExtensions
    {
        public static TValue GetValue<TValue>(this RegistryKey registryKey, string name,
            TValue defaultValue = default, RegistryValueOptions options = default)
            where TValue : struct
        {
            var value = registryKey.GetValue(name, defaultValue, options) as TValue?;
            return value.HasValue ? value.Value : defaultValue;
        }
    }
}
