using System.Globalization;
using CoreAnimation;

namespace HealthSyncy.Mobile.EntryPoint.Views;

internal static class Ui
{
    public static readonly UIColor Accent = UIColor.FromRGB(255, 94, 105);

    public static UIFont Font(int size, UIFontWeight weight, UIFontTextStyle style, bool rounded = false)
    {
        var font = UIFont.SystemFontOfSize(size, weight)!;
        if (rounded)
            font = UIFont.FromDescriptor(font.FontDescriptor.CreateWithDesign(UIFontDescriptorSystemDesign.Rounded)!, size)!;
        return UIFontMetrics.GetMetrics(style.GetConstant()!).GetScaledFont(font);
    }

    public static UILabel Label(UIFontTextStyle style, UIColor? colour = null, string? text = null) => new()
    {
        Font = UIFont.GetPreferredFontForTextStyle(style)!,
        AdjustsFontForContentSizeCategory = true,
        TextColor = colour ?? UIColor.Label,
        Text = text,
        Lines = 0
    };

    public static UITextField Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body),
        AdjustsFontForContentSizeCategory = true,
        AutocapitalizationType = UITextAutocapitalizationType.None,
        AutocorrectionType = UITextAutocorrectionType.No,
        SpellCheckingType = UITextSpellCheckingType.No,
        ClearButtonMode = UITextFieldViewMode.WhileEditing
    };

    public static UIView Dot()
    {
        var dot = new UIView { BackgroundColor = UIColor.SystemGray };
        dot.Layer.CornerRadius = 5;
        NSLayoutConstraint.ActivateConstraints([dot.WidthAnchor.ConstraintEqualTo(10), dot.HeightAnchor.ConstraintEqualTo(10)]);
        return dot;
    }

    public static UIStackView Vertical(int spacing, params UIView[] views) =>
        new(views) { Axis = UILayoutConstraintAxis.Vertical, Spacing = spacing };

    public static UIStackView Horizontal(int spacing, params UIView[] views) =>
        new(views) { Axis = UILayoutConstraintAxis.Horizontal, Spacing = spacing, Alignment = UIStackViewAlignment.Center };

    public static UIStackView Inset(UIView view) => new([view])
    {
        LayoutMarginsRelativeArrangement = true,
        DirectionalLayoutMargins = new(0, 16, 0, 16)
    };

    public static UIStackView Section(string title, params UIView[] views)
    {
        var header = Label(UIFontTextStyle.Footnote, UIColor.SecondaryLabel, title.ToUpper(CultureInfo.CurrentCulture));
        header.AccessibilityTraits = UIAccessibilityTrait.Header;
        return Vertical(8, [Inset(header), .. views]);
    }

    public static UIStackView Card(params UIView[] rows)
    {
        var card = Vertical(0, [.. rows.SelectMany<UIView, UIView>((row, index) => index == 0 ? [row] : [Separator(), row])]);
        card.LayoutMarginsRelativeArrangement = true;
        card.DirectionalLayoutMargins = new(0, 16, 0, 16);
        card.BackgroundColor = UIColor.SecondarySystemGroupedBackground;
        card.Layer.CornerRadius = 14;
        card.Layer.CornerCurve = CACornerCurve.Continuous;
        return card;
    }

    public static UIStackView Row(UIView leading, UIView? trailing = null)
    {
        leading.SetContentHuggingPriority(1, UILayoutConstraintAxis.Horizontal);
        leading.SetContentCompressionResistancePriority(250, UILayoutConstraintAxis.Horizontal);
        trailing?.SetContentHuggingPriority(1000, UILayoutConstraintAxis.Horizontal);
        var row = Horizontal(12, trailing is null ? [leading] : [leading, trailing]);
        row.LayoutMarginsRelativeArrangement = true;
        row.DirectionalLayoutMargins = new(12, 0, 12, 0);
        row.HeightAnchor.ConstraintGreaterThanOrEqualTo(48).Active = true;
        return row;
    }

    public static UIButton Button(string title, bool prominent, Action onTap)
    {
        var configuration = Style(prominent);
        configuration.AttributedTitle = new NSAttributedString(title,
            new UIStringAttributes { Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline) });
        return Make(configuration, onTap);
    }

    public static UIButton Link(string title, Action onTap)
    {
        var configuration = UIButtonConfiguration.PlainButtonConfiguration;
        configuration.Title = title;
        configuration.ContentInsets = NSDirectionalEdgeInsets.Zero;
        return Make(configuration, onTap);
    }

    public static void Retitle(UIButton button, string title)
    {
        var configuration = button.Configuration!;
        configuration.Title = title;
        button.Configuration = configuration;
    }

    public static void Busy(UIButton button, bool busy)
    {
        var configuration = button.Configuration!;
        configuration.ShowsActivityIndicator = busy;
        button.Configuration = configuration;
        button.Enabled = !busy;
    }

    private static UIView Separator()
    {
        var line = new UIView { BackgroundColor = UIColor.Separator };
        line.HeightAnchor.ConstraintEqualTo(0.5f).Active = true;
        return line;
    }

    private static UIButtonConfiguration Style(bool prominent)
    {
        var configuration = prominent
            ? UIButtonConfiguration.FilledButtonConfiguration
            : UIButtonConfiguration.GrayButtonConfiguration;
        configuration.CornerStyle = UIButtonConfigurationCornerStyle.Large;
        configuration.ButtonSize = UIButtonConfigurationSize.Large;
        return configuration;
    }

    private static UIButton Make(UIButtonConfiguration configuration, Action onTap)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.Configuration = configuration;
        button.TouchUpInside += (_, _) => onTap();
        return button;
    }
}
