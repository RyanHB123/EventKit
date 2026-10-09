using EventKit.Api;
using EventKit.Api.Domain;
using EventKit.Api.Services;

namespace EventKit.Tests;

public sealed class AvailabilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);
    private static Reservation Reserved(int day, int end, int qty, BookingStatus status = BookingStatus.Confirmed) =>
        new(Today.AddDays(day), Today.AddDays(end), qty, status);

    [Fact]
    public void NonOverlappingBookingsUsePeakDemandNotTheirSum()
    {
        var result = AvailabilityCalculator.Available(10, 0, Today, Today.AddDays(2), [Reserved(0, 0, 6), Reserved(1, 2, 6)], Today);
        Assert.Equal(4, result);
    }

    [Fact]
    public void CollectionAndReturnDatesAreInclusive()
    {
        var result = AvailabilityCalculator.Available(10, 0, Today.AddDays(1), Today.AddDays(1), [Reserved(0, 1, 6), Reserved(1, 2, 3)], Today);
        Assert.Equal(1, result);
    }

    [Fact]
    public void CancelledAndReturnedBookingsReleaseStock()
    {
        var result = AvailabilityCalculator.Available(10, 0, Today, Today, [Reserved(0, 1, 6, BookingStatus.Cancelled), Reserved(0, 1, 4, BookingStatus.Completed)], Today);
        Assert.Equal(10, result);
    }

    [Fact]
    public void MaintenanceStockCannotBeReserved()
    {
        Assert.Equal(2, AvailabilityCalculator.Available(10, 3, Today, Today, [Reserved(0, 0, 5)], Today));
    }

    [Fact]
    public void OverduePhysicalLoansBlockFutureReservationsUntilReturned()
    {
        var result = AvailabilityCalculator.Available(10, 0, Today.AddDays(30), Today.AddDays(31), [Reserved(-4, -2, 7, BookingStatus.CheckedOut)], Today);
        Assert.Equal(3, result);
    }

    [Fact]
    public void AConfirmedPastBookingDoesNotBlockFutureDates()
    {
        Assert.Equal(10, AvailabilityCalculator.Available(10, 0, Today, Today, [Reserved(-4, -2, 7)], Today));
    }

    [Fact]
    public void DemandPeakingMidwayThroughTheRangeIsCounted()
    {
        Assert.Equal(1, AvailabilityCalculator.Available(10, 0, Today, Today.AddDays(9), [Reserved(0, 9, 2), Reserved(3, 5, 4), Reserved(5, 6, 3)], Today));
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, "checkout", BookingStatus.CheckedOut)]
    [InlineData(BookingStatus.Confirmed, "cancel", BookingStatus.Cancelled)]
    [InlineData(BookingStatus.CheckedOut, "return", BookingStatus.Completed)]
    public void AllowedTransitions(BookingStatus current, string action, BookingStatus expected) =>
        Assert.Equal(expected, BookingService.NextStatus(current, action));

    [Theory]
    [InlineData(BookingStatus.Completed, "checkout")]
    [InlineData(BookingStatus.Cancelled, "checkout")]
    [InlineData(BookingStatus.Confirmed, "return")]
    [InlineData(BookingStatus.CheckedOut, "cancel")]
    public void InvalidTransitionsAreRejected(BookingStatus current, string action) =>
        Assert.Equal(409, Assert.Throws<DomainException>(() => BookingService.NextStatus(current, action)).StatusCode);

    private static BookingRequest Valid() => new("Test event", "Test Borrower", "test@example.test", null, Today, Today, null, [new(Guid.NewGuid(), 2)]);

    [Fact]
    public void DuplicateItemsAreRejected()
    {
        var request = Valid();
        request.Items!.Add(request.Items[0]);
        Assert.Throws<DomainException>(() => BookingService.ValidateBooking(request, Today));
    }

    [Fact]
    public void MissingItemsAreRejected() => Assert.Throws<DomainException>(() => BookingService.ValidateBooking(Valid() with { Items = null }, Today));

    [Fact]
    public void PastDatesAreRejected() => Assert.Throws<DomainException>(() => BookingService.ValidateBooking(Valid() with { StartDate = Today.AddDays(-1) }, Today));

    [Fact]
    public void InvalidEmailIsRejected() => Assert.Throws<DomainException>(() => BookingService.ValidateBooking(Valid() with { Email = "bad-email" }, Today));
}
