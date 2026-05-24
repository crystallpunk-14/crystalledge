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
 * @param {boolean}  [opts.hideFiles] - When true, only show directories.
 * @param {function} [opts.dirIsTerminal] - dir => bool; matching dirs are picked as final values instead of being descended into.
 */
function resPathAutocomplete(input, opts = {}) {
    const apiUrl = opts.apiUrl || '/api/texture-browse';
    const onPick = opts.onPick || (() => {});
    const filter = opts.filter || null;
    const hideFiles = !!opts.hideFiles;
    const dirIsTerminal = opts.dirIsTerminal || (() => false);
    // When set, file rows ending in .ogg get a tiny ▶ button that previews
    // the sound without committing the selection. Used by the SoundSpecifier
    // path picker so users can audition files before choosing.
    const previewAudio = !!opts.previewAudio;

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

        const filtered = hideFiles ? [] : (filter ? files.filter(filter) : files);
        for (const f of filtered) {
            const opt = _div('respath-item respath-file');
            opt.dataset.value = f;
            opt.dataset.isDir = 'false';

            const nameSpan = _el('span');
            nameSpan.className = 'respath-file-name';
            nameSpan.textContent = f;

            // Optional inline ▶ preview for .ogg files (placed at the START
            // of the row so it's reachable without scanning past long names).
            // Clicking the button must NOT trigger the row's `mousedown`
            // pick handler.
            if (previewAudio && f.toLowerCase().endsWith('.ogg')) {
                const cur = input.value;
                const lastSlash = cur.lastIndexOf('/');
                const prefix = lastSlash >= 0 ? cur.substring(0, lastSlash + 1) : '';
                const fullPath = prefix + f;
                const playBtn = _el('button');
                playBtn.type = 'button';
                playBtn.className = 'respath-play-btn';
                playBtn.title = `Preview ${fullPath}`;
                playBtn.textContent = '▶';
                let audio = null;
                const stop = () => { if (audio) { audio.pause(); audio = null; } playBtn.textContent = '▶'; };
                // mousedown is what triggers the row pick – stop propagation
                // *and* preventDefault so blur doesn't fire either.
                playBtn.addEventListener('mousedown', e => { e.stopPropagation(); e.preventDefault(); });
                playBtn.addEventListener('click', e => {
                    e.stopPropagation(); e.preventDefault();
                    if (audio) { stop(); return; }
                    audio = new Audio(`/api/audio?path=${encodeURIComponent(fullPath)}`);
                    audio.addEventListener('ended', stop);
                    audio.play().then(() => { playBtn.textContent = '■'; })
                        .catch(() => { playBtn.textContent = '!'; audio = null; });
                });
                opt.appendChild(playBtn);
            }

            opt.appendChild(nameSpan);

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

        const rawName = opt.dataset.value;
        if (opt.dataset.isDir === 'true') {
            // `rawName` for dirs already includes a trailing '/'.  If the
            // caller marks this dir as a terminal selection (e.g. an `.rsi`
            // folder), strip the slash and treat the pick as final.
            const dirName = rawName.endsWith('/') ? rawName.slice(0, -1) : rawName;
            if (dirIsTerminal(dirName)) {
                input.value = prefix + dirName;
                hide();
                onPick(input.value);
                return;
            }
            input.value = prefix + rawName;
            input.focus();
            browse();
        } else {
            input.value = prefix + rawName;
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

            // Filter by typed partial — smart search (subsequence + multi-token).
            if (typedPart) {
                dirs  = dirs.filter(d => smartMatch(d, typedPart));
                files = files.filter(f => smartMatch(f, typedPart));
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
 *
 * SS14 supports two YAML shapes for a SpriteSpecifier:
 *   - RSI:     { sprite: "Path/Foo.rsi", state: "icon" }
 *   - Texture: "Path/raw.png"  (a bare string)
 *
 * In practice prototypes overwhelmingly use the RSI form, and a bare RSI
 * path without a state is meaningless to the engine.  We therefore always
 * emit the mapping form for `.rsi` paths and only fall back to the string
 * form when the user explicitly types a non-`.rsi` texture path.  The
 * `state` input is disabled until the sprite path actually ends with `.rsi`.
 */
function spriteSpecifierCtrl(val, dis, cb) {
    const w = _div('field-control sprite-specifier-ctrl');

    // Parse current value
    let rsiPath = '', stateName = '';
    if (typeof val === 'string') {
        rsiPath = val;
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
        if (!rsiPath.endsWith('.rsi')) {
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

    // Sprite (RSI path) input — only navigates through dirs, never shows files.
    const spriteRow = _div('sprite-field-row');
    const spriteLbl = _el('label'); spriteLbl.className = 'sprite-input-label'; spriteLbl.textContent = 'sprite';
    const spriteInp = _el('input'); spriteInp.type = 'text'; spriteInp.className = 'field-input sprite-input';
    spriteInp.value = rsiPath; spriteInp.disabled = dis; spriteInp.placeholder = 'Path/to/sprite.rsi';
    spriteRow.append(spriteLbl, spriteInp);
    fields.appendChild(spriteRow);

    // State input — disabled until a valid .rsi path is entered.
    const stateRow = _div('sprite-field-row');
    const stateLbl = _el('label'); stateLbl.className = 'sprite-input-label'; stateLbl.textContent = 'state';
    const stateInp = _el('input'); stateInp.type = 'text'; stateInp.className = 'field-input sprite-input';
    stateInp.value = stateName; stateInp.placeholder = 'State name';
    stateRow.append(stateLbl, stateInp);
    fields.appendChild(stateRow);

    function refreshStateDisabled() {
        stateInp.disabled = dis || !rsiPath.endsWith('.rsi');
    }
    refreshStateDisabled();

    function emit() {
        rsiPath   = spriteInp.value.trim();
        stateName = stateInp.value.trim();
        refreshStateDisabled();
        updatePreview();
        if (!rsiPath) {
            cb(null);
            return;
        }
        if (rsiPath.endsWith('.rsi')) {
            // Always emit the mapping form for RSI paths so the engine sees
            // a well-formed SpriteSpecifier.Rsi.  An empty state is preserved
            // so the user knows they still need to fill it in.
            cb({ sprite: rsiPath, state: stateName });
        } else {
            // Bare texture — string form is the only valid representation.
            cb(rsiPath);
        }
    }

    // ── State dropdown (lists frames found in the RSI's meta.json) ──
    const stateDD = _div('sprite-state-dropdown');
    stateRow.appendChild(stateDD);

    async function loadStates() {
        stateDD.innerHTML = '';
        if (!rsiPath.endsWith('.rsi')) return;
        try {
            const meta = await SpriteView.loadMeta(rsiPath);
            if (!meta?.states?.length) return;
            const lower = stateInp.value;
            const filtered = lower
                ? meta.states.filter(s => smartMatch(s.name, lower))
                : meta.states;
            if (!filtered.length) return;
            stateDD.classList.add('visible');
            for (const s of filtered) {
                const opt = _div('dropdown-item sprite-state-option');
                // Small live preview of the first frame so the user can pick
                // by sight instead of by name.
                const thumb = _div('sprite-state-thumb');
                try { SpriteView.create(thumb, rsiPath, s.name, { size: 24 }); } catch { /* missing */ }
                const label = _el('span');
                label.className = 'sprite-state-name';
                label.textContent = s.name;
                opt.append(thumb, label);
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

    // Attach ResPath autocomplete to sprite input — directories only, and
    // any `.rsi` directory is treated as a terminal selection so the user
    // doesn't get dropped inside it (which would just show png files we
    // don't want listed here).
    if (!dis) {
        resPathAutocomplete(spriteInp, {
            hideFiles: true,
            dirIsTerminal: name => name.endsWith('.rsi'),
            onPick(v) { spriteInp.value = v; emit(); loadStates(); },
        });
    }

    stateInp.addEventListener('focus', loadStates);
    stateInp.addEventListener('input', loadStates);
    stateInp.addEventListener('blur', () => setTimeout(() => stateDD.classList.remove('visible'), 180));
    stateInp.addEventListener('change', emit);

    w.append(preview, fields);
    return w;
}

// ======================== SOUND SPECIFIER ==============================
/**
 * SoundSpecifier field control.
 *
 * SS14 (see RobustToolbox/Robust.Shared/Audio/SoundSpecifier.cs) accepts
 * three YAML shapes:
 *   - Bare string  → SoundPathSpecifier with default params
 *       weapon_swing: Audio/Effects/swing.ogg
 *   - Path mapping → SoundPathSpecifier with optional params
 *       weapon_swing: { path: "...ogg", params: { volume: -3 } }
 *   - Collection   → SoundCollectionSpecifier
 *       weapon_swing: { collection: "Explosions", params: { ... } }
 *
 * AudioParams is itself a [DataDefinition] in Robust.Shared.Audio, so its
 * inner fields are rendered through the generic `dataDefCtrl` rather than
 * being re-implemented here. This keeps validation/UX consistent with
 * every other DataDefinition in the editor.
 */
const AUDIO_PARAMS_TYPE = 'Robust.Shared.Audio.AudioParams';

function soundSpecifierCtrl(val, dis, cb) {
    const w = _div('field-control sound-specifier-ctrl');

    // ── Parse incoming value into a canonical shape ──
    let mode, path = '', collection = '', params = null;
    if (typeof val === 'string') {
        mode = 'path'; path = val;
    } else if (val && typeof val === 'object') {
        params = (val.params && typeof val.params === 'object') ? { ...val.params } : null;
        // Detect mode by *key presence*, not by truthiness – an empty string
        // value for `collection: ''` still means the user selected the
        // collection mode and just hasn't picked a prototype yet.
        if (Object.prototype.hasOwnProperty.call(val, 'collection')) {
            mode = 'collection'; collection = String(val.collection || '');
        } else {
            mode = 'path'; path = val.path || '';
        }
    } else {
        mode = 'path';
    }

    function emit() {
        // Always emit the mapping form ({ path } / { collection }, plus
        // params when non-empty). The engine accepts a bare string for
        // path mode too, but emitting the mapping form unconditionally is
        // the canonical SS14 style — it keeps the on-disk shape stable
        // across edits and round-trips cleanly through the YAML pipeline
        // when the field's static type is the abstract SoundSpecifier
        // base (engine deserialiser still has to discriminate path vs
        // collection by key presence).
        const hasParams = params && Object.keys(params).length > 0;
        if (mode === 'collection') {
            const out = { collection };
            if (hasParams) out.params = params;
            cb(out);
        } else {
            const out = { path };
            if (hasParams) out.params = params;
            cb(out);
        }
    }

    // ── Mode tabs ──
    const tabs = _div('sound-mode-tabs');
    const pathTab = _el('button'); pathTab.type = 'button'; pathTab.className = 'sound-mode-tab'; pathTab.textContent = 'path';
    const colTab  = _el('button'); colTab.type  = 'button'; colTab.className  = 'sound-mode-tab'; colTab.textContent  = 'collection';
    function syncTabs() {
        pathTab.classList.toggle('active', mode === 'path');
        colTab.classList.toggle('active', mode === 'collection');
    }
    pathTab.addEventListener('click', () => { if (dis) return; mode = 'path'; syncTabs(); renderBody(); emit(); });
    colTab.addEventListener('click',  () => { if (dis) return; mode = 'collection'; syncTabs(); renderBody(); emit(); });
    syncTabs();
    tabs.append(pathTab, colTab);

    // ── Body (rebuilt on mode switch) ──
    const body = _div('sound-body');

    function renderBody() {
        body.innerHTML = '';

        // -- Resource row --
        const row = _div('sound-field-row');
        const lbl = _el('label'); lbl.className = 'sound-input-label';
        const inp = _el('input'); inp.type = 'text'; inp.className = 'field-input sound-input'; inp.disabled = dis;

        if (mode === 'collection') {
            lbl.textContent = 'collection';
            inp.placeholder = 'SoundCollection prototype id';
            inp.value = collection;
            // Reuse the generic ProtoId search for soundCollection prototypes.
            // This stays consistent with every other ProtoId field.
            row.append(lbl, inp);
            body.appendChild(row);

            // Swap the bare input for the standard search dropdown.
            inp.remove();
            const drop = searchDropdown(collection, 'soundCollection', dis, v => {
                collection = String(v || '');
                emit();
            });
            row.appendChild(drop);
        } else {
            lbl.textContent = 'path';
            inp.placeholder = 'Effects/foo.ogg';
            inp.value = path;
            row.append(lbl, inp);
            body.appendChild(row);

            inp.addEventListener('change', () => { path = inp.value; emit(); updatePlayBtn(); });
            if (!dis) {
                resPathAutocomplete(inp, {
                    apiUrl: '/api/audio-browse',
                    filter: name => name.toLowerCase().endsWith('.ogg'),
                    previewAudio: true,
                    onPick(v) { inp.value = v; path = v; emit(); updatePlayBtn(); },
                });
            }

            // -- Play button --
            const playBtn = _el('button'); playBtn.type = 'button';
            playBtn.className = 'sound-play-btn'; playBtn.textContent = '▶';
            playBtn.title = 'Preview audio';
            let audio = null;
            function updatePlayBtn() {
                playBtn.disabled = !path || !path.toLowerCase().endsWith('.ogg');
            }
            updatePlayBtn();
            playBtn.addEventListener('click', () => {
                if (audio) { audio.pause(); audio = null; playBtn.textContent = '▶'; return; }
                audio = new Audio(`/api/audio?path=${encodeURIComponent(path)}`);
                // Honour the volume from AudioParams (engine treats it as dB).
                const vol = params?.volume;
                if (typeof vol === 'number') {
                    // Map dB → linear gain, clamped to [0,1].
                    audio.volume = Math.max(0, Math.min(1, Math.pow(10, vol / 20)));
                }
                audio.addEventListener('ended', () => { playBtn.textContent = '▶'; audio = null; });
                audio.play().then(() => { playBtn.textContent = '■'; })
                    .catch(() => { playBtn.textContent = '!'; audio = null; });
            });
            row.appendChild(playBtn);
        }

        // -- AudioParams (collapsible, rendered as a regular DataDefinition) --
        const paramsWrap = _div('sound-params-wrap');
        const toggle = _el('button'); toggle.type = 'button'; toggle.className = 'sound-params-toggle';
        let open = !!(params && Object.keys(params).length);
        toggle.textContent = (open ? '▼' : '▶') + ' params';
        const paramsHost = _div('sound-params-host');
        paramsHost.style.display = open ? '' : 'none';
        toggle.addEventListener('click', () => {
            open = !open;
            toggle.textContent = (open ? '▼' : '▶') + ' params';
            paramsHost.style.display = open ? '' : 'none';
        });
        paramsWrap.append(toggle, paramsHost);
        body.appendChild(paramsWrap);

        // Render AudioParams via the generic DataDefinition editor. If the
        // metadata doesn't carry AudioParams (very old metadata.json) we
        // fall back to the unsupported-type stub so the user at least sees
        // why nothing's editable.
        if (state.metadata?.dataDefinitions?.[AUDIO_PARAMS_TYPE]) {
            paramsHost.appendChild(dataDefCtrl(params || {}, AUDIO_PARAMS_TYPE, dis, np => {
                params = (np && typeof np === 'object' && Object.keys(np).length) ? np : null;
                emit();
            }));
        } else if (typeof unsupportedStub === 'function') {
            paramsHost.appendChild(unsupportedStub(AUDIO_PARAMS_TYPE));
        }
    }
    renderBody();

    w.append(tabs, body);
    return w;
}
