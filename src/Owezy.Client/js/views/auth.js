import { api, authState } from '../api.js';

export function renderAuthView(container, { error, info } = {}) {
  let step = 'request'; // 'request' | 'verify'
  let phoneNumber = '';
  let challengeId = '';

  function render() {
    container.innerHTML = `
      <div class="view-container">
        <div class="card" style="margin-top: 40px; text-align: center;">
          <div class="brand-icon" style="margin: 0 auto 16px auto; width: 48px; height: 48px; font-size: 24px;">O</div>
          <h2 style="font-size: 22px; font-weight: 700; margin-bottom: 6px;">Welcome to Owezy</h2>
          <p class="section-subtitle" style="margin-bottom: 24px;">Simple, lightweight bill splitting for everyone.</p>

          ${error ? `<div class="toast-banner toast-error" style="margin-bottom: 16px;">⚠️ ${error}</div>` : ''}
          ${info ? `<div class="toast-banner toast-success" style="margin-bottom: 16px;">ℹ️ ${info}</div>` : ''}

          ${step === 'request' ? `
            <form id="otp-request-form">
              <div class="form-group" style="text-align: left;">
                <label class="form-label" for="phone">Your Mobile Number</label>
                <input 
                  type="tel" 
                  id="phone" 
                  class="input-control" 
                  placeholder="+919876543210" 
                  value="${phoneNumber}"
                  required 
                />
              </div>
              <button type="submit" class="btn btn-primary" id="btn-submit">
                <span>Send OTP Code</span>
              </button>
            </form>
          ` : `
            <form id="otp-verify-form">
              <p class="section-subtitle" style="margin-bottom: 12px; color: var(--text-main);">
                Verification code sent to <strong>${phoneNumber}</strong>
              </p>
              <div class="form-group" style="text-align: left;">
                <label class="form-label" for="code">6-Digit Verification Code</label>
                <input 
                  type="text" 
                  id="code" 
                  class="input-control" 
                  placeholder="123456" 
                  maxlength="6"
                  required 
                  autofocus
                />
                <span class="section-subtitle" style="font-size: 11px; margin-top: 4px;">In development mode, check server logs or use any 6-digit code.</span>
              </div>
              <button type="submit" class="btn btn-primary" id="btn-verify">
                <span>Verify & Login</span>
              </button>
              <button type="button" class="btn btn-secondary" id="btn-back" style="margin-top: 10px;">
                <span>Change Mobile Number</span>
              </button>
            </form>
          `}
        </div>
      </div>
    `;

    if (step === 'request') {
      const form = container.querySelector('#otp-request-form');
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const phoneInput = container.querySelector('#phone').value.trim();
        if (!phoneInput) return;

        const btn = container.querySelector('#btn-submit');
        btn.disabled = true;
        btn.innerHTML = `<div class="spinner"></div><span>Sending OTP...</span>`;

        try {
          const res = await api.requestOtp(phoneInput);
          phoneNumber = phoneInput;
          challengeId = res.challengeId;
          step = 'verify';
          render();
        } catch (err) {
          renderAuthView(container, { error: err.message });
        }
      });
    } else {
      const form = container.querySelector('#otp-verify-form');
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const codeInput = container.querySelector('#code').value.trim();
        if (!codeInput) return;

        const btn = container.querySelector('#btn-verify');
        btn.disabled = true;
        btn.innerHTML = `<div class="spinner"></div><span>Verifying...</span>`;

        try {
          const res = await api.verifyOtp(phoneNumber, codeInput);
          authState.setAuth(res.accessToken, phoneNumber);
          window.location.hash = '#/';
        } catch (err) {
          renderAuthView(container, { error: err.message });
        }
      });

      const btnBack = container.querySelector('#btn-back');
      btnBack.addEventListener('click', () => {
        step = 'request';
        render();
      });
    }
  }

  render();
}
