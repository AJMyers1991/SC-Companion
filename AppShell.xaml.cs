using SCCompanion.Views;

namespace SCCompanion;

public partial class AppShell : Shell
{
    private const string HomeRoute = "//main/home";
    private const string MoreRoute = "//main/more";
    private bool _isNavigatingHome;
    private bool _isNavigatingMore;

    public AppShell()
    {
        InitializeComponent();

        ShipsShellContent.Content = MauiProgram.Services.GetRequiredService<ShipsPage>();
        TradeShellContent.Content = MauiProgram.Services.GetRequiredService<TradePage>();
        CraftingShellContent.Content = MauiProgram.Services.GetRequiredService<CraftingPage>();

        Routing.RegisterRoute(nameof(WikiPage), typeof(WikiPage));
        Routing.RegisterRoute(nameof(GuidesPage), typeof(GuidesPage));
        Routing.RegisterRoute(nameof(UsefulLinksPage), typeof(UsefulLinksPage));
        Routing.RegisterRoute(nameof(WikeloPage), typeof(WikeloPage));
        Routing.RegisterRoute(nameof(HangarTimerPage), typeof(HangarTimerPage));
        Routing.RegisterRoute(nameof(FinderPage), typeof(FinderPage));
    }

    public async Task ReturnToMoreRootAsync()
    {
        if (_isNavigatingMore)
        {
            return;
        }

        _isNavigatingMore = true;
        try
        {
            while (Navigation.ModalStack.Count > 0)
            {
                await Navigation.PopModalAsync(animated: false);
            }

            await GoToAsync(MoreRoute);
        }
        finally
        {
            _isNavigatingMore = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Let Shell dismiss the topmost modal or pop one pushed page at a time.
        // This also covers future screens nested below the current sub-screen.
        if (Navigation.ModalStack.Count > 0 || Navigation.NavigationStack.Count > 1)
        {
            return base.OnBackButtonPressed();
        }

        string? rootRoute = CurrentItem?.CurrentItem?.CurrentItem?.Route;

        // Returning false from the Home root allows Android to close the activity normally.
        if (string.Equals(rootRoute, "home", StringComparison.OrdinalIgnoreCase))
        {
            return base.OnBackButtonPressed();
        }

        // At any other root tab, consume Back and return to Home instead of closing.
        if (!_isNavigatingHome)
        {
            _isNavigatingHome = true;
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    await GoToAsync(HomeRoute);
                }
                finally
                {
                    _isNavigatingHome = false;
                }
            });
        }

        return true;
    }
}
