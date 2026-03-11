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
    const tipParts = [];
    if (meta.summary) tipParts.push(meta.summary);
    tipParts.push(`Type: ${meta.fullType || meta.type || meta.fieldKind || 'unknown'}`);
    if (!isLocal && source === 'inherited') tipParts.push('(inherited from parent)');
    if (!isLocal && source === 'default') tipParts.push('(default value)');
    lbl.title = tipParts.join('\n');
    row.appendChild(lbl);

    const controlWrap = _div('field-control-wrap');
    controlWrap.appendChild(controlFor(meta, value, false, onChange));

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

function controlFor(meta, value, dis, onChange) {
    switch (meta.fieldKind) {
        case 'boolean':       return boolCtrl(value, dis, onChange);
        case 'integer':       return intCtrl(value, dis, onChange);
        case 'float':         return floatCtrl(value, dis, onChange);
        case 'text':          return textCtrl(value, dis, onChange);
        case 'color':         return colorCtrl(value, dis, onChange);
        case 'enum':          return enumCtrl(value, meta.enumValues || [], dis, onChange);
        case 'entityProtoId': return searchDropdown(value, 'entity', dis, onChange);
        case 'protoId':       return searchDropdown(value, meta.protoTypeArg || 'entity', dis, onChange);
        case 'list':          return listCtrl(value, meta, dis, onChange);
        case 'map':           return mapCtrl(value, meta, dis, onChange);
        case 'vector2':       return vectorCtrl(value, ['x', 'y'], dis, onChange);
        case 'vector3':       return vectorCtrl(value, ['x', 'y', 'z'], dis, onChange);
        case 'vector4':       return vectorCtrl(value, ['x', 'y', 'z', 'w'], dis, onChange);
        case 'box2':          return vectorCtrl(value, ['l', 'b', 'r', 't'], dis, onChange);
        default:
            if (meta.isDataDefinition && meta.dataDefinitionType) return dataDefCtrl(value, meta.dataDefinitionType, dis, onChange);
            return autoControl(value, dis, onChange);
    }
}

function boolCtrl(val, dis, cb) {
    const w = _div('field-control');
    const lbl = _el('label'); lbl.className = 'toggle-switch';
    const inp = _el('input'); inp.type = 'checkbox'; inp.checked = !!val; inp.disabled = dis;
    const sl = _el('span'); sl.className = 'toggle-slider';
    lbl.append(inp, sl);
    const txt = _el('span'); txt.className = 'toggle-label'; txt.textContent = inp.checked ? 'true' : 'false';
    inp.addEventListener('change', () => { txt.textContent = inp.checked ? 'true' : 'false'; cb(inp.checked); });
    w.append(lbl, txt); return w;
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
    w.append(cp, tx); return w;
}

function enumCtrl(val, opts, dis, cb) {
    const w = _div('field-control');
    const sel = _el('select'); sel.className = 'field-select'; sel.disabled = dis;
    sel.innerHTML = `<option value="">-- select --</option>` + opts.map(o => `<option value="${esc(o)}"${o === val ? ' selected' : ''}>${esc(o)}</option>`).join('');
    sel.addEventListener('change', () => cb(sel.value));
    w.appendChild(sel); return w;
}

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
            const res = await api.searchProtos(searchType, q);
            renderDd(dd, res, inp, cb);
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
        el.innerHTML = `<span class="dropdown-id">${esc(r.id)}</span>${r.name ? `<span class="dropdown-name">${esc(r.name)}</span>` : ''}`;
        el.addEventListener('mousedown', e => { e.preventDefault(); inp.value = r.id; dd.classList.remove('visible'); cb(r.id); });
        dd.appendChild(el);
    }
}

function hlDd(items, idx) { items.forEach((el, i) => el.classList.toggle('selected', i === idx)); if (items[idx]) items[idx].scrollIntoView({ block: 'nearest' }); }

// ======================== LIST EDITOR ==================================
function listCtrl(val, meta, dis, onChange) {
    const arr = Array.isArray(val) ? [...val] : [];
    const w = _div('field-control list-editor');

    function rebuild() {
        w.innerHTML = '';
        arr.forEach((item, i) => {
            const row = _div('list-item');
            const content = _div('list-item-content');
            content.appendChild(elementControl(meta.elementKind, meta.elementFullType, meta.elementProtoTypeArg, item, dis, nv => {
                arr[i] = nv; onChange([...arr]);
            }));
            row.appendChild(content);
            if (!dis) {
                const rm = _el('button'); rm.className = 'item-remove-btn'; rm.textContent = '×'; rm.title = 'Remove';
                rm.addEventListener('click', () => { arr.splice(i, 1); onChange([...arr]); rebuild(); });
                row.appendChild(rm);
            }
            w.appendChild(row);
        });
        if (!dis) {
            const addRow = _div('list-add-row');
            const addBtn = _el('button'); addBtn.className = 'list-add-btn'; addBtn.textContent = '+ Add item';
            addBtn.addEventListener('click', () => { arr.push(defaultForKind(meta.elementKind)); onChange([...arr]); rebuild(); });
            addRow.appendChild(addBtn);
            w.appendChild(addRow);
        }
    }
    rebuild();
    return w;
}

// ======================== MAP EDITOR ===================================
function mapCtrl(val, meta, dis, onChange) {
    const obj = (val && typeof val === 'object' && !Array.isArray(val)) ? { ...val } : {};
    const w = _div('field-control map-editor');

    function rebuild() {
        w.innerHTML = '';
        for (const [k, v] of Object.entries(obj)) {
            const row = _div('map-entry');
            const keyLabel = _div('map-key-label'); keyLabel.textContent = k;
            row.appendChild(keyLabel);
            const content = _div('map-entry-content');
            content.appendChild(elementControl(meta.valueKind, meta.valueFullType, meta.valueProtoTypeArg, v, dis, nv => {
                obj[k] = nv; onChange({ ...obj });
            }));
            row.appendChild(content);
            if (!dis) {
                const rm = _el('button'); rm.className = 'entry-remove-btn'; rm.textContent = '×'; rm.title = 'Remove';
                rm.addEventListener('click', () => { delete obj[k]; onChange({ ...obj }); rebuild(); });
                row.appendChild(rm);
            }
            w.appendChild(row);
        }
        if (!dis) {
            const addRow = _div('map-add-row');
            const keyInp = _el('input'); keyInp.type = 'text'; keyInp.className = 'field-input'; keyInp.placeholder = 'key'; keyInp.style.maxWidth = '120px';
            const addBtn = _el('button'); addBtn.className = 'map-add-btn'; addBtn.textContent = '+ Add entry';
            addBtn.addEventListener('click', () => {
                const k = keyInp.value.trim(); if (!k) return;
                obj[k] = defaultForKind(meta.valueKind); onChange({ ...obj }); rebuild();
            });
            addRow.append(keyInp, addBtn);
            w.appendChild(addRow);
        }
    }
    rebuild();
    return w;
}

// ======================== DATADEFINITION EDITOR =========================
function dataDefCtrl(val, ddType, dis, onChange) {
    const obj = (val && typeof val === 'object') ? { ...val } : {};
    const ddMeta = state.metadata?.dataDefinitions?.[ddType];
    const w = _div('field-control datadef-inline');

    if (!ddMeta || !ddMeta.fields || ddMeta.fields.length === 0) {
        return autoControl(val, dis, onChange);
    }

    for (const f of ddMeta.fields) {
        if (f.isId || f.isParent || f.isAbstract) continue;
        const v = obj[f.tag];
        const src = v !== undefined ? 'local' : 'default';
        w.appendChild(fieldRow(f.tag, f, v, src, nv => {
            obj[f.tag] = nv; onChange({ ...obj });
        }, null));
    }
    for (const [k, v] of Object.entries(obj)) {
        if (k.startsWith('__')) continue;
        if (ddMeta.fields.some(f => f.tag === k)) continue;
        w.appendChild(genericRow(k, v, 'local', nv => { obj[k] = nv; onChange({ ...obj }); }, null));
    }
    return w;
}

// ======================== ELEMENT HELPERS ===============================
function elementControl(kind, fullType, protoArg, val, dis, cb) {
    if (fullType && state.metadata?.dataDefinitions?.[fullType]) {
        return dataDefCtrl(val, fullType, dis, cb);
    }
    if (val !== null && typeof val === 'object' && !Array.isArray(val) && (kind === 'text' || kind === 'object')) {
        return autoControl(val, dis, cb);
    }
    switch (kind) {
        case 'boolean':       return boolCtrl(val, dis, cb);
        case 'integer':       return intCtrl(val, dis, cb);
        case 'float':         return floatCtrl(val, dis, cb);
        case 'text':          return textCtrl(val, dis, cb);
        case 'color':         return colorCtrl(val, dis, cb);
        case 'entityProtoId': return searchDropdown(val, 'entity', dis, cb);
        case 'protoId':       return searchDropdown(val, protoArg || 'entity', dis, cb);
        case 'object':        return autoControl(val, dis, cb);
        default:              return autoControl(val, dis, cb);
    }
}

function defaultForKind(kind) {
    switch (kind) {
        case 'boolean': return false;
        case 'integer': return 0;
        case 'float':   return 0.0;
        case 'text': case 'entityProtoId': case 'protoId': case 'color': return '';
        case 'object':  return {};
        default: return '';
    }
}

function autoControl(val, dis, cb) {
    if (val === null || val === undefined) return textCtrl('', dis, cb);
    if (typeof val === 'boolean') return boolCtrl(val, dis, cb);
    if (typeof val === 'number') return Number.isInteger(val) ? intCtrl(val, dis, cb) : floatCtrl(val, dis, cb);
    if (typeof val === 'string') return textCtrl(val, dis, cb);
    if (Array.isArray(val)) {
        return listCtrl(val, { elementKind: inferKindFromArray(val), elementFullType: null, elementProtoTypeArg: null }, dis, cb);
    }
    const w = _div('field-control');
    const ta = _el('textarea'); ta.className = 'field-textarea';
    const yamlStr = dumpYaml(val).trim();
    ta.value = yamlStr; ta.disabled = dis;
    ta.rows = Math.min(yamlStr.split('\n').length + 1, 20);
    ta.addEventListener('change', () => {
        try { const p = jsyaml.load(ta.value, { schema: SCHEMA }); ta.classList.remove('error'); cb(p); }
        catch { ta.classList.add('error'); }
    });
    w.appendChild(ta); return w;
}

function inferKindFromArray(arr) {
    if (!arr.length) return 'text';
    const first = arr[0];
    if (typeof first === 'boolean') return 'boolean';
    if (typeof first === 'number') return Number.isInteger(first) ? 'integer' : 'float';
    if (typeof first === 'object' && first !== null) return 'object';
    return 'text';
}
