// ======================================================================
//  SS14 Prototype Redactor – RSI Sprite Renderer (reusable)
// ======================================================================

'use strict';

/**
 * Reusable RSI sprite loader and animated renderer.
 *
 * Usage:
 *   const view = SpriteView.create(container, 'Objects/Tools/wrench.rsi', 'icon');
 *   view.destroy();  // cleanup when done
 *
 * The module caches RSI meta.json and PNG images globally.
 */
const SpriteView = (() => {

    // ─── Cache ────────────────────────────────────────────────────
    const _metaCache = new Map();   // rsiPath → Promise<meta>
    const _imgCache  = new Map();   // "rsiPath/state.png" → Promise<HTMLImageElement>

    /** Fetch and cache RSI meta.json. rsiPath is relative to Textures/, e.g. "Objects/Tools/wrench.rsi" */
    function loadMeta(rsiPath) {
        const key = rsiPath.replace(/\\/g, '/').replace(/\/$/, '');
        if (_metaCache.has(key)) return _metaCache.get(key);
        const p = fetch(`/api/texture?path=${encodeURIComponent(key + '/meta.json')}`)
            .then(r => { if (!r.ok) throw new Error(r.status); return r.json(); })
            .catch(e => { _metaCache.delete(key); throw e; });
        _metaCache.set(key, p);
        return p;
    }

    /** Load and cache a PNG image for a specific RSI state. */
    function loadImage(rsiPath, stateName) {
        const rsi = rsiPath.replace(/\\/g, '/').replace(/\/$/, '');
        const key = `${rsi}/${stateName}.png`;
        if (_imgCache.has(key)) return _imgCache.get(key);
        const p = new Promise((resolve, reject) => {
            const img = new Image();
            img.onload  = () => resolve(img);
            img.onerror = () => { _imgCache.delete(key); reject(new Error('Image load failed: ' + key)); };
            img.src = `/api/texture?path=${encodeURIComponent(key)}`;
        });
        _imgCache.set(key, p);
        return p;
    }

    /**
     * Find a state definition in meta.json by name.
     * Returns { name, directions, delays } or null.
     */
    function findState(meta, stateName) {
        if (!meta?.states) return null;
        return meta.states.find(s => s.name === stateName) || null;
    }

    /** Get the number of frames for a given direction from a state def. */
    function frameCount(stateDef, dir) {
        if (!stateDef?.delays?.length) return 1;
        const d = Math.min(dir ?? 0, stateDef.delays.length - 1);
        return stateDef.delays[d]?.length || 1;
    }

    /**
     * Get the global frame index offset for a given direction.
     * Frames are laid out sequentially: dir0 frames, then dir1 frames, etc.
     */
    function dirFrameOffset(stateDef, dir) {
        if (!stateDef?.delays) return 0;
        let offset = 0;
        for (let d = 0; d < Math.min(dir, stateDef.delays.length); d++) {
            offset += (stateDef.delays[d]?.length || 1);
        }
        return offset;
    }

    // ─── SpriteView instance ─────────────────────────────────────

    /**
     * Create an animated sprite view.
     * @param {HTMLElement} container – parent element to insert canvas into
     * @param {string} rsiPath – path relative to Textures/, e.g. "Objects/Tools/wrench.rsi"
     * @param {string} stateName – RSI state name, e.g. "icon"
     * @param {object} [opts] – { direction: 0, size: 64 }
     * @returns {{ el, setSprite, destroy }}
     */
    function create(container, rsiPath, stateName, opts = {}) {
        const dir   = opts.direction ?? 0;
        const size  = opts.size ?? 64;

        const canvas = _el('canvas');
        canvas.width = size;
        canvas.height = size;
        canvas.className = 'sprite-canvas';
        container.appendChild(canvas);

        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingEnabled = false;

        let _raf   = 0;
        let _alive = true;
        let _meta  = null;
        let _img   = null;
        let _state = null;
        let _frame = 0;
        let _elapsed = 0;
        let _lastTime = 0;

        function draw() {
            ctx.clearRect(0, 0, size, size);
            if (!_img || !_meta || !_state) return;

            const sw = _meta.size.x;
            const sh = _meta.size.y;
            const nDirs = _state.directions || 1;
            const d = Math.min(dir, nDirs - 1);

            // Sequential layout: compute global frame index
            const globalIdx = dirFrameOffset(_state, d) + _frame;
            const cols = Math.max(1, Math.floor(_img.width / sw));
            const sx = (globalIdx % cols) * sw;
            const sy = Math.floor(globalIdx / cols) * sh;

            // Scale to fill canvas, centered
            const scale = Math.min(size / sw, size / sh);
            const dw = sw * scale;
            const dh = sh * scale;
            const dx = (size - dw) / 2;
            const dy = (size - dh) / 2;
            ctx.drawImage(_img, sx, sy, sw, sh, dx, dy, dw, dh);
        }

        function tick(ts) {
            if (!_alive) return;
            if (_lastTime) {
                const dt = (ts - _lastTime) / 1000;
                _elapsed += dt;

                if (_state && _state.delays && _state.delays.length > 0) {
                    const nDirs = _state.directions || 1;
                    const d = Math.min(dir, nDirs - 1);
                    const dirDelays = _state.delays[Math.min(d, _state.delays.length - 1)];
                    if (dirDelays && dirDelays.length > 1) {
                        const frameDelay = dirDelays[_frame % dirDelays.length];
                        if (_elapsed >= frameDelay) {
                            _elapsed -= frameDelay;
                            _frame = (_frame + 1) % dirDelays.length;
                            draw();
                        }
                    }
                }
            }
            _lastTime = ts;
            _raf = requestAnimationFrame(tick);
        }

        async function load(rsi, st) {
            ctx.clearRect(0, 0, size, size);
            _frame = 0; _elapsed = 0; _lastTime = 0;
            _meta = null; _img = null; _state = null;
            if (!rsi || !st) return;
            try {
                _meta = await loadMeta(rsi);
                _state = findState(_meta, st);
                if (!_state) { drawPlaceholder(ctx, size, '?'); return; }
                _img = await loadImage(rsi, st);
                draw();
                // Start animation loop only if animated
                if (frameCount(_state, dir) > 1) {
                    cancelAnimationFrame(_raf);
                    _raf = requestAnimationFrame(tick);
                }
            } catch {
                drawPlaceholder(ctx, size, '!');
            }
        }

        load(rsiPath, stateName);

        return {
            el: canvas,
            setSprite(rsi, st) { load(rsi, st); },
            destroy() {
                _alive = false;
                cancelAnimationFrame(_raf);
                canvas.remove();
            },
        };
    }

    function drawPlaceholder(ctx, size, ch) {
        ctx.fillStyle = '#333';
        ctx.fillRect(0, 0, size, size);
        ctx.fillStyle = '#888';
        ctx.font = `${size * 0.4}px monospace`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(ch, size / 2, size / 2);
    }

    /** Invalidate cache entries for a specific RSI path. */
    function invalidate(rsiPath) {
        const key = rsiPath.replace(/\\/g, '/').replace(/\/$/, '');
        _metaCache.delete(key);
        for (const k of _imgCache.keys()) {
            if (k.startsWith(key + '/')) _imgCache.delete(k);
        }
    }

    return { create, loadMeta, loadImage, findState, frameCount, invalidate };
})();
