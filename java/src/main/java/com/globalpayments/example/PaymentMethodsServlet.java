package com.globalpayments.example;

import com.google.gson.Gson;
import io.github.cdimascio.dotenv.Dotenv;
import jakarta.servlet.ServletException;
import jakarta.servlet.annotation.WebServlet;
import jakarta.servlet.http.HttpServlet;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;

import java.io.IOException;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * Payment Methods Endpoint
 * 
 * GET /payment-methods - Retrieve saved payment methods
 * POST /payment-methods - Create new payment method (vault token) OR edit existing payment method
 *                         - Create: Requires cardNumber, expiryMonth, expiryYear, cvv (+ optional nickname, isDefault)
 *                         - Edit: Requires id (+ optional nickname, isDefault) - only nickname and default status can be edited
 */
@WebServlet(name = "PaymentMethodsServlet", urlPatterns = {"/payment-methods"})
public class PaymentMethodsServlet extends HttpServlet {
    
    private static final Gson gson = new Gson();
    private final Dotenv dotenv = Dotenv.configure().ignoreIfMissing().load();
    
    @Override
    public void init() throws ServletException {
        try {
            PaymentUtils.configureSdk();
        } catch (Exception e) {
            throw new ServletException("Failed to configure Global Payments SDK", e);
        }
    }
    
    @Override
    protected void doGet(HttpServletRequest request, HttpServletResponse response)
            throws ServletException, IOException {
        
        handleCORS(response);
        
        try {
            List<Map<String, Object>> paymentMethods = JsonStorage.getFormattedPaymentMethods();
            
            Map<String, Object> responseData = new HashMap<>();
            responseData.put("success", true);
            responseData.put("data", paymentMethods);
            responseData.put("message", "Payment methods retrieved successfully");
            responseData.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
            
            response.getWriter().write(gson.toJson(responseData));
            
        } catch (Exception e) {
            sendErrorResponse(response, 500, "Failed to retrieve payment methods", "SERVER_ERROR");
        }
    }
    
    @Override
    protected void doPost(HttpServletRequest request, HttpServletResponse response)
            throws ServletException, IOException {
        
        handleCORS(response);
        
        try {
            // Parse JSON input
            String jsonString = request.getReader().lines().collect(Collectors.joining());
            @SuppressWarnings("unchecked")
            Map<String, Object> data = gson.fromJson(jsonString, Map.class);
            
            // Check if this is an edit operation
            if (data != null && data.get("id") != null) {
                handleEditPaymentMethod(response, data);
                return;
            }
            
            // Validate required fields for new payment method
            if (data == null || 
                isEmpty((String) data.get("cardNumber")) ||
                isEmpty((String) data.get("expiryMonth")) ||
                isEmpty((String) data.get("expiryYear")) ||
                isEmpty((String) data.get("cvv"))) {
                sendErrorResponse(response, 400, "Missing required fields", "VALIDATION_ERROR");
                return;
            }
            
            String cardNumber = (String) data.get("cardNumber");
            String expiryMonth = (String) data.get("expiryMonth");
            String expiryYear = (String) data.get("expiryYear");
            String cvv = (String) data.get("cvv");
            String nickname = (String) data.get("nickname");
            Boolean isDefault = (Boolean) data.get("isDefault");
            
            // Validate card number format
            String cleanCardNumber = cardNumber.replaceAll("\\s+", "");
            if (cleanCardNumber.length() < 13 || cleanCardNumber.length() > 19 || !cleanCardNumber.matches("\\d+")) {
                sendErrorResponse(response, 400, "Invalid card number format", "VALIDATION_ERROR");
                return;
            }
            
            // Validate expiry
            try {
                int month = Integer.parseInt(expiryMonth);
                int year = Integer.parseInt(expiryYear);
                if (month < 1 || month > 12) {
                    sendErrorResponse(response, 400, "Invalid expiry month", "VALIDATION_ERROR");
                    return;
                }
                if (year < LocalDateTime.now().getYear()) {
                    sendErrorResponse(response, 400, "Card has expired", "VALIDATION_ERROR");
                    return;
                }
            } catch (NumberFormatException e) {
                sendErrorResponse(response, 400, "Invalid expiry format", "VALIDATION_ERROR");
                return;
            }
            
            // Determine card brand and last 4 digits
            String cardBrand = PaymentUtils.determineCardBrand(cleanCardNumber);
            String last4 = cleanCardNumber.substring(cleanCardNumber.length() - 4);
            
            String vaultToken = null;
            boolean mockMode = false;
            
            // Check if mock mode is enabled globally
            if (MockModeServlet.isMockModeEnabled()) {
                mockMode = true;
                vaultToken = MockResponses.generateMockVaultToken();
                System.out.println("🟡 MOCK MODE - Using mock tokenization for card ending in " + last4);
            } else {
                // Try to create vault token with SDK in live mode only
                String secretApiKey = dotenv.get("SECRET_API_KEY");
                if (secretApiKey != null && !secretApiKey.trim().isEmpty()) {
                    try {
                        vaultToken = PaymentUtils.createVaultTokenWithSDK(data);
                    } catch (Exception e) {
                        System.err.println("❌ LIVE MODE - SDK tokenization failed: " + e.getMessage());
                        sendErrorResponse(response, 422, "Payment method creation failed: " + e.getMessage(), "PAYMENT_ERROR");
                        return;
                    }
                } else {
                    System.err.println("❌ LIVE MODE - No SECRET_API_KEY configured");
                    sendErrorResponse(response, 503, "Payment service not configured", "CONFIGURATION_ERROR");
                    return;
                }
            }
            
            // Create payment method data
            Map<String, Object> paymentMethodData = new HashMap<>();
            paymentMethodData.put("vaultToken", vaultToken);
            paymentMethodData.put("cardBrand", cardBrand);
            paymentMethodData.put("last4", last4);
            paymentMethodData.put("expiry", String.format("%02d/%s", Integer.parseInt(expiryMonth), expiryYear));
            paymentMethodData.put("nickname", nickname);
            paymentMethodData.put("isDefault", isDefault != null ? isDefault : false);
            paymentMethodData.put("mockMode", mockMode);
            
            // Save to storage
            Map<String, Object> savedMethod = JsonStorage.addPaymentMethod(paymentMethodData);
            
            // Format response
            Map<String, Object> formattedMethod = new HashMap<>();
            formattedMethod.put("id", savedMethod.get("id"));
            formattedMethod.put("brand", savedMethod.get("cardBrand"));
            formattedMethod.put("last4", savedMethod.get("last4"));
            formattedMethod.put("expiry", savedMethod.get("expiry"));
            formattedMethod.put("nickname", savedMethod.get("nickname"));
            formattedMethod.put("isDefault", savedMethod.get("isDefault"));
            formattedMethod.put("mockMode", mockMode);
            
            Map<String, Object> responseData = new HashMap<>();
            responseData.put("success", true);
            responseData.put("data", formattedMethod);
            responseData.put("message", "Payment method added successfully");
            responseData.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
            
            response.getWriter().write(gson.toJson(responseData));
            
        } catch (Exception e) {
            e.printStackTrace();
            sendErrorResponse(response, 500, "Payment method creation failed", "SERVER_ERROR");
        }
    }
    
    @Override
    protected void doOptions(HttpServletRequest request, HttpServletResponse response)
            throws ServletException, IOException {
        handleCORS(response);
        response.setStatus(HttpServletResponse.SC_OK);
    }
    
    private void handleCORS(HttpServletResponse response) {
        response.setContentType("application/json");
        response.setCharacterEncoding("UTF-8");
        response.setHeader("Access-Control-Allow-Origin", "*");
        response.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
    }
    
    private void sendErrorResponse(HttpServletResponse response, int statusCode, String message, String errorCode) 
            throws IOException {
        response.setStatus(statusCode);
        
        Map<String, Object> errorResponse = new HashMap<>();
        errorResponse.put("success", false);
        errorResponse.put("message", message);
        errorResponse.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
        
        if (errorCode != null) {
            errorResponse.put("error_code", errorCode);
        }
        
        response.getWriter().write(gson.toJson(errorResponse));
    }
    
    private boolean isEmpty(String str) {
        return str == null || str.trim().isEmpty();
    }
    
    private void handleEditPaymentMethod(HttpServletResponse response, Map<String, Object> data) 
            throws IOException {
        try {
            String id = (String) data.get("id");
            
            // Find existing payment method
            Map<String, Object> existingMethod = JsonStorage.findPaymentMethod(id);
            if (existingMethod == null) {
                sendErrorResponse(response, 404, "Payment method not found", "NOT_FOUND");
                return;
            }

            // Log the edit attempt
            System.out.println("✏️ PAYMENT METHOD EDIT - Editing payment method " + id);
            System.out.println("   💳 Card: " + existingMethod.get("cardBrand") + " ending in " + existingMethod.get("last4"));
            System.out.println("   📛 Nickname: " + stringOrNone((String) existingMethod.get("nickname")) + " → " + stringOrNone((String) data.get("nickname")));
            System.out.println("   ⭐ Default: " + existingMethod.get("isDefault") + " → " + data.get("isDefault"));
            System.out.println("   ⏰ Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));

            // Update the payment method
            Map<String, Object> updateData = new HashMap<>();
            updateData.put("nickname", data.get("nickname"));
            updateData.put("isDefault", data.get("isDefault"));
            
            JsonStorage.updatePaymentMethod(id, updateData);

            // If setting as default, update all others
            Boolean isDefault = (Boolean) data.get("isDefault");
            if (Boolean.TRUE.equals(isDefault)) {
                JsonStorage.setDefaultPaymentMethod(id);
            }

            // Get the updated method
            Map<String, Object> updatedMethod = JsonStorage.findPaymentMethod(id);
            if (updatedMethod == null) {
                sendErrorResponse(response, 500, "Server error", "SERVER_ERROR");
                return;
            }

            // Log successful edit
            System.out.println("✅ 📝 PAYMENT METHOD UPDATED Successfully:");
            System.out.println("   ⏰ Timestamp: " + LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
            System.out.println("   🆔 Payment Method ID: " + updatedMethod.get("id"));
            System.out.println("   💳 Card Brand: " + updatedMethod.get("cardBrand"));
            System.out.println("   🔢 Last 4: " + updatedMethod.get("last4"));
            System.out.println("   📛 Nickname: " + stringOrNone((String) updatedMethod.get("nickname")));
            System.out.println("   ⭐ Default: " + updatedMethod.get("isDefault"));
            System.out.println("   🔄 Updated: " + updatedMethod.get("updatedAt"));

            // Format response
            Map<String, Object> formattedMethod = new HashMap<>();
            formattedMethod.put("id", updatedMethod.get("id"));
            formattedMethod.put("brand", updatedMethod.get("cardBrand"));
            formattedMethod.put("last4", updatedMethod.get("last4"));
            formattedMethod.put("expiry", updatedMethod.get("expiry"));
            formattedMethod.put("nickname", updatedMethod.get("nickname"));
            formattedMethod.put("isDefault", updatedMethod.get("isDefault"));
            formattedMethod.put("mockMode", false); // Edit operations don't involve mock mode

            Map<String, Object> responseData = new HashMap<>();
            responseData.put("success", true);
            responseData.put("data", formattedMethod);
            responseData.put("message", "Payment method updated successfully");
            responseData.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));

            response.getWriter().write(gson.toJson(responseData));

        } catch (Exception e) {
            System.err.println("Error updating payment method: " + e.getMessage());
            sendErrorResponse(response, 500, "Payment method update failed", "SERVER_ERROR");
        }
    }
    
    private String stringOrNone(String s) {
        return s == null || s.trim().isEmpty() ? "None" : s;
    }
}