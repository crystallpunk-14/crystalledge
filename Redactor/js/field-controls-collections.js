// ======================================================================
//  SS14 Prototype Redactor – Collection & DataDefinition Editors
// ======================================================================
//  listCtrl / mapCtrl / dataDefCtrl extracted from fields.js.
//  Depends on globals from fields.js: elementControl, defaultForKind,
//  fieldRow, genericRow, autoControl.
// ======================================================================

'use strict';

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
            addBtn.addEventListener('click', () => { arr.push(defaultForKind(meta.elementKind)); rebuild(); });
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
        return autoControl(val, dis, onChange);
    }

    for (const f of ddMeta.fields) {
        if (f.isId || f.isParent || f.isAbstract) continue;
        const v = obj[f.tag];
        const src = v !== undefined ? 'local' : 'default';
        w.appendChild(fieldRow(f.tag, f, v, src, nv => {
            obj[f.tag] = nv; onChange({ ...obj });
        }, null));
    }
    for (const [k, v] of Object.entries(obj)) {
        if (k.startsWith('__')) continue;
        if (ddMeta.fields.some(f => f.tag === k)) continue;
        w.appendChild(genericRow(k, v, 'local', nv => { obj[k] = nv; onChange({ ...obj }); }, null));
    }
    return w;
}
