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
    /// Determine card brand from card number
    /// </summary>
    public static string DetermineCardBrand(string cardNumber)
    {
        var cleanNumber = Regex.Replace(cardNumber, @"\s+", "");

        if (Regex.IsMatch(cleanNumber, @"^4"))
            return "Visa";
        else if (Regex.IsMatch(cleanNumber, @"^5[1-5]") || Regex.IsMatch(cleanNumber, @"^2[2-7]"))
            return "Mastercard";
        else if (Regex.IsMatch(cleanNumber, @"^3[47]"))
            return "American Express";
        else if (Regex.IsMatch(cleanNumber, @"^6(?:011|5)"))
            return "Discover";
        else
            return "Unknown";
    }

    /// <summary>
    /// Create vault token using Global Payments SDK
    /// </summary>
    public static async Task<string> CreateVaultTokenWithSdkAsync(PaymentMethodData data)
    {
        return await Task.Run(() =>
        {
            try
            {
                var card = new CreditCardData
                {
                    Number = data.CardNumber,
                    ExpMonth = int.Parse(data.ExpiryMonth),
                    ExpYear = int.Parse(data.ExpiryYear.Length > 2 ? data.ExpiryYear.Substring(2) : data.ExpiryYear),
                    Cvn = data.Cvv
                };

                // Add billing address if available, otherwise use default
                if (data.BillingAddress != null)
                {
                    card.CardHolderName = data.BillingAddress.Name;
                }
                else
                {
                    card.CardHolderName = "Test User";
                }

                Console.WriteLine($"🔑 PAYMENT METHOD CREATION - Attempting tokenization for card ending in {data.CardNumber[^4..]}");
                Console.WriteLine($"   📝 Card Brand: {DetermineCardBrand(data.CardNumber)}");
                Console.WriteLine($"   📅 Expiry: {data.ExpiryMonth}/{data.ExpiryYear}");
                Console.WriteLine($"   👤 Nickname: {data.Nickname ?? "None"}");
                Console.WriteLine($"   ⭐ Set as Default: {data.IsDefault}");

                // For Portico, we need to use Verify() with store flag to create a usable token
                var response = card.Verify()
                    .WithRequestMultiUseToken(true)
                    .Execute();

                if (response.ResponseCode == "00" && !string.IsNullOrEmpty(response.Token))
                {
                    // Log successful tokenization in live mode
                    Console.WriteLine("✅ 🟢 LIVE MODE - Payment Method Created Successfully:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   🔐 Vault Token: {response.Token}");
                    Console.WriteLine($"   💳 Card Brand: {DetermineCardBrand(data.CardNumber)}");
                    Console.WriteLine($"   🔢 Last 4: {data.CardNumber[^4..]}");
                    Console.WriteLine($"   📅 Expiry: {data.ExpiryMonth}/{data.ExpiryYear}");
                    Console.WriteLine($"   📛 Nickname: {data.Nickname ?? "None"}");
                    Console.WriteLine($"   ⭐ Default: {data.IsDefault}");
                    Console.WriteLine($"   📡 API Status: Connected & Working");
                    
                    return response.Token;
                }
                else
                {
                    // Log failed tokenization attempt
                    Console.WriteLine($"❌ 🔴 LIVE MODE - Payment Method Creation Failed:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   💳 Card Brand: {DetermineCardBrand(data.CardNumber)}");
                    Console.WriteLine($"   🔢 Last 4: {data.CardNumber[^4..]}");
                    Console.WriteLine($"   📅 Expiry: {data.ExpiryMonth}/{data.ExpiryYear}");
                    Console.WriteLine($"   ❌ Error: {response.ResponseMessage ?? "No token returned"}");
                    Console.WriteLine($"   📡 API Status: Connected but Declined");
                    
                    throw new Exception($"Tokenization failed: {response.ResponseMessage ?? "No token returned"}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ 🔴 LIVE MODE - Payment Method Creation Error:");
                Console.Error.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                Console.Error.WriteLine($"   💳 Card Brand: {DetermineCardBrand(data.CardNumber)}");
                Console.Error.WriteLine($"   🔢 Last 4: {data.CardNumber[^4..]}");
                Console.Error.WriteLine($"   ❌ SDK Error: {ex.Message}");
                Console.Error.WriteLine($"   📡 API Status: Connection Failed");
                Console.Error.WriteLine($"   🚫 NO FALLBACK - Error will be returned to user");
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

    /// <summary>
    /// Create authorization using Global Payments SDK
    /// </summary>
    public static async Task<AuthorizationResponse> CreateAuthorizationWithSdkAsync(string vaultToken, decimal amount, string currency)
    {
        return await Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"⏰ PAYMENT SCHEDULING - Creating authorization with token: {vaultToken[..8]}...");
                Console.WriteLine($"   💵 Amount: ${amount} {currency}");

                var card = new CreditCardData
                {
                    Token = vaultToken
                };

                var response = card.Authorize(amount)
                    .WithCurrency(currency)
                    .Execute();

                if (response.ResponseCode == "00")
                {
                    var expiresAt = DateTime.UtcNow.AddDays(7);
                    
                    // Log successful authorization in live mode
                    Console.WriteLine("✅ 🟢 LIVE MODE - Payment Authorization Created Successfully:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   🆔 Transaction ID: {response.TransactionId ?? "N/A"}");
                    Console.WriteLine($"   💵 Amount: ${amount} {currency}");
                    Console.WriteLine($"   🔐 Vault Token: {vaultToken[..8]}...");
                    Console.WriteLine($"   📋 Response Code: {response.ResponseCode}");
                    Console.WriteLine($"   💬 Response Message: {response.ResponseMessage ?? "Authorized"}");
                    Console.WriteLine($"   🔑 Auth Code: {response.AuthorizationCode ?? "N/A"}");
                    Console.WriteLine($"   📄 Reference Number: {response.ReferenceNumber ?? "N/A"}");
                    Console.WriteLine($"   ⏰ Expires: {expiresAt:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   📡 API Status: Connected & Working");
                    
                    return new AuthorizationResponse
                    {
                        AuthorizationId = $"auth_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString()[^9..]}",
                        TransactionId = response.TransactionId ?? $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString()[^9..]}",
                        Amount = amount,
                        Currency = currency,
                        Status = "authorized",
                        ResponseCode = response.ResponseCode,
                        ResponseMessage = response.ResponseMessage ?? "Authorized",
                        Timestamp = DateTime.UtcNow,
                        ExpiresAt = expiresAt,
                        GatewayResponse = new GatewayResponse
                        {
                            AuthCode = response.AuthorizationCode ?? "",
                            ReferenceNumber = response.ReferenceNumber ?? ""
                        }
                    };
                }
                else
                {
                    // Log failed authorization
                    Console.WriteLine($"❌ 🔴 LIVE MODE - Payment Authorization Failed:");
                    Console.WriteLine($"   ⏰ Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
                    Console.WriteLine($"   💵 Amount: ${amount} {currency}");
                    Console.WriteLine($"   🔐 Vault Token: {vaultToken[..8]}...");
                    Console.WriteLine($"   📋 Response Code: {response.ResponseCode}");
                    Console.WriteLine($"   ❌ Error: {response.ResponseMessage ?? "Unknown error"}");
                    Console.WriteLine($"   📡 API Status: Connected but Declined");
                    
                    throw new Exception($"Authorization failed: {response.ResponseMessage ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ 🔴 LIVE MODE - Payment Authorization Error:");
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