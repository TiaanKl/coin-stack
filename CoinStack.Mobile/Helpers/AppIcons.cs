namespace CoinStack.Mobile.Helpers;

/// <summary>
/// Helper for creating Font Awesome icon ImageSources and Labels.
/// Uses Font Awesome 7 Free Solid/Regular OTF fonts registered in MauiProgram.
/// </summary>
public static class AppIcons
{
    private const string Solid = "FontAwesomeSolid";
    private const string Regular = "FontAwesomeRegular";

    // ── Font Awesome 7 Free Solid unicode glyphs ──
    public const string GlyphHome = "\uf015";
    public const string GlyphWallet = "\uf555";
    public const string GlyphFlag = "\uf024";
    public const string GlyphTrophy = "\uf091";
    public const string GlyphEllipsis = "\uf141";
    public const string GlyphReceipt = "\uf543";
    public const string GlyphArrowTrendUp = "\ue098";
    public const string GlyphBuildingColumns = "\uf19c";
    public const string GlyphRotate = "\uf021";
    public const string GlyphTags = "\uf02c";
    public const string GlyphBullseye = "\uf140";
    public const string GlyphPiggyBank = "\uf4d3";
    public const string GlyphCreditCard = "\uf09d";
    public const string GlyphCalculator = "\uf1ec";
    public const string GlyphClockRotateLeft = "\uf1da";
    public const string GlyphBolt = "\uf0e7";
    public const string GlyphStar = "\uf005";
    public const string GlyphHeart = "\uf004";
    public const string GlyphBookOpen = "\uf518";
    public const string GlyphCartShopping = "\uf07a";
    public const string GlyphChartBar = "\uf080";
    public const string GlyphGear = "\uf013";
    public const string GlyphCalendarWeek = "\uf784";
    public const string GlyphBrain = "\uf5dc";
    public const string GlyphChevronRight = "\uf054";
    public const string GlyphBell = "\uf0f3";
    public const string GlyphEyeSlash = "\uf070";
    public const string GlyphArrowUp = "\uf062";
    public const string GlyphArrowDown = "\uf063";
    public const string GlyphPlus = "\uf067";
    public const string GlyphCheck = "\uf00c";
    public const string GlyphXmark = "\uf00d";
    public const string GlyphTrash = "\uf1f8";
    public const string GlyphPen = "\uf304";
    public const string GlyphFire = "\uf06d";
    public const string GlyphSeedling = "\uf4d8";
    public const string GlyphChartLine = "\uf201";
    public const string GlyphMoneyBill = "\uf0d6";
    public const string GlyphCircleDollarToSlot = "\uf4b9";
    public const string GlyphHourglass = "\uf254";
    public const string GlyphLightbulb = "\uf0eb";
    public const string GlyphCircleInfo = "\uf05a";
    public const string GlyphShield = "\uf132";

    public static FontImageSource Create(string glyph, Color? color = null, double size = 24) => new()
    {
        FontFamily = Solid,
        Glyph = glyph,
        Color = color,
        Size = size
    };

    public static Label CreateLabel(string glyph, Color? color = null, double size = 24) => new()
    {
        FontFamily = Solid,
        Text = glyph,
        TextColor = color ?? AppColors.Dark,
        FontSize = size,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalTextAlignment = TextAlignment.Center
    };

    // ── Bottom tab icons ──
    public static FontImageSource HomeTab => Create(GlyphHome, size: 20);
    public static FontImageSource WalletTab => Create(GlyphWallet, size: 20);
    public static FontImageSource FlagTab => Create(GlyphFlag, size: 20);
    public static FontImageSource TrophyTab => Create(GlyphTrophy, size: 20);
    public static FontImageSource MoreTab => Create(GlyphEllipsis, size: 20);
}
