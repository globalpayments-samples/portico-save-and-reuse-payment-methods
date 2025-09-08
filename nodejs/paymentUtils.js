/**
 * Payment utility functions for Global Payments SDK
 */

import * as dotenv from 'dotenv';
import {
    ServicesContainer,
    PorticoConfig,
    Address,
    CreditCardData,
    ApiError
} from 'globalpayments-api';

// Load environment variables
dotenv.config();

let sdkConfigured = false;

/**
 * Configure the Global Payments SDK
 */
export const configureSdk = () => {
    if (!sdkConfigured) {
        const config = new PorticoConfig();
        config.secretApiKey = process.env.SECRET_API_KEY;
        config.developerId = '000000';
        config.versionNumber = '0000';
        config.serviceUrl = 'https://cert.api2.heartlandportico.com';
        
        ServicesContainer.configureService(config);
        sdkConfigured = true;
    }
};

/**
 * Sanitize postal code by removing invalid characters
 */
export const sanitizePostalCode = (postalCode) => {
    if (!postalCode) return '';
    
    const sanitized = postalCode.replace(/[^a-zA-Z0-9-]/g, '');
    return sanitized.slice(0, 10);
};

/**
 * Determine card brand from card number
 */
export const determineCardBrand = (cardNumber) => {
    const cleanNumber = cardNumber.replace(/\s+/g, '');
    
    if (cleanNumber.match(/^4/)) {
        return 'Visa';
    } else if (cleanNumber.match(/^5[1-5]/) || cleanNumber.match(/^2[2-7]/)) {
        return 'Mastercard';
    } else if (cleanNumber.match(/^3[47]/)) {
        return 'American Express';
    } else if (cleanNumber.match(/^6(?:011|5)/)) {
        return 'Discover';
    } else {
        return 'Unknown';
    }
};

/**
 * Create vault token using Global Payments SDK
 */
export const createVaultTokenWithSDK = async (data) => {
    try {
        const card = new CreditCardData();
        card.number = data.cardNumber;
        card.expMonth = parseInt(data.expiryMonth);
        card.expYear = parseInt(data.expiryYear);
        card.cvn = data.cvv;
        
        if (data.billingAddress) {
            const address = new Address();
            address.streetAddress1 = data.billingAddress.street || '';
            address.city = data.billingAddress.city || '';
            address.state = data.billingAddress.state || '';
            address.postalCode = data.billingAddress.zip || '';
            address.country = data.billingAddress.country || 'US';
            card.cardHolderName = data.billingAddress.name || '';
        }

        const response = await card.tokenize().execute();
        
        if (response.responseCode === '00' && response.token) {
            // Log successful tokenization in live mode
            console.log(`🔑 PAYMENT METHOD CREATION - Attempting tokenization for card ending in ${data.cardNumber.slice(-4)}`);
            console.log(`   📝 Card Brand: ${determineCardBrand(data.cardNumber)}`);
            console.log(`   📅 Expiry: ${data.expiryMonth}/${data.expiryYear}`);
            console.log(`   👤 Nickname: ${data.nickname || 'None'}`);
            console.log(`   ⭐ Set as Default: ${data.isDefault || false}`);
            
            console.log('✅ 🟢 LIVE MODE - Payment Method Created Successfully:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   🔐 Vault Token: ${response.token}`);
            console.log(`   💳 Card Brand: ${determineCardBrand(data.cardNumber)}`);
            console.log(`   🔢 Last 4: ${data.cardNumber.slice(-4)}`);
            console.log(`   📅 Expiry: ${data.expiryMonth}/${data.expiryYear}`);
            console.log(`   📛 Nickname: ${data.nickname || 'None'}`);
            console.log(`   ⭐ Default: ${data.isDefault || false}`);
            console.log('   📡 API Status: Connected & Working');
            
            return response.token;
        } else {
            // Log failed tokenization attempt
            console.log('❌ 🔴 LIVE MODE - Payment Method Creation Failed:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   💳 Card Brand: ${determineCardBrand(data.cardNumber)}`);
            console.log(`   🔢 Last 4: ${data.cardNumber.slice(-4)}`);
            console.log(`   📅 Expiry: ${data.expiryMonth}/${data.expiryYear}`);
            console.log(`   ❌ Error: ${response.responseMessage || 'Unknown error'}`);
            console.log('   📡 API Status: Connected but Declined');
            
            throw new Error(`Tokenization failed: ${response.responseMessage || 'Unknown error'}`);
        }
    } catch (error) {
        console.error('SDK tokenization error:', error.message);
        throw error;
    }
};

/**
 * Process payment using Global Payments SDK
 */
export const processPaymentWithSDK = async (vaultToken, amount, currency) => {
    try {
        const card = new CreditCardData();
        card.token = vaultToken;

        const response = await card.charge(amount)
            .withCurrency(currency)
            .execute();

        if (response.responseCode === '00') {
            // Log successful payment in live mode
            console.log(`💰 PAYMENT PROCESSING - Charging with token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            
            console.log('✅ 🟢 LIVE MODE - Payment Charged Successfully:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   🆔 Transaction ID: ${response.transactionId || 'N/A'}`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            console.log(`   🔐 Vault Token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   📋 Response Code: ${response.responseCode}`);
            console.log(`   💬 Response Message: ${response.responseMessage || 'Approved'}`);
            console.log(`   🔑 Auth Code: ${response.authorizationCode || 'N/A'}`);
            console.log(`   📄 Reference Number: ${response.referenceNumber || 'N/A'}`);
            console.log('   📡 API Status: Connected & Working');
            
            return {
                transaction_id: response.transactionId || `txn_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                amount: amount,
                currency: currency,
                status: 'approved',
                response_code: response.responseCode,
                response_message: response.responseMessage || 'Approved',
                timestamp: new Date().toISOString(),
                gateway_response: {
                    auth_code: response.authorizationCode || '',
                    reference_number: response.referenceNumber || ''
                }
            };
        } else {
            // Log failed payment
            console.log('❌ 🔴 LIVE MODE - Payment Charge Failed:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            console.log(`   🔐 Vault Token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   📋 Response Code: ${response.responseCode}`);
            console.log(`   ❌ Error: ${response.responseMessage || 'Unknown error'}`);
            console.log('   📡 API Status: Connected but Declined');
            
            throw new Error(`Payment failed: ${response.responseMessage || 'Unknown error'}`);
        }
    } catch (error) {
        console.error('SDK payment processing error:', error.message);
        throw error;
    }
};

/**
 * Create authorization using Global Payments SDK
 */
export const createAuthorizationWithSDK = async (vaultToken, amount, currency) => {
    try {
        const card = new CreditCardData();
        card.token = vaultToken;

        const response = await card.authorize(amount)
            .withCurrency(currency)
            .execute();

        if (response.responseCode === '00') {
            const expiresAt = new Date();
            expiresAt.setDate(expiresAt.getDate() + 7);
            
            // Log successful authorization in live mode
            console.log(`⏰ PAYMENT SCHEDULING - Creating authorization with token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            
            console.log('✅ 🟢 LIVE MODE - Payment Authorization Created Successfully:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   🆔 Transaction ID: ${response.transactionId || 'N/A'}`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            console.log(`   🔐 Vault Token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   📋 Response Code: ${response.responseCode}`);
            console.log(`   💬 Response Message: ${response.responseMessage || 'Authorized'}`);
            console.log(`   🔑 Auth Code: ${response.authorizationCode || 'N/A'}`);
            console.log(`   📄 Reference Number: ${response.referenceNumber || 'N/A'}`);
            console.log(`   ⏰ Expires: ${expiresAt.toISOString()}`);
            console.log('   📡 API Status: Connected & Working');
            
            return {
                authorization_id: `auth_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                transaction_id: response.transactionId || `txn_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                amount: amount,
                currency: currency,
                status: 'authorized',
                response_code: response.responseCode,
                response_message: response.responseMessage || 'Authorized',
                timestamp: new Date().toISOString(),
                expires_at: expiresAt.toISOString(),
                gateway_response: {
                    auth_code: response.authorizationCode || '',
                    reference_number: response.referenceNumber || ''
                }
            };
        } else {
            // Log failed authorization
            console.log('❌ 🔴 LIVE MODE - Payment Authorization Failed:');
            console.log(`   ⏰ Timestamp: ${new Date().toISOString()}`);
            console.log(`   💵 Amount: $${amount.toFixed(2)} ${currency}`);
            console.log(`   🔐 Vault Token: ${vaultToken.substring(0, 8)}...`);
            console.log(`   📋 Response Code: ${response.responseCode}`);
            console.log(`   ❌ Error: ${response.responseMessage || 'Unknown error'}`);
            console.log('   📡 API Status: Connected but Declined');
            
            throw new Error(`Authorization failed: ${response.responseMessage || 'Unknown error'}`);
        }
    } catch (error) {
        console.error('SDK authorization error:', error.message);
        throw error;
    }
};

/**
 * Send success response
 */
export const sendSuccessResponse = (res, data, message = 'Operation completed successfully') => {
    const response = {
        success: true,
        data: data,
        message: message,
        timestamp: new Date().toISOString()
    };
    
    res.json(response);
};

/**
 * Send error response
 */
export const sendErrorResponse = (res, statusCode, message, errorCode = null) => {
    const response = {
        success: false,
        message: message,
        timestamp: new Date().toISOString()
    };
    
    if (errorCode) {
        response.error_code = errorCode;
    }
    
    res.status(statusCode).json(response);
};

/**
 * Handle CORS headers
 */
export const handleCORS = (req, res, next) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    
    if (req.method === 'OPTIONS') {
        res.status(200).end();
        return;
    }
    
    next();
};