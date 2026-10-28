using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(4, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, true)]
    public async Task PromptAttachmentCardsUseOneRowOfEqualColumnsAtMinimumWindowSize(int count, bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts",
            $"prompt-attachment-columns-{count}-{(dark ? "dark" : "light")}.png");
        await WithAttachmentWindow(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640; window.Height = 540;
            var files = CreateAttachmentLayoutFiles();
            try
            {
                await PasteFiles(window, files.Take(count).ToArray());
                AssertAttachmentColumnLayout(window, count);
                for (var index = 0; index < count; index++)
                {
                    var name = Find<TextBlock>(window, $"ManagedAiAttachmentName{index + 1}");
                    Assert.AreEqual(Path.GetFileName(files[index]), name.Text);
                    Assert.AreEqual(TextTrimming.CharacterEllipsis, name.TextTrimming);
                    Assert.AreEqual(TextWrapping.NoWrap, name.TextWrapping);
                    Assert.AreEqual(name.Text, ToolTip.GetTip(name));
                    var remove = Find<Button>(window, $"ManagedAiAttachmentRemove{index + 1}");
                    Assert.AreEqual($"Remove attachment {name.Text}", AutomationProperties.GetName(remove));
                    Assert.AreEqual(AutomationProperties.GetName(remove), ToolTip.GetTip(remove));
                    Assert.IsTrue(remove.IsEnabled);
                }
                if (count >= 2)
                {
                    var imageCard = Find<Border>(window, "ManagedAiAttachmentCard2");
                    var image = imageCard.GetVisualDescendants().OfType<Image>().Single();
                    Assert.IsNotNull(image.Source);
                    Assert.Contains(Path.GetFileName(files[1]), AutomationProperties.GetName(image) ?? string.Empty);
                    VisibleBounds(window, image);
                }
                if (count >= 3)
                {
                    var masked = Find<Button>(window, "ManagedAiAttachmentTextPreview3");
                    Assert.AreEqual("Preview masked text", ((TextBlock)masked.Content!).Text);
                    Assert.AreEqual("Preview masked text", ToolTip.GetTip(masked));
                    Assert.Contains(Path.GetFileName(files[2]), AutomationProperties.GetName(masked) ?? string.Empty);
                    VisibleBounds(window, (TextBlock)masked.Content!);
                }
                foreach (var textContent in Controls(window).OfType<TextBox>().Where(control =>
                    AutomationProperties.GetAutomationId(control)?.StartsWith("ManagedAiAttachmentTextContent", StringComparison.Ordinal) == true))
                    Assert.IsFalse(textContent.IsEffectivelyVisible, "Attachment text previews must start collapsed.");
                Assert.HasCount(0, fixture.Chat.Requests);
                SaveFrame(window, screenshot);
            }
            finally { foreach (var file in files) DeleteAttachmentFile(file); }
        });
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PromptAttachmentColumnsKeepTextPreviewAndRebalanceAfterEachRemove(bool dark)
        => WithAttachmentWindow(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640; window.Height = 540;
            var files = CreateAttachmentLayoutFiles();
            try
            {
                await PasteFiles(window, files);
                var preview = Find<Button>(window, "ManagedAiAttachmentTextPreview3");
                var content = Find<TextBox>(window, "ManagedAiAttachmentTextContent3");
                Assert.IsFalse(content.IsVisible);
                Click(window, preview);
                await Until(() => content.IsEffectivelyVisible);
                Assert.IsTrue(content.IsReadOnly);
                Assert.Contains("fixture masked text", content.Text!);
                Assert.DoesNotContain("010-1234-5678", content.Text!);
                VisibleBounds(window, content);
                AssertAttachmentColumnLayout(window, 4);
                Assert.AreEqual("Hide preview", ((TextBlock)preview.Content!).Text);
                // The real themed button must also toggle through its keyboard command.
                Assert.IsTrue(preview.Focus());
                window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.IsFalse(content.IsVisible);
                Assert.AreEqual("Preview masked text", ((TextBlock)preview.Content!).Text);
                Click(window, Find<Button>(window, "ManagedAiAttachmentRemove2"));
                AssertAttachmentColumnLayout(window, 3);
                CollectionAssert.AreEqual(new[] { files[0], files[2], files[3] }.Select(Path.GetFileName).ToArray(),
                    Enumerable.Range(1, 3).Select(index => Find<TextBlock>(window, $"ManagedAiAttachmentName{index}").Text).ToArray());
                Click(window, Find<Button>(window, "ManagedAiAttachmentRemove2"));
                AssertAttachmentColumnLayout(window, 2);
                Click(window, Find<Button>(window, "ManagedAiAttachmentRemove1"));
                AssertAttachmentColumnLayout(window, 1);
                Assert.AreEqual(Path.GetFileName(files[3]), Find<TextBlock>(window, "ManagedAiAttachmentName1").Text);
                Click(window, Find<Button>(window, "ManagedAiAttachmentRemove1"));
                var list = Find<Grid>(window, "ManagedAiAttachments");
                Assert.HasCount(0, list.Children);
                Assert.HasCount(0, list.ColumnDefinitions);
                Assert.IsFalse(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { foreach (var file in files) DeleteAttachmentFile(file); }
        });

    private static void AssertAttachmentColumnLayout(Window window, int count)
    {
        Dispatcher.UIThread.RunJobs();
        var list = Find<Grid>(window, "ManagedAiAttachments");
        Assert.HasCount(count, list.Children);
        Assert.HasCount(count, list.ColumnDefinitions);
        Assert.HasCount(1, list.RowDefinitions);
        Assert.AreEqual(6d, list.ColumnSpacing);
        var listBounds = VisibleBounds(window, list);
        Rect? previous = null;
        for (var index = 0; index < count; index++)
        {
            var card = Find<Border>(window, $"ManagedAiAttachmentCard{index + 1}");
            Assert.AreEqual(index, Grid.GetColumn(card));
            Assert.AreEqual(0, Grid.GetRow(card));
            var bounds = VisibleBounds(window, card);
            Assert.IsLessThanOrEqualTo(1d, Math.Abs(bounds.Top - listBounds.Top), "Every attachment must remain on the same row.");
            Assert.IsTrue(bounds.Left >= listBounds.Left - 1 && bounds.Right <= listBounds.Right + 1);
            if (previous is { } before)
            {
                Assert.IsLessThanOrEqualTo(1d, Math.Abs(bounds.Width - before.Width), "Attachment columns must have equal widths.");
                Assert.IsLessThanOrEqualTo(1d, Math.Abs(bounds.Left - before.Right - 6), "Attachment columns must keep their six-pixel spacing.");
            }
            previous = bounds;
            foreach (var control in card.GetVisualDescendants().OfType<Control>().Where(control =>
                control.IsEffectivelyVisible && (control is Image || control is Button ||
                    control is TextBlock block && (block.Text == Find<TextBlock>(window, $"ManagedAiAttachmentName{index + 1}").Text ||
                        block.GetVisualAncestors().OfType<Button>().Any(button =>
                            AutomationProperties.GetAutomationId(button)?.StartsWith("ManagedAiAttachmentTextPreview", StringComparison.Ordinal) == true)))))
            {
                var child = VisibleBounds(window, control);
                Assert.IsTrue(child.Left >= bounds.Left - 1 && child.Right <= bounds.Right + 1,
                    $"{control.GetType().Name} must fit inside its attachment column: {child} within {bounds}.");
            }
        }
        VisibleBounds(window, Find<TextBox>(window, "ManagedAiChatInput"));
        VisibleBounds(window, Find<Button>(window, "ManagedAiChatSend"));
        VisibleBounds(window, Find<Button>(window, "ManagedAiAttachFiles"));
    }

    private static string[] CreateAttachmentLayoutFiles()
    {
        var files = new[]
        {
            CreateAttachmentFile("long-text-" + new string('a', 80) + ".txt", "First fixture text preview."),
            CreateAttachmentFile("long-image-" + new string('b', 80) + ".png", ""),
            CreateAttachmentFile("long-masked-text-" + new string('c', 70) + ".txt", "fixture masked text\nPhone: 010-1234-5678"),
            CreateAttachmentFile("long-second-image-" + new string('d', 70) + ".png", "")
        };
        File.WriteAllBytes(files[1], AttachmentTestPng);
        File.WriteAllBytes(files[3], AttachmentTestPng);
        return files;
    }
}
