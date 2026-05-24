// ======================================================================
//  SS14 Prototype Redactor – Editor & Proto Cards
// ======================================================================

'use strict';

// ======================== EDITOR =======================================
// renderEditor() rebuilds the whole right pane.  This is intentionally
// blunt — the editor is rarely the bottleneck — but rapid mutations
// (color pickers, sliders, batched undo) used to trigger many renders
// per frame.  scheduleRenderEditor() coalesces those into one rAF tick.
let _renderScheduled = false;
function scheduleRenderEditor() {
    if (_renderScheduled) return;
    _renderScheduled = true;
    requestAnimationFrame(() => {
        _renderScheduled = false;
        renderEditor();
    });
}

function renderEditor() {
    const area = document.getElementById('editor-area');
    if (!state.currentFile) {
        area.innerHTML = `<div class="empty-state"><div class="empty-icon">📋</div>
            <h2>SS14 Prototype Redactor</h2>
            <p>Open a YAML file from the sidebar to start editing prototypes visually.</p>
            <p class="hint">Ctrl+S — force save</p></div>`;
        return;
    }
    const fs = state.openFiles.get(state.currentFile);
    if (!fs) return;
    const protos = fs.yaml;
    if (!Array.isArray(protos) || protos.length === 0) {
        area.innerHTML = '<div class="empty-state"><p>No prototypes found in this file.</p></div>';
        area.appendChild(buildAddProtoFooter());
        return;
    }

    // Pre-load parent files then build cards
    preloadParents(protos).then(() => {
        state.resolvedCache.clear();

        // Save collapse state before re-render
        const collapseState = saveCollapseState(area);

        area.innerHTML = '';
        for (let i = 0; i < protos.length; i++) {
            try {
                area.appendChild(buildCard(protos[i], i));
            } catch (e) {
                console.error('[Editor] Error building card:', protos[i]?.id || i, e);
                const errCard = _div('proto-card proto-error');
                errCard.innerHTML = `<div class="proto-header"><span class="proto-type-badge">${esc(protos[i]?.type || '?')}</span><span class="proto-id-text">${esc(protos[i]?.id || '?')}</span></div><div class="proto-body" style="color:var(--warning);padding:8px">${esc(e.message)}</div>`;
                area.appendChild(errCard);
            }
        }
        area.appendChild(buildAddProtoFooter());

        // Restore collapse state
        restoreCollapseState(area, collapseState);
    }).catch(e => {
        console.error('[Editor] preloadParents failed:', e);
        area.innerHTML = `<div class="empty-state"><p style="color:var(--warning)">Render error: ${esc(e.message)}</p></div>`;
    });
}

function buildAddProtoFooter() {
    const footer = _div('add-proto-footer');
    const btn = _el('button'); btn.className = 'add-proto-btn'; btn.textContent = '+ Add Prototype';
    btn.addEventListener('click', () => showAddProtoModal());
    footer.appendChild(btn);
    return footer;
}

function showAddProtoModal() {
    const overlay = _div('modal-overlay');
    const modal = _div('modal');
    modal.innerHTML = `<div class="modal-header"><h3>Add Prototype</h3><button class="modal-close">\u00d7</button></div>
        <div class="modal-body">
            <input type="text" class="field-input modal-search" placeholder="Search prototype type\u2026" autocomplete="off">
            <div class="modal-list"></div>
        </div>`;
    overlay.appendChild(modal);
    document.body.appendChild(overlay);

    const searchInp = modal.querySelector('.modal-search');
    const listEl = modal.querySelector('.modal-list');
    const types = state.metadata?.prototypes ? Object.keys(state.metadata.prototypes).sort() : [];

    function renderList(q) {
        listEl.innerHTML = '';
        const filtered = types.filter(t => smartMatch(t, q));
        if (!filtered.length) { listEl.innerHTML = '<div class="dropdown-empty">No types found</div>'; return; }
        for (const t of filtered.slice(0, 100)) {
            const el = _div('modal-list-item');
            el.textContent = t;
            el.addEventListener('click', () => { overlay.remove(); addNewPrototype(t); });
            listEl.appendChild(el);
        }
    }
    renderList('');
    searchInp.addEventListener('input', () => renderList(searchInp.value));
    searchInp.focus();
    modal.querySelector('.modal-close').addEventListener('click', () => overlay.remove());
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
}

function addNewPrototype(type) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs) return;
    if (!Array.isArray(fs.yaml)) fs.yaml = [];
    const proto = { type, id: 'NewPrototype' };
    // NOTE: we intentionally do NOT seed `proto.parent = ''` here. An empty
    // parent slot would force every new prototype into a half-filled state
    // and dump as `parent: ''` until the user manually clears it. The
    // parent control still works without seeding because the user can click
    // "+ Add item" on the parent bar which commits an empty slot via
    // `onParentChange` — keeping the add-item codepath intact.
    fs.yaml.push(proto);
    commitChange(fs);
    renderEditor();
    const area = document.getElementById('editor-area');
    area.scrollTop = area.scrollHeight;
}

// ======================== PROTO CARD ===================================
function buildCard(proto, idx) {
    const card = _div('proto-card');
    const type = proto.type || 'unknown';
    const id   = proto.id   || '(no id)';
    const meta = state.metadata?.prototypes?.[type];
    const inheriting = meta?.inheriting ?? false;
    const isAbstract = !!proto.abstract;

    // Apply abstract styling
    if (isAbstract) card.classList.add('proto-abstract');

    // Multi-line header rendered as YAML lines. The header stays visible
    // during collapse, so id/abstract live here as pseudo field-rows.
    //   Line 1:  - type: foo                        (+ delete button on hover)
    //   Line 2:  id: my-proto-id                    (always visible)
    //   Line 3:  abstract: true                     (always visible, toggle)
    // The parent-bar (see below) is appended next, also part of the sticky header.
    const hdr = _div('proto-header');
    hdr.innerHTML = `<div class="proto-type-line">
            <span class="proto-type-badge" title="${esc(meta?.summary || '')}">${esc(type)}</span>
            <button class="delete-proto-btn" title="Delete prototype">×</button>
        </div>
        <div class="field-row field-local proto-id-row">
            <label class="field-label">id</label>
            <div class="field-control-wrap">
                <span class="proto-id-text" title="Double-click to rename ID">${esc(String(id))}</span>
            </div>
        </div>
        <div class="field-row ${isAbstract ? 'field-local' : 'inherited'} proto-abstract-row">
            <label class="field-label">abstract</label>
            <div class="field-control-wrap">
                <div class="field-control">
                    <span class="abstract-label toggle-label">${isAbstract ? 'true' : 'false'}</span>
                    <label class="toggle-switch" title="Abstract prototype – serves only as a template for children, never spawned at runtime">
                        <input type="checkbox" class="abstract-cb" ${isAbstract ? 'checked' : ''}>
                        <span class="toggle-slider"></span>
                    </label>
                </div>
            </div>
        </div>`;

    // Abstract checkbox
    const absCb = hdr.querySelector('.abstract-cb');
    absCb.addEventListener('change', e => {
        e.stopPropagation();
        const fs = state.openFiles.get(state.currentFile);
        if (!fs || !fs.yaml[idx]) return;
        if (absCb.checked) {
            fs.yaml[idx].abstract = true;
        } else {
            delete fs.yaml[idx].abstract;
        }
        state.resolvedCache.clear();
        commitChange(fs);
        renderEditor();
    });

    // ID rename on double-click
    const idSpan = hdr.querySelector('.proto-id-text');
    idSpan.addEventListener('dblclick', e => {
        e.stopPropagation();
        startIdRename(idSpan, proto, idx);
    });

    hdr.querySelector('.delete-proto-btn').addEventListener('click', e => {
        e.stopPropagation();
        if (!confirm(`Delete prototype "${id}"?`)) return;
        const fs = state.openFiles.get(state.currentFile);
        if (fs && fs.yaml) {
            fs.yaml.splice(idx, 1);
            commitChange(fs);
            renderEditor();
        }
    });
    // Click on the type line (only) toggles collapse. id/abstract/parent
    // sub-rows are NOT collapse-triggers because the user must be able to
    // edit them without folding the whole card.
    hdr.querySelector('.proto-type-line').addEventListener('click', e => {
        if (e.target.closest('a, button, input, label')) return;
        card.classList.toggle('collapsed');
    });
    hdr.addEventListener('contextmenu', e => {
        e.preventDefault(); e.stopPropagation();
        const items = [
            { label: 'Rename ID…', action: () => startIdRename(idSpan, proto, idx) },
            { label: 'Collapse / Expand', action: () => hdr.querySelector('.collapse-btn').click() },
        ];
        if (meta?.className) items.push({ label: 'Open .cs source', action: () => api.openSource(meta.className) });
        items.push('---', { label: 'Delete prototype', danger: true, action: () => hdr.querySelector('.delete-proto-btn').click() });
        showContextMenu(e.clientX, e.clientY, items);
    });
    card.appendChild(hdr);

    // Parent sub-header (uses same list<protoId> logic as other fields)
    if (inheriting) {
        const parentBar = _div('proto-parent-bar');

        // Normalize parent value to always be an array for the list control.
        // Critical: an empty string is treated as a single in-progress slot,
        // NOT as "no parent". This is what makes "+ Add item" work – clicking
        // it persists `proto.parent = ''` (via onParentChange below), the card
        // re-renders, and the slot survives as an empty protoId search row
        // for the user to fill in instead of vanishing.
        let parentVal = proto.parent;
        if (parentVal == null) parentVal = [];
        else if (!Array.isArray(parentVal)) parentVal = [parentVal];

        const parentMeta = {
            fieldKind: 'list', tag: 'parent',
            elementKind: 'protoId', elementFullType: 'protoId',
            elementProtoTypeArg: type, required: false
        };
        const parentSource = proto.parent !== undefined ? 'local' : 'default';
        const onParentChange = v => {
            // Preserve empty-string slots so "+ Add item" doesn't immediately
            // erase itself (listCtrl now commits on add so PrototypeLayerData
            // and friends persist – an empty parent slot is a valid in-progress
            // state that the user fills in next).
            const arr = Array.isArray(v) ? v.filter(x => x != null) : [];
            if (arr.length === 0) deleteField([idx], 'parent');
            else if (arr.length === 1) setFieldValue([idx], 'parent', arr[0]);
            else setFieldValue([idx], 'parent', arr);
        };
        const onParentReset = parentSource === 'local' ? () => deleteField([idx], 'parent') : null;
        parentBar.appendChild(fieldRow('parent', parentMeta, parentVal, parentSource, onParentChange, onParentReset));
        hdr.appendChild(parentBar);
    }

    // body
    const body = _div('proto-body');

    // Resolve inherited values
    let inherited = {};
    if (inheriting && proto.parent) inherited = resolveInheritance(type, proto.parent);

    // Render ALL metadata fields with override tracking
    const renderedTags = new Set(['type']);
    if (meta) {
        for (const f of meta.fields) {
            if (f.isId || f.isAbstract || f.isParent) { renderedTags.add(f.tag); continue; } // rendered in header/sub-header
            if (f.tag === 'components') { renderedTags.add('components'); continue; }
            renderedTags.add(f.tag);

            const { value, source } = getFieldValue(proto, f.tag, inherited, undefined);

            const onReset = source === 'local' ? () => deleteField([idx], f.tag) : null;
            body.appendChild(fieldRow(f.tag, f, value, source, v => setFieldValue([idx], f.tag, v), onReset));
        }
    }

    // Extra fields not in metadata (custom user fields in YAML)
    for (const [k, v] of Object.entries(proto)) {
        if (k.startsWith('__') || renderedTags.has(k)) continue;
        const source = 'local'; // if in proto, it's local
        body.appendChild(genericRow(k, v, source, nv => setFieldValue([idx], k, nv), () => deleteField([idx], k)));
    }

    // Inherited-only fields not yet shown
    for (const [k, v] of Object.entries(inherited)) {
        if (['type', 'id', 'parent', 'abstract', 'components'].includes(k)) continue;
        if (renderedTags.has(k) || proto[k] !== undefined) continue;
        body.appendChild(genericRow(k, v, 'inherited', nv => setFieldValue([idx], k, nv), null));
    }

    // Components section
    if (type === 'entity' || proto.components) {
        const cs = buildComponentsSection(proto, idx, inherited);
        if (cs) body.appendChild(cs);
    }

    card.appendChild(body);

    // Context menu on editor area background for collapse/expand all
    body.addEventListener('contextmenu', e => {
        if (e.target.closest('.field-row, .component-card, .component-header')) return;
        e.preventDefault();
        showContextMenu(e.clientX, e.clientY, [
            { label: 'Collapse all prototypes', action: () => collapseAllProtos(true) },
            { label: 'Expand all prototypes', action: () => collapseAllProtos(false) },
        ]);
    });

    return card;
}

// ======================== ID RENAME ====================================
function startIdRename(idSpan, proto, idx) {
    const oldId = proto.id || '';
    const inp = _el('input');
    inp.type = 'text';
    inp.className = 'field-input proto-id-input';
    inp.value = oldId;

    idSpan.textContent = '';
    idSpan.appendChild(inp);
    inp.focus();
    inp.select();

    function finish() {
        const newId = inp.value.trim();
        inp.removeEventListener('blur', finish);
        inp.removeEventListener('keydown', onKey);
        if (newId && newId !== oldId) {
            const fs = state.openFiles.get(state.currentFile);
            if (fs && fs.yaml[idx]) {
                fs.yaml[idx].id = newId;
                // Clear the resolved cache since IDs changed
                state.resolvedCache.clear();
                commitChange(fs);
                console.log(`[Editor] Renamed prototype ID: "${oldId}" → "${newId}"`);
            }
        }
        idSpan.textContent = proto.id || '(no id)';
    }

    function onKey(e) {
        if (e.key === 'Enter') { e.preventDefault(); finish(); }
        if (e.key === 'Escape') { inp.value = oldId; finish(); }
    }

    inp.addEventListener('blur', finish);
    inp.addEventListener('keydown', onKey);
}

// ======================== COLLAPSE / EXPAND ALL ========================
// Collapse state is keyed by *prototype id* and *component type*, not by
// index — adding / removing / reordering a component must not bleed the
// expanded/collapsed flag onto an unrelated card.
function saveCollapseState(area) {
    const state = { protos: {}, comps: {} };
    area.querySelectorAll('.proto-card').forEach(card => {
        const pid = card.dataset.protoId || card.querySelector('.proto-id-text')?.textContent || '';
        if (!pid) return;
        state.protos[pid] = card.classList.contains('collapsed');
        state.comps[pid] = {};
        card.querySelectorAll('.component-card').forEach(comp => {
            const ct = comp.querySelector('.component-type')?.textContent || '';
            if (!ct) return;
            state.comps[pid][ct] = comp.classList.contains('collapsed');
        });
    });
    return state;
}

function restoreCollapseState(area, saved) {
    area.querySelectorAll('.proto-card').forEach(card => {
        const pid = card.dataset.protoId || card.querySelector('.proto-id-text')?.textContent || '';
        if (!pid) return;
        if (saved.protos[pid] !== undefined) {
            card.classList.toggle('collapsed', saved.protos[pid]);
        }
        const cm = saved.comps[pid];
        if (cm) {
            card.querySelectorAll('.component-card').forEach(comp => {
                const ct = comp.querySelector('.component-type')?.textContent || '';
                if (!ct) return;
                if (cm[ct] !== undefined) {
                    comp.classList.toggle('collapsed', cm[ct]);
                }
            });
        }
    });
}

function collapseAllProtos(collapse) {
    const area = document.getElementById('editor-area');
    area.querySelectorAll('.proto-card').forEach(card => {
        card.classList.toggle('collapsed', collapse);
    });
}

// Editor area context menu for collapse/expand all
document.addEventListener('DOMContentLoaded', () => {
    const area = document.getElementById('editor-area');
    if (area) {
        area.addEventListener('contextmenu', e => {
            // Only handle when clicking on empty bg area
            if (e.target.closest('.proto-card, .field-row, .component-card, .add-proto-footer')) return;
            e.preventDefault();
            showContextMenu(e.clientX, e.clientY, [
                { label: 'Collapse all prototypes', action: () => collapseAllProtos(true) },
                { label: 'Expand all prototypes', action: () => collapseAllProtos(false) },
            ]);
        });
    }
});

// ======================== COMPONENTS SECTION ============================
function buildComponentsSection(proto, protoIdx, inherited) {
    const sec = _div('components-section');
    sec.innerHTML = `<div class="components-header"><span>Components</span><button class="add-component-btn" title="Add component">+</button></div>`;
    sec.querySelector('.add-component-btn').addEventListener('click', () => showAddComponentModal(proto, protoIdx));

    const localComps = proto.components || [];
    const inhComps   = inherited.components || [];
    const localMap = new Map();
    localComps.forEach((c, i) => { if (c && c.type) localMap.set(c.type, { data: c, idx: i }); });
    const inhMap = new Map();
    for (const c of inhComps) { if (c && c.type && !localMap.has(c.type)) inhMap.set(c.type, c); }

    for (const [ct, { data, idx }] of localMap) sec.appendChild(compCard(ct, data, false, protoIdx, idx, inherited));
    for (const [ct, data]          of inhMap)   sec.appendChild(compCard(ct, data, true,  protoIdx, -1, inherited));
    return sec;
}

function showAddComponentModal(proto, protoIdx) {
    const overlay = _div('modal-overlay');
    const modal = _div('modal');
    modal.innerHTML = `<div class="modal-header"><h3>Add Component</h3><button class="modal-close">\u00d7</button></div>
        <div class="modal-body">
            <input type="text" class="field-input modal-search" placeholder="Search component\u2026" autocomplete="off">
            <div class="modal-list"></div>
        </div>`;
    overlay.appendChild(modal);
    document.body.appendChild(overlay);

    const searchInp = modal.querySelector('.modal-search');
    const listEl = modal.querySelector('.modal-list');
    const existing = new Set((proto.components || []).map(c => c?.type).filter(Boolean));
    // Also exclude inherited components
    if (proto.parent) {
        const inh = resolveInheritance(proto.type, proto.parent);
        if (inh?.components) for (const c of inh.components) { if (c?.type) existing.add(c.type); }
    }
    const types = state.metadata?.components ? Object.keys(state.metadata.components).sort().filter(t => !existing.has(t)) : [];

    function renderList(q) {
        listEl.innerHTML = '';
        // Unified smart-search: subsequence per space-separated token.
        // Lets users find "CEStaminaThrowable" via "throw stam".
        const filtered = types.filter(t => smartMatch(t, q));
        if (!filtered.length) { listEl.innerHTML = '<div class="dropdown-empty">No components found</div>'; return; }
        for (const t of filtered.slice(0, 100)) {
            const el = _div('modal-list-item');
            el.textContent = t;
            el.addEventListener('click', () => {
                overlay.remove();
                const fs = state.openFiles.get(state.currentFile);
                if (!fs || !fs.yaml[protoIdx]) return;
                if (!fs.yaml[protoIdx].components) fs.yaml[protoIdx].components = [];
                fs.yaml[protoIdx].components.push({ type: t });
                commitChange(fs); renderEditor();
            });
            listEl.appendChild(el);
        }
    }
    renderList('');
    searchInp.addEventListener('input', () => renderList(searchInp.value));
    searchInp.focus();
    modal.querySelector('.modal-close').addEventListener('click', () => overlay.remove());
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
}

/** Copy an inherited component to local YAML so it can be edited. Returns new compIdx. */
function localizeComponent(protoIdx, compType) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs || !fs.yaml[protoIdx]) return -1;
    if (!fs.yaml[protoIdx].components) fs.yaml[protoIdx].components = [];
    // Check if already localized
    const existing = fs.yaml[protoIdx].components.findIndex(c => c && c.type === compType);
    if (existing >= 0) return existing;
    fs.yaml[protoIdx].components.push({ type: compType });
    return fs.yaml[protoIdx].components.length - 1;
}

function compCard(compType, data, isInh, protoIdx, compIdx, inherited) {
    // Collapsed by default ONLY for inherited components that haven't been
    // overridden in this prototype. Local components (= the user explicitly
    // wrote them in this YAML, or they carry overrides on top of an inherited
    // base) start expanded so the changes are immediately visible.
    const startCollapsed = isInh;
    const card = _div('component-card' + (startCollapsed ? ' collapsed' : '') + (isInh ? ' inherited' : ' comp-local'));
    const cMeta = state.metadata?.components?.[compType];
    const hdr = _div('component-header');
    hdr.innerHTML = `<span class="component-type" title="${esc(cMeta?.summary || '')}">${esc(compType)}</span>`;
    if (!isInh && compIdx >= 0) {
        const rmBtn = _el('button'); rmBtn.className = 'field-reset-btn'; rmBtn.title = 'Remove component'; rmBtn.textContent = '×';
        rmBtn.addEventListener('click', e => {
            e.stopPropagation();
            const fs = state.openFiles.get(state.currentFile);
            if (fs && fs.yaml[protoIdx]?.components) {
                fs.yaml[protoIdx].components.splice(compIdx, 1);
                commitChange(fs); renderEditor();
            }
        });
        hdr.appendChild(rmBtn);
    }
    hdr.addEventListener('click', e => {
        if (e.target.closest('button')) return;
        card.classList.toggle('collapsed');
    });
    hdr.addEventListener('contextmenu', e => {
        e.preventDefault(); e.stopPropagation();
        const items = [];
        if (cMeta?.className) items.push({ label: 'Open .cs source', action: () => api.openSource(cMeta.className) });
        if (!isInh && compIdx >= 0) {
            items.push('---', { label: 'Remove component', danger: true, action: () => {
                const fs = state.openFiles.get(state.currentFile);
                if (fs && fs.yaml[protoIdx]?.components) {
                    fs.yaml[protoIdx].components.splice(compIdx, 1);
                    commitChange(fs); renderEditor();
                }
            }});
        }
        items.push('---');
        items.push({ label: 'Collapse all components', action: () => collapseAllComponents(true) });
        items.push({ label: 'Expand all components', action: () => collapseAllComponents(false) });
        if (items.length) showContextMenu(e.clientX, e.clientY, items);
    });
    card.appendChild(hdr);

    const body = _div('component-body');
    const renderedTags = new Set(['type']);

    // Find inherited component data for this specific component type
    let inhCompData = {};
    if (inherited && inherited.components) {
        const inhC = (Array.isArray(inherited.components) ? inherited.components : [])
            .find(c => c && c.type === compType);
        if (inhC) inhCompData = inhC;
    }

    // For inherited components, editing any field "localizes" the component first
    function compOnChange(tag, nv) {
        if (isInh) {
            const newIdx = localizeComponent(protoIdx, compType);
            if (newIdx < 0) return;
            setFieldValue([protoIdx, 'components', newIdx], tag, nv);
        } else {
            setFieldValue([protoIdx, 'components', compIdx], tag, nv);
        }
    }
    function compOnReset(tag) {
        if (!isInh && compIdx >= 0) deleteField([protoIdx, 'components', compIdx], tag);
    }

    if (cMeta) {
        for (const f of cMeta.fields) {
            renderedTags.add(f.tag);

            let source, value;
            if (isInh) {
                source = 'inherited';
                value = data[f.tag];
            } else if (Object.prototype.hasOwnProperty.call(data, f.tag)) {
                source = 'local';
                value = data[f.tag];
            } else if (inhCompData[f.tag] !== undefined) {
                source = 'inherited';
                value = inhCompData[f.tag];
            } else {
                source = 'default';
                value = undefined;
            }

            const onReset = (!isInh && source === 'local') ? () => compOnReset(f.tag) : null;
            body.appendChild(fieldRow(f.tag, f, value, source, nv => compOnChange(f.tag, nv), onReset));
        }
    }

    // Extra fields in YAML not in metadata
    for (const [k, v] of Object.entries(data)) {
        if (k === 'type' || k.startsWith('__') || renderedTags.has(k)) continue;
        const source = isInh ? 'inherited' : 'local';
        const onReset = (!isInh) ? () => compOnReset(k) : null;
        body.appendChild(genericRow(k, v, source, nv => compOnChange(k, nv), onReset));
    }

    card.appendChild(body);
    return card;
}

function collapseAllComponents(collapse) {
    const area = document.getElementById('editor-area');
    area.querySelectorAll('.component-card').forEach(card => {
        card.classList.toggle('collapsed', collapse);
    });
}

// ======================== DATA UPDATES =================================
// Unified field update: works for proto fields, component fields, and datadef fields.
// `path` is an array of keys to reach the target object, e.g.:
//   proto field:     [protoIdx]               → fs.yaml[protoIdx]
//   component field: [protoIdx, 'components', compIdx] → fs.yaml[protoIdx].components[compIdx]
function setFieldValue(path, tag, value) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs) return;
    let obj = fs.yaml;
    for (const key of path) { obj = obj?.[key]; }
    if (!obj) return;
    obj[tag] = value;
    state.resolvedCache.clear();
    commitChange(fs);
    scheduleRenderEditor();
}

function deleteField(path, tag) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs) return;
    let obj = fs.yaml;
    for (const key of path) { obj = obj?.[key]; }
    if (!obj) return;
    delete obj[tag];
    state.resolvedCache.clear();
    commitChange(fs);
    scheduleRenderEditor();
}



function commitChange(fs) {
    const nc = dumpYaml(fs.yaml);
    state.resolvedCache.clear();
    fs.pushHistory(nc); renderTabs(); scheduleAutosave(fs);
}

// ======================== AUTOSAVE =====================================
function scheduleAutosave(fs) {
    if (fs.readOnly) return;
    clearTimeout(fs._saveTimer);
    fs._saveTimer = setTimeout(async () => {
        try {
            await api.saveFile(fs.path, fs.content);
            fs.modified = false; renderTabs(); toast('Saved', 'success');
            try {
                const stamps = await api.fileStamps([fs.path]);
                if (stamps[fs.path]) state.fileStamps.set(fs.path, stamps[fs.path]);
            } catch {}
        }
        catch (e) {
            console.error('[Editor] Save failed:', fs.path, e);
            toast(`Save failed: ${e.message}`, 'error');
        }
    }, CFG.autosaveDelay);
}

// ======================== UNDO / REDO ==================================
function handleUndo() {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs || !fs.undo()) return;
    fs.yaml = parseYaml(fs.content);
    state.resolvedCache.clear();
    renderEditor(); renderTabs(); scheduleAutosave(fs);
}

function handleRedo() {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs || !fs.redo()) return;
    fs.yaml = parseYaml(fs.content);
    state.resolvedCache.clear();
    renderEditor(); renderTabs(); scheduleAutosave(fs);
}
