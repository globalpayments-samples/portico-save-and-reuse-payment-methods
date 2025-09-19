# Vault One-Click Payment System

A PHP-based vault payment system that creates multi-use tokens with customer data for seamless one-click payments using the Global Payments SDK. Features secure payment method storage with integrated customer information and real-time payment processing.

## Requirements

- PHP 7.4 or later
- Composer
- Global Payments account and API credentials (optional - includes mock mode)

## Key Features

- **Multi-Use Tokens with Customer Data**: Creates vault tokens that include customer billing information
- **Integrated Customer Management**: Associates customer details directly with payment tokens
- **One-Click Payment Flow**: Streamlined process from card entry to payment processing
- **Mock Mode Support**: Test functionality without live API credentials
- **Real-Time Processing**: Immediate charges ($25)
- **Customer Data Storage**: Associates customer billing information with vault tokens

## Project Structure

- `index.html` - Complete user interface with add card, payment methods, and payment processing
- `payment-methods.php` - Multi-use token creation with customer data integration
- `PaymentUtils.php` - Core SDK utilities including customer token creation
- `charge.php` - Payment processing endpoint
- `JsonStorage.php` - Payment method and customer data storage
- `config.php` - Frontend SDK configuration
- `health.php` - System status monitoring

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

## How It Works

### 1. Customer Card Entry
Users enter their billing information and payment details through a secure Global Payments tokenization form. The frontend collects:
- Customer details (name, email, phone, billing address)
- Payment card information (handled by Global Payments JS SDK)

### 2. Multi-Use Token Creation
The system creates enhanced vault tokens with integrated customer data:
- Single-use payment tokens are converted to multi-use vault tokens
- Customer billing information is associated with each payment method
- Address and contact data enables enhanced fraud protection and user experience
- Customer context is maintained for regulatory compliance and support

### 3. One-Click Payments
Saved payment methods enable seamless transactions:
- Select from saved payment methods with customer context
- Process immediate charges using stored vault tokens
- Full payment history with customer and transaction details

### 4. Data Flow
```
Frontend Form → Global Payments Tokenization → Multi-Use Token + Customer Data → Vault Storage → One-Click Payments
```

### Key Implementation
- **Frontend**: Collects customer data and handles Global Payments tokenization
- **Backend**: Creates multi-use tokens with `createMultiUseTokenWithCustomer()` method
- **Storage**: JSON-based persistence including customer information with payment methods
- **Processing**: Uses stored vault tokens with associated customer data for payments

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
Create multi-use token with customer data.

Request:
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

Response:
```json
{
  "success": true,
  "data": {
    "id": "pm_123",
    "vaultToken": "multi_use_token_xyz",
    "type": "card",
    "last4": "4242",
    "brand": "Visa",
    "expiry": "12/28",
    "nickname": "My Visa Card",
    "isDefault": true,
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

## Multi-Use Token Implementation

This system creates multi-use tokens that combine Global Payments vault tokens with customer data, enabling secure one-click payments with enhanced customer context.

### Key Features

- **Enhanced Vault Tokens**: Converts single-use payment tokens into multi-use vault tokens
- **Customer Data Integration**: Associates customer billing information with payment methods
- **Secure Storage**: Maintains PCI compliance while storing customer context
- **One-Click Payments**: Enables seamless repeat transactions using stored tokens

### Token Creation Process

1. **Frontend Collection**: Customer enters payment and billing details
2. **Global Payments Tokenization**: Card data is tokenized by Global Payments JS SDK
3. **Multi-Use Enhancement**: Backend converts single-use token to multi-use with customer data
4. **Vault Storage**: Enhanced token with customer context is stored securely

### Customer Data Integration

The system enhances vault tokens with comprehensive customer information:

```php
// Example of multi-use token creation with customer data
$multiUseToken = PaymentUtils::createMultiUseTokenWithCustomer(
    $singleUseToken,
    [
        'first_name' => 'Jane',
        'last_name' => 'Doe',
        'email' => 'jane@example.com',
        'phone' => '5551234567',
        'street_address' => '123 Main St',
        'city' => 'Anytown',
        'state' => 'NY',
        'billing_zip' => '12345',
        'country' => 'USA'
    ]
);
```

### Implementation Benefits

- **Reduced PCI Scope**: Card data never touches your servers
- **Enhanced UX**: Customers see full context (name, address) with saved cards
- **Fraud Reduction**: Consistent customer data validation
- **Regulatory Compliance**: Maintains data protection standards

### Token Lifecycle

1. **Creation**: Single-use token + customer data → Multi-use vault token
2. **Storage**: Token metadata with customer context stored locally
3. **Usage**: Multi-use token enables repeat payments without re-entry
4. **Management**: Update customer data or payment preferences

### Advanced Use Cases

Multi-use tokens support various payment scenarios:

- **Subscription Billing**: Recurring charges with customer context
- **Express Checkout**: One-click purchases with pre-filled customer data
- **Customer Management**: Update billing addresses without new tokenization
- **Fraud Prevention**: Consistent customer validation across transactions

## Troubleshooting

### Common Issues

**Setup Issues**:
- Ensure PHP 7.4+ is installed: `php --version`
- Run `composer install` to install dependencies
- Set up `.env` file with Global Payments credentials (optional for mock mode)

**Payment Processing**:
- Use Global Payments test cards: 4012002000060016 (Visa), 2223000010005780 (MasterCard)
- Enable mock mode toggle in UI for testing without credentials
- Check browser console for frontend errors
- Verify API responses in Network tab

**Configuration**:
- Ensure `data/` directory has write permissions
- Verify CORS headers are properly set
- Check that Global Payments SDK loads correctly

### Mock Mode Testing
The application includes comprehensive mock mode for testing without live API credentials:
- Toggle mock mode in the UI header
- Test various card scenarios using different test card numbers
- All functionality works identically in mock mode

## Security Considerations

For production use:
- Use HTTPS for all communications
- Implement proper input validation and sanitization
- Add rate limiting and fraud detection
- Enable comprehensive logging and monitoring
- Use environment variables for sensitive configuration
- Implement CSRF protection for forms
- Regular security updates for dependencies
