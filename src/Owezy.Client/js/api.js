// Owezy API Client

const API_BASE = ''; // Relative path to API root

export const authState = {
  getToken: () => sessionStorage.getItem('owezy_token'),
  getPhone: () => sessionStorage.getItem('owezy_phone'),
  setAuth: (token, phone) => {
    sessionStorage.setItem('owezy_token', token);
    sessionStorage.setItem('owezy_phone', phone);
  },
  clearAuth: () => {
    sessionStorage.removeItem('owezy_token');
    sessionStorage.removeItem('owezy_phone');
  },
  isAuthenticated: () => !!sessionStorage.getItem('owezy_token')
};

async function fetchApi(endpoint, options = {}) {
  const token = authState.getToken();
  const headers = { ...options.headers };

  if (token && !options.skipAuth) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (options.body && !(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers
  });

  if (response.status === 401 && !options.skipAuth) {
    authState.clearAuth();
    window.location.hash = '#/auth';
    throw new Error('Session expired or unauthorized. Please log in again.');
  }

  let data = null;
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    data = await response.json();
  }

  if (!response.ok) {
    const errorMsg = data?.message || data?.title || `Request failed with status ${response.status}`;
    const err = new Error(errorMsg);
    err.status = response.status;
    err.data = data;
    throw err;
  }

  return data;
}

export const api = {
  // Auth
  requestOtp: (phoneNumber) => fetchApi('/auth/otp/request', {
    method: 'POST',
    skipAuth: true,
    body: JSON.stringify({ phoneNumber })
  }),

  verifyOtp: (phoneNumber, code) => fetchApi('/auth/otp/verify', {
    method: 'POST',
    skipAuth: true,
    body: JSON.stringify({ phoneNumber, code })
  }),

  // Bills (Splitter)
  createBill: (title) => fetchApi('/bills', {
    method: 'POST',
    body: JSON.stringify({ title })
  }),

  getBillSummary: (billId) => fetchApi(`/bills/${billId}`),

  addParticipant: (billId, phoneNumber) => fetchApi(`/bills/${billId}/participants`, {
    method: 'POST',
    body: JSON.stringify({ phoneNumber })
  }),

  addBillItem: (billId, description, quantity, amount, sharerParticipantIds = []) => fetchApi(`/bills/${billId}/items`, {
    method: 'POST',
    body: JSON.stringify({ description, quantity, amount, sharerParticipantIds })
  }),

  updateItemSharers: (billId, itemId, participantIds) => fetchApi(`/bills/${billId}/items/${itemId}/sharers`, {
    method: 'PUT',
    body: JSON.stringify({ participantIds })
  }),

  finalizeBill: (billId) => fetchApi(`/bills/${billId}/finalize`, {
    method: 'POST'
  }),

  generateAccessLink: (billId, participantId) => fetchApi(`/bills/${billId}/participants/${participantId}/access-link`, {
    method: 'POST'
  }),

  getBillPayments: (billId) => fetchApi(`/bills/${billId}/payments`),

  getBillSettlement: (billId) => fetchApi(`/bills/${billId}/settlement`),

  // Receipts
  uploadReceipt: (billId, file) => {
    const formData = new FormData();
    formData.append('file', file);
    return fetchApi(`/bills/${billId}/receipt`, {
      method: 'POST',
      body: formData
    });
  },

  getReceiptDraft: (billId, receiptId) => fetchApi(`/bills/${billId}/receipt/${receiptId}`),

  updateReceiptDraft: (billId, receiptId, draft) => fetchApi(`/bills/${billId}/receipt/${receiptId}`, {
    method: 'PUT',
    body: JSON.stringify(draft)
  }),

  confirmReceipt: (billId, receiptId) => fetchApi(`/bills/${billId}/receipt/${receiptId}/confirm`, {
    method: 'POST'
  }),

  // Participant View (Anonymous token access)
  getParticipantView: (token) => fetchApi(`/participant-access/${token}`, { skipAuth: true }),

  getParticipantSummary: (token) => fetchApi(`/participant-access/${token}/summary`, { skipAuth: true }),

  markParticipantPaidByToken: (token) => fetchApi(`/participant-access/${token}/payment`, {
    method: 'POST',
    skipAuth: true
  })
};
