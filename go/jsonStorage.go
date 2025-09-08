package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
	"sync"
	"time"
)

var storageMutex sync.RWMutex

const (
	paymentMethodsFile = "data/payment_methods.json"
	dataDirectory     = "data"
)

// Initialize storage directory and files
func initializeStorage() error {
	// Create data directory if it doesn't exist
	if err := os.MkdirAll(dataDirectory, 0755); err != nil {
		return fmt.Errorf("failed to create data directory: %v", err)
	}

	// Initialize payment methods file if it doesn't exist
	if _, err := os.Stat(paymentMethodsFile); os.IsNotExist(err) {
		initialData := PaymentMethodsStorage{
			PaymentMethods: []PaymentMethod{},
			LastUpdated:    getCurrentTimestamp(),
		}

		data, err := json.MarshalIndent(initialData, "", "  ")
		if err != nil {
			return fmt.Errorf("failed to marshal initial data: %v", err)
		}

		if err := ioutil.WriteFile(paymentMethodsFile, data, 0644); err != nil {
			return fmt.Errorf("failed to write initial payment methods file: %v", err)
		}
	}

	return nil
}

// Load payment methods from JSON file
func loadPaymentMethods() ([]PaymentMethod, error) {
	storageMutex.RLock()
	defer storageMutex.RUnlock()

	// Check if file exists
	if _, err := os.Stat(paymentMethodsFile); os.IsNotExist(err) {
		return []PaymentMethod{}, nil
	}

	// Read file
	data, err := ioutil.ReadFile(paymentMethodsFile)
	if err != nil {
		return nil, fmt.Errorf("failed to read payment methods file: %v", err)
	}

	// Parse JSON
	var storage PaymentMethodsStorage
	if err := json.Unmarshal(data, &storage); err != nil {
		return nil, fmt.Errorf("failed to parse payment methods JSON: %v", err)
	}

	return storage.PaymentMethods, nil
}

// Save a new payment method
func savePaymentMethod(method *PaymentMethod) error {
	storageMutex.Lock()
	defer storageMutex.Unlock()

	// Load existing methods
	methods, err := loadPaymentMethodsUnsafe()
	if err != nil {
		return err
	}

	// Add new method
	methods = append(methods, *method)

	// Save back to file
	return savePaymentMethodsUnsafe(methods)
}

// Update an existing payment method
func updatePaymentMethodInStorage(updatedMethod *PaymentMethod) error {
	storageMutex.Lock()
	defer storageMutex.Unlock()

	// Load existing methods
	methods, err := loadPaymentMethodsUnsafe()
	if err != nil {
		return err
	}

	// Find and update the method
	found := false
	for i := range methods {
		if methods[i].ID == updatedMethod.ID {
			methods[i] = *updatedMethod
			found = true
			break
		}
	}

	if !found {
		return fmt.Errorf("payment method not found: %s", updatedMethod.ID)
	}

	// Save back to file
	return savePaymentMethodsUnsafe(methods)
}

// Clear default flag from all payment methods
func clearDefaultPaymentMethods() error {
	storageMutex.Lock()
	defer storageMutex.Unlock()

	// Load existing methods
	methods, err := loadPaymentMethodsUnsafe()
	if err != nil {
		return err
	}

	// Clear default flag from all methods
	for i := range methods {
		if methods[i].IsDefault {
			methods[i].IsDefault = false
			methods[i].UpdatedAt = time.Now()
		}
	}

	// Save back to file
	return savePaymentMethodsUnsafe(methods)
}

// Load payment methods without mutex (for internal use)
func loadPaymentMethodsUnsafe() ([]PaymentMethod, error) {
	// Check if file exists
	if _, err := os.Stat(paymentMethodsFile); os.IsNotExist(err) {
		return []PaymentMethod{}, nil
	}

	// Read file
	data, err := ioutil.ReadFile(paymentMethodsFile)
	if err != nil {
		return nil, fmt.Errorf("failed to read payment methods file: %v", err)
	}

	// Parse JSON
	var storage PaymentMethodsStorage
	if err := json.Unmarshal(data, &storage); err != nil {
		return nil, fmt.Errorf("failed to parse payment methods JSON: %v", err)
	}

	return storage.PaymentMethods, nil
}

// Save payment methods without mutex (for internal use)
func savePaymentMethodsUnsafe(methods []PaymentMethod) error {
	storage := PaymentMethodsStorage{
		PaymentMethods: methods,
		LastUpdated:    getCurrentTimestamp(),
	}

	// Marshal to JSON with indentation
	data, err := json.MarshalIndent(storage, "", "  ")
	if err != nil {
		return fmt.Errorf("failed to marshal payment methods: %v", err)
	}

	// Write to file
	if err := ioutil.WriteFile(paymentMethodsFile, data, 0644); err != nil {
		return fmt.Errorf("failed to write payment methods file: %v", err)
	}

	return nil
}

// Mock mode configuration storage
const mockModeConfigFile = "data/mock_mode_config.json"

type MockModeStorage struct {
	IsEnabled   bool   `json:"isEnabled"`
	LastUpdated string `json:"lastUpdated"`
}

// Load mock mode configuration
func loadMockModeConfig() (bool, error) {
	if _, err := os.Stat(mockModeConfigFile); os.IsNotExist(err) {
		return false, nil // Default to disabled
	}

	data, err := ioutil.ReadFile(mockModeConfigFile)
	if err != nil {
		return false, fmt.Errorf("failed to read mock mode config: %v", err)
	}

	var config MockModeStorage
	if err := json.Unmarshal(data, &config); err != nil {
		return false, fmt.Errorf("failed to parse mock mode config: %v", err)
	}

	return config.IsEnabled, nil
}

// Save mock mode configuration
func saveMockModeConfig(enabled bool) error {
	config := MockModeStorage{
		IsEnabled:   enabled,
		LastUpdated: getCurrentTimestamp(),
	}

	data, err := json.MarshalIndent(config, "", "  ")
	if err != nil {
		return fmt.Errorf("failed to marshal mock mode config: %v", err)
	}

	if err := ioutil.WriteFile(mockModeConfigFile, data, 0644); err != nil {
		return fmt.Errorf("failed to write mock mode config: %v", err)
	}

	return nil
}

// Delete a payment method (for future use)
func deletePaymentMethod(methodID string) error {
	storageMutex.Lock()
	defer storageMutex.Unlock()

	// Load existing methods
	methods, err := loadPaymentMethodsUnsafe()
	if err != nil {
		return err
	}

	// Find and remove the method
	found := false
	for i := range methods {
		if methods[i].ID == methodID {
			// Remove element at index i
			methods = append(methods[:i], methods[i+1:]...)
			found = true
			break
		}
	}

	if !found {
		return fmt.Errorf("payment method not found: %s", methodID)
	}

	// Save back to file
	return savePaymentMethodsUnsafe(methods)
}