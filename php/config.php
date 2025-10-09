<?php

declare(strict_types=1);

/**
 * Configuration Endpoint
 *
 * This script provides configuration information for the client-side SDK,
 * including the public API key needed for tokenization.
 *
 * PHP version 7.4 or higher
 *
 * @category  Configuration
 * @package   GlobalPayments_Sample
 * @author    Global Payments
 * @license   MIT License
 * @link      https://github.com/globalpayments
 */

require_once 'PaymentUtils.php';

// Handle CORS
PaymentUtils::handleCORS();

// Initialize SDK (loads environment variables)
PaymentUtils::configureSdk();

// Only allow GET method
if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    PaymentUtils::sendErrorResponse(405, 'Method not allowed');
}

try {
    $response = [
        'publicApiKey' => $_ENV['PUBLIC_API_KEY'] ?? ''
    ];

    PaymentUtils::sendSuccessResponse($response, 'Configuration retrieved successfully');
} catch (Exception $e) {
    error_log('Configuration error: ' . $e->getMessage());
    PaymentUtils::sendErrorResponse(500, 'Error loading configuration', 'CONFIG_ERROR');
}
