// ======================================================================
//  SS14 Prototype Redactor – DOM Helpers & Utilities
// ======================================================================

'use strict';

function esc(s) { const d = document.createElement('div'); d.textContent = String(s); return d.innerHTML; }
function _el(tag) { return document.createElement(tag); }
function _div(cls) { const d = document.createElement('div'); if (cls) d.className = cls; return d; }
function _divClass(cls) { const d = document.createElement('div'); d.className = cls; return d; }

// ======================== TOAST ========================================
function toast(msg, type = 'info') {
    const c = document.getElementById('toast-container');
    const t = _div(`toast toast-${type}`); t.textContent = msg;
    c.appendChild(t);
    requestAnimationFrame(() => t.classList.add('visible'));
    setTimeout(() => { t.classList.remove('visible'); setTimeout(() => t.remove(), 300); }, 2200);
}
