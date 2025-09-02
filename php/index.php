<?php

declare(strict_types=1);

/**
 * API Router for Vault One-Click Payment System
 *
 * This script routes requests to individual endpoint files:
 * - /health -> health.php
 * - /payment-methods -> payment-methods.php  
 * - /charge -> charge.php
 * - /schedule-payment -> schedule-payment.php
 *
 * PHP version 7.4 or higher
 *
 * @category  Payment_Processing
 * @package   VaultOneClick
 * @author    Global Payments
 * @license   MIT License
 * @link      https://github.com/globalpayments
 */

ini_set('display_errors', '0');

// Get the requested path
$method = $_SERVER['REQUEST_METHOD'];
$path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);

// Remove leading slash and normalize path
$path = ltrim($path, '/');

// Handle CORS for all requests
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization');
header('Content-Type: application/json');

if ($method === 'OPTIONS') {
    http_response_code(200);
    exit();
}

// Route to appropriate endpoint file
switch ($path) {
    case 'health':
        require_once 'health.php';
        break;
        
    case 'payment-methods':
        require_once 'payment-methods.php';
        break;
        
    case 'charge':
        require_once 'charge.php';
        break;
        
    case 'schedule-payment':
        require_once 'schedule-payment.php';
        break;
        
    default:
        http_response_code(404);
        echo json_encode([
            'success' => false, 
            'message' => 'Endpoint not found',
            'available_endpoints' => [
                'GET /health' => 'System health check',
                'GET /payment-methods' => 'Get payment methods',
                'POST /payment-methods' => 'Create payment method',
                'POST /charge' => 'Process immediate charge ($25)',
                'POST /schedule-payment' => 'Schedule payment authorization ($50)'
            ]
        ]);
}
