namespace EventKit.Api.Domain;

public sealed class Equipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Audio";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "Main store";
    public int TotalQuantity { get; set; }
    public int MaintenanceQuantity { get; set; }
    public bool Archived { get; set; }
}

public sealed class Borrower
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
}

public enum BookingStatus { Confirmed, CheckedOut, Completed, Cancelled }

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = $"EK-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    public string EventName { get; set; } = "";
    public Guid BorrowerId { get; set; }
    public Borrower Borrower { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CheckedOutAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public List<BookingItem> Items { get; set; } = [];
}

public sealed class BookingItem
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public int Quantity { get; set; }
}

public sealed class StaffUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "EventKit admin";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class DomainException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
