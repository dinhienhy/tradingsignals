// Trading Signals API - Webhook Management UI

document.addEventListener('DOMContentLoaded', function() {
    // References to DOM elements - Webhooks
    const configApiKeyInput = document.getElementById('config-api-key');
    const saveApiKeyBtn = document.getElementById('save-api-key');
    const apiKeyNotice = document.getElementById('api-key-notice');
    const webhooksContainer = document.getElementById('webhooks-container');
    const webhooksList = document.getElementById('webhooks-list');
    const webhookForm = document.getElementById('webhook-form');
    const webhookIdInput = document.getElementById('webhook-id');
    const webhookPathInput = document.getElementById('webhook-path');
    const webhookSecretInput = document.getElementById('webhook-secret');
    const webhookDescriptionInput = document.getElementById('webhook-description');
    const saveWebhookBtn = document.getElementById('save-webhook');
    const confirmDeleteBtn = document.getElementById('confirm-delete');
    
    // References to DOM elements - Signals
    const signalApiKeyInput = document.getElementById('signal-api-key');
    const saveSignalApiKeyBtn = document.getElementById('save-signal-api-key');
    const signalApiKeyNotice = document.getElementById('signal-api-key-notice');
    const signalsContainer = document.getElementById('signals-container');
    const signalsList = document.getElementById('signals-list');
    const refreshSignalsBtn = document.getElementById('refresh-signals');
    
    // References to DOM elements - Active Signals
    const activeSignalApiKeyInput = document.getElementById('active-signal-api-key');
    const saveActiveSignalApiKeyBtn = document.getElementById('save-active-signal-api-key');
    const activeSignalApiKeyNotice = document.getElementById('active-signal-api-key-notice');
    const activeSignalsContainer = document.getElementById('active-signals-container');
    const activeSignalsList = document.getElementById('active-signals-list');
    const refreshActiveSignalsBtn = document.getElementById('refresh-active-signals');
    const allSignalsRadio = document.getElementById('all-signals');
    const pendingSignalsRadio = document.getElementById('pending-signals');
    const processedSignalsRadio = document.getElementById('processed-signals');
    
    // Bootstrap components
    const webhookModal = new bootstrap.Modal(document.getElementById('webhookModal'));
    const confirmationModal = new bootstrap.Modal(document.getElementById('confirmationModal'));
    const toastElement = document.getElementById('toast-notification');
    const toast = new bootstrap.Toast(toastElement);
    const toastTitle = document.getElementById('toast-title');
    const toastMessage = document.getElementById('toast-message');
    
    // State variables
    let configApiKey = localStorage.getItem('configApiKey') || '';
    let signalApiKey = localStorage.getItem('signalApiKey') || '';
    let activeSignalApiKey = localStorage.getItem('activeSignalApiKey') || '';
    let deleteWebhookId = null;
    let currentSignalFilter = 'all';  // 'all', 'pending', or 'processed'
    
    // Initialize
    init();
    
    // Event listeners - Webhooks
    saveApiKeyBtn.addEventListener('click', handleSaveApiKey);
    saveWebhookBtn.addEventListener('click', handleSaveWebhook);
    confirmDeleteBtn.addEventListener('click', handleConfirmDelete);
    
    // Event listeners - Signals
    saveSignalApiKeyBtn.addEventListener('click', handleSaveSignalApiKey);
    refreshSignalsBtn.addEventListener('click', loadSignals);
    allSignalsRadio.addEventListener('change', handleSignalFilterChange);
    pendingSignalsRadio.addEventListener('change', handleSignalFilterChange);
    processedSignalsRadio.addEventListener('change', handleSignalFilterChange);
    
    // Event listeners - Active Signals
    saveActiveSignalApiKeyBtn.addEventListener('click', handleSaveActiveSignalApiKey);
    refreshActiveSignalsBtn.addEventListener('click', loadActiveSignals);
    
    // Helper function to format timestamps to GMT+7
    function formatTimestampGMT7(timestamp) {
        // Create a date from the timestamp
        const date = new Date(timestamp);
        
        // Format to GMT+7 (Vietnam/Thailand timezone - Indochina Time)
        return date.toLocaleString('vi-VN', {
            timeZone: 'Asia/Bangkok',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false
        });
    }
    
    // Functions
    function init() {
        if (configApiKey) {
            configApiKeyInput.value = configApiKey;
            apiKeyNotice.classList.add('d-none');
            webhooksContainer.classList.remove('d-none');
            loadWebhooks();
        }
        
        if (signalApiKey) {
            signalApiKeyInput.value = signalApiKey;
            signalApiKeyNotice.classList.add('d-none');
            signalsContainer.classList.remove('d-none');
            loadSignals();
        }
        
        if (activeSignalApiKey) {
            activeSignalApiKeyInput.value = activeSignalApiKey;
            activeSignalApiKeyNotice.classList.add('d-none');
            activeSignalsContainer.classList.remove('d-none');
            loadActiveSignals();
        }
    }
    
    function handleSaveApiKey() {
        configApiKey = configApiKeyInput.value.trim();
        if (configApiKey) {
            localStorage.setItem('configApiKey', configApiKey);
            apiKeyNotice.classList.add('d-none');
            webhooksContainer.classList.remove('d-none');
            loadWebhooks();
            showToast('Success', 'API Key saved');
        } else {
            showToast('Error', 'Please enter a valid API Key', 'danger');
        }
    }
    
    function loadWebhooks() {
        // Show loading state
        webhooksList.innerHTML = '<tr><td colspan="4" class="text-center">Loading webhook configurations...</td></tr>';
        
        fetch('/config/webhooks', {
            headers: {
                'ConfigApiKey': configApiKey
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`Status: ${response.status}`);
            }
            return response.json();
        })
        .then(webhooks => {
            renderWebhooks(webhooks);
        })
        .catch(error => {
            console.error('Error loading webhooks:', error);
            if (error.message.includes('401')) {
                showToast('Error', 'Invalid API Key. Please check your credentials.', 'danger');
                resetApiKey();
            } else {
                showToast('Error', 'Failed to load webhooks. ' + error.message, 'danger');
                webhooksList.innerHTML = '<tr><td colspan="4" class="text-center text-danger">Failed to load webhooks. Check console for details.</td></tr>';
            }
        });
    }
    
    function renderWebhooks(webhooks) {
        webhooksList.innerHTML = '';
        
        if (!webhooks || webhooks.length === 0) {
            webhooksList.innerHTML = '<tr><td colspan="4" class="text-center">No webhook configurations found</td></tr>';
            return;
        }
        
        webhooks.forEach(webhook => {
            const row = document.createElement('tr');
            row.className = 'webhook-item';
            row.innerHTML = `
                <td>${webhook.path}</td>
                <td>${webhook.secret}</td>
                <td>${webhook.description || ''}</td>
                <td class="text-end">
                    <button class="btn btn-sm btn-primary edit-webhook" data-id="${webhook.id}">Edit</button>
                    <button class="btn btn-sm btn-danger delete-webhook" data-id="${webhook.id}">Delete</button>
                </td>
            `;
            webhooksList.appendChild(row);
            
            // Add event listeners
            row.querySelector('.edit-webhook').addEventListener('click', () => openEditWebhookModal(webhook));
            row.querySelector('.delete-webhook').addEventListener('click', () => openDeleteConfirmation(webhook.id));
        });
    }
    
    function openEditWebhookModal(webhook = null) {
        // Reset form
        webhookForm.reset();
        webhookIdInput.value = '';
        
        if (webhook) {
            // Edit existing webhook
            webhookIdInput.value = webhook.id;
            webhookPathInput.value = webhook.path;
            webhookSecretInput.value = webhook.secret;
            webhookDescriptionInput.value = webhook.description || '';
            
            document.querySelector('#webhookModalLabel').textContent = 'Edit Webhook Configuration';
        } else {
            // New webhook
            document.querySelector('#webhookModalLabel').textContent = 'Add New Webhook Configuration';
        }
        
        webhookModal.show();
    }
    
    function handleSaveWebhook() {
        const id = webhookIdInput.value.trim();
        const path = webhookPathInput.value.trim();
        const secret = webhookSecretInput.value.trim();
        const description = webhookDescriptionInput.value.trim();
        
        if (!path) {
            showToast('Error', 'Path is required', 'danger');
            return;
        }
        
        if (!secret) {
            showToast('Error', 'Secret is required', 'danger');
            return;
        }
        
        const webhook = {
            path,
            secret,
            description
        };
        
        if (id) {
            webhook.id = parseInt(id, 10);
        }
        
        const url = '/config/webhooks';
        
        fetch(url, {
            method: id ? 'PUT' : 'POST',
            headers: {
                'Content-Type': 'application/json',
                'ConfigApiKey': configApiKey
            },
            body: JSON.stringify(webhook)
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`Status: ${response.status}`);
            }
            return response.json();
        })
        .then(() => {
            webhookModal.hide();
            loadWebhooks();
            showToast('Success', `Webhook ${id ? 'updated' : 'created'} successfully`);
        })
        .catch(error => {
            console.error('Error saving webhook:', error);
            if (error.message.includes('401')) {
                showToast('Error', 'Invalid API Key. Please check your credentials.', 'danger');
            } else if (error.message.includes('409')) {
                showToast('Error', 'A webhook with this path already exists', 'danger');
            } else {
                showToast('Error', 'Failed to save webhook. ' + error.message, 'danger');
            }
        });
    }
    
    function openDeleteConfirmation(webhookId) {
        deleteWebhookId = webhookId;
        confirmationModal.show();
    }
    
    function handleConfirmDelete() {
        if (!deleteWebhookId) {
            return;
        }
        
        fetch(`/config/webhooks/${deleteWebhookId}`, {
            method: 'DELETE',
            headers: {
                'ConfigApiKey': configApiKey
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`Status: ${response.status}`);
            }
            confirmationModal.hide();
            loadWebhooks();
            showToast('Success', 'Webhook deleted successfully');
        })
        .catch(error => {
            console.error('Error deleting webhook:', error);
            confirmationModal.hide();
            if (error.message.includes('401')) {
                showToast('Error', 'Invalid API Key. Please check your credentials.', 'danger');
            } else {
                showToast('Error', 'Failed to delete webhook. ' + error.message, 'danger');
            }
        });
        
        deleteWebhookId = null;
    }
    
    function resetApiKey() {
        localStorage.removeItem('configApiKey');
        configApiKey = '';
        configApiKeyInput.value = '';
        apiKeyNotice.classList.remove('d-none');
        webhooksContainer.classList.add('d-none');
    }
    
    function showToast(title, message, type = 'success') {
        toastTitle.textContent = title;
        toastMessage.textContent = message;
        
        // Remove existing classes and add new one
        toastElement.className = 'toast';
        toastElement.classList.add(`text-bg-${type}`);
        
        toast.show();
    }
    
    // Signal related functions
    function handleSaveSignalApiKey() {
        signalApiKey = signalApiKeyInput.value.trim();
        if (signalApiKey) {
            localStorage.setItem('signalApiKey', signalApiKey);
            signalApiKeyNotice.classList.add('d-none');
            signalsContainer.classList.remove('d-none');
            loadSignals();
            showToast('Success', 'API Key saved');
        } else {
            showToast('Error', 'Please enter a valid API Key', 'danger');
        }
    }
    
    function handleSignalFilterChange(event) {
        currentSignalFilter = event.target.value;
        loadSignals();
    }
    
    function loadSignals() {
        // Show loading state
        signalsList.innerHTML = '<tr><td colspan="7" class="text-center">Loading signals...</td></tr>';
        
        let url = '/api/signals';
        if (currentSignalFilter === 'pending') {
            url = '/signals/pending';
        } else {
            // Use custom endpoint for all or processed signals
            url = '/api/signals?status=' + (currentSignalFilter === 'processed' ? 'processed' : 'all');
        }
        
        fetch(url, {
            headers: {
                'ApiKey': signalApiKey
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`Status: ${response.status}`);
            }
            return response.json();
        })
        .then(signals => {
            renderSignals(signals);
        })
        .catch(error => {
            console.error('Error loading signals:', error);
            if (error.message.includes('401')) {
                showToast('Error', 'Invalid API Key. Please check your credentials.', 'danger');
                resetSignalApiKey();
            } else {
                showToast('Error', 'Failed to load signals. ' + error.message, 'danger');
                signalsList.innerHTML = '<tr><td colspan="7" class="text-center text-danger">Failed to load signals. Check console for details.</td></tr>';
            }
        });
    }
    
    function renderSignals(signals) {
        signalsList.innerHTML = '';
        
        if (!signals || signals.length === 0) {
            signalsList.innerHTML = '<tr><td colspan="7" class="text-center">No signals found</td></tr>';
            return;
        }
        
        signals.forEach(signal => {
            const row = document.createElement('tr');
            row.className = 'signal-item';
            
            // Format timestamp using the helper function for GMT+7
            const formattedDate = formatTimestampGMT7(signal.timestamp);
            
            // Determine status class
            const statusClass = signal.status === 0 ? 'badge bg-warning' : 'badge bg-success';
            const statusText = signal.status === 0 ? 'Pending' : 'Processed';
            
            row.innerHTML = `
                <td>${signal.id}</td>
                <td>${signal.symbol}</td>
                <td>${signal.action}</td>
                <td>${signal.price}</td>
                <td>${formattedDate}</td>
                <td><span class="${statusClass}">${statusText}</span></td>
                <td>${signal.message || ''}</td>
            `;
            signalsList.appendChild(row);
        });
    }
    
    function resetSignalApiKey() {
        localStorage.removeItem('signalApiKey');
        signalApiKey = '';
        signalApiKeyInput.value = '';
        signalApiKeyNotice.classList.remove('d-none');
        signalsContainer.classList.add('d-none');
    }
    
    // Active Signal related functions
    function handleSaveActiveSignalApiKey() {
        activeSignalApiKey = activeSignalApiKeyInput.value.trim();
        if (activeSignalApiKey) {
            localStorage.setItem('activeSignalApiKey', activeSignalApiKey);
            activeSignalApiKeyNotice.classList.add('d-none');
            activeSignalsContainer.classList.remove('d-none');
            loadActiveSignals();
            showToast('Success', 'API Key saved');
        } else {
            showToast('Error', 'Please enter a valid API Key', 'danger');
        }
    }
    
    function loadActiveSignals() {
        // Show loading state
        activeSignalsList.innerHTML = '<tr><td colspan="6" class="text-center">Loading active signals...</td></tr>';
        
        fetch('/api/activesignals', {
            headers: {
                'X-API-Key': activeSignalApiKey
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`Status: ${response.status}`);
            }
            return response.json();
        })
        .then(signals => {
            renderActiveSignals(signals);
        })
        .catch(error => {
            console.error('Error loading active signals:', error);
            if (error.message.includes('401')) {
                showToast('Error', 'Invalid API Key. Please check your credentials.', 'danger');
                resetActiveSignalApiKey();
            } else {
                showToast('Error', 'Failed to load active signals. ' + error.message, 'danger');
                activeSignalsList.innerHTML = '<tr><td colspan="6" class="text-center text-danger">Failed to load active signals. Check console for details.</td></tr>';
            }
        });
    }
    
    function renderActiveSignals(signals) {
        activeSignalsList.innerHTML = '';
        
        if (!signals || signals.length === 0) {
            activeSignalsList.innerHTML = '<tr><td colspan="6" class="text-center">No active signals found</td></tr>';
            return;
        }
        
        signals.forEach(signal => {
            const row = document.createElement('tr');
            row.className = 'active-signal-item';
            
            // Format timestamp using the helper function for GMT+7
            const formattedDate = formatTimestampGMT7(signal.timestamp);
            
            row.innerHTML = `
                <td>${signal.symbol}</td>
                <td>${signal.action}</td>
                <td>${signal.price}</td>
                <td>${signal.type}</td>
                <td>${formattedDate}</td>
                <td>${signal.used ? '<span class="badge bg-success">Yes</span>' : '<span class="badge bg-secondary">No</span>'}</td>
            `;
            activeSignalsList.appendChild(row);
        });
    }
    
    function resetActiveSignalApiKey() {
        localStorage.removeItem('activeSignalApiKey');
        activeSignalApiKey = '';
        activeSignalApiKeyInput.value = '';
        activeSignalApiKeyNotice.classList.remove('d-none');
        activeSignalsContainer.classList.add('d-none');
    }
    
    // Add event listener for "Add New" button
    document.querySelector('[data-bs-target="#webhookModal"]').addEventListener('click', () => openEditWebhookModal());
});
