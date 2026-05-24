// ======================================================================
//  SS14 Prototype Redactor – API Client
// ======================================================================

'use strict';

const api = {
    async get(u) {
        const r = await fetch(u);
        if (!r.ok) {
            const text = await r.text().catch(() => r.statusText);
            console.error(`[API] GET ${u} → ${r.status}`, text);
            throw new Error(`${r.status}: ${text}`);
        }
        return r.json();
    },
    async post(u, b) {
        const r = await fetch(u, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(b) });
        if (!r.ok) {
            const text = await r.text().catch(() => r.statusText);
            console.error(`[API] POST ${u} → ${r.status}`, text);
            throw new Error(`${r.status}: ${text}`);
        }
        return r.json();
    },
    loadMetadata()    { return this.get('/api/metadata'); },
    loadTree()        { return this.get('/api/tree'); },
    loadProtoIndex()  { return this.get('/api/proto-index'); },
    loadFile(p)       { return this.get(`/api/file?path=${encodeURIComponent(p)}`); },
    saveFile(p, c)    { return this.post(`/api/file?path=${encodeURIComponent(p)}`, { content: c }); },
    searchProtos(t, q, l = CFG.maxDropdownItems) { return this.get(`/api/search-protos?type=${encodeURIComponent(t)}&q=${encodeURIComponent(q)}&limit=${l}`); },
    refreshIndex()    { return this.get('/api/refresh-index'); },
    openInExplorer(p) { return this.get(`/api/open-in-explorer?path=${encodeURIComponent(p)}`); },
    openDefault(p)    { return this.get(`/api/open-default?path=${encodeURIComponent(p)}`); },
    openSource(cls)   { return this.get(`/api/open-source?class=${encodeURIComponent(cls)}`); },
    renameFile(old,n) { return this.post('/api/rename-file', { oldPath: old, newName: n }); },
    deleteFile(p)     { return this.get(`/api/delete-file?path=${encodeURIComponent(p)}`); },
    createFile(dir,n,c){ return this.post('/api/create-file', { dir, name: n, content: c || '' }); },
    createFolder(dir, name) { return this.post('/api/create-folder', { dir, name }); },
    renameFolder(oldPath, newName) { return this.post('/api/rename-folder', { oldPath, newName }); },
    deleteFolder(path, recursive = false) { return this.post('/api/delete-folder', { path, recursive }); },
    fileStamps(paths) { return this.post('/api/file-stamps', { paths }); },
    renameProtoId(path, oldId, newId, type) { return this.post('/api/rename-proto-id', { path, oldId, newId, type }); },
};
