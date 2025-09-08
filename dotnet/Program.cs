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
        Console.WriteLine($"   POST /schedule-payment - Process $50 authorization");
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
            var secretKey = System.Environment.GetEnvironmentVariable("SECRET_API_KEY");
            var sdkStatus = !string.IsNullOrEmpty(secretKey) ? "configured" : "not_configured";
            
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

                var previousState = mockModeEnabled;
                mockModeEnabled = request.IsEnabled;
                
                Console.WriteLine($"🎭 Mock mode toggled from {previousState} to {mockModeEnabled}");
                
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
                return Results.Problem(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve payment methods",
                    ErrorCode = "SERVER_ERROR",
                    Timestamp = DateTime.UtcNow
                }.ToString());
            }
        });

        app.MapPost("/payment-methods", async (HttpContext context) =>
        {
            try
            {
                var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
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

                // Create new payment method - validate required fields first
                if (string.IsNullOrEmpty(data.CardNumber) || 
                    string.IsNullOrEmpty(data.ExpiryMonth) ||
                    string.IsNullOrEmpty(data.ExpiryYear) || 
                    string.IsNullOrEmpty(data.Cvv))
                {
                    return Results.BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Missing required card details",
                        ErrorCode = "VALIDATION_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return await HandleCreatePaymentMethodPhpStyle(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Payment methods error: {ex.Message}");
                return Results.Problem(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    ErrorCode = "SERVER_ERROR",
                    Timestamp = DateTime.UtcNow
                }.ToString());
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

        // Schedule payment endpoint
        app.MapPost("/schedule-payment", async (HttpContext context) =>
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

                var result = await ProcessAuthorization(request.PaymentMethodId, 50.00m);
                return Results.Ok(new ApiResponse<ScheduleResponseData>
                {
                    Success = true,
                    Data = result,
                    Message = "Payment scheduled successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schedule payment error: {ex.Message}");
                return Results.BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = "AUTHORIZATION_FAILED",
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
                return Results.Problem(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to update payment method",
                    ErrorCode = "UPDATE_ERROR",
                    Timestamp = DateTime.UtcNow
                }.ToString());
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
            return Results.Problem(new ApiResponse<object>
            {
                Success = false,
                Message = "Payment method update failed",
                ErrorCode = "SERVER_ERROR",
                Timestamp = DateTime.UtcNow
            }.ToString());
        }
    }

    private static async Task<IResult> HandleEditPaymentMethod(PaymentMethodData data)
    {
        try
        {
            // Validate payment method exists
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

            Console.WriteLine($"✏️ Editing payment method {data.Id}");
            Console.WriteLine($"   Card: {existingMethod.CardBrand} ending in {existingMethod.Last4}");
            Console.WriteLine($"   Nickname: {existingMethod.Nickname} → {data.Nickname}");
            Console.WriteLine($"   Default: {existingMethod.IsDefault} → {data.IsDefault}");

            // Update the payment method
            var editData = new PaymentMethodEditData
            {
                Nickname = data.Nickname,
                IsDefault = data.IsDefault
            };

            var updatedMethod = await JsonStorage.UpdatePaymentMethodAsync(data.Id!, editData);
            if (updatedMethod == null)
            {
                return Results.Problem("Failed to update payment method");
            }

            Console.WriteLine("✅ Payment method updated successfully");

            // Format response
            var response = new FormattedPaymentMethod
            {
                Id = updatedMethod.Id,
                Brand = updatedMethod.CardBrand,
                Last4 = updatedMethod.Last4,
                Expiry = $"{updatedMethod.ExpiryMonth}/{updatedMethod.ExpiryYear}",
                Nickname = updatedMethod.Nickname ?? "",
                IsDefault = updatedMethod.IsDefault,
                MockMode = false
            };

            return Results.Ok(new ApiResponse<FormattedPaymentMethod>
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
            return Results.Problem("Payment method update failed");
        }
    }

    private static async Task<IResult> HandleCreatePaymentMethodPhpStyle(PaymentMethodData data)
    {
        try
        {
            // Generate ID like PHP does
            var paymentMethodId = $"pm_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString()[^16..]}";
            var cleanCardNumber = data.CardNumber.Replace(" ", "");
            var expiryMonth = data.ExpiryMonth.PadLeft(2, '0');
            var expiryYear = data.ExpiryYear.Length > 2 ? data.ExpiryYear[^2..] : data.ExpiryYear;
            var last4 = cleanCardNumber[^4..];
            var cardBrand = PaymentUtils.DetermineCardBrand(cleanCardNumber);

            // Validate card data like PHP does
            if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19 || !cleanCardNumber.All(char.IsDigit))
            {
                return Results.BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid card number format",
                    ErrorCode = "VALIDATION_ERROR",
                    Timestamp = DateTime.UtcNow
                });
            }

            string vaultToken;
            bool isUsingMockMode = mockModeEnabled;

            if (mockModeEnabled)
            {
                // Generate mock vault token like PHP
                vaultToken = $"vault_{Guid.NewGuid().ToString().Replace("-", "")[^16..]}";
            }
            else
            {
                try
                {
                    // Try to create real vault token with SDK
                    vaultToken = CreateVaultTokenWithSDK(data, cleanCardNumber);
                }
                catch (Exception ex)
                {
                    // Return error instead of falling back to mock mode
                    Console.WriteLine($"Global Payments SDK error: {ex.Message}");
                    return Results.UnprocessableEntity(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Payment method creation failed: {ex.Message}",
                        ErrorCode = "PAYMENT_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Save payment method like PHP structure
            var storedData = new StoredPaymentMethodData
            {
                VaultToken = vaultToken,
                CardBrand = cardBrand,
                Last4 = last4,
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear,
                Nickname = data.Nickname ?? $"{cardBrand} ending in {last4}",
                IsDefault = data.IsDefault
            };

            var savedMethod = await JsonStorage.AddPaymentMethodAsync(storedData);

            // Format response to match PHP exactly
            var response = new
            {
                id = savedMethod.Id,
                vaultToken = vaultToken,
                type = "card",
                last4 = last4,
                brand = cardBrand,
                expiry = $"{expiryMonth}/{expiryYear}",
                nickname = savedMethod.Nickname,
                isDefault = savedMethod.IsDefault,
                mockMode = isUsingMockMode
            };

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
            return Results.Problem(new ApiResponse<object>
            {
                Success = false,
                Message = "Payment method creation failed",
                ErrorCode = "SERVER_ERROR",
                Timestamp = DateTime.UtcNow
            }.ToString());
        }
    }

    private static async Task<IResult> HandleCreatePaymentMethod(PaymentMethodData data)
    {
        try
        {
            // Validate card data
            var cleanCardNumber = data.CardNumber.Replace(" ", "");
            if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19 || !cleanCardNumber.All(char.IsDigit))
            {
                return Results.BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid card number format",
                    ErrorCode = "VALIDATION_ERROR",
                    Timestamp = DateTime.UtcNow
                });
            }

            var cardBrand = DetermineCardBrand(cleanCardNumber);
            var last4 = cleanCardNumber.Substring(cleanCardNumber.Length - 4);
            var expiry = $"{data.ExpiryMonth.PadLeft(2, '0')}/{data.ExpiryYear}";

            string vaultToken;
            bool isUsingMockMode = mockModeEnabled;

            if (mockModeEnabled)
            {
                // Use mock token
                vaultToken = $"mock_vault_{Guid.NewGuid().ToString()[^8..]}";
                Console.WriteLine($"🟡 Mock mode: Generated mock vault token for {cardBrand} ending in {last4}");
            }
            else
            {
                try
                {
                    // Try to create real vault token
                    vaultToken = CreateVaultTokenWithSDK(data, cleanCardNumber);
                    Console.WriteLine($"🟢 Live mode: Created vault token for {cardBrand} ending in {last4}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Live mode failed: {ex.Message}");
                    return Results.UnprocessableEntity(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Payment method creation failed: {ex.Message}",
                        ErrorCode = "PAYMENT_ERROR",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Save payment method
            var storedData = new StoredPaymentMethodData
            {
                VaultToken = vaultToken,
                CardBrand = cardBrand,
                Last4 = last4,
                ExpiryMonth = data.ExpiryMonth.PadLeft(2, '0'),
                ExpiryYear = data.ExpiryYear.Length > 2 ? data.ExpiryYear[^2..] : data.ExpiryYear,
                Nickname = data.Nickname ?? $"{cardBrand} ending in {last4}",
                IsDefault = data.IsDefault
            };

            var savedMethod = await JsonStorage.AddPaymentMethodAsync(storedData);

            var response = new FormattedPaymentMethod
            {
                Id = savedMethod.Id,
                Brand = savedMethod.CardBrand,
                Last4 = savedMethod.Last4,
                Expiry = $"{savedMethod.ExpiryMonth}/{savedMethod.ExpiryYear}",
                Nickname = savedMethod.Nickname ?? "",
                IsDefault = savedMethod.IsDefault,
                MockMode = isUsingMockMode
            };

            return Results.Ok(new ApiResponse<FormattedPaymentMethod>
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
            return Results.Problem("Payment method creation failed");
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
            // Process with real SDK
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

    private static async Task<ScheduleResponseData> ProcessAuthorization(string paymentMethodId, decimal amount)
    {
        var paymentMethod = await JsonStorage.FindPaymentMethodAsync(paymentMethodId);
        if (paymentMethod == null)
        {
            throw new Exception("Payment method not found");
        }

        Console.WriteLine($"⏰ Processing authorization for ${amount}");
        Console.WriteLine($"   Card: {paymentMethod.CardBrand} ending in {paymentMethod.Last4}");

        if (mockModeEnabled)
        {
            // Generate mock response
            var mockAuthId = $"auth_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var mockTransactionId = $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var expiresAt = DateTime.UtcNow.AddDays(7);

            Console.WriteLine($"🟡 Mock mode: Generated mock authorization {mockAuthId}");

            return new ScheduleResponseData
            {
                AuthorizationId = mockAuthId,
                TransactionId = mockTransactionId,
                Amount = amount,
                Currency = "USD",
                Status = "authorized",
                ResponseCode = "00",
                ResponseMessage = "AUTHORIZED",
                Timestamp = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                GatewayResponse = new GatewayResponse
                {
                    AuthCode = GenerateRandomString(6),
                    ReferenceNumber = $"ref_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                },
                PaymentMethod = new PaymentMethodInfo
                {
                    Id = paymentMethod.Id,
                    Brand = paymentMethod.CardBrand,
                    Last4 = paymentMethod.Last4,
                    Nickname = paymentMethod.Nickname ?? ""
                },
                MockMode = true,
                CaptureInfo = new CaptureInfo
                {
                    CanCapture = true,
                    ExpiresAt = expiresAt
                }
            };
        }
        else
        {
            // Process with real SDK
            try
            {
                var card = new CreditCardData { Token = paymentMethod.VaultToken };
                var response = card.Authorize(amount)
                    .WithCurrency("USD")
                    .WithAllowDuplicates(true)
                    .Execute();

                var expiresAt = DateTime.UtcNow.AddDays(7);
                Console.WriteLine($"🟢 Live authorization processed: {response.TransactionId}");

                return new ScheduleResponseData
                {
                    AuthorizationId = response.TransactionId ?? "",
                    TransactionId = response.TransactionId ?? "",
                    Amount = amount,
                    Currency = "USD",
                    Status = response.ResponseCode == "00" ? "authorized" : "declined",
                    ResponseCode = response.ResponseCode ?? "",
                    ResponseMessage = response.ResponseMessage ?? "",
                    Timestamp = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
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
                    MockMode = false,
                    CaptureInfo = new CaptureInfo
                    {
                        CanCapture = true,
                        ExpiresAt = expiresAt
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Authorization failed: {ex.Message}");
            }
        }
    }

    private static string CreateVaultTokenWithSDK(PaymentMethodData data, string cleanCardNumber)
    {
        var card = new CreditCardData
        {
            Number = cleanCardNumber,
            ExpMonth = int.Parse(data.ExpiryMonth),
            ExpYear = int.Parse(data.ExpiryYear.Length > 2 ? data.ExpiryYear.Substring(2) : data.ExpiryYear),
            Cvn = data.Cvv
        };

        try
        {
            // C# SDK has different pattern - Tokenize returns string directly in some versions
            // Try various SDK patterns to find the correct one for this version
            
            // First try: Direct tokenization
            try 
            {
                var token = card.Tokenize();
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
            catch (Exception tokenizeEx)
            {
                // If direct tokenize fails, try with a verification transaction
                var verifyTransaction = card.Verify();
                var response = verifyTransaction.Execute();
                
                if (response.ResponseCode == "00" && !string.IsNullOrEmpty(response.Token))
                {
                    return response.Token;
                }
                
                throw new Exception($"Tokenization failed: {response.ResponseMessage ?? tokenizeEx.Message}");
            }
            
            throw new Exception("No vault token returned from tokenization");
        }
        catch (Exception ex)
        {
            // If tokenize method doesn't work, try verification approach
            try
            {
                var response = card.Verify().Execute();

                if (response.ResponseCode != "00")
                {
                    throw new Exception($"Card verification failed: {response.ResponseMessage}");
                }

                // Check for token in response
                if (!string.IsNullOrEmpty(response.Token))
                {
                    return response.Token;
                }

                throw new Exception("No vault token returned from verification");
            }
            catch (Exception verifyEx)
            {
                throw new Exception($"Tokenization failed. Tokenize error: {ex.Message}. Verify error: {verifyEx.Message}");
            }
        }
    }

    private static string DetermineCardBrand(string cardNumber)
    {
        if (cardNumber.StartsWith("4")) return "Visa";
        if (cardNumber.StartsWith("5") || cardNumber.StartsWith("2")) return "Mastercard";
        if (cardNumber.StartsWith("3")) return cardNumber.StartsWith("34") || cardNumber.StartsWith("37") ? "American Express" : "Discover";
        if (cardNumber.StartsWith("6")) return "Discover";
        return "Unknown";
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}