package com.globalpayments.example;

import com.google.gson.Gson;
import io.github.cdimascio.dotenv.Dotenv;
import jakarta.servlet.ServletException;
import jakarta.servlet.annotation.WebServlet;
import jakarta.servlet.http.HttpServlet;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;

import java.io.IOException;
import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.HashMap;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * Schedule Payment Endpoint
 * 
 * POST /schedule-payment - Schedule payment authorization ($50.00)
 */
@WebServlet(name = "SchedulePaymentServlet", urlPatterns = {"/schedule-payment"})
public class SchedulePaymentServlet extends HttpServlet {
    
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
    protected void doPost(HttpServletRequest request, HttpServletResponse response)
            throws ServletException, IOException {
        
        handleCORS(response);
        
        try {
            // Parse JSON input
            String jsonString = request.getReader().lines().collect(Collectors.joining());
            @SuppressWarnings("unchecked")
            Map<String, Object> data = gson.fromJson(jsonString, Map.class);
            
            if (data == null || isEmpty((String) data.get("paymentMethodId"))) {
                sendErrorResponse(response, 400, "Payment method ID is required", "VALIDATION_ERROR");
                return;
            }
            
            String paymentMethodId = (String) data.get("paymentMethodId");
            Map<String, Object> paymentMethod = JsonStorage.findPaymentMethod(paymentMethodId);
            if (paymentMethod == null) {
                sendErrorResponse(response, 404, "Payment method not found", "NOT_FOUND");
                return;
            }
            
            BigDecimal amount = new BigDecimal("50.00");
            String currency = "USD";
            
            Map<String, Object> authorizationResult = null;
            boolean mockMode = false;
            
            // Check if mock mode is enabled globally
            if (MockModeServlet.isMockModeEnabled()) {
                mockMode = true;
                String last4 = (String) paymentMethod.get("last4");
                String responseType = MockResponses.getResponseByCardNumber(last4);
                
                System.out.println("🟡 MOCK MODE - Creating authorization with card ending in " + last4);
                
                if ("success".equals(responseType)) {
                    authorizationResult = MockResponses.getAuthorizationResponse(amount, paymentMethodId);
                } else {
                    String declineReason = responseType.replaceAll("(decline_|error_)", "");
                    Map<String, String> declineResponse = MockResponses.getDeclineResponse(declineReason);
                    sendErrorResponse(response, 422, declineResponse.get("responseMessage"), declineResponse.get("errorCode"));
                    return;
                }
            } else {
                // Live mode - no fallback to mock
                String secretApiKey = dotenv.get("SECRET_API_KEY");
                if (secretApiKey != null && !secretApiKey.trim().isEmpty()) {
                    try {
                        String vaultToken = (String) paymentMethod.get("vaultToken");
                        authorizationResult = PaymentUtils.createAuthorizationWithSDK(vaultToken, amount, currency);
                    } catch (Exception e) {
                        System.err.println("❌ LIVE MODE - Authorization failed: " + e.getMessage());
                        sendErrorResponse(response, 422, "Authorization failed: " + e.getMessage(), "PAYMENT_ERROR");
                        return;
                    }
                } else {
                    System.err.println("❌ LIVE MODE - No SECRET_API_KEY configured");
                    sendErrorResponse(response, 503, "Payment service not configured", "CONFIGURATION_ERROR");
                    return;
                }
            }
            
            // Build response
            Map<String, Object> responseData = new HashMap<>(authorizationResult);
            
            Map<String, Object> paymentMethodInfo = new HashMap<>();
            paymentMethodInfo.put("id", paymentMethod.get("id"));
            paymentMethodInfo.put("type", "card");
            paymentMethodInfo.put("brand", paymentMethod.get("cardBrand"));
            paymentMethodInfo.put("last4", paymentMethod.get("last4"));
            paymentMethodInfo.put("nickname", paymentMethod.get("nickname") != null ? paymentMethod.get("nickname") : "");
            
            responseData.put("paymentMethod", paymentMethodInfo);
            responseData.put("mockMode", mockMode);
            
            // Add capture information
            Map<String, Object> captureInfo = new HashMap<>();
            captureInfo.put("canCapture", true);
            captureInfo.put("expiresAt", authorizationResult.get("expiresAt"));
            responseData.put("captureInfo", captureInfo);
            
            Map<String, Object> finalResponse = new HashMap<>();
            finalResponse.put("success", true);
            finalResponse.put("data", responseData);
            finalResponse.put("message", "Payment authorization created successfully");
            finalResponse.put("timestamp", LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME));
            
            response.getWriter().write(gson.toJson(finalResponse));
            
        } catch (Exception e) {
            e.printStackTrace();
            sendErrorResponse(response, 500, "Payment authorization failed", "SERVER_ERROR");
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
}