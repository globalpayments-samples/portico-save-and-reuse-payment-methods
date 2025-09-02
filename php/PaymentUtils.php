<?php

declare(strict_types=1);

require_once 'vendor/autoload.php';

use Dotenv\Dotenv;
use GlobalPayments\Api\Entities\Address;
use GlobalPayments\Api\PaymentMethods\CreditCardData;
use GlobalPayments\Api\ServiceConfigs\Gateways\PorticoConfig;
use GlobalPayments\Api\ServicesContainer;

/**
 * Payment utility functions
 */
class PaymentUtils
{
    /**
     * Configure the Global Payments SDK
     */
    public static function configureSdk(): void
    {
        $dotenv = Dotenv::createImmutable(__DIR__);
        $dotenv->load();

        $config = new PorticoConfig();
        $config->secretApiKey = $_ENV['SECRET_API_KEY'];
        $config->developerId = '000000';
        $config->versionNumber = '0000';
        $config->serviceUrl = 'https://cert.api2.heartlandportico.com';
        
        ServicesContainer::configureService($config);
    }

    /**
     * Sanitize postal code by removing invalid characters
     */
    public static function sanitizePostalCode(?string $postalCode): string
    {
        if ($postalCode === null) {
            return '';
        }
        
        $sanitized = preg_replace('/[^a-zA-Z0-9-]/', '', $postalCode);
        return substr($sanitized, 0, 10);
    }

    /**
     * Determine card brand from card number
     */
    public static function determineCardBrand(string $cardNumber): string
    {
        $cardNumber = preg_replace('/\s+/', '', $cardNumber);
        
        if (preg_match('/^4/', $cardNumber)) {
            return 'Visa';
        } elseif (preg_match('/^5[1-5]/', $cardNumber) || preg_match('/^2[2-7]/', $cardNumber)) {
            return 'Mastercard';
        } elseif (preg_match('/^3[47]/', $cardNumber)) {
            return 'American Express';
        } elseif (preg_match('/^6(?:011|5)/', $cardNumber)) {
            return 'Discover';
        } else {
            return 'Unknown';
        }
    }

    /**
     * Create vault token using Global Payments SDK
     */
    public static function createVaultTokenWithSDK(array $data): string
    {
        try {
            $card = new CreditCardData();
            $card->number = $data['cardNumber'];
            $card->expMonth = $data['expiryMonth'];
            $card->expYear = $data['expiryYear'];
            $card->cvn = $data['cvv'];
            
            if (!empty($data['billingAddress'])) {
                $address = new Address();
                $address->streetAddress1 = $data['billingAddress']['street'] ?? '';
                $address->city = $data['billingAddress']['city'] ?? '';
                $address->state = $data['billingAddress']['state'] ?? '';
                $address->postalCode = $data['billingAddress']['zip'] ?? '';
                $address->country = $data['billingAddress']['country'] ?? 'US';
                $card->cardHolderName = $data['billingAddress']['name'] ?? '';
            }

            $response = $card->tokenize()->execute();
            
            if ($response->responseCode === '00' && !empty($response->token)) {
                return $response->token;
            } else {
                throw new \Exception('Tokenization failed: ' . ($response->responseMessage ?? 'Unknown error'));
            }
        } catch (\Exception $e) {
            error_log('SDK tokenization error: ' . $e->getMessage());
            throw $e;
        }
    }

    /**
     * Process payment using Global Payments SDK
     */
    public static function processPaymentWithSDK(string $vaultToken, float $amount, string $currency): array
    {
        try {
            $card = new CreditCardData();
            $card->token = $vaultToken;

            $response = $card->charge($amount)
                ->withCurrency($currency)
                ->execute();

            if ($response->responseCode === '00') {
                return [
                    'transaction_id' => $response->transactionId ?? 'txn_' . uniqid(),
                    'amount' => $amount,
                    'currency' => $currency,
                    'status' => 'approved',
                    'response_code' => $response->responseCode,
                    'response_message' => $response->responseMessage ?? 'Approved',
                    'timestamp' => date('c'),
                    'gateway_response' => [
                        'auth_code' => $response->authorizationCode ?? '',
                        'reference_number' => $response->referenceNumber ?? ''
                    ]
                ];
            } else {
                throw new \Exception('Payment failed: ' . ($response->responseMessage ?? 'Unknown error'));
            }
        } catch (\Exception $e) {
            error_log('SDK payment processing error: ' . $e->getMessage());
            throw $e;
        }
    }

    /**
     * Create authorization using Global Payments SDK
     */
    public static function createAuthorizationWithSDK(string $vaultToken, float $amount, string $currency): array
    {
        try {
            $card = new CreditCardData();
            $card->token = $vaultToken;

            $response = $card->authorize($amount)
                ->withCurrency($currency)
                ->execute();

            if ($response->responseCode === '00') {
                return [
                    'authorization_id' => 'auth_' . uniqid(),
                    'transaction_id' => $response->transactionId ?? 'txn_' . uniqid(),
                    'amount' => $amount,
                    'currency' => $currency,
                    'status' => 'authorized',
                    'response_code' => $response->responseCode,
                    'response_message' => $response->responseMessage ?? 'Authorized',
                    'timestamp' => date('c'),
                    'expires_at' => date('c', strtotime('+7 days')),
                    'gateway_response' => [
                        'auth_code' => $response->authorizationCode ?? '',
                        'reference_number' => $response->referenceNumber ?? ''
                    ]
                ];
            } else {
                throw new \Exception('Authorization failed: ' . ($response->responseMessage ?? 'Unknown error'));
            }
        } catch (\Exception $e) {
            error_log('SDK authorization error: ' . $e->getMessage());
            throw $e;
        }
    }

    /**
     * Send success response
     */
    public static function sendSuccessResponse($data, string $message = 'Operation completed successfully'): void
    {
        http_response_code(200);
        
        $response = [
            'success' => true,
            'data' => $data,
            'message' => $message,
            'timestamp' => date('c')
        ];
        
        echo json_encode($response);
        exit();
    }

    /**
     * Send error response
     */
    public static function sendErrorResponse(int $statusCode, string $message, string $errorCode = null): void
    {
        http_response_code($statusCode);
        
        $response = [
            'success' => false,
            'message' => $message,
            'timestamp' => date('c')
        ];
        
        if ($errorCode) {
            $response['error_code'] = $errorCode;
        }
        
        echo json_encode($response);
        exit();
    }

    /**
     * Handle CORS headers
     */
    public static function handleCORS(): void
    {
        header('Access-Control-Allow-Origin: *');
        header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
        header('Access-Control-Allow-Headers: Content-Type, Authorization');
        header('Content-Type: application/json');
        
        if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
            http_response_code(200);
            exit();
        }
    }

    /**
     * Parse JSON input for POST requests
     */
    public static function parseJsonInput(): array
    {
        $inputData = [];
        if ($_SERVER['REQUEST_METHOD'] === 'POST') {
            $rawInput = file_get_contents('php://input');
            if ($rawInput) {
                $inputData = json_decode($rawInput, true) ?? [];
            }
            $inputData = array_merge($_POST, $inputData);
        }
        return $inputData;
    }
}