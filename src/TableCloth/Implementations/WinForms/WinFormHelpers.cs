using System;
using System.Drawing;
using System.Windows.Forms;

namespace TableCloth.Implementations.WinForms
{
    internal static class WinFormHelpers
    {
        public static Label CreateLabel<TControl>(this TControl parentControl, string text = default)
            where TControl : Control
            => new()
            {
                Parent = parentControl,
                Text = text ?? string.Empty,
                AutoSize = true,
            };

        public static CheckBox CreateCheckBox<TControl>(this TControl parentControl, string text, bool @checked = false)
            where TControl : Control
            => new()
            {
                Parent = parentControl,
                Text = text,
                Checked = @checked,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };

        public static Button CreateButton<TControl>(this TControl parentControl, string text, DialogResult dialogResult = default, Action<Button> handler = null)
            where TControl : Control
        {
            var button = new Button()
            {
                Parent = parentControl,
                Text = text,
                AutoSize = true,
                DialogResult = dialogResult,
            };

            if (handler != null)
                button = button.AddClickEvent(handler);

            return button;
        }

        public static TButtonBase AddClickEvent<TButtonBase>(this TButtonBase targetControl, Action<TButtonBase> handler)
            where TButtonBase : ButtonBase
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            targetControl.Click += new EventHandler((_sender, _e) =>
            {
                if (_sender is TButtonBase realSender && handler != null)
                    handler.Invoke(realSender);
            });

            return targetControl;
        }
    }
}
