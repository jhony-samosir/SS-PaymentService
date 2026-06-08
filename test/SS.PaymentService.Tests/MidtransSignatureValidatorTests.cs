using System;
using SS.PaymentService.API.Infrastructure.Gateway;
using Xunit;

namespace SS.PaymentService.Tests;

public class MidtransSignatureValidatorTests
{
    [Fact]
    public void Validate_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        var orderId = "test-order-123";
        var statusCode = "200";
        var grossAmount = "150000.00";
        var serverKey = "SB-Mid-server-lhjG66e138kUoN7cW5_50z9Z";

        // Signature = SHA512(orderId + statusCode + grossAmount + serverKey)
        // test-order-123200150000.00SB-Mid-server-lhjG66e138kUoN7cW5_50z9Z
        var rawPayload = $"{orderId}{statusCode}{grossAmount}{serverKey}";
        using var sha512 = System.Security.Cryptography.SHA512.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawPayload);
        var hash = sha512.ComputeHash(bytes);
        var expectedSignature = Convert.ToHexString(hash);

        // Act
        var result = MidtransSignatureValidator.Validate(orderId, statusCode, grossAmount, serverKey, expectedSignature);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Validate_WithInvalidSignature_ReturnsFalse()
    {
        // Arrange
        var orderId = "test-order-123";
        var statusCode = "200";
        var grossAmount = "150000.00";
        var serverKey = "SB-Mid-server-lhjG66e138kUoN7cW5_50z9Z";
        var invalidSignature = "invalid_signature_hex_value";

        // Act
        var result = MidtransSignatureValidator.Validate(orderId, statusCode, grossAmount, serverKey, invalidSignature);

        // Assert
        Assert.False(result);
    }
}
