using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;

namespace SCCompanion;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity,
    NavigationBarView.IOnItemReselectedListener
{
    private BottomNavigationView? _bottomNavigationView;

    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);
        Window?.DecorView?.Post(AttachTabReselectionListener);
    }

    public override bool DispatchTouchEvent(MotionEvent? touchEvent)
    {
        if (touchEvent?.Action == MotionEventActions.Down &&
            CurrentFocus is EditText focusedInput)
        {
            Android.Graphics.Rect inputBounds = new();
            focusedInput.GetGlobalVisibleRect(inputBounds);

            bool tappedFocusedInput = inputBounds.Contains(
                (int)touchEvent.RawX,
                (int)touchEvent.RawY);

            if (!tappedFocusedInput)
            {
                focusedInput.ClearFocus();

                if (GetSystemService(Context.InputMethodService) is InputMethodManager keyboard)
                {
                    keyboard.HideSoftInputFromWindow(
                        focusedInput.WindowToken,
                        HideSoftInputFlags.None);
                }
            }
        }

        return base.DispatchTouchEvent(touchEvent);
    }

    public void OnNavigationItemReselected(IMenuItem item)
    {
        if (!string.Equals(
                item.TitleFormatted?.ToString(),
                "More",
                StringComparison.OrdinalIgnoreCase) ||
            Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page
                is not AppShell shell)
        {
            return;
        }

        shell.Dispatcher.Dispatch(async () => await shell.ReturnToMoreRootAsync());
    }

    private void AttachTabReselectionListener()
    {
        _bottomNavigationView = FindDescendant<BottomNavigationView>(Window?.DecorView);
        _bottomNavigationView?.SetOnItemReselectedListener(this);
    }

    private static T? FindDescendant<T>(Android.Views.View? view)
        where T : Android.Views.View
    {
        if (view is T match)
        {
            return match;
        }

        if (view is not ViewGroup group)
        {
            return null;
        }

        for (int index = 0; index < group.ChildCount; index++)
        {
            T? descendant = FindDescendant<T>(group.GetChildAt(index));
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}