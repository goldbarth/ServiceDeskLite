using System.Globalization;

namespace ServiceDeskLite.Web.Features.AiInsights;

/// <summary>
/// Display formatting for AI metrics. An unmeasured value renders as <see cref="NoValue"/>
/// and never as a zero, which a reader would take for a measured result.
/// </summary>
public static class Format
{
    /// <summary>Placeholder for a value that was not measured, as opposed to one that was zero.</summary>
    public const string NoValue = "n/a";

    public static string Percent(double? value) =>
        value is null
            ? NoValue
            : string.Create(CultureInfo.InvariantCulture, $"{value.Value * 100:0.#} %");

    public static string Count(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Sub-second latencies read as milliseconds; anything slower reads as seconds.</summary>
    public static string Milliseconds(double value) =>
        value < 1_000
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0} ms")
            : string.Create(CultureInfo.InvariantCulture, $"{value / 1000d:0.0} s");

    /// <summary>Compacts large token counts: 1,234 stays exact below 10k, then becomes "12.3k".</summary>
    public static string Tokens(long value) =>
        value < 10_000
            ? Count(value)
            : string.Create(CultureInfo.InvariantCulture, $"{value / 1000d:0.#}k");
}
