using System;

namespace SS.PaymentService.API.Domain.Entities;

public class InboxEvent
{
    public required string MessageId { get; set; }
    public required string EventType { get; set; }
    public string? AggregateType { get; set; }
    public required string Payload { get; set; } // JSON format
    public string Status { get; set; } = "PROCESSED";
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; set; }
}
