// ── More Menu ──────────────────────────────────────────────────────────
function toggleMoreMenu() {
  const menu = document.getElementById('more-menu');
  const btn = document.getElementById('more-btn');
  const isHidden = menu.classList.contains('hidden');
  menu.classList.toggle('hidden', !isHidden);
  btn.setAttribute('aria-expanded', isHidden ? 'true' : 'false');
  if (!isHidden) document.body.style.overflow = '';
  else document.body.style.overflow = 'hidden';
}

function closeMoreMenu(event) {
  if (!event || event.target === document.getElementById('more-menu')) {
    document.getElementById('more-menu').classList.add('hidden');
    document.getElementById('more-btn')?.setAttribute('aria-expanded', 'false');
    document.body.style.overflow = '';
  }
}

// ── Confirm Modal ───────────────────────────────────────────────────────
let _confirmCallback = null;

function showConfirm(title, message, onConfirm, dangerLabel = 'حذف') {
  document.getElementById('confirm-title').textContent = title;
  document.getElementById('confirm-message').textContent = message;
  document.getElementById('confirm-ok-btn').textContent = dangerLabel;
  _confirmCallback = onConfirm;
  document.getElementById('confirm-modal').classList.remove('hidden');
  document.body.style.overflow = 'hidden';
}

function confirmAction() {
  // Capture before closeConfirm() nulls _confirmCallback
  const cb = _confirmCallback;
  closeConfirm();
  if (typeof cb === 'function') cb();
}

function closeConfirm() {
  document.getElementById('confirm-modal').classList.add('hidden');
  document.body.style.overflow = '';
  _confirmCallback = null;
}

// Confirm modal close on backdrop click
document.getElementById('confirm-modal')?.addEventListener('click', function(e) {
  if (e.target === this) closeConfirm();
});

// ── Delete Confirm Helper ───────────────────────────────────────────────
// Usage: <button onclick="confirmDelete('حذف المنتج', 'هل أنت متأكد؟', 'form-id')">حذف</button>
function confirmDelete(title, message, formId) {
  showConfirm(title, message, () => {
    document.getElementById(formId)?.submit();
  });
}

// ── Form Submit Confirm Helper ──────────────────────────────────────────
// Usage: <button onclick="confirmSubmit('تأكيد', 'هل أنت متأكد؟', 'form-id', 'تأكيد')">
function confirmSubmit(title, message, formId, label = 'تأكيد') {
  showConfirm(title, message, () => {
    document.getElementById(formId)?.submit();
  }, label);
}

// ── Auto-dismiss alerts after 5s ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  const alerts = document.querySelectorAll('.alert-success, .alert-error, .alert-warning');
  alerts.forEach(alert => {
    setTimeout(() => {
      alert.style.transition = 'opacity 300ms ease';
      alert.style.opacity = '0';
      setTimeout(() => alert.remove(), 300);
    }, 5000);
  });
});

// ── Keyboard: Escape closes overlays ────────────────────────────────────
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    closeMoreMenu();
    closeConfirm();
  }
});

// ── POST helper (for simple forms without a full form element) ──────────
function postTo(url, data = {}) {
  const form = document.createElement('form');
  form.method = 'POST';
  form.action = url;
  form.style.display = 'none';

  // CSRF token
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
    || document.cookie.split('; ').find(r => r.startsWith('X-CSRF-TOKEN'))?.split('=')[1];
  if (token) {
    const csrf = document.createElement('input');
    csrf.type = 'hidden';
    csrf.name = '__RequestVerificationToken';
    csrf.value = token;
    form.appendChild(csrf);
  }

  Object.entries(data).forEach(([key, value]) => {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = key;
    input.value = value;
    form.appendChild(input);
  });

  document.body.appendChild(form);
  form.submit();
}
