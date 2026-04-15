# Node.js Save and Reuse Payment Methods Payment System

This example demonstrates a comprehensive save and reuse payment methods payment system using Express.js and the Global Payments SDK. It includes payment method management, secure tokenization, mock testing capabilities, and a complete web interface.

## Features

- **Payment Method Management** - Store, retrieve, and manage customer payment methods securely
- **Multi-Use Token Creation** - Convert single-use tokens to multi-use stored payment tokens with customer data
- **One-Click Payments** - Process charges using stored multi-use payment methods
- **Mock Mode** - Test payment flows with simulated responses without hitting live APIs
- **Comprehensive UI** - Complete web interface with payment method management and transaction processing
- **Test Card Integration** - Built-in Global Payments test cards for development and testing

## Requirements

- Node.js 18.x or later
- npm (Node Package Manager) 8.x or later
- Global Payments account and API credentials

## Project Structure

- `server.js` - Main Express.js application with all API endpoints and payment processing logic
- `jsonStorage.js` - JSON-based storage utility for payment methods with file operations
- `paymentUtils.js` - Payment utility functions and Global Payments SDK integration
- `mockResponses.js` - Mock data generation for testing scenarios and simulated responses
- `index.html` - Complete web interface with payment method management and transaction processing
- `package.json` - Node.js dependencies and npm scripts
- `.env.sample` - Template for environment variables
- `run.sh` - Convenience script to install dependencies and run the application

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
   npm install
   ```
5. Run the application:
   ```bash
   ./run.sh
   ```
   Or manually:
   ```bash
   node server.js
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
    "timestamp": "2024-09-08T14:00:00.000Z",
    "service": "save-reuse-payment-nodejs",
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
      "expiry": "12/2028",
      "isDefault": true,
      "nickname": "My Primary Card"
    }
  ]
}
```

### POST /payment-methods
Create multi-use token with customer data or edit an existing payment method.

**Create Multi-Use Token Request:**
```json
{
  "payment_token": "supt_abc123",
  "cardDetails": {
    "cardType": "visa",
    "cardLast4": "4242",
    "expiryMonth": "12",
    "expiryYear": "2028"
  },
  "first_name": "Jane",
  "last_name": "Doe",
  "email": "jane@example.com",
  "phone": "5551234567",
  "street_address": "123 Main St",
  "city": "Anytown",
  "state": "NY",
  "billing_zip": "12345",
  "country": "USA",
  "nickname": "My Visa Card",
  "isDefault": true
}
```

**Legacy Card Entry Request:**
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
    "storedPaymentToken": "store_payment_abc123def456",
    "type": "card",
    "last4": "0016",
    "brand": "Visa",
    "expiry": "12/2028",
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
    "timestamp": "2024-09-08T14:00:00.000Z",
    "gatewayResponse": {
      "authCode": "123456",
      "referenceNumber": "ref_789012345"
    },
    "paymentMethod": {
      "id": "pm_123456789",
      "type": "card",
      "brand": "Visa",
      "last4": "0016",
      "nickname": "Test Visa Card"
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
- **Simulated Responses**: Generates realistic transaction data with proper field formatting
- **Test Scenarios**: Different card numbers produce different response scenarios (success, declines, errors)
- **Safe Testing**: No actual charges or API calls are made
- **Development**: Perfect for development and integration testing

### Mock Response Scenarios
- **Success Cards**: Last 4 digits 1111, 4242, 0000
- **Decline Cards**: Last 4 digits 0002 (insufficient funds), 0004 (generic decline), 0051 (expired)
- **Error Cards**: Last 4 digits 0091 (processing error), 0096 (system error)

## Built-in Test Cards

The system includes Global Payments test cards:
- **Visa**: 4012002000060016
- **MasterCard**: 2223000010005780, 5473500000000014
- **Discover**: 6011000990156527  
- **American Express**: 372700699251018
- **JCB**: 3566007770007321

All test cards use:
- **Expiry**: 12/2028
- **CVV**: 123 (1234 for Amex)

## Implementation Details

### Express.js Architecture
- **Modern Express.js**: RESTful API with modular routing and middleware
- **Async/Await**: Modern JavaScript with proper error handling
- **CORS Support**: Cross-Origin Resource Sharing for frontend integration
- **JSON Parsing**: Built-in JSON body parsing for API requests
- **Static Files**: Serves web interface from root directory

### SDK Configuration
- Uses Global Payments Node.js SDK with proper configuration
- Loads credentials from .env file using dotenv package
- Configures service URLs and developer identification
- Handles both live and certification environments
- Implements proper SDK error handling and response processing

### Payment Processing
1. **Multi-Use Tokenization**: Convert single-use tokens to multi-use stored payment tokens with customer data using Global Payments SDK
2. **Customer Integration**: Associate customer billing information with payment methods for enhanced context
3. **Storage**: Store enhanced payment method metadata with customer context in JSON format with atomic file operations
4. **Processing**: Use multi-use stored payment tokens for immediate payment charges
5. **Error Handling**: Comprehensive error handling with meaningful HTTP status codes
6. **Logging**: Console logging for development and debugging

### Data Storage
- JSON file-based storage using Node.js fs module with atomic operations
- Thread-safe file operations with proper locking mechanisms
- Automatic backup and recovery capabilities
- Easy migration path to database systems (MongoDB, PostgreSQL, etc.)

### Security Features
- Environment variable configuration management for API keys and sensitive data
- CORS protection for API endpoints including multi-use token creation
- Input validation and sanitization for both payment and customer data
- Tokenization ensures sensitive payment data never touches your servers
- Customer data protection with proper validation and secure storage
- Proper error handling without exposing sensitive payment or customer information

## Multi-Use Token Implementation

The Node.js implementation creates enhanced stored payment tokens that combine Global Payments tokenization with customer data management:

### Key Features

- **Enhanced Multi-Use Tokens**: Converts single-use payment tokens into multi-use stored payment tokens
- **Customer Data Integration**: Associates customer billing information with payment methods
- **Async Processing**: Non-blocking async/await patterns for efficient multi-use token creation
- **Dynamic Typing**: Flexible JavaScript objects for customer data with validation

### Token Creation Process

```javascript
// Customer data structure for multi-use tokens
const customerData = {
    first_name: 'Jane',
    last_name: 'Doe',
    email: 'jane@example.com',
    phone: '5551234567',
    street_address: '123 Main St',
    city: 'Anytown',
    state: 'NY',
    billing_zip: '12345',
    country: 'USA'
};

// Create multi-use token with customer data
async function createMultiUseTokenWithCustomer(singleUseToken, customerData) {
    const customer = new Customer();
    customer.firstName = customerData.first_name;
    customer.lastName = customerData.last_name;
    customer.email = customerData.email;
    customer.homePhone = customerData.phone;

    const address = new Address();
    address.streetAddress1 = customerData.street_address;
    address.city = customerData.city;
    address.state = customerData.state;
    address.postalCode = customerData.billing_zip;
    address.country = customerData.country;
    customer.address = address;

    // Create multi-use token with customer context
    return await customer.create();
}
```

### Implementation Benefits

- **Async/Await**: Modern JavaScript patterns for non-blocking token creation
- **JSON Native**: Seamless handling of customer data with JavaScript objects
- **Event-Driven**: Node.js event loop for efficient concurrent processing
- **Memory Efficient**: V8 engine optimization for customer data management

## Production Considerations

For production deployment, enhance with:
- **Database Integration** - Replace JSON storage with MongoDB, PostgreSQL, or Redis for customer data
- **Authentication** - Add JWT or session-based authentication with passport.js
- **Rate Limiting** - Implement API rate limiting with express-rate-limit for token endpoints
- **Monitoring** - Add structured logging with Winston and APM tools like New Relic
- **Security** - Implement helmet.js for security headers and customer data validation
- **Process Management** - Use PM2 with cluster mode for production scaling
- **Load Balancing** - Configure nginx with sticky sessions for customer data consistency
- **PCI Compliance** - Follow PCI DSS requirements for payment and customer data processing
- **Testing** - Comprehensive unit and integration tests with Jest, including customer data scenarios
- **Environment Management** - Use proper environment configuration for different stages

## Development

### Running in Development
```bash
npm run dev  # If you have nodemon configured
# or
node server.js
```

### Available Scripts
```bash
npm start       # Start the server
npm test        # Run tests (if configured)
npm run lint    # Run ESLint (if configured)
```

### Package Dependencies
- **express** - Web framework for Node.js
- **dotenv** - Environment variable management
- **cors** - Cross-Origin Resource Sharing middleware
- **globalpayments-api** - Global Payments SDK for Node.js

## Troubleshooting

### Common Issues

**Node.js Version Issues**:
- Ensure Node.js 18.x or later is installed
- Check version with `node --version`
- Update npm with `npm install -g npm@latest`

**Dependency Installation**:
- Clear npm cache: `npm cache clean --force`
- Delete node_modules and reinstall: `rm -rf node_modules && npm install`
- Check for package-lock.json conflicts
- Verify npm registry access: `npm config get registry`
- Update npm: `npm install -g npm@latest`

**Build and Runtime Issues**:
- Check for port conflicts (default port 3000)
- Verify Node.js version compatibility with dependencies
- Check for conflicting global packages: `npm list -g --depth=0`
- Resolve peer dependency warnings: `npm install --legacy-peer-deps`

**Environment Variables**:
- Ensure .env file exists and is properly formatted
- Verify SECRET_API_KEY and PUBLIC_API_KEY are set
- Check file permissions on .env file (should not be world-readable)
- Validate environment variable loading with `console.log(process.env.SECRET_API_KEY)`

**API Configuration**:
- Ensure SECRET_API_KEY is set in .env file
- Verify API key is for the correct environment (test/production)
- Check console output for SDK configuration errors

**Payment Processing**:
- Use test cards in certification environment
- Enable mock mode for development testing
- Check server console logs for detailed error information
- Verify proper JSON formatting in API requests

**File System Permissions**:
- Ensure write permissions for data storage directory
- Check that JSON storage files can be created and modified
- Verify proper file paths in storage operations

**CORS Issues**:
- Check browser developer console for CORS errors
- Verify CORS configuration in server.js
- Ensure proper headers are set for API responses
- Check CORS middleware installation: `npm list cors`
- Verify OPTIONS preflight requests are handled correctly

**Express.js Issues**:
- Check for middleware conflicts or ordering issues
- Verify Express static file serving configuration
- Ensure body-parser middleware is properly configured
- Check for route conflicts or incorrect HTTP methods

**Performance and Memory**:
- Monitor Node.js memory usage for large deployments
- Check for memory leaks with `--inspect` flag
- Optimize JSON parsing for large payloads
- Consider clustering for production deployments

### Debug Mode
Enable debug logging by setting:
```bash
DEBUG=* node server.js
```

Or for specific modules:
```bash
DEBUG=express:* node server.js
```

---

## Resources

- [Parent Project README](../README.md)
- [Global Payments Developer Portal](https://developer.globalpayments.com/)
- [API Reference](https://developer.globalpayments.com/api/references-overview)
- [Node.js SDK](https://github.com/globalpayments/node-sdk)
- [Test Cards](https://developer.globalpayments.com/resources/test-cards)
