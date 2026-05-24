// ======================================================================
//  SS14 Prototype Redactor – Initialization & Keyboard Shortcuts
// ======================================================================

'use strict';

// ======================== REFRESH ======================================
async function refreshAll() {
    try {
        const [tree] = await Promise.all([api.loadTree(), api.refreshIndex()]);
        state.fileTree = tree;
        state.protoIndex = await api.loadProtoIndex();
        state.resolvedCache.clear();
        const treeEl = document.getElementById('file-tree');
        renderFileTree(state.fileTree, treeEl, document.getElementById('file-search').value);
    } catch (e) {
        console.error('[Init] Refresh failed:', e);
        toast(`Refresh error: ${e.message}`, 'error');
    }
}

// ======================== KEYBOARD =====================================
document.addEventListener('keydown', e => {
    if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        const fs = state.openFiles.get(state.currentFile);
        if (fs) {
            clearTimeout(fs._saveTimer);
            api.saveFile(fs.path, fs.content).then(async () => {
                fs.modified = false; renderTabs(); toast('Saved', 'success');
                try { const st = await api.fileStamps([fs.path]); if (st[fs.path]) state.fileStamps.set(fs.path, st[fs.path]); } catch {}
            }).catch(e => {
                console.error('[Keyboard] Manual save failed:', e);
                toast(`Save error: ${e.message}`, 'error');
            });
        }
    }
});

// ======================== FILE WATCHER (SSE) ============================
// The server pushes "file-change" events over an SSE channel; we react by
// reloading any open file whose external timestamp changed and the user hasn't
// modified locally. Falls back silently if EventSource is unavailable.
function startFileEventStream() {
    if (typeof EventSource === 'undefined') {
        console.warn('[FileWatcher] EventSource unavailable; live reload disabled.');
        return;
    }
    let backoff = 1000;
    function connect() {
        const es = new EventSource('/api/events');
        es.onopen = () => { backoff = 1000; };
        es.onmessage = async (ev) => {
            let payload;
            try { payload = JSON.parse(ev.data); } catch { return; }
            if (!payload || payload.type !== 'file-change') return;
            const path = payload.path;
            if (!path) return;
            const fs = state.openFiles.get(path);
            if (!fs) return;
            if (payload.kind === 'deleted') {
                toast(`${path.split('/').pop()} was deleted externally`, 'warning');
                return;
            }
            if (fs.modified) {
                toast(`${path.split('/').pop()} changed externally (local edits kept)`, 'warning');
                return;
            }
            try {
                const { content } = await api.loadFile(path);
                fs.content = content;
                fs.yaml = parseYaml(content);
                fs.history = [content];
                fs.historyIdx = 0;
                state.resolvedCache.clear();
                if (state.currentFile === path) renderEditor();
                toast(`Reloaded: ${path.split('/').pop()}`, 'info');
            } catch (e) {
                console.error('[FileWatcher] Reload failed:', path, e);
            }
        };
        es.onerror = () => {
            es.close();
            // Reconnect with capped exponential backoff.
            backoff = Math.min(backoff * 2, 30000);
            setTimeout(connect, backoff);
        };
    }
    connect();
}

// ======================== INIT =========================================
(async function init() {
    console.log('[Redactor] Initializing...');
    toast('Loading…', 'info');
    const results = await Promise.allSettled([
        api.loadMetadata().then(m => { state.metadata = m; console.log('[Init] Metadata loaded:', Object.keys(m.prototypes || {}).length, 'prototypes,', Object.keys(m.components || {}).length, 'components'); }),
        api.loadTree().then(t => { state.fileTree = t; console.log('[Init] File tree loaded'); }),
        api.loadProtoIndex().then(i => { state.protoIndex = i; console.log('[Init] Proto index loaded:', Object.values(i).reduce((s, a) => s + a.length, 0), 'entries'); }),
    ]);
    if (!state.metadata)   state.metadata   = { prototypes: {}, components: {} };
    if (!state.protoIndex) state.protoIndex = {};

    const treeEl = document.getElementById('file-tree');
    if (state.fileTree) renderFileTree(state.fileTree, treeEl);
    let _searchTimer;
    document.getElementById('file-search').addEventListener('input', e => {
        clearTimeout(_searchTimer);
        const q = e.target.value;
        _searchTimer = setTimeout(() => renderFileTree(state.fileTree || [], treeEl, q), CFG.searchDebounce);
    });
    document.getElementById('refresh-btn').addEventListener('click', () => refreshAll().then(() => toast('Refreshed', 'success')));

    // Start file change push channel (SSE)
    startFileEventStream();

    const failed = results.filter(r => r.status === 'rejected');
    if (failed.length) {
        console.warn('[Init] Some data unavailable:', failed.map(r => r.reason));
        toast('Some data unavailable – build the project first', 'warning');
    } else {
        console.log('[Redactor] Ready');
        toast('Ready', 'success');
    }
})();
