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
                if (entry.id && smartMatch(entry.id, q)) {
                    matchingFiles.add(entry.file);
                }
            }
        }
    }

    return nodes.map(n => {
        if (n.isDir) {
            // If the folder NAME matches, surface the whole subtree so the
            // user can browse it (instead of hiding everything inside).
            if (smartMatch(n.name, q))
                return n;
            const ch = filterTreeNodes(n.children || [], q);
            return ch.length ? { ...n, children: ch } : null;
        }
        // Match by file name OR by prototype ID in this file
        return (smartMatch(n.name, q) || matchingFiles.has(n.path)) ? n : null;
    }).filter(Boolean);
}

function buildTreeDom(nodes, parent, depth) {
    for (const n of nodes) {
        const el = document.createElement('div');
        el.className = `tree-item ${n.isDir ? 'tree-dir' : 'tree-file'}`;
        el.style.paddingLeft = `${12 + depth * 16}px`;
        if (n.isDir) {
            const expanded = state.expandedDirs?.has(n.path);
            el.innerHTML = `<span class="tree-icon">${expanded ? '▼' : '▶'}</span><span class="tree-name">${esc(n.name)}</span>`;
            if (expanded) el.classList.add('expanded');
            const childBox = _div('tree-children' + (expanded ? '' : ' collapsed'));
            el.addEventListener('click', e => {
                e.stopPropagation();
                const open = el.classList.toggle('expanded');
                el.querySelector('.tree-icon').textContent = open ? '▼' : '▶';
                childBox.classList.toggle('collapsed', !open);
                // Persist expansion across re-renders (refreshAll, search filter, etc.)
                if (open) state.expandedDirs.add(n.path);
                else state.expandedDirs.delete(n.path);
            });
            el.addEventListener('contextmenu', e => {
                e.preventDefault(); e.stopPropagation();
                const items = [
                    { label: 'New File…', action: () => promptCreateFile(n.path) },
                    { label: 'New Folder…', action: () => promptCreateFolder(n.path) },
                    '---',
                    { label: 'Open in Explorer', action: () => api.openInExplorer(n.path) },
                ];
                if (!n.readOnly) {
                    items.push('---');
                    items.push({ label: 'Rename Folder…', action: () => promptRenameFolder(n.path) });
                    items.push({ label: 'Delete Folder', danger: true, action: () => promptDeleteFolder(n.path) });
                }
                showContextMenu(e.clientX, e.clientY, items);
            });
            parent.appendChild(el);
            buildTreeDom(n.children || [], childBox, depth + 1);
            parent.appendChild(childBox);
        } else {
            el.innerHTML = `<span class="tree-icon">${n.readOnly ? '🔒' : '📄'}</span><span class="tree-name">${esc(n.name)}</span>`;
            el.addEventListener('click', () => openFile(n.path));
            el.addEventListener('contextmenu', e => {
                e.preventDefault(); e.stopPropagation();
                const items = [{ label: 'Open', action: () => openFile(n.path) }];
                if (!n.readOnly) {
                    items.push({ label: 'Open in Explorer', action: () => api.openInExplorer(n.path) });
                    items.push('---');
                    items.push({ label: 'New File…', action: () => promptCreateFile(n.path.includes('/') ? n.path.substring(0, n.path.lastIndexOf('/')) : '') });
                    items.push('---');
                    items.push({ label: 'Rename…', action: () => promptRenameFile(n.path) });
                    items.push({ label: 'Delete', danger: true, action: () => promptDeleteFile(n.path) });
                }
                showContextMenu(e.clientX, e.clientY, items);
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
        { label: 'New Folder…', action: () => promptCreateFolder('') },
    ]);
});

async function promptCreateFile(dir) {
    const name = prompt('Enter new file name:', 'new-prototype.yml');
    if (!name) return;
    try {
        const res = await api.createFile(dir, name, '');
        // Ensure every ancestor folder of the new file stays expanded so
        // the file is visible after the tree re-renders. Without this, the
        // whole explorer would appear to collapse on file creation.
        if (res?.path) {
            const parts = res.path.split('/');
            for (let i = 1; i < parts.length; i++)
                state.expandedDirs.add(parts.slice(0, i).join('/'));
        }
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

async function promptCreateFolder(parentDir) {
    const name = prompt('Enter new folder name:', 'NewFolder');
    if (!name) return;
    try {
        await api.createFolder(parentDir, name);
        await refreshAll();
        toast('Folder created', 'success');
    } catch (e) {
        console.error('[FileTree] Create folder failed:', e);
        toast(`Create folder failed: ${e.message}`, 'error');
    }
}

async function promptRenameFolder(path) {
    const oldName = path.split('/').pop();
    const newName = prompt('Rename folder:', oldName);
    if (!newName || newName === oldName) return;
    try {
        const res = await api.renameFolder(path, newName);
        // Re-map any open files that were under the renamed folder.
        const oldPrefix = path + '/';
        const newPrefix = res.newPath + '/';
        const remap = [];
        for (const [openPath, fs] of state.openFiles) {
            if (openPath.startsWith(oldPrefix)) {
                remap.push([openPath, newPrefix + openPath.slice(oldPrefix.length), fs]);
            }
        }
        for (const [oldP, newP, fs] of remap) {
            state.openFiles.delete(oldP);
            fs.path = newP;
            state.openFiles.set(newP, fs);
            if (state.currentFile === oldP) state.currentFile = newP;
        }
        await refreshAll();
        toast('Folder renamed', 'success');
    } catch (e) {
        console.error('[FileTree] Rename folder failed:', e);
        toast(`Rename folder failed: ${e.message}`, 'error');
    }
}

async function promptDeleteFolder(path) {
    const name = path.split('/').pop();
    // First attempt non-recursive; if folder is non-empty, ask for confirmation and retry recursively.
    try {
        if (!confirm(`Delete folder ${name}?`)) return;
        await api.deleteFolder(path, false);
        for (const openPath of [...state.openFiles.keys()]) {
            if (openPath.startsWith(path + '/')) closeTab(openPath);
        }
        await refreshAll();
        toast('Folder deleted', 'success');
    } catch (e) {
        // Server returns 409 "Folder not empty" — offer recursive delete.
        if (/Folder not empty/i.test(e.message)) {
            if (!confirm(`Folder "${name}" is not empty. Delete it and ALL its contents?`)) return;
            try {
                await api.deleteFolder(path, true);
                for (const openPath of [...state.openFiles.keys()]) {
                    if (openPath.startsWith(path + '/')) closeTab(openPath);
                }
                await refreshAll();
                toast('Folder deleted', 'success');
            } catch (e2) {
                console.error('[FileTree] Recursive delete failed:', e2);
                toast(`Delete folder failed: ${e2.message}`, 'error');
            }
        } else {
            console.error('[FileTree] Delete folder failed:', e);
            toast(`Delete folder failed: ${e.message}`, 'error');
        }
    }
}
