using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.ManagedAi;

public sealed partial class ManagedAiWindow
{
    private static readonly HashSet<string> PromptSkillReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH", "HOME", "USER", "SHELL", "PWD", "TMPDIR", "TEMP", "TMP", "LANG", "TERM", "XDG_CONFIG_HOME"
    };
    private readonly Button _promptAddSkill = new()
    {
        Content = "+", MinWidth = 36, Padding = new Thickness(8, 4),
        VerticalAlignment = VerticalAlignment.Bottom
    };
    private readonly ComboBox _promptSkills = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        PlaceholderText = L("설치된 스킬 선택", "Choose an installed skill"),
        MaxDropDownHeight = 240
    };
    private readonly TextBlock _promptSkillHint = Text("");
    private readonly StackPanel _promptSkillPicker = new() { Spacing = 4, IsVisible = false };
    private IReadOnlyList<AiSkill> _availablePromptSkills = [];
    private int _ambiguousPromptSkillCount;
    private bool _updatingPromptSkills;
    private bool _promptSkillRefreshQueued;
    private bool _promptSkillManual;
    private string? _promptSkillDraft;
    private int _promptSkillStart;
    private int _promptSkillEnd;
    private string? _dismissedPromptSkillDraft;
    private int _dismissedPromptSkillCaret = -1;
    private int _promptSkillCurrentIndex = -1;
    private int _promptSkillNavigationGeneration;

    private Button CreatePromptSkillButton()
    {
        AutomationProperties.SetAutomationId(_promptAddSkill, "ManagedAiPromptAddSkill");
        AutomationProperties.SetName(_promptAddSkill, L("설치된 스킬 추가", "Add an installed skill"));
        ToolTip.SetTip(_promptAddSkill, L("설치된 스킬을 메시지에 추가합니다. @로도 목록을 열 수 있습니다.",
            "Add an installed skill to your message. Type @ to open the list."));
        return _promptAddSkill;
    }

    private Control CreatePromptSkillPicker()
    {
        AutomationProperties.SetAutomationId(_promptSkillPicker, "ManagedAiPromptSkillPicker");
        AutomationProperties.SetAutomationId(_promptSkills, "ManagedAiPromptSkills");
        AutomationProperties.SetName(_promptSkills, L("메시지에 추가할 설치된 스킬", "Installed skill to add to the message"));
        AutomationProperties.SetAutomationId(_promptSkillHint, "ManagedAiPromptSkillHint");
        // The app's ComboBoxItem theme paints selected and pointer states, but not
        // keyboard focus. Scope the current-candidate paint to this picker only.
        var currentBackground = new Style(selector => selector.OfType<ComboBoxItem>().Class("prompt-skill-current")
            .Template().OfType<Border>().Name("Root"));
        currentBackground.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush")));
        currentBackground.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush")));
        _promptSkills.Styles.Add(currentBackground);
        var currentForeground = new Style(selector => selector.OfType<ComboBoxItem>().Class("prompt-skill-current")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        currentForeground.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("TextOnAccentFillColorPrimaryBrush")));
        _promptSkills.Styles.Add(currentForeground);
        _promptSkills.ItemTemplate = new FuncDataTemplate<AiSkill>((skill, _) =>
        {
            if (skill is null) return new TextBlock();
            var panel = new StackPanel { Spacing = 2, MaxWidth = 500 };
            panel.Children.Add(new TextBlock { Text = skill.Name, FontWeight = FontWeight.SemiBold });
            panel.Children.Add(new TextBlock
            {
                Text = skill.Description, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis
            });
            ToolTip.SetTip(panel, "$" + skill.Name + "\n" + skill.Description);
            return panel;
        });
        _promptSkillPicker.Children.Add(_promptSkills);
        _promptSkillPicker.Children.Add(_promptSkillHint);
        return _promptSkillPicker;
    }

    private void InitializePromptSkillPicker()
    {
        // Preserve the selected replacement range when the user opens the picker with +.
        _input.ClearSelectionOnLostFocus = false;
        _promptAddSkill.Click += (_, _) =>
        {
            if (_operation is not null || _input.IsReadOnly || IsPromptSkillCompositionActive()) return;
            _dismissedPromptSkillDraft = null;
            _promptSkillManual = true;
            _promptSkillDraft = _input.Text ?? string.Empty;
            _promptSkillStart = Math.Min(_input.SelectionStart, _input.SelectionEnd);
            _promptSkillEnd = Math.Max(_input.SelectionStart, _input.SelectionEnd);
            ShowPromptSkills(string.Empty, focusPicker: true);
        };
        _promptAddSkill.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (_promptSkillPicker.IsVisible && e.Key == Key.Escape)
            {
                e.Handled = true;
                ClosePromptSkillPicker(dismiss: true);
                _input.Focus();
            }
        }, RoutingStrategies.Tunnel);
        _promptSkills.DropDownClosed += (_, _) =>
        {
            if (!_updatingPromptSkills && _promptSkillPicker.IsVisible)
                ClosePromptSkillPicker(dismiss: true);
        };
        _promptSkills.ContainerPrepared += (_, e) =>
        {
            if (e.Index == _promptSkillCurrentIndex) e.Container.Classes.Add("prompt-skill-current");
            else e.Container.Classes.Remove("prompt-skill-current");
        };
        _promptSkills.AddHandler(GotFocusEvent, (_, e) =>
        {
            if (!_promptSkillPicker.IsVisible || e.Source is not Visual source) return;
            var item = source as ComboBoxItem ?? source.GetVisualAncestors().OfType<ComboBoxItem>().FirstOrDefault();
            if (item is null) return;
            var index = _promptSkills.IndexFromContainer(item);
            if (index < 0) return;
            _promptSkillCurrentIndex = index;
            UpdatePromptSkillHighlight();
        }, RoutingStrategies.Bubble);
        // TextChanging runs before TextBox updates its caret. Use the asynchronous TextChanged
        // event and coalesce selection notifications until the actual input operation is complete.
        _input.TextChanged += (_, _) => QueuePromptSkillRefresh();
        _input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _promptSkillNavigationGeneration++;
                _promptSkillCurrentIndex = -1;
                UpdatePromptSkillHighlight();
            }
            if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.SelectionStartProperty ||
                e.Property == TextBox.SelectionEndProperty)
                QueuePromptSkillRefresh();
        };
        _input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!_promptSkillPicker.IsVisible || IsPromptSkillCompositionActive()) return;
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ClosePromptSkillPicker(dismiss: true);
            }
            else if (e.Key is Key.Down or Key.Up && e.KeyModifiers == KeyModifiers.None && _promptSkills.ItemCount > 0)
            {
                e.Handled = true;
                MovePromptSkillCurrent(e.Key == Key.Down ? 1 : -1);
            }
            else if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
            {
                e.Handled = true;
                if (CurrentPromptSkill() is { } selected) InsertPromptSkill(selected);
            }
        }, RoutingStrategies.Tunnel);
        _promptSkills.SelectionChanged += (_, _) =>
        {
            if (!_updatingPromptSkills && _promptSkillPicker.IsVisible && _promptSkills.SelectedItem is AiSkill selected)
                InsertPromptSkill(selected);
        };
        _promptSkills.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!_promptSkillPicker.IsVisible || IsPromptSkillCompositionActive()) return;
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ClosePromptSkillPicker(dismiss: true);
                _input.Focus();
            }
            else if (e.Key is Key.Down or Key.Up && e.KeyModifiers == KeyModifiers.None && _promptSkills.ItemCount > 0)
            {
                e.Handled = true;
                MovePromptSkillCurrent(e.Key == Key.Down ? 1 : -1);
            }
            else if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
            {
                e.Handled = true;
                if (CurrentPromptSkill() is { } selected) InsertPromptSkill(selected);
            }
        }, RoutingStrategies.Tunnel);
    }

    private void UpdatePromptSkills(IReadOnlyList<AiSkill> skills)
    {
        // The native mention uses SKILL.md's name, which may differ from its installation ID.
        // Only a single bounded token can be inserted, regardless of imported metadata content.
        var names = skills.Where(skill => skill.Enabled && skill.IsValid && ValidPromptSkillName(skill.Name))
            .GroupBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        _ambiguousPromptSkillCount = names.Where(group => group.Count() > 1).Sum(group => group.Count());
        _availablePromptSkills = names.Where(group => group.Count() == 1).Select(group => group.First())
            .OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (_promptSkillPicker.IsVisible && _operation is null)
        {
            if (_promptSkillManual) ShowPromptSkills(string.Empty, focusPicker: false);
            else RefreshPromptSkillMention();
        }
    }

    private void UpdatePromptSkillAvailability()
    {
        _promptAddSkill.IsEnabled = _operation is null && !_input.IsReadOnly;
        if (!_promptAddSkill.IsEnabled) ClosePromptSkillPicker();
    }

    private void QueuePromptSkillRefresh()
    {
        if (_promptSkillRefreshQueued) return;
        _promptSkillRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _promptSkillRefreshQueued = false;
            RefreshPromptSkillMention();
        }, DispatcherPriority.Background);
    }

    private void RefreshPromptSkillMention()
    {
        if (_operation is not null || _input.IsReadOnly) { ClosePromptSkillPicker(); return; }
        if (IsPromptSkillCompositionActive()) return;
        var text = _input.Text ?? string.Empty;
        var caret = Math.Clamp(_input.CaretIndex, 0, text.Length);
        // A queued notification from opening + does not invalidate its saved selection.
        if (_promptSkillManual && _promptSkillDraft == text) return;
        if (_promptSkillPicker.IsVisible && _promptSkills.IsKeyboardFocusWithin && _promptSkillDraft == text &&
            _input.SelectionStart == _input.SelectionEnd && caret == _promptSkillEnd) return;
        _promptSkillManual = false;
        if (_dismissedPromptSkillDraft == text && _dismissedPromptSkillCaret == caret) return;
        if (!_input.IsFocused || _input.SelectionStart != _input.SelectionEnd)
        { ClosePromptSkillPicker(); return; }
        var start = caret - 1;
        while (start >= 0 && IsPromptSkillQueryCharacter(text[start])) start--;
        if (start < 0 || text[start] != '@' ||
            (start > 0 && !char.IsWhiteSpace(text[start - 1]) && text[start - 1] is not ('(' or '[' or '{' or '"' or '\'')))
        { ClosePromptSkillPicker(); return; }
        _promptSkillDraft = text;
        _promptSkillStart = start;
        _promptSkillEnd = caret;
        ShowPromptSkills(text[(start + 1)..caret], focusPicker: false);
    }

    private void ShowPromptSkills(string query, bool focusPicker)
    {
        _promptSkillNavigationGeneration++;
        _promptSkillCurrentIndex = -1;
        var matches = _availablePromptSkills.Where(skill =>
            skill.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            skill.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        _updatingPromptSkills = true;
        try
        {
            // Opening with no selection keeps ComboBox's popup from moving IME focus.
            _promptSkills.IsDropDownOpen = false;
            _promptSkills.ItemsSource = matches;
            _promptSkills.SelectedIndex = -1;
        }
        finally { _updatingPromptSkills = false; }
        _promptSkillPicker.IsVisible = true;
        _promptSkills.IsVisible = matches.Length > 0;
        _promptSkillHint.Text = matches.Length > 0
            ? L("스킬을 선택하면 $스킬이름을 메시지에 추가합니다. 위쪽 및 아래쪽 화살표로 이동하고 Enter로 선택하거나 Esc로 닫을 수 있습니다.",
                "Choose a skill to add $skill-name to your message. Use Up or Down to move, Enter to choose, or Esc to close.")
            : _availablePromptSkills.Count == 0
                ? _ambiguousPromptSkillCount > 0
                    ? L("이름이 겹치는 스킬을 메시지에서 지정할 수 없습니다. 스킬 설정에서 중복 항목을 비활성화하거나 제거할 수 있습니다.",
                        "Skills with duplicate names cannot be referenced. Disable or remove duplicates in skill settings.")
                    : L("사용할 수 있는 활성 스킬이 없습니다. 스킬 설정에서 설치하거나 활성화할 수 있습니다.",
                        "No enabled skills are available. Install or enable skills in skill settings.")
                : L("입력한 내용과 일치하는 스킬이 없습니다.", "No skills match what you typed.");
        if (_ambiguousPromptSkillCount > 0 && _availablePromptSkills.Count > 0)
            _promptSkillHint.Text += "\n" + L($"이름이 겹치는 스킬 {_ambiguousPromptSkillCount}개는 목록에서 제외했습니다.",
                $"Skipped {_ambiguousPromptSkillCount} skills with duplicate names.");
        if (matches.Length == 0) return;
        if (focusPicker) FocusPromptSkillCurrent(0);
        else _promptSkills.IsDropDownOpen = true;
    }

    private AiSkill? CurrentPromptSkill()
        => _promptSkills.Items.ElementAtOrDefault(_promptSkillCurrentIndex >= 0 ? _promptSkillCurrentIndex : 0) as AiSkill;

    private void MovePromptSkillCurrent(int direction)
    {
        // Clamp at both ends. Navigation never changes SelectedIndex or commits a token.
        var index = _promptSkillCurrentIndex < 0
            ? direction > 0 ? 0 : _promptSkills.ItemCount - 1
            : Math.Clamp(_promptSkillCurrentIndex + direction, 0, _promptSkills.ItemCount - 1);
        FocusPromptSkillCurrent(index);
    }

    private void FocusPromptSkillCurrent(int index)
    {
        _promptSkillCurrentIndex = index;
        var generation = ++_promptSkillNavigationGeneration;
        _promptSkills.Focus();
        _promptSkills.IsDropDownOpen = true;
        _promptSkills.ScrollIntoView(index);
        UpdatePromptSkillHighlight();
        FocusContainer(1);

        void FocusContainer(int attempts)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (generation != _promptSkillNavigationGeneration || !_promptSkillPicker.IsVisible ||
                    !_promptSkills.IsDropDownOpen || index != _promptSkillCurrentIndex ||
                    _promptSkillDraft != (_input.Text ?? string.Empty) || _input.IsReadOnly || IsPromptSkillCompositionActive()) return;
                _promptSkills.ScrollIntoView(index);
                UpdatePromptSkillHighlight();
                if (_promptSkills.ContainerFromIndex(index) is { } container)
                {
                    container.Focus(NavigationMethod.Directional);
                    container.BringIntoView();
                }
                else if (attempts > 0) FocusContainer(attempts - 1);
            }, DispatcherPriority.Loaded);
        }
    }

    private void UpdatePromptSkillHighlight()
    {
        foreach (var container in _promptSkills.GetRealizedContainers())
        {
            var current = _promptSkills.IndexFromContainer(container) == _promptSkillCurrentIndex;
            if (current) container.Classes.Add("prompt-skill-current");
            else container.Classes.Remove("prompt-skill-current");
        }
    }

    private void InsertPromptSkill(AiSkill skill)
    {
        var text = _input.Text ?? string.Empty;
        if (_operation is not null || _input.IsReadOnly || IsPromptSkillCompositionActive() ||
            _promptSkillDraft != text || _promptSkillStart < 0 || _promptSkillEnd < _promptSkillStart ||
            _promptSkillEnd > text.Length || !_availablePromptSkills.Contains(skill)) return;
        var token = "$" + skill.Name;
        if (_promptSkillStart > 0 && !char.IsWhiteSpace(text[_promptSkillStart - 1])) token = " " + token;
        if (_promptSkillEnd == text.Length || !char.IsWhiteSpace(text[_promptSkillEnd])) token += " ";
        if (_input.MaxLength > 0 && text.Length - (_promptSkillEnd - _promptSkillStart) + token.Length > _input.MaxLength)
        {
            _promptSkillHint.Text = L("메시지 길이 제한으로 스킬을 추가하지 못했습니다. 내용을 줄인 후 다시 선택할 수 있습니다.",
                "The skill would exceed the message limit. Shorten the message and choose again.");
            _status.Text = _promptSkillHint.Text;
            return;
        }
        var start = _promptSkillStart;
        var end = _promptSkillEnd;
        ClosePromptSkillPicker();
        _input.Focus();
        // SelectedText preserves TextBox undo/redo and replaces only the saved draft range.
        _input.SelectionStart = start;
        _input.SelectionEnd = end;
        _input.SelectedText = token;
        _input.CaretIndex = start + token.Length;
    }

    private void ClosePromptSkillPicker(bool dismiss = false)
    {
        _promptSkillNavigationGeneration++;
        _promptSkillCurrentIndex = -1;
        UpdatePromptSkillHighlight();
        if (dismiss)
        {
            _dismissedPromptSkillDraft = _input.Text ?? string.Empty;
            _dismissedPromptSkillCaret = _input.CaretIndex;
        }
        _promptSkillPicker.IsVisible = false;
        _promptSkills.IsDropDownOpen = false;
        _promptSkillManual = false;
        _promptSkillDraft = null;
        _updatingPromptSkills = true;
        try { _promptSkills.SelectedIndex = -1; }
        finally { _updatingPromptSkills = false; }
    }

    private bool IsPromptSkillCompositionActive() => _input.GetVisualDescendants()
        .OfType<TextPresenter>().Any(presenter => !string.IsNullOrEmpty(presenter.PreeditText));

    private static bool IsPromptSkillQueryCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '-' or '_' or '.';

    private static bool ValidPromptSkillName(string name)
        => name.Length is > 0 and <= 128 && char.IsAsciiLetterOrDigit(name[0]) &&
           !PromptSkillReservedNames.Contains(name) &&
           name.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_');
}
