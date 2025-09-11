# Vault One-Click Payment API

A comprehensive PHP API system for vault-based payment processing using the Global Payments SDK. This application provides secure payment method storage and processing with both immediate charges and scheduled payment authorizations.

## Requirements

- PHP 7.4 or later
- Composer
- Global Payments account and API credentials (optional - includes mock mode)

## Project Structure

- `index.php` - Main API router with CORS handling
- `config.php` - Configuration endpoint for client-side SDK
- `health.php` - System health check endpoint
- `payment-methods.php` - Payment method management (vault tokens)
- `charge.php` - Immediate payment processing ($25.00)
- `schedule-payment.php` - Payment authorization scheduling ($50.00)
- `PaymentUtils.php` - Core payment utilities and SDK integration
- `JsonStorage.php` - JSON-based data storage for payment methods
- `MockResponses.php` - Mock payment responses for testing
- `index.html` - Client-side demo interface
- `data/` - JSON storage directory for payment methods
- `composer.json` - Project dependencies
- `.env.sample` - Template for environment variables
- `run.sh` - Development server launcher

## Setup

1. Clone this repository
2. Copy `.env.sample` to `.env` (optional)
3. Update `.env` with your Global Payments credentials (if using real processing):
   ```
   PUBLIC_API_KEY=pk_test_xxx
   SECRET_API_KEY=sk_test_xxx
   ```
4. Install dependencies:
   ```bash
   composer install
   ```
5. Run the application:
   ```bash
   ./run.sh
   ```
   Or manually:
   ```bash
   php -S localhost:8000
   ```

## Implementation Details

### Application Architecture
The application uses a modular REST API structure:
- API router with endpoint-based file organization
- Utility classes for payment processing and data storage
- Mock mode fallback for testing without API credentials
- JSON-based data persistence for development

### SDK Configuration
Global Payments SDK integration:
- Environment-based configuration loading
- Portico gateway configuration for payment processing  
- Automatic fallback to mock responses when SDK unavailable
- Vault token management for secure payment method storage

### Data Storage
JSON-based storage system:
- Payment methods stored with vault tokens
- File-based persistence in `data/` directory
- Validation and error handling for data operations
- Mock data generation for testing scenarios

### Error Handling
Comprehensive error management with enhanced live mode support:
- **Live Mode Errors**: When not in mock mode, actual SDK errors are returned directly to clients with specific error codes (PAYMENT_DECLINED, AUTHORIZATION_DECLINED)
- **Mock Mode Fallback**: Mock responses only used when explicitly in mock mode or when SDK is unavailable
- **Structured Responses**: JSON error responses with HTTP status codes and descriptive error messages
- **Detailed Logging**: Error logging for debugging and monitoring payment processing issues
- **Field Consistency**: Automatic conversion from snake_case (SDK/internal) to camelCase (frontend) field naming

### Recent Improvements
- **Enhanced Error Transparency**: `/charge` and `/schedule-payment` endpoints now return actual SDK error messages instead of falling back to mock responses when payment processing fails in live mode
- **Frontend Compatibility**: Transaction IDs and authorization IDs properly mapped from snake_case internal format to camelCase for frontend display
- **Robust Payment Processing**: Improved error handling ensures clear communication of payment failures without masking underlying issues

## API Endpoints

### GET /health
System health check with detailed status information.

Response:
```json
{
  "success": true,
  "message": "System is healthy",
  "data": {
    "status": "healthy",
    "timestamp": "2024-01-01T12:00:00Z",
    "sdk_status": "available",
    "storage_status": "operational"
  }
}
```

### GET /payment-methods
Retrieve all saved payment methods.

Response:
```json
{
  "success": true,
  "data": [
    {
      "id": "pm_123",
      "type": "card",
      "last4": "4242",
      "brand": "visa",
      "expiry": "12/25",
      "isDefault": true,
      "nickname": "My Visa Card"
    }
  ]
}
```

### POST /payment-methods
Create and vault a new payment method.

Request:
```json
{
  "cardNumber": "4111111111111111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "cvv": "123",
  "nickname": "My Test Card",
  "isDefault": false
}
```

Response:
```json
{
  "success": true,
  "data": {
    "id": "pm_123",
    "vaultToken": "vault_abc123",
    "type": "card",
    "last4": "1111",
    "brand": "visa",
    "expiry": "12/25",
    "nickname": "My Test Card",
    "isDefault": false,
    "mockMode": false
  }
}
```

### POST /charge  
Process immediate payment charge ($25.00).

Request:
```json
{
  "paymentMethodId": "pm_123"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "transaction_id": "txn_456",
    "amount": 25.00,
    "currency": "USD",
    "status": "approved",
    "payment_method": {
      "id": "pm_123",
      "type": "card",
      "brand": "visa",
      "last4": "1111"
    },
    "mockMode": false
  }
}
```

### POST /schedule-payment
Create payment authorization for later capture ($50.00).

Request:
```json
{
  "paymentMethodId": "pm_123"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "authorization_id": "auth_789",
    "amount": 50.00,
    "currency": "USD",
    "status": "authorized",
    "payment_method": {
      "id": "pm_123",
      "type": "card", 
      "brand": "visa",
      "last4": "1111"
    },
    "capture_info": {
      "can_capture": true,
      "expires_at": "2024-01-08T12:00:00Z"
    },
    "mockMode": false
  }
}
```

## Troubleshooting

### Common Issues

**PHP Version Issues**:
- Ensure PHP 8.0 or later is installed
- Check version with `php --version`
- Verify required extensions are enabled (curl, json, mbstring)
- Update PHP from your system package manager or download from https://php.net

**Composer Dependencies**:
- Run `composer install` to install dependencies
- Clear composer cache: `composer clear-cache`
- Update dependencies: `composer update`
- Check for autoload issues: `composer dump-autoload`

**Web Server Configuration**:
- Ensure web server (Apache/Nginx) is configured for PHP
- Verify document root points to project directory
- Check .htaccess file permissions and mod_rewrite (Apache)
- Configure proper PHP-FPM settings for production

**API Configuration**:
- Ensure SECRET_API_KEY is set in .env file
- Verify API key is for the correct environment (test/production)
- Check console output for SDK configuration errors
- Validate .env file format and permissions

**Payment Processing**:
- Use test cards in certification environment
- Enable mock mode for development testing
- Check server error logs for detailed error information
- Verify proper JSON formatting in API requests
- Ensure proper error handling in payment endpoints

**File System Permissions**:
- Ensure write permissions for data storage directory
- Check that JSON storage files can be created and modified
- Verify proper file paths in storage operations
- Set appropriate directory permissions (755) and file permissions (644)

**CORS Issues**:
- Check browser developer console for CORS errors
- Verify CORS headers in HTTP responses
- Ensure proper preflight request handling
- Configure web server CORS settings if needed

**PHP Error Reporting**:
- Enable error reporting for development: `error_reporting(E_ALL)`
- Check PHP error logs for detailed error information
- Verify proper exception handling in payment processing
- Use try-catch blocks around SDK operations

### Debug Mode
Enable detailed error reporting for development:
```php
// Add to top of your PHP files for debugging
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);
```

## Security Considerations

This example demonstrates basic implementation. For production use, consider:
- Implementing additional input validation
- Adding request rate limiting
- Including security headers
- Implementing proper logging
- Adding payment fraud prevention measures
- Using HTTPS in production
- Implementing CSRF protection
- Configuring proper session handling
- Setting appropriate PHP security directives
