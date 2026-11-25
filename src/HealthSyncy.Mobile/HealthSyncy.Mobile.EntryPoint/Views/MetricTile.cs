using System.Globalization;
using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.EntryPoint.Views;

internal sealed class MetricTile : UIStackView
{
    private const string Missing = "–";

    private static readonly UIFont _number = Ui.Font(32, UIFontWeight.Semibold, UIFontTextStyle.LargeTitle, rounded: true);
    private static readonly UIFont _unit = Ui.Font(17, UIFontWeight.Medium, UIFontTextStyle.Body, rounded: true);

    private readonly UILabel _label = Ui.Label(UIFontTextStyle.Footnote, UIColor.SecondaryLabel);
    private readonly UILabel _value = new() { AdjustsFontSizeToFitWidth = true, MinimumScaleFactor = 0.5f };
    private readonly UIProgressView _progress = new() { TrackTintColor = UIColor.TertiarySystemFill };
    private readonly UILabel _goal = Ui.Label(UIFontTextStyle.Caption1, UIColor.SecondaryLabel);

    public MetricTile(Metric metric)
    {
        Axis = UILayoutConstraintAxis.Vertical;
        Spacing = 4;
        IsAccessibilityElement = true;
        foreach (var view in (UIView[])[_label, _value, _progress, _goal])
            AddArrangedSubview(view);
        SetCustomSpacing(10, _value);
        Show(metric);
    }

    public static string Count(Metric metric) => metric.Value is { } value ? Number(value) : Missing;

    public static string Amount(Metric metric) =>
        metric.Value is null ? Missing : $"{Count(metric)} {metric.Unit}".TrimEnd();

    public void Show(Metric metric)
    {
        var text = new NSMutableAttributedString(Count(metric),
            new UIStringAttributes { Font = _number, ForegroundColor = UIColor.Label });
        if (metric.Value is not null && metric.Unit.Length > 0)
        {
            text.Append(new NSAttributedString(" " + metric.Unit,
                new UIStringAttributes { Font = _unit, ForegroundColor = UIColor.SecondaryLabel }));
        }
        _label.Text = metric.Label;
        _value.AttributedText = text;
        _progress.Hidden = metric.Progress is null;
        _progress.Progress = (float)(metric.Progress ?? 0);
        _goal.Text = metric.Goal is { } goal ? $"of {Number(goal)} {metric.Unit} goal" : null;
        _goal.Hidden = _goal.Text is null;
        AccessibilityLabel = metric.Label;
        AccessibilityValue = string.Join(", ",
            new[] { metric.Value is null ? "No data" : Amount(metric), _goal.Text }.OfType<string>());
    }

    private static string Number(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
