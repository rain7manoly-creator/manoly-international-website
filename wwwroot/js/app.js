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

// ── Number steppers (− / +) ─────────────────────────────────────────────
// Every <input type="number" class="form-input"> gets − and + buttons so
// quantities can be changed by tapping, not only by typing. Opt out with
// data-no-stepper. Holding a button repeats. Fires "input" + "change" so any
// page script listening to the field (previews, totals) stays in sync.
function enhanceNumberInput(input) {
  if (input.dataset.stepperReady || input.hasAttribute('data-no-stepper')) return;
  input.dataset.stepperReady = '1';

  const wrap = document.createElement('div');
  wrap.className = 'stepper';
  input.parentNode.insertBefore(wrap, input);

  const minus = document.createElement('button');
  minus.type = 'button';
  minus.className = 'stepper-btn';
  minus.setAttribute('aria-label', 'إنقاص');
  minus.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M5 12h14"/></svg>';

  const plus = document.createElement('button');
  plus.type = 'button';
  plus.className = 'stepper-btn';
  plus.setAttribute('aria-label', 'زيادة');
  plus.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>';

  // RTL: the first child sits on the right. Put "+" on the right, "−" on the left,
  // the same order as a phone keypad reads for Arabic users.
  wrap.appendChild(plus);
  wrap.appendChild(input);
  wrap.appendChild(minus);
  input.classList.add('stepper-input');

  const min = () => (input.min !== '' ? parseInt(input.min, 10) : -Infinity);
  const max = () => (input.max !== '' ? parseInt(input.max, 10) : Infinity);

  function refresh() {
    const v = parseInt(input.value, 10);
    minus.disabled = !isNaN(v) && v <= min();
    plus.disabled = !isNaN(v) && v >= max();
  }

  function step(dir) {
    let v = parseInt(input.value, 10);
    if (isNaN(v)) v = dir > 0 ? Math.max(min(), 1) : Math.max(min(), 0); // empty field: start from 1
    else v += dir;
    v = Math.min(max(), Math.max(min(), v));
    if (String(v) === input.value) return refresh();
    input.value = v;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
    refresh();
  }

  // Tap = one step; press-and-hold = repeat (useful for big corrections)
  [[minus, -1], [plus, 1]].forEach(([btn, dir]) => {
    let holdTimer = null, repeatTimer = null;
    const stop = () => { clearTimeout(holdTimer); clearInterval(repeatTimer); holdTimer = repeatTimer = null; };
    btn.addEventListener('pointerdown', (e) => {
      if (btn.disabled) return;
      e.preventDefault();
      step(dir);
      holdTimer = setTimeout(() => { repeatTimer = setInterval(() => step(dir), 90); }, 450);
    });
    ['pointerup', 'pointerleave', 'pointercancel'].forEach(ev => btn.addEventListener(ev, stop));
    // Keyboard users: Enter/Space trigger click without a pointerdown
    btn.addEventListener('click', (e) => { if (e.detail === 0) step(dir); });
  });

  input.addEventListener('input', refresh);
  refresh();
}

document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('input[type="number"].form-input').forEach(enhanceNumberInput);
});

// ── Recent searches ─────────────────────────────────────────────────────
// Kept on this device only (localStorage). Wrapped in try/catch because
// private-browsing modes can block storage — search must still work.
const RECENT_KEY = 'manoly.recentSearches';
const RECENT_MAX = 8;

function readRecentSearches() {
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) || '[]').filter(s => typeof s === 'string'); }
  catch { return []; }
}

function saveRecentSearch(q) {
  q = (q || '').trim();
  if (!q) return;
  try {
    const list = readRecentSearches().filter(s => s !== q);
    list.unshift(q);
    localStorage.setItem(RECENT_KEY, JSON.stringify(list.slice(0, RECENT_MAX)));
  } catch { /* storage unavailable */ }
}

function removeRecentSearch(q) {
  try { localStorage.setItem(RECENT_KEY, JSON.stringify(readRecentSearches().filter(s => s !== q))); }
  catch { }
}

function clearRecentSearches() {
  try { localStorage.removeItem(RECENT_KEY); } catch { }
}
