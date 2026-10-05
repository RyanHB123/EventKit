using EventKit.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventKit.Api.Data;

public sealed class EventKitDbContext(DbContextOptions<EventKitDbContext> options) : DbContext(options)
{
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Borrower> Borrowers => Set<Borrower>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Equipment>(e => {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(600);
            e.Property(x => x.Location).HasMaxLength(100);
            e.ToTable(t => t.HasCheckConstraint("CK_Equipment_Stock", "\"TotalQuantity\" >= 0 AND \"MaintenanceQuantity\" >= 0 AND \"MaintenanceQuantity\" <= \"TotalQuantity\""));
        });
        model.Entity<Borrower>(e => {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.HasIndex(x => x.Email).IsUnique();
        });
        model.Entity<Booking>(e => {
            e.Property(x => x.Reference).HasMaxLength(20);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.EventName).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.StartDate, x.EndDate });
            e.HasOne(x => x.Borrower).WithMany().HasForeignKey(x => x.BorrowerId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Booking_Dates", "\"EndDate\" >= \"StartDate\""));
        });
        model.Entity<BookingItem>(e => {
            e.HasKey(x => new { x.BookingId, x.EquipmentId });
            e.HasOne(x => x.Booking).WithMany(x => x.Items).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_BookingItem_Quantity", "\"Quantity\" > 0"));
        });
        model.Entity<StaffUser>(e => {
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
        });
    }
}
