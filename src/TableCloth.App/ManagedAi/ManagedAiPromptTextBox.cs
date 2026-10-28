using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using System;

namespace TableCloth.ManagedAi;

internal sealed class ManagedAiPromptTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Avalonia 11.3 TextBox supplies no IME client while read-only, but does not
        // requery it when IsReadOnly changes. Startup can finish after the first
        // focus, leaving the native input method detached until focus changes.
        // Refresh the client through Avalonia's public lifecycle event instead.
        if (change.Property == IsReadOnlyProperty && IsFocused)
        {
            RaiseEvent(new TextInputMethodClientRequeryRequestedEventArgs
            {
                RoutedEvent = InputMethod.TextInputMethodClientRequeryRequestedEvent
            });
        }
    }
}
