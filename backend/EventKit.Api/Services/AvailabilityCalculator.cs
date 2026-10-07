using EventKit.Api.Domain;

namespace EventKit.Api.Services;

public sealed record Reservation(DateOnly StartDate, DateOnly EndDate, int Quantity, BookingStatus Status);

public static class AvailabilityCalculator
{
    // Availability over a range is stock minus PEAK simultaneous demand,
    // not the sum of every booking that touches the range.
    public static int Available(int stock, int maintenance, DateOnly start, DateOnly end, IEnumerable<Reservation> reservations, DateOnly today)
    {
        if (end < start) throw new ArgumentException("End must follow start.");
        var changes = new SortedDictionary<int, int>();
        foreach (var r in reservations)
        {
            if (r.Status is BookingStatus.Cancelled or BookingStatus.Completed) continue;
            var effectiveEnd = r.Status == BookingStatus.CheckedOut && r.EndDate < today ? end : r.EndDate;
            if (r.StartDate > end || effectiveEnd < start) continue;
            var first = Math.Max(start.DayNumber, r.StartDate.DayNumber);
            var last = Math.Min(end.DayNumber, effectiveEnd.DayNumber);
            changes[first] = changes.GetValueOrDefault(first) + r.Quantity;
            changes[last + 1] = changes.GetValueOrDefault(last + 1) - r.Quantity;
        }
        var used = 0;
        var peak = 0;
        foreach (var change in changes.Values) { used += change; peak = Math.Max(peak, used); }
        return Math.Max(0, stock - maintenance - peak);
    }
}

public sealed class BusinessClock(TimeProvider time)
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), London).DateTime);
}
