namespace SCCompanion;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += (_, _) => StartFinderPreload();
        return window;
    }

    private static void StartFinderPreload()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await MauiProgram.Services
                    .GetRequiredService<SCCompanion.Data.Search.FinderSearchService>()
                    .PreloadAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Finder index preload failed: {exception}");
            }
        });
    }
}