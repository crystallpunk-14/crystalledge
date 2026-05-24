// ======================================================================
//  SS14 Prototype Redactor – State Management
// ======================================================================

'use strict';

const state = {
    metadata   : null,
    fileTree   : null,
    protoIndex : null,
    openFiles  : new Map(),
    currentFile: null,
    resolvedCache: new Map(),
    fileStamps : new Map(),       // path → last-known ticks from server
    parentFileCache: new Map(),   // filePath → parsed yaml array (for inheritance lookup)
    expandedDirs: new Set(),      // tree directory paths that are currently expanded (preserved across re-renders)
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
