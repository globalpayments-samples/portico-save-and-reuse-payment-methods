<?php

declare(strict_types=1);

/**
 * Charge Endpoint
 * 
 * POST /charge - Process immediate payment ($25.00)
 */

require_once 'PaymentUtils.php';
require_once 'JsonStorage.php';
require_once 'MockResponses.php';

// Handle CORS
PaymentUtils::handleCORS();

// Initialize SDK
PaymentUtils::configureSdk();

// Only allow POST method
if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    PaymentUtils::sendErrorResponse(405, 'Method not allowed');
}

try {
    $data = PaymentUtils::parseJsonInput();
    
    if (empty($data['paymentMethodId'])) {
        PaymentUtils::sendErrorResponse(400, 'Payment method ID is required', 'VALIDATION_ERROR');
    }

    $paymentMethod = JsonStorage::findPaymentMethod($data['paymentMethodId']);
    if (!$paymentMethod) {
        PaymentUtils::sendErrorResponse(404, 'Payment method not found', 'NOT_FOUND');
    }

    $amount = 25.00;
    $currency = 'USD';
    
    $transactionResult = null;
    $mockMode = false;

    if (!empty($_ENV['SECRET_API_KEY'])) {
        try {
            $transactionResult = PaymentUtils::processPaymentWithSDK($paymentMethod['vaultToken'], $amount, $currency);
        } catch (\Exception $e) {
            error_log('Global Payments SDK payment error: ' . $e->getMessage());
            $mockMode = true;
        }
    } else {
        $mockMode = true;
    }

    if ($mockMode || !$transactionResult) {
        $responseType = MockResponses::getResponseByCardNumber($paymentMethod['last4']);
        
        if ($responseType === 'success') {
            $transactionResult = MockResponses::getPaymentResponse($amount, $data['paymentMethodId']);
        } else {
            $declineReason = str_replace(['decline_', 'error_'], '', $responseType);
            $declineResponse = MockResponses::getDeclineResponse($declineReason);
            PaymentUtils::sendErrorResponse(422, $declineResponse['response_message'], $declineResponse['error_code']);
        }
    }

    $response = array_merge($transactionResult, [
        'payment_method' => [
            'id' => $paymentMethod['id'],
            'type' => 'card',
            'brand' => $paymentMethod['cardBrand'],
            'last4' => $paymentMethod['last4'],
            'nickname' => $paymentMethod['nickname'] ?? ''
        ],
        'mockMode' => $mockMode
    ]);

    PaymentUtils::sendSuccessResponse($response, 'Payment processed successfully');

} catch (\Exception $e) {
    error_log('Error processing charge: ' . $e->getMessage());
    PaymentUtils::sendErrorResponse(500, 'Payment processing failed', 'SERVER_ERROR');
}