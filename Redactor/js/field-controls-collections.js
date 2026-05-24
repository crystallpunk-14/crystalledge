// ======================================================================
//  SS14 Prototype Redactor – Collection & DataDefinition Editors
// ======================================================================
//  listCtrl / mapCtrl / dataDefCtrl extracted from fields.js.
//  Depends on globals from fields.js: elementControl, defaultForKind,
//  fieldRow, genericRow, autoControl.
// ======================================================================

'use strict';

// ======================== POLYMORPHIC TYPE PICKER ======================
//  Searchable floating popup for choosing a concrete `!type:` from the
//  list of implementors of a polymorphic DataDefinition base. Behaves
//  similarly to the ProtoId search dropdown – input on top, filterable
//  list below, keyboard navigation, click-outside / Escape to close.
//
//    @param {HTMLElement} anchorEl   – element used to position the popup
//    @param {string[]}    items      – full type names (e.g.
//                                      "Content.Shared._CE.EntityEffect.Effects.ThrowToUser")
//    @param {string|null} currentFn  – currently selected full name, or null
//    @param {(fn:string|null)=>void} onPick – called with picked full name,
//                                            or null for "(base)"
//    @param {boolean} allowBase      – if true, show a "(base)" entry that
//                                      calls onPick(null) to clear the tag
// =======================================================================
function showSearchableTypePicker(anchorEl, items, currentFn, onPick, allowBase = false) {
    // Tear down any existing instance so two popups never coexist.
    document.querySelectorAll('.searchable-picker-popup').forEach(n => n.remove());

    const pop = _div('searchable-picker-popup');
    const inp = _el('input');
    inp.type = 'text'; inp.className = 'field-input';
    inp.placeholder = 'Search types…'; inp.autocomplete = 'off';
    const list = _div('searchable-picker-list');
    pop.append(inp, list);

    let selIdx = -1;
    let filtered = [];

    function render() {
        const q = inp.value;
        list.innerHTML = '';
        filtered = [];
        if (allowBase) filtered.push(null);
        for (const it of items) {
            // smart search: matches either the fully-qualified name OR the
            // short name (last segment after '.'), with subsequence + multi-
            // token support — e.g. "throw stam" matches CEStaminaThrowable.
            if (smartMatch(it, q) || smartMatch(it.split('.').pop(), q))
                filtered.push(it);
        }
        if (filtered.length === 0) {
            const empty = _div('dropdown-empty'); empty.textContent = 'No matches';
            list.appendChild(empty);
            return;
        }
        filtered.forEach((fn, i) => {
            const row = _div('dropdown-item');
            if (fn === null) {
                row.innerHTML = '<span class="dropdown-id" style="font-style:italic;color:var(--text-muted);">(base / no !type:)</span>';
            } else {
                const short = fn.split('.').pop();
                const ns = fn.substring(0, fn.length - short.length - 1);
                row.innerHTML = `<span class="dropdown-id">${esc(short)}</span><span class="dropdown-name">${esc(ns)}</span>`;
            }
            if (fn === currentFn) row.classList.add('selected');
            row.addEventListener('mousedown', e => {
                e.preventDefault();
                close();
                onPick(fn);
            });
            list.appendChild(row);
        });
        selIdx = filtered.findIndex(f => f === currentFn);
        highlight();
    }

    function highlight() {
        const rows = list.querySelectorAll('.dropdown-item');
        rows.forEach((r, i) => r.classList.toggle('selected', i === selIdx));
        if (selIdx >= 0 && rows[selIdx]) rows[selIdx].scrollIntoView({ block: 'nearest' });
    }

    function pickCurrent() {
        if (selIdx < 0 || selIdx >= filtered.length) return;
        const fn = filtered[selIdx];
        close();
        onPick(fn);
    }

    function close() {
        pop.remove();
        document.removeEventListener('mousedown', outsideHandler, true);
        document.removeEventListener('keydown', escHandler, true);
    }
    function outsideHandler(e) { if (!pop.contains(e.target)) close(); }
    function escHandler(e) { if (e.key === 'Escape') { e.preventDefault(); close(); } }

    inp.addEventListener('input', render);
    inp.addEventListener('keydown', e => {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selIdx = Math.min(selIdx + 1, filtered.length - 1);
            highlight();
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selIdx = Math.max(selIdx - 1, 0);
            highlight();
        } else if (e.key === 'Enter') {
            e.preventDefault();
            pickCurrent();
        }
    });

    // Position the popup just below the anchor element.
    const r = anchorEl.getBoundingClientRect();
    pop.style.position = 'fixed';
    pop.style.left = r.left + 'px';
    pop.style.top = (r.bottom + 2) + 'px';
    pop.style.minWidth = Math.max(r.width, 280) + 'px';
    pop.style.zIndex = '10000';

    document.body.appendChild(pop);
    render();
    inp.focus();

    // Flip up if the popup overflows the viewport.
    const pr = pop.getBoundingClientRect();
    if (pr.bottom > window.innerHeight) {
        pop.style.top = (r.top - pr.height - 2) + 'px';
    }

    // Defer outside-click binding by one tick so the click that opened
    // the popup doesn't immediately close it.
    setTimeout(() => document.addEventListener('mousedown', outsideHandler, true), 0);
    document.addEventListener('keydown', escHandler, true);
}

// ======================== LIST EDITOR ==================================
function listCtrl(val, meta, dis, onChange) {
    const arr = Array.isArray(val) ? [...val] : [];
    const w = _div('field-control list-editor');

    function rebuild() {
        w.innerHTML = '';
        arr.forEach((item, i) => {
            const row = _div('list-item');
            // Drag handle on the left so input clicks inside the row
            // don't accidentally initiate a drag.
            if (!dis) {
                const handle = _div('drag-handle');
                handle.textContent = '⋮⋮';
                handle.draggable = true;
                handle.title = 'Drag to reorder';
                handle.addEventListener('dragstart', e => {
                    e.dataTransfer.effectAllowed = 'move';
                    e.dataTransfer.setData('text/plain', String(i));
                    row.classList.add('dragging');
                });
                handle.addEventListener('dragend', () => row.classList.remove('dragging'));
                row.appendChild(handle);
            }
            const content = _div('list-item-content');
            content.appendChild(elementControl(meta.elementKind, meta.elementFullType, meta.elementProtoTypeArg, item, dis, nv => {
                arr[i] = nv; onChange([...arr]);
            }, {
                // Forward one level of inner-generic info so a List<List<X>>
                // element still gets its X element kind/type when rendered.
                elementKind: meta.elementElementKind,
                elementFullType: meta.elementElementFullType,
                elementProtoTypeArg: meta.elementElementProtoTypeArg,
                // Enum values for List<EnumType>.
                enumValues: meta.elementEnumValues,
            }));
            row.appendChild(content);
            if (!dis) {
                row.addEventListener('dragover', e => {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    row.classList.add('drop-target');
                });
                row.addEventListener('dragleave', () => row.classList.remove('drop-target'));
                row.addEventListener('drop', e => {
                    e.preventDefault();
                    row.classList.remove('drop-target');
                    const from = parseInt(e.dataTransfer.getData('text/plain'), 10);
                    const to = i;
                    if (Number.isNaN(from) || from === to) return;
                    const [moved] = arr.splice(from, 1);
                    arr.splice(to, 0, moved);
                    onChange([...arr]); rebuild();
                });

                const rm = _el('button'); rm.className = 'item-remove-btn'; rm.textContent = '×'; rm.title = 'Remove';
                rm.addEventListener('click', () => { arr.splice(i, 1); onChange([...arr]); rebuild(); });
                row.appendChild(rm);
            }
            w.appendChild(row);
        });
        if (!dis) {
            const addRow = _div('list-add-row');
            const addBtn = _el('button'); addBtn.className = 'list-add-btn'; addBtn.textContent = '+ Add item';
            // Prefer a DataDefinition shape ({}) when the element type is a
            // known DataDefinition (e.g. PrototypeLayerData) – `elementKind`
            // alone can fall back to 'text' for unknown classes, which would
            // otherwise produce an empty-string item that renders as a plain
            // text input instead of the proper structured editor.
            const isDD = !!(meta.elementFullType && state.metadata?.dataDefinitions?.[meta.elementFullType]);
            const impls = (meta.elementFullType && state.metadata?.polymorphicTypes?.[meta.elementFullType]) || null;
            addBtn.addEventListener('click', e => {
                if (impls && impls.length > 0) {
                    // Polymorphic base – open the searchable type picker
                    // so the user can filter the (potentially long) list
                    // of concrete implementors and pick the desired
                    // `!type:` explicitly.
                    e.stopPropagation();
                    showSearchableTypePicker(addBtn, impls, null, fullName => {
                        if (!fullName) return;
                        arr.push({ __yamlTag: fullName.split('.').pop() });
                        onChange([...arr]); rebuild();
                    }, false);
                    return;
                }
                const next = isDD ? {} : defaultForKind(meta.elementKind);
                arr.push(next);
                onChange([...arr]); rebuild();
            });
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
            }, {
                // Forward inner element info for Dictionary<K, List<V>> so the
                // nested list editor picks up V's elementKind/elementFullType
                // (and detects polymorphic DataDefinition bases like CEEntityEffect).
                elementKind: meta.valueElementKind,
                elementFullType: meta.valueElementFullType,
                elementProtoTypeArg: meta.valueElementProtoTypeArg,
                // Enum values for Dictionary<K, EnumType>.
                enumValues: meta.valueEnumValues,
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
            // ── Typed key control ─────────────────────────────────────
            // Map keys honor `keyKind` from the metadata extractor – so a
            // `Dictionary<ProtoId<EntityPrototype>, …>` gets a prototype
            // search dropdown for the key, an enum-keyed dictionary gets
            // an enum select, etc. Falls back to a plain text input when
            // the kind isn't known.
            let pendingKey = '';
            const keyHost = _div('map-key-host');
            function makeKeyCtrl() {
                keyHost.innerHTML = '';
                const c = elementControl(
                    meta.keyKind || 'text',
                    meta.keyFullType,
                    meta.keyProtoTypeArg,
                    pendingKey,
                    dis,
                    nv => { pendingKey = nv; },
                    // Forward enum values so an enum-keyed dictionary
                    // (e.g. Dictionary<CEUseType, …>) gets the proper enum
                    // selector instead of a degenerate empty text input.
                    { enumValues: meta.keyEnumValues }
                );
                keyHost.appendChild(c);
            }
            makeKeyCtrl();
            function readPendingKey() {
                // Text inputs may not have committed via change yet at the
                // moment of clicking Add – read the live DOM value as a
                // safety net. For non-input controls (enum <select>,
                // ProtoId search dropdown, …) fall back to the pendingKey
                // state which is updated through the standard onChange.
                const inp = keyHost.querySelector('input');
                if (inp && inp.value !== '' && inp.value != null) return String(inp.value).trim();
                const sel = keyHost.querySelector('select');
                if (sel && sel.value !== '' && sel.value != null) return String(sel.value).trim();
                return pendingKey != null ? String(pendingKey).trim() : '';
            }

            const addBtn = _el('button'); addBtn.className = 'map-add-btn'; addBtn.textContent = '+ Add entry';
            const isDD = !!(meta.valueFullType && state.metadata?.dataDefinitions?.[meta.valueFullType]);
            const impls = (meta.valueFullType && state.metadata?.polymorphicTypes?.[meta.valueFullType]) || null;
            addBtn.addEventListener('click', e => {
                const k = readPendingKey(); if (!k) return;
                if (Object.prototype.hasOwnProperty.call(obj, k)) {
                    // Refuse to silently overwrite an existing entry.
                    return;
                }
                if (impls && impls.length > 0) {
                    // Polymorphic value type – open the searchable type
                    // picker so the user can pick the concrete subtype.
                    e.stopPropagation();
                    showSearchableTypePicker(addBtn, impls, null, fullName => {
                        if (!fullName) return;
                        obj[k] = { __yamlTag: fullName.split('.').pop() };
                        pendingKey = '';
                        onChange({ ...obj }); rebuild();
                    }, false);
                    return;
                }
                const next = isDD ? {} : defaultForKind(meta.valueKind);
                obj[k] = next;
                pendingKey = '';
                onChange({ ...obj }); rebuild();
            });
            addRow.append(keyHost, addBtn);
            w.appendChild(addRow);
        }
    }
    rebuild();
    return w;
}

// ======================== DATADEFINITION EDITOR =========================
function dataDefCtrl(val, ddType, dis, onChange) {
    const obj = (val && typeof val === 'object') ? { ...val } : {};
    const w = _div('field-control datadef-inline');

    // ── Polymorphic resolution ───────────────────────────────────────
    // If the value carries an explicit `!type:` tag (preserved as
    // `__yamlTag` during YAML round-trip), render the *concrete* subtype's
    // fields rather than the abstract base's. This is what makes
    // `List<CEEntityEffect>` items show CEDamageEffect's fields when their
    // tag says `!type:CEDamageEffect`.
    const tag = obj.__yamlTag;
    let effectiveType = ddType;
    if (tag) {
        // Tag is the YAML short form (e.g. "CEDamageEffect"). Resolve to
        // the full type by scanning known polymorphic implementors of the
        // declared base type.
        const impls = state.metadata?.polymorphicTypes?.[ddType] || [];
        const matched = impls.find(fn => fn === tag || fn.endsWith('.' + tag) || fn.split('.').pop() === tag);
        if (matched) effectiveType = matched;
        else if (state.metadata?.dataDefinitions?.[tag]) effectiveType = tag;
    }

    const ddMeta = state.metadata?.dataDefinitions?.[effectiveType];

    // ── Subtype selector ─────────────────────────────────────────────
    // For abstract / polymorphic bases, show a searchable picker button
    // that lets the user switch the concrete `!type:` at this site
    // without hand-editing YAML.
    const impls = state.metadata?.polymorphicTypes?.[ddType] || [];
    if (impls.length > 0 && !dis) {
        const row = _div('datadef-type-row');
        const lbl = _el('label'); lbl.className = 'datadef-type-label'; lbl.textContent = '!type:';
        const btn = _el('button');
        btn.type = 'button';
        btn.className = 'datadef-type-btn';
        const currentFn = effectiveType !== ddType ? effectiveType : null;
        btn.textContent = currentFn ? currentFn.split('.').pop() : '(base)';
        btn.title = currentFn || '(base — no !type: tag)';
        btn.addEventListener('click', e => {
            e.stopPropagation();
            showSearchableTypePicker(btn, impls, currentFn, chosenFn => {
                // Resolve the new effective type's field schema so we can
                // strip values that don't exist on it. Without this, fields
                // from the previous concrete type would silently linger in
                // the YAML and surface as "unknown" generic rows.
                const newEffType = chosenFn || ddType;
                const newDdMeta = state.metadata?.dataDefinitions?.[newEffType];
                const allowedTags = new Set((newDdMeta?.fields || []).map(f => f.tag));

                const next = {};
                if (chosenFn) next.__yamlTag = chosenFn.split('.').pop();
                for (const [k, v] of Object.entries(obj)) {
                    // Preserve internal metadata other than __yamlTag.
                    if (k === '__yamlTag') continue;
                    if (k.startsWith('__')) { next[k] = v; continue; }
                    // Keep only fields that exist on the new effective type.
                    if (allowedTags.has(k)) next[k] = v;
                }
                onChange(next);
            }, true);
        });
        row.append(lbl, btn);
        w.appendChild(row);
    }

    if (!ddMeta || !ddMeta.fields || ddMeta.fields.length === 0) {
        // No fields known – at least keep the type-tag selector above and
        // fall back to a raw-YAML stub for the body so power users aren't
        // locked out.
        if (impls.length === 0) return autoControl(val, dis, onChange);
        w.appendChild(unsupportedStub(effectiveType, obj, onChange));
        return w;
    }

    for (const f of ddMeta.fields) {
        if (f.isId || f.isParent || f.isAbstract) continue;
        const v = obj[f.tag];
        const src = v !== undefined ? 'local' : 'default';
        // Each declared field gets the standard override/reset codepath:
        // an explicit value flips `src` to 'local' (which renders the
        // override bar + reset button), and the reset handler deletes the
        // key from the object so it falls back to the field default.
        w.appendChild(fieldRow(f.tag, f, v, src, nv => {
            obj[f.tag] = nv; onChange({ ...obj });
        }, () => {
            delete obj[f.tag]; onChange({ ...obj });
        }));
    }
    for (const [k, v] of Object.entries(obj)) {
        if (k.startsWith('__')) continue;
        if (ddMeta.fields.some(f => f.tag === k)) continue;
        w.appendChild(genericRow(k, v, 'local', nv => { obj[k] = nv; onChange({ ...obj }); }, () => {
            delete obj[k]; onChange({ ...obj });
        }));
    }
    return w;
}
