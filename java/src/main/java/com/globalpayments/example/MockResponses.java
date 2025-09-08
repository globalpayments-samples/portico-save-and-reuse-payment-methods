package com.globalpayments.example;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

/**
 * Mock responses for testing payment scenarios
 */
public class MockResponses {
    
    /**
     * Get response type based on card's last 4 digits
     */
    public static String getResponseByCardNumber(String last4) {
        // Success scenarios
        if ("1111".equals(last4) || "4242".equals(last4) || "0000".equals(last4)) {
            return "success";
        }
        
        // Decline scenarios
        if ("0002".equals(last4)) return "decline_insufficient_funds";
        if ("0004".equals(last4)) return "decline_generic";
        if ("0005".equals(last4)) return "decline_pickup_card";
        if ("0041".equals(last4)) return "decline_lost_card";
        if ("0043".equals(last4)) return "decline_stolen_card";
        if ("0051".equals(last4)) return "decline_expired_card";
        if ("0054".equals(last4)) return "decline_incorrect_cvc";
        if ("0055".equals(last4)) return "decline_incorrect_zip";
        if ("0065".equals(last4)) return "decline_card_declined";
        if ("0076".equals(last4)) return "decline_invalid_account";
        if ("0078".equals(last4)) return "decline_card_not_activated";
        
        // Error scenarios  
        if ("0091".equals(last4)) return "error_processing_error";
        if ("0096".equals(last4)) return "error_system_error";
        
        // Default to success for unknown cards
        return "success";
    }
    
    /**
     * Get successful payment response
     */
    public static Map<String, Object> getPaymentResponse(BigDecimal amount, String paymentMethodId) {
        Map<String, Object> response = new HashMap<>();
        response.put("transactionId", "txn_" + UUID.randomUUID().toString());
        response.put("amount", amount);
        response.put("currency", "USD");
        response.put("status", "approved");
        response.put("responseCode", "00");
        response.put("responseMessage", "Approved");
        response.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
        
        Map<String, Object> gatewayResponse = new HashMap<>();
        gatewayResponse.put("authCode", "A" + String.format("%05d", (int)(Math.random() * 100000)));
        gatewayResponse.put("referenceNumber", "REF" + String.format("%010d", (int)(Math.random() * 1000000000)));
        response.put("gatewayResponse", gatewayResponse);
        
        return response;
    }
    
    /**
     * Get successful authorization response
     */
    public static Map<String, Object> getAuthorizationResponse(BigDecimal amount, String paymentMethodId) {
        Map<String, Object> response = new HashMap<>();
        response.put("authorizationId", "auth_" + UUID.randomUUID().toString());
        response.put("transactionId", "txn_" + UUID.randomUUID().toString());
        response.put("amount", amount);
        response.put("currency", "USD");
        response.put("status", "authorized");
        response.put("responseCode", "00");
        response.put("responseMessage", "Authorized");
        response.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
        response.put("expiresAt", LocalDateTime.now().plusDays(7).format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
        
        Map<String, Object> gatewayResponse = new HashMap<>();
        gatewayResponse.put("authCode", "A" + String.format("%05d", (int)(Math.random() * 100000)));
        gatewayResponse.put("referenceNumber", "REF" + String.format("%010d", (int)(Math.random() * 1000000000)));
        response.put("gatewayResponse", gatewayResponse);
        
        return response;
    }
    
    /**
     * Get decline response with specific reason
     */
    public static Map<String, String> getDeclineResponse(String reason) {
        Map<String, String> declineReasons = new HashMap<>();
        declineReasons.put("insufficient_funds", "Insufficient Funds");
        declineReasons.put("generic", "Card Declined"); 
        declineReasons.put("pickup_card", "Pick Up Card");
        declineReasons.put("lost_card", "Lost Card");
        declineReasons.put("stolen_card", "Stolen Card");
        declineReasons.put("expired_card", "Expired Card");
        declineReasons.put("incorrect_cvc", "Incorrect CVC");
        declineReasons.put("incorrect_zip", "Incorrect ZIP");
        declineReasons.put("card_declined", "Card Declined");
        declineReasons.put("invalid_account", "Invalid Account");
        declineReasons.put("card_not_activated", "Card Not Activated");
        declineReasons.put("processing_error", "Processing Error");
        declineReasons.put("system_error", "System Error");
        
        Map<String, String> response = new HashMap<>();
        response.put("errorCode", reason.toUpperCase());
        response.put("responseMessage", declineReasons.getOrDefault(reason, "Card Declined"));
        
        return response;
    }
    
    /**
     * Generate mock vault token
     */
    public static String generateMockVaultToken() {
        return "token_" + UUID.randomUUID().toString().replace("-", "");
    }
}