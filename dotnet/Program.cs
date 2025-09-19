using GlobalPayments.Api;
using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using dotenv.net;
using System.Text.Json;

namespace CardPaymentSample;

/// <summary>
/// Vault One-Click Payment Processing Application
/// 
/// Complete REST API implementation with payment method management,
/// mock mode support, and comprehensive error handling.
/// </summary>
public class Program
{
    private static bool mockModeEnabled = false;
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Main(string[] args)
    {
        // Load environment variables from .env file
        DotEnv.Load();

        var builder = WebApplication.CreateBuilder(args);
        
        // Add CORS support
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure static file serving
        app.UseDefaultFiles();
        app.UseStaticFiles();
        
        // Enable CORS
        app.UseCors("AllowAll");
        
        // Configure the SDK on startup
        ConfigureGlobalPaymentsSDK();

        // Configure all endpoints
        ConfigureEndpoints(app);
        
        var port = System.Environment.GetEnvironmentVariable("PORT") ?? "8000";
        app.Urls.Add($"http://0.0.0.0:{port}");
        
        Console.WriteLine($"🚀 .NET Server starting on port {port}");
        Console.WriteLine($"📋 Available endpoints:");
        Console.WriteLine($"   GET  /health - System health check");
        Console.WriteLine($"   GET  /config - Frontend configuration");
        Console.WriteLine($"   GET  /payment-methods - List payment methods");
        Console.WriteLine($"   POST /payment-methods - Create/edit payment methods");
        Console.WriteLine($"   POST /charge - Process $25 charge");
        Console.WriteLine($"   GET  /mock-mode - Get mock mode status");
        Console.WriteLine($"   POST /mock-mode - Toggle mock mode");
        
        app.Run();
    }

    private static void ConfigureGlobalPaymentsSDK()
    {
        try
        {
            ServicesContainer.ConfigureService(new PorticoConfig
            {
                SecretApiKey = System.Environment.GetEnvironmentVariable("SECRET_API_KEY"),
                DeveloperId = "000000",
                VersionNumber = "0000",
                ServiceUrl = "https://cert.api2.heartlandportico.com"
            });
            
            Console.WriteLine("✅ Global Payments SDK configured");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  SDK configuration failed: {ex.Message}");
        }
    }

    private static void ConfigureEndpoints(WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () =>
        {
            return Results.Ok(new ApiResponse<HealthData>
            {
                Success = true,
                Data = new HealthData
                {
                    Status = "healthy",
                    Timestamp = DateTime.UtcNow,
                    Service = "vault-one-click-dotnet",
                    Version = "1.0.0"
                },
                Message = "System is healthy",
                Timestamp = DateTime.UtcNow
            });
        });

        // Configuration endpoint
        app.MapGet("/config", () =>
        {
            var publicKey = System.Environment.GetEnvironmentVariable("PUBLIC_API_KEY") ?? "pk_test_demo_key";
            
            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { PublicApiKey = publicKey, MockMode = mockModeEnabled },
                Timestamp = DateTime.UtcNow
            });
        });

        // Mock mode endpoints
        app.MapGet("/mock-mode", () =>
        {
            return Results.Ok(new ApiResponse<MockModeConfig>
            {
                Success = true,
                Data = new MockModeConfig { IsEnabled = mockModeEnabled },
                Message = $"Mock mode is {(mockModeEnabled ? "enabled" : "disabled")}",
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapPost("/mock-mode", async (HttpContext context) =>
        {
            try
            {
                var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<MockModeConfig>(json, jsonOptions);
                
                if (request == null)
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid request body",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                mockModeEnabled = request.IsEnabled;

                Console.WriteLine($"🎭 Mock mode {(mockModeEnabled ? "enabled" : "disabled")}");
                
                return Results.Ok(new ApiResponse<MockModeConfig>
                {
                    Success = true,
                    Data = new MockModeConfig { IsEnabled = mockModeEnabled },
                    Message = $"Mock mode {(mockModeEnabled ? "enabled" : "disabled")} successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                return Results.BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid JSON format",
                    ErrorCode = "VALIDATION_ERROR",
                    Timestamp = DateTime.UtcNow
                });
            }
        });

        // Payment methods endpoints
        app.MapGet("/payment-methods", async () =>
        {
            try
            {
                var methods = await JsonStorage.LoadPaymentMethodsAsync();
                
                // Format methods to match PHP structure exactly
                var formattedMethods = methods.Select(method => new
                {
                    id = method.Id,
                    type = "card",
                    last4 = method.Last4,
                    brand = method.CardBrand,
                    expiry = $"{method.ExpiryMonth}/{method.ExpiryYear}",
                    isDefault = method.IsDefault,
                    nickname = method.Nickname ?? ""
                }).ToList();
                
                return Results.Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = formattedMethods,
                    Message = "Payment methods retrieved successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving payment methods: {ex.Message}");
                return Results.Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve payment methods",
                    ErrorCode = "SERVER_ERROR",
                    Timestamp = DateTime.UtcNow
                }, statusCode: 500);
            }
        });

        app.MapPost("/payment-methods", async (HttpContext context) =>
        {
            try
            {
                var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
                Console.WriteLine($"Received payment method request: {json}");
                var data = JsonSerializer.Deserialize<PaymentMethodData>(json, jsonOptions);
                
                if (data == null)
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid request body",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Check if this is an edit operation (has 'id' field)
                if (!string.IsNullOrEmpty(data.Id))
                {
                    return await HandleEditPaymentMethodPhpStyle(data);
                }

                // Create new payment method using payment_token from GP PaymentForm
                if (string.IsNullOrEmpty(data.PaymentToken))
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Missing required payment_token",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                if (data.CardDetails == null)
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Missing required cardDetails",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return await HandleCreatePaymentMethodMultiUse(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Payment methods error: {ex.Message}");
                return Results.Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    ErrorCode = "SERVER_ERROR",
                    Timestamp = DateTime.UtcNow
                }, statusCode: 500);
            }
        });

        // Charge endpoint
        app.MapPost("/charge", async (HttpContext context) =>
        {
            try
            {
                var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<PaymentRequest>(json, jsonOptions);
                
                if (request == null || string.IsNullOrEmpty(request.PaymentMethodId))
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Payment method ID is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                var result = await ProcessPayment(request.PaymentMethodId, 25.00m, "charge");
                return Results.Ok(new ApiResponse<ChargeResponseData>
                {
                    Success = true,
                    Data = result,
                    Message = "Payment processed successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Charge error: {ex.Message}");
                return Results.BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = "PAYMENT_FAILED",
                    Timestamp = DateTime.UtcNow
                });
            }
        });

    }

    private static async Task<IResult> HandleEditPaymentMethodPhpStyle(PaymentMethodData data)
    {
        try
        {
            // Validate payment method exists (PHP style)
            var existingMethod = await JsonStorage.FindPaymentMethodAsync(data.Id!);
            if (existingMethod == null)
            {
                return Results.NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Payment method not found",
                    ErrorCode = "NOT_FOUND",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Update the payment method (only editable fields)
            var editData = new PaymentMethodEditData
            {
                Nickname = data.Nickname,
                IsDefault = data.IsDefault
            };

            var updatedMethod = await JsonStorage.UpdatePaymentMethodAsync(data.Id!, editData);
            if (updatedMethod == null)
            {
                return Results.Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to update payment method",
                    ErrorCode = "UPDATE_ERROR",
                    Timestamp = DateTime.UtcNow
                }, statusCode: 500);
            }

            // Format response to match PHP exactly
            var response = new
            {
                id = updatedMethod.Id,
                type = "card",
                last4 = updatedMethod.Last4,
                brand = updatedMethod.CardBrand,
                expiry = $"{updatedMethod.ExpiryMonth}/{updatedMethod.ExpiryYear}",
                nickname = updatedMethod.Nickname ?? "",
                isDefault = updatedMethod.IsDefault,
                updatedAt = updatedMethod.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Data = response,
                Message = "Payment method updated successfully",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating payment method: {ex.Message}");
            return Results.Json(new ApiResponse<object>
            {
                Success = false,
                Message = "Payment method update failed",
                ErrorCode = "SERVER_ERROR",
                Timestamp = DateTime.UtcNow
            }, statusCode: 500);
        }
    }


    private static async Task<IResult> HandleCreatePaymentMethodMultiUse(PaymentMethodData data)
    {
        try
        {
            var paymentToken = data.PaymentToken!;
            var cardDetails = data.CardDetails!;

            // Create customer data from flat properties if available, otherwise use nested CustomerData
            var customerData = data.CustomerData ?? new CustomerData
            {
                FirstName = data.FirstName,
                LastName = data.LastName,
                Email = data.Email,
                Phone = data.Phone,
                StreetAddress = data.StreetAddress,
                City = data.City,
                State = data.State,
                BillingZip = data.BillingZip,
                Country = data.Country
            };

            Console.WriteLine($"💳 Creating payment method with token: {paymentToken.Substring(0, Math.Min(8, paymentToken.Length))}...");
            Console.WriteLine($"   Nickname: {data.Nickname ?? "None"}");
            Console.WriteLine($"   Default: {data.IsDefault}");
            Console.WriteLine($"   Mock mode: {mockModeEnabled}");

            // Create multi-use token with customer data or use mock
            MultiUseTokenResult? multiUseTokenData = null;
            var finalToken = paymentToken;

            if (!mockModeEnabled && !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SECRET_API_KEY")))
            {
                try
                {
                    multiUseTokenData = await PaymentUtils.CreateMultiUseTokenWithCustomerAsync(paymentToken, customerData, cardDetails);
                    finalToken = multiUseTokenData.MultiUseToken;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Multi-use token creation error: {ex.Message}");
                    // Fall back to mock mode if token creation fails
                    mockModeEnabled = true;
                }
            }

            // Use mock data in mock mode or if token creation failed
            if (mockModeEnabled || multiUseTokenData == null)
            {
                var brand = PaymentUtils.DetermineCardBrandFromType(cardDetails.CardType ?? "");
                multiUseTokenData = new MultiUseTokenResult
                {
                    MultiUseToken = paymentToken,
                    Brand = brand,
                    Last4 = cardDetails.CardLast4 ?? "",
                    ExpiryMonth = cardDetails.ExpiryMonth ?? "",
                    ExpiryYear = cardDetails.ExpiryYear ?? "",
                    CustomerData = customerData
                };
            }

            var storedData = new StoredPaymentMethodData
            {
                VaultToken = finalToken,
                CardBrand = multiUseTokenData.Brand,
                Last4 = multiUseTokenData.Last4,
                ExpiryMonth = multiUseTokenData.ExpiryMonth,
                ExpiryYear = multiUseTokenData.ExpiryYear,
                Nickname = data.Nickname ?? $"{multiUseTokenData.Brand} ending in {multiUseTokenData.Last4}",
                IsDefault = data.IsDefault,
                CustomerData = customerData
            };

            var savedMethod = await JsonStorage.AddPaymentMethodAsync(storedData);

            var response = new
            {
                id = savedMethod.Id,
                vaultToken = finalToken,
                type = "card",
                last4 = multiUseTokenData.Last4,
                brand = multiUseTokenData.Brand,
                expiry = $"{multiUseTokenData.ExpiryMonth}/{multiUseTokenData.ExpiryYear}",
                nickname = savedMethod.Nickname,
                isDefault = savedMethod.IsDefault,
                mockMode = mockModeEnabled
            };

            Console.WriteLine("✅ Payment method created successfully");

            return Results.Ok(new ApiResponse<object>
            {
                Success = true,
                Data = response,
                Message = "Payment method created and saved successfully",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating payment method: {ex.Message}");
            return Results.Json(new ApiResponse<object>
            {
                Success = false,
                Message = "Payment method creation failed",
                ErrorCode = "SERVER_ERROR",
                Timestamp = DateTime.UtcNow
            }, statusCode: 500);
        }
    }


    private static async Task<ChargeResponseData> ProcessPayment(string paymentMethodId, decimal amount, string type)
    {
        var paymentMethod = await JsonStorage.FindPaymentMethodAsync(paymentMethodId);
        if (paymentMethod == null)
        {
            throw new Exception("Payment method not found");
        }

        Console.WriteLine($"💳 Processing {type} for ${amount}");
        Console.WriteLine($"   Card: {paymentMethod.CardBrand} ending in {paymentMethod.Last4}");

        if (mockModeEnabled)
        {
            // Generate mock response
            var mockTransactionId = $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var mockAuthCode = GenerateRandomString(6);
            var mockRefNumber = $"ref_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            Console.WriteLine($"🟡 Mock mode: Generated mock transaction {mockTransactionId}");

            return new ChargeResponseData
            {
                TransactionId = mockTransactionId,
                Amount = amount,
                Currency = "USD",
                Status = "approved",
                ResponseCode = "00",
                ResponseMessage = "APPROVAL",
                Timestamp = DateTime.UtcNow,
                GatewayResponse = new GatewayResponse
                {
                    AuthCode = mockAuthCode,
                    ReferenceNumber = mockRefNumber
                },
                PaymentMethod = new PaymentMethodInfo
                {
                    Id = paymentMethod.Id,
                    Brand = paymentMethod.CardBrand,
                    Last4 = paymentMethod.Last4,
                    Nickname = paymentMethod.Nickname ?? ""
                },
                MockMode = true
            };
        }
        else
        {
            // Process with real SDK using token directly
            try
            {
                var card = new CreditCardData { Token = paymentMethod.VaultToken };
                var response = card.Charge(amount)
                    .WithCurrency("USD")
                    .WithAllowDuplicates(true)
                    .Execute();

                Console.WriteLine($"🟢 Live payment processed: {response.TransactionId}");

                return new ChargeResponseData
                {
                    TransactionId = response.TransactionId ?? "",
                    Amount = amount,
                    Currency = "USD",
                    Status = response.ResponseCode == "00" ? "approved" : "declined",
                    ResponseCode = response.ResponseCode ?? "",
                    ResponseMessage = response.ResponseMessage ?? "",
                    Timestamp = DateTime.UtcNow,
                    GatewayResponse = new GatewayResponse
                    {
                        AuthCode = response.AuthorizationCode ?? "",
                        ReferenceNumber = response.ReferenceNumber ?? ""
                    },
                    PaymentMethod = new PaymentMethodInfo
                    {
                        Id = paymentMethod.Id,
                        Brand = paymentMethod.CardBrand,
                        Last4 = paymentMethod.Last4,
                        Nickname = paymentMethod.Nickname ?? ""
                    },
                    MockMode = false
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Payment processing failed: {ex.Message}");
            }
        }
    }



    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

