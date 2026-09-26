using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;
using TableCloth.Models;
using TableCloth.Theme.Controls;

namespace TableCloth.ManagedAi;

public sealed class ManagedAiWindow : Window
{
    private static readonly (string Korean, string English)[] StarterBankExamples =
        [("KB국민은행", "KB Kookmin Bank"), ("신한은행", "Shinhan Bank"),
         ("하나은행", "Hana Bank"), ("우리은행", "Woori Bank"), ("NH농협은행", "NH NongHyup Bank")];

    private readonly ManagedAiChatSession _session;
    private readonly IManagedRuntimeManager _runtimes;
    private readonly IProviderAuthentication _authentication;
    private readonly IAppMessageBox _messages;
    private readonly IManagedAiModelCatalog _modelCatalog;
    private readonly IManagedAiSkillManager _skills;
    private readonly IManagedAiCertificateBridge _certificateBridge;
    private readonly IManagedAiSandboxBridge _sandboxBridge;
    private readonly IPreferencesManager _preferences;
    private Task _modelSaveTask = Task.CompletedTask;
    private string? _preferredModelId;
    private bool _preferencesLoaded;
    private bool _updatingModelList;
    private readonly TextBox _input = new()
    {
        Watermark = L("메시지를 입력합니다. Shift+Enter로 전송하고 Enter로 줄을 바꿉니다.",
            "Type a message. Press Shift+Enter to send and Enter for a new line."),
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000, MinHeight = 78, MaxHeight = 180
    };
    private readonly TextBlock _status = Text(L("웹 링크를 누르면 Windows Sandbox 또는 현재 브라우저를 선택할 수 있습니다.",
        "Select Windows Sandbox or your current browser when opening a web link."));
    private readonly TextBlock _account = Text(L("로그인 상태를 확인하고 있습니다.", "Checking sign-in status."));
    private readonly StackPanel _transcript = new() { Spacing = 16, Margin = new Thickness(20) };
    private readonly ScrollViewer _scroll;
    private readonly List<Control> _actions = [];
    private readonly Button _manage = new() { Content = L("OpenAI 계정 / 설정", "OpenAI account / settings") };
    private readonly Grid _settingsOverlay = new() { IsVisible = false, ZIndex = 100 };
    private readonly Border _settingsPanel = new() { Width = 350, Padding = new Thickness(16), CornerRadius = new CornerRadius(10),
        BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 100, 0, 0) };
    private readonly Button _signIn;
    private readonly Button _signOut;
    private readonly Button _reconnect;
    private readonly StackPanel _starters = new() { Spacing = 8, MaxWidth = 690, HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 0, 48, 0) };
    private readonly List<Button> _starterButtons = [];
    private readonly Button _connect = new() { Content = L("연결 확인 중", "Checking connection"), IsEnabled = false };
    private readonly ComboBox _models = new() { MinWidth = 190, MaxWidth = 310,
        PlaceholderText = L("연결 후 모델을 선택할 수 있습니다.", "Connect to choose a model.") };
    private readonly TextBlock _modelHint = Text(L("연결하면 대화 모델을 자동으로 불러옵니다.", "Chat models load automatically after connecting."));
    private readonly TextBlock _skillCount = Text(L("활성 스킬을 확인하고 있습니다.", "Checking enabled skills."));
    private readonly TextBlock _preferenceNotice = new() { IsVisible = false, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly StackPanel _skillList = new() { Spacing = 8 };
    private readonly TextBlock _skillStatus = Text(L("전용 스킬을 불러오면 여기에 표시합니다.", "Dedicated skills appear here after loading."));
    private readonly List<Button> _skillActions = [];
    private bool _skillsLoaded;
    private bool _certificateSkillEnabled;
    private bool _sandboxSkillEnabled;
    private bool _skillRefreshInProgress;
    private readonly ProgressRing _busyBar = new() { IsIndeterminate = true, Width = 24, Height = 24,
        HorizontalAlignment = HorizontalAlignment.Left, IsVisible = false, IsActive = false };
    private readonly TextBlock _elapsed = new() { FontSize = 12, IsVisible = false };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _watch = new();
    private Border? _pending;
    private ProgressRing? _pendingRing;
    private TextBlock? _pendingStatus;
    private bool _runtimeInstalled;
    private bool _loggedIn;
    private bool _statusKnown;
    private readonly Button _send = new() { Content = L("보내기", "Send"), MinWidth = 86, IsEnabled = false };
    private readonly Button _cancel = new() { Content = L("중지", "Stop"), IsVisible = false };
    private readonly StackPanel _loginPanel = new() { Spacing = 8, Margin = new Thickness(0, 10), IsVisible = false };
    private readonly SelectableTextBlock _loginCode = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly Button _loginLink = new() { Content = L("OpenAI 로그인 페이지 열기", "Open OpenAI sign-in page") };
    private CancellationTokenSource? _operation;
    private Uri? _loginUri;
    private bool _closing;

    public ManagedAiWindow(ManagedAiChatSession session, IManagedRuntimeManager runtimes,
        IProviderAuthentication authentication, IAppMessageBox messages, IManagedAiModelCatalog modelCatalog,
        IManagedAiSkillManager skills, IPreferencesManager preferences,
        IManagedAiCertificateBridge? certificateBridge = null, IManagedAiSandboxBridge? sandboxBridge = null)
    {
        _session = session; _runtimes = runtimes; _authentication = authentication; _messages = messages; _modelCatalog = modelCatalog;
        _skills = skills; _preferences = preferences;
        _certificateBridge = certificateBridge ?? new ManagedAiCertificateBridge(new JsonlProcessRunner());
        _sandboxBridge = sandboxBridge ?? new ManagedAiSandboxBridge(new SandboxCliProcessRunner());
        Title = L("식탁보 AI (Preview)", "TableCloth AI (Preview)");
        Width = 900; Height = 780; MinWidth = 640; MinHeight = 540;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 8 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock { Text = Title, FontSize = 24, FontWeight = FontWeight.SemiBold });
        title.Children.Add(_account);
        heading.Children.Add(title);
        var clear = ActionButton(L("새 대화", "New chat"), _ =>
        { _session.Clear(); _transcript.Children.Clear(); AddWelcome(); return Task.CompletedTask; });
        AutomationProperties.SetAutomationId(clear, "ManagedAiNewChat");
        Grid.SetColumn(clear, 1); heading.Children.Add(clear);
        header.Children.Add(heading);
        var connection = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        var modelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modelRow.Children.Add(new TextBlock { Text = L("대화 모델", "Chat model"), VerticalAlignment = VerticalAlignment.Center });
        modelRow.Children.Add(_models);
        connection.Children.Add(modelRow);
        ToolTip.SetTip(_manage, L("로그인, Codex 런타임과 전용 스킬 설정을 엽니다.", "Open sign-in, Codex runtime, and dedicated skill settings."));
        _actions.Add(_manage);
        Grid.SetColumn(_manage, 1); connection.Children.Add(_manage);
        header.Children.Add(connection);
        header.Children.Add(_modelHint);
        ToolTip.SetTip(_skillCount, L("활성 스킬은 대화에 제공됩니다. 각 응답에서 실제로 사용한 스킬 수를 뜻하지는 않습니다.",
            "Enabled skills are available to the chat. This is not the number used in each response."));
        header.Children.Add(_skillCount);
        header.Children.Add(_preferenceNotice);
        _connect.Classes.Add("accent");
        _connect.Click += async (_, _) => await RunAsync(ConnectAsync);
        header.Children.Add(_connect);
        _loginPanel.Children.Add(Text(L("브라우저에서 로그인을 마치면 자동으로 대화 준비를 완료합니다. 기기 코드가 표시되면 로그인 페이지에 입력합니다.",
            "Complete sign-in in your browser to connect automatically. If a device code appears, enter it on the sign-in page.")));
        _loginPanel.Children.Add(_loginCode); _loginPanel.Children.Add(_loginLink);
        _loginLink.Click += (_, _) => OpenLoginPage();
        header.Children.Add(_loginPanel);
        root.Children.Add(header);
        _scroll = new ScrollViewer { Content = _transcript, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(_scroll, 1); root.Children.Add(_scroll);
        var composer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        composer.Children.Add(_busyBar);
        composer.Children.Add(_status);
        composer.Children.Add(_elapsed);
        var editor = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        editor.Children.Add(_input);
        var sendButtons = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        _send.Classes.Add("accent");
        sendButtons.Children.Add(_send); sendButtons.Children.Add(_cancel);
        Grid.SetColumn(sendButtons, 1); editor.Children.Add(sendButtons);
        composer.Children.Add(editor);
        composer.Children.Add(Text(L("대화는 이 창의 메모리에만 보관합니다. 전송 시 OpenAI 구독 사용량을 사용하며 AI 응답의 정확성을 보장하지 않습니다.",
            "Chats remain only in this window's memory. Sending uses your OpenAI subscription, and AI responses may be inaccurate.")));
        Grid.SetRow(composer, 2); root.Children.Add(composer);
        // The application's theme has no MenuFlyoutPresenter/MenuItem template.
        // Use the same in-window panel approach as AboutWindow with themed native buttons.
        var dismiss = new Border { Background = Brushes.Transparent };
        dismiss.PointerPressed += (_, e) => { CloseSettings(); e.Handled = true; };
        _settingsOverlay.Children.Add(dismiss);
        var settings = new StackPanel { Spacing = 10 };
        var settingsHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        settingsHeading.Children.Add(new TextBlock { Text = L("OpenAI 계정", "OpenAI account"), FontSize = 18, FontWeight = FontWeight.SemiBold });
        var closeSettings = new Button { Content = L("닫기", "Close"), Padding = new Thickness(8, 3) };
        closeSettings.Click += (_, _) => CloseSettings();
        AutomationProperties.SetAutomationId(closeSettings, "ManagedAiSettingsClose");
        Grid.SetColumn(closeSettings, 1); settingsHeading.Children.Add(closeSettings);
        settings.Children.Add(settingsHeading);
        var accountStatus = Text("");
        accountStatus.Bind(TextBlock.TextProperty, _account.GetObservable(TextBlock.TextProperty));
        settings.Children.Add(accountStatus);
        settings.Children.Add(Text(L("식탁보에서 사용하는 ChatGPT 로그인을 관리합니다.", "Manage the ChatGPT sign-in used by TableCloth.")));
        _signIn = SettingsButton(L("OpenAI 로그인", "Sign in to OpenAI"), "ManagedAiSignIn", ConnectAsync);
        _signOut = SettingsButton(L("로그아웃", "Sign out"), "ManagedAiSignOut", LogoutAsync);
        _reconnect = SettingsButton(L("로그아웃 후 다시 로그인", "Sign out and sign in again"), "ManagedAiReconnect", async token =>
        { await LogoutAsync(token); token.ThrowIfCancellationRequested(); await ConnectAsync(token); });
        settings.Children.Add(_signIn); settings.Children.Add(_signOut); settings.Children.Add(_reconnect);
        settings.Children.Add(new TextBlock { Text = L("런타임 및 연결 설정", "Runtime and connection"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        settings.Children.Add(SettingsButton(L("연결 및 모델 목록 새로 고침", "Refresh connection and models"), "ManagedAiRefresh", RefreshStatusAsync));
        settings.Children.Add(SettingsButton(L("Codex 설치 / 업데이트", "Install / update Codex"), "ManagedAiUpdate", InstallAsync));
        settings.Children.Add(SettingsButton(L("기기 코드로 로그인", "Sign in with device code"), "ManagedAiDeviceLogin", async token =>
        { if (await EnsureRuntimeAsync(token)) await LoginAsync(AiLoginMethod.DeviceCode, token); }));
        settings.Children.Add(SettingsButton(L("이전 런타임 버전 복원", "Restore previous runtime"), "ManagedAiRollback", async token =>
        { await _runtimes.RollbackAsync(token); _skillsLoaded = false; await RefreshStatusAsync(token); }));
        settings.Children.Add(new TextBlock { Text = L("전용 스킬", "Dedicated skills"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        settings.Children.Add(Text(L("이 백엔드의 전용 폴더에 있는 활성 스킬을 대화에 제공합니다. 런타임을 다시 설치해도 스킬과 사용 설정을 보존합니다.",
            "Enabled skills in this backend's dedicated folder are available to chats. Reinstalling the runtime preserves the skills and their enabled states.")));
        settings.Children.Add(SettingsButton(L("폴더에서 스킬 가져오기", "Import skill from folder"), "ManagedAiSkillImport", ImportSkillAsync, keepSettingsOpen: true));
        settings.Children.Add(SettingsButton(L("스킬 다시 불러오기", "Reload skills"), "ManagedAiSkillRefresh", RefreshSkillsAsync, keepSettingsOpen: true));
        settings.Children.Add(SettingsButton(L("전용 스킬 폴더 열기", "Open dedicated skill folder"), "ManagedAiSkillFolder", OpenSkillFolderAsync, keepSettingsOpen: true));
        settings.Children.Add(_skillStatus);
        settings.Children.Add(_skillList);
        _settingsPanel.Child = new ScrollViewer { Content = settings, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _settingsPanel.Bind(Border.BackgroundProperty, _settingsPanel.GetResourceObservable("SolidBackgroundFillColorBaseBrush"));
        _settingsPanel.Bind(Border.BorderBrushProperty, _settingsPanel.GetResourceObservable("ControlStrokeColorDefaultBrush"));
        _settingsOverlay.Children.Add(_settingsPanel);
        KeyboardNavigation.SetTabNavigation(_settingsOverlay, KeyboardNavigationMode.Cycle);
        _manage.Click += async (_, _) =>
        {
            _settingsOverlay.IsVisible = !_settingsOverlay.IsVisible;
            if (_settingsOverlay.IsVisible)
            {
                (_loggedIn ? _signOut : _signIn).Focus();
                if (!_skillsLoaded) await RunAsync(RefreshSkillsAsync, keepSettingsOpen: true);
            }
        };
        Grid.SetRowSpan(_settingsOverlay, 3); root.Children.Add(_settingsOverlay);
        SizeChanged += (_, e) => _settingsPanel.MaxHeight = Math.Max(180, e.NewSize.Height - 160);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && _settingsOverlay.IsVisible) { CloseSettings(); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Content = root;
        AutomationProperties.SetAutomationId(_input, "ManagedAiChatInput");
        AutomationProperties.SetAutomationId(_send, "ManagedAiChatSend");
        AutomationProperties.SetAutomationId(_transcript, "ManagedAiChatTranscript");
        AutomationProperties.SetAutomationId(_connect, "ManagedAiConnect");
        AutomationProperties.SetAutomationId(_models, "ManagedAiModels");
        AutomationProperties.SetAutomationId(_modelHint, "ManagedAiModelHint");
        AutomationProperties.SetAutomationId(_skillCount, "ManagedAiActiveSkillCount");
        AutomationProperties.SetAutomationId(_preferenceNotice, "ManagedAiPreferenceNotice");
        AutomationProperties.SetAutomationId(_skillStatus, "ManagedAiSkillStatus");
        AutomationProperties.SetAutomationId(_skillList, "ManagedAiSkillList");
        AutomationProperties.SetAutomationId(_cancel, "ManagedAiCancel");
        AutomationProperties.SetAutomationId(_manage, "ManagedAiManage");
        AutomationProperties.SetAutomationId(_settingsOverlay, "ManagedAiSettings");
        AutomationProperties.SetAutomationId(_settingsPanel, "ManagedAiSettingsPanel");
        AutomationProperties.SetAutomationId(_starters, "ManagedAiStarters");
        AutomationProperties.SetAutomationId(_loginPanel, "ManagedAiLoginInstructions");
        AutomationProperties.SetAutomationId(_status, "ManagedAiStatus");
        _models.SelectionChanged += (_, _) =>
        {
            UpdateModelHint();
            if (!_updatingModelList && _models.SelectedItem is AiModel model) RememberModel(model);
            UpdateEnabled();
        };
        _timer.Tick += (_, _) => _elapsed.Text = L($"{(int)_watch.Elapsed.TotalSeconds}초 경과",
            $"{(int)_watch.Elapsed.TotalSeconds}s elapsed");
        _input.TextChanged += (_, _) => UpdateEnabled();
        _send.Click += async (_, _) => await SendAsync();
        _input.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Shift)
            { e.Handled = true; await SendAsync(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _cancel.Click += (_, _) => _operation?.Cancel();
        Opened += async (_, _) => await RunAsync(async token =>
        {
            await RefreshSkillsAsync(token);
            await RefreshStatusAsync(token);
        });
        Activated += async (_, _) =>
        {
            if (!_statusKnown || _operation is not null || _closing || _skillRefreshInProgress) return;
            _skillRefreshInProgress = true;
            try { await RefreshSkillsAsync(CancellationToken.None); }
            catch { _skillCount.Text = L("활성 스킬 수를 확인하지 못했습니다.", "Could not check the number of enabled skills."); }
            finally { _skillRefreshInProgress = false; }
        };
        Closing += async (_, e) =>
        {
            if (_operation is not null) { e.Cancel = true; _closing = true; _operation.Cancel(); }
            else if (!_modelSaveTask.IsCompleted)
            {
                e.Cancel = true; _closing = true;
                _models.IsEnabled = false;
                await _modelSaveTask;
                Close();
            }
            else { _timer.Stop(); _session.Clear(); _loginUri = null; _loginCode.Text = string.Empty; }
        };
        AddWelcome();
    }

    private void CloseSettings()
    {
        _settingsOverlay.IsVisible = false;
        _manage.Focus();
    }

    private Button SettingsButton(string label, string id, Func<CancellationToken, Task> action, bool keepSettingsOpen = false)
    {
        var button = ActionButton(label, action, keepSettingsOpen);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0);
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }

    private Button ActionButton(string label, Func<CancellationToken, Task> action, bool keepSettingsOpen = false)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 6) };
        button.Click += async (_, _) => await RunAsync(action, keepSettingsOpen: keepSettingsOpen);
        _actions.Add(button); return button;
    }

    private async Task ImportSkillAsync(CancellationToken token)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = L("SKILL.md가 들어 있는 스킬 폴더 선택", "Select a skill folder containing SKILL.md"),
            AllowMultiple = false
        });
        token.ThrowIfCancellationRequested();
        var source = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(source))
        {
            _skillStatus.Text = L("스킬 가져오기를 취소했습니다.", "Skill import canceled.");
            return;
        }
        _skillStatus.Text = L("스킬 폴더를 검사하고 전용 저장소로 복사하고 있습니다.", "Checking the skill folder and copying it to dedicated storage.");
        RenderSkills(await _skills.ImportAsync(source, token));
        _skillsLoaded = true;
        _skillStatus.Text = L("스킬을 가져왔습니다. 다음 메시지부터 사용할 수 있습니다.", "Skill imported. It is available from the next message.");
    }

    private async Task RefreshSkillsAsync(CancellationToken token)
    {
        _skillStatus.Text = L("전용 스킬과 격리 설정을 확인하고 있습니다.", "Checking dedicated skills and isolation settings.");
        var skills = await _skills.ListAsync(token);
        RenderSkills(skills);
        _skillsLoaded = true;
        _skillStatus.Text = skills.Count == 0
            ? L("가져온 전용 스킬이 없습니다.", "No dedicated skills are installed.")
            : L($"전용 스킬 {skills.Count}개를 불러왔습니다.", $"Loaded {skills.Count} dedicated skills.");
    }

    private Task OpenSkillFolderAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(_skills.StorageDirectory);
            Process.Start(new ProcessStartInfo(_skills.StorageDirectory) { UseShellExecute = true })?.Dispose();
            _skillStatus.Text = L("전용 스킬 폴더를 열었습니다. 폴더를 수정한 뒤 스킬 다시 불러오기를 선택할 수 있습니다.",
                "Opened the dedicated skill folder. After editing it, choose Reload skills.");
            return Task.CompletedTask;
        }
        catch { throw new ManagedAiException(AiFailureCode.SkillInvalid); }
    }

    private void RenderSkills(IReadOnlyList<AiSkill> skills)
    {
        _certificateSkillEnabled = skills.Any(skill => skill.Id == "tablecloth-certificate-expiry" && skill.Enabled);
        _sandboxSkillEnabled = skills.Any(skill => skill.Id == "tablecloth-windows-sandbox" && skill.Enabled);
        _skillCount.Text = L($"활성 스킬 {skills.Count(skill => skill.Enabled)}개 (보유 {skills.Count}개)",
            $"{skills.Count(skill => skill.Enabled)} enabled skills ({skills.Count} installed)");
        foreach (var action in _skillActions) _actions.Remove(action);
        _skillActions.Clear();
        _skillList.Children.Clear();
        foreach (var skill in skills)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10 };
            var summary = new StackPanel { Spacing = 2 };
            summary.Children.Add(new TextBlock { Text = skill.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            summary.Children.Add(Text(skill.Description));
            row.Children.Add(summary);
            var toggle = new Button { Content = skill.Enabled ? L("사용 중", "Enabled") : L("사용 안 함", "Disabled"), MinWidth = 86,
                VerticalAlignment = VerticalAlignment.Center };
            if (skill.Enabled) toggle.Classes.Add("accent");
            AutomationProperties.SetAutomationId(toggle, "ManagedAiSkillToggle_" + skill.Id);
            toggle.Click += async (_, _) => await RunAsync(async token =>
            {
                var updated = await _skills.SetEnabledAsync(skill.Id, !skill.Enabled, token);
                RenderSkills(updated);
                _skillStatus.Text = !skill.Enabled
                    ? L($"{skill.Name} 스킬을 사용하도록 설정했습니다.", $"Enabled the {skill.Name} skill.")
                    : L($"{skill.Name} 스킬을 사용하지 않도록 설정했습니다.", $"Disabled the {skill.Name} skill.");
            }, keepSettingsOpen: true);
            Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
            var remove = new Button { Content = L("제거", "Remove"), MinWidth = 58, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(remove, "ManagedAiSkillRemove_" + skill.Id);
            remove.Click += async (_, _) =>
            {
                if (_messages.DisplayQuestion(L($"{skill.Name} 스킬과 전용 폴더의 파일을 삭제하시겠습니까?",
                    $"Delete the {skill.Name} skill and its files from the dedicated folder?"),
                    AppMessageBoxButton.YesNo, AppMessageBoxResult.No) != AppMessageBoxResult.Yes) return;
                await RunAsync(async token =>
                {
                    RenderSkills(await _skills.RemoveAsync(skill.Id, token));
                    _skillStatus.Text = L($"{skill.Name} 스킬을 제거했습니다.", $"Removed the {skill.Name} skill.");
                }, keepSettingsOpen: true);
            };
            Grid.SetColumn(remove, 2); row.Children.Add(remove);
            _skillList.Children.Add(row);
            _skillActions.Add(toggle); _actions.Add(toggle);
            _skillActions.Add(remove); _actions.Add(remove);
        }
    }

    private void AddWelcome()
    {
        _starters.Children.Clear(); _starterButtons.Clear(); _starters.IsVisible = true;
        _starters.Children.Add(new TextBlock { Text = L("이런 질문으로 시작할 수 있습니다", "Try one of these questions"), FontSize = 16, FontWeight = FontWeight.SemiBold });
        _starters.Children.Add(Text(L("카드를 누르면 예시 질문이 입력됩니다. 내용을 수정한 뒤 전송할 수 있습니다.",
            "Choose a card to fill in a sample question. You can edit it before sending.")));
        var choices = new UniformGrid { Columns = 2 };
        var bank = StarterBankExamples[Random.Shared.Next(StarterBankExamples.Length)];
        AddStarter(L("서비스 찾기", "Find a service"),
            L($"{bank.Korean} 인터넷뱅킹 공식 사이트를 찾아 링크와 이용 준비 사항을 알려 주세요.",
              $"Find the official online banking website for {bank.English}, its link, and how to prepare to use it."),
            L($"찾으려는 서비스 이름을 말씀해 주세요. 예: {bank.Korean}",
              $"Tell me the name of the service you want to find. For example: {bank.English}"));
        AddStarter(L("공공서비스 찾기", "Find a public service"),
            L("주민등록등본을 온라인으로 발급받을 수 있는 공식 사이트와 이용 절차를 알려 주세요.",
              "Find the official website for getting a Korean resident registration certificate online and explain the steps."));
        AddStarter(L("세금 신고 사이트 찾기", "Find a tax filing site"),
            L("세금 신고에 사용할 공식 사이트를 찾아 링크를 알려 주세요.", "Find the official tax filing website and provide its link."));
        AddStarter(L("식탁보 사용법", "Using TableCloth"),
            L("식탁보에서 웹사이트를 열고 필요한 소프트웨어를 설치하는 방법을 알려 주세요.",
              "Explain how to open a website in TableCloth and install its required software."));
        AddStarter(L("인증서 만료 확인", "Check certificate expiry"),
            L("이 컴퓨터에서 30일 안에 만료되는 공동인증서가 있는지 확인해 주세요.",
              "Check whether any digital certificates on this computer expire within 30 days."));
        _starters.Children.Add(choices);
        _transcript.Children.Add(_starters);
        Dispatcher.UIThread.Post(() => _scroll.ScrollToHome(), DispatcherPriority.Loaded);

        void AddStarter(string title, string prompt, string? description = null)
        {
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(Text(description ?? prompt));
            var button = new Button { Content = content, Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            button.Classes.Add("chat-starter");
            AutomationProperties.SetName(button, title + ". " + (description ?? prompt));
            button.Click += (_, _) =>
            {
                if (_operation is not null) return;
                var draft = string.IsNullOrWhiteSpace(_input.Text) ? prompt : _input.Text.TrimEnd() + "\n\n" + prompt;
                if (draft.Length > _input.MaxLength) { _status.Text = L("입력란의 글자 수를 줄이면 추천 문장을 추가할 수 있습니다.",
                    "Shorten your message before adding this suggestion."); return; }
                _input.Text = draft; _input.CaretIndex = draft.Length; _input.Focus();
                _status.Text = L("추천 문장을 추가했습니다. 내용을 수정한 뒤 Shift+Enter로 전송할 수 있습니다.",
                    "Suggestion added. Edit it and press Shift+Enter to send.");
            };
            _starterButtons.Add(button); choices.Children.Add(button);
        }
    }

    private void AddMessage(string author, string content, bool user = false)
    {
        var body = new StackPanel { Spacing = 8 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        heading.Children.Add(new TextBlock { Text = author, FontWeight = FontWeight.SemiBold, FontSize = 12,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button { Content = L("복사", "Copy"), Padding = new Thickness(10, 3), MinHeight = 28, FontSize = 12 };
        copy.Classes.Add("chat-copy");
        AutomationProperties.SetName(copy, L($"{author} 메시지 복사", $"Copy {author} message"));
        ToolTip.SetTip(copy, L("마크다운 원문을 복사합니다.", "Copy the original Markdown."));
        copy.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is not { } clipboard) throw new InvalidOperationException();
                await clipboard.SetTextAsync(content);
                copy.Content = L("복사 완료", "Copied");
                await Task.Delay(1600);
                copy.Content = L("복사", "Copy");
            }
            catch { copy.Content = L("다시 복사", "Retry copy"); _status.Text = L("클립보드에 복사하지 못했습니다. 다시 시도할 수 있습니다.",
                "Could not copy to the clipboard. Try again."); }
        };
        Grid.SetColumn(copy, 1); heading.Children.Add(copy);
        body.Children.Add(heading);
        body.Children.Add(new ChatMarkdownView(content, async link =>
            await RunAsync(token => _session.OpenLinkAsync(link, token))));
        var bubble = new Border
        {
            Child = body, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(12), MaxWidth = 690,
            HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = user ? new Thickness(48, 0, 0, 0) : new Thickness(0, 0, 48, 0), BorderThickness = new Thickness(1)
        };
        bubble.Bind(Border.BackgroundProperty, bubble.GetResourceObservable(user ? "InfoBannerBackground" : "ControlFillColorDefaultBrush"));
        bubble.Bind(Border.BorderBrushProperty, bubble.GetResourceObservable("ControlStrokeColorDefaultBrush"));
        _transcript.Children.Add(bubble);
        while (_transcript.Children.Count > 100) _transcript.Children.RemoveAt(0);
        Dispatcher.UIThread.Post(() => _scroll.ScrollToEnd(), DispatcherPriority.Loaded);
    }

    private async Task SendAsync()
    {
        if (_operation is not null || !_loggedIn || _models.SelectedItem is not AiModel model || string.IsNullOrWhiteSpace(_input.Text)) return;
        var text = _input.Text.Trim();
        var certificateLookup = _certificateSkillEnabled && CertificateExpiryIntent.Matches(text);
        var sandboxRequest = _sandboxSkillEnabled ? WindowsSandboxIntent.Parse(text) : null;
        if (certificateLookup && _messages.DisplayQuestion(
            L("이 컴퓨터의 공동인증서 만료일과 로컬 Catalog 현황을 읽고 인증서 이름과 경로를 제외한 조회 결과를 OpenAI 대화에 전송하시겠습니까?",
              "Read certificate expiry dates and local Catalog status, then send the results without certificate names or paths to the OpenAI chat?"),
            AppMessageBoxButton.YesNo, AppMessageBoxResult.No) != AppMessageBoxResult.Yes)
        { _status.Text = L("인증서 조회를 취소했습니다. 메시지는 입력란에 남아 있습니다.",
            "Certificate scan canceled. Your message remains in the input box."); return; }
        if (sandboxRequest?.Action == SandboxCliAction.Stop && _messages.DisplayQuestion(
            L("Windows Sandbox를 종료하시겠습니까? 해당 Sandbox의 파일과 설치 상태가 사라집니다.",
              "Stop Windows Sandbox? Its files and installed software will be lost."),
            AppMessageBoxButton.YesNo, AppMessageBoxResult.No) != AppMessageBoxResult.Yes)
        { _status.Text = L("Windows Sandbox 종료를 취소했습니다. 메시지는 입력란에 남아 있습니다.",
            "Stopping Windows Sandbox was canceled. Your message remains in the input box."); return; }
        _starters.IsVisible = false;
        AddMessage(L("사용자", "You"), text, user: true);
        _input.Text = string.Empty;
        await RunAsync(async token =>
        {
            AddPending(model);
            string? localReport = null;
            string? localSandboxReport = null;
            if (certificateLookup)
            {
                _status.Text = L("로컬 인증서 만료일을 확인하고 있습니다.", "Checking local certificate expiry dates.");
                if (_pendingStatus is not null) _pendingStatus.Text = _status.Text;
                localReport = await _certificateBridge.GetExpiryReportAsync(token);
            }
            if (sandboxRequest is not null)
            {
                _status.Text = L("Windows Sandbox 명령을 실행하고 있습니다.", "Running the Windows Sandbox command.");
                if (_pendingStatus is not null) _pendingStatus.Text = _status.Text;
                localSandboxReport = await _sandboxBridge.ExecuteAsync(sandboxRequest, token);
            }
            var response = await _session.SendAsync(text, Progress(), token, model.Id, localReport, localSandboxReport);
            RemovePending();
            AddMessage($"{L("식탁보", "TableCloth")} / {response.Model ?? model.Id}", response.Text);
            _status.Text = response.SearchCalls > 0
                ? L($"응답을 완료했습니다. 웹 검색 {response.SearchCalls}회", $"Response complete. {response.SearchCalls} web searches.")
                : L("응답을 완료했습니다.", "Response complete.");
        }, errorInChat: true);
        _input.Focus();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, bool errorInChat = false, bool keepSettingsOpen = false)
    {
        if (_operation is not null) return;
        if (!keepSettingsOpen) _settingsOverlay.IsVisible = false;
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation; UpdateEnabled();
        _watch.Restart(); _elapsed.Text = L("0초 경과", "0s elapsed"); _elapsed.IsVisible = true; _timer.Start();
        _status.Text = L("요청을 처리하고 있습니다.", "Processing your request.");
        try
        {
            await action(cancellation.Token);
            if (_status.Text == L("요청을 처리하고 있습니다.", "Processing your request."))
                _status.Text = L("요청을 완료했습니다.", "Request complete.");
        }
        catch (OperationCanceledException) { ShowFailure(L("요청을 중지했습니다. 새 메시지를 보내 대화를 계속할 수 있습니다.",
            "Request stopped. Send a new message to continue the chat.")); }
        catch (ManagedAiException ex)
        {
            if (ex.Code is AiFailureCode.AuthenticationRequired or AiFailureCode.RuntimeNotInstalled)
            {
                ResetConnection(); _statusKnown = true;
                if (ex.Code == AiFailureCode.RuntimeNotInstalled) _runtimeInstalled = false;
            }
            ShowFailure(FailureText(ex.Code));
        }
        catch { ShowFailure(L("요청을 완료하지 못했습니다. 연결 상태를 확인한 후 다시 시도할 수 있습니다.",
            "Could not complete the request. Check your connection and try again.")); }
        finally
        {
            _operation = null;
            RemovePending(); _timer.Stop(); _watch.Stop(); _elapsed.IsVisible = false;
            _loginUri = null; _loginCode.Text = string.Empty; _loginPanel.IsVisible = false;
            UpdateEnabled();
            if (_closing) Close();
        }
        void ShowFailure(string message) { _status.Text = message; if (errorInChat) AddMessage(L("안내", "Notice"), message); }
    }

    private void UpdateEnabled()
    {
        foreach (var action in _actions) action.IsEnabled = _operation is null;
        foreach (var starter in _starterButtons) starter.IsEnabled = _operation is null;
        _signIn.IsVisible = !_loggedIn;
        _signIn.Content = _runtimeInstalled ? L("OpenAI 로그인", "Sign in to OpenAI") : L("설치하고 로그인", "Install and sign in");
        _signOut.IsVisible = _loggedIn;
        _reconnect.IsVisible = _loggedIn;
        _connect.IsVisible = !_loggedIn || _models.SelectedItem is null;
        _connect.IsEnabled = _operation is null;
        _connect.Content = !_statusKnown ? L("연결 다시 확인", "Check connection again")
            : !_runtimeInstalled ? L("AI 사용 준비", "Set up AI")
            : !_loggedIn ? L("OpenAI 로그인", "Sign in to OpenAI") : L("모델 목록 다시 불러오기", "Reload models");
        _models.IsEnabled = _operation is null && _loggedIn;
        _send.IsEnabled = _operation is null && _loggedIn && _models.SelectedItem is AiModel && !string.IsNullOrWhiteSpace(_input.Text);
        _cancel.IsVisible = _operation is not null;
        _busyBar.IsVisible = _operation is not null && _pending is null;
        _busyBar.IsActive = _busyBar.IsVisible;
        _input.IsReadOnly = _operation is not null;
    }

    private void AddPending(AiModel model)
    {
        _pendingStatus = Text(L("응답을 준비하고 있습니다.", "Preparing a response."));
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = $"{L("식탁보", "TableCloth")} / {model.Id}", FontWeight = FontWeight.SemiBold, FontSize = 12 });
        var activity = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        var ring = _pendingRing = new ProgressRing { IsIndeterminate = true, IsActive = true, Width = 28, Height = 28 };
        AutomationProperties.SetName(ring, L("응답 작성 중", "Writing a response"));
        activity.Children.Add(ring);
        _pendingStatus.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_pendingStatus, 1); activity.Children.Add(_pendingStatus);
        body.Children.Add(activity);
        _pending = new Border { Child = body, Padding = new Thickness(16), CornerRadius = new CornerRadius(12),
            Width = 440, MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Left };
        _pending.Bind(Border.BackgroundProperty, _pending.GetResourceObservable("InfoBannerBackground"));
        AutomationProperties.SetAutomationId(_pending, "ManagedAiPendingResponse");
        _transcript.Children.Add(_pending);
        UpdateEnabled();
        Dispatcher.UIThread.Post(() => _scroll.ScrollToEnd(), DispatcherPriority.Loaded);
    }
    private void RemovePending()
    {
        if (_pendingRing is not null) _pendingRing.IsActive = false;
        _pendingRing = null;
        if (_pending is not null) _transcript.Children.Remove(_pending);
        _pending = null; _pendingStatus = null;
    }
    private void ResetConnection()
    {
        _loggedIn = false; _statusKnown = false;
        _models.ItemsSource = null; _models.SelectedItem = null;
        _account.Text = L("OpenAI 연결을 완료하면 대화를 시작할 수 있습니다.", "Connect to OpenAI to start chatting.");
    }
    private async Task RefreshStatusAsync(CancellationToken token)
    {
        await _modelSaveTask;
        if (!_preferencesLoaded)
        {
            try
            {
                _preferredModelId = (await _preferences.LoadPreferencesAsync(token))?.LastSelectedAiModel;
                _preferencesLoaded = true;
                ShowPreferenceNotice(null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { ShowPreferenceNotice(L("저장된 모델을 불러오지 못했습니다. 현재 목록에서 기본 모델을 선택합니다.",
                "Could not load the saved model. Selecting a default from the current list.")); }
        }
        _statusKnown = false; _loggedIn = false;
        _models.ItemsSource = null; _models.SelectedItem = null;
        var runtime = await _runtimes.GetActiveAsync(token);
        _runtimeInstalled = runtime is not null;
        _loggedIn = runtime is not null && await _authentication.IsLoggedInAsync(token);
        _statusKnown = true;
        _account.Text = runtime is null ? L("AI 사용 준비를 누르면 런타임 설치와 OpenAI 로그인을 진행합니다.",
                "Select Set up AI to install the runtime and sign in to OpenAI.")
            : $"Codex {runtime.Coordinate.Version}  |  " + (_loggedIn
                ? L("ChatGPT 연결됨", "Connected to ChatGPT")
                : L("OpenAI에 연결하면 대화를 시작할 수 있습니다.", "Connect to OpenAI to start chatting."));
        if (!_loggedIn) { _models.ItemsSource = null; _models.SelectedItem = null; return; }
        _status.Text = L("사용할 수 있는 모델을 불러오고 있습니다.", "Loading available models.");
        var models = await _modelCatalog.ListAsync(token);
        if (models.Count == 0) throw new ManagedAiException(AiFailureCode.ModelListUnavailable);
        var selected = AiModelSelection.Select(models, _preferredModelId, DateOnly.FromDateTime(DateTime.UtcNow));
        _updatingModelList = true;
        try { _models.ItemsSource = models; _models.SelectedItem = selected; }
        finally { _updatingModelList = false; }
        RememberModel(selected);
        await _modelSaveTask;
        _status.Text = L("대화할 준비를 마쳤습니다. Shift+Enter로 메시지를 전송할 수 있습니다.",
            "Ready to chat. Press Shift+Enter to send a message.");
    }

    private void UpdateModelHint()
    {
        _modelHint.Text = _models.SelectedItem is AiModel model
            ? L($"다음 응답에 사용할 모델: {model.Id}", $"Model for the next response: {model.Id}")
            : L("연결하면 대화 모델을 자동으로 불러옵니다.", "Chat models load automatically after connecting.");
        if (_models.SelectedItem is AiModel { TokenCost: { } cost } && cost.IsCurrent(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            _modelHint.Text += L($"\n100만 토큰당 입력 {cost.Input:0.###}, 출력 {cost.Output:0.###} 크레딧",
                $"\nPer million tokens: {cost.Input:0.###} input and {cost.Output:0.###} output credits");
            ToolTip.SetTip(_modelHint, L(
                $"{cost.VerifiedOn:yyyy년 M월 d일} 확인한 Standard 단가입니다. 캐시 입력은 {cost.CachedInput:0.###} 크레딧입니다.\n" +
                    "기본 모델은 입력과 출력 각 100만 토큰의 합계로 비교합니다. 실제 구독 사용량과 청구액은 달라질 수 있습니다.\n",
                $"Standard rates verified on {cost.VerifiedOn:yyyy-MM-dd}. Cached input costs {cost.CachedInput:0.###} credits.\n" +
                    "The default model compares the combined cost of one million input and output tokens. Actual subscription usage and charges may differ.\n")
                + OpenAiCodexRateCard.Source);
        }
        else ToolTip.SetTip(_modelHint, L("최근에 확인한 단가가 없으면 최신 Luna 모델을 선택하고, Luna 모델도 없으면 목록의 첫 모델을 선택합니다.",
            "Without recent rate data, the latest Luna model is selected. If none is available, the first model is selected."));
    }

    private void RememberModel(AiModel model)
    {
        _preferredModelId = model.Id;
        if (_preferencesLoaded) _modelSaveTask = SaveModelAfterAsync(_modelSaveTask, model.Id);
    }

    private async Task SaveModelAfterAsync(Task previous, string id)
    {
        await previous;
        try
        {
            // Read the latest settings for every write so unrelated preferences are retained.
            // Serialize rapid selections, and let Closing drain these writes without cancellation.
            var settings = await _preferences.LoadPreferencesAsync() ?? _preferences.GetDefaultPreferences();
            if (settings.LastSelectedAiModel != id)
            {
                settings.LastSelectedAiModel = id;
                await _preferences.SavePreferencesAsync(settings);
            }
            if (_preferredModelId == id) ShowPreferenceNotice(null);
        }
        catch
        {
            if (_preferredModelId == id) ShowPreferenceNotice(L("모델 선택을 저장하지 못했습니다. 현재 선택은 이 창에서 계속 사용할 수 있습니다.",
                "Could not save the model choice. The current choice remains available in this window."));
        }
    }

    private void ShowPreferenceNotice(string? message)
    {
        _preferenceNotice.Text = message;
        _preferenceNotice.IsVisible = message is not null;
    }
    private async Task ConnectAsync(CancellationToken token)
    {
        if (!await EnsureRuntimeAsync(token)) return;
        if (!await _authentication.IsLoggedInAsync(token)) await LoginAsync(AiLoginMethod.Browser, token);
        else await RefreshStatusAsync(token);
    }
    private async Task LogoutAsync(CancellationToken token)
    {
        await _authentication.LogoutAsync(token);
        ResetConnection();
        await RefreshStatusAsync(token);
        _status.Text = L("로그아웃했습니다. OpenAI 로그인으로 다시 연결할 수 있습니다.",
            "Signed out. Sign in to OpenAI to reconnect.");
    }
    private async Task<bool> EnsureRuntimeAsync(CancellationToken token)
    {
        if (await _runtimes.GetActiveAsync(token) is not null) return true;
        if (!ConfirmInstall()) { _status.Text = L("설치를 취소했습니다.", "Installation canceled."); return false; }
        await _runtimes.InstallAsync(null, Progress(), token);
        _runtimeInstalled = true; _skillsLoaded = false;
        return true;
    }
    private bool ConfirmInstall() => _messages.DisplayQuestion(L("OpenAI 공식 Codex를 TableCloth 전용 폴더에 설치하거나 업데이트하시겠습니까?",
            "Install or update the official OpenAI Codex runtime in TableCloth's dedicated folder?"),
        AppMessageBoxButton.YesNo, AppMessageBoxResult.No) == AppMessageBoxResult.Yes;
    private async Task InstallAsync(CancellationToken token)
    {
        if (!ConfirmInstall())
        { _status.Text = L("설치를 취소했습니다.", "Installation canceled."); return; }
        await _runtimes.InstallAsync(null, Progress(), token);
        _skillsLoaded = false;
        await RefreshStatusAsync(token); _status.Text = L("런타임 설치를 완료했습니다.", "Runtime installation complete.");
    }
    private async Task LoginAsync(AiLoginMethod method, CancellationToken token)
    {
        var operation = _operation;
        var acceptingLoginUpdates = true;
        var progress = new Progress<AiLoginUpdate>(update =>
        {
            if (!acceptingLoginUpdates || _operation != operation || operation is null) return;
            _status.Text = update.Stage switch
            {
                AiLoginStage.Starting => L("OpenAI 로그인 절차를 준비하고 있습니다.", "Preparing OpenAI sign-in."),
                AiLoginStage.Completed => L("ChatGPT 로그인을 확인했습니다.", "ChatGPT sign-in confirmed."),
                _ => L("브라우저에서 로그인을 완료하면 자동으로 연결합니다.", "Complete sign-in in your browser to connect automatically.")
            };
            if (update.VerificationUri is { } uri)
            {
                _loginUri = uri; _loginCode.Text = update.UserCode ?? string.Empty;
                _loginPanel.IsVisible = true;
            }
        });
        try { await _authentication.LoginAsync(method, progress, token); }
        finally
        {
            acceptingLoginUpdates = false;
            _loginUri = null; _loginCode.Text = string.Empty; _loginPanel.IsVisible = false;
        }
        _account.Text = L("ChatGPT 연결됨", "Connected to ChatGPT");
        _status.Text = L("ChatGPT 로그인을 확인했습니다. 사용할 수 있는 모델을 불러오고 있습니다.",
            "ChatGPT sign-in confirmed. Loading available models.");
        await RefreshStatusAsync(token);
    }
    private void OpenLoginPage()
    {
        if (_loginUri is null || !CodexLoginOutputParser.IsAllowedLoginUri(_loginUri)) return;
        try { Process.Start(new ProcessStartInfo(_loginUri.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch { _status.Text = L("로그인 페이지를 열지 못했습니다. 기본 브라우저 설정을 확인한 후 다시 시도할 수 있습니다.",
            "Could not open the sign-in page. Check your default browser and try again."); }
    }
    private IProgress<AiProgress> Progress()
    {
        var operation = _operation;
        return new Progress<AiProgress>(update =>
        {
            if (_operation != operation || operation is null) return;
            var text = update.Stage switch
            {
                "ResolvingRelease" => L("공식 릴리스를 확인하고 있습니다.", "Checking the official release."),
                "VerifyingChecksums" => L("체크섬을 검증하고 있습니다.", "Verifying checksums."),
                "Downloading" => L("런타임을 내려받고 있습니다.", "Downloading the runtime."),
                "Extracting" => L("패키지를 검사하고 설치하고 있습니다.", "Checking and installing the package."),
                "Activating" => L("새 런타임을 활성화하고 있습니다.", "Activating the new runtime."),
                "CheckingAuthentication" => L("ChatGPT 로그인 상태를 확인하고 있습니다.", "Checking ChatGPT sign-in status."),
                "TakingLonger" => L("응답이 지연되고 있습니다. 중지하거나 최대 180초까지 기다릴 수 있습니다.",
                    "The response is taking longer. You can stop or wait up to 180 seconds."),
                "Validating" => L("응답을 정리하고 있습니다.", "Validating the response."),
                "Searching" => update.SearchCalls > 0
                    ? L($"웹 검색을 진행하고 있습니다. 현재 {update.SearchCalls}회 완료", $"Searching the web. {update.SearchCalls} searches completed.")
                    : L("웹 검색을 진행하고 있습니다.", "Searching the web."),
                "Writing" => L("응답을 작성하고 있습니다.", "Writing a response."),
                _ => update.SearchCalls > 0
                    ? L($"웹 검색 {update.SearchCalls}회를 마쳤습니다. 응답을 작성하고 있습니다.",
                        $"Completed {update.SearchCalls} web searches. Writing a response.")
                    : L("응답을 준비하고 있습니다.", "Preparing a response.")
            };
            _status.Text = text;
            if (_pendingStatus is not null) _pendingStatus.Text = text;
        });
    }
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private static string L(string korean, string english) => ManagedAiText.Select(korean, english);
    private static string FailureText(AiFailureCode code) => code switch
    {
        AiFailureCode.RuntimeNotInstalled => L("AI 사용 준비 버튼으로 런타임 설치와 로그인을 진행할 수 있습니다.", "Select Set up AI to install the runtime and sign in."),
        AiFailureCode.BlockedByPolicy => L("전용 Codex 설정이 정책 검사를 통과하지 못했거나 런타임 실행이 차단되었습니다. OpenAI 계정 / 설정에서 연결을 다시 확인할 수 있습니다.",
            "The dedicated Codex configuration failed its policy check or the runtime could not start. Check the connection in OpenAI account / settings."),
        AiFailureCode.AuthenticationRequired => L("로그인 정보가 없거나 만료되었습니다. OpenAI 로그인 버튼으로 다시 연결할 수 있습니다.", "Sign-in is missing or expired. Sign in to OpenAI again."),
        AiFailureCode.LoginFlowUnavailable => L("로그인을 완료하지 못했습니다. OpenAI 로그인으로 다시 시도하거나 OpenAI 계정 / 설정에서 기기 코드로 로그인할 수 있습니다.",
            "Could not complete sign-in. Retry or use device-code sign-in in OpenAI account / settings."),
        AiFailureCode.ModelListUnavailable => L("모델 목록을 불러오지 못했습니다. 모델 목록 다시 불러오기 버튼으로 재시도할 수 있습니다.", "Could not load models. Reload the model list to retry."),
        AiFailureCode.InvalidModel => L("선택한 모델을 사용할 수 없습니다. OpenAI 계정 / 설정에서 모델 목록을 새로 고친 후 다시 선택할 수 있습니다.",
            "The selected model is unavailable. Refresh the model list in OpenAI account / settings and choose again."),
        AiFailureCode.RuntimeBusy => L("다른 창에서 설치 또는 대화를 진행하고 있습니다.", "Another window is installing or chatting."),
        AiFailureCode.ProviderTimeout => L("제한 시간을 초과해 요청을 종료했습니다. 메시지를 다시 보내 재시도할 수 있습니다.", "The request timed out. Send the message again to retry."),
        AiFailureCode.NoPreviousVersion => L("복원할 이전 버전이 없습니다.", "No previous runtime version is available."),
        AiFailureCode.InvalidQuery => L("메시지가 비어 있거나 너무 깁니다. 4,000자 이내의 메시지를 입력할 수 있습니다.", "Enter a message of up to 4,000 characters."),
        AiFailureCode.UnsafeUrl => L("공개 웹사이트의 HTTP 또는 HTTPS 링크만 열 수 있습니다. 로컬 주소와 다른 링크 형식은 지원하지 않습니다.",
            "Only public HTTP or HTTPS websites can be opened. Local addresses and other link types are unsupported."),
        AiFailureCode.SubscriptionUnavailable => L("구독 사용량 또는 요청 한도에 도달했습니다. ChatGPT 계정의 사용량을 확인한 후 다시 시도할 수 있습니다.",
            "Your subscription usage or request limit was reached. Check your ChatGPT usage and retry."),
        AiFailureCode.ProviderNetworkUnavailable => L("OpenAI 서비스에 연결하지 못했습니다. 네트워크나 프록시 설정을 확인한 후 다시 시도할 수 있습니다.",
            "Could not connect to OpenAI. Check your network or proxy and retry."),
        AiFailureCode.ProviderConfigurationInvalid => L("설치된 Codex와 실행 설정이 호환되지 않습니다. 런타임 버전과 구성을 점검할 수 있습니다.",
            "The installed Codex runtime and execution settings are incompatible. Check the runtime version and configuration."),
        AiFailureCode.ProviderRequestRejected => L("OpenAI가 요청 형식을 거부했습니다. 실행 옵션이나 응답 형식의 호환성을 점검할 수 있습니다.",
            "OpenAI rejected the request format. Check execution options and response-format compatibility."),
        AiFailureCode.InvalidStructuredOutput => L("공급자 응답을 해석하지 못했습니다.", "Could not parse the provider response."),
        AiFailureCode.CatalogUnavailable => L("Catalog를 불러오지 못했습니다. 연결 상태를 확인한 후 링크를 다시 열 수 있습니다.", "Could not load the Catalog. Check the connection and reopen the link."),
        AiFailureCode.SkillInvalid => L("스킬 폴더를 불러오지 못했습니다. SKILL.md와 폴더 구성을 확인할 수 있습니다.", "Could not load the skill folder. Check SKILL.md and its folder structure."),
        AiFailureCode.SkillAlreadyInstalled => L("같은 이름의 스킬 폴더가 이미 있습니다. 전용 스킬 폴더에서 기존 항목을 확인할 수 있습니다.",
            "A skill folder with this name already exists. Check the dedicated skill folder."),
        AiFailureCode.SkillIsolationFailed => L("전용 스킬 격리를 적용하지 못해 요청을 중지했습니다. 스킬을 다시 불러온 후 재시도할 수 있습니다.",
            "Could not isolate dedicated skills, so the request was stopped. Reload skills and retry."),
        AiFailureCode.CertificateScanUnavailable => L("인증서 조회 도구를 실행하지 못했습니다. TableClothCli 설치 상태를 확인한 후 다시 시도할 수 있습니다.",
            "Could not run the certificate scanner. Check the TableClothCli installation and retry."),
        _ => L($"요청을 완료하지 못했습니다. 오류 코드: {code}", $"Could not complete the request. Error code: {code}")
    };
}
