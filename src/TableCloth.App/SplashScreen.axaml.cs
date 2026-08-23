using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth;

public partial class SplashScreen : Window
{
    public SplashScreen() => InitializeComponent();

    public SplashScreen(
        SplashScreenViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.StatusUpdate += ViewModel_StatusUpdate;
        Loaded += OnLoaded;
    }

    public SplashScreenViewModel ViewModel
        => (SplashScreenViewModel)DataContext!;

    // 이슈 #296: 인트로 애니메이션 완료 Task. 부팅이 애니메이션보다 빨리 끝나도 애니메이션이
    // 완전히 끝난 뒤에 스플래시를 닫도록, App(OnInitializeDone) 이 이 Task 를 await 한다.
    private Task _introAnimation = Task.CompletedTask;

    public Task IntroAnimationTask => _introAnimation;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 이슈 #296: VNext 스플래시 인트로 애니메이션(식탁보 슬라이드인 + 타이틀 페이드인)을 재생한다.
        // 부트 게이트라 자동 페이드아웃/제거는 하지 않는다(창은 부팅 완료 시 InitializeDone 로 닫힘).
        _introAnimation = PlayIntroAnimationAsync();

        // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅.
        if (ViewModel.SplashScreenLoadedCommand.CanExecute(null))
            ViewModel.SplashScreenLoadedCommand.Execute(null);
    }

    private async Task PlayIntroAnimationAsync()
    {
        var easeOut = new CubicEaseOut();

        // 빨간 식탁보: 위(-700)에서 제자리(0)로 내려오며 나타남.
        var clothAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.2d),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, -700d),
                        new Setter(Visual.OpacityProperty, 0d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, 0d),
                        new Setter(Visual.OpacityProperty, 1d),
                    },
                },
            },
        };

        // 타이틀/슬로건: 식탁보가 어느 정도 내려온 뒤(40%) 페이드인.
        var textAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.2d),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(0.4d), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
            },
        };

        try
        {
            await Task.WhenAll(
                clothAnim.RunAsync(ClothImage),
                textAnim.RunAsync(TitleText));
        }
        catch
        {
            // 애니메이션 실패가 부팅을 막지 않도록 무시(끝 상태는 FillMode.Forward 로 보존).
        }
    }

    private void ViewModel_StatusUpdate(object? sender, StatusUpdateRequestEventArgs e)
        => ViewModel.Status = e.Status;

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 무테두리 스플래시를 드래그로 이동(WPF DragMove 대체).
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
