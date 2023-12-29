using System;

namespace TableCloth.Events;

public sealed class StatusUpdateRequestEventArgs : EventArgs
{
    public StatusUpdateRequestEventArgs(string status)
    {
        Status = status;
    }

    public string Status { get; set; }
}
