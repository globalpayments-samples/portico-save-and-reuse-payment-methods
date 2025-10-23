/**
 * Mock responses for testing payment scenarios
 */

/**
 * Get successful payment response
 */
export const getPaymentResponse = (amount, paymentMethodId) => {
    return {
        transactionId: `txn_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        amount: amount,
        currency: 'USD',
        status: 'approved',
        responseCode: '00',
        responseMessage: 'Approved',
        timestamp: new Date().toISOString(),
        gatewayResponse: {
            authCode: `A${Math.floor(Math.random() * 100000).toString().padStart(5, '0')}`,
            referenceNumber: `REF${Math.floor(Math.random() * 1000000000).toString().padStart(10, '0')}`
        }
    };
};


/**
 * Get decline response with specific reason
 */
export const getDeclineResponse = (reason) => {
    const declineReasons = {
        'insufficient_funds': 'Insufficient Funds',
        'generic': 'Card Declined',
        'pickup_card': 'Pick Up Card',
        'lost_card': 'Lost Card',
        'stolen_card': 'Stolen Card',
        'expired_card': 'Expired Card',
        'incorrect_cvc': 'Incorrect CVC',
        'incorrect_zip': 'Incorrect ZIP',
        'card_declined': 'Card Declined',
        'invalid_account': 'Invalid Account',
        'card_not_activated': 'Card Not Activated',
        'processing_error': 'Processing Error',
        'system_error': 'System Error'
    };
    
    return {
        errorCode: reason.toUpperCase(),
        responseMessage: declineReasons[reason] || 'Card Declined'
    };
};

/**
 * Generate mock stored payment token
 */
export const generateMockStoredPaymentToken = () => {
    return `token_${Date.now()}_${Math.random().toString(36).substr(2, 16)}`;
};

/**
 * Get card details from mock stored payment token
 */
export const getCardDetailsFromToken = (storedPaymentToken) => {
    // Extract mock data from token pattern or use defaults for demo
    const mockDetails = {
        brand: 'Visa',
        last4: '0016',
        expiryMonth: '12',
        expiryYear: '28'
    };

    // If token contains identifiable patterns, use them
    const tokenLower = storedPaymentToken.toLowerCase();
    if (tokenLower.includes('visa')) {
        mockDetails.brand = 'Visa';
        mockDetails.last4 = '0016';
    } else if (tokenLower.includes('mastercard') || tokenLower.includes('mc')) {
        mockDetails.brand = 'Mastercard';
        mockDetails.last4 = '5780';
    } else if (tokenLower.includes('amex')) {
        mockDetails.brand = 'American Express';
        mockDetails.last4 = '1018';
    } else if (tokenLower.includes('discover')) {
        mockDetails.brand = 'Discover';
        mockDetails.last4 = '6527';
    }

    return mockDetails;
};