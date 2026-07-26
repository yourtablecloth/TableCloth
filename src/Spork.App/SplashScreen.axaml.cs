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
using Spork.ViewModels;

namespace Spork
{
    // 이슈 #296: TableCloth 와 동일한 VNext 스플래시(빨간 식탁보). 인트로 애니메이션은 동일하나
    // 부팅 트리거는 여기가 아니라 App.RunStartupAsync 가 담당한다(스플래시 표시→부팅→애니메이션 완료 후 메인 창).
    public partial class SplashScreen : Window
    {
        public SplashScreen() => InitializeComponent();

        public SplashScreen(
            SplashScreenViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += OnLoaded;
        }

        public SplashScreenViewModel ViewModel
            => (SplashScreenViewModel)DataContext!;

        // 인트로 애니메이션 완료 Task. App 이 부팅 완료 후 이 Task 를 await 한 뒤 스플래시를 닫는다
        // (부팅이 애니메이션보다 빨라도 애니메이션 도중 닫히지 않게).
        private Task _introAnimation = Task.CompletedTask;

        public Task IntroAnimationTask => _introAnimation;

        private void OnLoaded(object? sender, RoutedEventArgs e)
            => _introAnimation = PlayIntroAnimationAsync();

        private async Task PlayIntroAnimationAsync()
        {
            var easeOut = new CubicEaseOut();

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
                // 애니메이션 실패가 부팅/전환을 막지 않도록 무시(끝 상태는 FillMode.Forward 로 보존).
            }
        }

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 무테두리 스플래시를 드래그로 이동(WPF DragMove 대체).
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }
    }
}
