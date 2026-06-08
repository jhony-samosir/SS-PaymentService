using System;
using System.Security.Cryptography;
using System.Text;

namespace SS.PaymentService.API.Infrastructure.Gateway;

public static class MidtransSignatureValidator
{
    public static bool Validate(string orderId, string statusCode, string grossAmount, string serverKey, string signatureKey)
    {
        if (string.IsNullOrWhiteSpace(orderId) ||
            string.IsNullOrWhiteSpace(statusCode) ||
            string.IsNullOrWhiteSpace(grossAmount) ||
            string.IsNullOrWhiteSpace(serverKey) ||
            string.IsNullOrWhiteSpace(signatureKey))
        {
            return false;
        }

        // Midtrans format: order_id + status_code + gross_amount + server_key
        var payload = $"{orderId}{statusCode}{grossAmount}{serverKey}";

        var calculated = ComputeSha512(payload);

        return string.Equals(calculated, signatureKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha512(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA512.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
