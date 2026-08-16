namespace SCCompanion.Views;

/// <summary>
/// Displays a selected Finder or Wiki result inside the application.
/// </summary>
public partial class SearchResultBrowserPage : ContentPage
{
    private readonly Uri _sourceUri;
    private bool _isClosing;

    public SearchResultBrowserPage(string title, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(sourceUri);

        _sourceUri = sourceUri;

        InitializeComponent();
        PageTitleLabel.Text = title.Trim();
        SizeChanged += OnPageSizeChanged;
        BrowserWebView.Source = _sourceUri.AbsoluteUri;
    }

    protected override bool OnBackButtonPressed()
    {
        CloseAsync();
        return true;
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (BrowserWebView.Handler?.PlatformView is Android.Webkit.WebView nativeWebView)
        {
            nativeWebView.StopLoading();
        }
#endif
        base.OnDisappearing();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        PortraitToolbar.IsVisible = Height >= Width;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        CloseAsync();
    }

    private async void CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        try
        {
            await Navigation.PopModalAsync();
        }
        finally
        {
            _isClosing = false;
        }
    }

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;
        ErrorLabel.IsVisible = e.Result != WebNavigationResult.Success;
    }

    private void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (BrowserWebView.Handler?.PlatformView is not Android.Webkit.WebView nativeWebView)
        {
            return;
        }

        nativeWebView.Settings.JavaScriptEnabled = true;
        nativeWebView.Settings.DomStorageEnabled = true;
        nativeWebView.Settings.SetSupportZoom(true);
        nativeWebView.Settings.BuiltInZoomControls = true;
        nativeWebView.Settings.DisplayZoomControls = false;
        nativeWebView.Settings.UseWideViewPort = true;
        nativeWebView.Settings.LoadWithOverviewMode = true;
#endif
    }
}
