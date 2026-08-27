import { api, authState } from '../api.js';

export function renderDashboardView(container) {
  const phone = authState.getPhone();

  container.innerHTML = `
    <div class="view-container">
      <div class="card">
        <h2 class="card-title">✨ Create a New Bill</h2>
        <p class="section-subtitle">Start splitting an expense with your group.</p>
        
        <form id="create-bill-form">
          <div class="form-group">
            <label class="form-label" for="title">Bill Title / Occasion</label>
            <input 
              type="text" 
              id="title" 
              class="input-control" 
              placeholder="e.g. Saturday Dinner, Road Trip, Groceries" 
              required 
            />
          </div>
          <button type="submit" class="btn btn-primary" id="btn-create">
            <span>Create Bill</span>
          </button>
        </form>
      </div>

      <div class="card">
        <h2 class="card-title">📋 Active Bills</h2>
        <div id="recent-bills-container">
          <p class="section-subtitle">Your created or recently accessed bills will appear here.</p>
          <div class="empty-state">
            <div class="empty-state-icon">🧾</div>
            <p>No active bills in session. Create a new bill above to begin!</p>
          </div>
        </div>
      </div>
    </div>
  `;

  // Render stored recent bills from localStorage if any
  const recentBills = JSON.parse(localStorage.getItem('owezy_recent_bills') || '[]');
  if (recentBills.length > 0) {
    const listContainer = container.querySelector('#recent-bills-container');
    listContainer.innerHTML = `
      <div class="list-group">
        ${recentBills.map(b => `
          <div class="list-item" style="cursor: pointer;" onclick="window.location.hash='#/bills/${b.id}'">
            <div>
              <div class="list-item-title">${escapeHtml(b.title)}</div>
              <div class="list-item-sub">Created ${new Date(b.createdAt).toLocaleDateString()}</div>
            </div>
            <span class="badge ${b.status === 'Finalized' ? 'badge-finalized' : 'badge-active'}">${b.status || 'Active'}</span>
          </div>
        `).join('')}
      </div>
    `;
  }

  const form = container.querySelector('#create-bill-form');
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const titleInput = container.querySelector('#title').value.trim();
    if (!titleInput) return;

    const btn = container.querySelector('#btn-create');
    btn.disabled = true;
    btn.innerHTML = `<div class="spinner"></div><span>Creating...</span>`;

    try {
      const res = await api.createBill(titleInput);
      
      // Save to recent bills list in localStorage
      const bills = JSON.parse(localStorage.getItem('owezy_recent_bills') || '[]');
      bills.unshift({
        id: res.billId,
        title: res.title,
        status: 'Active',
        createdAt: res.createdAt
      });
      localStorage.setItem('owezy_recent_bills', JSON.stringify(bills.slice(0, 10)));

      window.location.hash = `#/bills/${res.billId}`;
    } catch (err) {
      alert(`Error creating bill: ${err.message}`);
      btn.disabled = false;
      btn.innerHTML = `<span>Create Bill</span>`;
    }
  });
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}
