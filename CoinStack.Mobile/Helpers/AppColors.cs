namespace CoinStack.Mobile.Helpers;

/// <summary>
/// Design‑system colour tokens. All pages should reference these instead of
/// hardcoded hex values. The palette flips between Light and Dark automatically
/// when <see cref="AppThemeManager.ApplyTheme"/> is called.
/// </summary>
public static class AppColors
{
    // ── Core tokens ─────────────────────────────────────────────────
    public static Color Dark { get; private set; } = default!;
    public static Color Accent { get; private set; } = default!;
    public static Color AccentDark { get; private set; } = default!;
    public static Color Background { get; private set; } = default!;
    public static Color Surface { get; private set; } = default!;
    public static Color Border { get; private set; } = default!;
    public static Color Muted { get; private set; } = default!;
    public static Color Success { get; private set; } = default!;
    public static Color Danger { get; private set; } = default!;
    public static Color TabUnselected { get; private set; } = default!;

    // ── Surface variants ────────────────────────────────────────────
    public static Color SurfaceDim { get; private set; } = default!;       // #F9FAFB / #1e1e1e  — list‑item / row bg
    public static Color SurfaceContainer { get; private set; } = default!; // #F3F4F6 / #2a2a2a  — neutral badge bg

    // ── Status badge backgrounds ────────────────────────────────────
    public static Color BgSuccess { get; private set; } = default!;        // #ECFDF5 / #0d2818
    public static Color BgDanger { get; private set; } = default!;         // #FEF2F2 / #2d1111
    public static Color BgWarning { get; private set; } = default!;        // #FFFBEB / #2d2611

    // ── Feature accent (indigo) ─────────────────────────────────────
    public static Color AccentIndigo { get; private set; } = default!;     // #6577F3
    public static Color BgIndigo { get; private set; } = default!;         // #F0F1FE / #1e1f3a

    // ── Psychology / growth accent (purple) ─────────────────────────
    public static Color AccentPurple { get; private set; } = default!;     // #7C3AED
    public static Color BgPurple { get; private set; } = default!;         // #EDE9FE / #1f162e

    // ── Warning / amber ─────────────────────────────────────────────
    public static Color Warning { get; private set; } = default!;          // #D97706

    // ── Misc ────────────────────────────────────────────────────────
    public static Color NavIconBg { get; private set; } = default!;        // #f0f5e0 / #1f2518
    public static Color TextOnDark { get; private set; } = default!;       // white on dark surfaces
    public static Color BgSuccessStrong { get; private set; } = default!;  // #F0FDF9 / #0a1f14  — completed card bg
    public static Color BorderSuccess { get; private set; } = default!;    // #A7F3D0 / #065f46
    public static Color BgUnlocked { get; private set; } = default!;       // #DDE0FA / #232556
    public static Color TextSecondary { get; private set; } = default!;    // #4B5563 / #9ca3af

    // ── Button surface (inverted in dark mode) ──────────────────────
    public static Color ButtonBg { get; private set; } = default!;
    public static Color ButtonText { get; private set; } = default!;

    /// <summary>Apply the light palette.</summary>
    internal static void ApplyLight()
    {
        Dark = Color.FromArgb("#202020");
        Accent = Color.FromArgb("#c9f158");
        AccentDark = Color.FromArgb("#a8d143");
        Background = Color.FromArgb("#f2f3f5");
        Surface = Color.FromArgb("#ffffff");
        Border = Color.FromArgb("#e5e7eb");
        Muted = Color.FromArgb("#6b7280");
        Success = Color.FromArgb("#047857");
        Danger = Color.FromArgb("#b91c1c");
        TabUnselected = Color.FromArgb("#9ca3af");

        SurfaceDim = Color.FromArgb("#F9FAFB");
        SurfaceContainer = Color.FromArgb("#F3F4F6");

        BgSuccess = Color.FromArgb("#ECFDF5");
        BgDanger = Color.FromArgb("#FEF2F2");
        BgWarning = Color.FromArgb("#FFFBEB");

        AccentIndigo = Color.FromArgb("#6577F3");
        BgIndigo = Color.FromArgb("#F0F1FE");

        AccentPurple = Color.FromArgb("#7C3AED");
        BgPurple = Color.FromArgb("#EDE9FE");

        Warning = Color.FromArgb("#D97706");

        NavIconBg = Color.FromArgb("#f0f5e0");
        TextOnDark = Colors.White;
        BgSuccessStrong = Color.FromArgb("#F0FDF9");
        BorderSuccess = Color.FromArgb("#A7F3D0");
        BgUnlocked = Color.FromArgb("#DDE0FA");
        TextSecondary = Color.FromArgb("#4B5563");

        ButtonBg = Color.FromArgb("#202020");
        ButtonText = Color.FromArgb("#ffffff");
    }

    /// <summary>Apply the dark palette.</summary>
    internal static void ApplyDark()
    {
        Dark = Color.FromArgb("#EDEDED");
        Accent = Color.FromArgb("#c9f158");
        AccentDark = Color.FromArgb("#a8d143");
        Background = Color.FromArgb("#121212");
        Surface = Color.FromArgb("#1E1E1E");
        Border = Color.FromArgb("#333333");
        Muted = Color.FromArgb("#9ca3af");
        Success = Color.FromArgb("#34D399");
        Danger = Color.FromArgb("#F87171");
        TabUnselected = Color.FromArgb("#6b7280");

        SurfaceDim = Color.FromArgb("#1A1A1A");
        SurfaceContainer = Color.FromArgb("#2A2A2A");

        BgSuccess = Color.FromArgb("#0D2818");
        BgDanger = Color.FromArgb("#2D1111");
        BgWarning = Color.FromArgb("#2D2611");

        AccentIndigo = Color.FromArgb("#8B9CF6");
        BgIndigo = Color.FromArgb("#1E1F3A");

        AccentPurple = Color.FromArgb("#A78BFA");
        BgPurple = Color.FromArgb("#1F162E");

        Warning = Color.FromArgb("#FBBF24");

        NavIconBg = Color.FromArgb("#1F2518");
        TextOnDark = Colors.White;
        BgSuccessStrong = Color.FromArgb("#0A1F14");
        BorderSuccess = Color.FromArgb("#065F46");
        BgUnlocked = Color.FromArgb("#232556");
        TextSecondary = Color.FromArgb("#9ca3af");

        ButtonBg = Color.FromArgb("#ffffff");
        ButtonText = Color.FromArgb("#202020");
    }

    // Initialise to light by default
    static AppColors() => ApplyLight();
}
