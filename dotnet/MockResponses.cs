namespace CardPaymentSample;

/// <summary>
/// Mock responses for testing payment scenarios
/// </summary>
public static class MockResponses
{
    /// <summary>
    /// Get response type based on card's last 4 digits
    /// </summary>
    public static string GetResponseByCardNumber(string last4)
    {
        // Success scenarios - Heartland Portico compatible test cards
        var successCards = new[] { "0016", "0014", "6527", "0608", "1111", "1112" };
        if (successCards.Contains(last4))
        {
            return "success";
        }

        // Decline scenarios
        var declineMap = new Dictionary<string, string>
        {
            {"0002", "decline_insufficient_funds"},
            {"0004", "decline_generic"},
            {"0005", "decline_pickup_card"},
            {"0041", "decline_lost_card"},
            {"0043", "decline_stolen_card"},
            {"0051", "decline_expired_card"},
            {"0054", "decline_incorrect_cvc"},
            {"0055", "decline_incorrect_zip"},
            {"0065", "decline_card_declined"},
            {"0076", "decline_invalid_account"},
            {"0078", "decline_card_not_activated"},
            {"0091", "error_processing_error"},
            {"0096", "error_system_error"}
        };

        return declineMap.TryGetValue(last4, out var responseType) ? responseType : "success";
    }

    /// <summary>
    /// Get successful payment response
    /// </summary>
    public static PaymentResponse GetPaymentResponse(decimal amount, string paymentMethodId)
    {
        var random = new Random();
        
        return new PaymentResponse
        {
            TransactionId = $"txn_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString()[^9..]}",
            Amount = amount,
            Currency = "USD",
            Status = "approved",
            ResponseCode = "00",
            ResponseMessage = "Approved",
            Timestamp = DateTime.UtcNow,
            GatewayResponse = new GatewayResponse
            {
                AuthCode = $"A{random.Next(10000, 99999):D5}",
                ReferenceNumber = $"REF{random.Next(100000000, 999999999):D10}"
            }
        };
    }


    /// <summary>
    /// Get decline response with specific reason
    /// </summary>
    public static DeclineResponse GetDeclineResponse(string reason)
    {
        var declineReasons = new Dictionary<string, string>
        {
            {"insufficient_funds", "Insufficient Funds"},
            {"generic", "Card Declined"},
            {"pickup_card", "Pick Up Card"},
            {"lost_card", "Lost Card"},
            {"stolen_card", "Stolen Card"},
            {"expired_card", "Expired Card"},
            {"incorrect_cvc", "Incorrect CVC"},
            {"incorrect_zip", "Incorrect ZIP"},
            {"card_declined", "Card Declined"},
            {"invalid_account", "Invalid Account"},
            {"card_not_activated", "Card Not Activated"},
            {"processing_error", "Processing Error"},
            {"system_error", "System Error"}
        };

        var message = declineReasons.TryGetValue(reason, out var msg) ? msg : "Card Declined";

        return new DeclineResponse
        {
            ErrorCode = reason.ToUpper(),
            ResponseMessage = message
        };
    }

    /// <summary>
    /// Generate mock vault token
    /// </summary>
    public static string GenerateMockVaultToken()
    {
        return $"token_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid().ToString().Replace("-", "")[..16]}";
    }
}