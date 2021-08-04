using System;
using System.Windows.Forms;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations.WinForms
{
    public sealed class WinFormMessageBox : IAppMessageBox
    {
        public void DisplayInfo(string message)
            => MessageBox.Show(
                message, StringResources.TitleText_Info,
                MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

        public void DisplayError(Exception failureReason, bool isCritical)
            => DisplayError(failureReason is AggregateException ? failureReason.InnerException.Message : failureReason.Message, isCritical);

        public void DisplayError(string message, bool isCritical)
            => MessageBox.Show(
                message, (isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning),
                MessageBoxButtons.OK, (isCritical ? MessageBoxIcon.Stop : MessageBoxIcon.Warning), MessageBoxDefaultButton.Button1);
    }
}
