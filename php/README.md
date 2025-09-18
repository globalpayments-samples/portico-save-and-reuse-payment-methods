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
The system creates vault tokens that include customer data:
- Payment token from Global Payments is enhanced with customer billing information
- Address data is attached to the multi-use token for future use
- Customer information is stored alongside the payment method for easy retrieval

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

## Scheduled Payments Implementation

This sample project focuses on immediate payment processing using vault tokens. For developers who need scheduled payment functionality, the Global Payments SDK's PayPlan feature requires additional setup and architecture that is beyond the scope of this demonstration.

### Alternative Scheduling Approaches

For production applications requiring scheduled payments, consider implementing your own scheduling layer using one of these approaches:

#### 1. Framework-Based Schedulers

**Laravel Queue Jobs & Scheduler**
```php
// Schedule recurring payments
// In your Laravel app
Schedule::call(function () {
    ProcessRecurringPayments::dispatch();
})->daily();

// Queue job to process payments
class ProcessRecurringPayments implements ShouldQueue {
    public function handle() {
        $duePayments = PaymentSchedule::where('next_payment_date', '<=', now())->get();
        foreach ($duePayments as $payment) {
            // Use stored vault token to process payment
            PaymentUtils::processPaymentWithSDK($payment->vault_token, $payment->amount, 'USD');
        }
    }
}
```

#### 2. Server-Level Scheduling

**Cron Jobs**
```bash
# Schedule daily payment processing
0 9 * * * php /path/to/your/app/process-payments.php

# Weekly payment processing
0 9 * * 1 php /path/to/your/app/weekly-payments.php
```

**System-level scheduling**
```php
// process-payments.php
<?php
require_once 'PaymentUtils.php';

$scheduledPayments = getScheduledPaymentsForToday(); // Your implementation
foreach ($scheduledPayments as $payment) {
    try {
        $result = PaymentUtils::processPaymentWithSDK(
            $payment['vault_token'],
            $payment['amount'],
            $payment['currency']
        );
        updatePaymentStatus($payment['id'], 'completed', $result);
    } catch (Exception $e) {
        updatePaymentStatus($payment['id'], 'failed', ['error' => $e->getMessage()]);
    }
}
?>
```

#### 3. Queue-Based Systems

**Redis Queue with PHP**
```php
// Enqueue payment for future processing
$redis = new Redis();
$redis->zadd('scheduled_payments', time() + $delay, json_encode([
    'vault_token' => $vaultToken,
    'amount' => $amount,
    'customer_data' => $customerData
]));

// Worker process
while (true) {
    $payments = $redis->zrangebyscore('scheduled_payments', 0, time(), ['limit' => [0, 10]]);
    foreach ($payments as $paymentData) {
        $payment = json_decode($paymentData, true);
        // Process payment using vault token
        processScheduledPayment($payment);
        $redis->zrem('scheduled_payments', $paymentData);
    }
    sleep(60); // Check every minute
}
```

#### 4. Cloud-Based Scheduling

**AWS EventBridge**
```php
// Schedule payment event
$eventBridge = new Aws\EventBridge\EventBridgeClient([...]);
$eventBridge->putEvents([
    'Entries' => [[
        'Source' => 'payment.scheduler',
        'DetailType' => 'Process Payment',
        'Detail' => json_encode([
            'vault_token' => $vaultToken,
            'amount' => $amount,
            'schedule_date' => '2024-02-01T09:00:00Z'
        ]),
        'ScheduleExpression' => 'at(2024-02-01T09:00:00)'
    ]]
]);
```

**Google Cloud Scheduler**
```bash
# Create scheduled job
gcloud scheduler jobs create http payment-processor \
    --schedule="0 9 * * *" \
    --uri="https://your-app.com/process-payments" \
    --http-method=POST \
    --headers="Content-Type=application/json" \
    --message-body='{"action":"process_scheduled_payments"}'
```

### Implementation Considerations

When implementing scheduled payments:

1. **Token Persistence**: Vault tokens remain valid for extended periods, making them suitable for scheduled payments
2. **Error Handling**: Implement retry logic for failed payments with exponential backoff
3. **Notification Systems**: Alert customers about upcoming and completed payments
4. **Compliance**: Consider PCI compliance requirements for storing payment schedules
5. **Monitoring**: Log all scheduled payment attempts and results for auditing

### Recommended Architecture

```
Customer Data + Payment Schedule → Database Storage
                                 ↓
Scheduler Service (Cron/Queue) → Check Due Payments
                                 ↓
Payment Processor → Use Vault Token → Global Payments API
                                 ↓
Results Handler → Update Database + Notify Customer
```

The vault tokens created by this sample application are fully compatible with any of these scheduling approaches, providing a solid foundation for building sophisticated payment scheduling systems.

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
