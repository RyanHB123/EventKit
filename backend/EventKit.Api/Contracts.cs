using EventKit.Api.Domain;

namespace EventKit.Api;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record EquipmentRequest(string? Name, string? Category, string? Description, string? Location, int TotalQuantity, int MaintenanceQuantity);
public sealed record BookingLineRequest(Guid EquipmentId, int Quantity);
public sealed record BookingRequest(string? EventName, string? BorrowerName, string? Email, string? Phone, DateOnly StartDate, DateOnly EndDate, string? Notes, List<BookingLineRequest>? Items);
public sealed record EquipmentDto(Guid Id, string Name, string Category, string Description, string Location, int TotalQuantity, int MaintenanceQuantity, int AvailableQuantity);
public sealed record BookingLineDto(Guid EquipmentId, string Name, string Category, int Quantity);
public sealed record BookingDto(Guid Id, string Reference, string EventName, string BorrowerName, string Email, string? Phone, DateOnly StartDate, DateOnly EndDate, string Status, string? Notes, bool IsOverdue, List<BookingLineDto> Items)
{
    public static BookingDto From(Booking booking, DateOnly today) => new(
        booking.Id, booking.Reference, booking.EventName, booking.Borrower.Name,
        booking.Borrower.Email, booking.Borrower.Phone, booking.StartDate, booking.EndDate,
        booking.Status.ToString(), booking.Notes,
        booking.Status == BookingStatus.CheckedOut && booking.EndDate < today,
        booking.Items.Select(i => new BookingLineDto(i.EquipmentId, i.Equipment.Name, i.Equipment.Category, i.Quantity)).ToList());
}
