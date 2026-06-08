using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SS.PaymentService.API.Infrastructure.Gateway;

public sealed class MidtransClient
{
    private readonly HttpClient _httpClient;
    private readonly MidtransOptions _options;

    public MidtransClient(HttpClient httpClient, IOptions<MidtransOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var serverKeyBytes = Encoding.UTF8.GetBytes($"{_options.ServerKey}:");
        var base64Auth = Convert.ToBase64String(serverKeyBytes);

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
    }

    public async Task<SnapResponse?> CreateSnapTransactionAsync(Guid paymentPublicId, decimal amount, string currency = "IDR")
    {
        var snapUrl = $"{_options.SnapBaseUrl.TrimEnd('/')}/snap/v1/transactions";

        var payload = new
        {
            transaction_details = new
            {
                order_id = paymentPublicId.ToString(),
                gross_amount = (long)amount
            },
            credit_card = new
            {
                secure = true
            }
        };

        var response = await _httpClient.PostAsJsonAsync(snapUrl, payload);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Midtrans Snap API failed: {response.StatusCode} - {errorContent}");
        }

        return await response.Content.ReadFromJsonAsync<SnapResponse>();
    }

    public async Task<bool> CancelTransactionAsync(string paymentPublicId)
    {
        var cancelUrl = $"{_options.ApiBaseUrl.TrimEnd('/')}/v2/{paymentPublicId}/cancel";

        var response = await _httpClient.PostAsync(cancelUrl, null);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Midtrans Cancel API failed: {response.StatusCode} - {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<MidtransCancelResponse>();
        return result?.StatusCode == "200" || result?.StatusCode == "412"; // 412 is already cancelled / done
    }

    public async Task<MidtransRefundResponse?> RefundTransactionAsync(string paymentPublicId, decimal amount, string reason)
    {
        var refundUrl = $"{_options.ApiBaseUrl.TrimEnd('/')}/v2/{paymentPublicId}/refund";

        var payload = new
        {
            refund_key = Guid.NewGuid().ToString(),
            amount = (long)amount,
            reason = reason
        };

        var response = await _httpClient.PostAsJsonAsync(refundUrl, payload);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Midtrans Refund API failed: {response.StatusCode} - {errorContent}");
        }

        return await response.Content.ReadFromJsonAsync<MidtransRefundResponse>();
    }
}

public class SnapResponse
{
    public string Token { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}

public class MidtransCancelResponse
{
    public string StatusCode { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
}

public class MidtransRefundResponse
{
    public string StatusCode { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string RefundChargebackId { get; set; } = string.Empty;
}
