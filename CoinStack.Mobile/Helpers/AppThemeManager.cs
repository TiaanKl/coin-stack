namespace CoinStack.Mobile.Helpers;

/// <summary>
/// Manages application‑wide theme switching (Light / Dark / System).
/// Persists user preference via <see cref="Preferences"/>.
/// </summary>
public static class AppThemeManager
{
    private const string ThemePrefKey = "app_theme";

    /// <summary>Light, Dark, or System.</summary>
    public enum ThemeOption { System, Light, Dark }

    /// <summary>Read the persisted preference (defaults to System).</summary>
    public static ThemeOption GetSavedTheme()
    {
        var saved = Preferences.Get(ThemePrefKey, "System");
        return Enum.TryParse<ThemeOption>(saved, out var opt) ? opt : ThemeOption.System;
    }

    /// <summary>Persist and immediately apply the selected theme.</summary>
    public static void SetTheme(ThemeOption option)
    {
        Preferences.Set(ThemePrefKey, option.ToString());
        ApplyTheme(option);
    }

    /// <summary>
    /// Applies the theme to <see cref="AppColors"/>, MAUI resource dictionaries
    /// and the <see cref="Application.UserAppTheme"/>.
    /// Call once at startup and again whenever the user changes the preference.
    /// </summary>
    public static void ApplyTheme(ThemeOption? option = null)
    {
        option ??= GetSavedTheme();

        var effectiveTheme = option switch
        {
            ThemeOption.Light => AppTheme.Light,
            ThemeOption.Dark => AppTheme.Dark,
            _ => Application.Current?.PlatformAppTheme ?? AppTheme.Light
        };

        // 1) Update static colour tokens
        if (effectiveTheme == AppTheme.Dark)
            AppColors.ApplyDark();
        else
            AppColors.ApplyLight();

        // 2) Tell MAUI which theme we want (for AppThemeBinding in XAML)
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = option == ThemeOption.System
                ? AppTheme.Unspecified
                : effectiveTheme;

            // 3) Push colours into the XAML resource dictionary so styles pick them up
            var res = Application.Current.Resources;
            SetColor(res, "Dark", AppColors.Dark);
            SetColor(res, "Accent", AppColors.Accent);
            SetColor(res, "AccentDark", AppColors.AccentDark);
            SetColor(res, "Background", AppColors.Background);
            SetColor(res, "Surface", AppColors.Surface);
            SetColor(res, "Border", AppColors.Border);
            SetColor(res, "Muted", AppColors.Muted);
            SetColor(res, "Success", AppColors.Success);
            SetColor(res, "Danger", AppColors.Danger);
            SetColor(res, "TabUnselected", AppColors.TabUnselected);
            SetColor(res, "SurfaceDim", AppColors.SurfaceDim);
            SetColor(res, "SurfaceContainer", AppColors.SurfaceContainer);
            SetColor(res, "BgSuccess", AppColors.BgSuccess);
            SetColor(res, "BgDanger", AppColors.BgDanger);
            SetColor(res, "BgWarning", AppColors.BgWarning);
            SetColor(res, "AccentIndigo", AppColors.AccentIndigo);
            SetColor(res, "BgIndigo", AppColors.BgIndigo);
            SetColor(res, "AccentPurple", AppColors.AccentPurple);
            SetColor(res, "BgPurple", AppColors.BgPurple);
            SetColor(res, "Warning", AppColors.Warning);
            SetColor(res, "NavIconBg", AppColors.NavIconBg);
            SetColor(res, "TextOnDark", AppColors.TextOnDark);
            SetColor(res, "BgSuccessStrong", AppColors.BgSuccessStrong);
            SetColor(res, "BorderSuccess", AppColors.BorderSuccess);
            SetColor(res, "BgUnlocked", AppColors.BgUnlocked);
            SetColor(res, "TextSecondary", AppColors.TextSecondary);
            SetColor(res, "ButtonBg", AppColors.ButtonBg);
            SetColor(res, "ButtonText", AppColors.ButtonText);
        }
    }

    private static void SetColor(ResourceDictionary dict, string key, Color color)
    {
        dict[key] = color;
    }
}
