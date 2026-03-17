using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StandupReminder.App;

public partial class FirstRunSplashWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public FirstRunSplashWindow()
    {
        InitializeComponent();
        LoadSplashImage();

        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(7)
        };

        _closeTimer.Tick += OnCloseTimerTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _closeTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _closeTimer.Stop();
        _closeTimer.Tick -= OnCloseTimerTick;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Close();
    }

    private void LoadSplashImage()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "wizard-side-dark.bmp");

        if (!File.Exists(assetPath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(assetPath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        SplashImage.Source = image;
    }
}
