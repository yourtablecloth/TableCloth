using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.Models;
using TableCloth.Theme.Controls;

namespace TableCloth.ManagedAi;

public sealed class ManagedAiWindow : Window
{
    private readonly ManagedAiChatSession _session;
    private readonly IManagedRuntimeManager _runtimes;
    private readonly IProviderAuthentication _authentication;
    private readonly IAppMessageBox _messages;
    private readonly IManagedAiModelCatalog _modelCatalog;
    private readonly IPreferencesManager _preferences;
    private Task _modelSaveTask = Task.CompletedTask;
    private string? _preferredModelId;
    private bool _preferencesLoaded;
    private bool _updatingModelList;
    private readonly TextBox _input = new()
    {
        Watermark = "메시지를 입력합니다. Shift+Enter로 전송하고 Enter로 줄을 바꿉니다.",
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000, MinHeight = 78, MaxHeight = 180
    };
    private readonly TextBlock _status = Text("웹 링크를 누르면 Windows Sandbox 또는 현재 브라우저를 선택할 수 있습니다.");
    private readonly TextBlock _account = Text("로그인 상태를 확인하고 있습니다.");
    private readonly StackPanel _transcript = new() { Spacing = 16, Margin = new Thickness(20) };
    private readonly ScrollViewer _scroll;
    private readonly List<Control> _actions = [];
    private readonly Button _manage = new() { Content = "OpenAI 계정 / 설정" };
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
    private readonly Button _connect = new() { Content = "연결 확인 중", IsEnabled = false };
    private readonly ComboBox _models = new() { MinWidth = 190, MaxWidth = 310, PlaceholderText = "연결 후 모델을 선택할 수 있습니다." };
    private readonly TextBlock _modelHint = Text("연결하면 대화 모델을 자동으로 불러옵니다.");
    private readonly TextBlock _preferenceNotice = new() { IsVisible = false, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
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
    private readonly Button _send = new() { Content = "보내기", MinWidth = 86, IsEnabled = false };
    private readonly Button _cancel = new() { Content = "중지", IsVisible = false };
    private readonly StackPanel _loginPanel = new() { Spacing = 8, Margin = new Thickness(0, 10), IsVisible = false };
    private readonly SelectableTextBlock _loginCode = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly Button _loginLink = new() { Content = "OpenAI 로그인 페이지 열기" };
    private CancellationTokenSource? _operation;
    private Uri? _loginUri;
    private bool _closing;

    public ManagedAiWindow(ManagedAiChatSession session, IManagedRuntimeManager runtimes,
        IProviderAuthentication authentication, IAppMessageBox messages, IManagedAiModelCatalog modelCatalog,
        IPreferencesManager preferences)
    {
        _session = session; _runtimes = runtimes; _authentication = authentication; _messages = messages; _modelCatalog = modelCatalog;
        _preferences = preferences;
        Title = "식탁보 AI (Preview)";
        Width = 900; Height = 780; MinWidth = 640; MinHeight = 540;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 8 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock { Text = "식탁보 AI (Preview)", FontSize = 24, FontWeight = FontWeight.SemiBold });
        title.Children.Add(_account);
        heading.Children.Add(title);
        var clear = ActionButton("새 대화", _ =>
        { _session.Clear(); _transcript.Children.Clear(); AddWelcome(); return Task.CompletedTask; });
        AutomationProperties.SetAutomationId(clear, "ManagedAiNewChat");
        Grid.SetColumn(clear, 1); heading.Children.Add(clear);
        header.Children.Add(heading);
        var connection = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        var modelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modelRow.Children.Add(new TextBlock { Text = "대화 모델", VerticalAlignment = VerticalAlignment.Center });
        modelRow.Children.Add(_models);
        connection.Children.Add(modelRow);
        ToolTip.SetTip(_manage, "로그인, 로그아웃과 Codex 런타임 설정을 엽니다.");
        _actions.Add(_manage);
        Grid.SetColumn(_manage, 1); connection.Children.Add(_manage);
        header.Children.Add(connection);
        header.Children.Add(_modelHint);
        header.Children.Add(_preferenceNotice);
        _connect.Classes.Add("accent");
        _connect.Click += async (_, _) => await RunAsync(ConnectAsync);
        header.Children.Add(_connect);
        _loginPanel.Children.Add(Text("브라우저에서 로그인을 마치면 자동으로 대화 준비를 완료합니다. 기기 코드가 표시되면 로그인 페이지에 입력합니다."));
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
        composer.Children.Add(Text("대화는 이 창의 메모리에만 보관합니다. 전송 시 OpenAI 구독 사용량을 사용하며 AI 응답의 정확성을 보장하지 않습니다."));
        Grid.SetRow(composer, 2); root.Children.Add(composer);
        // The application's theme has no MenuFlyoutPresenter/MenuItem template.
        // Use the same in-window panel approach as AboutWindow with themed native buttons.
        var dismiss = new Border { Background = Brushes.Transparent };
        dismiss.PointerPressed += (_, e) => { CloseSettings(); e.Handled = true; };
        _settingsOverlay.Children.Add(dismiss);
        var settings = new StackPanel { Spacing = 10 };
        var settingsHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        settingsHeading.Children.Add(new TextBlock { Text = "OpenAI 계정", FontSize = 18, FontWeight = FontWeight.SemiBold });
        var closeSettings = new Button { Content = "닫기", Padding = new Thickness(8, 3) };
        closeSettings.Click += (_, _) => CloseSettings();
        AutomationProperties.SetAutomationId(closeSettings, "ManagedAiSettingsClose");
        Grid.SetColumn(closeSettings, 1); settingsHeading.Children.Add(closeSettings);
        settings.Children.Add(settingsHeading);
        var accountStatus = Text("");
        accountStatus.Bind(TextBlock.TextProperty, _account.GetObservable(TextBlock.TextProperty));
        settings.Children.Add(accountStatus);
        settings.Children.Add(Text("식탁보에서 사용하는 ChatGPT 로그인을 관리합니다."));
        _signIn = SettingsButton("OpenAI 로그인", "ManagedAiSignIn", ConnectAsync);
        _signOut = SettingsButton("로그아웃", "ManagedAiSignOut", LogoutAsync);
        _reconnect = SettingsButton("로그아웃 후 다시 로그인", "ManagedAiReconnect", async token =>
        { await LogoutAsync(token); token.ThrowIfCancellationRequested(); await ConnectAsync(token); });
        settings.Children.Add(_signIn); settings.Children.Add(_signOut); settings.Children.Add(_reconnect);
        settings.Children.Add(new TextBlock { Text = "런타임 및 연결 설정", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        settings.Children.Add(SettingsButton("연결 및 모델 목록 새로 고침", "ManagedAiRefresh", RefreshStatusAsync));
        settings.Children.Add(SettingsButton("Codex 설치 / 업데이트", "ManagedAiUpdate", InstallAsync));
        settings.Children.Add(SettingsButton("기기 코드로 로그인", "ManagedAiDeviceLogin", async token =>
        { if (await EnsureRuntimeAsync(token)) await LoginAsync(AiLoginMethod.DeviceCode, token); }));
        settings.Children.Add(SettingsButton("이전 런타임 버전 복원", "ManagedAiRollback", async token =>
        { await _runtimes.RollbackAsync(token); await RefreshStatusAsync(token); }));
        _settingsPanel.Child = new ScrollViewer { Content = settings, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _settingsPanel.Bind(Border.BackgroundProperty, _settingsPanel.GetResourceObservable("SolidBackgroundFillColorBaseBrush"));
        _settingsPanel.Bind(Border.BorderBrushProperty, _settingsPanel.GetResourceObservable("ControlStrokeColorDefaultBrush"));
        _settingsOverlay.Children.Add(_settingsPanel);
        KeyboardNavigation.SetTabNavigation(_settingsOverlay, KeyboardNavigationMode.Cycle);
        _manage.Click += (_, _) =>
        {
            _settingsOverlay.IsVisible = !_settingsOverlay.IsVisible;
            if (_settingsOverlay.IsVisible) (_loggedIn ? _signOut : _signIn).Focus();
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
        AutomationProperties.SetAutomationId(_preferenceNotice, "ManagedAiPreferenceNotice");
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
        _timer.Tick += (_, _) => _elapsed.Text = $"{(int)_watch.Elapsed.TotalSeconds}초 경과";
        _input.TextChanged += (_, _) => UpdateEnabled();
        _send.Click += async (_, _) => await SendAsync();
        _input.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Shift)
            { e.Handled = true; await SendAsync(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _cancel.Click += (_, _) => _operation?.Cancel();
        Opened += async (_, _) => await RunAsync(RefreshStatusAsync);
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

    private Button SettingsButton(string label, string id, Func<CancellationToken, Task> action)
    {
        var button = ActionButton(label, action);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0);
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }

    private Button ActionButton(string label, Func<CancellationToken, Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 6) };
        button.Click += async (_, _) => await RunAsync(action);
        _actions.Add(button); return button;
    }
    private void AddWelcome()
    {
        AddMessage("식탁보", "찾으려는 서비스를 말씀해 주시면 관련 웹사이트를 찾아드리겠습니다.\n\n웹 링크를 누르면 Windows Sandbox 또는 현재 브라우저를 선택할 수 있습니다. Windows Sandbox를 선택한 경우 Catalog에 등록된 서비스는 Spork로 필요한 소프트웨어를 설치한 후 해당 페이지로 이동합니다.");
        _starters.Children.Clear(); _starterButtons.Clear(); _starters.IsVisible = true;
        _starters.Children.Add(new TextBlock { Text = "이런 질문으로 시작할 수 있습니다", FontSize = 16, FontWeight = FontWeight.SemiBold });
        _starters.Children.Add(Text("선택한 문장을 입력란에 추가합니다. 내용을 수정한 뒤 전송할 수 있습니다."));
        var choices = new UniformGrid { Columns = 2 };
        AddStarter("공공서비스 찾기", "주민등록등본을 온라인으로 발급받을 수 있는 공식 사이트와 이용 절차를 알려 주세요.");
        AddStarter("은행 업무 준비", "인터넷뱅킹을 이용하려고 합니다. 먼저 어느 은행인지 물어보고 공식 사이트와 준비 사항을 안내해 주세요.");
        AddStarter("세금 신고 사이트 찾기", "세금 신고에 사용할 공식 사이트를 찾아 링크를 알려 주세요.");
        AddStarter("식탁보 사용법", "식탁보에서 웹사이트를 열고 필요한 소프트웨어를 설치하는 방법을 알려 주세요.");
        _starters.Children.Add(choices);
        _transcript.Children.Add(_starters);
        Dispatcher.UIThread.Post(() => _scroll.ScrollToHome(), DispatcherPriority.Loaded);

        void AddStarter(string title, string prompt)
        {
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(Text(prompt));
            var button = new Button { Content = content, Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            button.Classes.Add("chat-starter");
            AutomationProperties.SetName(button, title + ". " + prompt);
            button.Click += (_, _) =>
            {
                if (_operation is not null) return;
                var draft = string.IsNullOrWhiteSpace(_input.Text) ? prompt : _input.Text.TrimEnd() + "\n\n" + prompt;
                if (draft.Length > _input.MaxLength) { _status.Text = "입력란의 글자 수를 줄이면 추천 문장을 추가할 수 있습니다."; return; }
                _input.Text = draft; _input.CaretIndex = draft.Length; _input.Focus();
                _status.Text = "추천 문장을 추가했습니다. 내용을 수정한 뒤 Shift+Enter로 전송할 수 있습니다.";
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
        var copy = new Button { Content = "복사", Padding = new Thickness(10, 3), MinHeight = 28, FontSize = 12 };
        copy.Classes.Add("chat-copy");
        AutomationProperties.SetName(copy, $"{author} 메시지 복사");
        ToolTip.SetTip(copy, "마크다운 원문을 복사합니다.");
        copy.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is not { } clipboard) throw new InvalidOperationException();
                await clipboard.SetTextAsync(content);
                copy.Content = "복사 완료";
                await Task.Delay(1600);
                copy.Content = "복사";
            }
            catch { copy.Content = "다시 복사"; _status.Text = "클립보드에 복사하지 못했습니다. 다시 시도할 수 있습니다."; }
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
        _starters.IsVisible = false;
        var text = _input.Text.Trim();
        AddMessage("사용자", text, user: true);
        _input.Text = string.Empty;
        await RunAsync(async token =>
        {
            AddPending(model);
            var response = await _session.SendAsync(text, Progress(), token, model.Id);
            RemovePending();
            AddMessage($"식탁보 / {response.Model ?? model.Id}", response.Text);
            _status.Text = response.SearchCalls > 0 ? $"응답을 완료했습니다. 웹 검색 {response.SearchCalls}회" : "응답을 완료했습니다.";
        }, errorInChat: true);
        _input.Focus();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, bool errorInChat = false)
    {
        if (_operation is not null) return;
        _settingsOverlay.IsVisible = false;
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation; UpdateEnabled();
        _watch.Restart(); _elapsed.Text = "0초 경과"; _elapsed.IsVisible = true; _timer.Start();
        _status.Text = "요청을 처리하고 있습니다.";
        try
        {
            await action(cancellation.Token);
            if (_status.Text == "요청을 처리하고 있습니다.") _status.Text = "요청을 완료했습니다.";
        }
        catch (OperationCanceledException) { ShowFailure("요청을 중지했습니다. 새 메시지를 보내 대화를 계속할 수 있습니다."); }
        catch (ManagedAiException ex)
        {
            if (ex.Code is AiFailureCode.AuthenticationRequired or AiFailureCode.RuntimeNotInstalled)
            {
                ResetConnection(); _statusKnown = true;
                if (ex.Code == AiFailureCode.RuntimeNotInstalled) _runtimeInstalled = false;
            }
            ShowFailure(FailureText(ex.Code));
        }
        catch { ShowFailure("요청을 완료하지 못했습니다. 연결 상태를 확인한 후 다시 시도할 수 있습니다."); }
        finally
        {
            _operation = null;
            RemovePending(); _timer.Stop(); _watch.Stop(); _elapsed.IsVisible = false;
            _loginUri = null; _loginCode.Text = string.Empty; _loginPanel.IsVisible = false;
            UpdateEnabled();
            if (_closing) Close();
        }
        void ShowFailure(string message) { _status.Text = message; if (errorInChat) AddMessage("안내", message); }
    }

    private void UpdateEnabled()
    {
        foreach (var action in _actions) action.IsEnabled = _operation is null;
        foreach (var starter in _starterButtons) starter.IsEnabled = _operation is null;
        _signIn.IsVisible = !_loggedIn;
        _signIn.Content = _runtimeInstalled ? "OpenAI 로그인" : "설치하고 로그인";
        _signOut.IsVisible = _loggedIn;
        _reconnect.IsVisible = _loggedIn;
        _connect.IsVisible = !_loggedIn || _models.SelectedItem is null;
        _connect.IsEnabled = _operation is null;
        _connect.Content = !_statusKnown ? "연결 다시 확인" : !_runtimeInstalled ? "AI 사용 준비" : !_loggedIn ? "OpenAI 로그인" : "모델 목록 다시 불러오기";
        _models.IsEnabled = _operation is null && _loggedIn;
        _send.IsEnabled = _operation is null && _loggedIn && _models.SelectedItem is AiModel && !string.IsNullOrWhiteSpace(_input.Text);
        _cancel.IsVisible = _operation is not null;
        _busyBar.IsVisible = _operation is not null && _pending is null;
        _busyBar.IsActive = _busyBar.IsVisible;
        _input.IsReadOnly = _operation is not null;
    }

    private void AddPending(AiModel model)
    {
        _pendingStatus = Text("응답을 준비하고 있습니다.");
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = $"식탁보 / {model.Id}", FontWeight = FontWeight.SemiBold, FontSize = 12 });
        var activity = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        var ring = _pendingRing = new ProgressRing { IsIndeterminate = true, IsActive = true, Width = 28, Height = 28 };
        AutomationProperties.SetName(ring, "응답 작성 중");
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
        _account.Text = "OpenAI 연결을 완료하면 대화를 시작할 수 있습니다.";
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
            catch { ShowPreferenceNotice("저장된 모델을 불러오지 못했습니다. 현재 목록에서 기본 모델을 선택합니다."); }
        }
        _statusKnown = false; _loggedIn = false;
        _models.ItemsSource = null; _models.SelectedItem = null;
        var runtime = await _runtimes.GetActiveAsync(token);
        _runtimeInstalled = runtime is not null;
        _loggedIn = runtime is not null && await _authentication.IsLoggedInAsync(token);
        _statusKnown = true;
        _account.Text = runtime is null ? "AI 사용 준비를 누르면 런타임 설치와 OpenAI 로그인을 진행합니다."
            : $"Codex {runtime.Coordinate.Version}  |  " + (_loggedIn ? "ChatGPT 연결됨" : "OpenAI에 연결하면 대화를 시작할 수 있습니다.");
        if (!_loggedIn) { _models.ItemsSource = null; _models.SelectedItem = null; return; }
        _status.Text = "사용할 수 있는 모델을 불러오고 있습니다.";
        var models = await _modelCatalog.ListAsync(token);
        if (models.Count == 0) throw new ManagedAiException(AiFailureCode.ModelListUnavailable);
        var selected = AiModelSelection.Select(models, _preferredModelId, DateOnly.FromDateTime(DateTime.UtcNow));
        _updatingModelList = true;
        try { _models.ItemsSource = models; _models.SelectedItem = selected; }
        finally { _updatingModelList = false; }
        RememberModel(selected);
        await _modelSaveTask;
        _status.Text = "대화할 준비를 마쳤습니다. Shift+Enter로 메시지를 전송할 수 있습니다.";
    }

    private void UpdateModelHint()
    {
        _modelHint.Text = _models.SelectedItem is AiModel model ? $"다음 응답에 사용할 모델: {model.Id}" : "연결하면 대화 모델을 자동으로 불러옵니다.";
        if (_models.SelectedItem is AiModel { TokenCost: { } cost } && cost.IsCurrent(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            _modelHint.Text += $"\n100만 토큰당 입력 {cost.Input:0.###}, 출력 {cost.Output:0.###} 크레딧";
            ToolTip.SetTip(_modelHint, $"{cost.VerifiedOn:yyyy년 M월 d일} 확인한 Standard 단가입니다. 캐시 입력은 {cost.CachedInput:0.###} 크레딧입니다.\n" +
                "기본 모델은 입력과 출력 각 100만 토큰의 합계로 비교합니다. 실제 구독 사용량과 청구액은 달라질 수 있습니다.\n" + OpenAiCodexRateCard.Source);
        }
        else ToolTip.SetTip(_modelHint, "최근에 확인한 단가가 없으면 최신 Luna 모델을 선택하고, Luna 모델도 없으면 목록의 첫 모델을 선택합니다.");
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
            if (_preferredModelId == id) ShowPreferenceNotice("모델 선택을 저장하지 못했습니다. 현재 선택은 이 창에서 계속 사용할 수 있습니다.");
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
        _status.Text = "로그아웃했습니다. OpenAI 로그인으로 다시 연결할 수 있습니다.";
    }
    private async Task<bool> EnsureRuntimeAsync(CancellationToken token)
    {
        if (await _runtimes.GetActiveAsync(token) is not null) return true;
        if (!ConfirmInstall()) { _status.Text = "설치를 취소했습니다."; return false; }
        await _runtimes.InstallAsync(null, Progress(), token);
        _runtimeInstalled = true;
        return true;
    }
    private bool ConfirmInstall() => _messages.DisplayQuestion("OpenAI 공식 Codex를 TableCloth 전용 폴더에 설치하거나 업데이트하시겠습니까?",
        AppMessageBoxButton.YesNo, AppMessageBoxResult.No) == AppMessageBoxResult.Yes;
    private async Task InstallAsync(CancellationToken token)
    {
        if (!ConfirmInstall())
        { _status.Text = "설치를 취소했습니다."; return; }
        await _runtimes.InstallAsync(null, Progress(), token);
        await RefreshStatusAsync(token); _status.Text = "런타임 설치를 완료했습니다.";
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
                AiLoginStage.Starting => "OpenAI 로그인 절차를 준비하고 있습니다.",
                AiLoginStage.Completed => "ChatGPT 로그인을 확인했습니다.",
                _ => "브라우저에서 로그인을 완료하면 자동으로 연결합니다."
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
        _account.Text = "ChatGPT 연결됨";
        _status.Text = "ChatGPT 로그인을 확인했습니다. 사용할 수 있는 모델을 불러오고 있습니다.";
        await RefreshStatusAsync(token);
    }
    private void OpenLoginPage()
    {
        if (_loginUri is null || !CodexLoginOutputParser.IsAllowedLoginUri(_loginUri)) return;
        try { Process.Start(new ProcessStartInfo(_loginUri.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch { _status.Text = "로그인 페이지를 열지 못했습니다. 기본 브라우저 설정을 확인한 후 다시 시도할 수 있습니다."; }
    }
    private IProgress<AiProgress> Progress()
    {
        var operation = _operation;
        return new Progress<AiProgress>(update =>
        {
            if (_operation != operation || operation is null) return;
            var text = update.Stage switch
            {
                "ResolvingRelease" => "공식 릴리스를 확인하고 있습니다.",
                "VerifyingChecksums" => "체크섬을 검증하고 있습니다.",
                "Downloading" => "런타임을 내려받고 있습니다.",
                "Extracting" => "패키지를 검사하고 설치하고 있습니다.",
                "Activating" => "새 런타임을 활성화하고 있습니다.",
                "CheckingAuthentication" => "ChatGPT 로그인 상태를 확인하고 있습니다.",
                "TakingLonger" => "응답이 지연되고 있습니다. 중지하거나 최대 180초까지 기다릴 수 있습니다.",
                "Validating" => "응답을 정리하고 있습니다.",
                "Searching" => update.SearchCalls > 0 ? $"웹 검색을 진행하고 있습니다. 현재 {update.SearchCalls}회 완료" : "웹 검색을 진행하고 있습니다.",
                "Writing" => "응답을 작성하고 있습니다.",
                _ => update.SearchCalls > 0 ? $"웹 검색 {update.SearchCalls}회를 마쳤습니다. 응답을 작성하고 있습니다." : "응답을 준비하고 있습니다."
            };
            _status.Text = text;
            if (_pendingStatus is not null) _pendingStatus.Text = text;
        });
    }
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private static string FailureText(AiFailureCode code) => code switch
    {
        AiFailureCode.RuntimeNotInstalled => "AI 사용 준비 버튼으로 런타임 설치와 로그인을 진행할 수 있습니다.",
        AiFailureCode.AuthenticationRequired => "로그인 정보가 없거나 만료되었습니다. OpenAI 로그인 버튼으로 다시 연결할 수 있습니다.",
        AiFailureCode.LoginFlowUnavailable => "로그인을 완료하지 못했습니다. OpenAI 로그인으로 다시 시도하거나 OpenAI 계정 / 설정에서 기기 코드로 로그인할 수 있습니다.",
        AiFailureCode.ModelListUnavailable => "모델 목록을 불러오지 못했습니다. 모델 목록 다시 불러오기 버튼으로 재시도할 수 있습니다.",
        AiFailureCode.InvalidModel => "선택한 모델을 사용할 수 없습니다. OpenAI 계정 / 설정에서 모델 목록을 새로 고친 후 다시 선택할 수 있습니다.",
        AiFailureCode.RuntimeBusy => "다른 창에서 설치 또는 대화를 진행하고 있습니다.",
        AiFailureCode.ProviderTimeout => "제한 시간을 초과해 요청을 종료했습니다. 메시지를 다시 보내 재시도할 수 있습니다.",
        AiFailureCode.NoPreviousVersion => "복원할 이전 버전이 없습니다.",
        AiFailureCode.InvalidQuery => "메시지가 비어 있거나 너무 깁니다. 4,000자 이내의 메시지를 입력할 수 있습니다.",
        AiFailureCode.UnsafeUrl => "공개 웹사이트의 HTTP 또는 HTTPS 링크만 열 수 있습니다. 로컬 주소와 다른 링크 형식은 지원하지 않습니다.",
        AiFailureCode.SubscriptionUnavailable => "구독 사용량 또는 요청 한도에 도달했습니다. ChatGPT 계정의 사용량을 확인한 후 다시 시도할 수 있습니다.",
        AiFailureCode.ProviderNetworkUnavailable => "OpenAI 서비스에 연결하지 못했습니다. 네트워크나 프록시 설정을 확인한 후 다시 시도할 수 있습니다.",
        AiFailureCode.ProviderConfigurationInvalid => "설치된 Codex와 실행 설정이 호환되지 않습니다. 런타임 버전과 구성을 점검할 수 있습니다.",
        AiFailureCode.ProviderRequestRejected => "OpenAI가 요청 형식을 거부했습니다. 실행 옵션이나 응답 형식의 호환성을 점검할 수 있습니다.",
        AiFailureCode.InvalidStructuredOutput => "공급자 응답을 해석하지 못했습니다.",
        AiFailureCode.CatalogUnavailable => "Catalog를 불러오지 못했습니다. 연결 상태를 확인한 후 링크를 다시 열 수 있습니다.",
        _ => $"요청을 완료하지 못했습니다. 오류 코드: {code}"
    };
}
