using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Guides;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class GuideImageViewerPage : ContentPage
{
    private const double MinimumScale = 1;
    private const double MaximumScale = 8;

    private readonly GuideDefinition _guide;
    private readonly DiskResourceCache _resourceCache;
    private CancellationTokenSource? _pageLoadCancellation;
    private int _currentPageIndex;
    private bool _hasShownRotationToast;
    private double _currentScale = MinimumScale;
    private double _pinchStartScale = MinimumScale;
    private double _pinchStartX;
    private double _pinchStartY;
    private double _panStartX;
    private double _panStartY;
    private bool _isPinching;
#if ANDROID
    private Android.Graphics.Bitmap? _androidBitmap;
#endif

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
        ClearGuideImage();
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
        ClearGuideImage();
        GuideImage.IsVisible = false;
        ImageErrorLabel.IsVisible = false;
        ResetImageTransform();
        GuideLoadingIndicator.IsRunning = true;

        try
        {
            string cachedPath = await _resourceCache.GetOrDownloadAsync(
                remoteUri,
                "guides",
                relativePath,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await SetCachedImageAsync(cachedPath, cancellationToken);
            GuideImage.IsVisible = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to cache guide page: {exception}");
            ClearGuideImage();
            GuideImage.IsVisible = false;
            ImageErrorLabel.IsVisible = true;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                GuideLoadingIndicator.IsRunning = false;
            }
        }

        SemanticProperties.SetDescription(
            GuideImage,
            $"{_guide.Name}, page {_currentPageIndex + 1} of {_guide.PagePaths.Count}. Pinch to zoom and drag with one finger to pan.");
    }

    private async Task SetCachedImageAsync(
        string cachedPath,
        CancellationToken cancellationToken)
    {
#if ANDROID
        // MAUI routes both stream and absolute file sources through Glide's 5 MiB
        // rewind buffer. Decode the cached file directly to support the 7–10 MiB guides.
        Android.Graphics.Bitmap? bitmap = await Task.Run(
            () => Android.Graphics.BitmapFactory.DecodeFile(cachedPath),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (bitmap is null)
        {
            throw new InvalidDataException("Android could not decode the cached guide image.");
        }

        if (GuideImage.Handler?.PlatformView is not Android.Widget.ImageView nativeImageView)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("The Android guide image view is unavailable.");
        }

        _androidBitmap?.Dispose();
        _androidBitmap = bitmap;
        nativeImageView.SetImageBitmap(bitmap);
#else
        // Stream loading avoids treating an absolute cache path as a bundled iOS resource.
        GuideImage.Source = ImageSource.FromStream(streamCancellationToken =>
        {
            streamCancellationToken.ThrowIfCancellationRequested();
            Stream stream = new FileStream(
                cachedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return Task.FromResult(stream);
        });
        await Task.CompletedTask;
#endif
    }

    private void ClearGuideImage()
    {
#if ANDROID
        if (GuideImage.Handler?.PlatformView is Android.Widget.ImageView nativeImageView)
        {
            nativeImageView.SetImageDrawable(null);
        }

        _androidBitmap?.Dispose();
        _androidBitmap = null;
#endif
        GuideImage.Source = null;
    }

    private void OnImagePinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _isPinching = true;
                _pinchStartScale = _currentScale;
                _pinchStartX = GuideImage.TranslationX;
                _pinchStartY = GuideImage.TranslationY;
                break;

            case GestureStatus.Running:
                if (GuideImage.Width <= 0 || GuideImage.Height <= 0 ||
                    ImageViewport.Width <= 0 || ImageViewport.Height <= 0)
                {
                    return;
                }

                _currentScale = Math.Clamp(
                    _currentScale + ((e.Scale - 1) * _pinchStartScale),
                    MinimumScale,
                    MaximumScale);

                double renderedX = GuideImage.X + _pinchStartX;
                double renderedY = GuideImage.Y + _pinchStartY;
                double deltaX = renderedX / ImageViewport.Width;
                double deltaY = renderedY / ImageViewport.Height;
                double deltaWidth = ImageViewport.Width / (GuideImage.Width * _pinchStartScale);
                double deltaHeight = ImageViewport.Height / (GuideImage.Height * _pinchStartScale);
                double originX = (e.ScaleOrigin.X - deltaX) * deltaWidth;
                double originY = (e.ScaleOrigin.Y - deltaY) * deltaHeight;
                double targetX = _pinchStartX -
                    ((originX * GuideImage.Width) * (_currentScale - _pinchStartScale));
                double targetY = _pinchStartY -
                    ((originY * GuideImage.Height) * (_currentScale - _pinchStartScale));

                ApplyImageTransform(targetX, targetY);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isPinching = false;
                ApplyImageTransform(GuideImage.TranslationX, GuideImage.TranslationY);
                break;
        }
    }

    private void OnImagePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isPinching || _currentScale <= MinimumScale)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = GuideImage.TranslationX;
                _panStartY = GuideImage.TranslationY;
                break;

            case GestureStatus.Running:
                ApplyImageTransform(
                    _panStartX + e.TotalX,
                    _panStartY + e.TotalY);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                ApplyImageTransform(GuideImage.TranslationX, GuideImage.TranslationY);
                break;
        }
    }

    private void OnImageViewportSizeChanged(object? sender, EventArgs e)
    {
        ApplyImageTransform(GuideImage.TranslationX, GuideImage.TranslationY);
    }

    private void ApplyImageTransform(double targetX, double targetY)
    {
        if (_currentScale <= MinimumScale ||
            GuideImage.Width <= 0 || GuideImage.Height <= 0)
        {
            _currentScale = MinimumScale;
            GuideImage.Scale = MinimumScale;
            GuideImage.TranslationX = 0;
            GuideImage.TranslationY = 0;
            return;
        }

        double minimumX = -GuideImage.Width * (_currentScale - MinimumScale);
        double minimumY = -GuideImage.Height * (_currentScale - MinimumScale);

        GuideImage.Scale = _currentScale;
        GuideImage.TranslationX = Math.Clamp(targetX, minimumX, 0);
        GuideImage.TranslationY = Math.Clamp(targetY, minimumY, 0);
    }

    private void ResetImageTransform()
    {
        _currentScale = MinimumScale;
        _pinchStartScale = MinimumScale;
        _pinchStartX = 0;
        _pinchStartY = 0;
        _panStartX = 0;
        _panStartY = 0;
        _isPinching = false;
        GuideImage.Scale = MinimumScale;
        GuideImage.TranslationX = 0;
        GuideImage.TranslationY = 0;
    }
}
