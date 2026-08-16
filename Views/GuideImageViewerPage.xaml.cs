using System.Net;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Guides;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class GuideImageViewerPage : ContentPage
{
    private readonly GuideDefinition _guide;
    private readonly DiskResourceCache _resourceCache;
    private CancellationTokenSource? _pageLoadCancellation;
    private int _currentPageIndex;
    private bool _hasShownRotationToast;

    public GuideImageViewerPage(
        GuideDefinition guide,
        DiskResourceCache resourceCache)
    {
        _guide = guide;
        _resourceCache = resourceCache;

        InitializeComponent();
        GuideTitleLabel.Text = guide.Name;
        AttributionButton.Text = guide.Attribution;
        AttributionButton.IsVisible = !string.IsNullOrWhiteSpace(guide.Attribution);
        NavigationPanel.IsVisible = guide.PagePaths.Count > 1;
        ImageWebView.HandlerChanged += OnImageWebViewHandlerChanged;

        _ = LoadCurrentPageAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasShownRotationToast ||
            DeviceDisplay.Current.MainDisplayInfo.Orientation != DisplayOrientation.Portrait)
        {
            return;
        }

        _hasShownRotationToast = true;
        await Toast.Make(
                "Rotate device for landscape mode.",
                ToastDuration.Short,
                textSize: 14)
            .Show();
    }

    protected override void OnDisappearing()
    {
        _pageLoadCancellation?.Cancel();
        _pageLoadCancellation?.Dispose();
        _pageLoadCancellation = null;
        base.OnDisappearing();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnAttributionClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_guide.AttributionUrl))
        {
            return;
        }

        try
        {
            await Browser.Default.OpenAsync(
                new Uri(_guide.AttributionUrl),
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open guide attribution: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open the guide attribution.",
                "OK");
        }
    }

    private void OnPreviousClicked(object? sender, EventArgs e)
    {
        _currentPageIndex = GuidePageNavigator.Previous(
            _currentPageIndex,
            _guide.PagePaths.Count);
        _ = LoadCurrentPageAsync();
    }

    private void OnNextClicked(object? sender, EventArgs e)
    {
        _currentPageIndex = GuidePageNavigator.Next(
            _currentPageIndex,
            _guide.PagePaths.Count);
        _ = LoadCurrentPageAsync();
    }

    private async Task LoadCurrentPageAsync()
    {
        _pageLoadCancellation?.Cancel();
        _pageLoadCancellation?.Dispose();
        _pageLoadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _pageLoadCancellation.Token;

        string relativePath = _guide.PagePaths[_currentPageIndex];
        var remoteUri = new Uri(new Uri(GuideCatalog.BaseUrl), relativePath);

        PageLabel.Text = $"Page {_currentPageIndex + 1} of {_guide.PagePaths.Count}";
        ImageWebView.Source = new HtmlWebViewSource
        {
            Html = BuildLoadingHtml()
        };
        GuideLoadingIndicator.IsRunning = true;

        try
        {
            string cachedPath = await _resourceCache.GetOrDownloadAsync(
                remoteUri,
                "guides",
                relativePath,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string cacheDirectory = Path.GetDirectoryName(cachedPath) ?? string.Empty;
            string cacheBaseUrl = new Uri(
                cacheDirectory + Path.DirectorySeparatorChar).AbsoluteUri;
            ImageWebView.Source = new HtmlWebViewSource
            {
                Html = BuildImageViewerHtml(Path.GetFileName(cachedPath)),
                BaseUrl = cacheBaseUrl
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to cache guide page: {exception}");
            ImageWebView.Source = new HtmlWebViewSource
            {
                Html = BuildErrorHtml()
            };
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                GuideLoadingIndicator.IsRunning = false;
            }
        }

        SemanticProperties.SetDescription(
            ImageWebView,
            $"{_guide.Name}, page {_currentPageIndex + 1} of {_guide.PagePaths.Count}. Pinch to zoom and drag to pan.");
    }

    private static string BuildLoadingHtml()
    {
        return "<html><body style='margin:0;background:#000;color:#d0d0d0;display:flex;align-items:center;justify-content:center;font:14px sans-serif'>Loading and caching full-resolution guide…</body></html>";
    }

    private static string BuildErrorHtml()
    {
        return "<html><body style='margin:0;background:#000;color:#d0d0d0;display:flex;align-items:center;justify-content:center;text-align:center;padding:24px;font:14px sans-serif'>Unable to load this guide image. Check your connection and try again.</body></html>";
    }

    private string BuildImageViewerHtml(string pageUrl)
    {
        string encodedUrl = WebUtility.HtmlEncode(pageUrl);
        string encodedDescription = WebUtility.HtmlEncode(
            $"{_guide.Name} page {_currentPageIndex + 1}");

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0, minimum-scale=0.25, maximum-scale=12.0, user-scalable=yes">
              <style>
                html, body {
                  width: 100%;
                  height: 100%;
                  margin: 0;
                  padding: 0;
                  overflow: auto;
                  overscroll-behavior: contain;
                  background: #000;
                }
                body {
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  touch-action: pan-x pan-y pinch-zoom;
                  color: #d0d0d0;
                  font: 14px sans-serif;
                }
                #status {
                  position: fixed;
                  inset: 0;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  text-align: center;
                  padding: 24px;
                  background: #000;
                  z-index: 2;
                }
                #guide {
                  width: auto;
                  height: auto;
                  max-width: 100vw;
                  max-height: 100vh;
                  object-fit: contain;
                  display: block;
                  -webkit-user-select: none;
                  user-select: none;
                  -webkit-user-drag: none;
                }
                body.mouse-pan-enabled {
                  cursor: grab;
                }
                body.mouse-panning {
                  cursor: grabbing;
                }
              </style>
            </head>
            <body>
              <div id="status">Loading full-resolution guide…</div>
              <img id="guide"
                   src="{{encodedUrl}}"
                   alt="{{encodedDescription}}"
                   draggable="false"
                   onload="document.getElementById('status').style.display='none'"
                   onerror="document.getElementById('status').textContent='Unable to load this guide image.'">
              <script>
                let isMousePanning = false;
                let lastPointerX = 0;
                let lastPointerY = 0;
                let isTouchPanning = false;
                let lastTouchX = 0;
                let lastTouchY = 0;

                document.body.classList.add('mouse-pan-enabled');

                document.addEventListener('pointerdown', event => {
                  if (event.pointerType !== 'mouse' || event.button !== 0) {
                    return;
                  }

                  isMousePanning = true;
                  lastPointerX = event.clientX;
                  lastPointerY = event.clientY;
                  document.body.classList.add('mouse-panning');
                  document.body.setPointerCapture(event.pointerId);
                  event.preventDefault();
                });

                document.addEventListener('pointermove', event => {
                  if (!isMousePanning || event.pointerType !== 'mouse') {
                    return;
                  }

                  window.scrollBy(
                    lastPointerX - event.clientX,
                    lastPointerY - event.clientY);
                  lastPointerX = event.clientX;
                  lastPointerY = event.clientY;
                  event.preventDefault();
                });

                const stopMousePanning = event => {
                  if (!isMousePanning) {
                    return;
                  }

                  isMousePanning = false;
                  document.body.classList.remove('mouse-panning');
                  if (document.body.hasPointerCapture(event.pointerId)) {
                    document.body.releasePointerCapture(event.pointerId);
                  }
                };

                document.addEventListener('pointerup', stopMousePanning);
                document.addEventListener('pointercancel', stopMousePanning);

                document.addEventListener('touchstart', event => {
                  if (event.touches.length !== 1) {
                    isTouchPanning = false;
                    return;
                  }

                  isTouchPanning = true;
                  lastTouchX = event.touches[0].clientX;
                  lastTouchY = event.touches[0].clientY;
                }, { passive: true });

                document.addEventListener('touchmove', event => {
                  if (!isTouchPanning || event.touches.length !== 1) {
                    isTouchPanning = false;
                    return;
                  }

                  const touch = event.touches[0];
                  window.scrollBy(
                    lastTouchX - touch.clientX,
                    lastTouchY - touch.clientY);
                  lastTouchX = touch.clientX;
                  lastTouchY = touch.clientY;
                  event.preventDefault();
                }, { passive: false });

                const stopTouchPanning = () => {
                  isTouchPanning = false;
                };

                document.addEventListener('touchend', stopTouchPanning, { passive: true });
                document.addEventListener('touchcancel', stopTouchPanning, { passive: true });
              </script>
            </body>
            </html>
            """;
    }

    private void OnImageWebViewHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (ImageWebView.Handler?.PlatformView is not Android.Webkit.WebView nativeWebView)
        {
            return;
        }

        nativeWebView.Settings.JavaScriptEnabled = true;
        nativeWebView.Settings.AllowFileAccess = true;
        nativeWebView.Settings.SetSupportZoom(true);
        nativeWebView.Settings.BuiltInZoomControls = true;
        nativeWebView.Settings.DisplayZoomControls = false;
        nativeWebView.Settings.UseWideViewPort = true;
        nativeWebView.Settings.LoadWithOverviewMode = true;
        nativeWebView.SetBackgroundColor(Android.Graphics.Color.Black);
#endif
    }
}
