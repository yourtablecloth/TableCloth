using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Hostess.Components
{
    public sealed class AppStartup : IDisposable
    {
        public AppStartup()
        {
            _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
        }

        private bool _disposed;
        private readonly Mutex _mutex;
        private readonly bool _isFirstInstance;

        ~AppStartup() => Dispose(false);

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }

            _disposed = true;
        }

        public bool HasRequirementsMet(IList<string> warnings, out Exception failedReason, out bool isCritical)
        {
            if (!this._isFirstInstance)
            {
                failedReason = new ApplicationException(StringResources.Error_Already_TableCloth_Running);
                isCritical = true;
                return false;
            }

            failedReason = null;
            isCritical = false;
            return true;
        }

        public bool Initialize(out Exception failedReason, out bool isCritical)
        {
            failedReason = null;
            isCritical = false;
            return true;
        }
    }
}
