# Java Vault One-Click Payment System

This example demonstrates a comprehensive vault one-click payment system using Jakarta EE and the Global Payments SDK. It includes payment method management, secure tokenization, mock testing capabilities, and a complete web interface.

## Features

- **Payment Method Management** - Store, retrieve, and manage customer payment methods securely
- **Vault Tokenization** - Securely tokenize and store payment methods using Global Payments vault
- **One-Click Payments** - Process charges and scheduled payments using stored payment methods
- **Mock Mode** - Test payment flows with simulated responses without hitting live APIs
- **Comprehensive UI** - Complete web interface with payment method management and transaction processing
- **Test Card Integration** - Built-in Heartland certification test cards for development and testing

## Requirements

- Java 11 or later
- Maven 3.6 or later
- Global Payments account and API credentials

## Project Structure

- `src/main/java/com/globalpayments/example/`:
  - `HealthServlet.java` - System health check endpoint
  - `PaymentMethodsServlet.java` - Payment method CRUD operations
  - `ChargeServlet.java` - Payment processing ($25 charges)
  - `SchedulePaymentServlet.java` - Payment scheduling ($50 authorizations)
  - `MockModeServlet.java` - Mock mode toggle functionality
  - `PaymentUtils.java` - Payment utility functions and SDK integration
  - `JsonStorage.java` - JSON-based storage for payment methods
  - `MockResponses.java` - Mock data generation for testing scenarios
- `src/main/webapp/index.html` - Complete web interface with payment management
- `pom.xml` - Maven dependencies and build configuration with Tomcat plugin
- `.env.sample` - Template for environment variables
- `run.sh` - Convenience script to run the application

## Recent Improvements

### ✅ Transaction ID Field Naming Fix (September 2024)
Fixed a critical issue where Transaction ID was showing as "undefined" in the frontend. The system now correctly:
- Returns `transactionId` (camelCase) instead of `transaction_id` (snake_case) in Live Mode responses
- Maintains consistent field naming between Live Mode and Mock Mode responses
- Ensures all response fields use camelCase formatting for frontend compatibility
- Provides proper `authorizationId` field for scheduled payments

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
   mvn clean install
   ```
5. Run the application:
   ```bash
   ./run.sh
   ```
   Or manually:
   ```bash
   mvn cargo:run
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
    "timestamp": "2024-09-08T14:00:00",
    "service": "vault-one-click-java",
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
    "transactionId": "637041702",
    "amount": 25.00,
    "currency": "USD",
    "status": "approved",
    "responseCode": "00",
    "responseMessage": "APPROVAL",
    "timestamp": "2024-09-08T14:00:00",
    "gatewayResponse": {
      "authCode": "31398A",
      "referenceNumber": "525111681208"
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
    "transactionId": "637041723",
    "amount": 50.00,
    "currency": "USD",
    "status": "authorized",
    "expiresAt": "2024-09-15T14:00:00",
    "responseCode": "00",
    "responseMessage": "APPROVAL",
    "gatewayResponse": {
      "authCode": "31400A",
      "referenceNumber": "525111680899"
    },
    "captureInfo": {
      "canCapture": true,
      "expiresAt": "2024-09-15T14:00:00"
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

The system includes Heartland certification test cards:
- **Visa**: 4012002000060016
- **MasterCard**: 2223000010005780, 5473500000000014
- **Discover**: 6011000990156527  
- **American Express**: 372700699251018
- **JCB**: 3566007770007321

All test cards use:
- **Expiry**: 12/2028
- **CVV**: 123 (1234 for Amex)

## Implementation Details

### Servlet Architecture
- **Jakarta EE**: Modern servlet-based architecture using Jakarta EE 9+
- **Maven Cargo**: Embedded Tomcat server for easy development and deployment
- **Modular Design**: Separate servlets for different API endpoints
- **Thread Safety**: Concurrent request handling with thread-safe storage

### SDK Configuration
- Uses PorticoConfig for Global Payments SDK setup
- Loads credentials from .env file using dotenv-java library
- Configures service URLs and developer identification
- Handles both live and certification environments

### Payment Processing
1. **Tokenization**: Create secure vault tokens for payment methods using SDK
2. **Storage**: Store payment method metadata in JSON format with thread-safe operations
3. **Processing**: Use vault tokens for charges and authorizations
4. **Error Handling**: Comprehensive error handling with meaningful HTTP status codes

### Data Storage
- JSON file-based storage for payment methods using JsonStorage utility
- Thread-safe operations for concurrent servlet access
- Automatic file locking and recovery capabilities
- Easy migration path to database systems

### Field Naming Consistency
- **Live Mode**: All response fields use camelCase formatting (transactionId, authorizationId, etc.)
- **Mock Mode**: Consistent camelCase field naming across all responses
- **Frontend Compatibility**: Ensures seamless integration with JavaScript frontend
- **API Standards**: Follows modern REST API naming conventions

## Production Considerations

For production deployment, enhance with:
- **Database Integration** - Replace JSON storage with JPA/Hibernate and proper database
- **Authentication** - Add JWT or session-based authentication
- **Connection Pooling** - Configure database connection pooling
- **Caching** - Implement caching for frequently accessed data
- **Rate Limiting** - Implement servlet filters for API rate limiting
- **Monitoring** - Add logging with SLF4J and monitoring capabilities
- **Security** - Implement additional security filters and validation
- **Container Deployment** - Deploy to production servlet containers (Tomcat, Jetty)
- **PCI Compliance** - Follow PCI DSS requirements for payment processing
- **Testing** - Comprehensive unit and integration tests with JUnit

## Build and Deployment

### Development
```bash
mvn clean compile
mvn cargo:run
```

### Production Build
```bash
mvn clean package
# Deploys ROOT.war to target/
```

### Docker Support
```bash
# Build with Maven
mvn clean package
# Deploy war file to your servlet container
```

## Troubleshooting

### Common Issues

**Transaction ID Shows "undefined" (Fixed)**:
- Issue: Frontend displaying "undefined" for Transaction ID
- Solution: Field naming consistency implemented (camelCase)
- Status: ✅ Resolved in recent update

**Maven Build Issues**:
- Ensure Java 11+ is installed and JAVA_HOME is set
- Run `mvn clean install` to resolve dependency issues
- Check internet connection for Maven Central downloads

**Port Conflicts**:
- Default port is 8000, modify `pom.xml` if needed:
  ```xml
  <cargo.servlet.port>8080</cargo.servlet.port>
  ```

**API Configuration**:
- Ensure SECRET_API_KEY is set in .env file
- Verify API key is for the correct environment
- Check servlet initialization logs for SDK configuration errors

**Payment Processing**:
- Use test cards in certification environment
- Enable mock mode for development testing
- Check server logs for detailed error information and stack traces