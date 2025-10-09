package main

import (
	"github.com/google/uuid"
)

// getMockTransactionResult generates mock transaction responses based on card last 4 digits
func getMockTransactionResult(last4 string, amount float64, transactionType string) MockTransaction {
	// Generate unique IDs for this transaction
	transactionID := "txn_" + uuid.New().String()
	authorizationID := "auth_" + uuid.New().String()
	
	// Default successful response
	response := MockTransaction{
		Success:         true,
		TransactionID:   transactionID,
		AuthorizationID: authorizationID,
		Amount:          amount,
		Currency:        "USD",
		Status:          "approved",
		ResponseCode:    "00",
		ResponseMessage: "Approved",
		AuthCode:        "123456",
		ReferenceNumber: "REF" + uuid.New().String()[:8],
	}
	
	// Adjust status based on transaction type
	if transactionType == "authorize" {
		response.Status = "authorized"
	}
	
	// Apply card-specific test scenarios based on last 4 digits
	switch last4 {
	case "0002":
		// Declined - Insufficient Funds
		response.Success = false
		response.ResponseCode = "51"
		response.ResponseMessage = "Declined - Insufficient Funds"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "0003":
		// Declined - Invalid Card
		response.Success = false
		response.ResponseCode = "14"
		response.ResponseMessage = "Declined - Invalid Card Number"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "0004":
		// Declined - Expired Card
		response.Success = false
		response.ResponseCode = "54"
		response.ResponseMessage = "Declined - Expired Card"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "0005":
		// Declined - Do Not Honor
		response.Success = false
		response.ResponseCode = "05"
		response.ResponseMessage = "Declined - Do Not Honor"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "0006":
		// Timeout/System Error
		response.Success = false
		response.ResponseCode = "91"
		response.ResponseMessage = "System Error - Please Try Again"
		response.Status = "error"
		response.AuthCode = ""
		
	case "0007":
		// Partial Approval (for testing edge cases)
		response.Success = true
		response.Amount = amount * 0.5 // Half the requested amount
		response.ResponseMessage = "Partial Approval"
		response.Status = "partial"
		
	case "0008":
		// CVV Mismatch
		response.Success = false
		response.ResponseCode = "85"
		response.ResponseMessage = "Declined - CVV Mismatch"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "0009":
		// Invalid Amount
		response.Success = false
		response.ResponseCode = "13"
		response.ResponseMessage = "Declined - Invalid Amount"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "1111":
		// Processing Error
		response.Success = false
		response.ResponseCode = "96"
		response.ResponseMessage = "Processing Error"
		response.Status = "error"
		response.AuthCode = ""
		
	case "2222":
		// Fraud Suspected
		response.Success = false
		response.ResponseCode = "59"
		response.ResponseMessage = "Declined - Suspected Fraud"
		response.Status = "declined"
		response.AuthCode = ""
		
	case "3333":
		// Card Restricted
		response.Success = false
		response.ResponseCode = "62"
		response.ResponseMessage = "Declined - Restricted Card"
		response.Status = "declined"
		response.AuthCode = ""
		
	default:
		// All other cards: Approved
		// Default response is already set to approved
	}
	
	return response
}

// getTestCardInfo returns information about test cards for mock mode
func getTestCardInfo() map[string]string {
	return map[string]string{
		"4111111111111111": "Visa - Approved",
		"4111111111110002": "Visa - Declined (Insufficient Funds)",
		"4111111111110003": "Visa - Declined (Invalid Card)",
		"4111111111110004": "Visa - Declined (Expired Card)",
		"4111111111110005": "Visa - Declined (Do Not Honor)",
		"4111111111110006": "Visa - System Error",
		"4111111111110007": "Visa - Partial Approval",
		"4111111111110008": "Visa - CVV Mismatch",
		"4111111111110009": "Visa - Invalid Amount",
		"4111111111111110": "Visa - Processing Error",
		"4111111111112222": "Visa - Fraud Suspected",
		"4111111111113333": "Visa - Restricted Card",
		"5425233430109903": "Mastercard - Approved",
		"5425233430100002": "Mastercard - Declined (Insufficient Funds)",
		"378282246310005":  "American Express - Approved",
		"6011111111116611": "Discover - Approved",
	}
}

// getMockPaymentMethodResult generates mock responses for payment method creation
func getMockPaymentMethodResult(cardNumber, last4 string) (bool, string) {
	// Remove spaces from card number
	cleanCardNumber := cardNumber
	
	// Check for specific test scenarios based on card number
	switch cleanCardNumber {
	case "4000000000009995":
		return false, "Invalid card number"
	case "4000000000009987":
		return false, "Card declined by issuer"
	case "4000000000000002":
		return false, "Card declined - insufficient funds"
	default:
		// Most cards succeed in mock mode
		return true, "Payment method created successfully"
	}
}