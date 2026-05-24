// ======================================================================
//  SS14 Prototype Redactor – Custom YAML Schema & Helpers
// ======================================================================

'use strict';

const _TagType = new jsyaml.Type('!type:', {
    kind: 'mapping', multi: true,
    // js-yaml's multi-tag callback passes the FULL tag URI (e.g.
    // '!type:CEDamageEffect') in the `type` argument, not just the suffix.
    // Strip the registered prefix so `__yamlTag` stores the bare short name
    // (e.g. 'CEDamageEffect') — that's what the picker code, polymorphic
    // resolver, and represent() codepath all assume.
    construct(data, type) {
        data = data || {};
        const tag = typeof type === 'string' ? type.replace(/^!type:/, '') : type;
        data.__yamlTag = tag;
        return data;
    },
    predicate(obj) { return obj !== null && typeof obj === 'object' && !Array.isArray(obj) && '__yamlTag' in obj; },
    represent(obj) { const out = {}; for (const k of Object.keys(obj)) if (k !== '__yamlTag') out[k] = obj[k]; return out; },
    representName(obj) { return obj.__yamlTag; },
});
const SCHEMA = jsyaml.DEFAULT_SCHEMA.extend([_TagType]);

function parseYaml(text) {
    try { return jsyaml.load(text, { schema: SCHEMA }) || []; }
    catch (e) { console.error('YAML parse error', e); return []; }
}

// `type` and `id` are the structural YAML discriminators every prototype
// must carry — they always come first.  Everything else (parent, abstract,
// name, description, …) is taken from the metadata field order so the YAML
// matches the redactor's visual layout exactly.
const _PROTO_STRUCTURAL_HEAD = ['type', 'id'];

/**
 * Returns a copy of `obj` whose keys are in metadata-defined order so the
 * serialized YAML matches the visual layout of the redactor, not the
 * accidental order in which the user happened to override fields.
 */
function _orderKeys(obj, fieldOrder, structuralHead) {
    if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return obj;
    const seen = new Set();
    const out = {};
    if (obj.__yamlTag !== undefined) { out.__yamlTag = obj.__yamlTag; seen.add('__yamlTag'); }
    if (structuralHead) {
        for (const k of structuralHead) {
            if (Object.prototype.hasOwnProperty.call(obj, k) && !seen.has(k)) {
                out[k] = obj[k]; seen.add(k);
            }
        }
    }
    if (fieldOrder) {
        for (const tag of fieldOrder) {
            if (Object.prototype.hasOwnProperty.call(obj, tag) && !seen.has(tag)) {
                out[tag] = obj[tag]; seen.add(tag);
            }
        }
    }
    for (const k of Object.keys(obj)) {
        if (!seen.has(k)) out[k] = obj[k];
    }
    return out;
}

function _fieldOrderFor(meta) {
    if (!meta?.fields) return null;
    return meta.fields.map(f => f.tag);
}

function _canonicalizeProto(proto) {
    if (!proto || typeof proto !== 'object') return proto;
    const type = proto.type;
    const metaProto = (typeof state !== 'undefined' && state.metadata?.prototypes?.[type]) || null;
    const fieldOrder = _fieldOrderFor(metaProto);
    const ordered = _orderKeys(proto, fieldOrder, _PROTO_STRUCTURAL_HEAD);
    if (Array.isArray(ordered.components)) {
        ordered.components = ordered.components.map(c => _canonicalizeComponent(c));
    }
    return ordered;
}

function _canonicalizeComponent(comp) {
    if (!comp || typeof comp !== 'object') return comp;
    const compType = comp.type;
    const metaComp = (typeof state !== 'undefined' && state.metadata?.components?.[compType]) || null;
    const fieldOrder = _fieldOrderFor(metaComp);
    // `type` must always be the first key inside a component mapping –
    // this is both convention (matches every existing SS14 prototype file)
    // and what humans expect when scanning a component list.
    return _orderKeys(comp, fieldOrder, ['type']);
}

function dumpYaml(data) {
    if (Array.isArray(data)) {
        return data.map(item =>
            _fixupTypeTags(jsyaml.dump([_canonicalizeProto(item)], { schema: SCHEMA, indent: 2, lineWidth: -1, noRefs: true, quotingType: "'", forceQuotes: false, sortKeys: false }).trimEnd())
        ).join('\n\n') + '\n';
    }
    return _fixupTypeTags(jsyaml.dump(data, { schema: SCHEMA, indent: 2, lineWidth: -1, noRefs: true, quotingType: "'", forceQuotes: false, sortKeys: false }));
}

// ─────────────────────────────────────────────────────────────────────
// js-yaml emits custom multi-tag types in verbatim form `!<!type:Foo>`
// (or sometimes `!<Foo>`) because the `:` character is not a legal
// shorthand-tag suffix in YAML 1.2. SS14 expects the canonical
// `!type:Foo` form, so this post-pass rewrites the verbatim tags back
// into the project's expected shorthand. Both observed verbatim forms
// are handled.
// ─────────────────────────────────────────────────────────────────────
function _fixupTypeTags(yamlText) {
    return yamlText
        .replace(/!<!type:([^>]+)>/g, '!type:$1')
        .replace(/!<([A-Za-z_][A-Za-z0-9_.]*)>/g, '!type:$1');
}
