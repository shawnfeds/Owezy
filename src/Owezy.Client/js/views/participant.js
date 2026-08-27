import { api } from '../api.js';

export function renderParticipantView(container, token) {
  let viewData = null;
  let errorMsg = null;
  let isUpdating = false;

  async function loadData() {
    try {
      viewData = await api.getParticipantView(token);
      errorMsg = null;
    } catch (err) {
      errorMsg = err.message || 'Invalid or expired participant link.';
    }
    render();
  }

  function render() {
    if (errorMsg) {
      container.innerHTML = `
        <div class="view-container">
          <div class="card toast-banner toast-error">
            <span>⚠️ ${escapeHtml(errorMsg)}</span>
          </div>
          <p class="section-subtitle" style="text-align: center; margin-top: 16px;">
            Please contact the bill splitter to get a valid access link.
          </p>
        </div>
      `;
      return;
    }

    if (!viewData) {
      container.innerHTML = `
        <div class="view-container" style="justify-content: center; align-items: center;">
          <div class="spinner" style="width: 32px; height: 32px;"></div>
          <p class="section-subtitle" style="margin-top: 12px;">Loading your bill summary...</p>
        </div>
      `;
      return;
    }

    const isPaid = viewData.paymentStatus === 'Paid';

    container.innerHTML = `
      <div class="view-container">
        <!-- Header / Identity -->
        <div class="card">
          <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 8px;">
            <div>
              <span style="font-size: 11px; text-transform: uppercase; color: var(--primary); font-weight: 700;">Participant View</span>
              <h2 style="font-size: 20px; font-weight: 700; color: var(--text-main);">${escapeHtml(viewData.billTitle)}</h2>
              <div class="section-subtitle" style="margin-bottom: 0;">
                Mobile: <strong>${escapeHtml(viewData.participantPhoneNumber)}</strong>
              </div>
            </div>
            <span class="badge ${isPaid ? 'badge-paid' : 'badge-unpaid'}">
              ${isPaid ? '✅ Paid' : '⏳ Unpaid'}
            </span>
          </div>

          <!-- Total Owed Card -->
          <div style="background: var(--bg-input); border: 1px solid var(--border-color); padding: 16px; border-radius: var(--radius-sm); margin-top: 12px; display: flex; justify-content: space-between; align-items: center;">
            <div>
              <div style="font-size: 12px; color: var(--text-muted);">Your Share Total</div>
              <div style="font-size: 24px; font-weight: 700; color: var(--primary);">₹${viewData.totalAmountOwed.toFixed(2)}</div>
            </div>
            <div style="text-align: right;">
              <div style="font-size: 11px; color: var(--text-muted);">Full Bill Total</div>
              <div style="font-size: 14px; font-weight: 600; color: var(--text-main);">₹${viewData.billTotalAmount.toFixed(2)}</div>
            </div>
          </div>

          <!-- Self-Payment Button -->
          <div style="margin-top: 16px;">
            ${!isPaid ? `
              <button type="button" class="btn btn-emerald" id="btn-mark-paid" ${isUpdating ? 'disabled' : ''}>
                ${isUpdating ? '<div class="spinner"></div><span>Updating...</span>' : '<span>Mark My Share as Paid</span>'}
              </button>
            ` : `
              <div class="toast-banner toast-success">
                ✅ Payment recorded on ${new Date(viewData.paidAt || Date.now()).toLocaleDateString()}. Thank you!
              </div>
            `}
          </div>
        </div>

        <!-- Scoped Items List -->
        <div class="card">
          <h3 class="card-title">Your Shared Items (${viewData.items.length})</h3>
          <p class="section-subtitle">Displaying items assigned to your share.</p>

          ${viewData.items.length === 0 ? `
            <div class="empty-state">
              <div class="empty-state-icon">📋</div>
              <p>No items assigned to your share yet.</p>
            </div>
          ` : `
            <div class="list-group">
              ${viewData.items.map(item => `
                <div class="list-item">
                  <div>
                    <div class="list-item-title">${escapeHtml(item.description)} (x${item.quantity})</div>
                    <div class="list-item-sub">Item Total: ₹${item.itemTotalAmount.toFixed(2)}</div>
                  </div>
                  <div style="text-align: right;">
                    <div style="font-size: 15px; font-weight: 700; color: var(--primary);">₹${item.myShareAmount.toFixed(2)}</div>
                    <div style="font-size: 11px; color: var(--text-muted);">Your share</div>
                  </div>
                </div>
              `).join('')}
            </div>
          `}
        </div>
      </div>
    `;

    const btnMarkPaid = container.querySelector('#btn-mark-paid');
    if (btnMarkPaid) {
      btnMarkPaid.addEventListener('click', async () => {
        isUpdating = true;
        render();
        try {
          await api.markParticipantPaidByToken(token);
          await loadData();
        } catch (err) {
          alert(`Error updating payment status: ${err.message}`);
        } finally {
          isUpdating = false;
        }
      });
    }
  }

  loadData();
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}
