using EventKit.Api;
using EventKit.Api.Data;
using EventKit.Api.Domain;
using EventKit.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EventKit.Tests;

public sealed class PostgresBookingTests
{
    // Opt in with a dedicated development/test branch connection. No production
    // connection is fetched or inferred by the tests.
    [PostgresFact]
    public async Task ConcurrentReservationsCannotOversellAndCancellationReleasesStock()
    {
        var url = Environment.GetEnvironmentVariable("EVENTKIT_TEST_DATABASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(url), "Set EVENTKIT_TEST_DATABASE_URL to an isolated migrated test branch.");
        var options = new DbContextOptionsBuilder<EventKitDbContext>().UseNpgsql(DatabaseConnection.Parse(url!)).Options;
        var clock = new BusinessClock(TimeProvider.System);
        await using var setup = new EventKitDbContext(options);
        var item = new Equipment { Name = "Integration test equipment", Category = "Audio", TotalQuantity = 1 };
        setup.Equipment.Add(item); await setup.SaveChangesAsync();
        var email = $"verification-{Guid.NewGuid():N}@example.test";
        async Task<(BookingDto? booking, DomainException? conflict)> Reserve()
        {
            await using var db = new EventKitDbContext(options);
            try { return (await new BookingService(db, clock).Create(new BookingRequest("Concurrent test", "Test", email, null,
                clock.Today, clock.Today, null, [new BookingLineRequest(item.Id, 1)]), default), null); }
            catch (DomainException ex) { return (null, ex); }
        }
        try
        {
            var results = await Task.WhenAll(Reserve(), Reserve());
            var successful = Assert.Single(results, r => r.booking is not null).booking!;
            Assert.Equal(409, Assert.Single(results, r => r.conflict is not null).conflict!.StatusCode);
            var service = new BookingService(setup, clock);
            Assert.Equal(0, (await service.Catalogue(clock.Today, clock.Today, default)).Single(e => e.Id == item.Id).AvailableQuantity);
            await service.Transition(successful.Id, "cancel", default);
            Assert.Equal(1, (await service.Catalogue(clock.Today, clock.Today, default)).Single(e => e.Id == item.Id).AvailableQuantity);
        }
        finally
        {
            setup.ChangeTracker.Clear();
            await setup.BookingItems.Where(i => i.EquipmentId == item.Id).ExecuteDeleteAsync();
            await setup.Bookings.Where(b => b.Borrower.Email == email).ExecuteDeleteAsync();
            await setup.Borrowers.Where(b => b.Email == email).ExecuteDeleteAsync();
            await setup.Equipment.Where(e => e.Id == item.Id).ExecuteDeleteAsync();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVENTKIT_TEST_DATABASE_URL")))
            Skip = "Set EVENTKIT_TEST_DATABASE_URL to opt into the real PostgreSQL concurrency test.";
    }
}
