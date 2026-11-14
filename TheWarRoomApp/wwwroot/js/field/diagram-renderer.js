// Draws a diagram on a <canvas>. This is the only file that draws the field, the players and their routes,
// so the play designer, the read-only pages, the small previews in lists and the before/after comparison
// all look the same.
(function () {
    'use strict';

    var Model = window.WarRoom.DiagramModel;

    var COLOURS = {
        field: '#f3f8f3',
        yardLine: '#cfdccf',
        hashMark: '#b3c4b3',
        sideline: '#8fa58f',
        scrimmage: '#1e3a66',
        ball: '#7a4a21',
        ownFill: '#ffffff',
        ownStroke: '#13233f',
        oppFill: '#dee2e6',
        oppStroke: '#6c757d',
        path: '#13233f',
        text: '#13233f',
        selection: '#0d6efd',
        draft: '#0d6efd'
    };

    // In yards. Linemen stand two yards apart, so a marker has to be a little narrower than that.
    var PLAYER_RADIUS = 0.9;

    // Works out how big the canvas should be: it takes the width it is given on the page,
    // and its height follows from how much of the field the diagram shows.
    function layout(canvas, diagram) {
        var width = canvas.clientWidth || (canvas.parentElement && canvas.parentElement.clientWidth) || 600;
        var scale = width / Model.FIELD_WIDTH;   // pixels per yard
        return {
            width: width,
            height: (diagram.view.yMax - diagram.view.yMin) * scale,
            scale: scale,
            view: diagram.view
        };
    }

    // Yards -> pixels. The y axis is flipped because on screen "up the field" is towards the top.
    function toPixels(size, x, y) {
        return {
            x: (x + Model.HALF_WIDTH) * size.scale,
            y: (size.view.yMax - y) * size.scale
        };
    }

    // Pixels -> yards, for turning a click on the canvas into a place on the field
    function toYards(size, pixelX, pixelY) {
        return {
            x: pixelX / size.scale - Model.HALF_WIDTH,
            y: size.view.yMax - pixelY / size.scale
        };
    }

    // options:
    //   mode            'view' (default), 'edit' or 'thumb' (a small preview: no labels or text)
    //   highlightIds    players to ring, used by the before/after comparison
    //   highlightColour the colour of that ring
    //   selection       what is selected in the designer: { type: 'player'|'path'|'drawing', ... }
    //   draft           a route being drawn in the designer: { playerId, kind, pts }
    //   pointer         where the mouse is, in yards, so the route being drawn follows it
    function draw(canvas, diagram, options) {
        options = options || {};

        var size = layout(canvas, diagram);
        var ratio = window.devicePixelRatio || 1;

        // The drawing surface is made sharper on high-density screens
        canvas.style.height = size.height + 'px';
        canvas.width = Math.round(size.width * ratio);
        canvas.height = Math.round(size.height * ratio);

        var context = canvas.getContext('2d');
        context.setTransform(ratio, 0, 0, ratio, 0, 0);

        // A small preview shows where everybody is and where they go, without labels or text
        var small = options.mode === 'thumb';

        drawField(context, size, diagram);

        diagram.players.forEach(function (player) {
            player.paths.forEach(function (path, index) {
                var selected = isSelectedPath(options.selection, player.id, index);
                drawPath(context, size, Model.pathPoints(player, path), path.kind, selected ? COLOURS.selection : COLOURS.path, small);
                if (selected) {
                    drawHandles(context, size, player, path);
                }
            });
        });

        if (options.draft) {
            drawDraft(context, size, diagram, options.draft, options.pointer);
        }

        diagram.players.forEach(function (player) {
            var highlighted = options.highlightIds && options.highlightIds.indexOf(player.id) >= 0;
            var selected = options.selection && options.selection.type === 'player' && options.selection.id === player.id;
            drawPlayer(context, size, player, small, highlighted ? options.highlightColour : null, selected);
        });

        if (!small) {
            diagram.drawings.forEach(function (drawing) {
                var selected = options.selection && options.selection.type === 'drawing' && options.selection.id === drawing.id;
                drawText(context, size, drawing, selected);
            });
        }

        return size;
    }

    function isSelectedPath(selection, playerId, index) {
        return selection && selection.type === 'path' && selection.playerId === playerId && selection.index === index;
    }

    // ---------- The field ----------

    function drawField(context, size, diagram) {
        var view = diagram.view;

        context.fillStyle = COLOURS.field;
        context.fillRect(0, 0, size.width, size.height);

        // A line across the field every five yards
        context.strokeStyle = COLOURS.yardLine;
        context.lineWidth = 1;
        for (var y = Math.ceil(view.yMin / 5) * 5; y <= view.yMax; y += 5) {
            if (y !== 0) {
                line(context, toPixels(size, -Model.HALF_WIDTH, y), toPixels(size, Model.HALF_WIDTH, y));
            }
        }

        // Hash marks: short ticks every yard. High school hashes are 53 feet 4 inches in from each sideline.
        var hashX = 8.89;
        context.strokeStyle = COLOURS.hashMark;
        for (var yard = Math.ceil(view.yMin); yard <= view.yMax; yard++) {
            if (yard % 5 !== 0) {
                [-hashX, hashX].forEach(function (x) {
                    line(context, toPixels(size, x - 0.35, yard), toPixels(size, x + 0.35, yard));
                });
            }
        }

        // The line of scrimmage
        context.strokeStyle = COLOURS.scrimmage;
        context.lineWidth = 2;
        line(context, toPixels(size, -Model.HALF_WIDTH, 0), toPixels(size, Model.HALF_WIDTH, 0));

        // The ball
        var ball = toPixels(size, diagram.ballX, 0);
        context.fillStyle = COLOURS.ball;
        context.beginPath();
        context.ellipse(ball.x, ball.y, 0.35 * size.scale, 0.6 * size.scale, 0, 0, Math.PI * 2);
        context.fill();

        // The sidelines
        context.strokeStyle = COLOURS.sideline;
        context.lineWidth = 2;
        context.strokeRect(1, 1, size.width - 2, size.height - 2);
    }

    // ---------- Players ----------

    function drawPlayer(context, size, player, small, highlightColour, selected) {
        var centre = toPixels(size, player.x, player.y);
        var radius = PLAYER_RADIUS * size.scale;
        var own = player.team !== 'opp';

        // A ring behind the player marks it as changed (comparison) or selected (designer)
        if (highlightColour || selected) {
            context.strokeStyle = selected ? COLOURS.selection : highlightColour;
            context.lineWidth = Math.max(3, radius * 0.35);
            context.beginPath();
            context.arc(centre.x, centre.y, radius * 1.45, 0, Math.PI * 2);
            context.stroke();
        }

        context.fillStyle = own ? COLOURS.ownFill : COLOURS.oppFill;
        context.strokeStyle = own ? COLOURS.ownStroke : COLOURS.oppStroke;
        context.lineWidth = small ? 1 : 2;

        context.beginPath();
        if (player.shape === 'square') {
            context.rect(centre.x - radius, centre.y - radius, radius * 2, radius * 2);
        } else if (player.shape === 'triangle') {
            // Points down the field, towards the offence
            context.moveTo(centre.x - radius * 1.15, centre.y - radius * 0.9);
            context.lineTo(centre.x + radius * 1.15, centre.y - radius * 0.9);
            context.lineTo(centre.x, centre.y + radius * 1.1);
            context.closePath();
        } else {
            context.arc(centre.x, centre.y, radius, 0, Math.PI * 2);
        }
        context.fill();
        context.stroke();

        if (!small && player.label) {
            // Longer labels get a smaller font so they still fit inside the marker
            var fit = player.label.length <= 2 ? 1.05 : (player.label.length === 3 ? 0.82 : 0.66);
            var fontSize = radius * fit;
            context.fillStyle = own ? COLOURS.ownStroke : COLOURS.oppStroke;
            context.font = '600 ' + fontSize + 'px system-ui, -apple-system, "Segoe UI", sans-serif';
            context.textAlign = 'center';
            context.textBaseline = 'middle';
            var offset = player.shape === 'triangle' ? -radius * 0.2 : 0;
            context.fillText(player.label, centre.x, centre.y + offset);
        }
    }

    // ---------- Routes, blocks and motion ----------

    function drawPath(context, size, points, kind, colour, small) {
        if (points.length < 2) {
            return;
        }

        var pixels = points.map(function (point) { return toPixels(size, point.x, point.y); });

        context.strokeStyle = colour;
        context.fillStyle = colour;
        context.lineWidth = small ? 1 : 2;
        context.lineJoin = 'round';
        context.setLineDash(kind === 'motion' ? [6, 4] : []);

        context.beginPath();
        context.moveTo(pixels[0].x, pixels[0].y);
        for (var i = 1; i < pixels.length; i++) {
            context.lineTo(pixels[i].x, pixels[i].y);
        }
        context.stroke();
        context.setLineDash([]);

        var end = pixels[pixels.length - 1];
        var before = pixels[pixels.length - 2];
        var angle = Math.atan2(end.y - before.y, end.x - before.x);

        if (kind === 'block') {
            // A block ends in a short bar across the line
            var half = 0.8 * size.scale;
            context.beginPath();
            context.moveTo(end.x + Math.cos(angle + Math.PI / 2) * half, end.y + Math.sin(angle + Math.PI / 2) * half);
            context.lineTo(end.x + Math.cos(angle - Math.PI / 2) * half, end.y + Math.sin(angle - Math.PI / 2) * half);
            context.stroke();
        } else {
            // A route or motion ends in an arrowhead
            var length = (small ? 0.9 : 1.0) * size.scale;
            context.beginPath();
            context.moveTo(end.x, end.y);
            context.lineTo(end.x - Math.cos(angle - 0.45) * length, end.y - Math.sin(angle - 0.45) * length);
            context.lineTo(end.x - Math.cos(angle + 0.45) * length, end.y - Math.sin(angle + 0.45) * length);
            context.closePath();
            context.fill();
        }
    }

    // Small squares on the points of the selected path, which can be dragged in the designer
    function drawHandles(context, size, player, path) {
        context.fillStyle = '#ffffff';
        context.strokeStyle = COLOURS.selection;
        context.lineWidth = 2;

        path.pts.forEach(function (offset) {
            var point = toPixels(size, player.x + offset[0], player.y + offset[1]);
            context.beginPath();
            context.rect(point.x - 4, point.y - 4, 8, 8);
            context.fill();
            context.stroke();
        });
    }

    // The route that is being drawn right now, with a dotted line following the mouse
    function drawDraft(context, size, diagram, draft, pointer) {
        var player = Model.findPlayer(diagram, draft.playerId);
        if (!player) {
            return;
        }

        var points = Model.pathPoints(player, { kind: draft.kind, pts: draft.pts });
        if (points.length >= 2) {
            drawPath(context, size, points, draft.kind, COLOURS.draft, false);
        }

        if (pointer) {
            var from = toPixels(size, points[points.length - 1].x, points[points.length - 1].y);
            var to = toPixels(size, pointer.x, pointer.y);
            context.strokeStyle = COLOURS.draft;
            context.lineWidth = 1;
            context.setLineDash([3, 3]);
            line(context, from, to);
            context.setLineDash([]);
        }
    }

    // ---------- Text ----------

    function textFont(size) {
        return Math.max(11, 1.15 * size.scale) + 'px system-ui, -apple-system, "Segoe UI", sans-serif';
    }

    function drawText(context, size, drawing, selected) {
        var anchor = toPixels(size, drawing.x, drawing.y);

        context.font = textFont(size);
        context.textAlign = 'left';
        context.textBaseline = 'middle';
        context.fillStyle = selected ? COLOURS.selection : COLOURS.text;
        context.fillText(drawing.text, anchor.x, anchor.y);

        if (selected) {
            var width = context.measureText(drawing.text).width;
            context.strokeStyle = COLOURS.selection;
            context.lineWidth = 1;
            context.strokeRect(anchor.x - 3, anchor.y - size.scale * 0.9, width + 6, size.scale * 1.8);
        }
    }

    // How wide a text label is, in yards (the designer needs this to know when a click lands on it)
    function textWidth(canvas, size, text) {
        var context = canvas.getContext('2d');
        context.save();
        context.setTransform(1, 0, 0, 1, 0, 0);
        context.font = textFont(size);
        var width = context.measureText(text).width;
        context.restore();
        return width / size.scale;
    }

    // On a narrow screen a diagram is wider than the page and scrolls sideways inside its box.
    // It starts in the middle, where the ball is, instead of at the left sideline.
    function centre(canvas) {
        var box = canvas.parentElement;
        if (box && box.scrollWidth > box.clientWidth) {
            box.scrollLeft = (box.scrollWidth - box.clientWidth) / 2;
        }
    }

    function line(context, from, to) {
        context.beginPath();
        context.moveTo(from.x, from.y);
        context.lineTo(to.x, to.y);
        context.stroke();
    }

    window.WarRoom.DiagramRenderer = {
        PLAYER_RADIUS: PLAYER_RADIUS,
        draw: draw,
        centre: centre,
        layout: layout,
        toYards: toYards,
        toPixels: toPixels,
        textWidth: textWidth
    };
})();
