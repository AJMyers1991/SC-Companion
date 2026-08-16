using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Crafting;
using SCCompanion.Data.Search;
using SCCompanion.Data.Ships;
using SCCompanion.Data.Trade;
using SCCompanion.Data.Wikelo;
using SCCompanion.Views;

namespace SCCompanion;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        });
        builder.Services.AddSingleton(_ =>
        {
            string databasePath = Path.Combine(
                FileSystem.AppDataDirectory,
                AppDatabase.DatabaseFilename);
            return new AppDatabase(databasePath);
        });
        builder.Services.AddSingleton(serviceProvider =>
        {
            string cacheRoot = Path.Combine(
                FileSystem.CacheDirectory,
                "downloaded-resources");
            return new DiskResourceCache(
                serviceProvider.GetRequiredService<HttpClient>(),
                cacheRoot);
        });
        builder.Services.AddSingleton<FinderSearchService>();
        builder.Services.AddSingleton<WikiSearchService>();
        builder.Services.AddSingleton<WikeloTradeService>();
        builder.Services.AddSingleton<WikeloRewardImageService>();
        builder.Services.AddSingleton<CraftingBlueprintService>();
        builder.Services.AddSingleton<CraftingImageService>();
        builder.Services.AddSingleton<UexTradeService>();
        builder.Services.AddSingleton<FleetYardsService>();
        builder.Services.AddTransient<CraftingPage>();
        builder.Services.AddTransient<FinderPage>();
        builder.Services.AddTransient<GuidesPage>();
        builder.Services.AddTransient<HangarTimerPage>();
        builder.Services.AddTransient<ShipsPage>();
        builder.Services.AddTransient<ShipDetailPage>();
        builder.Services.AddTransient<TradePage>();
        builder.Services.AddTransient<UsefulLinksPage>();
        builder.Services.AddTransient<WikeloPage>();
        builder.Services.AddTransient<WikiPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        MauiApp app = builder.Build();
        Services = app.Services;
        return app;
    }
}