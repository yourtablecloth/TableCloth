#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Models.Catalog;

namespace TableCloth
{
    internal static class SharedExtensions
    {
        public static bool HasAnyCompatNotes(this CatalogDocument catalog, IEnumerable<string> targets)
            => catalog.Services.Where(x => targets.Contains(x.Id)).Any(x => !string.IsNullOrWhiteSpace(x.CompatibilityNotes?.Trim()));

        public static TExpectedType EnsureNotNullWithCast<T, TExpectedType>(this T? value, string message, Exception? innerException = default,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            where T : class
            where TExpectedType : class
        {
            var expected = value as TExpectedType;

            if (expected == null)
                throw new TableClothAppException(message, file, member, line, innerException);

            return expected;
        }

        public static TExpectedType EnsureArgumentNotNullWithCast<T, TExpectedType>(this T? value, string message, string paramName,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            where T : class
            where TExpectedType : class
        {
            var expected = value as TExpectedType;

            if (expected == null)
                throw new TableClothAppException(message, file, member, line, new ArgumentException("Incompatible parameter type.", paramName: paramName));

            return expected;
        }

        public static T EnsureNotNull<T>(this T? value, string message, Exception? innerException = default,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            where T : class
        {
            if (value == null)
                throw new TableClothAppException(message, file, member, line, innerException);

            return value;
        }

        public static T EnsureArgumentNotNull<T>(this T? value, string message, string paramName,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            where T : class
        {
            if (value == null)
                throw new TableClothAppException(message, file, member, line, new ArgumentNullException(paramName, "Parameter cannot be null."));

            return value;
        }
    }
}
