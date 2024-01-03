using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.Models
{
    public sealed class ApplicationStartupResultModel
    {
        public static ApplicationStartupResultModel FromSucceedResult(IEnumerable<string>
#if !NETFX
            ?
#endif
            providedWarnings = default)
            => new ApplicationStartupResultModel(true, providedWarnings, default, false);

        public static ApplicationStartupResultModel FromHaltedResult(IEnumerable<string>
#if !NETFX
            ?
#endif
            providedWarnings = default)
            => new ApplicationStartupResultModel(false, providedWarnings, default, false);

        public static ApplicationStartupResultModel FromException(Exception thrownException, bool isCritical = default, IEnumerable<string>
#if !NETFX
            ?
#endif
            providedWarnings = default)
            => new ApplicationStartupResultModel(false, providedWarnings, thrownException, isCritical);

        public static ApplicationStartupResultModel FromErrorMessage(string errorMessage, bool isCritical = default, IEnumerable<string>
#if !NETFX
            ?
#endif
            providedWarnings = default)
            => new ApplicationStartupResultModel(false, providedWarnings, new ApplicationException(errorMessage), isCritical);

        public static ApplicationStartupResultModel FromErrorMessage(string errorMessage, Exception
#if !NETFX
            ?
#endif
            innerException, bool isCritical = default, IEnumerable<string>
#if !NETFX
            ?
#endif
            providedWarnings = default)
            => new ApplicationStartupResultModel(false, providedWarnings, new ApplicationException(errorMessage, innerException), isCritical);

        public ApplicationStartupResultModel(
            bool succeed,

            IEnumerable<string>
#if !NETFX
            ?
#endif
            warnings = default,

            Exception
#if !NETFX
            ?
#endif
            failedReason = default,
            bool isCritical = default)
        {
            Succeed = succeed;
            Warnings = warnings ?? Enumerable.Empty<string>();
            FailedReason = failedReason;
            IsCritical = isCritical;
        }

        public bool Succeed { get; } = false;

        public IEnumerable<string> Warnings { get; } = new List<string>();

        public Exception
#if !NETFX
            ?
#endif
            FailedReason { get; }

        public bool IsCritical { get; } = false;
    }
}
