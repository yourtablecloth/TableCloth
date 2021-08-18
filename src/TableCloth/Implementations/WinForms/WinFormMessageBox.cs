using System;
using System.Windows.Forms;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations.WinForms
{
    public sealed class WinFormMessageBox : IAppMessageBox
    {
        public void DisplayInfo(object parentWindowHandle, string message)
            => InvokeViaUIThread(parentWindowHandle as IWin32Window, () => MessageBox.Show(
                (parentWindowHandle is IWin32Window window ? window : null),
                message, StringResources.TitleText_Info,
                MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1));

        public void DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical)
            => DisplayError(parentWindowHandle, failureReason is AggregateException ? failureReason.InnerException.Message : failureReason.Message, isCritical);

        public void DisplayError(object parentWindowHandle, string message, bool isCritical)
            => InvokeViaUIThread(parentWindowHandle as IWin32Window, () => MessageBox.Show(
                (parentWindowHandle is IWin32Window window ? window : null),
                message, (isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning),
                MessageBoxButtons.OK, (isCritical ? MessageBoxIcon.Stop : MessageBoxIcon.Warning), MessageBoxDefaultButton.Button1));

        private DialogResult InvokeViaUIThread(IWin32Window targetWindow, Func<DialogResult> func)
            => (targetWindow is Control c && c.InvokeRequired) ? (DialogResult)c.Invoke(func) : func.Invoke();
    }
}
