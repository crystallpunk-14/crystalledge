// ======================================================================
//  SS14 Prototype Redactor – Field Validation
// ======================================================================
//  Provides best-effort, client-side validation of field values against
//  the metadata extracted from C# code.  Validation is **non-blocking**:
//  errors decorate the UI but never prevent saving (the YAML linter is
//  the authoritative source of truth at build time).
//
//  Severity levels:
//    'error'   – almost certainly broken (e.g. missing required field,
//                unknown ProtoId).
//    'warning' – probably a mistake (e.g. NaN in a numeric field).
//
//  Each rule returns { severity, message } or null.
// ======================================================================

'use strict';

/**
 * Run all applicable rules for a single field.
 * @returns {Array<{severity:'error'|'warning', message:string}>}
 */
function validateField(meta, value, source) {
    const issues = [];
    if (!meta) return issues;

    const isMissing = value === undefined || value === null
        || (typeof value === 'string' && value === '');

    // Required field check.
    //
    // A required field is satisfied only by an *explicit* value somewhere
    // in the inheritance chain. `source === 'default'` means the field
    // fell back to the type's default (e.g. null/0/empty string) because
    // nothing in the prototype tree set it — this is still a missing
    // required field from the engine's perspective and must be flagged.
    if (meta.required) {
        if (source === 'default' || isMissing) {
            issues.push({ severity: 'error', message: 'Required field is empty' });
        }
    }

    if (isMissing) return issues;

    // Numeric sanity
    if (meta.fieldKind === 'integer') {
        const n = Number(value);
        if (!Number.isFinite(n) || !Number.isInteger(n)) {
            issues.push({ severity: 'warning', message: `Not an integer: ${value}` });
        }
    } else if (meta.fieldKind === 'float') {
        const n = Number(value);
        if (!Number.isFinite(n)) {
            issues.push({ severity: 'warning', message: `Not a number: ${value}` });
        }
    }

    // ProtoId existence
    const protoIssue = validateProtoIdValue(meta, value);
    if (protoIssue) issues.push(protoIssue);

    return issues;
}

function validateProtoIdValue(meta, value) {
    if (typeof value !== 'string' || !value) return null;
    if (meta.fieldKind === 'entityProtoId') {
        return checkProtoExists('entity', value);
    }
    if (meta.fieldKind === 'protoId' && meta.protoTypeArg) {
        return checkProtoExists(meta.protoTypeArg, value);
    }
    return null;
}

function checkProtoExists(type, id) {
    const idx = state.protoIndex;
    if (!idx) return null; // index not loaded yet — silent
    // Try exact type first, then fall back to scanning every type since
    // ProtoId<T>'s T isn't always the literal index key (subtype aliases).
    const entries = idx[type];
    if (entries && entries.some(e => e.id === id)) return null;
    for (const k of Object.keys(idx)) {
        if (idx[k].some(e => e.id === id)) return null;
    }
    return { severity: 'error', message: `Unknown ${type} prototype: "${id}"` };
}

/**
 * Decorate a field row DOM node with validation styling/tooltips.
 * Safe to call multiple times — clears previous decoration first.
 */
function decorateFieldValidation(rowEl, meta, value, source) {
    if (!rowEl) return;
    rowEl.classList.remove('field-invalid', 'field-warning');
    const old = rowEl.querySelector(':scope > .field-validation-badge');
    if (old) old.remove();

    const issues = validateField(meta, value, source);
    if (!issues.length) return;

    const hasError = issues.some(i => i.severity === 'error');
    rowEl.classList.add(hasError ? 'field-invalid' : 'field-warning');

    const badge = document.createElement('span');
    badge.className = 'field-validation-badge ' + (hasError ? 'badge-error' : 'badge-warning');
    badge.textContent = hasError ? '!' : '?';
    badge.title = issues.map(i => `[${i.severity}] ${i.message}`).join('\n');
    rowEl.appendChild(badge);
}
