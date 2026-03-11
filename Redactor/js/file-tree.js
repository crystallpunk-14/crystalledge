// ======================================================================
//  SS14 Prototype Redactor – File Tree
// ======================================================================

'use strict';

function renderFileTree(nodes, container, filter = '') {
    container.innerHTML = '';
    const filtered = filterTreeNodes(nodes, filter.toLowerCase());
    buildTreeDom(filtered, container, 0);
}

function filterTreeNodes(nodes, q) {
    if (!q) return nodes;

    // Also search prototype IDs from the index
    const matchingFiles = new Set();
    if (state.protoIndex) {
        for (const entries of Object.values(state.protoIndex)) {
            for (const entry of entries) {
                if (entry.id && entry.id.toLowerCase().includes(q)) {
                    matchingFiles.add(entry.file);
                }
            }
        }
    }

    return nodes.map(n => {
        if (n.isDir) {
            const ch = filterTreeNodes(n.children || [], q);
            return ch.length ? { ...n, children: ch } : null;
        }
        // Match by file name OR by prototype ID in this file
        return (n.name.toLowerCase().includes(q) || matchingFiles.has(n.path)) ? n : null;
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
    if (e.target.closest('.tree-item')) return;
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
    } catch (e) {
        console.error('[FileTree] Create file failed:', e);
        toast(`Create failed: ${e.message}`, 'error');
    }
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
    } catch (e) {
        console.error('[FileTree] Rename file failed:', e);
        toast(`Rename failed: ${e.message}`, 'error');
    }
}

async function promptDeleteFile(path) {
    if (!confirm(`Delete ${path.split('/').pop()}?`)) return;
    try {
        await api.deleteFile(path);
        if (state.openFiles.has(path)) closeTab(path);
        await refreshAll();
        toast('Deleted', 'success');
    } catch (e) {
        console.error('[FileTree] Delete file failed:', e);
        toast(`Delete failed: ${e.message}`, 'error');
    }
}
