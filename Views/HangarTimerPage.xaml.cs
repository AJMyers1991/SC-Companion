using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using SCCompanion.Data.Hangar;

namespace SCCompanion.Views;

public partial class HangarTimerPage : ContentPage
{
    private const string TimerDataUrl = "https://contestedzonetimers.com/lib/cfg.dat";
    private const string AttributionUrl = "https://contestedzonetimers.com";

    private static readonly Brush RedLightBrush =
        new SolidColorBrush(Color.FromArgb("#FF1744"));
    private static readonly Brush GreenLightBrush =
        new SolidColorBrush(Color.FromArgb("#00C853"));
    private static readonly Brush DarkLightBrush =
        new SolidColorBrush(Color.FromArgb("#424242"));

    private readonly HttpClient _httpClient;
    private readonly Ellipse[] _lights;

    private CancellationTokenSource? _pageCancellation;
    private CancellationTokenSource? _clockCancellation;
    private long? _cycleStartUnixSeconds;
    private bool _isLoading;

    public HangarTimerPage(HttpClient httpClient)
    {
        _httpClient = httpClient;

        InitializeComponent();
        _lights = [Light1, Light2, Light3, Light4, Light5];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();

        if (_cycleStartUnixSeconds is null)
        {
            await LoadTimerAsync(_pageCancellation.Token);
            return;
        }

        ShowTimer();
        StartClock(_pageCancellation.Token);
    }

    protected override void OnDisappearing()
    {
        _clockCancellation?.Cancel();
        _clockCancellation?.Dispose();
        _clockCancellation = null;

        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;

        base.OnDisappearing();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        if (_pageCancellation is null || _pageCancellation.IsCancellationRequested)
        {
            _pageCancellation?.Dispose();
            _pageCancellation = new CancellationTokenSource();
        }

        await LoadTimerAsync(_pageCancellation.Token);
    }

    private async void OnAttributionTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(
                new Uri(AttributionUrl),
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Unable to open timer attribution link: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open contestedzonetimers.com.",
                "OK");
        }
    }

    private async Task LoadTimerAsync(CancellationToken cancellationToken)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        ShowLoading();

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                TimerDataUrl,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseText = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (!long.TryParse(
                    responseText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long cycleStartUnixSeconds) ||
                cycleStartUnixSeconds <= 0)
            {
                ShowError("Invalid timer data received");
                return;
            }

            _cycleStartUnixSeconds = cycleStartUnixSeconds;
            ShowTimer();
            StartClock(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navigation away from the page cancels in-flight network and clock work.
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to fetch Executive Hangar timer: {exception}");
            ShowError("Unable to fetch timer data");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void StartClock(CancellationToken pageCancellationToken)
    {
        _clockCancellation?.Cancel();
        _clockCancellation?.Dispose();
        _clockCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageCancellationToken);
        _ = RunClockAsync(_clockCancellation.Token);
    }

    private async Task RunClockAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateTimerDisplay();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the user leaves the page or retries loading.
        }
    }

    private void UpdateTimerDisplay()
    {
        if (_cycleStartUnixSeconds is not long cycleStartUnixSeconds)
        {
            return;
        }

        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            cycleStartUnixSeconds,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        string countdown = HangarTimerCalculator.FormatCountdown(snapshot.SecondsRemaining);
        switch (snapshot.Phase)
        {
            case HangarPhase.Closed:
                StatusLabel.Text = "Hangar Closed";
                StatusLabel.TextColor = Color.FromArgb("#FF1744");
                CountdownLabel.Text = $"Opens in {countdown}";
                break;
            case HangarPhase.Open:
                StatusLabel.Text = "Hangar Open";
                StatusLabel.TextColor = Color.FromArgb("#00C853");
                CountdownLabel.Text = $"Resets in {countdown}";
                break;
            case HangarPhase.Resetting:
                StatusLabel.Text = "Hangar Resetting";
                StatusLabel.TextColor = Color.FromArgb("#FFAB00");
                CountdownLabel.Text = $"Reopens in {countdown}";
                break;
            default:
                throw new InvalidOperationException($"Unknown hangar phase: {snapshot.Phase}");
        }

        for (int index = 0; index < _lights.Length; index++)
        {
            _lights[index].Fill = snapshot.Lights[index] switch
            {
                HangarLightColor.Red => RedLightBrush,
                HangarLightColor.Green => GreenLightBrush,
                _ => DarkLightBrush
            };
        }

        SemanticProperties.SetDescription(
            TimerPanel,
            $"{StatusLabel.Text}. {CountdownLabel.Text}.");
    }

    private void ShowLoading()
    {
        LoadingPanel.IsVisible = true;
        ErrorPanel.IsVisible = false;
        TimerPanel.IsVisible = false;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        LoadingPanel.IsVisible = false;
        ErrorPanel.IsVisible = true;
        TimerPanel.IsVisible = false;
    }

    private void ShowTimer()
    {
        LoadingPanel.IsVisible = false;
        ErrorPanel.IsVisible = false;
        TimerPanel.IsVisible = true;
        UpdateTimerDisplay();
    }
}