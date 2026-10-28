using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Threading;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class ManagedAiPromptTextBoxTests
{
    [TestMethod]
    public async Task FirstReadOnlyFocusRequeriesEditableClientWithoutLosingFocus()
    {
        await HeadlessTestSession.Shared.Dispatch(() =>
        {
            var input = new ManagedAiPromptTextBox { IsReadOnly = true, AcceptsReturn = true, Text = "draft" };
            var window = new Window { Content = input };
            var ime = new ClientRequestObserver(window);
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(input.Focus());
                ime.RequestFocusedClient();
                Assert.IsNull(ime.Client, "A read-only TextBox must not activate text input.");
                input.SelectionStart = 1;
                input.SelectionEnd = 4;
                var caret = input.CaretIndex;
                var lostFocus = 0;
                input.LostFocus += (_, _) => lostFocus++;

                input.IsReadOnly = false;

                Assert.IsNotNull(ime.Client, "Completing startup must expose the client on the first focused control.");
                Assert.AreSame(input, window.FocusManager!.GetFocusedElement());
                Assert.AreEqual(0, lostFocus);
                Assert.AreEqual("draft", input.Text);
                Assert.AreEqual(1, input.SelectionStart);
                Assert.AreEqual(4, input.SelectionEnd);
                Assert.AreEqual(caret, input.CaretIndex);

                // Exercise the real TextBox client supplied by the public query event.
                ime.Client.SetPreeditText("한");
                Assert.IsTrue(ime.Client.SupportsPreedit);
                Assert.IsInstanceOfType<TextPresenter>(ime.Client.TextViewVisual);
                Assert.AreEqual("한", ((TextPresenter)ime.Client.TextViewVisual).PreeditText);
                ime.Client.SetPreeditText(null);
            }
            finally { window.Close(); }
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task BusyTransitionsRequeryClientOnlyForFocusedPrompt()
    {
        await HeadlessTestSession.Shared.Dispatch(() =>
        {
            var input = new ManagedAiPromptTextBox { AcceptsReturn = true, Text = "draft" };
            var other = new TextBox();
            var window = new Window
            {
                Content = new StackPanel { Children = { input, other } }
            };
            var ime = new ClientRequestObserver(window);
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(input.Focus());
                ime.RequestFocusedClient();
                var originalClient = ime.Client;
                Assert.IsNotNull(originalClient);

                input.IsReadOnly = true;
                Assert.IsNull(ime.Client, "Starting an operation must stop exposing the read-only prompt's input client.");
                input.IsReadOnly = false;
                Assert.AreSame(originalClient, ime.Client);

                Assert.IsTrue(other.Focus());
                ime.RequestFocusedClient();
                var otherClient = ime.Client;
                var calls = ime.RequestCount;
                input.IsReadOnly = true;
                input.IsReadOnly = false;
                Assert.AreSame(otherClient, ime.Client);
                Assert.AreEqual(calls, ime.RequestCount);
                Assert.AreSame(other, window.FocusManager!.GetFocusedElement());
            }
            finally { window.Close(); }
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    // Headless has no platform IME, and ITextInputMethodImpl prevents external
    // implementations. Observe the public requery route and request the actual
    // focused TextBox client, as the input manager does. This verifies the
    // control lifecycle contract, not the Win32 input manager or Korean IME.
    private sealed class ClientRequestObserver
    {
        private readonly Window _window;

        public TextInputMethodClient? Client { get; private set; }
        public int RequestCount { get; private set; }

        public ClientRequestObserver(Window window)
        {
            _window = window;
            window.AddHandler(InputMethod.TextInputMethodClientRequeryRequestedEvent,
                (_, _) => RequestFocusedClient());
        }

        public void RequestFocusedClient()
        {
            var args = new TextInputMethodClientRequestedEventArgs
            {
                RoutedEvent = InputElement.TextInputMethodClientRequestedEvent
            };
            _window.FocusManager!.GetFocusedElement()!.RaiseEvent(args);
            Client = args.Client;
            RequestCount++;
        }
    }
}
