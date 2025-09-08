/**
 * Vault One-Click Payment Processing Server - Node.js Implementation
 * 
 * Complete REST API implementation with payment method management,
 * mock mode support, and comprehensive error handling.
 */

import express from 'express';
import * as dotenv from 'dotenv';
import {
    ServicesContainer,
    PorticoConfig,
    Address,
    CreditCardData,
    ApiError
} from 'globalpayments-api';

// Import our custom modules
import * as jsonStorage from './jsonStorage.js';
import * as paymentUtils from './paymentUtils.js';
import * as mockResponses from './mockResponses.js';

// Load environment variables from .env file
dotenv.config();

// Initialize Express application
const app = express();
const port = process.env.PORT || 8000;

// Global mock mode state
let mockModeEnabled = false;

// Configure middleware
app.use(express.static('.')); // Serve static files
app.use(express.urlencoded({ extended: true })); // Parse form data
app.use(express.json()); // Parse JSON requests

// CORS middleware
app.use((req, res, next) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept, Authorization');
    
    if (req.method === 'OPTIONS') {
        res.sendStatus(200);
    } else {
        next();
    }
});

// Configure Global Payments SDK
function configureGlobalPaymentsSDK() {
    try {
        const config = new PorticoConfig();
        config.secretApiKey = process.env.SECRET_API_KEY;
        config.serviceUrl = 'https://cert.api2.heartlandportico.com';
        ServicesContainer.configureService(config);
        console.log('✅ Global Payments SDK configured');
    } catch (error) {
        console.log('⚠️  SDK configuration failed:', error.message);
    }
}

// Initialize mock mode from storage
async function initializeMockMode() {
    try {
        mockModeEnabled = await jsonStorage.loadMockModeConfig();
    } catch (error) {
        console.log('Warning: Failed to load mock mode config, using default (disabled)');
        mockModeEnabled = false;
    }
}

// Health check endpoint
app.get('/health', (req, res) => {
    const secretKey = process.env.SECRET_API_KEY;
    const sdkStatus = secretKey ? 'configured' : 'not_configured';
    
    res.json({
        success: true,
        data: {
            status: 'healthy',
            timestamp: new Date().toISOString(),
            service: 'vault-one-click-nodejs',
            version: '1.0.0',
            sdkStatus: sdkStatus,
            mockMode: mockModeEnabled
        },
        message: 'System is healthy',
        timestamp: new Date().toISOString()
    });
});

// Configuration endpoint  
app.get('/config', (req, res) => {
    res.json({
        success: true,
        data: {
            publicApiKey: process.env.PUBLIC_API_KEY || 'pk_test_demo_key',
            mockMode: mockModeEnabled
        },
        timestamp: new Date().toISOString()
    });
});

// Mock mode endpoints
app.get('/mock-mode', (req, res) => {
    res.json({
        success: true,
        data: {
            isEnabled: mockModeEnabled
        },
        message: `Mock mode is ${mockModeEnabled ? 'enabled' : 'disabled'}`,
        timestamp: new Date().toISOString()
    });
});

app.post('/mock-mode', async (req, res) => {
    try {
        const { isEnabled } = req.body;
        
        if (typeof isEnabled !== 'boolean') {
            return res.status(400).json({
                success: false,
                message: 'Invalid request body: isEnabled must be boolean',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        const previousState = mockModeEnabled;
        mockModeEnabled = isEnabled;
        
        // Persist mock mode configuration
        try {
            await jsonStorage.saveMockModeConfig(mockModeEnabled);
        } catch (error) {
            console.log('Warning: Failed to save mock mode config:', error.message);
        }

        console.log(`🎭 Mock mode toggled from ${previousState} to ${mockModeEnabled}`);
        
        res.json({
            success: true,
            data: {
                isEnabled: mockModeEnabled
            },
            message: `Mock mode ${mockModeEnabled ? 'enabled' : 'disabled'} successfully`,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        res.status(400).json({
            success: false,
            message: 'Invalid JSON format',
            errorCode: 'VALIDATION_ERROR',
            timestamp: new Date().toISOString()
        });
    }
});

// Payment methods endpoints
app.get('/payment-methods', async (req, res) => {
    try {
        const methods = await jsonStorage.getFormattedPaymentMethods();
        
        res.json({
            success: true,
            data: methods,
            message: 'Payment methods retrieved successfully',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error retrieving payment methods:', error.message);
        res.status(500).json({
            success: false,
            message: 'Failed to retrieve payment methods',
            errorCode: 'SERVER_ERROR',
            timestamp: new Date().toISOString()
        });
    }
});

app.post('/payment-methods', async (req, res) => {
    try {
        const data = req.body;
        
        // Check if this is an edit operation
        if (data.id) {
            return await handleEditPaymentMethod(req, res, data);
        }

        // Validate required fields for new payment method
        if (!data.cardNumber || !data.expiryMonth || !data.expiryYear || !data.cvv) {
            return res.status(400).json({
                success: false,
                message: 'Missing required card details',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        // Create new payment method
        return await handleCreatePaymentMethod(req, res, data);
    } catch (error) {
        console.error('Error in payment methods endpoint:', error.message);
        res.status(500).json({
            success: false,
            message: 'Internal server error',
            errorCode: 'SERVER_ERROR',
            timestamp: new Date().toISOString()
        });
    }
});

// Handle editing an existing payment method
async function handleEditPaymentMethod(req, res, data) {
    try {
        // Validate payment method exists
        const existingMethod = await jsonStorage.findPaymentMethod(data.id);
        if (!existingMethod) {
            return res.status(404).json({
                success: false,
                message: 'Payment method not found',
                errorCode: 'NOT_FOUND',
                timestamp: new Date().toISOString()
            });
        }

        console.log(`✏️ Editing payment method ${data.id}`);
        console.log(`   Card: ${existingMethod.cardBrand} ending in ${existingMethod.last4}`);
        console.log(`   Nickname: ${existingMethod.nickname || 'None'} → ${data.nickname || 'None'}`);
        console.log(`   Default: ${existingMethod.isDefault} → ${data.isDefault}`);

        // Update the payment method
        const updateData = {};
        if (data.nickname !== undefined) updateData.nickname = data.nickname;
        if (data.isDefault !== undefined) updateData.isDefault = data.isDefault;

        const updatedMethod = await jsonStorage.updatePaymentMethod(data.id, updateData);
        if (!updatedMethod) {
            return res.status(500).json({
                success: false,
                message: 'Failed to update payment method',
                errorCode: 'UPDATE_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        console.log('✅ Payment method updated successfully');

        // Format response
        const response = {
            id: updatedMethod.id,
            type: 'card',
            last4: updatedMethod.last4,
            brand: updatedMethod.cardBrand,
            expiry: updatedMethod.expiry,
            nickname: updatedMethod.nickname || '',
            isDefault: updatedMethod.isDefault || false,
            updatedAt: updatedMethod.updatedAt
        };

        res.json({
            success: true,
            data: response,
            message: 'Payment method updated successfully',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error updating payment method:', error.message);
        res.status(500).json({
            success: false,
            message: 'Payment method update failed',
            errorCode: 'SERVER_ERROR',
            timestamp: new Date().toISOString()
        });
    }
}

// Handle creating a new payment method
async function handleCreatePaymentMethod(req, res, data) {
    try {
        // Validate card data
        const cleanCardNumber = data.cardNumber.replace(/\s+/g, '');
        if (cleanCardNumber.length < 13 || cleanCardNumber.length > 19 || !/^\d+$/.test(cleanCardNumber)) {
            return res.status(400).json({
                success: false,
                message: 'Invalid card number format',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        const cardBrand = paymentUtils.determineCardBrand(cleanCardNumber);
        const last4 = cleanCardNumber.substring(cleanCardNumber.length - 4);
        const expiry = `${data.expiryMonth.padStart(2, '0')}/${data.expiryYear}`;

        let vaultToken;
        let isUsingMockMode = mockModeEnabled;

        if (mockModeEnabled) {
            // Use mock token
            vaultToken = `mock_vault_${Date.now()}_${Math.random().toString(36).substring(7)}`;
            console.log(`🟡 Mock mode: Generated mock vault token for ${cardBrand} ending in ${last4}`);
        } else {
            try {
                // Try to create real vault token
                vaultToken = await paymentUtils.createVaultTokenWithSDK(data, cleanCardNumber);
                console.log(`🟢 Live mode: Created vault token for ${cardBrand} ending in ${last4}`);
            } catch (error) {
                console.log(`❌ Live mode failed: ${error.message}`);
                return res.status(422).json({
                    success: false,
                    message: `Payment method creation failed: ${error.message}`,
                    errorCode: 'PAYMENT_ERROR',
                    timestamp: new Date().toISOString()
                });
            }
        }

        // Save payment method
        const storedData = {
            vaultToken: vaultToken,
            cardBrand: cardBrand,
            last4: last4,
            expiry: expiry,
            nickname: data.nickname || `${cardBrand} ending in ${last4}`,
            isDefault: data.isDefault || false
        };

        const savedMethod = await jsonStorage.addPaymentMethod(storedData);

        const response = {
            id: savedMethod.id,
            type: 'card',
            last4: savedMethod.last4,
            brand: savedMethod.cardBrand,
            expiry: savedMethod.expiry,
            nickname: savedMethod.nickname || '',
            isDefault: savedMethod.isDefault || false,
            mockMode: isUsingMockMode
        };

        res.json({
            success: true,
            data: response,
            message: 'Payment method created and saved successfully',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error creating payment method:', error.message);
        res.status(500).json({
            success: false,
            message: 'Payment method creation failed',
            errorCode: 'SERVER_ERROR',
            timestamp: new Date().toISOString()
        });
    }
}

// Charge endpoint
app.post('/charge', async (req, res) => {
    try {
        const { paymentMethodId } = req.body;
        
        if (!paymentMethodId) {
            return res.status(400).json({
                success: false,
                message: 'Payment method ID is required',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        const result = await processPayment(paymentMethodId, 25.00, 'charge');
        res.json({
            success: true,
            data: result,
            message: 'Payment processed successfully',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Charge error:', error.message);
        res.status(400).json({
            success: false,
            message: error.message,
            errorCode: 'PAYMENT_FAILED',
            timestamp: new Date().toISOString()
        });
    }
});

// Schedule payment endpoint
app.post('/schedule-payment', async (req, res) => {
    try {
        const { paymentMethodId } = req.body;
        
        if (!paymentMethodId) {
            return res.status(400).json({
                success: false,
                message: 'Payment method ID is required',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        const result = await processAuthorization(paymentMethodId, 50.00);
        res.json({
            success: true,
            data: result,
            message: 'Payment scheduled successfully',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Schedule payment error:', error.message);
        res.status(400).json({
            success: false,
            message: error.message,
            errorCode: 'AUTHORIZATION_FAILED',
            timestamp: new Date().toISOString()
        });
    }
});

// Process payment (charge)
async function processPayment(paymentMethodId, amount, type) {
    const paymentMethod = await jsonStorage.findPaymentMethod(paymentMethodId);
    if (!paymentMethod) {
        throw new Error('Payment method not found');
    }

    console.log(`💳 Processing ${type} for $${amount}`);
    console.log(`   Card: ${paymentMethod.cardBrand} ending in ${paymentMethod.last4}`);

    if (mockModeEnabled) {
        // Generate mock response
        const mockTransactionId = `txn_${Date.now()}`;
        const mockAuthCode = Math.random().toString(36).substring(2, 8).toUpperCase();
        const mockRefNumber = `ref_${Date.now()}`;

        console.log(`🟡 Mock mode: Generated mock transaction ${mockTransactionId}`);

        return {
            transactionId: mockTransactionId,
            amount: amount,
            currency: 'USD',
            status: 'approved',
            responseCode: '00',
            responseMessage: 'APPROVAL',
            timestamp: new Date().toISOString(),
            gatewayResponse: {
                authCode: mockAuthCode,
                referenceNumber: mockRefNumber
            },
            paymentMethod: {
                id: paymentMethod.id,
                type: 'card',
                brand: paymentMethod.cardBrand,
                last4: paymentMethod.last4,
                nickname: paymentMethod.nickname || ''
            },
            mockMode: true
        };
    } else {
        // Process with real SDK
        try {
            const card = new CreditCardData();
            card.token = paymentMethod.vaultToken;
            
            const response = await card.charge(amount)
                .withCurrency('USD')
                .withAllowDuplicates(true)
                .execute();

            console.log(`🟢 Live payment processed: ${response.transactionId}`);

            return {
                transactionId: response.transactionId || '',
                amount: amount,
                currency: 'USD',
                status: response.responseCode === '00' ? 'approved' : 'declined',
                responseCode: response.responseCode || '',
                responseMessage: response.responseMessage || '',
                timestamp: new Date().toISOString(),
                gatewayResponse: {
                    authCode: response.authorizationCode || '',
                    referenceNumber: response.referenceNumber || ''
                },
                paymentMethod: {
                    id: paymentMethod.id,
                    type: 'card',
                    brand: paymentMethod.cardBrand,
                    last4: paymentMethod.last4,
                    nickname: paymentMethod.nickname || ''
                },
                mockMode: false
            };
        } catch (error) {
            throw new Error(`Payment processing failed: ${error.message}`);
        }
    }
}

// Process authorization (schedule payment)
async function processAuthorization(paymentMethodId, amount) {
    const paymentMethod = await jsonStorage.findPaymentMethod(paymentMethodId);
    if (!paymentMethod) {
        throw new Error('Payment method not found');
    }

    console.log(`⏰ Processing authorization for $${amount}`);
    console.log(`   Card: ${paymentMethod.cardBrand} ending in ${paymentMethod.last4}`);

    if (mockModeEnabled) {
        // Generate mock response
        const mockAuthId = `auth_${Date.now()}`;
        const mockTransactionId = `txn_${Date.now()}`;
        const expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();

        console.log(`🟡 Mock mode: Generated mock authorization ${mockAuthId}`);

        return {
            authorizationId: mockAuthId,
            transactionId: mockTransactionId,
            amount: amount,
            currency: 'USD',
            status: 'authorized',
            responseCode: '00',
            responseMessage: 'AUTHORIZED',
            timestamp: new Date().toISOString(),
            expiresAt: expiresAt,
            gatewayResponse: {
                authCode: Math.random().toString(36).substring(2, 8).toUpperCase(),
                referenceNumber: `ref_${Date.now()}`
            },
            paymentMethod: {
                id: paymentMethod.id,
                type: 'card',
                brand: paymentMethod.cardBrand,
                last4: paymentMethod.last4,
                nickname: paymentMethod.nickname || ''
            },
            mockMode: true,
            captureInfo: {
                canCapture: true,
                expiresAt: expiresAt
            }
        };
    } else {
        // Process with real SDK
        try {
            const card = new CreditCardData();
            card.token = paymentMethod.vaultToken;
            
            const response = await card.authorize(amount)
                .withCurrency('USD')
                .withAllowDuplicates(true)
                .execute();

            const expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
            console.log(`🟢 Live authorization processed: ${response.transactionId}`);

            return {
                authorizationId: response.transactionId || '',
                transactionId: response.transactionId || '',
                amount: amount,
                currency: 'USD',
                status: response.responseCode === '00' ? 'authorized' : 'declined',
                responseCode: response.responseCode || '',
                responseMessage: response.responseMessage || '',
                timestamp: new Date().toISOString(),
                expiresAt: expiresAt,
                gatewayResponse: {
                    authCode: response.authorizationCode || '',
                    referenceNumber: response.referenceNumber || ''
                },
                paymentMethod: {
                    id: paymentMethod.id,
                    type: 'card',
                    brand: paymentMethod.cardBrand,
                    last4: paymentMethod.last4,
                    nickname: paymentMethod.nickname || ''
                },
                mockMode: false,
                captureInfo: {
                    canCapture: true,
                    expiresAt: expiresAt
                }
            };
        } catch (error) {
            throw new Error(`Authorization failed: ${error.message}`);
        }
    }
}

// Initialize and start the server
async function startServer() {
    // Configure SDK
    configureGlobalPaymentsSDK();
    
    // Initialize storage
    await jsonStorage.initializeStorage();
    
    // Load mock mode configuration
    await initializeMockMode();
    
    console.log('🚀 Node.js Server starting...');
    console.log('📋 Available endpoints:');
    console.log('   GET  /health - System health check');
    console.log('   GET  /config - Frontend configuration');
    console.log('   GET  /payment-methods - List payment methods');
    console.log('   POST /payment-methods - Create/edit payment methods');
    console.log('   POST /charge - Process $25 charge');
    console.log('   POST /schedule-payment - Process $50 authorization');
    console.log('   GET  /mock-mode - Get mock mode status');
    console.log('   POST /mock-mode - Toggle mock mode');
    console.log(`🎭 Mock mode: ${mockModeEnabled ? 'enabled' : 'disabled'}`);
    
    app.listen(port, '0.0.0.0', () => {
        console.log(`🚀 Server running at http://localhost:${port}`);
        console.log(`📊 Environment: ${process.env.NODE_ENV || 'development'}`);
    });
}

// Start the server
startServer().catch(console.error);