using System;

namespace TableCloth.Events;

[Serializable]
public sealed class DialogRequestEventArgs : EventArgs
{
    public DialogRequestEventArgs(bool? dialogResult)
    {
        this.DialogResult = dialogResult;
    }

    public bool? DialogResult { get; set; }
}
