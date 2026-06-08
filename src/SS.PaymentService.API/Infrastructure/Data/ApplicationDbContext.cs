using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;

namespace SS.PaymentService.API.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentRefund> Refunds => Set<PaymentRefund>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Payment Configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            entity.Property(e => e.PublicId).HasColumnName("public_id").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
            entity.HasIndex(e => e.PublicId).IsUnique();

            entity.Property(e => e.OrderPublicId).HasColumnName("order_public_id").IsRequired();
            entity.HasIndex(e => e.OrderPublicId);

            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.UserPublicId).HasColumnName("user_public_id").IsRequired();

            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("IDR");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50).HasDefaultValue("Midtrans");

            entity.Property(e => e.PaymentStatus)
                .HasColumnName("payment_status")
                .HasConversion(
                    v => v.ToString(),
                    v => (PaymentStatus)Enum.Parse(typeof(PaymentStatus), v)
                )
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.PaymentReference).HasColumnName("payment_reference").HasMaxLength(255);
            entity.Property(e => e.SnapToken).HasColumnName("snap_token").HasMaxLength(255);
            entity.Property(e => e.SnapRedirectUrl).HasColumnName("snap_redirect_url").HasMaxLength(1000);

            // Audit
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by").HasMaxLength(100);

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // PaymentRefund Configuration
        modelBuilder.Entity<PaymentRefund>(entity =>
        {
            entity.ToTable("payment_refunds");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            entity.Property(e => e.PublicId).HasColumnName("public_id").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
            entity.HasIndex(e => e.PublicId).IsUnique();

            entity.Property(e => e.PaymentId).HasColumnName("payment_id").IsRequired();
            entity.Property(e => e.RefundAmount).HasColumnName("refund_amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.RefundReason).HasColumnName("refund_reason").HasMaxLength(500);

            entity.Property(e => e.RefundStatus)
                .HasColumnName("refund_status")
                .HasConversion(
                    v => v.ToString(),
                    v => (RefundStatus)Enum.Parse(typeof(RefundStatus), v)
                )
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.RefundReference).HasColumnName("refund_reference").HasMaxLength(255);

            // Audit
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("System");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by").HasMaxLength(100);

            entity.HasOne(e => e.Payment)
                .WithMany(p => p.Refunds)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // OutboxEvent Configuration
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.ToTable("outbox_events");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            entity.Property(e => e.PublicId).HasColumnName("public_id").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
            entity.HasIndex(e => e.PublicId).IsUnique();

            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(255);
            entity.Property(e => e.AggregateType).HasColumnName("aggregate_type").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("PENDING");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_events_status_created")
                .HasFilter("status = 'PENDING'");
        });

        // InboxEvent Configuration
        modelBuilder.Entity<InboxEvent>(entity =>
        {
            entity.ToTable("inbox_events");

            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasColumnName("message_id").HasMaxLength(255);
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(255);
            entity.Property(e => e.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("PROCESSED");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
        });
    }
}
