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

// ── Math in quantity boxes ───────────────────────────────────────────────
// Any number box accepts a sum: type 1000-200 and it becomes 800 (also +, ×/*, ÷//,
// Arabic digits). The result shows under the box while typing, is put in the box
// when you leave it, and is always worked out before the form is sent.
// Opt out with data-no-calc.
function calcExpr(text) {
  let s = String(text)
    .replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d))
    .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d))
    .replace(/[×xX]/g, '*').replace(/÷/g, '/').replace(/[−–]/g, '-').replace(/٫/g, '.').replace(/,/g, '')
    .replace(/\s+/g, '');
  if (s === '') return null;
  if (!/^[0-9.+\-*/()]+$/.test(s)) return NaN;
  let i = 0;
  const peek = () => s[i];
  function num() {
    if (peek() === '(') { i++; const v = add(); if (peek() !== ')') throw 0; i++; return v; }
    if (peek() === '-') { i++; return -num(); }
    if (peek() === '+') { i++; return num(); }
    const m = /^[0-9]*\.?[0-9]+|^[0-9]+\.?/.exec(s.slice(i));
    if (!m) throw 0;
    i += m[0].length; return parseFloat(m[0]);
  }
  function mul() { let v = num(); while (peek() === '*' || peek() === '/') { const op = s[i++]; const r = num(); v = op === '*' ? v * r : v / r; } return v; }
  function add() { let v = mul(); while (peek() === '+' || peek() === '-') { const op = s[i++]; const r = mul(); v = op === '+' ? v + r : v - r; } return v; }
  try { const v = add(); if (i !== s.length || !isFinite(v)) return NaN; return Math.round(v * 1000) / 1000; }
  catch { return NaN; }
}

function enhanceCalcInput(input) {
  if (input.dataset.calcReady || input.hasAttribute('data-no-calc')) return;
  input.dataset.calcReady = '1';
  const min = input.min !== '' ? parseFloat(input.min) : -Infinity;
  const whole = !input.step || input.step === '1';
  input.type = 'text';                 // a number box can't hold "1000-200"
  input.setAttribute('inputmode', 'text');
  input.setAttribute('autocomplete', 'off');
  input.setAttribute('dir', 'ltr');
  const hint = document.createElement('div');
  hint.className = 'calc-hint'; hint.hidden = true;
  (input.closest('.stepper') || input).insertAdjacentElement('afterend', hint);

  const hasOp = () => /[0-9)]\s*[+\-*/×x÷−]/.test(input.value) || /^\s*\(/.test(input.value);
  let applying = false;
  function check() {
    if (applying) return;               // keep "1000-200 = 800" showing, so nothing below moves
    if (!hasOp()) { hint.hidden = true; input.classList.remove('calc-bad'); return; }
    const v = calcExpr(input.value);
    const bad = v === null || isNaN(v) || v < min || (whole && v !== Math.round(v));
    hint.hidden = false;
    hint.textContent = bad ? (v < min ? 'النتيجة أقل من ' + min : 'العملية غير صحيحة') : '= ' + v.toLocaleString('en-US');
    hint.classList.toggle('bad', bad);
    input.classList.toggle('calc-bad', bad);
  }
  function apply() {
    if (!hasOp()) return true;
    const v = calcExpr(input.value);
    if (v === null || isNaN(v) || v < min || (whole && v !== Math.round(v))) { check(); return false; }
    const expr = input.value.trim();
    input.value = String(v);
    hint.hidden = false; hint.classList.remove('bad'); input.classList.remove('calc-bad');
    hint.textContent = expr + ' = ' + v.toLocaleString('en-US');
    applying = true;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
    applying = false;
    return true;
  }
  input.addEventListener('input', check);
  // A moment later, so a tap on «حفظ» lands before anything on the page moves (the form still works it out on send)
  input.addEventListener('blur', () => setTimeout(apply, 300));
  input.addEventListener('keydown', e => { if (e.key === 'Enter' && hasOp()) { apply(); } });
  const form = input.form;
  if (form) form.addEventListener('submit', e => {
    if (!apply()) { e.preventDefault(); e.stopImmediatePropagation(); input.focus(); }
  }, true);
}

document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('input[type="number"]').forEach(enhanceCalcInput);
});
