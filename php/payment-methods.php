<?php

declare(strict_types=1);

/**
 * Payment Methods Endpoint
 * 
 * GET /payment-methods - Retrieve saved payment methods
 * POST /payment-methods - Create new payment method (vault token)
 */

require_once 'PaymentUtils.php';
require_once 'JsonStorage.php';
require_once 'MockResponses.php';
require_once 'mock-mode.php';

// Handle CORS
PaymentUtils::handleCORS();

// Initialize SDK
PaymentUtils::configureSdk();

$method = $_SERVER['REQUEST_METHOD'];

try {
    if ($method === 'GET') {
        // Get all payment methods
        $paymentMethods = JsonStorage::readPaymentMethods();
        
        $formattedMethods = array_map(function($method) {
            return [
                'id' => $method['id'],
                'type' => 'card',
                'last4' => $method['last4'],
                'brand' => $method['cardBrand'],
                'expiry' => $method['expiryMonth'] . '/' . $method['expiryYear'],
                'isDefault' => $method['isDefault'] ?? false,
                'nickname' => $method['nickname'] ?? ''
            ];
        }, $paymentMethods);
        
        PaymentUtils::sendSuccessResponse($formattedMethods, 'Payment methods retrieved successfully');
        
    } elseif ($method === 'POST') {
        // Create a new payment method
        $data = PaymentUtils::parseJsonInput();
        
        if (empty($data['cardNumber']) || empty($data['expiryMonth']) || 
            empty($data['expiryYear']) || empty($data['cvv'])) {
            PaymentUtils::sendErrorResponse(400, 'Missing required card details', 'VALIDATION_ERROR');
        }

        $paymentMethodId = JsonStorage::generateId();
        $cardNumber = $data['cardNumber'];
        $expiryMonth = str_pad($data['expiryMonth'], 2, '0', STR_PAD_LEFT);
        $expiryYear = substr($data['expiryYear'], -2);
        $cvv = $data['cvv'];
        $last4 = substr($cardNumber, -4);
        $cardBrand = PaymentUtils::determineCardBrand($cardNumber);
        
        $validationData = [
            'cardBrand' => $cardBrand,
            'last4' => $last4,
            'expiryMonth' => $expiryMonth,
            'expiryYear' => $expiryYear
        ];
        
        $validationErrors = JsonStorage::validatePaymentMethod($validationData);
        if (!empty($validationErrors)) {
            PaymentUtils::sendErrorResponse(400, implode(', ', $validationErrors), 'VALIDATION_ERROR');
        }

        $vaultToken = null;
        $mockMode = MockModeConfig::isMockModeEnabled();

        if (!$mockMode && !empty($_ENV['SECRET_API_KEY'])) {
            try {
                $vaultToken = PaymentUtils::createVaultTokenWithSDK($data);
            } catch (\Exception $e) {
                error_log('Global Payments SDK error: ' . $e->getMessage());
                $mockMode = true;
            }
        } else {
            $mockMode = true;
        }

        if ($mockMode || !$vaultToken) {
            $mockResponse = MockResponses::getVaultToken([
                'brand' => $cardBrand,
                'last4' => $last4,
                'exp_month' => $expiryMonth,
                'exp_year' => $expiryYear
            ]);
            $vaultToken = $mockResponse['id'];
        }

        $paymentMethod = [
            'id' => $paymentMethodId,
            'vaultToken' => $vaultToken,
            'cardBrand' => $cardBrand,
            'last4' => $last4,
            'expiryMonth' => $expiryMonth,
            'expiryYear' => $expiryYear,
            'nickname' => $data['nickname'] ?? ($cardBrand . ' ending in ' . $last4),
            'isDefault' => $data['isDefault'] ?? false
        ];

        if (!JsonStorage::addPaymentMethod($paymentMethod)) {
            PaymentUtils::sendErrorResponse(500, 'Failed to save payment method', 'STORAGE_ERROR');
        }

        $response = [
            'id' => $paymentMethodId,
            'vaultToken' => $vaultToken,
            'type' => 'card',
            'last4' => $last4,
            'brand' => $cardBrand,
            'expiry' => $expiryMonth . '/' . $expiryYear,
            'nickname' => $paymentMethod['nickname'],
            'isDefault' => $paymentMethod['isDefault'],
            'mockMode' => $mockMode
        ];

        PaymentUtils::sendSuccessResponse($response, 'Payment method created and saved successfully');
        
    } else {
        PaymentUtils::sendErrorResponse(405, 'Method not allowed');
    }

} catch (\Exception $e) {
    error_log('Payment methods error: ' . $e->getMessage());
    PaymentUtils::sendErrorResponse(500, 'Internal server error', 'SERVER_ERROR');
}