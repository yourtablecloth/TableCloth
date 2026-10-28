using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using System.Text;
using TableCloth.ManagedAi;
using TableCloth.Models;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PromptAttachmentIconAndSkillButtonShareOneRowAboveEditor(bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"prompt-buttons-{(dark ? "dark" : "light")}.png");
        await WithAttachmentWindow(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640;
            window.Height = 540;
            Dispatcher.UIThread.RunJobs();
            var addSkill = Find<Button>(window, "ManagedAiPromptAddSkill");
            var attach = Find<Button>(window, "ManagedAiAttachFiles");
            var buttons = Find<StackPanel>(window, "ManagedAiPromptButtons");
            Assert.AreSame(buttons, addSkill.Parent);
            Assert.AreSame(buttons, attach.Parent);
            Assert.AreEqual(Avalonia.Layout.Orientation.Horizontal, buttons.Orientation);
            Assert.IsInstanceOfType<Avalonia.Controls.Shapes.Path>(attach.Content);
            Assert.AreEqual("Attach files to the message", AutomationProperties.GetName(attach));
            Assert.IsFalse(string.IsNullOrWhiteSpace(ToolTip.GetTip(attach)?.ToString()));
            var addBounds = VisibleBounds(window, addSkill);
            var attachBounds = VisibleBounds(window, attach);
            var buttonBounds = VisibleBounds(window, buttons);
            var inputBounds = VisibleBounds(window, Find<TextBox>(window, "ManagedAiChatInput"));
            var sendBounds = VisibleBounds(window, Find<Button>(window, "ManagedAiChatSend"));
            Assert.IsLessThanOrEqualTo(1d, Math.Abs(addBounds.Center.Y - attachBounds.Center.Y));
            Assert.IsLessThanOrEqualTo(attachBounds.Left, addBounds.Right);
            Assert.IsLessThanOrEqualTo(inputBounds.Top, buttonBounds.Bottom,
                "The skill and attachment button row must fit above the editor.");
            Assert.IsLessThanOrEqualTo(1d, Math.Abs(buttonBounds.Left - inputBounds.Left),
                "The button row must align with the editor's left edge.");
            Assert.IsGreaterThanOrEqualTo(window.ClientSize.Width * 0.75, inputBounds.Width,
                "The editor must retain horizontal space when the buttons move above it.");
            Assert.IsLessThanOrEqualTo(sendBounds.Left, inputBounds.Right,
                "The editor must fit beside the Send button without overlap.");
            SaveFrame(window, screenshot);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PromptPlainTextPastePreservesSelectionAndUndoWithoutAttaching(bool shiftInsert)
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "before OLD after";
            input.Focus();
            input.SelectionStart = 7;
            input.SelectionEnd = 10;
            await window.Clipboard!.SetTextAsync("new\ntext");
            Paste(window, shiftInsert);
            await Until(() => input.Text == "before new\ntext after" && !input.IsReadOnly);
            Assert.AreEqual(0, AttachmentCount(window));
            Assert.HasCount(0, fixture.Chat.Requests);
            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("before OLD after", input.Text);
        });

    [TestMethod]
    public Task PromptPastedTextStillMasksPersonalData()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            await window.Clipboard!.SetTextAsync("전화번호: 010-1234-5678");
            Paste(window);
            await Until(() => input.Text?.Contains("[전화번호]") == true && !input.IsReadOnly);
            Assert.DoesNotContain("010-1234-5678", input.Text!);
            Assert.AreEqual(0, AttachmentCount(window));
            Assert.HasCount(0, fixture.Chat.Requests);
        });

    [TestMethod]
    public Task PromptDirectPastePreservesUncommittedImeComposition()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "Existing draft";
            input.Focus();
            await window.Clipboard!.SetTextAsync("New clipboard text");
            var request = new TextInputMethodClientRequestedEventArgs
            {
                RoutedEvent = InputElement.TextInputMethodClientRequestedEvent
            };
            input.RaiseEvent(request);
            Assert.IsNotNull(request.Client);
            request.Client.SetPreeditText("한");
            try
            {
                // IME context-menu commands can invoke Paste without a KeyDown event.
                input.Paste();
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual("Existing draft", input.Text);
                Assert.AreEqual("한", ((Avalonia.Controls.Presenters.TextPresenter)request.Client.TextViewVisual!).PreeditText);
                Assert.IsFalse(input.IsReadOnly);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { request.Client.SetPreeditText(null); }
        });

    [TestMethod]
    public Task PromptClipboardFileAttachesImmutableContentAndSupportsAttachmentOnlySend()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var file = CreateAttachmentFile("notes.txt", "The sample value is BLUE-427.");
            try
            {
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "Keep this draft";
                await PasteFiles(window, file);
                Assert.AreEqual("Keep this draft", input.Text);
                Assert.HasCount(0, fixture.Chat.Requests);
                await File.WriteAllTextAsync(file, "Changed original content");
                input.Text = string.Empty;
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                var request = fixture.Chat.Requests.Single();
                Assert.AreEqual(string.Empty, request.Message);
                Assert.IsNotNull(request.Attachments);
                Assert.HasCount(1, request.Attachments);
                Assert.AreEqual("The sample value is BLUE-427.", request.Attachments[0].Text);
                Assert.AreEqual("notes.txt", request.Attachments[0].FileName);
                Assert.AreEqual(0, AttachmentCount(window), "Composer cards must disappear when the request starts.");
                Assert.IsFalse(Controls(window).Any(control => AutomationProperties.GetAutomationId(control) == "ManagedAiAttachmentRemove1"));
                fixture.Chat.Complete("BLUE-427");
                await Ready(window);
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.IsTrue(File.Exists(file), "Sending must never delete the user's original file.");
                Assert.IsTrue(Controls(window).OfType<SelectableTextBlock>().Any(block =>
                    block.Inlines?.OfType<Run>().Any(run => run.Text?.Contains("notes.txt") == true) == true));
            }
            finally { DeleteAttachmentFile(file); }
        });

    [TestMethod]
    public Task PromptAttachmentsAndTextRemainAvailableAfterFailureAndCancel()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var file = CreateAttachmentFile("retry.txt", "A stable attachment.");
            try
            {
                await PasteFiles(window, file);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "Please retry this";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.AreEqual(string.Empty, input.Text);
                fixture.Chat.Fail(AiFailureCode.ProviderNetworkUnavailable);
                await Ready(window);
                Assert.AreEqual("Please retry this", input.Text);
                Assert.AreEqual(1, AttachmentCount(window));
                Assert.IsTrue(Find<Button>(window, "ManagedAiAttachmentRemove1").IsEnabled);
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 2);
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.AreEqual(string.Empty, input.Text);
                CollectionAssert.AreEqual(fixture.Chat.Requests[0].Attachments![0].GetContent(),
                    fixture.Chat.Requests[1].Attachments![0].GetContent());
                Click(window, Find<Button>(window, "ManagedAiCancel"));
                await Ready(window);
                Assert.AreEqual("Please retry this", input.Text);
                Assert.AreEqual(1, AttachmentCount(window));
                Assert.IsTrue(Find<Button>(window, "ManagedAiAttachmentRemove1").IsEnabled);
            }
            finally { DeleteAttachmentFile(file); }
        });

    [TestMethod]
    public Task PromptAttachmentConsentCancellationKeepsCardsAndDraftWithoutStartingRequest()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var file = CreateAttachmentFile("consent.txt", "A stable attachment.");
            try
            {
                await PasteFiles(window, file);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                const string draft = "공동인증서 만료일을 알려 주세요.";
                input.Text = draft;
                fixture.Messages.Answer = AppMessageBoxResult.No;
                fixture.Messages.OnQuestion = () => Assert.AreEqual(1, AttachmentCount(window),
                    "Consent must not remove composer attachments before the request starts.");
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Ready(window);
                Assert.AreEqual(1, fixture.Messages.Questions);
                Assert.AreEqual(0, fixture.Certificates.Calls);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.AreEqual(draft, input.Text);
                Assert.AreEqual(1, AttachmentCount(window));
                Assert.IsTrue(Find<Button>(window, "ManagedAiAttachmentRemove1").IsEnabled);
            }
            finally { DeleteAttachmentFile(file); }
        }, fixture => fixture.Skills.Items =
            [new("tablecloth-certificate-expiry", "Certificate expiry", "Local expiry", "fixture", true)]);

    [TestMethod]
    public Task ClosingPendingAttachmentRequestDoesNotRestoreDisposedDraftControls()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var file = CreateAttachmentFile("closing.txt", "A stable attachment.");
            try
            {
                await PasteFiles(window, file);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                var cards = Find<Grid>(window, "ManagedAiAttachments");
                input.Text = "Close this pending request";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.HasCount(0, cards.Children);
                Assert.AreEqual(string.Empty, input.Text);
                var closed = false;
                window.Closed += (_, _) => closed = true;
                window.Close();
                await Until(() => closed);
                Dispatcher.UIThread.RunJobs();
                Assert.HasCount(0, cards.Children);
                Assert.AreEqual(string.Empty, input.Text, "Closing must not restore the canceled message after its controls are released.");
                Assert.AreEqual("A stable attachment.", fixture.Chat.Requests[0].Attachments![0].Text);
                Assert.IsTrue(File.Exists(file));
            }
            finally { DeleteAttachmentFile(file); }
        });

    [TestMethod]
    public Task HiddenImagePreviewRemainsUsableWhenFailedRequestRestoresCards()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            using var original = new MemoryStream(AttachmentTestPng);
            using var bitmap = new Bitmap(original);
            await window.Clipboard!.SetBitmapAsync(bitmap);
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            Paste(window);
            await Until(() => AttachmentCount(window) == 1 && !input.IsReadOnly);
            var preview = (Bitmap)Find<Grid>(window, "ManagedAiAttachments").GetVisualDescendants().OfType<Image>().Single().Source!;
            input.Text = "Retry the image";
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            Assert.AreEqual(0, AttachmentCount(window));
            fixture.Chat.Fail(AiFailureCode.ProviderNetworkUnavailable);
            await Ready(window);
            Assert.AreEqual(1, AttachmentCount(window));
            Assert.AreEqual("Retry the image", input.Text);
            Assert.AreSame(preview, Find<Grid>(window, "ManagedAiAttachments").GetVisualDescendants().OfType<Image>().Single().Source);
            using var preserved = new MemoryStream();
            preview.Save(preserved);
            Assert.IsGreaterThan(0L, preserved.Length);
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 2);
            Assert.AreEqual(0, AttachmentCount(window));
            CollectionAssert.AreEqual(fixture.Chat.Requests[0].Attachments![0].GetContent(),
                fixture.Chat.Requests[1].Attachments![0].GetContent());
            fixture.Chat.Complete("Image accepted.");
            await Ready(window);
            Assert.AreEqual(0, AttachmentCount(window));
        });

    [TestMethod]
    public Task PromptAttachmentBatchRejectsUnsupportedFileWithoutPartialAddition()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var valid = CreateAttachmentFile("valid.txt", "A text file");
            var unsupported = CreateAttachmentFile("unsupported.pdf", "%PDF-1.7 sample");
            try
            {
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "Original draft";
                await window.Clipboard!.SetFilesAsync(await StorageFiles(window, valid, unsupported));
                input.Focus();
                Paste(window);
                await Until(() => !input.IsReadOnly && Find<TextBlock>(window, "ManagedAiStatus").Text?.StartsWith("Could not attach") == true);
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.AreEqual("Original draft", input.Text);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { DeleteAttachmentFile(valid); DeleteAttachmentFile(unsupported); }
        });

    [TestMethod]
    public Task PromptRemoveAndNewChatClearAttachmentsWithDraftConfirmation()
        => WithAttachmentWindow(async (window, fixture) =>
        {
            var file = CreateAttachmentFile("draft.txt", "A draft attachment");
            try
            {
                await PasteFiles(window, file);
                Click(window, Find<Button>(window, "ManagedAiAttachmentRemove1"));
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.IsFalse(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                await PasteFiles(window, file);
                var questions = fixture.Messages.Questions;
                Click(window, Find<Button>(window, "ManagedAiNewChat"));
                Assert.AreEqual(questions + 1, fixture.Messages.Questions);
                Assert.AreEqual(0, AttachmentCount(window));
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { DeleteAttachmentFile(file); }
        });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PromptClipboardImageHasPreviewAndFitsMinimumWindow(bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"prompt-attachments-{(dark ? "dark" : "light")}.png");
        await WithAttachmentWindow(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640;
            window.Height = 540;
            using var stream = new MemoryStream(AttachmentTestPng);
            using var bitmap = new Bitmap(stream);
            await window.Clipboard!.SetBitmapAsync(bitmap);
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            Paste(window);
            await Until(() => AttachmentCount(window) == 1 && !input.IsReadOnly);
            using var sourceStillOwnedByClipboardSetter = new MemoryStream();
            bitmap.Save(sourceStillOwnedByClipboardSetter);
            Assert.IsGreaterThan(0L, sourceStillOwnedByClipboardSetter.Length);
            Assert.IsTrue(Controls(window).OfType<Image>().Any(image => image.Source is not null));
            VisibleBounds(window, input);
            VisibleBounds(window, Find<Button>(window, "ManagedAiChatSend"));
            VisibleBounds(window, Find<Button>(window, "ManagedAiAttachFiles"));
            VisibleBounds(window, Find<Grid>(window, "ManagedAiAttachments"));
            SaveFrame(window, screenshot);
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            Assert.AreEqual(0, AttachmentCount(window));
            Assert.IsTrue(fixture.Chat.Requests[0].Attachments!.Single().IsImage);
            fixture.Chat.Complete("A small test image.");
            await Ready(window);
            Assert.AreEqual(0, AttachmentCount(window));
        });
        TestContext.AddResultFile(screenshot);
    }

    private static readonly byte[] AttachmentTestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    private static int AttachmentCount(Window window) => Find<Grid>(window, "ManagedAiAttachments").Children.Count;

    private static void Paste(Window window, bool shiftInsert = false)
    {
        var key = shiftInsert ? PhysicalKey.Insert : PhysicalKey.V;
        var modifiers = shiftInsert ? RawInputModifiers.Shift : RawInputModifiers.Control;
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task PasteFiles(Window window, params string[] files)
    {
        var before = AttachmentCount(window);
        await window.Clipboard!.SetFilesAsync(await StorageFiles(window, files));
        var input = Find<TextBox>(window, "ManagedAiChatInput");
        input.Focus();
        Paste(window);
        await Until(() => AttachmentCount(window) == before + files.Length && !input.IsReadOnly);
    }

    private static async Task<IStorageFile[]> StorageFiles(Window window, params string[] paths)
    {
        var files = new List<IStorageFile>();
        foreach (var path in paths)
        {
            var file = await window.StorageProvider.TryGetFileFromPathAsync(path);
            Assert.IsNotNull(file);
            files.Add(file);
        }
        return files.ToArray();
    }

    private static string CreateAttachmentFile(string name, string text)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tablecloth-attachment-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private static void DeleteAttachmentFile(string path)
    {
        File.Delete(path);
        Directory.Delete(Path.GetDirectoryName(path)!);
    }

    private static Task WithAttachmentWindow(Func<ManagedAiWindow, Fixture, Task> action, Action<Fixture>? configure = null)
        => HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            configure?.Invoke(fixture);
            var window = fixture.Create(CultureInfo.GetCultureInfo("en-US"));
            try { window.Show(); await Ready(window); await action(window, fixture); }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
}
