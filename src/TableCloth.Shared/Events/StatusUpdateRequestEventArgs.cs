using System;

namespace TableCloth.Events
{
    public sealed class StatusUpdateRequestEventArgs : EventArgs
    {
        public string Status { get; set; } = string.Empty;
    }
}
