# Go Vault One-Click Payment System

This example demonstrates a comprehensive vault one-click payment system using Go and the Global Payments SDK. It includes payment method management, secure tokenization, mock testing capabilities, and a complete web interface.

## Features

- **Payment Method Management** - Store, retrieve, and manage customer payment methods securely
- **Vault Tokenization** - Securely tokenize and store payment methods using Global Payments vault
- **Multi-Use Token Creation** - Convert single-use tokens to multi-use vault tokens with customer data
- **One-Click Payments** - Process charges using stored multi-use payment methods
- **Mock Mode** - Test payment flows with simulated responses without hitting live APIs
- **Comprehensive UI** - Complete web interface with payment method management and transaction processing
- **Test Card Integration** - Built-in Heartland certification test cards for development and testing

## Requirements

- Go 1.21 or later
- Global Payments account and API credentials

## Project Structure

- `main.go` - Main application with all HTTP handlers and payment processing logic
- `handlers.go` - HTTP request handlers for all API endpoints
- `types.go` - Go structs for request/response data models
- `paymentUtils.go` - Payment utility functions and Global Payments SDK integration
- `jsonStorage.go` - JSON-based storage utility for payment methods with file operations
- `mockResponses.go` - Mock data generation for testing scenarios and simulated responses
- `static/index.html` - Complete web interface with payment method management and transaction processing
- `go.mod` - Go module dependencies
- `go.sum` - Go module checksums
- `.env.sample` - Template for environment variables
- `run.sh` - Convenience script to build and run the application

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
   go mod download
   ```
5. Run the application:
   ```bash
   ./run.sh
   ```
   Or manually:
   ```bash
   go run *.go
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
    "service": "vault-one-click-go",
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

### Go Architecture
- **Modern Go**: Uses Go 1.21+ features with proper error handling and concurrency
- **Standard Library**: Built primarily with Go standard library (net/http, encoding/json, etc.)
- **Modular Design**: Separate files for different concerns (handlers, types, utilities)
- **Goroutines**: Efficient concurrent processing for API requests
- **JSON Processing**: Native JSON marshaling/unmarshaling for API responses

### HTTP Server
- **Standard net/http**: Uses Go's built-in HTTP server with custom routing
- **CORS Support**: Cross-Origin Resource Sharing headers for frontend integration
- **Static Files**: Serves web interface from static directory
- **JSON API**: RESTful API endpoints with proper HTTP status codes
- **Request Logging**: Built-in request logging for development and debugging

### SDK Configuration
- Uses Global Payments Go SDK with proper configuration
- Loads credentials from .env file using environment variables
- Configures service URLs and developer identification
- Handles both live and certification environments
- Implements proper SDK error handling and response processing

### Payment Processing
1. **Multi-Use Tokenization**: Convert single-use tokens to multi-use vault tokens with customer data using Global Payments SDK
2. **Customer Integration**: Associate customer billing information with payment methods for enhanced context
3. **Storage**: Store enhanced payment method metadata with customer context in JSON format with atomic file operations
4. **Processing**: Use multi-use vault tokens for immediate payment charges
5. **Error Handling**: Comprehensive error handling with meaningful HTTP status codes
6. **Logging**: Standard Go logging for development and debugging

### Data Storage
- JSON file-based storage using Go's standard file I/O
- Mutex-protected operations for concurrent access safety
- Atomic write operations to prevent data corruption
- Automatic backup and recovery capabilities
- Easy migration path to database systems (PostgreSQL, MySQL, etc.)

### Type Safety
- **Strongly Typed**: Go structs for all request/response data including customer information
- **JSON Tags**: Proper JSON field mapping with Go struct tags for multi-use token fields
- **Validation**: Input validation and sanitization for both payment and customer data
- **Error Types**: Custom error types for different failure scenarios including token creation errors

## Multi-Use Token Implementation

The Go implementation creates enhanced vault tokens that combine Global Payments tokenization with customer data management:

### Key Features

- **Enhanced Vault Tokens**: Converts single-use payment tokens into multi-use vault tokens
- **Customer Data Integration**: Associates customer billing information with payment methods
- **Concurrent Processing**: Efficient goroutine-based handling for multi-use token creation
- **Type Safety**: Strongly-typed Go structs for all token operations

### Token Creation Process

```go
// CustomerData represents the customer information for multi-use tokens
type CustomerData struct {
    FirstName     string `json:"first_name"`
    LastName      string `json:"last_name"`
    Email         string `json:"email"`
    Phone         string `json:"phone"`
    StreetAddress string `json:"street_address"`
    City          string `json:"city"`
    State         string `json:"state"`
    BillingZip    string `json:"billing_zip"`
    Country       string `json:"country"`
}

// CreateMultiUseTokenWithCustomer creates vault token with customer data
func CreateMultiUseTokenWithCustomer(singleUseToken string, customerData CustomerData) (*entities.Customer, error) {
    customer := entities.NewCustomer()
    customer.FirstName = customerData.FirstName
    customer.LastName = customerData.LastName
    customer.Email = customerData.Email
    customer.HomePhone = customerData.Phone

    address := entities.NewAddress()
    address.StreetAddress1 = customerData.StreetAddress
    address.City = customerData.City
    address.State = customerData.State
    address.PostalCode = customerData.BillingZip
    address.Country = customerData.Country
    customer.Address = address

    return customer.Create()
}
```

### Implementation Benefits

- **Concurrent Processing**: Goroutines enable efficient multi-use token creation
- **Memory Efficiency**: Go's efficient memory management for customer data
- **Type Safety**: Compile-time validation prevents runtime errors
- **Performance**: Fast JSON processing and HTTP handling

## Production Considerations

For production deployment, enhance with:
- **Database Integration** - Replace JSON storage with PostgreSQL, MySQL using database/sql or GORM
- **Authentication** - Add JWT or session-based authentication middleware
- **Rate Limiting** - Implement API rate limiting with golang.org/x/time/rate
- **Monitoring** - Add structured logging with logrus and monitoring with Prometheus
- **Security** - Implement additional security middleware and validation
- **TLS/HTTPS** - Configure TLS certificates for production deployment
- **Load Balancing** - Configure nginx or similar for load balancing
- **Container Deployment** - Docker containerization for easy deployment
- **PCI Compliance** - Follow PCI DSS requirements for payment processing
- **Testing** - Comprehensive unit and integration tests with testify
- **Graceful Shutdown** - Implement proper server shutdown handling with context

## Build and Deployment

### Development
```bash
go run *.go
```

### Production Build
```bash
go build -o vault-server *.go
./vault-server
```

### Cross Compilation
```bash
# Linux
GOOS=linux GOARCH=amd64 go build -o vault-server-linux *.go

# Windows
GOOS=windows GOARCH=amd64 go build -o vault-server-windows.exe *.go

# macOS
GOOS=darwin GOARCH=amd64 go build -o vault-server-macos *.go
```

### Docker Support
```dockerfile
FROM golang:1.21-alpine AS builder
WORKDIR /app
COPY . .
RUN go mod download
RUN go build -o vault-server *.go

FROM alpine:latest
WORKDIR /app
COPY --from=builder /app/vault-server .
COPY --from=builder /app/static ./static
EXPOSE 8000
CMD ["./vault-server"]
```

## Module Dependencies

The Go module uses the following key dependencies:
- **Global Payments SDK**: Payment processing and tokenization
- **Environment Variables**: Standard library for .env file processing
- **HTTP Router**: Custom routing with standard library
- **JSON Processing**: Standard library encoding/json package

## Troubleshooting

### Common Issues

**Go Version Issues**:
- Ensure Go 1.21 or later is installed
- Check version with `go version`
- Update Go from https://golang.org/dl/

**Module Dependencies**:
- Run `go mod tidy` to clean up dependencies
- Run `go mod download` to ensure all modules are downloaded
- Check for module proxy issues with `GOPROXY=direct go mod download`

**Build Issues**:
- Ensure all *.go files are in the same package
- Check for import cycle errors
- Verify module path in go.mod

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
- Verify CORS headers in HTTP responses
- Ensure proper preflight request handling

### Debug Mode
Enable verbose logging by setting:
```bash
export DEBUG=true
go run *.go
```

### Performance Tuning
For high-traffic deployments:
```bash
export GOMAXPROCS=4  # Set to number of CPU cores
export GOGC=100      # Adjust garbage collection frequency
```