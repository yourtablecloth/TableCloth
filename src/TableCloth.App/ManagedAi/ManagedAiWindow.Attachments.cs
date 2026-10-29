using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ManagedAi;

public sealed partial class ManagedAiWindow
{
    private readonly Button _attachPromptFiles = new()
    {
        MinWidth = 36, MinHeight = 32, Padding = new Thickness(8, 4),
        VerticalAlignment = VerticalAlignment.Bottom
    };
    private readonly Grid _promptAttachmentList = new()
    {
        ColumnSpacing = 6, RowDefinitions = new RowDefinitions("Auto")
    };
    private readonly List<ManagedAiPromptAttachment> _promptAttachments = [];
    private readonly TextBlock _promptAttachmentNotice = Text(L(
        "첨부 파일은 메시지와 함께 OpenAI에 전송합니다. 이미지는 자동 가림 없이 전송합니다.",
        "Attachments are sent to OpenAI with your message. Images are sent without automatic redaction."));
    private CancellationTokenSource? _promptAttachmentRead;
    private int _promptAttachmentGeneration;
    private bool _promptAttachmentClosed;

    private bool HasPromptAttachmentOperation => _promptAttachmentRead is not null;
    private bool HasPromptAttachments => _promptAttachments.Count > 0;

    private Button CreatePromptAttachmentButton()
    {
        var clip = new Avalonia.Controls.Shapes.Path
        {
            Width = 18, Height = 18, Stretch = Stretch.Uniform,
            StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
            Data = Geometry.Parse("M7.5,14.5 L14.5,7.5 C15.3,6.7 16.7,6.7 17.5,7.5 " +
                "C18.3,8.3 18.3,9.7 17.5,10.5 L9.5,18.5 C7.8,20.2 5.2,20.2 3.5,18.5 " +
                "C1.8,16.8 1.8,14.2 3.5,12.5 L12.5,3.5 C14.9,1.1 18.6,1.1 21,3.5 " +
                "C23.4,5.9 23.4,9.6 21,12 L12,21")
        };
        clip.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, _attachPromptFiles.GetObservable(Button.ForegroundProperty));
        _attachPromptFiles.Content = clip;
        return _attachPromptFiles;
    }

    private Control CreatePromptAttachmentControls()
    {
        var container = new StackPanel { Spacing = 4 };
        AutomationProperties.SetAutomationId(container, "ManagedAiAttachmentControls");
        AutomationProperties.SetAutomationId(_attachPromptFiles, "ManagedAiAttachFiles");
        AutomationProperties.SetName(_attachPromptFiles, L("메시지에 파일 첨부", "Attach files to the message"));
        ToolTip.SetTip(_attachPromptFiles, L(
            "PNG, JPEG 및 텍스트 파일을 최대 4개, 파일당 8 MiB, 전체 16 MiB까지 첨부할 수 있습니다. " +
            "텍스트는 파일당 64 KiB 및 12,000자, 전체 24,000자까지 지원합니다. 이미지는 한 변 8,192픽셀 및 16메가픽셀까지 지원합니다.",
            "Attach up to 4 PNG, JPEG, or text files, 8 MiB each and 16 MiB total. " +
            "Text supports 64 KiB and 12,000 characters per file, 24,000 characters total. Images support 8,192 pixels per side and 16 megapixels."));
        AutomationProperties.SetAutomationId(_promptAttachmentList, "ManagedAiAttachments");
        AutomationProperties.SetAutomationId(_promptAttachmentNotice, "ManagedAiAttachmentNotice");
        container.Children.Add(_promptAttachmentNotice);
        container.Children.Add(new ScrollViewer
        {
            Content = _promptAttachmentList, MaxHeight = 150,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        return container;
    }

    private void InitializePromptAttachments()
    {
        _attachPromptFiles.Click += async (_, _) => await PickPromptFilesAsync();
        // TextBox's native paste command raises this cancellable event for keyboard,
        // Shift+Insert and context-menu paste. Read one chosen transfer for every command.
        _input.PastingFromClipboard += (_, e) =>
        {
            e.Handled = true;
            _ = PastePromptClipboardAsync();
        };
        Closed += (_, _) =>
        {
            _promptAttachmentClosed = true;
            ClearPromptAttachments();
        };
    }

    private void UpdatePromptAttachmentAvailability()
    {
        var enabled = _operation is null && !HasPromptAttachmentOperation && !_promptAttachmentClosed;
        _attachPromptFiles.IsEnabled = enabled && _promptAttachments.Count < ManagedAiPromptAttachmentReader.MaximumFiles;
        foreach (var row in _promptAttachmentList.Children.OfType<Border>())
            if (row.Child is Grid grid)
                foreach (var button in grid.Children.OfType<Button>()) button.IsEnabled = enabled;
        _send.IsEnabled &= !HasPromptAttachmentOperation;
    }

    private IReadOnlyList<AiChatAttachment> SnapshotPromptAttachments()
        => _promptAttachments.Select(item => item.Attachment).ToArray();

    private void ClearPromptAttachments()
    {
        _promptAttachmentGeneration++;
        _promptAttachmentRead?.Cancel();
        foreach (var item in _promptAttachments) item.Dispose();
        _promptAttachments.Clear();
        _promptAttachmentList.Children.Clear();
        _promptAttachmentList.ColumnDefinitions.Clear();
        if (!_promptAttachmentClosed) UpdateEnabled();
    }

    private sealed record PromptAttachmentRead(CancellationTokenSource Cancellation, CancellationToken Token, int Generation,
        string Draft, int SelectionStart, int SelectionEnd);

    private PromptAttachmentRead? BeginPromptAttachmentRead()
    {
        if (_operation is not null || HasPromptAttachmentOperation || _input.IsReadOnly ||
            IsPromptSkillCompositionActive() || _promptAttachmentClosed || !IsVisible)
            return null;
        var cancellation = new CancellationTokenSource();
        var read = new PromptAttachmentRead(cancellation, cancellation.Token, _promptAttachmentGeneration, _input.Text ?? string.Empty,
            _input.SelectionStart, _input.SelectionEnd);
        _promptAttachmentRead = cancellation;
        ClosePromptSkillPicker();
        UpdateEnabled();
        return read;
    }

    private bool CanApplyPromptAttachmentRead(PromptAttachmentRead read)
        => !_promptAttachmentClosed && IsVisible && _operation is null &&
           read.Generation == _promptAttachmentGeneration && !read.Token.IsCancellationRequested;

    private void EndPromptAttachmentRead(PromptAttachmentRead read)
    {
        if (ReferenceEquals(_promptAttachmentRead, read.Cancellation)) _promptAttachmentRead = null;
        read.Cancellation.Dispose();
        if (!_promptAttachmentClosed) UpdateEnabled();
    }

    private async Task PickPromptFilesAsync()
    {
        var read = BeginPromptAttachmentRead();
        if (read is null) return;
        IReadOnlyList<IStorageFile>? files = null;
        try
        {
            if (!StorageProvider.CanOpen) throw new ManagedAiException(AiFailureCode.AttachmentInvalid);
            files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = L("메시지에 첨부할 파일 선택", "Choose files to attach"), AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(L("이미지 및 텍스트 파일", "Images and text files"))
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.txt", "*.md", "*.markdown", "*.csv", "*.tsv",
                            "*.json", "*.jsonl", "*.log", "*.xml", "*.yaml", "*.yml", "*.cs", "*.py", "*.js", "*.ts",
                            "*.tsx", "*.jsx", "*.html", "*.htm", "*.css", "*.ini", "*.cfg", "*.conf", "*.toml", "*.sql",
                            "*.sh", "*.ps1", "*.bat", "*.cmd", "*.rst", "*.go", "*.rs", "*.c", "*.h", "*.cpp", "*.java",
                            "*.kt", "*.swift", "*.vb", "*.xaml", "*.mjs", "*.cjs", "*.scss", "*.config", "*.psm1", "*.r", "*.hpp"]
                    }
                ]
            });
            if (files.Count == 0 || !CanApplyPromptAttachmentRead(read)) return;
            await ImportPromptAttachmentFilesAsync(files, read);
        }
        catch (OperationCanceledException) { }
        catch (ManagedAiException error) { ShowPromptAttachmentFailure(read, error.Code); }
        catch { ShowPromptAttachmentFailure(read, AiFailureCode.AttachmentInvalid); }
        finally
        {
            if (files is not null) foreach (var file in files) file.Dispose();
            EndPromptAttachmentRead(read);
        }
    }

    private async Task ImportPromptAttachmentFilesAsync(IReadOnlyList<IStorageItem> files, PromptAttachmentRead read)
    {
        if (files.Count + _promptAttachments.Count > ManagedAiPromptAttachmentReader.MaximumFiles)
            throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        var prepared = await ManagedAiPromptAttachmentReader.ReadFilesAsync(files, read.Token);
        AddPromptAttachments(prepared, read);
    }

    private void AddPromptAttachments(IReadOnlyList<ManagedAiPromptAttachment> prepared, PromptAttachmentRead read)
    {
        var added = false;
        try
        {
            if (!CanApplyPromptAttachmentRead(read)) return;
            AiChatAttachmentPolicy.Validate(SnapshotPromptAttachments().Concat(prepared.Select(item => item.Attachment)).ToArray());
            _promptAttachments.AddRange(prepared);
            added = true;
            RefreshPromptAttachmentList();
            _status.Text = prepared.Any(item => item.TextMasked)
                ? L("텍스트 파일에서 개인정보로 보이는 내용을 가렸습니다. 첨부 미리보기에서 전송할 내용을 검토할 수 있습니다.",
                    "Potential personal data was masked in a text file. Review the attachment preview before sending.")
                : L("파일을 첨부했습니다. 보내기를 누르면 메시지와 함께 전송합니다.",
                    "Files attached. They will be sent with your message when you select Send.");
        }
        finally { if (!added) foreach (var item in prepared) item.Dispose(); }
    }

    private async Task PastePromptClipboardAsync()
    {
        var read = BeginPromptAttachmentRead();
        if (read is null) return;
        string? plainText = null;
        IAsyncDataTransfer? data = null;
        var ownsTransfer = false;
        try
        {
            var clipboard = Clipboard;
            if (clipboard is null) return;
            // In-process transfers return the setter's objects. Native Win32 transfers
            // create new bitmaps/storage items, and disposing the transfer releases only
            // its COM reference. Borrow the former and explicitly own the latter.
            data = await clipboard.TryGetInProcessDataAsync();
            if (data is null)
            {
                data = await clipboard.TryGetDataAsync();
                ownsTransfer = true;
            }
            if (data is null || !CanApplyPromptAttachmentRead(read)) return;
            if (data.Contains(DataFormat.File))
            {
                var files = await data.TryGetFilesAsync();
                if (files is null || files.Length == 0) throw new ManagedAiException(AiFailureCode.AttachmentInvalid);
                try { await ImportPromptAttachmentFilesAsync(files, read); }
                finally { if (ownsTransfer) foreach (var file in files) file.Dispose(); }
            }
            else if (data.Formats.Any(format => format.Identifier.Contains("FileGroupDescriptor", StringComparison.OrdinalIgnoreCase) ||
                         format.Identifier.Contains("FileContents", StringComparison.OrdinalIgnoreCase)))
            {
                _status.Text = L("가상 파일은 붙여넣을 수 없습니다. 파일을 저장한 후 파일 첨부로 선택할 수 있습니다.",
                    "Virtual files cannot be pasted. Save the file, then choose Attach files.");
            }
            else if (data.Contains(DataFormat.Bitmap))
            {
                if (_promptAttachments.Count >= ManagedAiPromptAttachmentReader.MaximumFiles)
                    throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
                var bitmap = await data.TryGetBitmapAsync() ?? throw new ManagedAiException(AiFailureCode.AttachmentInvalid);
                try
                {
                    var prepared = await ManagedAiPromptAttachmentReader.ReadBitmapAsync(bitmap, read.Token);
                    AddPromptAttachments([prepared], read);
                }
                finally { if (ownsTransfer) bitmap.Dispose(); }
            }
            else if (data.Contains(DataFormat.Text)) plainText = await data.TryGetTextAsync();
            else _status.Text = L("이 클립보드 형식은 첨부할 수 없습니다. PNG, JPEG 또는 텍스트 파일을 선택할 수 있습니다.",
                "This clipboard format cannot be attached. Choose a PNG, JPEG, or text file.");
        }
        catch (OperationCanceledException) { }
        catch (ManagedAiException error) { ShowPromptAttachmentFailure(read, error.Code); }
        catch { ShowPromptAttachmentFailure(read, AiFailureCode.AttachmentInvalid); }
        finally
        {
            try { if (ownsTransfer) data?.Dispose(); }
            finally { EndPromptAttachmentRead(read); }
        }
        // Restore editing before SelectedText, which honors TextBox.IsReadOnly. Preserve
        // ordinary text insertion, undo history, selection replacement and the privacy filter.
        if (!string.IsNullOrEmpty(plainText) && CanApplyPromptAttachmentRead(read) &&
            (_input.Text ?? string.Empty) == read.Draft && _input.SelectionStart == read.SelectionStart &&
            _input.SelectionEnd == read.SelectionEnd)
        {
            _input.Focus();
            _input.SelectionStart = read.SelectionStart;
            _input.SelectionEnd = read.SelectionEnd;
            _input.SelectedText = plainText;
        }
    }

    private void ShowPromptAttachmentFailure(PromptAttachmentRead read, AiFailureCode code)
    {
        if (!CanApplyPromptAttachmentRead(read)) return;
        _status.Text = code switch
        {
            AiFailureCode.AttachmentLimitExceeded => L(
                "첨부 파일의 개수, 크기 또는 내용이 한도를 초과했습니다. 파일 첨부 버튼의 도움말에서 제한을 확인할 수 있습니다.",
                "The attachments exceed a count, size, or content limit. See Attach files help for the limits."),
            AiFailureCode.AttachmentBlocked => L("인증서 또는 비밀 정보가 포함될 수 있어 파일을 첨부하지 않았습니다.",
                "The file was not attached because it may contain certificate or secret material."),
            _ => L("파일을 첨부하지 못했습니다. PNG, JPEG 또는 지원하는 텍스트 파일을 선택할 수 있습니다.",
                "Could not attach the file. Choose a PNG, JPEG, or supported text file.")
        };
    }

    private void RefreshPromptAttachmentList()
    {
        _promptAttachmentList.Children.Clear();
        _promptAttachmentList.ColumnDefinitions.Clear();
        for (var index = 0; index < _promptAttachments.Count; index++)
            _promptAttachmentList.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        for (var index = 0; index < _promptAttachments.Count; index++)
        {
            var item = _promptAttachments[index];
            var attachment = item.Attachment;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4,
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = 3
            };
            var fileName = new TextBlock
            {
                Text = attachment.FileName, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetAutomationId(fileName, $"ManagedAiAttachmentName{index + 1}");
            ToolTip.SetTip(fileName, attachment.FileName);
            grid.Children.Add(fileName);
            var body = new Grid { ColumnDefinitions = new ColumnDefinitions(item.Preview is null ? "*" : "40,*"), ColumnSpacing = 4 };
            if (item.Preview is { } preview)
            {
                var image = new Image { Source = preview, Width = 40, Height = 40, Stretch = Stretch.Uniform };
                AutomationProperties.SetName(image, L($"{attachment.FileName} 이미지 미리보기", $"Image preview for {attachment.FileName}"));
                body.Children.Add(image);
            }
            var details = new StackPanel { Spacing = 2 };
            var mediaType = new TextBlock
            {
                Text = attachment.MediaType, FontSize = 11, TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var size = new TextBlock { Text = $"{attachment.Length / 1024d:0.#} KiB", FontSize = 11 };
            ToolTip.SetTip(details, $"{attachment.MediaType} | {size.Text}");
            details.Children.Add(mediaType);
            details.Children.Add(size);
            if (item.Preview is not null) Grid.SetColumn(details, 1);
            body.Children.Add(details);
            Grid.SetRow(body, 1); Grid.SetColumnSpan(body, 2); grid.Children.Add(body);
            if (attachment.Text is { } text)
            {
                var previewLabel = item.TextMasked ? L("가림 처리한 텍스트 미리보기", "Preview masked text") : L("텍스트 미리보기", "Preview text");
                // The application theme supplies Button/TextBox templates, but no Expander
                // template. Keep the disclosure explicit so both its label and state render.
                var previewCaption = new TextBlock
                {
                    Text = previewLabel, FontSize = 12, TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                var textPreview = new Button
                {
                    Content = previewCaption, Padding = new Thickness(4, 2), MinHeight = 24,
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                var textContent = new TextBox
                {
                    Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                    Height = 48, FontSize = 12, Padding = new Thickness(4), IsVisible = false
                };
                AutomationProperties.SetAutomationId(textPreview, $"ManagedAiAttachmentTextPreview{index + 1}");
                AutomationProperties.SetAutomationId(textContent, $"ManagedAiAttachmentTextContent{index + 1}");
                AutomationProperties.SetName(textPreview, L($"{attachment.FileName} 텍스트 미리보기", $"Text preview for {attachment.FileName}"));
                AutomationProperties.SetName(textContent, L($"{attachment.FileName} 전송할 텍스트", $"Text to send from {attachment.FileName}"));
                ToolTip.SetTip(textPreview, previewLabel);
                textPreview.Click += (_, _) =>
                {
                    textContent.IsVisible = !textContent.IsVisible;
                    previewCaption.Text = textContent.IsVisible ? L("미리보기 닫기", "Hide preview") : previewLabel;
                    AutomationProperties.SetHelpText(textPreview, textContent.IsVisible
                        ? L("텍스트 미리보기를 표시하고 있습니다.", "The text preview is expanded.")
                        : L("텍스트 미리보기를 접었습니다.", "The text preview is collapsed."));
                };
                Grid.SetRow(textPreview, 2); Grid.SetColumnSpan(textPreview, 2); grid.Children.Add(textPreview);
                Grid.SetRow(textContent, 3); Grid.SetColumnSpan(textContent, 2); grid.Children.Add(textContent);
            }
            var remove = new Button
            {
                Width = 24, Height = 24, MinWidth = 24, MinHeight = 24, Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Top
            };
            var removeIcon = new Avalonia.Controls.Shapes.Path
            {
                Width = 10, Height = 10, Stretch = Stretch.Uniform, StrokeThickness = 1.5,
                StrokeLineCap = PenLineCap.Round, Data = Geometry.Parse("M1,1 L9,9 M9,1 L1,9")
            };
            removeIcon.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, remove.GetObservable(Button.ForegroundProperty));
            remove.Content = removeIcon;
            AutomationProperties.SetAutomationId(remove, $"ManagedAiAttachmentRemove{index + 1}");
            AutomationProperties.SetName(remove, L($"{attachment.FileName} 첨부 제거", $"Remove attachment {attachment.FileName}"));
            ToolTip.SetTip(remove, AutomationProperties.GetName(remove));
            remove.Click += (_, _) =>
            {
                if (_operation is not null || HasPromptAttachmentOperation) return;
                _promptAttachments.Remove(item);
                item.Dispose();
                RefreshPromptAttachmentList();
                UpdateEnabled();
            };
            Grid.SetColumn(remove, 1); grid.Children.Add(remove);
            var card = new Border
            {
                Child = grid, Padding = new Thickness(6), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("ControlFillColorDefaultBrush"));
            card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("ControlStrokeColorDefaultBrush"));
            AutomationProperties.SetAutomationId(card, $"ManagedAiAttachmentCard{index + 1}");
            Grid.SetColumn(card, index);
            _promptAttachmentList.Children.Add(card);
        }
        UpdateEnabled();
    }
}
