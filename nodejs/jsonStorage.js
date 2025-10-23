/**
 * JSON Storage utility for payment methods and configuration
 * Node.js implementation with async file operations
 */

import fs from 'fs/promises';
import path from 'path';
import { v4 as uuidv4 } from 'uuid';

const DATA_DIR = 'data';
const PAYMENT_METHODS_FILE = path.join(DATA_DIR, 'payment_methods.json');
const MOCK_MODE_CONFIG_FILE = path.join(DATA_DIR, 'mock_mode_config.json');

/**
 * Initialize storage directory and files
 */
export async function initializeStorage() {
    try {
        // Create data directory if it doesn't exist
        await fs.mkdir(DATA_DIR, { recursive: true });
        
        // Initialize payment methods file if it doesn't exist
        try {
            await fs.access(PAYMENT_METHODS_FILE);
        } catch {
            await fs.writeFile(PAYMENT_METHODS_FILE, '[]', 'utf8');
        }
        
        console.log('✅ Storage initialized successfully');
    } catch (error) {
        console.error('❌ Failed to initialize storage:', error.message);
        throw error;
    }
}

/**
 * Generate unique ID for payment methods
 */
function generateId() {
    return `pm_${Date.now()}_${uuidv4().replace(/-/g, '').substring(0, 16)}`;
}

/**
 * Load all payment methods
 */
export async function loadPaymentMethods() {
    try {
        const data = await fs.readFile(PAYMENT_METHODS_FILE, 'utf8');
        return JSON.parse(data);
    } catch (error) {
        console.log('Warning: Could not load payment methods, returning empty array');
        return [];
    }
}

/**
 * Save payment methods to file
 */
async function savePaymentMethods(methods) {
    try {
        await fs.writeFile(PAYMENT_METHODS_FILE, JSON.stringify(methods, null, 2), 'utf8');
    } catch (error) {
        console.error('Error saving payment methods:', error.message);
        throw error;
    }
}

/**
 * Add a new payment method
 */
export async function addPaymentMethod(data) {
    const methods = await loadPaymentMethods();
    
    // Generate unique ID
    const id = generateId();
    
    // Create payment method object
    const paymentMethod = {
        id: id,
        storedPaymentToken: data.storedPaymentToken,
        cardBrand: data.cardBrand,
        last4: data.last4,
        expiry: data.expiry,
        nickname: data.nickname || `${data.cardBrand} ending in ${data.last4}`,
        isDefault: data.isDefault || false,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
    };
    
    // Handle default payment method
    if (paymentMethod.isDefault) {
        // Remove default from all existing methods
        methods.forEach(method => {
            method.isDefault = false;
        });
    } else if (methods.length === 0) {
        // Make first method default
        paymentMethod.isDefault = true;
    }
    
    methods.push(paymentMethod);
    await savePaymentMethods(methods);
    
    return paymentMethod;
}

/**
 * Find payment method by ID
 */
export async function findPaymentMethod(id) {
    const methods = await loadPaymentMethods();
    return methods.find(method => method.id === id) || null;
}

/**
 * Update an existing payment method (nickname and default status only)
 */
export async function updatePaymentMethod(id, updateData) {
    const methods = await loadPaymentMethods();
    const methodIndex = methods.findIndex(method => method.id === id);
    
    if (methodIndex === -1) {
        return null;
    }
    
    const method = methods[methodIndex];
    
    // Update editable fields
    if (updateData.nickname !== undefined) {
        method.nickname = updateData.nickname;
    }
    
    // Handle default payment method change
    if (updateData.isDefault !== undefined) {
        if (updateData.isDefault && !method.isDefault) {
            // Remove default from all existing methods
            methods.forEach(m => {
                m.isDefault = false;
            });
            method.isDefault = true;
        } else if (!updateData.isDefault && method.isDefault) {
            method.isDefault = false;
        }
    }
    
    method.updatedAt = new Date().toISOString();
    await savePaymentMethods(methods);
    
    return method;
}

/**
 * Get all payment methods formatted for display
 */
export async function getFormattedPaymentMethods() {
    const methods = await loadPaymentMethods();
    
    return methods.map(method => ({
        id: method.id,
        type: 'card',
        last4: method.last4,
        brand: method.cardBrand,
        expiry: method.expiry,
        nickname: method.nickname || '',
        isDefault: method.isDefault || false
    }));
}

/**
 * Set a payment method as default (and remove default from others)
 */
export async function setDefaultPaymentMethod(id) {
    const methods = await loadPaymentMethods();
    const targetMethod = methods.find(method => method.id === id);
    
    if (!targetMethod) {
        return false;
    }
    
    // Remove default from all methods
    methods.forEach(method => {
        method.isDefault = false;
    });
    
    // Set target method as default
    targetMethod.isDefault = true;
    targetMethod.updatedAt = new Date().toISOString();
    
    await savePaymentMethods(methods);
    return true;
}

/**
 * Delete a payment method (for future use)
 */
export async function deletePaymentMethod(id) {
    const methods = await loadPaymentMethods();
    const filteredMethods = methods.filter(method => method.id !== id);
    
    if (filteredMethods.length === methods.length) {
        return false; // Method not found
    }
    
    await savePaymentMethods(filteredMethods);
    return true;
}

/**
 * Load mock mode configuration
 */
export async function loadMockModeConfig() {
    try {
        const data = await fs.readFile(MOCK_MODE_CONFIG_FILE, 'utf8');
        const config = JSON.parse(data);
        return config.isEnabled || false;
    } catch {
        return false; // Default to disabled
    }
}

/**
 * Save mock mode configuration
 */
export async function saveMockModeConfig(enabled) {
    const config = {
        isEnabled: enabled,
        lastUpdated: new Date().toISOString()
    };
    
    try {
        await fs.writeFile(MOCK_MODE_CONFIG_FILE, JSON.stringify(config, null, 2), 'utf8');
    } catch (error) {
        console.error('Error saving mock mode config:', error.message);
        throw error;
    }
}