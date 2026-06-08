namespace SS.PaymentService.API.Infrastructure.Gateway;

public class MidtransOptions
{
    public const string SectionName = "Midtrans";

    public string ServerKey { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public bool IsProduction { get; set; } = false;
    public string SnapBaseUrl { get; set; } = "https://app.sandbox.midtrans.com";
    public string ApiBaseUrl { get; set; } = "https://api.sandbox.midtrans.com";
}
