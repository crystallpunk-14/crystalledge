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

// ======================== RES-PATH AUTOCOMPLETE ========================
/**
 * Attach filesystem autocomplete to a text input.
 * Reusable for any field that accepts a resource path.
 *
 * @param {HTMLInputElement} input - The text input element.
 * @param {object} opts
 * @param {string}   opts.apiUrl   - Browse API endpoint (default '/api/texture-browse').
 * @param {function} [opts.onPick] - Called when user selects a value.
 * @param {function} [opts.filter] - Optional filter(name) => bool for file entries.
 */
function resPathAutocomplete(input, opts = {}) {
    const apiUrl = opts.apiUrl || '/api/texture-browse';
    const onPick = opts.onPick || (() => {});
    const filter = opts.filter || null;

    const dd = _div('respath-dropdown');
    input.parentElement.style.position = 'relative';
    input.parentElement.appendChild(dd);

    let _visible = false;
    let _items = [];
    let _selIdx = -1;

    function show() { dd.classList.add('visible'); _visible = true; }
    function hide() { dd.classList.remove('visible'); _visible = false; _selIdx = -1; }

    function render(dirs, files) {
        dd.innerHTML = '';
        _items = [];
        _selIdx = -1;

        for (const d of dirs) {
            const opt = _div('respath-item respath-dir');
            opt.textContent = d + '/';
            opt.dataset.value = d + '/';
            opt.dataset.isDir = 'true';
            opt.addEventListener('mousedown', e => { e.preventDefault(); pick(opt); });
            dd.appendChild(opt);
            _items.push(opt);
        }

        const filtered = filter ? files.filter(filter) : files;
        for (const f of filtered) {
            const opt = _div('respath-item respath-file');
            opt.textContent = f;
            opt.dataset.value = f;
            opt.dataset.isDir = 'false';
            opt.addEventListener('mousedown', e => { e.preventDefault(); pick(opt); });
            dd.appendChild(opt);
            _items.push(opt);
        }

        if (_items.length) show(); else hide();
    }

    function pick(opt) {
        const cur = input.value;
        const lastSlash = cur.lastIndexOf('/');
        const prefix = lastSlash >= 0 ? cur.substring(0, lastSlash + 1) : '';

        if (opt.dataset.isDir === 'true') {
            input.value = prefix + opt.dataset.value;
            input.focus();
            browse();
        } else {
            input.value = prefix + opt.dataset.value;
            hide();
            onPick(input.value);
        }
    }

    function highlight(idx) {
        _items.forEach(it => it.classList.remove('selected'));
        if (idx >= 0 && idx < _items.length) {
            _items[idx].classList.add('selected');
            _items[idx].scrollIntoView({ block: 'nearest' });
        }
        _selIdx = idx;
    }

    async function browse() {
        const cur = input.value;
        const lastSlash = cur.lastIndexOf('/');
        const dirPart = lastSlash >= 0 ? cur.substring(0, lastSlash) : '';
        const typedPart = (lastSlash >= 0 ? cur.substring(lastSlash + 1) : cur).toLowerCase();

        try {
            const resp = await fetch(`${apiUrl}?path=${encodeURIComponent(dirPart)}`);
            if (!resp.ok) { hide(); return; }
            const data = await resp.json();

            let dirs  = data.dirs  || [];
            let files = data.files || [];

            // Filter by typed partial
            if (typedPart) {
                dirs  = dirs.filter(d => d.toLowerCase().includes(typedPart));
                files = files.filter(f => f.toLowerCase().includes(typedPart));
            }

            render(dirs, files);
        } catch { hide(); }
    }

    input.addEventListener('focus', browse);
    input.addEventListener('input', browse);
    input.addEventListener('blur', () => setTimeout(hide, 200));

    input.addEventListener('keydown', e => {
        if (!_visible) return;
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            highlight(Math.min(_selIdx + 1, _items.length - 1));
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            highlight(Math.max(_selIdx - 1, 0));
        } else if (e.key === 'Enter' && _selIdx >= 0) {
            e.preventDefault();
            pick(_items[_selIdx]);
        } else if (e.key === 'Escape') {
            hide();
        }
    });

    return { browse, hide, destroy: () => dd.remove() };
}

// ======================== SPRITE SPECIFIER =============================
/**
 * SpriteSpecifier field control.
 * Handles both formats:
 *   - RSI: { sprite: "path.rsi", state: "icon" }
 *   - Texture: "path/to/texture.png" (string)
 */
function spriteSpecifierCtrl(val, dis, cb) {
    const w = _div('field-control sprite-specifier-ctrl');

    // Parse current value
    let rsiPath = '', stateName = '', isTexture = false;
    if (typeof val === 'string') {
        // Could be "path.rsi" (bare RSI path) or "path.png" (texture)
        if (val.endsWith('.rsi')) { rsiPath = val; }
        else { isTexture = true; rsiPath = val; }
    } else if (val && typeof val === 'object') {
        rsiPath   = val.sprite || '';
        stateName = val.state  || '';
    }

    // ── Preview area ──
    const preview = _div('sprite-preview');
    let view = null;
    function updatePreview() {
        if (view) view.destroy();
        view = null;
        preview.innerHTML = '';
        if (!rsiPath) return;
        if (isTexture) {
            // Plain texture — show as <img>
            const img = _el('img');
            img.className = 'sprite-canvas';
            img.src = `/api/texture?path=${encodeURIComponent(rsiPath)}`;
            img.width = 64; img.height = 64;
            img.style.imageRendering = 'pixelated';
            img.onerror = () => { img.alt = '!'; };
            preview.appendChild(img);
        } else if (stateName) {
            view = SpriteView.create(preview, rsiPath, stateName, { size: 64 });
        }
    }
    updatePreview();

    // ── Inputs ──
    const fields = _div('sprite-fields');

    // Sprite (RSI path) input
    const spriteRow = _div('sprite-field-row');
    const spriteLbl = _el('label'); spriteLbl.className = 'sprite-input-label'; spriteLbl.textContent = 'sprite';
    const spriteInp = _el('input'); spriteInp.type = 'text'; spriteInp.className = 'field-input sprite-input';
    spriteInp.value = rsiPath; spriteInp.disabled = dis; spriteInp.placeholder = 'Path/to/sprite.rsi';
    spriteRow.append(spriteLbl, spriteInp);
    fields.appendChild(spriteRow);

    // State input (only for RSI, not for bare textures)
    const stateRow = _div('sprite-field-row');
    const stateLbl = _el('label'); stateLbl.className = 'sprite-input-label'; stateLbl.textContent = 'state';
    const stateInp = _el('input'); stateInp.type = 'text'; stateInp.className = 'field-input sprite-input';
    stateInp.value = stateName; stateInp.disabled = dis; stateInp.placeholder = 'State name';
    stateRow.append(stateLbl, stateInp);
    fields.appendChild(stateRow);

    function emit() {
        rsiPath   = spriteInp.value.trim();
        stateName = stateInp.value.trim();
        isTexture = rsiPath && !rsiPath.endsWith('.rsi') && !stateName;
        updatePreview();
        if (isTexture) cb(rsiPath);
        else if (rsiPath && stateName) cb({ sprite: rsiPath, state: stateName });
        else if (rsiPath) cb({ sprite: rsiPath });
    }

    // Load state dropdown suggestions from RSI meta.json
    const stateDD = _div('sprite-state-dropdown');
    stateRow.appendChild(stateDD);

    async function loadStates() {
        stateDD.innerHTML = '';
        if (!rsiPath || !rsiPath.endsWith('.rsi')) return;
        try {
            const meta = await SpriteView.loadMeta(rsiPath);
            if (!meta?.states?.length) return;
            stateDD.classList.add('visible');
            for (const s of meta.states) {
                const opt = _div('dropdown-item');
                opt.textContent = s.name;
                if (s.name === stateName) opt.classList.add('selected');
                opt.addEventListener('mousedown', e => {
                    e.preventDefault();
                    stateInp.value = s.name;
                    stateDD.classList.remove('visible');
                    emit();
                });
                stateDD.appendChild(opt);
            }
        } catch { /* RSI not found */ }
    }

    spriteInp.addEventListener('change', () => { emit(); loadStates(); });

    // Attach ResPath autocomplete to sprite input
    if (!dis) {
        resPathAutocomplete(spriteInp, {
            onPick(v) { spriteInp.value = v; emit(); loadStates(); },
        });
    }

    stateInp.addEventListener('focus', loadStates);
    stateInp.addEventListener('blur', () => setTimeout(() => stateDD.classList.remove('visible'), 180));
    stateInp.addEventListener('change', emit);

    w.append(preview, fields);
    return w;
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
            addBtn.addEventListener('click', () => { arr.push(defaultForKind(meta.elementKind)); rebuild(); });
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
        case 'vector2':       return vectorCtrl(val, ['x', 'y'], dis, cb);
        case 'vector3':       return vectorCtrl(val, ['x', 'y', 'z'], dis, cb);
        case 'vector4':       return vectorCtrl(val, ['x', 'y', 'z', 'w'], dis, cb);
        case 'box2':          return vectorCtrl(val, ['l', 'b', 'r', 't'], dis, cb);
        case 'spriteSpecifier': return spriteSpecifierCtrl(val, dis, cb);
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
        case 'vector2': return { x: 0, y: 0 };
        case 'vector3': return { x: 0, y: 0, z: 0 };
        case 'vector4': return { x: 0, y: 0, z: 0, w: 0 };
        case 'box2':    return { l: 0, b: 0, r: 0, t: 0 };
        case 'spriteSpecifier': return { sprite: '', state: '' };
        case 'object':  return {};
        default: return '';
    }
}

function defaultValueForMeta(meta) {
    return defaultForKind(meta.fieldKind);
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
