namespace SCCompanion.Controls;

public partial class RocketLoadingIndicator : ContentView
{
    private Animation? _orbitAnimation;

    public RocketLoadingIndicator()
    {
        InitializeComponent();
        IsVisible = false;
    }

    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
        nameof(IsRunning),
        typeof(bool),
        typeof(RocketLoadingIndicator),
        false,
        propertyChanged: OnIsRunningChanged);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message),
        typeof(string),
        typeof(RocketLoadingIndicator),
        "Loading...");

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnIsRunningChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RocketLoadingIndicator control)
        {
            control.OnIsRunningChanged((bool)newValue);
        }
    }

    private void OnIsRunningChanged(bool isRunning)
    {
        IsVisible = isRunning;
        
        if (isRunning)
        {
            StartRocketAnimation();
        }
        else
        {
            StopRocketAnimation();
        }
    }

    private void StartRocketAnimation()
    {
        StopRocketAnimation(); // Ensure any existing animation is stopped
        
        // Create smooth circular orbit animation
        _orbitAnimation = new Animation(v =>
        {
            double angle = v * 2 * Math.PI; // Full rotation (0 to 2π)
            double radius = 50; // Orbit radius
            
            // Calculate position on circle
            double x = radius * Math.Cos(angle);
            double y = radius * Math.Sin(angle);
            
            // Update rocket position
            RocketLabel.TranslationX = x;
            RocketLabel.TranslationY = y;
            RocketLabel.Rotation = (v * 360 + 135) % 360;
        });
        
        // Start infinite smooth orbit (3 second duration, linear easing)
        _orbitAnimation.Commit(
            this,
            "RocketOrbit",
            16,
            3000,
            Easing.Linear,
            null,
            () => IsRunning);
    }

    private void StopRocketAnimation()
    {
        if (_orbitAnimation != null)
        {
            this.AbortAnimation("RocketOrbit");
            _orbitAnimation = null;
        }
        
        // Reset to initial position (right side of orbit)
        RocketLabel.TranslationX = 50;
        RocketLabel.TranslationY = 0;
        RocketLabel.Rotation = 135;
    }
}