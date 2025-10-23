package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
)

// Health check endpoint
func healthHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == "OPTIONS" {
		return
	}

	response := APIResponse{
		Success: true,
		Message: "System is healthy",
		Data: map[string]interface{}{
			"status":        "healthy",
			"timestamp":     getCurrentTimestamp(),
			"storage_status": "operational",
			"mock_mode":     mockModeEnabled,
		},
		Timestamp: getCurrentTimestamp(),
	}

	sendJSONResponse(w, http.StatusOK, response)
}

// Configuration endpoint
func configHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == "OPTIONS" {
		return
	}

	publicKey := os.Getenv("PUBLIC_API_KEY")
	if publicKey == "" {
		publicKey = "pk_test_demo_key"
	}

	response := APIResponse{
		Success: true,
		Data: map[string]interface{}{
			"publicApiKey": publicKey,
			"mock_mode":    mockModeEnabled,
		},
		Timestamp: getCurrentTimestamp(),
	}

	sendJSONResponse(w, http.StatusOK, response)
}

// Mock mode toggle endpoint
func mockModeHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == "OPTIONS" {
		return
	}

	switch r.Method {
	case "GET":
		response := APIResponse{
			Success: true,
			Data: map[string]interface{}{
				"isEnabled": mockModeEnabled,
			},
			Message:   fmt.Sprintf("Mock mode is %s", map[bool]string{true: "enabled", false: "disabled"}[mockModeEnabled]),
			Timestamp: getCurrentTimestamp(),
		}
		sendJSONResponse(w, http.StatusOK, response)

	case "POST":
		var toggleReq struct {
			IsEnabled bool `json:"isEnabled"`
		}

		if err := json.NewDecoder(r.Body).Decode(&toggleReq); err != nil {
			sendErrorResponse(w, http.StatusBadRequest, "Invalid request body", "INVALID_JSON")
			return
		}

		mockModeEnabled = toggleReq.IsEnabled

		// Persist mock mode configuration
		err := saveMockModeConfig(mockModeEnabled)
		if err != nil {
			log.Printf("Warning: Failed to save mock mode config: %v", err)
		}

		log.Printf("Mock mode is now %v", mockModeEnabled)

		response := APIResponse{
			Success: true,
			Data: map[string]interface{}{
				"isEnabled": mockModeEnabled,
			},
			Message:   fmt.Sprintf("Mock mode %s", map[bool]string{true: "enabled", false: "disabled"}[mockModeEnabled]),
			Timestamp: getCurrentTimestamp(),
		}

		sendJSONResponse(w, http.StatusOK, response)
	}
}

// Payment methods endpoint
func paymentMethodsHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == "OPTIONS" {
		return
	}

	switch r.Method {
	case "GET":
		methods, err := loadPaymentMethods()
		if err != nil {
			sendErrorResponse(w, http.StatusInternalServerError, "Failed to load payment methods", "STORAGE_ERROR")
			return
		}

		// Format methods to match PHP structure
		formattedMethods := make([]map[string]interface{}, len(methods))
		for i, method := range methods {
			formattedMethods[i] = map[string]interface{}{
				"id":        method.ID,
				"type":      method.Type,
				"last4":     method.Last4,
				"brand":     method.Brand,
				"expiry":    method.Expiry,
				"isDefault": method.IsDefault,
				"nickname":  "",
			}
			if method.Nickname != nil {
				formattedMethods[i]["nickname"] = *method.Nickname
			}
		}

		response := APIResponse{
			Success:   true,
			Data:      formattedMethods,
			Message:   fmt.Sprintf("Retrieved %d payment method(s)", len(methods)),
			Timestamp: getCurrentTimestamp(),
		}

		sendJSONResponse(w, http.StatusOK, response)

	case "POST":
		var req PaymentMethodRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			sendErrorResponse(w, http.StatusBadRequest, "Invalid request body", "INVALID_JSON")
			return
		}

		// Check if this is an edit request (has ID)
		if req.ID != "" {
			method, err := editPaymentMethod(req)
			if err != nil {
				sendErrorResponse(w, http.StatusBadRequest, err.Error(), "EDIT_FAILED")
				return
			}

			response := APIResponse{
				Success:   true,
				Data:      method,
				Message:   "Payment method updated successfully",
				Timestamp: getCurrentTimestamp(),
			}

			sendJSONResponse(w, http.StatusOK, response)
			return
		}

		// Validate required fields for new payment method
		if req.PaymentToken != "" {
			// New approach: validate payment token and card details
			if req.CardDetails.CardType == "" || req.CardDetails.CardLast4 == "" ||
			   req.CardDetails.ExpiryMonth == "" || req.CardDetails.ExpiryYear == "" {
				sendErrorResponse(w, http.StatusBadRequest, "Missing required cardDetails fields: cardType, cardLast4, expiryMonth, expiryYear", "VALIDATION_ERROR")
				return
			}
		} else if req.CardNumber != "" {
			// Legacy approach: validate card data
			if req.ExpiryMonth == "" || req.ExpiryYear == "" || req.CVV == "" {
				sendErrorResponse(w, http.StatusBadRequest, "Missing required fields: cardNumber, expiryMonth, expiryYear, cvv", "VALIDATION_ERROR")
				return
			}
		} else {
			sendErrorResponse(w, http.StatusBadRequest, "Missing required payment data: either paymentToken+cardDetails or cardNumber+expiryMonth+expiryYear+cvv", "VALIDATION_ERROR")
			return
		}

		// Create new payment method
		method, err := createPaymentMethod(req)
		if err != nil {
			sendErrorResponse(w, http.StatusBadRequest, err.Error(), "CREATION_FAILED")
			return
		}

		// Format response to match PHP structure
		responseData := map[string]interface{}{
			"id":        method.ID,
			"storedPaymentToken": method.StoredPaymentToken,
			"type":      method.Type,
			"last4":     method.Last4,
			"brand":     method.Brand,
			"expiry":    method.Expiry,
			"nickname":  "",
			"isDefault": method.IsDefault,
			"mockMode":  method.MockMode,
		}

		if method.Nickname != nil {
			responseData["nickname"] = *method.Nickname
		}

		response := APIResponse{
			Success:   true,
			Data:      responseData,
			Message:   "Payment method created successfully",
			Timestamp: getCurrentTimestamp(),
		}

		sendJSONResponse(w, http.StatusCreated, response)
	}
}

// Charge endpoint
func chargeHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == "OPTIONS" {
		return
	}

	var req PaymentRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendErrorResponse(w, http.StatusBadRequest, "Invalid request body", "INVALID_JSON")
		return
	}

	if req.PaymentMethodID == "" {
		sendErrorResponse(w, http.StatusBadRequest, "Missing required field: paymentMethodId", "VALIDATION_ERROR")
		return
	}

	// Process charge
	result, err := processPayment(req.PaymentMethodID, 25.00, "charge")
	if err != nil {
		sendErrorResponse(w, http.StatusBadRequest, err.Error(), "PAYMENT_FAILED")
		return
	}

	response := APIResponse{
		Success:   true,
		Data:      result,
		Message:   "Payment charged successfully",
		Timestamp: getCurrentTimestamp(),
	}

	sendJSONResponse(w, http.StatusOK, response)
}

