package com.globalpayments.example;

import com.global.api.ServicesContainer;
import com.global.api.entities.Address;
import com.global.api.entities.Transaction;
import com.global.api.entities.exceptions.ApiException;
import com.global.api.entities.exceptions.ConfigurationException;
import com.global.api.paymentMethods.CreditCardData;
import com.global.api.serviceConfigs.PorticoConfig;
import io.github.cdimascio.dotenv.Dotenv;

import java.math.BigDecimal;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;

/**
 * Payment utility functions for Global Payments SDK
 */
public class PaymentUtils {
    
    private static boolean sdkConfigured = false;
    private static final Dotenv dotenv = Dotenv.configure().ignoreIfMissing().load();
    
    /**
     * Configure the Global Payments SDK
     */
    public static void configureSdk() throws ConfigurationException {
        if (!sdkConfigured) {
            PorticoConfig config = new PorticoConfig();
            config.setSecretApiKey(dotenv.get("SECRET_API_KEY"));
            config.setDeveloperId("000000");
            config.setVersionNumber("0000");
            config.setServiceUrl("https://cert.api2.heartlandportico.com");
            
            ServicesContainer.configureService(config);
            sdkConfigured = true;
        }
    }
    
    /**
     * Sanitize postal code by removing invalid characters
     */
    public static String sanitizePostalCode(String postalCode) {
        if (postalCode == null) {
            return "";
        }
        
        String sanitized = postalCode.replaceAll("[^a-zA-Z0-9-]", "");
        return sanitized.length() > 10 ? sanitized.substring(0, 10) : sanitized;
    }
    
    /**
     * Determine card brand from card number
     */
    public static String determineCardBrand(String cardNumber) {
        cardNumber = cardNumber.replaceAll("\\s+", "");
        
        if (cardNumber.matches("^4.*")) {
            return "Visa";
        } else if (cardNumber.matches("^5[1-5].*") || cardNumber.matches("^2[2-7].*")) {
            return "Mastercard";
        } else if (cardNumber.matches("^3[47].*")) {
            return "American Express";
        } else if (cardNumber.matches("^6(?:011|5).*")) {
            return "Discover";
        } else {
            return "Unknown";
        }
    }
    
    /**
     * Create vault token using Global Payments SDK
     */
    public static String createVaultTokenWithSDK(Map<String, Object> data) throws Exception {
        try {
            CreditCardData card = new CreditCardData();
            card.setNumber((String) data.get("cardNumber"));
            card.setExpMonth(Integer.parseInt((String) data.get("expiryMonth")));
            card.setExpYear(Integer.parseInt((String) data.get("expiryYear")));
            card.setCvn((String) data.get("cvv"));
            
            @SuppressWarnings("unchecked")
            Map<String, String> billingAddress = (Map<String, String>) data.get("billingAddress");
            if (billingAddress != null) {
                Address address = new Address();
                address.setStreetAddress1(billingAddress.getOrDefault("street", ""));
                address.setCity(billingAddress.getOrDefault("city", ""));
                address.setState(billingAddress.getOrDefault("state", ""));
                address.setPostalCode(billingAddress.getOrDefault("zip", ""));
                address.setCountry(billingAddress.getOrDefault("country", "US"));
                card.setCardHolderName(billingAddress.getOrDefault("name", ""));
            }

            String token = card.tokenize();
            
            if (token != null && !token.isEmpty()) {
                // Log successful tokenization in live mode
                System.out.println("✅ LIVE MODE - Tokenization Success:");
                System.out.println("   Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                System.out.println("   Vault Token: " + token);
                System.out.println("   Card Brand: " + determineCardBrand((String) data.get("cardNumber")));
                System.out.println("   Last 4: " + ((String) data.get("cardNumber")).replaceAll("\\s+", "").substring(((String) data.get("cardNumber")).replaceAll("\\s+", "").length() - 4));
                System.out.println("   Expiry: " + data.get("expiryMonth") + "/" + data.get("expiryYear"));
                return token;
            } else {
                throw new Exception("Tokenization failed: No token returned");
            }
        } catch (Exception e) {
            System.err.println("SDK tokenization error: " + e.getMessage());
            throw e;
        }
    }
    
    /**
     * Process payment using Global Payments SDK
     */
    public static Map<String, Object> processPaymentWithSDK(String vaultToken, BigDecimal amount, String currency) throws Exception {
        try {
            CreditCardData card = new CreditCardData();
            card.setToken(vaultToken);

            Transaction response = card.charge(amount)
                    .withCurrency(currency)
                    .execute();

            if ("00".equals(response.getResponseCode())) {
                // Log successful payment in live mode
                System.out.println("💰 PAYMENT PROCESSING - Charging with token: " + vaultToken.substring(0, Math.min(8, vaultToken.length())) + "...");
                System.out.println("   💵 Amount: $" + amount + " " + currency);
                
                System.out.println("✅ 🟢 LIVE MODE - Payment Charged Successfully:");
                System.out.println("   ⏰ Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                System.out.println("   🆔 Transaction ID: " + (response.getTransactionId() != null ? response.getTransactionId() : "N/A"));
                System.out.println("   💵 Amount: $" + amount + " " + currency);
                System.out.println("   🔐 Vault Token: " + vaultToken.substring(0, Math.min(8, vaultToken.length())) + "...");
                System.out.println("   📋 Response Code: " + response.getResponseCode());
                System.out.println("   💬 Response Message: " + (response.getResponseMessage() != null ? response.getResponseMessage() : "Approved"));
                System.out.println("   🔑 Auth Code: " + (response.getAuthorizationCode() != null ? response.getAuthorizationCode() : "N/A"));
                System.out.println("   📄 Reference Number: " + (response.getReferenceNumber() != null ? response.getReferenceNumber() : "N/A"));
                System.out.println("   📡 API Status: Connected & Working");
                
                Map<String, Object> result = new HashMap<>();
                result.put("transactionId", response.getTransactionId() != null ? response.getTransactionId() : "txn_" + UUID.randomUUID().toString());
                result.put("amount", amount);
                result.put("currency", currency);
                result.put("status", "approved");
                result.put("responseCode", response.getResponseCode());
                result.put("responseMessage", response.getResponseMessage() != null ? response.getResponseMessage() : "Approved");
                result.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                
                Map<String, Object> gatewayResponse = new HashMap<>();
                gatewayResponse.put("authCode", response.getAuthorizationCode() != null ? response.getAuthorizationCode() : "");
                gatewayResponse.put("referenceNumber", response.getReferenceNumber() != null ? response.getReferenceNumber() : "");
                result.put("gatewayResponse", gatewayResponse);
                
                return result;
            } else {
                // Log failed payment
                System.out.println("❌ 🔴 LIVE MODE - Payment Charge Failed:");
                System.out.println("   ⏰ Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                System.out.println("   💵 Amount: $" + amount + " " + currency);
                System.out.println("   🔐 Vault Token: " + vaultToken.substring(0, Math.min(8, vaultToken.length())) + "...");
                System.out.println("   📋 Response Code: " + response.getResponseCode());
                System.out.println("   ❌ Error: " + (response.getResponseMessage() != null ? response.getResponseMessage() : "Unknown error"));
                System.out.println("   📡 API Status: Connected but Declined");
                
                throw new Exception("Payment failed: " + (response.getResponseMessage() != null ? response.getResponseMessage() : "Unknown error"));
            }
        } catch (Exception e) {
            System.err.println("SDK payment processing error: " + e.getMessage());
            throw e;
        }
    }
    
    /**
     * Create authorization using Global Payments SDK
     */
    public static Map<String, Object> createAuthorizationWithSDK(String vaultToken, BigDecimal amount, String currency) throws Exception {
        try {
            CreditCardData card = new CreditCardData();
            card.setToken(vaultToken);

            Transaction response = card.authorize(amount)
                    .withCurrency(currency)
                    .execute();

            if ("00".equals(response.getResponseCode())) {
                // Log successful authorization in live mode
                System.out.println("✅ LIVE MODE - Authorization Success:");
                System.out.println("   Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                System.out.println("   Transaction ID: " + (response.getTransactionId() != null ? response.getTransactionId() : "N/A"));
                System.out.println("   Amount: $" + amount + " " + currency);
                System.out.println("   Vault Token: " + vaultToken);
                System.out.println("   Response Code: " + response.getResponseCode());
                System.out.println("   Response Message: " + (response.getResponseMessage() != null ? response.getResponseMessage() : "Authorized"));
                System.out.println("   Auth Code: " + (response.getAuthorizationCode() != null ? response.getAuthorizationCode() : "N/A"));
                System.out.println("   Reference Number: " + (response.getReferenceNumber() != null ? response.getReferenceNumber() : "N/A"));
                System.out.println("   Expires: " + LocalDateTime.now().plusDays(7).format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                
                Map<String, Object> result = new HashMap<>();
                result.put("authorizationId", "auth_" + UUID.randomUUID().toString());
                result.put("transactionId", response.getTransactionId() != null ? response.getTransactionId() : "txn_" + UUID.randomUUID().toString());
                result.put("amount", amount);
                result.put("currency", currency);
                result.put("status", "authorized");
                result.put("responseCode", response.getResponseCode());
                result.put("responseMessage", response.getResponseMessage() != null ? response.getResponseMessage() : "Authorized");
                result.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                result.put("expiresAt", LocalDateTime.now().plusDays(7).format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
                
                Map<String, Object> gatewayResponse = new HashMap<>();
                gatewayResponse.put("authCode", response.getAuthorizationCode() != null ? response.getAuthorizationCode() : "");
                gatewayResponse.put("referenceNumber", response.getReferenceNumber() != null ? response.getReferenceNumber() : "");
                result.put("gatewayResponse", gatewayResponse);
                
                return result;
            } else {
                throw new Exception("Authorization failed: " + (response.getResponseMessage() != null ? response.getResponseMessage() : "Unknown error"));
            }
        } catch (Exception e) {
            System.err.println("SDK authorization error: " + e.getMessage());
            throw e;
        }
    }
    
    /**
     * Helper function to return string or "None" if empty
     */
    private static String stringOrNone(String s) {
        return (s == null || s.trim().isEmpty()) ? "None" : s;
    }
}