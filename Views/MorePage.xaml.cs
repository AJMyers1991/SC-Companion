namespace SCCompanion.Views;

public partial class MorePage : ContentPage
{
    public MorePage()
    {
        InitializeComponent();
    }

    private async void OnFinderClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(FinderPage));

    private async void OnHangarTimerClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(HangarTimerPage));

    private async void OnWikeloClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(WikeloPage));

    private async void OnWikiClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(WikiPage));

    private async void OnGuidesClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(GuidesPage));

    private async void OnUsefulLinksClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(UsefulLinksPage));
}