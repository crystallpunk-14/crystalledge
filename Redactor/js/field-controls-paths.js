// ======================================================================
//  SS14 Prototype Redactor – Path/Sprite Controls
// ======================================================================
//  Resource path autocomplete + the rich SpriteSpecifier editor.  Pulled
//  out of fields.js to keep that file focused on primitive controls and
//  row layout.  Globals: resPathAutocomplete, spriteSpecifierCtrl.
// ======================================================================

'use strict';

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
