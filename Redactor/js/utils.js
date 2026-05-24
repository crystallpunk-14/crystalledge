// ======================================================================
//  SS14 Prototype Redactor – DOM Helpers & Utilities
// ======================================================================

'use strict';

function esc(s) { const d = document.createElement('div'); d.textContent = String(s); return d.innerHTML; }
function _el(tag) { return document.createElement(tag); }
function _div(cls) { const d = document.createElement('div'); if (cls) d.className = cls; return d; }
function _divClass(cls) { const d = document.createElement('div'); d.className = cls; return d; }

// ======================== SMART SEARCH =================================
// Subsequence match: letters of `pattern` must appear in `text` in order
// but not necessarily contiguously. Whitespace in `query` splits it into
// independent tokens that may match in any order. Empty query matches
// everything.
//
// Examples:
//   smartMatch('CEStaminaThrowable', 'throw stam') === true
//   smartMatch('CEStaminaThrowable', 'stmth')      === true
//   smartMatch('CEStaminaThrowable', 'xyz')        === false
//
// This is the single search predicate used by every dropdown, picker,
// file tree, and prototype list in the redactor. Keep it pure & fast.
function smartMatch(text, query) {
    if (!query) return true;
    if (text == null) return false;
    const hay = String(text).toLowerCase();
    const q = String(query).toLowerCase().trim();
    if (!q) return true;
    const tokens = q.split(/\s+/);
    return tokens.every(tok => _isSubsequence(hay, tok));
}

function _isSubsequence(hay, needle) {
    if (!needle) return true;
    let i = 0;
    for (let h = 0; h < hay.length && i < needle.length; h++) {
        if (hay[h] === needle[i]) i++;
    }
    return i === needle.length;
}

// ======================== TOAST ========================================
function toast(msg, type = 'info') {
    const c = document.getElementById('toast-container');
    const t = _div(`toast toast-${type}`); t.textContent = msg;
    c.appendChild(t);
    requestAnimationFrame(() => t.classList.add('visible'));
    setTimeout(() => { t.classList.remove('visible'); setTimeout(() => t.remove(), 300); }, 2200);
}
