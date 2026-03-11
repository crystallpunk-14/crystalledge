// ======================================================================
//  SS14 Prototype Redactor – Context Menu
// ======================================================================

'use strict';

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
document.addEventListener('contextmenu', e => { if (!e.target.closest('.tree-item, .tab, .file-tree, .proto-header, .component-header, .editor-area')) hideContextMenu(); });
