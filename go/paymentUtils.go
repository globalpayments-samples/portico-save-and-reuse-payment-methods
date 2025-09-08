package main

import (
	"context"
	"fmt"
	"log"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/globalpayments/go-sdk/api"
	"github.com/globalpayments/go-sdk/api/entities/transactions"
	"github.com/globalpayments/go-sdk/api/paymentmethods"
	"github.com/globalpayments/go-sdk/api/serviceconfigs"
	"github.com/globalpayments/go-sdk/api/utils/stringutils"
	"github.com/google/uuid"
)

// Initialize Global Payments SDK
func initializeSDK() error {
	secretKey := os.Getenv("SECRET_API_KEY")
	if secretKey == "" {
		return fmt.Errorf("SECRET_API_KEY not configured")
	}

	config := serviceconfigs.NewPorticoConfig()
	config.SecretApiKey = secretKey
	config.DeveloperId = "000000"
	config.VersionNumber = "0000"
	config.ServiceUrl = "https://cert.api2.heartlandportico.com"

	return api.ConfigureService(config, "default")
}

// Create a new payment method (vault token)
func createPaymentMethod(req PaymentMethodRequest) (*PaymentMethod, error) {
	log.Printf("Creating payment method. Mock mode: %v", mockModeEnabled)

	now := time.Now()
	paymentMethod := &PaymentMethod{
		ID:        "pm_" + uuid.New().String(),
		Type:      "card",
		CreatedAt: now,
		UpdatedAt: now,
		MockMode:  mockModeEnabled,
	}

	// Set nickname
	if req.Nickname != nil && *req.Nickname != "" {
		paymentMethod.Nickname = req.Nickname
	}

	// Determine card brand and get last 4 digits
	cardNumber := strings.ReplaceAll(req.CardNumber, " ", "")
	paymentMethod.Brand = getCardBrand(cardNumber)
	paymentMethod.Last4 = cardNumber[len(cardNumber)-4:]
	paymentMethod.Expiry = fmt.Sprintf("%s/%s", req.ExpiryMonth, req.ExpiryYear)

	if mockModeEnabled {
		// Generate mock vault token
		paymentMethod.VaultToken = "mock_token_" + uuid.New().String()
		log.Printf("Generated mock vault token for card ending in %s", paymentMethod.Last4)
	} else {
		// Create real vault token using Global Payments SDK
		vaultToken, err := createVaultToken(cardNumber, req.ExpiryMonth, req.ExpiryYear, req.CVV)
		if err != nil {
			log.Printf("Error creating vault token: %v", err)
			return nil, fmt.Errorf("failed to create vault token: %v", err)
		}
		paymentMethod.VaultToken = vaultToken
		log.Printf("Created live vault token for card ending in %s", paymentMethod.Last4)
	}

	// Handle default payment method
	if req.IsDefault {
		err := clearDefaultPaymentMethods()
		if err != nil {
			log.Printf("Warning: Failed to clear existing default payment methods: %v", err)
		}
		paymentMethod.IsDefault = true
	}

	// Save to storage
	err := savePaymentMethod(paymentMethod)
	if err != nil {
		return nil, fmt.Errorf("failed to save payment method: %v", err)
	}

	log.Printf("Payment method created successfully with ID: %s", paymentMethod.ID)
	return paymentMethod, nil
}

// Edit an existing payment method
func editPaymentMethod(req PaymentMethodRequest) (*PaymentMethod, error) {
	log.Printf("Editing payment method %s. Mock mode: %v", req.ID, mockModeEnabled)

	methods, err := loadPaymentMethods()
	if err != nil {
		return nil, fmt.Errorf("failed to load payment methods: %v", err)
	}

	// Find the payment method to edit
	var targetMethod *PaymentMethod
	for i := range methods {
		if methods[i].ID == req.ID {
			targetMethod = &methods[i]
			break
		}
	}

	if targetMethod == nil {
		return nil, fmt.Errorf("payment method not found: %s", req.ID)
	}

	// Update editable fields
	if req.Nickname != nil {
		targetMethod.Nickname = req.Nickname
	}

	// Handle default payment method change
	if req.IsDefault && !targetMethod.IsDefault {
		err := clearDefaultPaymentMethods()
		if err != nil {
			log.Printf("Warning: Failed to clear existing default payment methods: %v", err)
		}
		targetMethod.IsDefault = true
	} else if !req.IsDefault && targetMethod.IsDefault {
		targetMethod.IsDefault = false
	}

	targetMethod.UpdatedAt = time.Now()

	// Save updated payment method
	err = updatePaymentMethodInStorage(targetMethod)
	if err != nil {
		return nil, fmt.Errorf("failed to update payment method: %v", err)
	}

	log.Printf("Payment method %s updated successfully", req.ID)
	return targetMethod, nil
}

// Process a payment (charge or authorize)
func processPayment(paymentMethodID string, amount float64, transactionType string) (*TransactionResult, error) {
	log.Printf("Processing %s for amount %.2f with payment method %s. Mock mode: %v", 
		transactionType, amount, paymentMethodID, mockModeEnabled)

	// Load payment method
	methods, err := loadPaymentMethods()
	if err != nil {
		return nil, fmt.Errorf("failed to load payment methods: %v", err)
	}

	var paymentMethod *PaymentMethod
	for i := range methods {
		if methods[i].ID == paymentMethodID {
			paymentMethod = &methods[i]
			break
		}
	}

	if paymentMethod == nil {
		return nil, fmt.Errorf("payment method not found: %s", paymentMethodID)
	}

	result := &TransactionResult{
		Amount:   amount,
		Currency: "USD",
		PaymentMethod: PaymentMethodInfo{
			ID:       paymentMethod.ID,
			Type:     paymentMethod.Type,
			Brand:    paymentMethod.Brand,
			Last4:    paymentMethod.Last4,
			Nickname: paymentMethod.Nickname,
		},
		MockMode: mockModeEnabled,
	}

	if mockModeEnabled {
		// Use mock responses
		mockResult := getMockTransactionResult(paymentMethod.Last4, amount, transactionType)
		if !mockResult.Success {
			return nil, fmt.Errorf("payment declined: %s", mockResult.ResponseMessage)
		}

		if transactionType == "charge" {
			txnID := mockResult.TransactionID
			result.TransactionID = &txnID
			result.Status = "approved"
		} else {
			authID := mockResult.AuthorizationID
			result.AuthorizationID = &authID
			result.Status = "authorized"
			
			// Add capture info for authorizations
			expiresAt := time.Now().Add(7 * 24 * time.Hour).Format(time.RFC3339)
			result.CaptureInfo = &CaptureInfo{
				CanCapture: true,
				ExpiresAt:  expiresAt,
			}
		}

		result.GatewayResponse = &GatewayResponseInfo{
			ResponseCode:    mockResult.ResponseCode,
			ResponseMessage: mockResult.ResponseMessage,
			AuthCode:        mockResult.AuthCode,
			ReferenceNumber: mockResult.ReferenceNumber,
		}

		log.Printf("Mock %s completed successfully", transactionType)
		return result, nil
	}

	// Use real SDK for live processing
	err = initializeSDK()
	if err != nil {
		return nil, fmt.Errorf("failed to initialize SDK: %v", err)
	}

	ctx := context.Background()
	
	// Create card from vault token
	card := paymentmethods.NewCreditCardDataWithToken(paymentMethod.VaultToken)
	
	// Configure transaction
	amountStr := fmt.Sprintf("%.2f", amount)
	val, _ := stringutils.ToDecimalAmount(amountStr)

	var response *transactions.Transaction
	if transactionType == "charge" {
		transaction := card.ChargeWithAmount(val)
		transaction.WithAllowDuplicates(true)
		transaction.WithCurrency("USD")
		response, err = api.ExecuteGateway[transactions.Transaction](ctx, transaction)
	} else {
		transaction := card.AuthorizeWithAmount(val, true)
		transaction.WithAllowDuplicates(true) 
		transaction.WithCurrency("USD")
		response, err = api.ExecuteGateway[transactions.Transaction](ctx, transaction)
	}
	if err != nil {
		log.Printf("SDK transaction error: %v", err)
		return nil, fmt.Errorf("transaction failed: %v", err)
	}

	// Check response code
	if response.GetResponseCode() != "00" {
		log.Printf("Transaction declined. Response code: %s, Message: %s", 
			response.GetResponseCode(), response.GetResponseMessage())
		return nil, fmt.Errorf("payment declined: %s", response.GetResponseMessage())
	}

	// Build result
	if transactionType == "charge" {
		txnID := response.GetTransactionId()
		result.TransactionID = &txnID
		result.Status = "approved"
	} else {
		authID := response.GetTransactionId()
		result.AuthorizationID = &authID
		result.Status = "authorized"
		
		// Add capture info for authorizations
		expiresAt := time.Now().Add(7 * 24 * time.Hour).Format(time.RFC3339)
		result.CaptureInfo = &CaptureInfo{
			CanCapture: true,
			ExpiresAt:  expiresAt,
		}
	}

	result.GatewayResponse = &GatewayResponseInfo{
		ResponseCode:    response.GetResponseCode(),
		ResponseMessage: response.GetResponseMessage(),
		AuthCode:        "", // Will need to check SDK documentation for correct method
		ReferenceNumber: response.GetReferenceNumber(),
	}

	log.Printf("Live %s completed successfully", transactionType)
	return result, nil
}

// Create vault token using SDK
func createVaultToken(cardNumber, expiryMonth, expiryYear, cvv string) (string, error) {
	err := initializeSDK()
	if err != nil {
		return "", err
	}

	// Create credit card data
	card := paymentmethods.NewCreditCardData()
	card.SetNumber(cardNumber)
	
	// Convert month and year to integers
	month, err := strconv.Atoi(expiryMonth)
	if err != nil {
		return "", fmt.Errorf("invalid expiry month: %v", err)
	}
	year, err := strconv.Atoi(expiryYear)
	if err != nil {
		return "", fmt.Errorf("invalid expiry year: %v", err)
	}
	
	card.SetExpMonth(&month)
	card.SetExpYear(&year)
	card.SetCvn(cvv)

	ctx := context.Background()
	
	// Create transaction for tokenization
	transaction, err := card.Tokenize()
	if err != nil {
		return "", fmt.Errorf("tokenization setup failed: %v", err)
	}
	response, err := api.ExecuteGateway[transactions.Transaction](ctx, transaction)
	if err != nil {
		return "", fmt.Errorf("tokenization failed: %v", err)
	}

	// Check response code first
	if response.GetResponseCode() != "00" {
		return "", fmt.Errorf("tokenization declined: %s - %s", 
			response.GetResponseCode(), response.GetResponseMessage())
	}

	if response.GetToken() == "" {
		return "", fmt.Errorf("no token returned from gateway")
	}

	return response.GetToken(), nil
}

// Determine card brand from card number
func getCardBrand(cardNumber string) string {
	if len(cardNumber) < 4 {
		return "unknown"
	}

	firstDigits := cardNumber[:4]
	first2 := cardNumber[:2]
	firstDigit := cardNumber[:1]

	// American Express
	if firstDigits[:2] == "34" || firstDigits[:2] == "37" {
		return "amex"
	}

	// Visa
	if firstDigit == "4" {
		return "visa"
	}

	// Mastercard
	if first2 >= "51" && first2 <= "55" {
		return "mastercard"
	}
	if firstDigits >= "2221" && firstDigits <= "2720" {
		return "mastercard"
	}

	// Discover
	if firstDigits == "6011" || firstDigits[:2] == "65" {
		return "discover"
	}
	if firstDigits >= "644" && firstDigits <= "649" {
		return "discover"
	}

	// JCB
	if firstDigits >= "3528" && firstDigits <= "3589" {
		return "jcb"
	}

	return "unknown"
}

// Get current timestamp in ISO format
func getCurrentTimestamp() string {
	return time.Now().UTC().Format(time.RFC3339)
}