// The server map: the world's picture with players, bases and named places drawn over it on a canvas, which can be zoomed and
// panned. Blazor owns what to show and when to fetch; this owns the pixels and the view. World coordinates are metres from the
// world's centre (z runs north).
//
// The game's own picture is NOT just the world's square: it adds a margin of open ocean on every side (measured on a 4500 m
// world: a 5500 x 5500 picture, i.e. 500 m each side at one pixel per metre). So the picture spans size + 2 * MARGIN metres, and
// a point is (x / span + 0.5, 0.5 - z / span) of the way across and down: its "map position" (u, v), each 0 to 1.
//
// The view is a centre (cu, cv) in map positions and a zoom (1 = the whole picture fits). Markers and labels keep a constant size
// on screen; only their positions follow the view.
window.serverMap = (function () {
    var images = {};

    // Metres of ocean the game adds around the world in its picture, on each side.
    var MARGIN = 500;

    // The game's picture is one pixel per metre, so on a screen about a thousand pixels wide it is sharpest at about 6x and only
    // soft beyond that, however it is served. 8x leaves a little room without inviting people to zoom into blur.
    var MIN_ZOOM = 1;
    var MAX_ZOOM = 8;

    function canvasOf(id) {
        return document.getElementById(id);
    }

    function clamp(v, lo, hi) {
        return Math.max(lo, Math.min(hi, v));
    }

    function toMap(size, x, z) {
        var span = size + 2 * MARGIN;
        return { u: x / span + 0.5, v: 0.5 - z / span };
    }

    // The centre is kept so the view never shows beyond the picture; at zoom 1 that pins it to the middle.
    function constrain(view) {
        view.zoom = clamp(view.zoom, MIN_ZOOM, MAX_ZOOM);
        var half = 0.5 / view.zoom;
        view.cu = clamp(view.cu, half, 1 - half);
        view.cv = clamp(view.cv, half, 1 - half);
    }

    // Where a map position is on the canvas, in CSS pixels.
    function toScreen(entry, u, v) {
        var w = 1 / entry.view.zoom;
        return { x: ((u - (entry.view.cu - w / 2)) / w) * entry.side, y: ((v - (entry.view.cv - w / 2)) / w) * entry.side };
    }

    // Which map position is under a canvas point (CSS pixels).
    function fromScreen(entry, px, py) {
        var w = 1 / entry.view.zoom;
        return { u: entry.view.cu - w / 2 + (px / entry.side) * w, v: entry.view.cv - w / 2 + (py / entry.side) * w };
    }

    // Zooms by a factor about a canvas point, so the spot under it stays put (the point the person is looking at).
    function zoomAbout(entry, factor, px, py) {
        var before = fromScreen(entry, px, py);
        entry.view.zoom = clamp(entry.view.zoom * factor, MIN_ZOOM, MAX_ZOOM);
        var w = 1 / entry.view.zoom;
        entry.view.cu = before.u - (px / entry.side - 0.5) * w;
        entry.view.cv = before.v - (py / entry.side - 0.5) * w;
        constrain(entry.view);
    }

    function dot(ctx, p, radius, fill, stroke) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, radius, 0, Math.PI * 2);
        ctx.fillStyle = fill;
        ctx.fill();
        ctx.lineWidth = 1.5;
        ctx.strokeStyle = stroke;
        ctx.stroke();
    }

    function label(ctx, text, p, dy, color) {
        ctx.font = '12px sans-serif';
        ctx.textAlign = 'center';
        ctx.lineWidth = 3;
        ctx.strokeStyle = 'rgba(0,0,0,0.75)';
        ctx.strokeText(text, p.x, p.y + dy);
        ctx.fillStyle = color;
        ctx.fillText(text, p.x, p.y + dy);
    }

    function inView(entry, p, pad) {
        return p.x >= -pad && p.y >= -pad && p.x <= entry.side + pad && p.y <= entry.side + pad;
    }

    // One frame: the part of the picture in view, then the layers over it. Cheap however far in: only the visible part of the
    // picture is drawn and markers outside the view are skipped.
    function render(entry) {
        var canvas = entry.canvas;
        var wrap = canvas.parentElement;
        var side = Math.max(320, Math.min(wrap ? wrap.clientWidth : 900, 1100));
        var dpr = window.devicePixelRatio || 1;
        entry.side = side;
        if (canvas.width !== Math.round(side * dpr) || canvas.height !== Math.round(side * dpr)) {
            canvas.width = Math.round(side * dpr);
            canvas.height = Math.round(side * dpr);
        }

        // The size on the page follows the container (width 100%) and the shape stays square from the canvas's own proportions
        // (height auto), whatever the container does later - a fixed pixel size went out of square when a scrollbar appeared.
        canvas.style.width = '100%';
        canvas.style.height = 'auto';

        var ctx = canvas.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';

        var model = entry.model;
        var img = entry.image;
        var w = 1 / entry.view.zoom;
        var sx = (entry.view.cu - w / 2) * img.naturalWidth;
        var sy = (entry.view.cv - w / 2) * img.naturalHeight;
        ctx.clearRect(0, 0, side, side);
        ctx.drawImage(img, sx, sy, w * img.naturalWidth, w * img.naturalHeight, 0, 0, side, side);

        if (!model) { return; }

        if (model.showMonuments) {
            (model.monuments || []).forEach(function (m) {
                var q = toMap(model.size, m.x, m.z);
                var p = toScreen(entry, q.u, q.v);
                if (!inView(entry, p, 60)) { return; }
                dot(ctx, p, 3, 'rgba(255,255,255,0.9)', 'rgba(0,0,0,0.8)');
                label(ctx, m.name, p, -7, '#ffffff');
            });
        }

        if (model.showBases) {
            (model.bases || []).forEach(function (b) {
                var q = toMap(model.size, b.x, b.z);
                var p = toScreen(entry, q.u, q.v);
                if (!inView(entry, p, 60)) { return; }
                ctx.fillStyle = '#ffb000';
                ctx.strokeStyle = '#000';
                ctx.lineWidth = 1.5;
                ctx.fillRect(p.x - 4, p.y - 4, 8, 8);
                ctx.strokeRect(p.x - 4, p.y - 4, 8, 8);
                if (b.name) { label(ctx, b.name, p, 16, '#ffd45c'); }
            });
        }

        if (model.showPlayers) {
            (model.players || []).forEach(function (pl) {
                var q = toMap(model.size, pl.x, pl.z);
                var p = toScreen(entry, q.u, q.v);
                if (!inView(entry, p, 60)) { return; }
                dot(ctx, p, 5, pl.online ? '#2ecc71' : '#7f8c8d', '#000');
                label(ctx, pl.name, p, -9, pl.online ? '#b6f5cd' : '#c8cdd1');
            });
        }

        // A pinned tooltip follows its base as the view changes.
        positionPinned(entry);
    }

    // Many input events can arrive between frames; one frame is drawn per animation frame.
    function schedule(entry) {
        if (entry.frame) { return; }
        entry.frame = requestAnimationFrame(function () {
            entry.frame = 0;
            if (images[entry.id] === entry) { render(entry); }
        });
    }

    function canvasPoint(entry, event) {
        var rect = entry.canvas.getBoundingClientRect();
        var scale = entry.side / rect.width;
        return { x: (event.clientX - rect.left) * scale, y: (event.clientY - rect.top) * scale };
    }

    // ---- The base tooltip: who is authorized on a cupboard, shown over its marker. ----------------------------------------------
    //
    // Two modes share one element. Hovering a base (a mouse) shows a short, quick version that goes when the pointer moves on. Clicking
    // or tapping a base PINS it: the full list, with nothing cut, in a box that scrolls if it is long, stays open until it is closed
    // (its close button, Escape, or clicking the empty map), and stays attached to its base as the map is panned and zoomed.
    //
    // Everything in it is text from the game (a Steam name is whatever its player chose), so it is only ever put on the page with
    // textContent, never as markup. The element lives on the body with fixed positioning rather than inside the page's own markup:
    // Blazor owns that markup and re-renders it, and a tooltip must not be clipped by, or confuse, the container around the canvas.

    // How close, in CSS pixels, the pointer has to be to a base marker to count as pointing at it (the marker is 8 px square).
    var HOVER_RADIUS = 10;

    // The hover version stays short: bases stacked on one spot are common (several cupboards to one base), so it lists every base
    // under the pointer, up to this many, and a long authorized list is cut the same way. Pinning lifts both limits.
    var TOOLTIP_MAX_BASES = 4;
    var TOOLTIP_MAX_NAMES = 12;

    // A press and release closer together than this (CSS pixels) is a click or a tap; further apart it was a drag.
    var CLICK_SLOP = 6;

    // The bases under a canvas point, nearest first. Only when the bases layer is showing.
    function basesNear(entry, px, py) {
        var model = entry.model;
        if (!model || !model.showBases) { return []; }
        var found = [];
        (model.bases || []).forEach(function (b) {
            var q = toMap(model.size, b.x, b.z);
            var p = toScreen(entry, q.u, q.v);
            var distance = Math.hypot(p.x - px, p.y - py);
            if (distance <= HOVER_RADIUS) { found.push({ base: b, distance: distance }); }
        });
        found.sort(function (a, b) { return a.distance - b.distance; });
        return found.map(function (f) { return f.base; });
    }

    function line(text, color, weight) {
        var el = document.createElement('div');
        el.textContent = text;
        if (color) { el.style.color = color; }
        if (weight) { el.style.fontWeight = weight; }
        return el;
    }

    // A Steam id is all digits. Checked before one is put in an address, so nothing else can ever end up in it.
    function isSteamId(id) {
        return /^\d{1,20}$/.test(String(id == null ? '' : id));
    }

    // A player's name: a link to their page in a pinned tooltip (which can be clicked), plain text otherwise (a hover one cannot be,
    // and a name with no id to go with it has nowhere to link to). textContent, not markup, for the reason given above.
    function playerNode(entry, name, id, linkable, color) {
        var text = String(name == null ? '' : name);
        var base = entry.model && entry.model.playerUrl;
        if (!linkable || !base || !isSteamId(id)) { return document.createTextNode(text); }

        var a = document.createElement('a');
        a.href = base + encodeURIComponent(String(id));
        a.textContent = text;
        a.setAttribute('data-testid', 'map-tooltip-player');
        a.style.cssText = 'color:' + color + ';text-decoration:underline;';
        return a;
    }

    // A line made of a plain label and then a player's name.
    function playerLine(entry, label, name, id, linkable, color, weight) {
        var el = document.createElement('div');
        el.appendChild(document.createTextNode(label));
        el.appendChild(playerNode(entry, name, id, linkable, color));
        el.style.color = color;
        if (weight) { el.style.fontWeight = weight; }
        return el;
    }

    // "and {0} more" in the person's language, or English if the model did not carry it.
    function more(labels, count) {
        return ((labels && labels.more) || 'and {0} more').replace('{0}', String(count));
    }

    function tooltipOf(entry) {
        if (!entry.tooltip) {
            var el = document.createElement('div');
            el.setAttribute('role', 'tooltip');
            el.setAttribute('data-testid', 'map-tooltip');
            // A column so a pinned one can keep its close button in view while the list beneath it scrolls.
            el.style.cssText = 'position:fixed;display:none;flex-direction:column;pointer-events:none;z-index:2000;max-width:300px;' +
                'padding:6px 9px;border:1px solid #ffb000;border-radius:4px;background:rgba(18,18,18,0.96);color:#f2f2f2;' +
                'font:12px/1.4 sans-serif;box-shadow:0 2px 8px rgba(0,0,0,0.5);word-break:break-word;';
            document.body.appendChild(el);
            entry.tooltip = el;
        }
        return entry.tooltip;
    }

    // Hides the hover version. A pinned tooltip is not the pointer's to dismiss, so this leaves it alone.
    function hideHover(entry) {
        if (entry.tooltip && !entry.pinned) { entry.tooltip.style.display = 'none'; }
    }

    // Where a base is, as the game numbers it - "x 379, z -611 (height 12)", the same form as the Bases tab. Whole metres: the game
    // reports fractions, and nobody stands on a tenth of one. Math.round then String, so -0.4 reads 0 rather than "-0".
    function where(b, labels) {
        var metres = function (v) { return String(Math.round(Number(v) || 0)); };
        return 'x ' + metres(b.x) + ', z ' + metres(b.z) + ' (' + (labels.height || 'height') + ' ' + metres(b.y) + ')';
    }

    // One base's block: the owner, where it is, then the authorized players - all of them when `full`, otherwise cut short. Names
    // are links when `full` (the pinned tooltip), which is also the only one the mouse can reach.
    function baseBlock(entry, b, labels, full, first) {
        var block = document.createElement('div');
        if (!first) {
            block.style.marginTop = '6px';
            block.style.paddingTop = '6px';
            block.style.borderTop = '1px solid rgba(255,255,255,0.2)';
        }

        var names = b.authorized || [];
        block.appendChild(playerLine(entry, (labels.owner || 'Owner') + ': ', b.owner || '?', b.ownerId, full, '#ffd45c', '600'));
        block.appendChild(line((labels.location || 'Location') + ': ' + where(b, labels), '#b5b5b5'));
        block.appendChild(line((labels.authorized || 'Authorized') + (names.length ? ' (' + names.length + ')' : '') + ':', '#b5b5b5'));

        if (names.length === 0) {
            block.appendChild(line(labels.nobody || 'Nobody'));
            return block;
        }

        var shown = full ? names : names.slice(0, TOOLTIP_MAX_NAMES);
        shown.forEach(function (n) {
            // A name and the id to link it by (an older model sent bare names, which are simply not links).
            var isObject = n !== null && typeof n === 'object';
            var el = document.createElement('div');
            el.appendChild(playerNode(entry, isObject ? n.name : n, isObject ? n.id : null, full, '#8ec5ff'));
            block.appendChild(el);
        });
        if (shown.length < names.length) {
            block.appendChild(line(more(labels, names.length - shown.length), '#b5b5b5'));
        }

        return block;
    }

    function fillTooltip(entry, bases, full) {
        var el = tooltipOf(entry);
        var labels = (entry.model && entry.model.labels) || {};
        el.textContent = '';

        if (full) {
            // Kept apart from the list so it stays put while the list scrolls.
            var bar = document.createElement('div');
            bar.style.cssText = 'display:flex;justify-content:flex-end;flex:0 0 auto;';
            var close = document.createElement('button');
            close.type = 'button';
            close.textContent = '×';
            close.setAttribute('aria-label', labels.close || 'Close');
            close.setAttribute('data-testid', 'map-tooltip-close');
            close.style.cssText = 'border:0;background:transparent;color:#f2f2f2;font-size:16px;line-height:1;padding:0 2px;cursor:pointer;';
            close.addEventListener('click', function () { unpin(entry); });
            bar.appendChild(close);
            el.appendChild(bar);
        }

        var body = document.createElement('div');
        body.style.cssText = 'flex:1 1 auto;min-height:0;' + (full ? 'overflow-y:auto;max-height:min(60vh,420px);' : '');
        var shownBases = full ? bases : bases.slice(0, TOOLTIP_MAX_BASES);
        shownBases.forEach(function (b, i) { body.appendChild(baseBlock(entry, b, labels, full, i === 0)); });
        if (shownBases.length < bases.length) {
            var rest = line(more(labels, bases.length - shownBases.length), '#b5b5b5');
            rest.style.marginTop = '6px';
            body.appendChild(rest);
        }

        el.appendChild(body);
        el.style.pointerEvents = full ? 'auto' : 'none';
        el.setAttribute('role', full ? 'dialog' : 'tooltip');
        if (full) {
            el.setAttribute('aria-label', (labels.owner || 'Owner') + ': ' + (bases[0].owner || '?'));
        } else {
            el.removeAttribute('aria-label');
        }
    }

    // Beside the point, flipped to the other side of it when it would run off the window.
    function placeTooltip(el, clientX, clientY) {
        var gap = 14;
        el.style.display = 'flex';
        var w = el.offsetWidth, h = el.offsetHeight;
        var x = clientX + gap, y = clientY + gap;
        if (x + w > window.innerWidth - 4) { x = clientX - gap - w; }
        if (y + h > window.innerHeight - 4) { y = clientY - gap - h; }
        el.style.left = Math.max(4, x) + 'px';
        el.style.top = Math.max(4, y) + 'px';
    }

    // Shows the quick tooltip for whatever base is at the event's point, or hides it when there is none. Not while one is pinned:
    // the pinned one stays until it is closed.
    function showTooltipAt(entry, e) {
        if (entry.pinned) { return; }
        var p = canvasPoint(entry, e);
        var near = basesNear(entry, p.x, p.y);
        if (near.length === 0) { hideHover(entry); return; }
        fillTooltip(entry, near, false);
        placeTooltip(entry.tooltip, e.clientX, e.clientY);
    }

    // ---- Pinning. ----------------------------------------------------------------------------------------------------------------

    // Where the pinned tooltip's base is on the screen now. It follows the base as the view changes, and is out of sight (still
    // pinned) while the base is scrolled off the map.
    function positionPinned(entry) {
        if (!entry.pinned || !entry.tooltip) { return; }
        var q = toMap(entry.model.size, entry.pinned.x, entry.pinned.z);
        var p = toScreen(entry, q.u, q.v);
        if (p.x < 0 || p.y < 0 || p.x > entry.side || p.y > entry.side) {
            entry.tooltip.style.display = 'none';
            return;
        }

        var rect = entry.canvas.getBoundingClientRect();
        var scale = rect.width / entry.side;
        placeTooltip(entry.tooltip, rect.left + p.x * scale, rect.top + p.y * scale);
    }

    function onPinnedKey(entry, e) {
        if (e.key === 'Escape') { unpin(entry); }
    }

    function pin(entry, bases) {
        var first = !entry.pinned;
        entry.pinned = { x: bases[0].x, z: bases[0].z, keys: bases.map(function (b) { return { x: b.x, z: b.z }; }) };
        fillTooltip(entry, bases, true);
        positionPinned(entry);

        if (first) {
            entry.onKey = function (e) { onPinnedKey(entry, e); };
            entry.onMoved = function () { positionPinned(entry); };
            document.addEventListener('keydown', entry.onKey);
            window.addEventListener('resize', entry.onMoved);
            window.addEventListener('scroll', entry.onMoved, true); // capture: a scrolling container, not just the window
        }
    }

    function unpin(entry) {
        if (!entry.pinned) { return; }
        entry.pinned = null;
        document.removeEventListener('keydown', entry.onKey);
        window.removeEventListener('resize', entry.onMoved);
        window.removeEventListener('scroll', entry.onMoved, true);
        if (entry.tooltip) {
            entry.tooltip.style.display = 'none';
            entry.tooltip.style.pointerEvents = 'none';
            entry.tooltip.setAttribute('role', 'tooltip');
        }
    }

    // New data arrived (a refresh, a layer toggled): keep the pinned tooltip if its bases are still there, with what they say now.
    function refreshPinned(entry) {
        if (!entry.pinned) { return; }
        var model = entry.model;
        var still = model && model.showBases ? (model.bases || []).filter(function (b) {
            return entry.pinned.keys.some(function (k) { return Math.abs(b.x - k.x) < 0.5 && Math.abs(b.z - k.z) < 0.5; });
        }) : [];
        if (still.length === 0) { unpin(entry); return; }
        entry.pinned.x = still[0].x;
        entry.pinned.z = still[0].z;
        fillTooltip(entry, still, true);
    }

    // A click or tap on the map: a base under it is pinned (replacing any other pinned one), and empty map lets go.
    function clickAt(entry, e) {
        var p = canvasPoint(entry, e);
        var near = basesNear(entry, p.x, p.y);
        if (near.length > 0) { pin(entry, near); } else { unpin(entry); }
    }

    // The mouse and touch behaviour: wheel to zoom about the pointer, drag to pan, two fingers to pinch, double click to zoom in,
    // hover (a mouse) or tap (a finger) on a base to see who is authorized on it.
    function attach(entry) {
        var canvas = entry.canvas;
        var pointers = {};
        var starts = {};
        var lastPinch = 0;
        canvas.style.touchAction = 'none';
        canvas.style.cursor = 'grab';

        canvas.addEventListener('pointerleave', function () { hideHover(entry); });

        canvas.addEventListener('wheel', function (e) {
            e.preventDefault();
            hideHover(entry);
            var p = canvasPoint(entry, e);
            // Trackpads send many small deltas and mice a few large ones; an exponential scales both smoothly.
            zoomAbout(entry, Math.exp(-e.deltaY * 0.0015), p.x, p.y);
            schedule(entry);
        }, { passive: false });

        canvas.addEventListener('dblclick', function (e) {
            e.preventDefault();
            hideHover(entry);
            var p = canvasPoint(entry, e);
            zoomAbout(entry, 2, p.x, p.y);
            schedule(entry);
        });

        canvas.addEventListener('pointerdown', function (e) {
            canvas.setPointerCapture(e.pointerId);
            pointers[e.pointerId] = canvasPoint(entry, e);
            starts[e.pointerId] = pointers[e.pointerId];
            lastPinch = 0;
            canvas.style.cursor = 'grabbing';
            hideHover(entry);
        });

        canvas.addEventListener('pointermove', function (e) {
            if (!pointers[e.pointerId]) {
                // Nothing pressed: a mouse is just passing over. A finger has no such state - it taps instead (see release).
                if (e.pointerType === 'mouse') {
                    showTooltipAt(entry, e);

                    // A base is something to click, so it says so; anywhere else the map is something to drag.
                    var over = canvasPoint(entry, e);
                    canvas.style.cursor = basesNear(entry, over.x, over.y).length > 0 ? 'pointer' : 'grab';
                }
                return;
            }

            var now = canvasPoint(entry, e);
            var ids = Object.keys(pointers);

            if (ids.length === 1) {
                var before = pointers[e.pointerId];
                var w = 1 / entry.view.zoom;
                entry.view.cu -= ((now.x - before.x) / entry.side) * w;
                entry.view.cv -= ((now.y - before.y) / entry.side) * w;
                constrain(entry.view);
            } else if (ids.length === 2) {
                pointers[e.pointerId] = now;
                var a = pointers[ids[0]], b = pointers[ids[1]];
                var distance = Math.hypot(a.x - b.x, a.y - b.y);
                if (lastPinch > 0 && distance > 0) {
                    zoomAbout(entry, distance / lastPinch, (a.x + b.x) / 2, (a.y + b.y) / 2);
                }
                lastPinch = distance;
            }

            pointers[e.pointerId] = now;
            schedule(entry);
        });

        function release(e) {
            // A press and release in about the same place, alone, is a click (or a tap): it pins the base under it, or lets go of
            // the pinned one when there is none. Anything that moved further was a drag, which pans and pins nothing.
            var start = starts[e.pointerId];
            var wasAlone = Object.keys(pointers).length === 1;
            var end = canvasPoint(entry, e);
            var clicked = e.type === 'pointerup' && wasAlone && start && Math.hypot(end.x - start.x, end.y - start.y) < CLICK_SLOP;

            delete pointers[e.pointerId];
            delete starts[e.pointerId];
            lastPinch = 0;
            if (Object.keys(pointers).length === 0) { canvas.style.cursor = 'grab'; }
            if (clicked) { clickAt(entry, e); }
        }
        canvas.addEventListener('pointerup', release);
        canvas.addEventListener('pointercancel', release);

        // The page can resize (a window, a side panel); the canvas follows.
        if (window.ResizeObserver && canvas.parentElement) {
            entry.observer = new ResizeObserver(function () { schedule(entry); });
            entry.observer.observe(canvas.parentElement);
        }
    }

    return {
        // stream: the picture (a DotNetStreamReference arrives as an object with arrayBuffer()).
        load: async function (canvasId, stream) {
            var buffer = await stream.arrayBuffer();
            var url = URL.createObjectURL(new Blob([buffer]));
            var image = new Image();
            await new Promise(function (resolve, reject) {
                image.onload = resolve;
                image.onerror = function () { reject(new Error('The map picture could not be read.')); };
                image.src = url;
            });

            var old = images[canvasId];
            if (old && old.url) { URL.revokeObjectURL(old.url); }
            var canvas = canvasOf(canvasId);
            var entry = old && old.canvas === canvas ? old : { id: canvasId, canvas: canvas, view: { cu: 0.5, cv: 0.5, zoom: 1 }, side: 0, attached: false };
            entry.image = image;
            entry.url = url;
            images[canvasId] = entry;
            return { width: image.naturalWidth, height: image.naturalHeight };
        },

        // model: { size, showMonuments, showBases, showPlayers, monuments: [{name,x,z}], players: [{name,x,z,online}],
        //          bases: [{name,x,y,z,owner,ownerId,authorized:[{name,id}]}], playerUrl: "/servers/<id>/players/",
        //          labels: {owner,authorized,nobody,more,close,location,height} }
        draw: function (canvasId, model) {
            var entry = images[canvasId];
            var canvas = canvasOf(canvasId);
            if (!canvas || !entry) { return false; }

            entry.canvas = canvas;
            entry.model = model;
            hideHover(entry); // what it showed may be out of date now
            refreshPinned(entry); // a pinned one is kept, refilled, if its bases are still there
            if (!entry.attached) { attach(entry); entry.attached = true; }
            render(entry);
            return true;
        },

        // Buttons: zoom about the middle of the view.
        zoomBy: function (canvasId, factor) {
            var entry = images[canvasId];
            if (!entry) { return false; }
            zoomAbout(entry, factor, entry.side / 2, entry.side / 2);
            schedule(entry);
            return true;
        },

        resetView: function (canvasId) {
            var entry = images[canvasId];
            if (!entry) { return false; }
            entry.view = { cu: 0.5, cv: 0.5, zoom: 1 };
            schedule(entry);
            return true;
        },

        // For tests and diagnostics: the current view.
        view: function (canvasId) {
            var entry = images[canvasId];
            return entry ? { cu: entry.view.cu, cv: entry.view.cv, zoom: entry.view.zoom } : null;
        },

        dispose: function (canvasId) {
            var entry = images[canvasId];
            if (entry) {
                if (entry.url) { URL.revokeObjectURL(entry.url); }
                if (entry.observer) { entry.observer.disconnect(); }
                if (entry.frame) { cancelAnimationFrame(entry.frame); }
                unpin(entry);
                if (entry.tooltip && entry.tooltip.parentNode) { entry.tooltip.parentNode.removeChild(entry.tooltip); }
            }
            delete images[canvasId];
        }
    };
})();
