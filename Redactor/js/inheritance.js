// ======================================================================
//  SS14 Prototype Redactor – Inheritance Resolution
// ======================================================================

'use strict';

function resolveInheritance(type, parents) {
    if (!parents || !state.protoIndex) return {};
    const ids = Array.isArray(parents) ? parents : [parents];
    let merged = {};
    // Process parents in order (first parent is lowest priority, last is highest)
    // matching the game's behavior: later parents override earlier ones
    for (const pid of ids) {
        const pd = resolveProto(type, pid);
        if (pd) merged = deepMerge(merged, pd);
    }
    return merged;
}

function resolveProto(type, id) {
    const key = `${type}:${id}`;
    if (state.resolvedCache.has(key)) return state.resolvedCache.get(key);

    // First check open files
    for (const [, fs] of state.openFiles) {
        if (!fs.yaml) continue;
        const p = fs.yaml.find(x => x.type === type && x.id === id);
        if (!p) continue;
        let data = { ...p };
        if (p.parent) data = deepMerge(resolveInheritance(type, p.parent), data);
        state.resolvedCache.set(key, data);
        return data;
    }

    // Check parent file cache (files loaded for inheritance resolution)
    for (const [, protos] of state.parentFileCache) {
        if (!protos) continue;
        const p = protos.find(x => x.type === type && x.id === id);
        if (!p) continue;
        let data = { ...p };
        if (p.parent) data = deepMerge(resolveInheritance(type, p.parent), data);
        state.resolvedCache.set(key, data);
        return data;
    }

    return null;
}

/**
 * Pre-load all parent files needed for inheritance resolution.
 * Walks all prototypes in the current file, finds parents,
 * and loads their YAML files into parentFileCache.
 */
async function preloadParents(protos) {
    if (!protos || !state.protoIndex) return;

    const filesToLoad = new Set();

    function collectNeeded(type, parentIds) {
        const ids = Array.isArray(parentIds) ? parentIds : [parentIds];
        for (const pid of ids) {
            // Already in open files?
            let found = false;
            for (const [, fs] of state.openFiles) {
                if (fs.yaml?.find(x => x.type === type && x.id === pid)) { found = true; break; }
            }
            if (found) continue;

            // Already in cache?
            for (const [, cached] of state.parentFileCache) {
                if (cached?.find(x => x.type === type && x.id === pid)) { found = true; break; }
            }
            if (found) continue;

            // Find file in proto index
            const entries = state.protoIndex[type];
            if (!entries) continue;
            const entry = entries.find(e => e.id === pid);
            if (entry?.file) filesToLoad.add(entry.file);
        }
    }

    // Collect direct parents
    for (const proto of protos) {
        if (proto.parent) collectNeeded(proto.type, proto.parent);
    }

    // Load files (may reveal more parents to load)
    let maxDepth = 10;
    while (filesToLoad.size > 0 && maxDepth-- > 0) {
        const batch = [...filesToLoad];
        filesToLoad.clear();

        await Promise.all(batch.map(async (relPath) => {
            if (state.parentFileCache.has(relPath)) return;
            try {
                const { content } = await api.loadFile(relPath);
                const parsed = parseYaml(content);
                state.parentFileCache.set(relPath, parsed);

                // Check if loaded protos have their own parents
                for (const p of parsed) {
                    if (p.parent) collectNeeded(p.type, p.parent);
                }
            } catch (e) {
                console.warn('[Inheritance] Could not load parent file:', relPath, e);
                state.parentFileCache.set(relPath, []);
            }
        }));
    }
}

function deepMerge(a, b) {
    const out = { ...a };
    for (const [k, v] of Object.entries(b)) {
        if (k.startsWith('__')) continue;
        // Special case: 'components' arrays merge by component 'type'
        if (k === 'components' && Array.isArray(v) && Array.isArray(a[k])) {
            out[k] = mergeComponentArrays(a[k], v);
        } else if (v && typeof v === 'object' && !Array.isArray(v) && a[k] && typeof a[k] === 'object' && !Array.isArray(a[k])) {
            out[k] = deepMerge(a[k], v);
        } else if (v !== undefined) {
            out[k] = v;
        }
    }
    return out;
}

/**
 * Merge two component arrays by component type.
 * Parent components are inherited; child components override fields of matching types.
 */
function mergeComponentArrays(parentComps, childComps) {
    // Build map from parent, keyed by 'type'
    const map = new Map();
    for (const c of parentComps) {
        if (c && c.type) map.set(c.type, { ...c });
    }
    // Merge child components
    for (const c of childComps) {
        if (!c || !c.type) continue;
        if (map.has(c.type)) {
            // Merge child fields into parent component
            const parent = map.get(c.type);
            const merged = { ...parent };
            for (const [fk, fv] of Object.entries(c)) {
                if (fk === 'type') continue;
                if (fv && typeof fv === 'object' && !Array.isArray(fv) && parent[fk] && typeof parent[fk] === 'object' && !Array.isArray(parent[fk])) {
                    merged[fk] = deepMerge(parent[fk], fv);
                } else if (fv !== undefined) {
                    merged[fk] = fv;
                }
            }
            map.set(c.type, merged);
        } else {
            map.set(c.type, { ...c });
        }
    }
    return [...map.values()];
}

/**
 * Check if a field value is locally defined in the prototype's YAML data.
 * Returns true if the key exists directly in the proto object.
 */
function isFieldLocal(proto, tag) {
    return Object.prototype.hasOwnProperty.call(proto, tag);
}

/**
 * Get the effective value for a field considering inheritance.
 * Returns { value, source: 'local' | 'inherited' | 'default' }
 */
function getFieldValue(proto, tag, inherited, defaultValue) {
    if (isFieldLocal(proto, tag)) {
        return { value: proto[tag], source: 'local' };
    }
    if (inherited && inherited[tag] !== undefined) {
        return { value: inherited[tag], source: 'inherited' };
    }
    return { value: defaultValue, source: 'default' };
}
