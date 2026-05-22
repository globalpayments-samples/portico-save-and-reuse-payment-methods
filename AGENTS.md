# Portico Save and Reuse Payment Methods

> Tokenize a card via Portico, promote the single-use token to a multi-use stored token, and charge it on demand — demonstrated in PHP, Node.js, .NET, Java, and Go.

## Critical Patterns

1. **`withRequestMultiUseToken(true)` on `verify()` is the step that produces a reusable stored token.** The frontend (`globalpayments.js`) issues a short-lived single-use token. The backend calls `card.verify().withCurrency("USD").withRequestMultiUseToken(true).execute()` — the response carries a durable multi-use token, which is what gets persisted and later used for `charge()`. Storing the original single-use token instead means subsequent charge calls against it will fail once that token expires. Go currently calls `card.Tokenize()` rather than `verify().withRequestMultiUseToken(true)` — a divergence from the other four languages worth knowing if you're adapting the code.

2. **Node.js, .NET, and Go include `withAllowDuplicates(true)` on charge calls; PHP and Java do not.** Portico returns a duplicate-transaction error when the same card and amount are submitted in quick succession. Node.js (`processPayment` in `server.js`), .NET (`ProcessPayment`), and Go (`processPayment`) all set this flag. PHP (`processPaymentWithSDK`) and Java (`processPaymentWithSDK`) omit it — repeated test charges through those implementations will be declined. When extending or porting the charge logic in PHP or Java, add `.withAllowDuplicates(true)` to the charge call.

3. **Mock mode falls back silently when live token creation fails.** All five implementations fall back to mock mode if `withRequestMultiUseToken(true)` fails (bad credentials, SDK error) and store the original single-use token instead. Toggling mock mode off later will send those stored single-use tokens to live charge attempts, which will fail. After recovering from a fallback, re-add the payment method in live mode to get a real multi-use token.

4. **`POST /payment-methods` handles both create and edit in one endpoint.** If the request body includes an `id` field, the endpoint updates an existing record (nickname and default status only). Without `id`, it creates a new stored payment method from a fresh `paymentToken`. An agent modifying this endpoint must preserve both branches.

## Repository Structure

### PHP (native PHP + Global Payments SDK)
- [`php/PaymentUtils.php`](php/PaymentUtils.php) — `configureSdk`, `createMultiUseTokenWithCustomer`, `processPaymentWithSDK`
- [`php/payment-methods.php`](php/payment-methods.php) — POST handler for create/edit stored payment methods
- [`php/charge.php`](php/charge.php) — POST `/charge`; charges $25 against a stored token
- [`php/config.php`](php/config.php) — GET `/config`; returns `PUBLIC_API_KEY`
- [`php/router.php`](php/router.php) — routes all API paths to individual PHP handlers
- [`php/JsonStorage.php`](php/JsonStorage.php) — file-based JSON persistence for payment methods
- [`php/composer.json`](php/composer.json) — `globalpayments/php-sdk` ^13.1

### Node.js (Express + Global Payments SDK)
- [`nodejs/server.js`](nodejs/server.js) — all routes; `configureGlobalPaymentsSDK`, `handleCreatePaymentMethod`, `handleEditPaymentMethod`, `processPayment`
- [`nodejs/paymentUtils.js`](nodejs/paymentUtils.js) — `createMultiUseTokenWithCustomer`, `getCardDetailsFromToken`
- [`nodejs/jsonStorage.js`](nodejs/jsonStorage.js) — JSON-based storage helpers
- [`nodejs/package.json`](nodejs/package.json) — `globalpayments-api` ^3.10.6

### .NET (ASP.NET Core minimal API + Global Payments SDK)
- [`dotnet/Program.cs`](dotnet/Program.cs) — all endpoints; `ConfigureGlobalPaymentsSDK`, `HandleCreatePaymentMethodMultiUse`, `HandleEditPaymentMethodPhpStyle`, `ProcessPayment`
- [`dotnet/PaymentUtils.cs`](dotnet/PaymentUtils.cs) — `CreateMultiUseTokenWithCustomerAsync`, `DetermineCardBrandFromType`
- [`dotnet/JsonStorage.cs`](dotnet/JsonStorage.cs) — async JSON file storage
- [`dotnet/dotnet.csproj`](dotnet/dotnet.csproj) — `GlobalPayments.Api` 9.0.16

### Java (Jakarta EE servlet + Global Payments SDK)
- [`java/src/main/java/com/globalpayments/example/PaymentUtils.java`](java/src/main/java/com/globalpayments/example/PaymentUtils.java) — `configureSdk`, `createMultiUseTokenWithCustomer`, `processPaymentWithSDK`
- [`java/src/main/java/com/globalpayments/example/PaymentMethodsServlet.java`](java/src/main/java/com/globalpayments/example/PaymentMethodsServlet.java) — `@WebServlet("/payment-methods")`
- [`java/src/main/java/com/globalpayments/example/ChargeServlet.java`](java/src/main/java/com/globalpayments/example/ChargeServlet.java) — `@WebServlet("/charge")`
- [`java/src/main/java/com/globalpayments/example/ProcessPaymentServlet.java`](java/src/main/java/com/globalpayments/example/ProcessPaymentServlet.java) — legacy servlet (`/process-payment` + `/config`) using form-post, not JSON; pre-dates the saved-token flow
- [`java/pom.xml`](java/pom.xml) — `globalpayments-sdk` 14.2.20

### Go (net/http + Global Payments SDK)
- [`go/main.go`](go/main.go) — router setup, `main`, port via `getEnv("PORT", "8000")`
- [`go/handlers.go`](go/handlers.go) — `healthHandler`, `configHandler`, `mockModeHandler`, `paymentMethodsHandler`, `chargeHandler`
- [`go/paymentUtils.go`](go/paymentUtils.go) — `createMultiUseTokenWithCustomer` (uses `card.Tokenize()` — see Critical Pattern 1), `createPaymentMethodFromToken`, `processPayment`
- [`go/jsonStorage.go`](go/jsonStorage.go) — file-based JSON persistence
- [`go/go.mod`](go/go.mod) — `github.com/globalpayments/go-sdk` v1.1.3

### Shared
- [`index.html`](index.html) — root-level frontend; each language dir also has its own copy
- [`docker-compose.yml`](docker-compose.yml) — multi-service; maps each language to a unique host port; **note: references a `python` service, but no `python/` directory exists in the repo**

## API Surface

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | System status and mock mode indicator |
| GET | `/config` | Returns `publicApiKey` for frontend `globalpayments.js` init |
| GET | `/payment-methods` | List all stored payment methods |
| POST | `/payment-methods` | Create stored payment method (or edit existing when `id` is present) |
| POST | `/charge` | Process $25 charge against a stored payment method by `paymentMethodId` |
| GET | `/mock-mode` | Get current mock mode status |
| POST | `/mock-mode` | Toggle mock mode (`{ "isEnabled": true/false }`) |

All five implementations expose identical endpoints and identical JSON response shapes.

## Environment Variables

```bash
PUBLIC_API_KEY=pkapi_cert_...   # Portico public key — returned to frontend for globalpayments.js
SECRET_API_KEY=skapi_cert_...   # Portico secret key — SDK config only, never sent to client
PORT=8000                       # Optional; all implementations default to 8000
```

Each language directory has a `.env.sample` — copy it to `.env` before running.

## Sandbox Credentials

| Brand | Number | CVV | Expiry |
|-------|--------|-----|--------|
| Visa | 4012002000060016 | 123 | Any future date |
| Mastercard | 5473500000000014 | 123 | Any future date |

Get sandbox credentials at [developer.globalpayments.com](https://developer.globalpayments.com).

## Architecture Summary

**Add payment method:** `globalpayments.js` hosted fields → single-use token → `POST /payment-methods` → `card.verify().withRequestMultiUseToken(true)` → multi-use token → persisted to JSON file

**Charge:** `POST /charge` with `paymentMethodId` → load stored token → `card.charge(25).withCurrency("USD").withAllowDuplicates(true)` → transaction result

## Security Notes

API credentials are read from `.env` files — never commit them. JSON file storage is a demo convenience; replace with an encrypted database in production. No auth middleware protects any endpoint; add authentication before deploying. The `/mock-mode` toggle is unauthenticated — gate or remove it in production.

## How to Run

```bash
cd php && ./run.sh       # PHP — :8000
cd nodejs && ./run.sh    # Node.js — :8000
cd dotnet && ./run.sh    # .NET — :8000
cd java && ./run.sh      # Java — :8000
cd go && ./run.sh        # Go — :8000
# All at once:
docker-compose up
```

## How to Verify

```bash
# Config endpoint
curl http://localhost:8000/config
# Expected: {"success":true,"data":{"publicApiKey":"pkapi_cert_...","mockMode":false},...}

# List stored payment methods (empty initially)
curl http://localhost:8000/payment-methods
# Expected: {"success":true,"data":[],...}

# Charge a stored payment method (replace with an ID from POST /payment-methods)
curl -X POST http://localhost:8000/charge \
  -H "Content-Type: application/json" \
  -d '{"paymentMethodId":"pm_abc123"}'
# Expected: {"success":true,"data":{"transactionId":"...","amount":25,"status":"approved",...}}

# Toggle mock mode on
curl -X POST http://localhost:8000/mock-mode \
  -H "Content-Type: application/json" \
  -d '{"isEnabled":true}'
# Expected: {"success":true,"data":{"isEnabled":true},...}
```

`POST /payment-methods` requires a real single-use token generated by the `globalpayments.js` hosted fields form — it cannot be fully tested with curl alone. Use mock mode (`POST /mock-mode` with `isEnabled: true`) to bypass live tokenization during development.

## Making Changes

All language implementations expose identical behavior. A change to one must be applied to all — each language in a separate commit. Do not modify shared files (`index.html`, `docker-compose.yml`) without confirming the change applies to every implementation.

## SDK Versions

- **PHP**: `globalpayments/php-sdk` ^13.1
- **Node.js**: `globalpayments-api` ^3.10.6
- **.NET**: `GlobalPayments.Api` 9.0.16
- **Java**: `globalpayments-sdk` 14.2.20
- **Go**: `github.com/globalpayments/go-sdk` v1.1.3
