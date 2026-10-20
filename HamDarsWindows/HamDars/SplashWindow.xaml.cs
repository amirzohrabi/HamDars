using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HamDars;

/// <summary>
/// Short cinematic intro: shows the HamDars logo, then opens two neon curtains
/// to reveal the actual MainWindow behind the transparent splash window.
/// </summary>
public partial class SplashWindow : Window
{
    private MainWindow? _mainWindow;
    private readonly DispatcherTimer _openingTimer;
    private readonly DispatcherTimer _finishTimer;
    private DispatcherTimer? _sweepFadeTimer;
    private bool _openingStarted;
    private bool _finished;

    public SplashWindow()
    {
        InitializeComponent();
        _openingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1050) };
        _openingTimer.Tick += (_, _) => BeginCurtainOpening();
        _finishTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1050) };
        _finishTimer.Tick += (_, _) => FinishIntro();
        Loaded += SplashWindow_Loaded;
        Closed += (_, _) => _mainWindow?.Activate();
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Prepare the live application under the intro; the splash is the only visible layer at first.
        _mainWindow = new MainWindow
        {
            Opacity = 0,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        Application.Current.MainWindow = _mainWindow;
        _mainWindow.Show();
        Topmost = true;
        Activate();

        if (!SystemParameters.ClientAreaAnimation)
        {
            FinishIntro();
            return;
        }

        Animate(LogoStage, UIElement.OpacityProperty, 0, 1, 620, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
        Animate(LogoScale, ScaleTransform.ScaleXProperty, 0.82, 1.0, 720, 0, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 });
        Animate(LogoScale, ScaleTransform.ScaleYProperty, 0.82, 1.0, 720, 0, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 });
        Animate(LogoOffset, TranslateTransform.YProperty, 16, 0, 650, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
        Animate(Backdrop, UIElement.OpacityProperty, 0.93, 1.0, 450, 0, new SineEase { EasingMode = EasingMode.EaseInOut });
        Animate(LightSweep, UIElement.OpacityProperty, 0, 0.8, 160, 120, null);
        Animate(LightSweepMove, TranslateTransform.XProperty, -350, Math.Max(ActualWidth, 1200) + 250, 1450, 120, new CubicEase { EasingMode = EasingMode.EaseInOut });
        _sweepFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1050) };
        _sweepFadeTimer.Tick += (_, _) =>
        {
            _sweepFadeTimer.Stop();
            Animate(LightSweep, UIElement.OpacityProperty, LightSweep.Opacity, 0, 400, 0, null);
        };
        _sweepFadeTimer.Start();

        if (SystemParameters.WorkArea.Width < 800 || SystemParameters.WorkArea.Height < 600)
        {
            _openingTimer.Interval = TimeSpan.FromMilliseconds(500);
        }
        _openingTimer.Start();
    }

    private void BeginCurtainOpening()
    {
        if (_openingStarted || _finished) return;
        _openingStarted = true;
        _openingTimer.Stop();

        double leftTravel = -(LeftCurtain.ActualWidth + 80);
        double rightTravel = RightCurtain.ActualWidth + 80;
        Animate(LeftCurtainMove, TranslateTransform.XProperty, 0, leftTravel, 920, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
        Animate(RightCurtainMove, TranslateTransform.XProperty, 0, rightTravel, 920, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
        Animate(Backdrop, UIElement.OpacityProperty, Backdrop.Opacity, 0, 900, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
        Animate(LogoStage, UIElement.OpacityProperty, LogoStage.Opacity, 0, 490, 200, new CubicEase { EasingMode = EasingMode.EaseIn });
        Animate(_mainWindow!, UIElement.OpacityProperty, 0, 1, 850, 100, new CubicEase { EasingMode = EasingMode.EaseOut });
        Animate(SkipButton, UIElement.OpacityProperty, 1, 0, 250, 0, null);
        _finishTimer.Start();
    }

    private static void Animate(IAnimatable target, DependencyProperty property,
        double from, double to, double durationMs, double delayMs, IEasingFunction? easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = easing
        };
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e) => FinishIntro();

    private void SplashWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        FinishIntro();
    }

    private void FinishIntro()
    {
        if (_finished) return;
        _finished = true;
        _openingTimer.Stop();
        _finishTimer.Stop();
        _sweepFadeTimer?.Stop();

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow { Opacity = 1 };
            Application.Current.MainWindow = _mainWindow;
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.BeginAnimation(UIElement.OpacityProperty, null);
            _mainWindow.Opacity = 1;
        }

        Topmost = false;
        Close();
    }
}
