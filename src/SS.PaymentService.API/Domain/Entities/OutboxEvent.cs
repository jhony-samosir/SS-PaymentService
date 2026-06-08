using System;

namespace SS.PaymentService.API.Domain.Entities;

public class OutboxEvent
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public required string EventType { get; set; }
    public required string AggregateType { get; set; }
    public required string AggregateId { get; set; }
    public required string Payload { get; set; } // JSON format
    public string Status { get; set; } = "PENDING";
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
