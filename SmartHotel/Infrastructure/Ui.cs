using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace SmartHotel.Infrastructure;

/// <summary>Formatting and badge helpers shared by all views.</summary>
public static class Ui
{
    public static string Currency { get; set; } = "$";

    public static string Money(decimal value, bool whole = false) =>
        (value < 0 ? "-" : "") + Currency + Math.Abs(value).ToString(whole ? "N0" : "N2", CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "—";

    public static string Stamp(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? "—";

    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    public static string TimeAgo(DateTime value)
    {
        var span = DateTime.Now - value;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minute{(span.TotalMinutes >= 2 ? "s" : "")} ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour{(span.TotalHours >= 2 ? "s" : "")} ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} day{(span.TotalDays >= 2 ? "s" : "")} ago";
        return value.ToString("dd MMM yyyy");
    }

    private static IHtmlContent Pill(string text, string tone) =>
        new HtmlString($"<span class=\"pill pill-{tone}\">{HtmlEncoder.Default.Encode(text)}</span>");

    public static string Tone(RoomStatus status) => status switch
    {
        RoomStatus.Available => "info",
        RoomStatus.Occupied => "success",
        RoomStatus.Dirty => "warning",
        RoomStatus.Clean => "teal",
        _ => "danger"
    };

    public static string Icon(RoomStatus status) => status switch
    {
        RoomStatus.Available => "bi-door-open",
        RoomStatus.Occupied => "bi-person-fill-check",
        RoomStatus.Dirty => "bi-trash3",
        RoomStatus.Clean => "bi-stars",
        _ => "bi-tools"
    };

    public static IHtmlContent Badge(RoomStatus status) => Pill(status.DisplayName(), Tone(status));

    public static IHtmlContent Badge(BookingStatus status) => Pill(status.DisplayName(), status switch
    {
        BookingStatus.Pending => "warning",
        BookingStatus.Confirmed => "success",
        BookingStatus.CheckedIn => "primary",
        BookingStatus.CheckedOut => "secondary",
        _ => "danger"
    });

    public static IHtmlContent Badge(InvoiceStatus status) => Pill(status.DisplayName(), status switch
    {
        InvoiceStatus.Paid => "success",
        InvoiceStatus.PartiallyPaid => "warning",
        InvoiceStatus.Unpaid => "danger",
        _ => "secondary"
    });

    public static IHtmlContent ActiveBadge(bool active) => active ? Pill("Active", "success") : Pill("Inactive", "secondary");

    public static (string Icon, string Tone) Style(NotificationType type) => type switch
    {
        NotificationType.Booking => ("bi-calendar-plus", "primary"),
        NotificationType.Payment => ("bi-cash-coin", "green"),
        NotificationType.CheckIn => ("bi-box-arrow-in-right", "sky"),
        NotificationType.CheckOut => ("bi-box-arrow-right", "purple"),
        NotificationType.Maintenance => ("bi-tools", "red"),
        NotificationType.Housekeeping => ("bi-stars", "orange"),
        _ => ("bi-info-circle", "primary")
    };
}
