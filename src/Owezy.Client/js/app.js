import { authState } from './api.js';
import { renderAuthView } from './views/auth.js';
import { renderDashboardView } from './views/dashboard.js';
import { renderWorkspaceView } from './views/workspace.js';
import { renderParticipantView } from './views/participant.js';

function renderHeader(headerContainer) {
  const isAuth = authState.isAuthenticated();
  const phone = authState.getPhone();

  headerContainer.innerHTML = `
    <div class="brand" style="cursor: pointer;" onclick="window.location.hash='#/'">
      <div class="brand-icon">O</div>
      <span class="brand-title">Owezy</span>
    </div>
    ${isAuth ? `
      <div class="user-badge">
        <span>📱 ${escapeHtml(phone || '')}</span>
        <button class="btn-logout" id="btn-logout-head">Logout</button>
      </div>
    ` : ''}
  `;

  const btnLogout = headerContainer.querySelector('#btn-logout-head');
  if (btnLogout) {
    btnLogout.addEventListener('click', () => {
      authState.clearAuth();
      window.location.hash = '#/auth';
    });
  }
}

function router() {
  const headerContainer = document.querySelector('#header-container');
  const viewContainer = document.querySelector('#view-container');
  const hash = window.location.hash || '#/';

  renderHeader(headerContainer);

  // Route: Participant Portal access (#/access/:token) — Anonymous allowed!
  if (hash.startsWith('#/access/')) {
    const token = hash.replace('#/access/', '');
    renderParticipantView(viewContainer, token);
    return;
  }

  // Auth Guard for Splitter routes
  if (!authState.isAuthenticated()) {
    renderAuthView(viewContainer);
    return;
  }

  if (hash === '#/auth') {
    window.location.hash = '#/';
    return;
  }

  // Route: Splitter Workspace (#/bills/:id)
  if (hash.startsWith('#/bills/')) {
    const billId = hash.replace('#/bills/', '');
    renderWorkspaceView(viewContainer, billId);
    return;
  }

  // Route: Dashboard (#/)
  renderDashboardView(viewContainer);
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

window.addEventListener('hashchange', router);
window.addEventListener('DOMContentLoaded', router);
