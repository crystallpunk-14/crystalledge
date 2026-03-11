// ======================================================================
//  SS14 Prototype Redactor – Custom YAML Schema & Helpers
// ======================================================================

'use strict';

const _TagType = new jsyaml.Type('!type:', {
    kind: 'mapping', multi: true,
    construct(data, type) { data = data || {}; data.__yamlTag = type; return data; },
    predicate(obj) { return obj !== null && typeof obj === 'object' && !Array.isArray(obj) && '__yamlTag' in obj; },
    represent(obj) { const out = {}; for (const k of Object.keys(obj)) if (k !== '__yamlTag') out[k] = obj[k]; return out; },
    representName(obj) { return obj.__yamlTag; },
});
const SCHEMA = jsyaml.DEFAULT_SCHEMA.extend([_TagType]);

function parseYaml(text) {
    try { return jsyaml.load(text, { schema: SCHEMA }) || []; }
    catch (e) { console.error('YAML parse error', e); return []; }
}

function dumpYaml(data) {
    if (Array.isArray(data)) {
        return data.map(item =>
            jsyaml.dump([item], { schema: SCHEMA, indent: 2, lineWidth: -1, noRefs: true, quotingType: "'", forceQuotes: false, sortKeys: false }).trimEnd()
        ).join('\n\n') + '\n';
    }
    return jsyaml.dump(data, { schema: SCHEMA, indent: 2, lineWidth: -1, noRefs: true, quotingType: "'", forceQuotes: false, sortKeys: false });
}
