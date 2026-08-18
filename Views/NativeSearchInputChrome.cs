namespace SCCompanion.Views;

/// <summary>
/// Removes native iOS text-field chrome when a MAUI search input is already
/// presented by an intentionally styled outer surface.
/// </summary>
internal static class NativeSearchInputChrome
{
    public static void RemoveIosChrome(object? sender)
    {
#if IOS
        UIKit.UITextField? nativeTextField = sender switch
        {
            Entry entry => entry.Handler?.PlatformView as UIKit.UITextField,
            SearchBar searchBar when searchBar.Handler?.PlatformView is UIKit.UISearchBar nativeSearchBar =>
                nativeSearchBar.SearchTextField,
            _ => null,
        };

        if (nativeTextField is null)
        {
            return;
        }

        nativeTextField.BorderStyle = UIKit.UITextBorderStyle.None;
        nativeTextField.Layer.BorderWidth = 0;
#endif
    }
}
