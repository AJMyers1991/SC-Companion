namespace SCCompanion.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();

#if ANDROID
        StoreIcon.Source = "icon_store_play.png";
#elif IOS || MACCATALYST
        StoreIcon.Source = "icon_store_apple.png";
        StoreLabel.Text = "App Store Listing";
#elif WINDOWS
        StoreIcon.Source = "icon_store_windows.png";
        StoreLabel.Text = "Microsoft Store";
#endif
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;

        double logoSize = width * 0.8;
        LogoImage.WidthRequest = logoSize;
        LogoImage.HeightRequest = logoSize;

        double fontSize = logoSize * 0.12;
        TitleLabel.FontSize = Math.Max(24, Math.Min(fontSize, 64));
        TitleLabel.WidthRequest = width * 0.85;
    }

    private async void OnStoreClicked(object? sender, EventArgs e)
    {
#if ANDROID
        await Browser.Default.OpenAsync("https://play.google.com/store/apps/details?id=com.sccompanion.app", BrowserLaunchMode.SystemPreferred);
#elif IOS
        await Browser.Default.OpenAsync("https://apps.apple.com/app/id0000000000", BrowserLaunchMode.SystemPreferred);
#else
        await DisplayAlertAsync("Not Available", "No store listing for this platform yet.", "OK");
#endif
    }

    private async void OnFeedbackClicked(object? sender, EventArgs e)
    {
        await Browser.Default.OpenAsync("https://forms.gle/k8NqL7d4kwS7N3wy9", BrowserLaunchMode.SystemPreferred);
    }

    private async void OnCitizenClicked(object? sender, TappedEventArgs e)
        => await Browser.Default.OpenAsync(
            "https://robertsspaceindustries.com/en/citizens/turbo-virgin",
            BrowserLaunchMode.SystemPreferred);

    private async void OnOrganizationClicked(object? sender, TappedEventArgs e)
        => await Browser.Default.OpenAsync(
            "https://robertsspaceindustries.com/en/orgs/FRIG",
            BrowserLaunchMode.SystemPreferred);
}