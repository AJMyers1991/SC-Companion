using System.Net;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace SCCompanion.Views;

public partial class ShipImageViewerPage : ContentPage
{
    private readonly string _imagePath;

    public ShipImageViewerPage(string title, string imagePath)
    {
        _imagePath = imagePath;
        InitializeComponent();
        TitleLabel.Text = title;
        ImageWebView.HandlerChanged += OnHandlerChanged;
        LoadImage();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateOrientation();
        if (DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Portrait)
            await Toast.Make("Rotate phone for full screen", ToastDuration.Short).Show();
        DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayChanged;
    }

    protected override void OnDisappearing()
    {
        DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayChanged;
        base.OnDisappearing();
    }

    private void OnDisplayChanged(object? sender, DisplayInfoChangedEventArgs e) => Dispatcher.Dispatch(UpdateOrientation);
    private void UpdateOrientation()
    {
        bool isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;
        Toolbar.IsVisible = !isLandscape;
        LandscapeBackButton.IsVisible = isLandscape;
    }
    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private void LoadImage()
    {
        string directory = Path.GetDirectoryName(_imagePath) ?? string.Empty;
        string baseUrl = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Host = string.Empty,
            Path = directory + Path.DirectorySeparatorChar
        }.Uri.AbsoluteUri;

        ImageWebView.Source = new HtmlWebViewSource
        {
            BaseUrl = baseUrl,
            Html = "<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width,initial-scale=1,minimum-scale=.25,maximum-scale=12,user-scalable=yes\">" +
                   "<style>html,body{margin:0;width:100%;height:100%;background:#000;overflow:auto}body{display:flex;align-items:center;justify-content:center}img{max-width:100vw;max-height:100vh;object-fit:contain}</style></head>" +
                   $"<body><img src=\"{WebUtility.HtmlEncode(Path.GetFileName(_imagePath))}\" alt=\"Ship image\"></body></html>"
        };
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (ImageWebView.Handler?.PlatformView is Android.Webkit.WebView view)
        {
            view.Settings.AllowFileAccess = true; view.Settings.SetSupportZoom(true); view.Settings.BuiltInZoomControls = true;
            view.Settings.DisplayZoomControls = false; view.Settings.UseWideViewPort = true; view.Settings.LoadWithOverviewMode = true;
        }
#endif
    }
}
