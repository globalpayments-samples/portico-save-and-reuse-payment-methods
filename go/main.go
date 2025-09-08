// Package main implements a comprehensive vault-based payment processing server
// using the Global Payments SDK. It provides secure payment method storage,
// processing, and mock/live mode functionality.
package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"

	"github.com/gorilla/mux"
	"github.com/joho/godotenv"
	"github.com/rs/cors"
)

// Global mock mode state
var mockModeEnabled bool = false


func main() {
	// Load environment variables
	err := godotenv.Load(".env")
	if err != nil {
		log.Println("Warning: .env file not found, using environment variables")
	}

	// Initialize storage directory
	err = initializeStorage()
	if err != nil {
		log.Fatalf("Failed to initialize storage: %v", err)
	}

	// Load mock mode configuration
	mockModeEnabled, err = loadMockModeConfig()
	if err != nil {
		log.Printf("Warning: Failed to load mock mode config: %v. Using default (disabled)", err)
		mockModeEnabled = false
	}

	// Create router
	r := mux.NewRouter()

	// Setup CORS
	c := cors.New(cors.Options{
		AllowedOrigins:   []string{"*"},
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS"},
		AllowedHeaders:   []string{"*"},
		AllowCredentials: true,
	})

	// API endpoints
	r.HandleFunc("/health", healthHandler).Methods("GET", "OPTIONS")
	r.HandleFunc("/config", configHandler).Methods("GET", "OPTIONS")
	r.HandleFunc("/mock-mode", mockModeHandler).Methods("GET", "POST", "OPTIONS")
	r.HandleFunc("/payment-methods", paymentMethodsHandler).Methods("GET", "POST", "OPTIONS")
	r.HandleFunc("/charge", chargeHandler).Methods("POST", "OPTIONS")
	r.HandleFunc("/schedule-payment", schedulePaymentHandler).Methods("POST", "OPTIONS")

	// Serve static files
	r.PathPrefix("/").Handler(http.FileServer(http.Dir("./")))

	// Apply CORS middleware
	handler := c.Handler(r)

	// Start server
	port := getEnv("PORT", "8000")
	fmt.Printf("🚀 Go Vault Payment Server starting on http://localhost:%s\n", port)
	fmt.Printf("📊 Mock Mode: %v\n", mockModeEnabled)
	fmt.Printf("🔧 Environment: %s\n", getEnv("APP_ENV", "development"))
	
	log.Fatal(http.ListenAndServe(":"+port, handler))
}

// Helper function to get environment variable with default
func getEnv(key, defaultValue string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return defaultValue
}

// Send JSON response helper
func sendJSONResponse(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

// Send error response helper
func sendErrorResponse(w http.ResponseWriter, status int, message, errorCode string) {
	response := APIResponse{
		Success:   false,
		Message:   message,
		ErrorCode: &errorCode,
		Timestamp: getCurrentTimestamp(),
	}

	sendJSONResponse(w, status, response)
}
