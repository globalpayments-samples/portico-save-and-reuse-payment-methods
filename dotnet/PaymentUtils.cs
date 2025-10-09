using GlobalPayments.Api;
using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using GlobalPayments.Api.Gateways;
using dotenv.net;
using System.Text.RegularExpressions;

namespace CardPaymentSample;

/// <summary>
/// Payment utility functions for Global Payments SDK
/// </summary>
public static class PaymentUtils
{
    private static bool _sdkConfigured = false;

    /// <summary>
    /// Configure the Global Payments SDK
    /// </summary>
    public static void ConfigureSdk()
    {
        if (!_sdkConfigured)
        {
            DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true));
            
            ServicesContainer.ConfigureService(new PorticoConfig
            {
                SecretApiKey = System.Environment.GetEnvironmentVariable("SECRET_API_KEY"),
                DeveloperId = "000000",
                VersionNumber = "0000",
                ServiceUrl = "https://cert.api2.heartlandportico.com"
            });
            
            _sdkConfigured = true;
        }
    }

    /// <summary>
    /// Sanitize postal code by removing invalid characters
    /// </summary>
    public static string SanitizePostalCode(string? postalCode)
    {
        if (string.IsNullOrEmpty(postalCode))
            return string.Empty;

        var sanitized = Regex.Replace(postalCode, "[^a-zA-Z0-9-]", "");
        return sanitized.Length > 10 ? sanitized[..10] : sanitized;
    }

    /// <summary>
    /// Determine card brand from Global Payments card type
    /// </summary>
    public static string DetermineCardBrandFromType(string cardType)
    {
        if (string.IsNullOrEmpty(cardType)) return "Unknown";

        var type = cardType.ToLower();

        return type switch
        {
            "visa" => "Visa",
            "mastercard" or "mc" => "Mastercard",
            "amex" or "americanexpress" => "American Express",
            "discover" => "Discover",
            "jcb" => "JCB",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Create multi-use token with customer data attached
    /// </summary>
    public static async Task<MultiUseTokenResult> CreateMultiUseTokenWithCustomerAsync(string paymentToken, CustomerData customerData, CardDetails cardDetails)
    {
        return await Task.Run(() =>
        {
            try
            {
                var card = new CreditCardData
                {
                    Token = paymentToken
                };

                // Create address from customer data
                var address = new Address
                {
                    StreetAddress1 = customerData.StreetAddress?.Trim() ?? "",
                    City = customerData.City?.Trim() ?? "",
                    Province = customerData.State?.Trim() ?? "",
                    PostalCode = SanitizePostalCode(customerData.BillingZip),
                    Country = customerData.Country?.Trim() ?? ""
                };

                var response = card.Verify()
                    .WithCurrency("USD")
                    .WithRequestMultiUseToken(true)
                    .WithAddress(address)
                    .Execute();

                if (response.ResponseCode == "00")
                {
                    var brand = DetermineCardBrandFromType(cardDetails.CardType ?? "");

                    return new MultiUseTokenResult
                    {
                        MultiUseToken = response.Token ?? paymentToken,
                        Brand = brand,
                        Last4 = cardDetails.CardLast4 ?? "",
                        ExpiryMonth = cardDetails.ExpiryMonth ?? "",
                        ExpiryYear = cardDetails.ExpiryYear ?? "",
                        CustomerData = customerData
                    };
                }
                else
                {
                    throw new Exception($"Multi-use token creation failed: {response.ResponseMessage ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Multi-use token creation error: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Get card details from vault token using Global Payments SDK
    /// </summary>
    public static async Task<MultiUseTokenResult> GetCardDetailsFromTokenAsync(string vaultToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var card = new CreditCardData
                {
                    Token = vaultToken
                };

                var response = card.Verify()
                    .WithCurrency("USD")
                    .WithRequestMultiUseToken(true)
                    .Execute();

                if (response.ResponseCode == "00")
                {
                    var cardBrand = DetermineCardBrandFromType(response.CardType ?? "");
                    var last4 = response.CardDetails?.MaskedCardNumber?.Substring(Math.Max(0, response.CardDetails.MaskedCardNumber.Length - 4)) ?? "";
                    var expiryMonth = response.CardDetails?.CardExpMonth?.ToString().PadLeft(2, '0') ?? "";
                    var expiryYear = response.CardDetails?.CardExpYear?.ToString()[^2..] ?? "";

                    return new MultiUseTokenResult
                    {
                        Brand = cardBrand,
                        Last4 = last4,
                        ExpiryMonth = expiryMonth,
                        ExpiryYear = expiryYear,
                        MultiUseToken = response.Token ?? ""
                    };
                }
                else
                {
                    throw new Exception($"Token verification failed: {response.ResponseMessage ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SDK token lookup error: {ex.Message}");
                throw;
            }
        });
    }


    /// <summary>
    /// Process payment using Global Payments SDK
    /// </summary>
    public static async Task<PaymentResponse> ProcessPaymentWithSdkAsync(string vaultToken, decimal amount, string currency)
    {
        return await Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"💰 PAYMENT PROCESSING - Charging with token: {vaultToken[..8]}...");
                Console.WriteLine($"   💵 Amount: ${amount} {currency}");

                var card = new CreditCardData
                {
                    Token = vaultToken
                };

                var response = card.Charge(amount)
                    .WithCurrency(currency)
                    .Execute();

                if (response.ResponseCode == "00")
                {
                    // Log successful payment in live mode
                    Console.WriteLine("✅ 🟢 LIVE MODE - Payment Charged Successfully:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   🆔 Transaction ID: {response.TransactionId ?? "N/A"}");
                    Console.WriteLine($"   💵 Amount: ${amount} {currency}");
                    Console.WriteLine($"   🔐 Vault Token: {vaultToken[..8]}...");
                    Console.WriteLine($"   📋 Response Code: {response.ResponseCode}");
                    Console.WriteLine($"   💬 Response Message: {response.ResponseMessage ?? "Approved"}");
                    Console.WriteLine($"   🔑 Auth Code: {response.AuthorizationCode ?? "N/A"}");
                    Console.WriteLine($"   📄 Reference Number: {response.ReferenceNumber ?? "N/A"}");
                    Console.WriteLine($"   📡 API Status: Connected & Working");
                    
                    return new PaymentResponse
                    {
                        TransactionId = response.TransactionId ?? $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString()[^9..]}",
                        Amount = amount,
                        Currency = currency,
                        Status = "approved",
                        ResponseCode = response.ResponseCode,
                        ResponseMessage = response.ResponseMessage ?? "Approved",
                        Timestamp = DateTime.UtcNow,
                        GatewayResponse = new GatewayResponse
                        {
                            AuthCode = response.AuthorizationCode ?? "",
                            ReferenceNumber = response.ReferenceNumber ?? ""
                        }
                    };
                }
                else
                {
                    // Log failed payment
                    Console.WriteLine($"❌ 🔴 LIVE MODE - Payment Charge Failed:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   💵 Amount: ${amount} {currency}");
                    Console.WriteLine($"   🔐 Vault Token: {vaultToken[..8]}...");
                    Console.WriteLine($"   📋 Response Code: {response.ResponseCode}");
                    Console.WriteLine($"   ❌ Error: {response.ResponseMessage ?? "Unknown error"}");
                    Console.WriteLine($"   📡 API Status: Connected but Declined");
                    
                    throw new Exception($"Payment failed: {response.ResponseMessage ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ 🔴 LIVE MODE - Payment Processing Error:");
                Console.Error.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                Console.Error.WriteLine($"   💵 Amount: ${amount} {currency}");
                Console.Error.WriteLine($"   🔐 Vault Token: {vaultToken[..8]}...");
                Console.Error.WriteLine($"   ❌ SDK Error: {ex.Message}");
                Console.Error.WriteLine($"   📡 API Status: Connection Failed");
                Console.Error.WriteLine($"   🚫 NO FALLBACK - Error will be returned to user");
                throw;
            }
        });
    }

}