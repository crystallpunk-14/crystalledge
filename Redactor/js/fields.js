// ======================================================================
//  SS14 Prototype Redactor – Field Renderers (Controls)
// ======================================================================

'use strict';

/** Ctrl+click helper: find proto by type+id in the index and open its file. */
function navigateToProto(type, id) {
    if (!id || !state.protoIndex) return;
    // type may not always match — scan all types
    const types = type ? [type] : Object.keys(state.protoIndex);
    for (const t of types) {
        const entries = state.protoIndex[t];
        if (!entries) continue;
        const entry = entries.find(e => e.id === id);
        if (entry?.file) { openFile(entry.file); return; }
    }
    toast('Prototype not found in index');
}

/**
 * Build a field row with override tracking.
 * @param {string} key – YAML tag
 * @param {object} meta – field metadata
 * @param {*} value – current effective value
 * @param {string} source – 'local' | 'inherited' | 'default'
 * @param {function} onChange – callback when value changes
 * @param {function|null} onReset – callback to reset (remove) the field from YAML
 */
function fieldRow(key, meta, value, source, onChange, onReset) {
    const isLocal = source === 'local';
    const row = _div('field-row' + (isLocal ? ' field-local' : '') + (!isLocal ? ' inherited' : ''));

    // Left override indicator bar (blue bar for locally-defined fields)
    if (isLocal) {
        const bar = _div('field-override-bar');
        row.appendChild(bar);
    }

    const lbl = _el('label');
    lbl.className = 'field-label' + (meta.required ? ' required' : '') + (isLocal ? ' field-label-bold' : '');
    lbl.textContent = key;
    if (meta.required) {
        const star = _el('span');
        star.className = 'field-required-star';
        star.textContent = '*';
        star.title = 'Required field';
        lbl.appendChild(star);
    }
    const tipParts = [];
    if (meta.summary) tipParts.push(meta.summary);
    tipParts.push(`Type: ${meta.fullType || meta.type || meta.fieldKind || 'unknown'}`);
    if (!isLocal && source === 'inherited') tipParts.push('(inherited from parent)');
    if (!isLocal && source === 'default') tipParts.push('(default value)');
    lbl.title = tipParts.join('\n');
    row.appendChild(lbl);

    const controlWrap = _div('field-control-wrap');
    if (source === 'default' && (value === undefined || value === null)) {
        const ph = _el('span'); ph.className = 'field-default-placeholder'; ph.textContent = '(default)';
        ph.title = 'Click to set a value';
        ph.addEventListener('click', () => onChange(defaultValueForMeta(meta)));
        controlWrap.appendChild(ph);
    } else {
        controlWrap.appendChild(controlFor(meta, value, false, onChange));
    }

    // Reset button for locally-defined fields
    if (isLocal && onReset) {
        const resetBtn = _el('button');
        resetBtn.className = 'field-reset-btn';
        resetBtn.title = 'Reset to inherited / default value';
        resetBtn.textContent = '↺';
        resetBtn.addEventListener('click', e => { e.stopPropagation(); onReset(); });
        controlWrap.appendChild(resetBtn);
    }

    row.appendChild(controlWrap);
    if (typeof decorateFieldValidation === 'function')
        decorateFieldValidation(row, meta, value, source);
    return row;
}

function genericRow(key, value, source, onChange, onReset) {
    const isLocal = source === 'local';
    const row = _div('field-row' + (isLocal ? ' field-local' : '') + (!isLocal ? ' inherited' : ''));

    if (isLocal) {
        const bar = _div('field-override-bar');
        row.appendChild(bar);
    }

    const lbl = _el('label');
    lbl.className = 'field-label' + (isLocal ? ' field-label-bold' : '');
    lbl.textContent = key;
    row.appendChild(lbl);

    const controlWrap = _div('field-control-wrap');
    // `autoControl` is the no-metadata sibling of `controlFor` – same
    // architectural contract: never coerce an object into '[object Object]'.
    // Both ultimately funnel through the same scalar/list/object renderers.
    controlWrap.appendChild(autoControl(value, false, onChange));

    if (isLocal && onReset) {
        const resetBtn = _el('button');
        resetBtn.className = 'field-reset-btn';
        resetBtn.title = 'Reset to inherited / default value';
        resetBtn.textContent = '↺';
        resetBtn.addEventListener('click', e => { e.stopPropagation(); onReset(); });
        controlWrap.appendChild(resetBtn);
    }

    row.appendChild(controlWrap);
    return row;
}

/**
 * Single rendering entry point for every value the editor displays – top
 * level fields, list elements, map values, dataDef inner fields, component
 * fields, all funnel through here. Adding a new control type means adding
 * one switch case; there is no parallel switch elsewhere in the codebase.
 *
 * Robustness contract: regardless of how stale or mismatched the metadata
 * is, this function never falls back to coercing an object value into the
 * literal string "[object Object]". A plain object always lands in either
 * `dataDefCtrl` (when its full type is a known DataDefinition) or
 * `unsupportedStub` (with a raw-YAML escape hatch).
 */
function controlFor(meta, value, dis, onChange) {
    const kind = meta.fieldKind;
    const isObj = value !== null && typeof value === 'object' && !Array.isArray(value);

    // Polymorphic shapes that legitimately accept BOTH scalars and objects
    // (e.g. SoundSpecifier may be a bare path string or a {path, params}
    // mapping) are handled inside their dedicated control – do not divert.
    const polymorphicKinds = new Set(['soundSpecifier', 'spriteSpecifier']);

    // ── Universal object-value guard ────────────────────────────────────
    // If the metadata claims this is a scalar but the actual value is an
    // object, the metadata is stale / mismatched – never let a scalar control
    // stringify it. Route to the best-known structured renderer instead.
    if (isObj && !polymorphicKinds.has(kind)
        && kind !== 'list' && kind !== 'map' && kind !== 'vector2'
        && kind !== 'vector3' && kind !== 'vector4' && kind !== 'box2') {
        const ddType = meta.dataDefinitionType || meta.fullType || meta.type;
        if (ddType && state.metadata?.dataDefinitions?.[ddType])
            return dataDefCtrl(value, ddType, dis, onChange);
        // Last resort: surface the gap and offer raw YAML.
        return unsupportedStub(ddType || 'Object', value, onChange);
    }

    switch (kind) {
        case 'boolean':       return boolCtrl(value, dis, onChange);
        case 'integer':       return intCtrl(value, dis, onChange);
        case 'float':         return floatCtrl(value, dis, onChange);
        case 'text':          return textCtrl(value, dis, onChange);
        case 'color':         return colorCtrl(value, dis, onChange);
        case 'enum':          return enumCtrl(value, meta.enumValues || [], dis, onChange);
        case 'flags':         return flagsCtrl(value, meta.enumValues || [], dis, onChange);
        case 'entityProtoId': return searchDropdown(value, 'entity', dis, onChange);
        case 'protoId':       return searchDropdown(value, meta.protoTypeArg || 'entity', dis, onChange);
        case 'list':          return listCtrl(value, meta, dis, onChange);
        case 'map':           return mapCtrl(value, meta, dis, onChange);
        case 'vector2':       return vectorCtrl(value, ['x', 'y'], dis, onChange);
        case 'vector3':       return vectorCtrl(value, ['x', 'y', 'z'], dis, onChange);
        case 'vector4':       return vectorCtrl(value, ['x', 'y', 'z', 'w'], dis, onChange);
        case 'box2':          return vectorCtrl(value, ['l', 'b', 'r', 't'], dis, onChange);
        case 'spriteSpecifier': return spriteSpecifierCtrl(value, dis, onChange);
        case 'soundSpecifier':  return soundSpecifierCtrl(value, dis, onChange);
        default:
            if (meta.isDataDefinition && meta.dataDefinitionType) return dataDefCtrl(value, meta.dataDefinitionType, dis, onChange);
            return autoControl(value, dis, onChange, meta.fullType || meta.type);
    }
}

/**
 * Render a yellow placeholder for fields whose backing C# type the editor
 * does not yet know how to serialize. Surfaces the underlying type so the
 * user (and we) can see exactly which serializer is missing.
 *
 * `value` and `onChange` are optional – when provided, the stub exposes an
 * "Open raw YAML" toggle so power users can still hand-edit the value.
 */
function unsupportedStub(typeName, value, onChange) {
    const w = _div('field-control');
    const box = _div('field-unsupported-stub');
    box.innerHTML = `TODO: Serialize <b>[${esc(String(typeName || 'unknown'))}]</b>`;
    w.appendChild(box);

    if (typeof onChange === 'function') {
        const btn = _el('button');
        btn.type = 'button';
        btn.className = 'field-unsupported-raw-btn';
        btn.textContent = 'Open raw YAML';
        btn.title = 'Edit the underlying YAML by hand. Dangerous – no validation.';
        box.appendChild(btn);

        const ta = _el('textarea');
        ta.className = 'field-textarea field-unsupported-raw';
        ta.style.display = 'none';
        const yamlStr = (value === undefined || value === null) ? '' : dumpYaml(value).trim();
        ta.value = yamlStr;
        ta.rows = Math.min(Math.max(yamlStr.split('\n').length, 3), 20);
        ta.addEventListener('change', () => {
            try {
                const p = jsyaml.load(ta.value, { schema: SCHEMA });
                ta.classList.remove('error');
                onChange(p);
            } catch { ta.classList.add('error'); }
        });
        btn.addEventListener('click', () => {
            const open = ta.style.display === '';
            ta.style.display = open ? 'none' : '';
            btn.textContent = open ? 'Open raw YAML' : 'Close raw YAML';
            if (!open) ta.focus();
        });
        w.appendChild(ta);
    }
    return w;
}

function boolCtrl(val, dis, cb) {
    const w = _div('field-control');
    const lbl = _el('label'); lbl.className = 'toggle-switch';
    const inp = _el('input'); inp.type = 'checkbox'; inp.checked = !!val; inp.disabled = dis;
    const sl = _el('span'); sl.className = 'toggle-slider';
    lbl.append(inp, sl);
    const txt = _el('span'); txt.className = 'toggle-label'; txt.textContent = inp.checked ? 'true' : 'false';
    inp.addEventListener('change', () => { txt.textContent = inp.checked ? 'true' : 'false'; cb(inp.checked); });
    // YAML look: textual value (true/false) FIRST, switch AFTER — so it
    // reads "enabled: true [⏻]" rather than "[⏻] true".
    w.append(txt, lbl); return w;
}

function intCtrl(val, dis, cb) {
    const w = _div('field-control');
    const inp = _el('input'); inp.type = 'number'; inp.className = 'field-input number-input';
    inp.value = val != null ? val : ''; inp.step = '1'; inp.disabled = dis;
    inp.addEventListener('change', () => { const n = parseInt(inp.value); if (!isNaN(n)) cb(n); });
    inp.addEventListener('keydown', e => {
        if (!/^[0-9-]$/.test(e.key) && !['Backspace','Delete','ArrowLeft','ArrowRight','Tab','Home','End'].includes(e.key) && !e.ctrlKey) e.preventDefault();
    });
    w.appendChild(inp); return w;
}

function floatCtrl(val, dis, cb) {
    const w = _div('field-control');
    const inp = _el('input'); inp.type = 'number'; inp.className = 'field-input number-input';
    inp.value = val != null ? val : ''; inp.step = 'any'; inp.disabled = dis;
    inp.addEventListener('change', () => { const n = parseFloat(inp.value); if (!isNaN(n)) cb(n); });
    w.appendChild(inp); return w;
}

function textCtrl(val, dis, cb) {
    const w = _div('field-control');
    const inp = _el('input'); inp.type = 'text'; inp.className = 'field-input';
    inp.value = val != null ? String(val) : ''; inp.disabled = dis;
    inp.addEventListener('change', () => cb(inp.value));
    w.appendChild(inp); return w;
}

function colorCtrl(val, dis, cb) {
    const w = _div('field-control color-control');
    const cp = _el('input'); cp.type = 'color'; cp.className = 'color-picker';
    cp.value = typeof val === 'string' && val.startsWith('#') ? val.slice(0, 7) : '#ffffff'; cp.disabled = dis;
    const tx = _el('input'); tx.type = 'text'; tx.className = 'field-input color-text';
    tx.value = typeof val === 'string' ? val : ''; tx.disabled = dis;
    cp.addEventListener('input', () => { tx.value = cp.value; });
    cp.addEventListener('change', () => cb(cp.value));
    tx.addEventListener('change', () => { try { cp.value = tx.value; } catch {} cb(tx.value); });
    // YAML look: text value first, picker swatch after — keeps the textual
    // hex token in line with other scalar inputs; the swatch is a tiny
    // accessory on the right.
    w.append(tx, cp); return w;
}

function enumCtrl(val, opts, dis, cb) {
    const w = _div('field-control');
    const sel = _el('select'); sel.className = 'field-select'; sel.disabled = dis;
    sel.innerHTML = `<option value="">-- select --</option>` + opts.map(o => `<option value="${esc(o)}"${o === val ? ' selected' : ''}>${esc(o)}</option>`).join('');
    sel.addEventListener('change', () => cb(sel.value));
    w.appendChild(sel); return w;
}

function flagsCtrl(val, opts, dis, cb) {
    // Parse current value: could be comma-separated string or array
    let selected = new Set();
    if (Array.isArray(val)) val.forEach(v => selected.add(String(v)));
    else if (typeof val === 'string' && val) val.split(',').map(s => s.trim()).filter(Boolean).forEach(v => selected.add(v));

    const w = _div('field-control flags-control');
    const toggle = _el('button'); toggle.className = 'flags-toggle'; toggle.disabled = dis;
    toggle.type = 'button';
    function updateLabel() {
        const sel = [...selected].filter(f => f !== 'NONE' && f !== 'None' && f !== '0');
        toggle.textContent = sel.length ? sel.join(', ') : '(none)';
    }
    updateLabel();

    const dd = _div('flags-dropdown');
    // Filter out NONE/0 from options
    const validOpts = opts.filter(o => o !== 'NONE' && o !== 'None' && o !== '0');
    for (const o of validOpts) {
        const row = _el('label'); row.className = 'flags-option';
        const chk = _el('input'); chk.type = 'checkbox'; chk.checked = selected.has(o); chk.disabled = dis;
        const lbl = _el('span'); lbl.textContent = o;
        chk.addEventListener('change', () => {
            if (chk.checked) selected.add(o); else selected.delete(o);
            updateLabel();
            const arr = [...selected].filter(f => f !== 'NONE' && f !== 'None' && f !== '0');
            cb(arr.length ? arr : 'None');
        });
        row.append(chk, lbl);
        dd.appendChild(row);
    }

    toggle.addEventListener('click', e => {
        e.stopPropagation();
        dd.classList.toggle('visible');
    });
    // Close dropdown when clicking outside
    document.addEventListener('click', e => {
        if (!w.contains(e.target)) dd.classList.remove('visible');
    });
    w.append(toggle, dd); return w;
}

// resPathAutocomplete + spriteSpecifierCtrl moved to field-controls-paths.js

function vectorCtrl(val, axes, dis, cb) {
    const w = _div('field-control vector-control');
    // Parse value: could be "x, y" string or {x:1, y:2} object
    const parts = {};
    if (typeof val === 'string' && val.includes(',')) {
        const nums = val.split(',').map(s => s.trim());
        axes.forEach((a, i) => { parts[a] = nums[i] || '0'; });
    } else if (val && typeof val === 'object') {
        axes.forEach(a => { parts[a] = val[a] != null ? String(val[a]) : '0'; });
    } else {
        axes.forEach(a => { parts[a] = '0'; });
    }
    function emit() {
        const result = axes.map(a => parts[a] || '0').join(', ');
        cb(result);
    }
    for (const a of axes) {
        const g = _div('vector-axis');
        const lbl = _el('span'); lbl.className = 'vector-axis-label'; lbl.textContent = a.toUpperCase();
        const inp = _el('input'); inp.type = 'number'; inp.step = 'any';
        inp.className = 'field-input vector-input'; inp.value = parts[a]; inp.disabled = dis;
        inp.addEventListener('change', () => { parts[a] = inp.value; emit(); });
        g.append(lbl, inp);
        w.appendChild(g);
    }
    return w;
}

function searchDropdown(val, searchType, dis, cb) {
    const w = _div('field-control search-dropdown');
    const inp = _el('input'); inp.type = 'text'; inp.className = 'field-input dropdown-input';
    inp.value = val != null ? String(val) : ''; inp.disabled = dis;
    inp.placeholder = 'Search prototypes…'; inp.autocomplete = 'off';
    const dd = _div('dropdown-list');
    let timer, selIdx = -1;
    async function doSearch(q) {
        try {
            // Server search is substring-only. To make every dropdown
            // share the same smart-search behaviour (subsequence + multi-
            // token), we send the FIRST token as the server hint (so the
            // returned set contains everything that contains that token
            // anywhere), then refine on the client with smartMatch using
            // the full query. Result: "throw stam" matches
            // "CEStaminaThrowable" without any server changes.
            const tokens = String(q || '').trim().split(/\s+/).filter(Boolean);
            const serverHint = tokens[0] || '';
            const res = await api.searchProtos(searchType, serverHint);
            const refined = tokens.length > 1
                ? res.filter(r => smartMatch(r.id, q) || smartMatch(r.name || '', q))
                : res;
            renderDd(dd, refined, inp, cb);
            dd.classList.add('visible');
            selIdx = -1;
        } catch (e) { console.error('[Fields] Prototype search failed:', e); }
    }
    inp.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => doSearch(inp.value), CFG.searchDebounce); });
    inp.addEventListener('focus', () => doSearch(inp.value));
    inp.addEventListener('blur', () => setTimeout(() => dd.classList.remove('visible'), 180));
    inp.addEventListener('change', () => { if (inp.value) cb(inp.value); });
    inp.addEventListener('contextmenu', e => {
        if (inp.value) {
            e.preventDefault();
            showContextMenu(e.clientX, e.clientY, [
                { label: `Open "${inp.value}" in editor`, action: () => navigateToProto(searchType, inp.value) },
            ]);
        }
    });
    inp.addEventListener('keydown', e => {
        const items = dd.querySelectorAll('.dropdown-item');
        if (e.key === 'ArrowDown') { e.preventDefault(); selIdx = Math.min(selIdx + 1, items.length - 1); hlDd(items, selIdx); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); selIdx = Math.max(selIdx - 1, 0); hlDd(items, selIdx); }
        else if (e.key === 'Enter' && selIdx >= 0 && items[selIdx]) { e.preventDefault(); items[selIdx].click(); }
        else if (e.key === 'Escape') dd.classList.remove('visible');
    });
    w.append(inp, dd); return w;
}

function renderDd(dd, results, inp, cb) {
    dd.innerHTML = '';
    if (!results.length) { dd.innerHTML = '<div class="dropdown-empty">No results</div>'; return; }
    for (const r of results) {
        const el = _div('dropdown-item');
        el.innerHTML = `<span class="dropdown-id">${esc(r.id)}</span>`;
        el.addEventListener('mousedown', e => { e.preventDefault(); inp.value = r.id; dd.classList.remove('visible'); cb(r.id); });
        dd.appendChild(el);
    }
}

function hlDd(items, idx) { items.forEach((el, i) => el.classList.toggle('selected', i === idx)); if (items[idx]) items[idx].scrollIntoView({ block: 'nearest' }); }

// listCtrl / mapCtrl / dataDefCtrl moved to field-controls-collections.js

// ======================== ELEMENT HELPERS ===============================
/**
 * Build a synthetic field meta describing a single *element* of a list, a
 * single *value* of a map, or any nested value that doesn't have its own
 * `field` entry from the C# extractor. This is the cornerstone of the
 * "one rendering pipeline" rule: every nested value – no matter how deep –
 * is rendered by `controlFor(meta, …)`, never by a parallel switch.
 *
 * @param {string|undefined} kind  – fieldKind (e.g. 'soundSpecifier', 'integer')
 * @param {string|undefined} fullType – C# full type name, used to look up DataDefinitions
 * @param {string|undefined} protoArg – protoTypeArg for protoId fields
 */
function synthMeta(kind, fullType, protoArg, extras) {
    const isDD = !!(fullType && state.metadata?.dataDefinitions?.[fullType]);
    const m = {
        fieldKind: kind || (isDD ? 'object' : 'text'),
        type: fullType,
        fullType,
        protoTypeArg: protoArg,
        isDataDefinition: isDD,
        dataDefinitionType: isDD ? fullType : null,
        required: false,
    };
    if (extras) Object.assign(m, extras);
    return m;
}

/**
 * Render a nested element using the single unified pipeline. List and map
 * editors call this for every element/value so behaviour is identical to a
 * top-level field of the same type.
 */
function elementControl(kind, fullType, protoArg, val, dis, cb, extras) {
    return controlFor(synthMeta(kind, fullType, protoArg, extras), val, dis, cb);
}

function defaultForKind(kind) {
    switch (kind) {
        case 'boolean': return false;
        case 'integer': return 0;
        case 'float':   return 0.0;
        case 'text': case 'entityProtoId': case 'protoId': case 'color': return '';
        case 'vector2': return '0, 0';
        case 'vector3': return '0, 0, 0';
        case 'vector4': return '0, 0, 0, 0';
        case 'box2':    return '0, 0, 0, 0';
        case 'spriteSpecifier': return { sprite: '', state: '' };
        case 'soundSpecifier': return { path: '' };
        case 'object':  return {};
        default: return '';
    }
}

function defaultValueForMeta(meta) {
    return defaultForKind(meta.fieldKind);
}

function autoControl(val, dis, cb, typeHint) {
    if (val === null || val === undefined) return textCtrl('', dis, cb);
    if (typeof val === 'boolean') return boolCtrl(val, dis, cb);
    if (typeof val === 'number') return Number.isInteger(val) ? intCtrl(val, dis, cb) : floatCtrl(val, dis, cb);
    if (typeof val === 'string') return textCtrl(val, dis, cb);
    if (Array.isArray(val)) {
        return listCtrl(val, { elementKind: inferKindFromArray(val), elementFullType: null, elementProtoTypeArg: null }, dis, cb);
    }
    // Object value with no known schema – the editor can't safely round-trip
    // these. Surface a yellow stub pointing at the type so the gap is visible,
    // with an opt-in raw-YAML escape hatch for power users.
    return unsupportedStub(typeHint || 'Object', val, cb);
}

function inferKindFromArray(arr) {
    if (!arr.length) return 'text';
    const first = arr[0];
    if (typeof first === 'boolean') return 'boolean';
    if (typeof first === 'number') return Number.isInteger(first) ? 'integer' : 'float';
    if (typeof first === 'object' && first !== null) return 'object';
    return 'text';
}
