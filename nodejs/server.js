/**
 * Multi-Use One-Click Payment Processing Server - Node.js Implementation
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
            service: 'save-reuse-payment-nodejs',
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

        mockModeEnabled = isEnabled;
        
        // Persist mock mode configuration
        try {
            await jsonStorage.saveMockModeConfig(mockModeEnabled);
        } catch (error) {
            console.log('Warning: Failed to save mock mode config:', error.message);
        }

        console.log(`🎭 Mock mode is now ${mockModeEnabled}`);
        
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

        // Check if this is a multi-use token creation with customer data
        const paymentToken = data.paymentToken;
        const storedPaymentToken = data.storedPaymentToken;

        // Validate required fields - either paymentToken + customerData for multi-use, or storedPaymentToken for existing
        if (!paymentToken && !storedPaymentToken) {
            return res.status(400).json({
                success: false,
                message: 'Missing required payment token or stored payment token',
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

// Handle creating a new payment method using token from frontend
async function handleCreatePaymentMethod(req, res, data) {
    try {
        const paymentToken = data.paymentToken;
        const storedPaymentToken = data.storedPaymentToken;
        let isUsingMockMode = mockModeEnabled;
        let cardDetails;
        let finalToken = storedPaymentToken;

        // Handle multi-use token creation with customer data
        if (paymentToken) {
            const customerData = data.customerData;
            const cardDetailsData = data.cardDetails;

            if (!customerData || !cardDetailsData) {
                return res.status(400).json({
                    success: false,
                    message: 'Customer data and card details required for multi-use token creation',
                    errorCode: 'VALIDATION_ERROR',
                    timestamp: new Date().toISOString()
                });
            }

            // Create multi-use token with customer data or use mock
            if (mockModeEnabled) {
                cardDetails = mockResponses.getCardDetailsFromToken(paymentToken);
                finalToken = paymentToken; // In mock mode, use original token
                console.log(`🟡 Mock mode: Using payment token ${paymentToken.substring(0, 12)}... as final token`);
            } else {
                try {
                    const multiUseResult = await paymentUtils.createMultiUseTokenWithCustomer(paymentToken, customerData, cardDetailsData);
                    finalToken = multiUseResult.multiUseToken;

                    // Create cardDetails object from result
                    cardDetails = {
                        brand: multiUseResult.brand,
                        last4: multiUseResult.last4,
                        expiryMonth: multiUseResult.expiryMonth,
                        expiryYear: multiUseResult.expiryYear,
                        token: finalToken
                    };

                    console.log(`🟢 Live mode: Created multi-use token for ${cardDetails.brand} ending in ${cardDetails.last4}`);
                } catch (error) {
                    console.log(`❌ Live mode multi-use token creation failed: ${error.message}`);
                    // Fall back to mock mode
                    isUsingMockMode = true;
                    cardDetails = mockResponses.getCardDetailsFromToken(paymentToken);
                    finalToken = paymentToken;
                    console.log(`🟡 Fallback to mock mode: Using payment token as final token`);
                }
            }
        } else {
            // Handle existing stored payment token (legacy flow)
            finalToken = storedPaymentToken;

            if (mockModeEnabled) {
                // Use mock card details
                cardDetails = mockResponses.getCardDetailsFromToken(storedPaymentToken);
                console.log(`🟡 Mock mode: Retrieved mock card details for token ${storedPaymentToken.substring(0, 12)}...`);
            } else {
                try {
                    // Try to get card details from real stored payment token
                    cardDetails = await paymentUtils.getCardDetailsFromToken(storedPaymentToken);
                    console.log(`🟢 Live mode: Retrieved card details for ${cardDetails.brand} ending in ${cardDetails.last4}`);
                } catch (error) {
                    console.log(`❌ Live mode token lookup failed: ${error.message}`);
                    // Fall back to mock mode
                    isUsingMockMode = true;
                    cardDetails = mockResponses.getCardDetailsFromToken(storedPaymentToken);
                    console.log(`🟡 Fallback to mock mode: Using mock card details`);
                }
            }
        }

        // Validate card details
        if (!cardDetails || !cardDetails.brand || !cardDetails.last4) {
            return res.status(400).json({
                success: false,
                message: 'Invalid token or unable to retrieve card details',
                errorCode: 'VALIDATION_ERROR',
                timestamp: new Date().toISOString()
            });
        }

        const expiry = `${cardDetails.expiryMonth}/${cardDetails.expiryYear}`;

        console.log(`💳 Creating payment method: ${cardDetails.brand} ending in ${cardDetails.last4}`);
        console.log(`   Nickname: ${data.nickname || 'None'}`);
        console.log(`   Default: ${data.isDefault || false}`);
        console.log(`   Mock mode: ${isUsingMockMode}`);

        // Save payment method
        const storedData = {
            storedPaymentToken: finalToken,
            cardBrand: cardDetails.brand,
            last4: cardDetails.last4,
            expiry: expiry,
            nickname: data.nickname || `${cardDetails.brand} ending in ${cardDetails.last4}`,
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

        console.log('✅ Payment method created successfully');

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
            card.token = paymentMethod.storedPaymentToken;
            
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