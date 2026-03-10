// ======================================================================
//  SS14 Prototype Redactor – Main Application
// ======================================================================

'use strict';

// ======================== CONFIGURATION ================================
const CFG = Object.freeze({
    maxDropdownItems : 50,
    autosaveDelay    : 800,
    searchDebounce   : 250,
    undoLimit        : 200,
    fileWatchInterval: 3000,  // ms – poll interval for external file changes
});

// ======================== STATE ========================================
const state = {
    metadata   : null,
    fileTree   : null,
    protoIndex : null,
    openFiles  : new Map(),
    currentFile: null,
    resolvedCache: new Map(),
    fileStamps : new Map(),       // path → last-known ticks from server
};

class FileState {
    constructor(path, content) {
        this.path       = path;
        this.content    = content;
        this.yaml       = null;
        this.modified   = false;
        this.history    = [content];
        this.historyIdx = 0;
        this._saveTimer = null;
    }
    pushHistory(nc) {
        this.history = this.history.slice(0, this.historyIdx + 1);
        this.history.push(nc);
        if (this.history.length > CFG.undoLimit) this.history.shift();
        else this.historyIdx++;
        this.content = nc; this.modified = true;
    }
    undo() { if (this.historyIdx <= 0) return false; this.content = this.history[--this.historyIdx]; this.modified = true; return true; }
    redo() { if (this.historyIdx >= this.history.length - 1) return false; this.content = this.history[++this.historyIdx]; this.modified = true; return true; }
}

// ======================== CUSTOM YAML SCHEMA ===========================
const _TagType = new jsyaml.Type('!type:', {
    kind: 'mapping', multi: true,
    construct(data, type) { data = data || {}; data.__yamlTag = type; return data; },
    predicate(obj) { return obj !== null && typeof obj === 'object' && !Array.isArray(obj) && '__yamlTag' in obj; },
    represent(obj) { const out = {}; for (const k of Object.keys(obj)) if (k !== '__yamlTag') out[k] = obj[k]; return out; },
    representName(obj) { return obj.__yamlTag; },
});
const SCHEMA = jsyaml.DEFAULT_SCHEMA.extend([_TagType]);

// ======================== API ==========================================
const api = {
    async get(u)      { const r = await fetch(u); if (!r.ok) throw new Error(r.statusText); return r.json(); },
    async post(u, b)  { const r = await fetch(u, { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(b) }); if (!r.ok) throw new Error(r.statusText); return r.json(); },
    loadMetadata()    { return this.get('/api/metadata'); },
    loadTree()        { return this.get('/api/tree'); },
    loadProtoIndex()  { return this.get('/api/proto-index'); },
    loadFile(p)       { return this.get(`/api/file?path=${encodeURIComponent(p)}`); },
    saveFile(p, c)    { return this.post(`/api/file?path=${encodeURIComponent(p)}`, { content: c }); },
    searchProtos(t, q, l = CFG.maxDropdownItems) { return this.get(`/api/search-protos?type=${encodeURIComponent(t)}&q=${encodeURIComponent(q)}&limit=${l}`); },
    refreshIndex()    { return this.get('/api/refresh-index'); },
    openInExplorer(p) { return this.get(`/api/open-in-explorer?path=${encodeURIComponent(p)}`); },
    openDefault(p)     { return this.get(`/api/open-default?path=${encodeURIComponent(p)}`); },
    renameFile(old,n) { return this.post('/api/rename-file', { oldPath: old, newName: n }); },
    deleteFile(p)     { return this.get(`/api/delete-file?path=${encodeURIComponent(p)}`); },
    createFile(dir,n,c){ return this.post('/api/create-file', { dir, name: n, content: c || '' }); },
    fileStamps(paths) { return this.post('/api/file-stamps', { paths }); },
};

// ======================== YAML HELPERS =================================
function parseYaml(text) {
    try { return jsyaml.load(text, { schema: SCHEMA }) || []; }
    catch (e) { console.error('YAML parse error', e); return []; }
}
function dumpYaml(data) {
    return jsyaml.dump(data, { schema: SCHEMA, indent: 2, lineWidth: -1, noRefs: true, quotingType: "'", forceQuotes: false, sortKeys: false });
}

// ======================== CONTEXT MENU =================================
let _ctxMenu = null;
function showContextMenu(x, y, items) {
    hideContextMenu();
    const m = _div('context-menu');
    m.style.left = x + 'px'; m.style.top = y + 'px';
    for (const it of items) {
        if (it === '---') { m.appendChild(_divClass('context-menu-sep')); continue; }
        const el = _div('context-menu-item' + (it.danger ? ' danger' : ''));
        el.textContent = it.label;
        el.addEventListener('click', () => { hideContextMenu(); it.action(); });
        m.appendChild(el);
    }
    document.body.appendChild(m);
    _ctxMenu = m;
    const rect = m.getBoundingClientRect();
    if (rect.right > window.innerWidth) m.style.left = (x - rect.width) + 'px';
    if (rect.bottom > window.innerHeight) m.style.top = (y - rect.height) + 'px';
}
function hideContextMenu() { if (_ctxMenu) { _ctxMenu.remove(); _ctxMenu = null; } }
document.addEventListener('click', hideContextMenu);
document.addEventListener('contextmenu', e => { if (!e.target.closest('.tree-item, .tab, .file-tree')) hideContextMenu(); });

// ======================== FILE TREE ====================================
function renderFileTree(nodes, container, filter = '') {
    container.innerHTML = '';
    const filtered = filterNodes(nodes, filter.toLowerCase());
    buildTreeDom(filtered, container, 0);
}
function filterNodes(nodes, q) {
    if (!q) return nodes;
    return nodes.map(n => {
        if (n.isDir) { const ch = filterNodes(n.children || [], q); return ch.length ? { ...n, children: ch } : null; }
        return n.name.toLowerCase().includes(q) ? n : null;
    }).filter(Boolean);
}
function buildTreeDom(nodes, parent, depth) {
    for (const n of nodes) {
        const el = document.createElement('div');
        el.className = `tree-item ${n.isDir ? 'tree-dir' : 'tree-file'}`;
        el.style.paddingLeft = `${12 + depth * 16}px`;
        if (n.isDir) {
            el.innerHTML = `<span class="tree-icon">▶</span><span class="tree-name">${esc(n.name)}</span>`;
            const childBox = _div('tree-children collapsed');
            el.addEventListener('click', e => {
                e.stopPropagation();
                const open = el.classList.toggle('expanded');
                el.querySelector('.tree-icon').textContent = open ? '▼' : '▶';
                childBox.classList.toggle('collapsed', !open);
            });
            el.addEventListener('contextmenu', e => {
                e.preventDefault(); e.stopPropagation();
                showContextMenu(e.clientX, e.clientY, [
                    { label: 'New File…', action: () => promptCreateFile(n.path) },
                    '---',
                    { label: 'Open in Explorer', action: () => api.openInExplorer(n.path) },
                ]);
            });
            parent.appendChild(el);
            buildTreeDom(n.children || [], childBox, depth + 1);
            parent.appendChild(childBox);
        } else {
            el.innerHTML = `<span class="tree-icon">📄</span><span class="tree-name">${esc(n.name)}</span>`;
            el.addEventListener('click', () => openFile(n.path));
            el.addEventListener('contextmenu', e => {
                e.preventDefault(); e.stopPropagation();
                showContextMenu(e.clientX, e.clientY, [
                    { label: 'Open', action: () => openFile(n.path) },
                    { label: 'Open in Explorer', action: () => api.openInExplorer(n.path) },
                    '---',
                    { label: 'New File…', action: () => promptCreateFile(n.path.includes('/') ? n.path.substring(0, n.path.lastIndexOf('/')) : '') },
                    '---',
                    { label: 'Rename…', action: () => promptRenameFile(n.path) },
                    { label: 'Delete', danger: true, action: () => promptDeleteFile(n.path) },
                ]);
            });
            parent.appendChild(el);
        }
    }
}

// Attach context menu to the file-tree background for "New File at root"
document.getElementById('file-tree').addEventListener('contextmenu', e => {
    if (e.target.closest('.tree-item')) return; // handled above
    e.preventDefault();
    showContextMenu(e.clientX, e.clientY, [
        { label: 'New File…', action: () => promptCreateFile('') },
    ]);
});

async function promptCreateFile(dir) {
    const name = prompt('Enter new file name:', 'new-prototype.yml');
    if (!name) return;
    try {
        const res = await api.createFile(dir, name, '');
        await refreshAll();
        openFile(res.path);
        toast('Created', 'success');
    } catch (e) { toast(`Create failed: ${e.message}`, 'error'); }
}

async function promptRenameFile(path) {
    const oldName = path.split('/').pop();
    const newName = prompt('Rename file:', oldName);
    if (!newName || newName === oldName) return;
    try {
        const res = await api.renameFile(path, newName);
        if (state.openFiles.has(path)) {
            const fs = state.openFiles.get(path);
            state.openFiles.delete(path);
            fs.path = res.newPath;
            state.openFiles.set(res.newPath, fs);
            if (state.currentFile === path) state.currentFile = res.newPath;
        }
        await refreshAll();
        toast('Renamed', 'success');
    } catch (e) { toast(`Rename failed: ${e.message}`, 'error'); }
}
async function promptDeleteFile(path) {
    if (!confirm(`Delete ${path.split('/').pop()}?`)) return;
    try {
        await api.deleteFile(path);
        if (state.openFiles.has(path)) closeTab(path);
        await refreshAll();
        toast('Deleted', 'success');
    } catch (e) { toast(`Delete failed: ${e.message}`, 'error'); }
}

// ======================== TABS =========================================
function renderTabs() {
    const box = document.getElementById('tabs');
    box.innerHTML = '';
    for (const [path, fs] of state.openFiles) {
        const tab = document.createElement('div');
        tab.className = `tab${path === state.currentFile ? ' active' : ''}`;
        tab.dataset.path = path;
        const shortName = path.split('/').pop() + (fs.modified ? ' •' : '');
        tab.innerHTML = `<span class="tab-name">${esc(shortName)}</span><button class="tab-close">×</button>`;
        tab.querySelector('.tab-name').addEventListener('click', () => switchTab(path));
        tab.querySelector('.tab-close').addEventListener('click', e => { e.stopPropagation(); closeTab(path); });
        tab.addEventListener('contextmenu', e => {
            e.preventDefault();
            showContextMenu(e.clientX, e.clientY, [
                { label: 'Close', action: () => closeTab(path) },
                { label: 'Close Others', action: () => closeOtherTabs(path) },
                { label: 'Close All', action: () => closeAllTabs() },
                '---',
                { label: 'Open with Default Editor', action: () => api.openDefault(path) },
                { label: 'Open in Explorer', action: () => api.openInExplorer(path) },
                { label: 'Copy Path', action: () => { navigator.clipboard.writeText(path).catch(()=>{}); toast('Copied', 'info'); } },
                '---',
                { label: 'Rename…', action: () => promptRenameFile(path) },
            ]);
        });
        box.appendChild(tab);
    }
}

async function openFile(path) {
    if (!state.openFiles.has(path)) {
        try {
            const { content } = await api.loadFile(path);
            const fs = new FileState(path, content);
            fs.yaml = parseYaml(content);
            state.openFiles.set(path, fs);
            // Record initial file stamp for change detection
            try {
                const stamps = await api.fileStamps([path]);
                if (stamps[path]) state.fileStamps.set(path, stamps[path]);
            } catch {}
        } catch (e) { toast(`Open failed: ${e.message}`, 'error'); return; }
    }
    state.currentFile = path;
    renderTabs(); renderEditor();
}
function switchTab(path) { if (!state.openFiles.has(path)) return; state.currentFile = path; renderTabs(); renderEditor(); }
function closeTab(path) {
    state.openFiles.delete(path);
    if (state.currentFile === path) { const k = [...state.openFiles.keys()]; state.currentFile = k.length ? k[k.length - 1] : null; }
    renderTabs(); renderEditor();
}
function closeOtherTabs(keep) {
    for (const p of [...state.openFiles.keys()]) if (p !== keep) state.openFiles.delete(p);
    state.currentFile = keep; renderTabs(); renderEditor();
}
function closeAllTabs() { state.openFiles.clear(); state.currentFile = null; renderTabs(); renderEditor(); }

// ======================== EDITOR =======================================
function renderEditor() {
    const area = document.getElementById('editor-area');
    if (!state.currentFile) {
        area.innerHTML = `<div class="empty-state"><div class="empty-icon">📋</div>
            <h2>SS14 Prototype Redactor</h2>
            <p>Open a YAML file from the sidebar to start editing prototypes visually.</p>
            <p class="hint">Ctrl+Z / Ctrl+Y — undo / redo &bull; Ctrl+S — force save</p></div>`;
        return;
    }
    const fs = state.openFiles.get(state.currentFile);
    if (!fs) return;
    const protos = fs.yaml;
    if (!Array.isArray(protos) || protos.length === 0) {
        area.innerHTML = '<div class="empty-state"><p>No prototypes found in this file.</p></div>';
        // Still show add-proto footer for empty files
        area.appendChild(buildAddProtoFooter());
        return;
    }
    area.innerHTML = '';
    for (let i = 0; i < protos.length; i++) area.appendChild(buildCard(protos[i], i));
    area.appendChild(buildAddProtoFooter());
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
        const lq = (q || '').toLowerCase();
        const filtered = lq ? types.filter(t => t.toLowerCase().includes(lq)) : types;
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
    const meta = state.metadata?.prototypes?.[type];
    if (meta?.inheriting) proto.parent = '';
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

    // header
    const hdr = _div('proto-header');
    hdr.innerHTML = `<span class="proto-type-badge">${esc(type)}</span>
        <span class="proto-id-text">${esc(String(id))}</span>
        <button class="delete-proto-btn" title="Delete prototype">×</button>
        <button class="collapse-btn">▼</button>`;
    hdr.querySelector('.collapse-btn').addEventListener('click', e => {
        e.stopPropagation();
        const c = card.classList.toggle('collapsed');
        hdr.querySelector('.collapse-btn').textContent = c ? '▶' : '▼';
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
    card.appendChild(hdr);

    // body
    const body = _div('proto-body');

    // Resolve inherited
    let inherited = {};
    if (inheriting && proto.parent) inherited = resolveInheritance(type, proto.parent);

    // Render ALL metadata fields (not just those in YAML)
    const renderedTags = new Set(['type']);
    if (meta) {
        for (const f of meta.fields) {
            if (f.isId || f.isParent || f.isAbstract) { renderedTags.add(f.tag); continue; }
            if (f.tag === 'components') { renderedTags.add('components'); continue; }
            renderedTags.add(f.tag);

            const local = proto[f.tag];
            const inh   = inherited[f.tag];
            const onlyInh = local === undefined && inh !== undefined;
            const value   = local !== undefined ? local : inh;

            // Always show all metadata fields, even if value is undefined
            body.appendChild(fieldRow(f.tag, f, value, onlyInh, v => updateField(idx, f.tag, v)));
        }
    }

    // Extra fields not in metadata (custom user fields in YAML)
    for (const [k, v] of Object.entries(proto)) {
        if (k.startsWith('__') || renderedTags.has(k)) continue;
        body.appendChild(genericRow(k, v, false, nv => updateField(idx, k, nv)));
    }

    // Inherited-only fields not yet shown
    for (const [k, v] of Object.entries(inherited)) {
        if (['type','id','parent','abstract','components'].includes(k)) continue;
        if (renderedTags.has(k) || proto[k] !== undefined) continue;
        body.appendChild(genericRow(k, v, true, nv => updateField(idx, k, nv)));
    }

    // Components section
    if (type === 'entity' || proto.components) {
        const cs = buildComponentsSection(proto, idx, inherited);
        if (cs) body.appendChild(cs);
    }

    card.appendChild(body);
    return card;
}

// ======================== COMPONENTS SECTION ============================
function buildComponentsSection(proto, protoIdx, inherited) {
    const sec = _div('components-section');
    sec.innerHTML = `<div class="components-header"><span>Components</span><button class="add-component-btn" title="Add component">+</button></div>`;

    const localComps = proto.components || [];
    const inhComps   = inherited.components || [];
    const localMap = new Map();
    localComps.forEach((c, i) => { if (c && c.type) localMap.set(c.type, { data: c, idx: i }); });
    const inhMap = new Map();
    for (const c of inhComps) { if (c && c.type && !localMap.has(c.type)) inhMap.set(c.type, c); }

    for (const [ct, { data, idx }] of localMap) sec.appendChild(compCard(ct, data, false, protoIdx, idx));
    for (const [ct, data]          of inhMap)   sec.appendChild(compCard(ct, data, true,  protoIdx, -1));
    return sec;
}

function compCard(compType, data, isInh, protoIdx, compIdx) {
    const card = _div('component-card' + (isInh ? ' inherited' : ''));
    const hdr = _div('component-header');
    hdr.innerHTML = `<span class="component-type">${esc(compType)}</span><button class="collapse-btn">▼</button>`;
    hdr.addEventListener('click', () => {
        const c = card.classList.toggle('collapsed');
        hdr.querySelector('.collapse-btn').textContent = c ? '▶' : '▼';
    });
    card.appendChild(hdr);

    const body = _div('component-body');
    const cMeta = state.metadata?.components?.[compType];
    const renderedTags = new Set(['type']);

    // Render ALL metadata fields for this component
    if (cMeta) {
        for (const f of cMeta.fields) {
            if (f.isId || f.isParent || f.isAbstract) { renderedTags.add(f.tag); continue; }
            renderedTags.add(f.tag);
            const val = data[f.tag];
            const isFieldInh = isInh || val === undefined;
            body.appendChild(fieldRow(f.tag, f, val, isInh, nv => updateComp(protoIdx, compIdx, f.tag, nv)));
        }
    }

    // Extra fields in YAML not in metadata
    for (const [k, v] of Object.entries(data)) {
        if (k === 'type' || k.startsWith('__') || renderedTags.has(k)) continue;
        body.appendChild(genericRow(k, v, isInh, nv => updateComp(protoIdx, compIdx, k, nv)));
    }

    card.appendChild(body);
    return card;
}

// ======================== FIELD RENDERERS ===============================
function fieldRow(key, meta, value, isInh, onChange) {
    const row = _div('field-row' + (isInh ? ' inherited' : ''));
    const lbl = _el('label'); lbl.className = 'field-label' + (meta.required ? ' required' : '');
    lbl.textContent = key;
    row.appendChild(lbl);
    row.appendChild(controlFor(meta, value, isInh, onChange));
    return row;
}
function genericRow(key, value, isInh, onChange) {
    const row = _div('field-row' + (isInh ? ' inherited' : ''));
    const lbl = _el('label'); lbl.className = 'field-label'; lbl.textContent = key;
    row.appendChild(lbl);
    row.appendChild(autoControl(value, isInh, onChange));
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
        default:
            // Check DataDefinition
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
    cp.value = typeof val === 'string' && val.startsWith('#') ? val.slice(0,7) : '#ffffff'; cp.disabled = dis;
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
    sel.innerHTML = `<option value="">-- select --</option>` + opts.map(o => `<option value="${esc(o)}"${o === val ? ' selected':''}>${esc(o)}</option>`).join('');
    sel.addEventListener('change', () => cb(sel.value));
    w.appendChild(sel); return w;
}
function searchDropdown(val, searchType, dis, cb) {
    const w = _div('field-control search-dropdown');
    const inp = _el('input'); inp.type = 'text'; inp.className = 'field-input dropdown-input';
    inp.value = val != null ? String(val) : ''; inp.disabled = dis;
    inp.placeholder = 'Search prototypes…'; inp.autocomplete = 'off';
    const dd = _div('dropdown-list');
    let timer, selIdx = -1;
    async function doSearch(q) {
        try { const res = await api.searchProtos(searchType, q); renderDd(dd, res, inp, cb); dd.classList.add('visible'); selIdx = -1; }
        catch (e) { console.error(e); }
    }
    inp.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => doSearch(inp.value), CFG.searchDebounce); });
    inp.addEventListener('focus', () => doSearch(inp.value));
    inp.addEventListener('blur', () => setTimeout(() => dd.classList.remove('visible'), 180));
    inp.addEventListener('change', () => { if (inp.value) cb(inp.value); });
    inp.addEventListener('keydown', e => {
        const items = dd.querySelectorAll('.dropdown-item');
        if (e.key === 'ArrowDown')  { e.preventDefault(); selIdx = Math.min(selIdx + 1, items.length - 1); hlDd(items, selIdx); }
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
        // Fallback to autoControl
        return autoControl(val, dis, onChange);
    }

    for (const f of ddMeta.fields) {
        if (f.isId || f.isParent || f.isAbstract) continue;
        const v = obj[f.tag];
        w.appendChild(fieldRow(f.tag, f, v, dis, nv => {
            obj[f.tag] = nv; onChange({ ...obj });
        }));
    }
    // Extra keys not in metadata
    for (const [k, v] of Object.entries(obj)) {
        if (k.startsWith('__')) continue;
        if (ddMeta.fields.some(f => f.tag === k)) continue;
        w.appendChild(genericRow(k, v, dis, nv => { obj[k] = nv; onChange({ ...obj }); }));
    }
    return w;
}

// ======================== ELEMENT HELPERS ===============================
function elementControl(kind, fullType, protoArg, val, dis, cb) {
    // Build a synthetic meta for the element
    const fakeMeta = { fieldKind: kind || 'text', protoTypeArg: protoArg, isDataDefinition: false };
    // Check if the fullType is a DataDefinition
    if (fullType && state.metadata?.dataDefinitions?.[fullType]) {
        return dataDefCtrl(val, fullType, dis, cb);
    }
    switch (kind) {
        case 'boolean':       return boolCtrl(val, dis, cb);
        case 'integer':       return intCtrl(val, dis, cb);
        case 'float':         return floatCtrl(val, dis, cb);
        case 'text':          return textCtrl(val, dis, cb);
        case 'color':         return colorCtrl(val, dis, cb);
        case 'entityProtoId': return searchDropdown(val, 'entity', dis, cb);
        case 'protoId':       return searchDropdown(val, protoArg || 'entity', dis, cb);
        default:              return autoControl(val, dis, cb);
    }
}

function defaultForKind(kind) {
    switch (kind) {
        case 'boolean': return false;
        case 'integer': return 0;
        case 'float':   return 0.0;
        case 'text': case 'entityProtoId': case 'protoId': case 'color': return '';
        default: return '';
    }
}

function autoControl(val, dis, cb) {
    if (val === null || val === undefined) return textCtrl('', dis, cb);
    if (typeof val === 'boolean') return boolCtrl(val, dis, cb);
    if (typeof val === 'number')  return Number.isInteger(val) ? intCtrl(val, dis, cb) : floatCtrl(val, dis, cb);
    if (typeof val === 'string')  return textCtrl(val, dis, cb);
    // Arrays → inline list editor (auto-detect element kind)
    if (Array.isArray(val)) {
        return listCtrl(val, { elementKind: inferKindFromArray(val), elementFullType: null, elementProtoTypeArg: null }, dis, cb);
    }
    // Objects → YAML textarea fallback
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
    if (typeof first === 'number')  return Number.isInteger(first) ? 'integer' : 'float';
    return 'text';
}

// ======================== DATA UPDATES =================================
function updateField(protoIdx, tag, value) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs || !fs.yaml[protoIdx]) return;
    fs.yaml[protoIdx][tag] = value;
    commitChange(fs);
}
function updateComp(protoIdx, compIdx, tag, value) {
    const fs = state.openFiles.get(state.currentFile);
    if (!fs || !fs.yaml[protoIdx]) return;
    const comps = fs.yaml[protoIdx].components;
    if (!comps || !comps[compIdx]) return;
    comps[compIdx][tag] = value;
    commitChange(fs);
}
function commitChange(fs) {
    const nc = dumpYaml(fs.yaml);
    fs.pushHistory(nc); renderTabs(); scheduleAutosave(fs);
}

// ======================== AUTOSAVE =====================================
function scheduleAutosave(fs) {
    clearTimeout(fs._saveTimer);
    fs._saveTimer = setTimeout(async () => {
        try {
            await api.saveFile(fs.path, fs.content);
            fs.modified = false; renderTabs(); toast('Saved', 'success');
            // Update stamp so poller doesn't detect our own save as external
            try {
                const stamps = await api.fileStamps([fs.path]);
                if (stamps[fs.path]) state.fileStamps.set(fs.path, stamps[fs.path]);
            } catch {}
        }
        catch (e) { toast(`Save failed: ${e.message}`, 'error'); }
    }, CFG.autosaveDelay);
}

// ======================== UNDO / REDO ==================================
function handleUndo() { const fs = state.openFiles.get(state.currentFile); if (!fs || !fs.undo()) return; fs.yaml = parseYaml(fs.content); renderEditor(); renderTabs(); scheduleAutosave(fs); }
function handleRedo() { const fs = state.openFiles.get(state.currentFile); if (!fs || !fs.redo()) return; fs.yaml = parseYaml(fs.content); renderEditor(); renderTabs(); scheduleAutosave(fs); }

// ======================== INHERITANCE ==================================
function resolveInheritance(type, parents) {
    if (!parents || !state.protoIndex) return {};
    const ids = Array.isArray(parents) ? parents : [parents];
    let merged = {};
    for (const pid of ids) { const pd = resolveProto(type, pid); if (pd) merged = deepMerge(merged, pd); }
    return merged;
}
function resolveProto(type, id) {
    const key = `${type}:${id}`;
    if (state.resolvedCache.has(key)) return state.resolvedCache.get(key);
    for (const [, fs] of state.openFiles) {
        if (!fs.yaml) continue;
        const p = fs.yaml.find(x => x.type === type && x.id === id);
        if (!p) continue;
        let data = { ...p };
        if (p.parent) data = deepMerge(resolveInheritance(type, p.parent), data);
        state.resolvedCache.set(key, data); return data;
    }
    return null;
}
function deepMerge(a, b) {
    const out = { ...a };
    for (const [k, v] of Object.entries(b)) {
        if (k.startsWith('__')) continue;
        if (v && typeof v === 'object' && !Array.isArray(v) && a[k] && typeof a[k] === 'object' && !Array.isArray(a[k])) out[k] = deepMerge(a[k], v);
        else if (v !== undefined) out[k] = v;
    }
    return out;
}

// ======================== TOAST ========================================
function toast(msg, type = 'info') {
    const c = document.getElementById('toast-container');
    const t = _div(`toast toast-${type}`); t.textContent = msg;
    c.appendChild(t);
    requestAnimationFrame(() => t.classList.add('visible'));
    setTimeout(() => { t.classList.remove('visible'); setTimeout(() => t.remove(), 300); }, 2200);
}

// ======================== HELPERS ======================================
function esc(s) { const d = document.createElement('div'); d.textContent = String(s); return d.innerHTML; }
function _el(tag) { return document.createElement(tag); }
function _div(cls) { const d = document.createElement('div'); if (cls) d.className = cls; return d; }
function _divClass(cls) { const d = document.createElement('div'); d.className = cls; return d; }

// ======================== REFRESH ======================================
async function refreshAll() {
    try {
        const [tree] = await Promise.all([api.loadTree(), api.refreshIndex()]);
        state.fileTree = tree;
        state.protoIndex = await api.loadProtoIndex();
        const treeEl = document.getElementById('file-tree');
        renderFileTree(state.fileTree, treeEl, document.getElementById('file-search').value);
    } catch (e) { toast(`Refresh error: ${e.message}`, 'error'); }
}

// ======================== KEYBOARD =====================================
document.addEventListener('keydown', e => {
    if (e.ctrlKey && e.key === 'z') { e.preventDefault(); handleUndo(); }
    else if (e.ctrlKey && e.key === 'y') { e.preventDefault(); handleRedo(); }
    else if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        const fs = state.openFiles.get(state.currentFile);
        if (fs) {
            clearTimeout(fs._saveTimer);
            api.saveFile(fs.path, fs.content).then(async () => {
                fs.modified = false; renderTabs(); toast('Saved', 'success');
                try { const st = await api.fileStamps([fs.path]); if (st[fs.path]) state.fileStamps.set(fs.path, st[fs.path]); } catch {}
            }).catch(e => toast(`Save error: ${e.message}`, 'error'));
        }
    }
});

// ======================== FILE WATCHER ==================================
async function pollFileChanges() {
    const paths = [...state.openFiles.keys()];
    if (!paths.length) return;
    try {
        const stamps = await api.fileStamps(paths);
        for (const [path, ticks] of Object.entries(stamps)) {
            if (ticks === -1) continue; // file deleted externally
            const prev = state.fileStamps.get(path);
            if (prev !== undefined && prev !== ticks) {
                // File changed externally — reload if not modified locally
                const fs = state.openFiles.get(path);
                if (fs && !fs.modified) {
                    try {
                        const { content } = await api.loadFile(path);
                        fs.content = content;
                        fs.yaml = parseYaml(content);
                        fs.history = [content]; fs.historyIdx = 0;
                        if (state.currentFile === path) renderEditor();
                        toast(`Reloaded: ${path.split('/').pop()}`, 'info');
                    } catch { /* ignore reload errors */ }
                } else if (fs && fs.modified) {
                    toast(`${path.split('/').pop()} changed externally (local edits kept)`, 'warning');
                }
            }
            state.fileStamps.set(path, ticks);
        }
    } catch { /* polling errors are non-critical */ }
}

// ======================== INIT =========================================
(async function init() {
    toast('Loading…', 'info');
    const results = await Promise.allSettled([
        api.loadMetadata().then(m => { state.metadata = m; }),
        api.loadTree().then(t => { state.fileTree = t; }),
        api.loadProtoIndex().then(i => { state.protoIndex = i; }),
    ]);
    if (!state.metadata)   state.metadata   = { prototypes: {}, components: {} };
    if (!state.protoIndex) state.protoIndex = {};

    const treeEl = document.getElementById('file-tree');
    if (state.fileTree) renderFileTree(state.fileTree, treeEl);
    document.getElementById('file-search').addEventListener('input', e => renderFileTree(state.fileTree || [], treeEl, e.target.value));
    document.getElementById('refresh-btn').addEventListener('click', () => refreshAll().then(() => toast('Refreshed', 'success')));

    // Start file change polling
    setInterval(pollFileChanges, CFG.fileWatchInterval);

    const failed = results.filter(r => r.status === 'rejected');
    if (failed.length) toast('Some data unavailable – build the project first', 'warning');
    else toast('Ready', 'success');
})();
