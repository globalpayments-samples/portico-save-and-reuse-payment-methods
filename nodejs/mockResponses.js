/**
 * Mock responses for testing payment scenarios
 */

/**
 * Get response type based on card's last 4 digits
 */
export const getResponseByCardNumber = (last4) => {
    // Success scenarios
    if (['1111', '4242', '0000'].includes(last4)) {
        return 'success';
    }
    
    // Decline scenarios
    const declineMap = {
        '0002': 'decline_insufficient_funds',
        '0004': 'decline_generic',
        '0005': 'decline_pickup_card',
        '0041': 'decline_lost_card',
        '0043': 'decline_stolen_card',
        '0051': 'decline_expired_card',
        '0054': 'decline_incorrect_cvc',
        '0055': 'decline_incorrect_zip',
        '0065': 'decline_card_declined',
        '0076': 'decline_invalid_account',
        '0078': 'decline_card_not_activated',
        '0091': 'error_processing_error',
        '0096': 'error_system_error'
    };
    
    return declineMap[last4] || 'success';
};

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
 * Generate mock vault token
 */
export const generateMockVaultToken = () => {
    return `token_${Date.now()}_${Math.random().toString(36).substr(2, 16)}`;
};

/**
 * Get card details from mock vault token
 */
export const getCardDetailsFromToken = (vaultToken) => {
    // Extract mock data from token pattern or use defaults for demo
    const mockDetails = {
        brand: 'Visa',
        last4: '0016',
        expiryMonth: '12',
        expiryYear: '28'
    };

    // If token contains identifiable patterns, use them
    const tokenLower = vaultToken.toLowerCase();
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