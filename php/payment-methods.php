<?php

declare(strict_types=1);

/**
 * Payment Methods Endpoint
 * 
 * GET /payment-methods - Retrieve saved payment methods
 * POST /payment-methods - Create new payment method (vault token) OR edit existing payment method
 *                         - Create: Requires cardNumber, expiryMonth, expiryYear, cvv (+ optional nickname, isDefault)
 *                         - Edit: Requires id (+ optional nickname, isDefault) - only nickname and default status can be edited
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
        $data = PaymentUtils::parseJsonInput();
        
        // Check if this is an edit operation (has 'id' field)
        if (!empty($data['id'])) {
            // Edit existing payment method
            $paymentMethodId = $data['id'];
            
            // Validate that the payment method exists
            if (!JsonStorage::paymentMethodExists($paymentMethodId)) {
                PaymentUtils::sendErrorResponse(404, 'Payment method not found', 'NOT_FOUND');
            }
            
            // Prepare update data (only editable fields)
            $updateData = [];
            if (isset($data['nickname'])) {
                $updateData['nickname'] = $data['nickname'];
            }
            if (isset($data['isDefault'])) {
                $updateData['isDefault'] = $data['isDefault'];
            }
            
            // Validate update data
            $validationErrors = JsonStorage::validateUpdateData($updateData);
            if (!empty($validationErrors)) {
                PaymentUtils::sendErrorResponse(400, implode(', ', $validationErrors), 'VALIDATION_ERROR');
            }
            
            // Update the payment method
            if (!JsonStorage::updatePaymentMethod($paymentMethodId, $updateData)) {
                PaymentUtils::sendErrorResponse(500, 'Failed to update payment method', 'UPDATE_ERROR');
            }
            
            // Get updated payment method for response
            $updatedMethod = JsonStorage::findPaymentMethod($paymentMethodId);
            
            $response = [
                'id' => $updatedMethod['id'],
                'type' => 'card',
                'last4' => $updatedMethod['last4'],
                'brand' => $updatedMethod['cardBrand'],
                'expiry' => $updatedMethod['expiryMonth'] . '/' . $updatedMethod['expiryYear'],
                'nickname' => $updatedMethod['nickname'] ?? '',
                'isDefault' => $updatedMethod['isDefault'] ?? false,
                'updatedAt' => $updatedMethod['updatedAt']
            ];
            
            PaymentUtils::sendSuccessResponse($response, 'Payment method updated successfully');
            
        } else {
            // Create a new payment method
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
        }
        
    } else {
        PaymentUtils::sendErrorResponse(405, 'Method not allowed');
    }

} catch (\Exception $e) {
    error_log('Payment methods error: ' . $e->getMessage());
    PaymentUtils::sendErrorResponse(500, 'Internal server error', 'SERVER_ERROR');
}