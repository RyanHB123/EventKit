using EventKit.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventKit.Api.Data;

public static class DatabaseSetup
{
    public const string DemoEmail = "admin@eventkit.local";
    public const string DemoPassword = "EventKit-demo-2026!";

    public static async Task EnsureStaff(EventKitDbContext db, IConfiguration config, bool development)
    {
        if (await db.StaffUsers.AnyAsync()) return;
        var email = config["Admin:Email"] ?? (development ? DemoEmail : null);
        var password = config["Admin:Password"] ?? (development ? DemoPassword : null);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Set Admin__Email and Admin__Password (at least 12 characters) when creating the first production staff account.");
        var user = new StaffUser { Email = email.Trim().ToLowerInvariant(), Name = config["Admin:Name"] ?? "EventKit admin" };
        user.PasswordHash = new PasswordHasher<StaffUser>().HashPassword(user, password);
        db.StaffUsers.Add(user);
        await db.SaveChangesAsync();
    }

    public static async Task Seed(EventKitDbContext db, DateOnly today)
    {
        if (await db.Equipment.AnyAsync()) return;
        Equipment Item(string name, string category, int stock, string description, string location, int maintenance = 0) =>
            new() { Name = name, Category = category, TotalQuantity = stock, Description = description, Location = location, MaintenanceQuantity = maintenance };
        var equipment = new[] {
            Item("Powered PA speaker", "Audio", 8, "A clear, powerful sound for talks, live music and celebrations. Includes a power cable.", "Audio cupboard · A1"),
            Item("Wireless microphone", "Audio", 12, "Handheld microphone and receiver. Perfect for speeches and hosting.", "Audio cupboard · A2", 1),
            Item("Audio mixer", "Audio", 3, "Compact 8-channel mixer with microphone and line inputs.", "Audio cupboard · A3"),
            Item("LED stage light", "Lighting", 10, "Colour-changing LED PAR light with adjustable stand mount.", "Lighting rack · B1", 2),
            Item("Festoon lights", "Lighting", 6, "A 10-metre string of warm lights for an inviting atmosphere.", "Lighting rack · B2"),
            Item("Folding chair", "Furniture", 60, "Comfortable, sturdy chairs that fold flat for easy transport.", "Main store · C1", 4),
            Item("Folding table", "Furniture", 15, "A 6-foot table for dining, displays and registration desks.", "Main store · C2"),
            Item("Projector", "Presentation", 4, "Bright HD projector with HDMI cable and remote control.", "Tech cupboard · D1"),
            Item("Projection screen", "Presentation", 4, "Portable pull-up screen, ready for presentations and film nights.", "Tech cupboard · D2"),
            Item("Pop-up gazebo", "Outdoor", 3, "A 3 × 3 metre shelter. Supplied with weights and a carry bag.", "Outdoor store · E1"),
            Item("Extension reel", "Presentation", 10, "A 15-metre cable reel with four sockets and thermal protection.", "Tech cupboard · D3"),
            Item("Speaker stand", "Audio", 8, "Adjustable tripod stand for a secure, elevated speaker setup.", "Audio cupboard · A4")
        };
        db.Equipment.AddRange(equipment);
        var people = new[] {
            new Borrower { Name = "Jamie Taylor", Email = "jamie@example.test", Phone = "" },
            new Borrower { Name = "Amira Patel", Email = "amira@example.test", Phone = "" },
            new Borrower { Name = "Alex Morgan", Email = "alex@example.test", Phone = "" }
        };
        db.Borrowers.AddRange(people);
        Booking BookingFor(string eventName, Borrower person, DateOnly start, DateOnly end, BookingStatus status, params (int index, int quantity)[] items) =>
            new() { EventName = eventName, Borrower = person, StartDate = start, EndDate = end, Status = status,
                CheckedOutAt = status == BookingStatus.CheckedOut ? DateTimeOffset.UtcNow : null,
                Notes = "Fictional booking for the EventKit development demo.",
                Items = items.Select(i => new BookingItem { Equipment = equipment[i.index], Quantity = i.quantity }).ToList() };
        db.Bookings.AddRange(
            BookingFor("Community film night", people[0], today, today, BookingStatus.Confirmed, (7, 1), (8, 1), (5, 20)),
            BookingFor("Autumn makers market", people[1], today, today.AddDays(1), BookingStatus.CheckedOut, (6, 4), (5, 12), (9, 1)),
            BookingFor("Charity music evening", people[2], today.AddDays(3), today.AddDays(3), BookingStatus.Confirmed, (0, 2), (1, 3), (3, 4)),
            BookingFor("Neighbourhood gathering", people[0], today.AddDays(7), today.AddDays(8), BookingStatus.Confirmed, (5, 25), (6, 5), (4, 2))
        );
        await db.SaveChangesAsync();
    }
}
