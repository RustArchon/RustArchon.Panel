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

    // The mouse and touch behaviour: wheel to zoom about the pointer, drag to pan, two fingers to pinch, double click to zoom in.
    function attach(entry) {
        var canvas = entry.canvas;
        var pointers = {};
        var lastPinch = 0;
        canvas.style.touchAction = 'none';
        canvas.style.cursor = 'grab';

        canvas.addEventListener('wheel', function (e) {
            e.preventDefault();
            var p = canvasPoint(entry, e);
            // Trackpads send many small deltas and mice a few large ones; an exponential scales both smoothly.
            zoomAbout(entry, Math.exp(-e.deltaY * 0.0015), p.x, p.y);
            schedule(entry);
        }, { passive: false });

        canvas.addEventListener('dblclick', function (e) {
            e.preventDefault();
            var p = canvasPoint(entry, e);
            zoomAbout(entry, 2, p.x, p.y);
            schedule(entry);
        });

        canvas.addEventListener('pointerdown', function (e) {
            canvas.setPointerCapture(e.pointerId);
            pointers[e.pointerId] = canvasPoint(entry, e);
            lastPinch = 0;
            canvas.style.cursor = 'grabbing';
        });

        canvas.addEventListener('pointermove', function (e) {
            if (!pointers[e.pointerId]) { return; }
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
            delete pointers[e.pointerId];
            lastPinch = 0;
            if (Object.keys(pointers).length === 0) { canvas.style.cursor = 'grab'; }
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

        // model: { size, showMonuments, showBases, showPlayers, monuments: [{name,x,z}], bases: [{name,x,z}], players: [{name,x,z,online}] }
        draw: function (canvasId, model) {
            var entry = images[canvasId];
            var canvas = canvasOf(canvasId);
            if (!canvas || !entry) { return false; }

            entry.canvas = canvas;
            entry.model = model;
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
            }
            delete images[canvasId];
        }
    };
})();
