import { api, authState } from '../api.js';

export function renderWorkspaceView(container, billId) {
  let billSummary = null;
  let activeTab = 'items'; // 'items' | 'receipt' | 'sharers' | 'settlement'
  let currentReceiptDraft = null;
  let currentReceiptId = null;
  let selectedItemId = null;
  let errorMsg = null;
  let successMsg = null;

  async function loadData() {
    try {
      billSummary = await api.getBillSummary(billId);
      if (billSummary && billSummary.items && billSummary.items.length > 0 && !selectedItemId) {
        selectedItemId = billSummary.items[0].itemId;
      }
      errorMsg = null;
    } catch (err) {
      errorMsg = err.message;
    }
    render();
  }

  function render() {
    if (errorMsg) {
      container.innerHTML = `
        <div class="view-container">
          <div class="card toast-banner toast-error">
            <span>⚠️ ${errorMsg}</span>
          </div>
          <button class="btn btn-secondary" onclick="window.location.hash='#/'">Back to Dashboard</button>
        </div>
      `;
      return;
    }

    if (!billSummary) {
      container.innerHTML = `
        <div class="view-container" style="justify-content: center; align-items: center;">
          <div class="spinner" style="width: 32px; height: 32px;"></div>
          <p class="section-subtitle" style="margin-top: 12px;">Loading bill workspace...</p>
        </div>
      `;
      return;
    }

    const isFinalized = billSummary.status === 'Finalized';

    container.innerHTML = `
      <div class="view-container">
        <!-- Bill Overview Header Card -->
        <div class="card">
          <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 8px;">
            <div>
              <h2 style="font-size: 20px; font-weight: 700; color: var(--text-main);">${escapeHtml(billSummary.title)}</h2>
              <div class="section-subtitle" style="margin-bottom: 0;">
                Splitter: <strong>${escapeHtml(billSummary.splitterPhoneNumber)}</strong>
              </div>
            </div>
            <span class="badge ${isFinalized ? 'badge-finalized' : 'badge-active'}">
              ${isFinalized ? '🔒 Finalized' : '⚡ Active'}
            </span>
          </div>

          <div style="display: flex; justify-content: space-between; align-items: center; background: var(--bg-input); padding: 12px; border-radius: var(--radius-sm); margin-top: 12px;">
            <span style="color: var(--text-muted); font-size: 13px;">Total Bill Amount</span>
            <span style="font-size: 20px; font-weight: 700; color: var(--primary);">₹${billSummary.totalAmount.toFixed(2)}</span>
          </div>
        </div>

        ${successMsg ? `<div class="toast-banner toast-success">✅ ${successMsg}</div>` : ''}

        <!-- Workspace Tabs -->
        <div class="tab-bar">
          <button class="tab-btn ${activeTab === 'items' ? 'active' : ''}" data-tab="items">Items & Members</button>
          <button class="tab-btn ${activeTab === 'receipt' ? 'active' : ''}" data-tab="receipt">📷 OCR Receipt</button>
          <button class="tab-btn ${activeTab === 'sharers' ? 'active' : ''}" data-tab="sharers">👥 Sharers</button>
          <button class="tab-btn ${activeTab === 'settlement' ? 'active' : ''}" data-tab="settlement">💰 Settlement</button>
        </div>

        <!-- Tab Contents -->
        <div id="tab-content">
          ${renderTabContent(isFinalized)}
        </div>
      </div>
    `;

    bindEvents(isFinalized);
  }

  function renderTabContent(isFinalized) {
    if (activeTab === 'items') {
      return `
        <!-- Add Participant Section -->
        ${!isFinalized ? `
          <div class="card">
            <h3 class="card-title">➕ Add Participant</h3>
            <form id="add-participant-form" style="display: flex; gap: 10px;">
              <input type="tel" id="part-phone" class="input-control" placeholder="+919123456789" required />
              <button type="submit" class="btn btn-primary btn-sm" style="white-space: nowrap;">Add</button>
            </form>
          </div>

          <!-- Add Item Manually -->
          <div class="card">
            <h3 class="card-title">🛒 Add Bill Item Manually</h3>
            <form id="add-item-form">
              <div class="form-group">
                <label class="form-label" for="item-desc">Item Description</label>
                <input type="text" id="item-desc" class="input-control" placeholder="e.g. Pizza, Drinks" required />
              </div>
              <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px;">
                <div class="form-group">
                  <label class="form-label" for="item-qty">Quantity</label>
                  <input type="number" id="item-qty" class="input-control" value="1" min="1" required />
                </div>
                <div class="form-group">
                  <label class="form-label" for="item-amount">Amount (₹)</label>
                  <input type="number" id="item-amount" class="input-control" step="0.01" min="0.01" placeholder="0.00" required />
                </div>
              </div>
              <button type="submit" class="btn btn-secondary">Add Item</button>
            </form>
          </div>
        ` : ''}

        <!-- Participants List -->
        <div class="card">
          <h3 class="card-title">Group Participants (${billSummary.participants.length})</h3>
          <div class="list-group">
            ${billSummary.participants.map(p => `
              <div class="list-item">
                <div>
                  <div class="list-item-title">${escapeHtml(p.phoneNumber)}</div>
                  <div class="list-item-sub">Owes: ₹${p.amountOwed.toFixed(2)}</div>
                </div>
                <span class="badge ${p.paymentStatus === 'Paid' ? 'badge-paid' : 'badge-unpaid'}">${p.paymentStatus}</span>
              </div>
            `).join('')}
          </div>
        </div>

        <!-- Items List -->
        <div class="card">
          <h3 class="card-title">Bill Items (${billSummary.items.length})</h3>
          ${billSummary.items.length === 0 ? `
            <div class="empty-state">
              <div class="empty-state-icon">🛒</div>
              <p>No items added yet. Add manually above or upload a receipt scan!</p>
            </div>
          ` : `
            <div class="list-group">
              ${billSummary.items.map(item => `
                <div class="list-item" style="flex-direction: column; align-items: stretch; gap: 8px;">
                  <div style="display: flex; justify-content: space-between; align-items: center;">
                    <span class="list-item-title">${escapeHtml(item.description)} (x${item.quantity})</span>
                    <span style="font-weight: 700; color: var(--primary);">₹${item.amount.toFixed(2)}</span>
                  </div>
                  <div style="font-size: 12px; color: var(--text-muted); display: flex; justify-content: space-between;">
                    <span>Sharers: ${item.sharerParticipantIds.length} members</span>
                    <span>${item.calculatedShares.length > 0 ? `₹${item.calculatedShares[0].amount.toFixed(2)} / person` : 'Unassigned'}</span>
                  </div>
                </div>
              `).join('')}
            </div>
          `}
        </div>
      `;
    }

    if (activeTab === 'receipt') {
      return `
        <div class="card">
          <h3 class="card-title">📸 Receipt Capture & OCR</h3>
          <p class="section-subtitle">Scan a paper receipt using device camera or choose from gallery.</p>

          ${!isFinalized ? `
            <div class="receipt-actions">
              <!-- Camera capture input -->
              <input type="file" id="input-camera" class="hidden-file-input" accept="image/*" capture="environment" />
              <button type="button" class="btn btn-primary" id="btn-camera">
                <span>📷 Take Photo with Camera</span>
              </button>

              <!-- Gallery / File selection input -->
              <input type="file" id="input-gallery" class="hidden-file-input" accept="image/*" />
              <button type="button" class="btn btn-secondary" id="btn-gallery">
                <span>📁 Choose from Gallery</span>
              </button>
            </div>
          ` : `
            <div class="toast-banner toast-banner-info">🔒 Receipt upload disabled for finalized bills.</div>
          `}
        </div>

        <!-- OCR Draft & Review Section -->
        <div id="ocr-draft-container">
          ${currentReceiptDraft ? renderOcrDraft(isFinalized) : `
            <div class="card empty-state">
              <div class="empty-state-icon">📄</div>
              <p>No receipt active. Upload a receipt image above to review OCR draft!</p>
            </div>
          `}
        </div>
      `;
    }

    if (activeTab === 'sharers') {
      const selectedItem = billSummary.items.find(i => i.itemId === selectedItemId) || billSummary.items[0];

      return `
        <div class="card">
          <h3 class="card-title">👥 Assign Item Sharers</h3>
          <p class="section-subtitle">Select a bill item and choose who shares the expense.</p>

          ${billSummary.items.length === 0 ? `
            <div class="empty-state">
              <div class="empty-state-icon">⚠️</div>
              <p>Add bill items first before assigning sharers.</p>
            </div>
          ` : `
            <div class="form-group">
              <label class="form-label" for="select-item">Select Item</label>
              <select id="select-item" class="input-control">
                ${billSummary.items.map(i => `
                  <option value="${i.itemId}" ${selectedItem && selectedItem.itemId === i.itemId ? 'selected' : ''}>
                    ${escapeHtml(i.description)} — ₹${i.amount.toFixed(2)} (${i.sharerParticipantIds.length} sharers)
                  </option>
                `).join('')}
              </select>
            </div>

            ${selectedItem ? `
              <div style="margin-top: 16px;">
                <h4 style="font-size: 14px; font-weight: 600; margin-bottom: 10px;">
                  Who shared "${escapeHtml(selectedItem.description)}"?
                </h4>
                <form id="sharers-form">
                  <div class="list-group" style="margin-bottom: 16px;">
                    ${billSummary.participants.map(p => {
                      const isChecked = selectedItem.sharerParticipantIds.includes(p.participantId);
                      return `
                        <label class="sharer-checkbox-item">
                          <input type="checkbox" name="sharer" value="${p.participantId}" ${isChecked ? 'checked' : ''} ${isFinalized ? 'disabled' : ''} />
                          <div>
                            <div style="font-weight: 600;">${escapeHtml(p.phoneNumber)}</div>
                            <div style="font-size: 11px; color: var(--text-muted);">${p.participantId === billSummary.participants[0].participantId ? '(Splitter)' : 'Participant'}</div>
                          </div>
                        </label>
                      `;
                    }).join('')}
                  </div>

                  ${!isFinalized ? `
                    <button type="submit" class="btn btn-primary">Save Sharer Assignment</button>
                  ` : ''}
                </form>
              </div>
            ` : ''}
          `}
        </div>
      `;
    }

    if (activeTab === 'settlement') {
      return `
        <!-- Settlement Overview -->
        <div class="card">
          <h3 class="card-title">💰 Settlement Summary</h3>
          
          <div style="display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 8px; margin-bottom: 16px; text-align: center;">
            <div style="background: var(--bg-input); padding: 10px; border-radius: var(--radius-sm);">
              <div style="font-size: 11px; color: var(--text-muted);">Total Owed</div>
              <div style="font-size: 16px; font-weight: 700; color: var(--text-main);">₹${billSummary.totalAmount.toFixed(2)}</div>
            </div>
            <div style="background: var(--accent-emerald-light); padding: 10px; border-radius: var(--radius-sm); border: 1px solid rgba(16,185,129,0.3);">
              <div style="font-size: 11px; color: var(--accent-emerald);">Total Paid</div>
              <div style="font-size: 16px; font-weight: 700; color: var(--accent-emerald);">
                ₹${billSummary.participants.filter(p => p.paymentStatus === 'Paid').reduce((acc, p) => acc + p.amountOwed, 0).toFixed(2)}
              </div>
            </div>
            <div style="background: var(--accent-amber-light); padding: 10px; border-radius: var(--radius-sm); border: 1px solid rgba(245,158,11,0.3);">
              <div style="font-size: 11px; color: var(--accent-amber);">Remaining</div>
              <div style="font-size: 16px; font-weight: 700; color: var(--accent-amber);">
                ₹${billSummary.participants.filter(p => p.paymentStatus === 'Unpaid').reduce((acc, p) => acc + p.amountOwed, 0).toFixed(2)}
              </div>
            </div>
          </div>

          <!-- Finalize Button -->
          ${!isFinalized ? `
            <button type="button" class="btn btn-emerald" id="btn-finalize-modal" style="margin-bottom: 16px;">
              🔒 Finalize Bill & Generate Links
            </button>
          ` : `
            <div class="toast-banner toast-success" style="margin-bottom: 16px;">
              🔒 Bill is finalized and immutable. Participant access links are active!
            </div>
          `}

          <!-- Participant Access Links Table -->
          <h4 style="font-size: 14px; font-weight: 600; margin-bottom: 10px;">Participant Access & Payments</h4>
          <div class="list-group">
            ${billSummary.participants.map(p => `
              <div class="list-item" style="flex-direction: column; align-items: stretch; gap: 8px;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                  <span style="font-weight: 600;">${escapeHtml(p.phoneNumber)}</span>
                  <span class="badge ${p.paymentStatus === 'Paid' ? 'badge-paid' : 'badge-unpaid'}">${p.paymentStatus}</span>
                </div>
                <div style="display: flex; justify-content: space-between; align-items: center; font-size: 13px;">
                  <span>Share: <strong>₹${p.amountOwed.toFixed(2)}</strong></span>
                  ${isFinalized ? `
                    <button type="button" class="btn btn-secondary btn-sm btn-get-link" data-partid="${p.participantId}">
                      🔗 Access Link
                    </button>
                  ` : `<span style="font-size: 11px; color: var(--text-muted);">Finalize bill to generate link</span>`}
                </div>
              </div>
            `).join('')}
          </div>
        </div>
      `;
    }
  }

  function renderOcrDraft(isFinalized) {
    const draft = currentReceiptDraft;
    return `
      <div class="card">
        <h3 class="card-title">📝 Review OCR Draft</h3>
        <p class="section-subtitle">Extracted from <strong>${escapeHtml(draft.merchantName || 'Scanned Receipt')}</strong></p>

        <form id="ocr-review-form">
          <div class="form-group">
            <label class="form-label">Merchant Name</label>
            <input type="text" id="ocr-merchant" class="input-control" value="${escapeHtml(draft.merchantName || '')}" ${isFinalized ? 'disabled' : ''} />
          </div>

          <h4 style="font-size: 14px; font-weight: 600; margin-bottom: 8px;">Extracted Line Items</h4>
          <div id="ocr-items-list">
            ${(draft.lineItems || []).map((item, idx) => `
              <div class="ocr-item-row">
                <input type="text" class="input-control ocr-desc" value="${escapeHtml(item.description)}" placeholder="Description" ${isFinalized ? 'disabled' : ''} />
                <input type="number" class="input-control ocr-qty" value="${item.quantity || 1}" min="1" placeholder="Qty" ${isFinalized ? 'disabled' : ''} />
                <input type="number" class="input-control ocr-total" step="0.01" value="${item.lineTotal || 0}" placeholder="Total ₹" ${isFinalized ? 'disabled' : ''} />
                ${!isFinalized ? `<button type="button" class="btn btn-danger btn-sm btn-remove-ocr" data-idx="${idx}">✕</button>` : ''}
              </div>
            `).join('')}
          </div>

          ${!isFinalized ? `
            <div style="display: flex; gap: 10px; margin-top: 16px;">
              <button type="button" class="btn btn-secondary btn-sm" id="btn-add-ocr-line">+ Add Line Item</button>
              <button type="submit" class="btn btn-primary btn-sm">Save Corrections</button>
            </div>
            <button type="button" class="btn btn-emerald" id="btn-confirm-receipt" style="margin-top: 16px;">
              ✅ Confirm Receipt & Create Bill Items
            </button>
          ` : ''}
        </form>
      </div>
    `;
  }

  function bindEvents(isFinalized) {
    // Tab Switching
    container.querySelectorAll('.tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        activeTab = btn.dataset.tab;
        successMsg = null;
        render();
      });
    });

    if (activeTab === 'items' && !isFinalized) {
      // Add Participant Form
      const partForm = container.querySelector('#add-participant-form');
      if (partForm) {
        partForm.addEventListener('submit', async (e) => {
          e.preventDefault();
          const phone = container.querySelector('#part-phone').value.trim();
          try {
            await api.addParticipant(billId, phone);
            successMsg = `Added participant ${phone}`;
            await loadData();
          } catch (err) {
            alert(`Error adding participant: ${err.message}`);
          }
        });
      }

      // Add Item Form
      const itemForm = container.querySelector('#add-item-form');
      if (itemForm) {
        itemForm.addEventListener('submit', async (e) => {
          e.preventDefault();
          const desc = container.querySelector('#item-desc').value.trim();
          const qty = parseInt(container.querySelector('#item-qty').value);
          const amount = parseFloat(container.querySelector('#item-amount').value);

          try {
            await api.addBillItem(billId, desc, qty, amount, []);
            successMsg = `Added item "${desc}"`;
            await loadData();
          } catch (err) {
            alert(`Error adding item: ${err.message}`);
          }
        });
      }
    }

    if (activeTab === 'receipt') {
      const btnCamera = container.querySelector('#btn-camera');
      const inputCamera = container.querySelector('#input-camera');
      const btnGallery = container.querySelector('#btn-gallery');
      const inputGallery = container.querySelector('#input-gallery');

      if (btnCamera && inputCamera) {
        btnCamera.addEventListener('click', () => inputCamera.click());
        inputCamera.addEventListener('change', (e) => handleFileUpload(e.target.files[0]));
      }

      if (btnGallery && inputGallery) {
        btnGallery.addEventListener('click', () => inputGallery.click());
        inputGallery.addEventListener('change', (e) => handleFileUpload(e.target.files[0]));
      }

      // OCR Review Form & Confirmation
      const ocrForm = container.querySelector('#ocr-review-form');
      if (ocrForm) {
        ocrForm.addEventListener('submit', async (e) => {
          e.preventDefault();
          const merchantName = container.querySelector('#ocr-merchant').value.trim();
          const rows = container.querySelectorAll('.ocr-item-row');
          const lineItems = [];
          rows.forEach(r => {
            const desc = r.querySelector('.ocr-desc').value.trim();
            const qty = parseFloat(r.querySelector('.ocr-qty').value) || 1;
            const lineTotal = parseFloat(r.querySelector('.ocr-total').value) || 0;
            if (desc) lineItems.push({ description: desc, quantity: qty, lineTotal: lineTotal });
          });

          try {
            const updated = await api.updateReceiptDraft(billId, currentReceiptId, {
              merchantName,
              lineItems
            });
            currentReceiptDraft = updated.ocrDraft;
            successMsg = 'OCR corrections saved!';
            render();
          } catch (err) {
            alert(`Error saving OCR corrections: ${err.message}`);
          }
        });

        const btnConfirm = container.querySelector('#btn-confirm-receipt');
        if (btnConfirm) {
          btnConfirm.addEventListener('click', async () => {
            try {
              await api.confirmReceipt(billId, currentReceiptId);
              successMsg = 'Receipt confirmed! Bill items created successfully.';
              currentReceiptDraft = null;
              currentReceiptId = null;
              activeTab = 'items';
              await loadData();
            } catch (err) {
              alert(`Error confirming receipt: ${err.message}`);
            }
          });
        }
      }
    }

    if (activeTab === 'sharers') {
      const selectItem = container.querySelector('#select-item');
      if (selectItem) {
        selectItem.addEventListener('change', (e) => {
          selectedItemId = e.target.value;
          render();
        });
      }

      const sharersForm = container.querySelector('#sharers-form');
      if (sharersForm && !isFinalized) {
        sharersForm.addEventListener('submit', async (e) => {
          e.preventDefault();
          const checked = Array.from(sharersForm.querySelectorAll('input[name="sharer"]:checked')).map(c => c.value);
          try {
            await api.updateItemSharers(billId, selectedItemId, checked);
            successMsg = 'Sharer assignment saved!';
            await loadData();
          } catch (err) {
            alert(`Error updating sharers: ${err.message}`);
          }
        });
      }
    }

    if (activeTab === 'settlement') {
      const btnFinalize = container.querySelector('#btn-finalize-modal');
      if (btnFinalize) {
        btnFinalize.addEventListener('click', async () => {
          if (!confirm('Are you sure you want to finalize this bill? Finalizing makes items immutable and activates participant access links.')) {
            return;
          }
          try {
            await api.finalizeBill(billId);
            successMsg = 'Bill finalized successfully!';
            await loadData();
          } catch (err) {
            alert(`Error finalizing bill: ${err.message}`);
          }
        });
      }

      // Generate / Show access link buttons
      container.querySelectorAll('.btn-get-link').forEach(btn => {
        btn.addEventListener('click', async () => {
          const partId = btn.dataset.partid;
          try {
            const res = await api.generateAccessLink(billId, partId);
            const linkUrl = `${window.location.origin}${window.location.pathname}#/access/${res.token}`;
            showLinkModal(linkUrl);
          } catch (err) {
            alert(`Error generating link: ${err.message}`);
          }
        });
      });
    }
  }

  async function handleFileUpload(file) {
    if (!file) return;
    try {
      const uploadRes = await api.uploadReceipt(billId, file);
      currentReceiptId = uploadRes.receiptId;
      currentReceiptDraft = uploadRes.ocrDraft;
      successMsg = 'Receipt uploaded and OCR processed!';
      render();
    } catch (err) {
      alert(`Error uploading receipt: ${err.message}`);
    }
  }

  function showLinkModal(url) {
    const modal = document.createElement('div');
    modal.className = 'modal-backdrop';
    modal.innerHTML = `
      <div class="modal-card">
        <h3 class="card-title">🔗 Participant Access Link</h3>
        <p class="section-subtitle">Share this direct access link with the participant.</p>
        <input type="text" class="input-control" value="${url}" readonly id="modal-link-input" />
        <div class="modal-actions">
          <button type="button" class="btn btn-primary" id="btn-copy">📋 Copy Link</button>
          <button type="button" class="btn btn-secondary" id="btn-close">Close</button>
        </div>
      </div>
    `;
    document.body.appendChild(modal);

    modal.querySelector('#btn-copy').addEventListener('click', () => {
      const input = modal.querySelector('#modal-link-input');
      input.select();
      navigator.clipboard.writeText(url);
      alert('Link copied to clipboard!');
    });

    modal.querySelector('#btn-close').addEventListener('click', () => {
      document.body.removeChild(modal);
    });
  }

  loadData();
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}
