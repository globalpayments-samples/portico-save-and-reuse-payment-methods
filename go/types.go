package main

import (
	"time"
)

// APIResponse represents a standardized API response
type APIResponse struct {
	Success   bool        `json:"success"`
	Data      interface{} `json:"data,omitempty"`
	Message   string      `json:"message,omitempty"`
	Timestamp string      `json:"timestamp"`
	ErrorCode *string     `json:"errorCode,omitempty"`
}

// PaymentMethod represents a stored payment method
type PaymentMethod struct {
	ID          string    `json:"id"`
	VaultToken  string    `json:"vaultToken"`
	Type        string    `json:"type"`
	Last4       string    `json:"last4"`
	Brand       string    `json:"brand"`
	Expiry      string    `json:"expiry"`
	Nickname    *string   `json:"nickname,omitempty"`
	IsDefault   bool      `json:"isDefault"`
	MockMode    bool      `json:"mockMode"`
	CreatedAt   time.Time `json:"createdAt"`
	UpdatedAt   time.Time `json:"updatedAt"`
}

// PaymentMethodRequest represents the request to create/edit a payment method
type PaymentMethodRequest struct {
	ID          string  `json:"id,omitempty"`
	CardNumber  string  `json:"cardNumber,omitempty"`
	ExpiryMonth string  `json:"expiryMonth,omitempty"`
	ExpiryYear  string  `json:"expiryYear,omitempty"`
	CVV         string  `json:"cvv,omitempty"`
	Nickname    *string `json:"nickname,omitempty"`
	IsDefault   bool    `json:"isDefault"`
}

// PaymentRequest represents a payment processing request
type PaymentRequest struct {
	PaymentMethodID string `json:"paymentMethodId"`
}

// TransactionResult represents a payment transaction result
type TransactionResult struct {
	TransactionID   *string                `json:"transactionId,omitempty"`
	AuthorizationID *string                `json:"authorizationId,omitempty"`
	Amount          float64                `json:"amount"`
	Currency        string                 `json:"currency"`
	Status          string                 `json:"status"`
	PaymentMethod   PaymentMethodInfo      `json:"paymentMethod"`
	MockMode        bool                   `json:"mockMode"`
	CaptureInfo     *CaptureInfo           `json:"captureInfo,omitempty"`
	GatewayResponse *GatewayResponseInfo   `json:"gatewayResponse,omitempty"`
}

// PaymentMethodInfo represents payment method info in transaction results
type PaymentMethodInfo struct {
	ID       string  `json:"id"`
	Type     string  `json:"type"`
	Brand    string  `json:"brand"`
	Last4    string  `json:"last4"`
	Nickname *string `json:"nickname,omitempty"`
}

// CaptureInfo represents authorization capture information
type CaptureInfo struct {
	CanCapture bool   `json:"canCapture"`
	ExpiresAt  string `json:"expiresAt"`
}

// GatewayResponseInfo represents gateway response details
type GatewayResponseInfo struct {
	ResponseCode    string `json:"responseCode"`
	ResponseMessage string `json:"responseMessage"`
	AuthCode        string `json:"authCode,omitempty"`
	ReferenceNumber string `json:"referenceNumber,omitempty"`
}

// MockTransaction represents a mock transaction for testing
type MockTransaction struct {
	Success         bool    `json:"success"`
	TransactionID   string  `json:"transactionId"`
	AuthorizationID string  `json:"authorizationId,omitempty"`
	Amount          float64 `json:"amount"`
	Currency        string  `json:"currency"`
	Status          string  `json:"status"`
	ResponseCode    string  `json:"responseCode"`
	ResponseMessage string  `json:"responseMessage"`
	AuthCode        string  `json:"authCode,omitempty"`
	ReferenceNumber string  `json:"referenceNumber,omitempty"`
}

// PaymentMethodsStorage represents the JSON structure for payment methods file
type PaymentMethodsStorage struct {
	PaymentMethods []PaymentMethod `json:"paymentMethods"`
	LastUpdated    string         `json:"lastUpdated"`
}