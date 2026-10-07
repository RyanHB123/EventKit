using System.Net.Mail;
using EventKit.Api.Data;
using EventKit.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventKit.Api.Services;

public sealed class BookingService(EventKitDbContext db, BusinessClock clock)
{
    public static readonly string[] Categories = ["Audio", "Lighting", "Furniture", "Outdoor", "Presentation"];
    // Every stock-affecting writer takes the same transaction lock. Availability
    // is checked AFTER acquiring it, so two simultaneous requests cannot oversell.
    private Task<int> Lock(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724901)", ct);

    private async Task<Dictionary<Guid, List<Reservation>>> Reservations(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var rows = await db.BookingItems.AsNoTracking()
            .Where(i => (i.Booking.Status == BookingStatus.Confirmed || i.Booking.Status == BookingStatus.CheckedOut)
                && i.Booking.StartDate <= end
                && (i.Booking.EndDate >= start || i.Booking.Status == BookingStatus.CheckedOut))
            .Select(i => new { i.EquipmentId, i.Booking.StartDate, i.Booking.EndDate, i.Quantity, i.Booking.Status }).ToListAsync(ct);
        return rows.GroupBy(i => i.EquipmentId).ToDictionary(g => g.Key,
            g => g.Select(i => new Reservation(i.StartDate, i.EndDate, i.Quantity, i.Status)).ToList());
    }

    public async Task<List<EquipmentDto>> Catalogue(DateOnly start, DateOnly end, CancellationToken ct)
    {
        if (end < start || end.DayNumber - start.DayNumber > 180) throw new DomainException("Choose a date range of up to 180 days.");
        var items = await db.Equipment.AsNoTracking().Where(e => !e.Archived).OrderBy(e => e.Name).ToListAsync(ct);
        var reservations = await Reservations(start, end, ct);
        return items.Select(e => new EquipmentDto(e.Id, e.Name, e.Category, e.Description, e.Location, e.TotalQuantity,
            e.MaintenanceQuantity, AvailabilityCalculator.Available(e.TotalQuantity, e.MaintenanceQuantity, start, end,
                reservations.GetValueOrDefault(e.Id, []), clock.Today))).ToList();
    }

    public async Task<List<BookingDto>> ListBookings(CancellationToken ct) =>
        (await db.Bookings.AsNoTracking().Include(b => b.Borrower).Include(b => b.Items).ThenInclude(i => i.Equipment)
            .OrderByDescending(b => b.CreatedAt).Take(1000).ToListAsync(ct)).Select(b => BookingDto.From(b, clock.Today)).ToList();

    public async Task<BookingDto> Create(BookingRequest request, CancellationToken ct)
    {
        ValidateBooking(request, clock.Today);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var requested = request.Items!;
        var ids = requested.Select(i => i.EquipmentId).ToList();
        var equipment = await db.Equipment.Where(e => ids.Contains(e.Id) && !e.Archived).ToListAsync(ct);
        if (equipment.Count != requested.Count) throw new DomainException("An equipment item is no longer available.", 409);
        var reservations = await Reservations(request.StartDate, request.EndDate, ct);
        foreach (var line in requested)
        {
            var item = equipment.Single(e => e.Id == line.EquipmentId);
            var available = AvailabilityCalculator.Available(item.TotalQuantity, item.MaintenanceQuantity,
                request.StartDate, request.EndDate, reservations.GetValueOrDefault(item.Id, []), clock.Today);
            if (line.Quantity > available) throw new DomainException($"Only {available} × {item.Name} available for these dates. Refresh your selection.", 409);
        }
        var email = request.Email!.Trim().ToLowerInvariant();
        var borrower = await db.Borrowers.SingleOrDefaultAsync(b => b.Email == email, ct);
        if (borrower is null)
        {
            borrower = new Borrower { Name = request.BorrowerName!.Trim(), Email = email, Phone = request.Phone?.Trim() };
            db.Borrowers.Add(borrower);
        }
        var booking = new Booking {
            EventName = request.EventName!.Trim(), Borrower = borrower,
            StartDate = request.StartDate, EndDate = request.EndDate, Notes = request.Notes?.Trim(),
            Items = requested.Select(i => new BookingItem { EquipmentId = i.EquipmentId, Quantity = i.Quantity,
                Equipment = equipment.Single(e => e.Id == i.EquipmentId) }).ToList()
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return BookingDto.From(booking, clock.Today);
    }

    public async Task<BookingDto> Transition(Guid id, string action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var booking = await db.Bookings.Include(b => b.Borrower).Include(b => b.Items).ThenInclude(i => i.Equipment)
            .SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw new DomainException("Booking not found.", 404);
        var next = NextStatus(booking.Status, action);
        if (action == "checkout")
        {
            if (booking.StartDate > clock.Today || booking.EndDate < clock.Today)
                throw new DomainException("Equipment can only be checked out during its booked dates.", 409);
            // Overdue physical loans can invalidate an otherwise confirmed booking.
            var equipment = booking.Items.Select(i => i.EquipmentId).ToList();
            var outItems = await db.BookingItems.AsNoTracking().Where(i => equipment.Contains(i.EquipmentId)
                && i.Booking.Status == BookingStatus.CheckedOut).ToListAsync(ct);
            foreach (var item in booking.Items)
                if (item.Quantity + outItems.Where(i => i.EquipmentId == item.EquipmentId).Sum(i => i.Quantity)
                    > item.Equipment.TotalQuantity - item.Equipment.MaintenanceQuantity)
                    throw new DomainException($"{item.Equipment.Name} has not been returned by another borrower yet.", 409);
            booking.CheckedOutAt = DateTimeOffset.UtcNow;
        }
        if (action == "return") booking.ReturnedAt = DateTimeOffset.UtcNow;
        booking.Status = next;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return BookingDto.From(booking, clock.Today);
    }

    public async Task<EquipmentDto> SaveEquipment(Guid? id, EquipmentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100) throw new DomainException("Enter an equipment name (up to 100 characters).");
        if (!Categories.Contains(request.Category)) throw new DomainException("Choose a valid category.");
        if (request.TotalQuantity < 1 || request.TotalQuantity > 10000 || request.MaintenanceQuantity < 0
            || request.MaintenanceQuantity > request.TotalQuantity) throw new DomainException("Check the stock and maintenance quantities.");
        if ((request.Description?.Length ?? 0) > 600 || (request.Location?.Length ?? 0) > 100) throw new DomainException("Description or location is too long.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var item = id is null ? new Equipment() : await db.Equipment.SingleOrDefaultAsync(e => e.Id == id && !e.Archived, ct)
            ?? throw new DomainException("Equipment not found.", 404);
        if (id is not null)
        {
            var reservations = await Reservations(clock.Today, DateOnly.MaxValue, ct);
            var active = reservations.GetValueOrDefault(item.Id, []);
            var peak = 100000 - AvailabilityCalculator.Available(100000, 0, clock.Today, DateOnly.MaxValue, active, clock.Today);
            if (request.TotalQuantity - request.MaintenanceQuantity < peak)
                throw new DomainException($"At least {peak} usable units are needed for existing bookings. Cancel or return those bookings first.", 409);
        }
        item.Name = request.Name.Trim(); item.Category = request.Category!;
        item.Description = request.Description?.Trim() ?? ""; item.Location = request.Location?.Trim() ?? "Main store";
        item.TotalQuantity = request.TotalQuantity; item.MaintenanceQuantity = request.MaintenanceQuantity;
        if (id is null) db.Equipment.Add(item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new EquipmentDto(item.Id, item.Name, item.Category, item.Description, item.Location,
            item.TotalQuantity, item.MaintenanceQuantity, item.TotalQuantity - item.MaintenanceQuantity);
    }

    public static BookingStatus NextStatus(BookingStatus current, string action) => (current, action) switch {
        (BookingStatus.Confirmed, "checkout") => BookingStatus.CheckedOut,
        (BookingStatus.CheckedOut, "return") => BookingStatus.Completed,
        (BookingStatus.Confirmed, "cancel") => BookingStatus.Cancelled,
        _ => throw new DomainException("That action is not allowed for the current booking status.", 409)
    };

    public static void ValidateBooking(BookingRequest request, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(request.EventName) || request.EventName.Length > 150) throw new DomainException("Enter an event name (up to 150 characters).");
        if (string.IsNullOrWhiteSpace(request.BorrowerName) || request.BorrowerName.Length > 100) throw new DomainException("Enter the borrower's name (up to 100 characters).");
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || !MailAddress.TryCreate(request.Email.Trim(), out var address)
            || address.Address != request.Email.Trim()) throw new DomainException("Enter a valid email address.");
        if ((request.Phone?.Length ?? 0) > 40 || (request.Notes?.Length ?? 0) > 1000) throw new DomainException("Phone or notes are too long.");
        if (request.StartDate < today || request.EndDate < request.StartDate || request.EndDate.DayNumber - request.StartDate.DayNumber > 180)
            throw new DomainException("Choose dates from today onwards, with a booking of up to 180 days.");
        if (request.Items is not { Count: > 0 and <= 50 } || request.Items.Any(i => i.Quantity < 1 || i.Quantity > 10000 || i.EquipmentId == Guid.Empty)
            || request.Items.Select(i => i.EquipmentId).Distinct().Count() != request.Items.Count)
            throw new DomainException("Select at least one item with a valid quantity. Each item must appear only once.");
    }
}
