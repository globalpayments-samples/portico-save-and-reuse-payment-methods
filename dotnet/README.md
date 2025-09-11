# .NET Vault One-Click Payment System

This example demonstrates a comprehensive vault one-click payment system using ASP.NET Core and the Global Payments SDK. It includes payment method management, secure tokenization, mock testing capabilities, and a complete web interface.

## Features

- **Payment Method Management** - Store, retrieve, and manage customer payment methods securely
- **Vault Tokenization** - Securely tokenize and store payment methods using Global Payments vault
- **One-Click Payments** - Process charges and scheduled payments using stored payment methods
- **Mock Mode** - Test payment flows with simulated responses without hitting live APIs
- **Comprehensive UI** - Complete web interface with payment method management and transaction processing
- **Test Card Integration** - Built-in Heartland certification test cards for development and testing

## Requirements

- .NET 9.0 or later
- Global Payments account and API credentials

## Project Structure

- `Program.cs` - Main application with all payment processing logic and API endpoints
- `Models.cs` - Data models and response structures
- `PaymentUtils.cs` - Payment utility functions and SDK integration
- `JsonStorage.cs` - JSON-based storage for payment methods
- `MockResponses.cs` - Mock data generation for testing scenarios
- `wwwroot/index.html` - Complete web interface with payment management
- `.env.sample` - Template for environment variables
- `run.sh` - Convenience script to run the application

## Recent Improvements

### ✅ Expiration Date Handling Fix (September 2024)
Fixed a critical issue where payment methods with 4-digit expiration years (e.g., "2028") were causing API errors. The system now correctly:
- Accepts both 2-digit ("28") and 4-digit ("2028") year formats from the frontend
- Converts 4-digit years to 2-digit format for SDK compatibility  
- Maintains backward compatibility with existing implementations
- Works seamlessly with all built-in test cards

## Setup

1. Clone this repository
2. Copy `.env.sample` to `.env`
3. Update `.env` with your Global Payments credentials:
   ```
   PUBLIC_API_KEY=pk_test_xxx
   SECRET_API_KEY=sk_test_xxx
   ```
4. Install dependencies:
   ```bash
   dotnet restore
   ```
5. Run the application:
   ```bash
   ./run.sh
   ```
   Or manually:
   ```bash
   dotnet run
   ```
6. Open your browser to `http://localhost:8000`

## API Endpoints

### GET /health
System health check endpoint.

**Response:**
```json
{
  "success": true,
  "data": {
    "status": "healthy",
    "timestamp": "2024-09-08T14:00:00Z",
    "service": "vault-one-click-dotnet",
    "version": "1.0.0"
  },
  "message": "System is healthy"
}
```

### GET /config
Returns configuration for frontend SDK initialization.

**Response:**
```json
{
  "success": true,
  "data": {
    "publicApiKey": "pk_test_xxx",
    "mockMode": false
  }
}
```

### GET /payment-methods
Retrieve all stored payment methods for the customer.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "pm_123456789",
      "type": "card",
      "last4": "1234",
      "brand": "Visa",
      "expiry": "12/28",
      "isDefault": true,
      "nickname": "My Primary Card"
    }
  ]
}
```

### POST /payment-methods
Create a new payment method or edit an existing one.

**Create Request:**
```json
{
  "cardNumber": "4012002000060016",
  "expiryMonth": "12",
  "expiryYear": "2028",
  "cvv": "123",
  "nickname": "Test Visa Card",
  "isDefault": true
}
```

**Edit Request:**
```json
{
  "id": "pm_123456789",
  "nickname": "Updated Nickname",
  "isDefault": false
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "pm_123456789",
    "vaultToken": "vault_abc123def456",
    "type": "card",
    "last4": "0016",
    "brand": "Visa",
    "expiry": "12/28",
    "nickname": "Test Visa Card",
    "isDefault": true,
    "mockMode": false
  }
}
```

### POST /charge
Process a $25.00 charge using a stored payment method.

**Request:**
```json
{
  "paymentMethodId": "pm_123456789"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "transactionId": "txn_987654321",
    "amount": 25.00,
    "currency": "USD",
    "status": "approved",
    "responseCode": "00",
    "responseMessage": "APPROVAL",
    "timestamp": "2024-09-08T14:00:00Z",
    "gatewayResponse": {
      "authCode": "123456",
      "referenceNumber": "ref_789012345"
    },
    "paymentMethod": {
      "id": "pm_123456789",
      "brand": "Visa",
      "last4": "0016",
      "nickname": "Test Visa Card"
    },
    "mockMode": false
  }
}
```

### POST /schedule-payment
Create a $50.00 authorization for scheduled payment.

**Request:**
```json
{
  "paymentMethodId": "pm_123456789"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "authorizationId": "auth_456789012",
    "transactionId": "txn_345678901",
    "amount": 50.00,
    "currency": "USD",
    "status": "authorized",
    "expiresAt": "2024-09-15T14:00:00Z",
    "captureInfo": {
      "canCapture": true,
      "expiresAt": "2024-09-15T14:00:00Z"
    },
    "mockMode": false
  }
}
```

### GET /mock-mode
Get current mock mode status.

**Response:**
```json
{
  "success": true,
  "data": {
    "isEnabled": false
  },
  "message": "Mock mode is disabled"
}
```

### POST /mock-mode
Toggle mock mode on/off.

**Request:**
```json
{
  "isEnabled": true
}
```

## Mock Mode

Mock mode allows you to test payment flows without hitting live APIs:
- **Enable/Disable**: Use the toggle in the web interface or the `/mock-mode` endpoint
- **Simulated Responses**: Generates realistic transaction data
- **Test Scenarios**: Different card numbers produce different response scenarios
- **Safe Testing**: No actual charges or API calls are made
- **Development**: Perfect for development and integration testing

## Built-in Test Cards

The system includes Heartland certification test cards:
- **Visa**: 4012002000060016
- **MasterCard**: 2223000010005780, 5473500000000014
- **Discover**: 6011000990156527  
- **American Express**: 372700699251018
- **JCB**: 3566007770007321

All test cards use:
- **Expiry**: 12/2028 (automatically handled by the expiration date fix)
- **CVV**: 123 (1234 for Amex)

## Implementation Details

### SDK Configuration
- Uses PorticoConfig for Global Payments SDK setup
- Loads credentials from environment variables
- Configures service URLs and developer identification
- Handles both live and certification environments

### Payment Processing
1. **Tokenization**: Create secure vault tokens for payment methods
2. **Storage**: Store payment method metadata in JSON format
3. **Processing**: Use vault tokens for charges and authorizations
4. **Error Handling**: Comprehensive error handling with meaningful messages

### Data Storage
- JSON file-based storage for payment methods
- Thread-safe operations for concurrent access
- Automatic backup and recovery capabilities
- Easy migration to database systems

### Security Features
- Vault tokenization ensures sensitive data never touches your servers
- Environment-based configuration management
- CORS protection for API endpoints
- Input validation and sanitization

## Production Considerations

For production deployment, enhance with:
- **Database Integration** - Replace JSON storage with proper database
- **Authentication** - Add user authentication and authorization
- **Rate Limiting** - Implement API rate limiting
- **Monitoring** - Add logging, metrics, and monitoring
- **Security** - Implement additional security headers and validation
- **PCI Compliance** - Follow PCI DSS requirements
- **Error Handling** - Enhanced error handling and user notifications
- **Testing** - Comprehensive unit and integration tests

## Troubleshooting

### Common Issues

**.NET Version Issues**:
- Ensure .NET 6.0 or later is installed
- Check version with `dotnet --version`
- Verify SDK is installed: `dotnet --list-sdks`
- Update .NET from https://dotnet.microsoft.com/download

**NuGet Package Issues**:
- Restore packages: `dotnet restore`
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Check for package conflicts in project file
- Verify package sources: `dotnet nuget list source`

**Build Configuration Issues**:
- Check for compilation errors: `dotnet build`
- Verify project file (.csproj) configuration
- Ensure proper target framework is specified
- Check for missing references or using statements

**Port and Hosting Issues**:
- Default port conflicts (check if 5000/5001 are available)
- Configure custom port: `dotnet run --urls "http://localhost:8080"`
- Verify HTTPS certificate for development
- Check firewall settings for port access

**Expiration Date Errors (Fixed)**:
- Issue: 4-digit years causing API errors
- Solution: Automatic conversion implemented
- Status: ✅ Resolved in recent update

**API Configuration**:
- Ensure SECRET_API_KEY is set in .env file
- Verify API key is for the correct environment (test/production)
- Check service URLs match your account configuration
- Validate .env file loading in Program.cs

**Payment Processing**:
- Use test cards in certification environment
- Enable mock mode for development testing
- Check console logs for detailed error information
- Verify proper exception handling in payment endpoints

**File System Permissions**:
- Ensure write permissions for data storage directory
- Check that JSON storage files can be created and modified
- Verify proper file paths in storage operations
- Set appropriate directory permissions on deployment

**CORS Issues**:
- Check browser developer console for CORS errors
- Verify CORS policy configuration in Program.cs
- Ensure proper preflight request handling
- Check allowed origins, methods, and headers

**ASP.NET Core Issues**:
- Verify middleware registration order
- Check for dependency injection configuration errors
- Ensure proper controller routing
- Validate model binding and validation attributes