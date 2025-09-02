<?php

declare(strict_types=1);

/**
 * Schedule Payment Endpoint
 * 
 * POST /schedule-payment - Create delayed charge authorization ($50.00)
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

    $amount = 50.00;
    $currency = 'USD';
    
    $authorizationResult = null;
    $mockMode = false;

    if (!empty($_ENV['SECRET_API_KEY']) && class_exists('\GlobalPayments\Api\ServicesContainer')) {
        try {
            $authorizationResult = PaymentUtils::createAuthorizationWithSDK($paymentMethod['vaultToken'], $amount, $currency);
        } catch (\Exception $e) {
            error_log('Global Payments SDK authorization error: ' . $e->getMessage());
            $mockMode = true;
        }
    } else {
        $mockMode = true;
    }

    if ($mockMode || !$authorizationResult) {
        $responseType = MockResponses::getResponseByCardNumber($paymentMethod['last4']);
        
        if ($responseType === 'success') {
            $authorizationResult = MockResponses::getAuthorizationResponse($amount, $data['paymentMethodId']);
        } else {
            $declineReason = str_replace(['decline_', 'error_'], '', $responseType);
            $declineResponse = MockResponses::getDeclineResponse($declineReason);
            PaymentUtils::sendErrorResponse(422, $declineResponse['response_message'], $declineResponse['error_code']);
        }
    }

    $response = array_merge($authorizationResult, [
        'payment_method' => [
            'id' => $paymentMethod['id'],
            'type' => 'card',
            'brand' => $paymentMethod['cardBrand'],
            'last4' => $paymentMethod['last4'],
            'nickname' => $paymentMethod['nickname'] ?? ''
        ],
        'mockMode' => $mockMode,
        'capture_info' => [
            'can_capture' => true,
            'expires_at' => $authorizationResult['expires_at'] ?? date('c', strtotime('+7 days'))
        ]
    ]);

    PaymentUtils::sendSuccessResponse($response, 'Payment scheduled successfully');

} catch (\Exception $e) {
    error_log('Error scheduling payment: ' . $e->getMessage());
    PaymentUtils::sendErrorResponse(500, 'Payment scheduling failed', 'SERVER_ERROR');
}