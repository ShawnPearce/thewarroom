// The play designer: the tools for drawing a formation or a play on the field.
//
// The drawing itself is done by diagram-renderer.js. This file handles the mouse, the keyboard,
// the toolbar and the box that shows what is selected, and it keeps the form's hidden DiagramJson
// field up to date, so that saving is an ordinary form post.
//
// The page marks the designer with data-designer and tells it:
//   data-form        the id of the form that holds the DiagramJson field
//   data-formation   "true" for a formation, which has players only (no routes or text)
//   data-draft-key   the name under which an unsaved copy is kept in this browser
//   data-dirty       "true" when the page is shown again after a failed save
(function () {
    'use strict';

    var Model = window.WarRoom.DiagramModel;
    var Renderer = window.WarRoom.DiagramRenderer;

    var DRAFT_PREFIX = 'warroom.draft.';
    var DRAFT_DAYS = 14;        // unsaved copies older than this are forgotten
    var HISTORY_LIMIT = 100;    // how many steps can be undone
    var MAX_TEXTS = 20;
    var MAX_LABEL_LENGTH = 4;
    var MAX_TEXT_LENGTH = 60;

    var PATH_TOOLS = ['route', 'block', 'motion'];

    var HINTS = {
        select: 'Drag a player to move it. Click a player, a route or a text to select it.',
        selectPlayer: 'Drag a player to move it. Click a player to select it.',
        own: 'Click on the field to place one of your players.',
        opp: 'Click on the field to place an opponent.',
        route: 'Click the player who runs the route.',
        block: 'Click the player who makes the block.',
        motion: 'Click the player who goes in motion.',
        text: 'Click where the text should go.',
        drawing: 'Click each point in turn. Click the last point again, or press Enter, to finish. Esc cancels.'
    };

    var SHORTCUTS = { v: 'select', p: 'own', o: 'opp', r: 'route', b: 'block', m: 'motion', t: 'text' };

    function Designer(root) {
        this.root = root;
        this.canvas = root.querySelector('canvas');
        this.form = document.getElementById(root.getAttribute('data-form'));
        this.field = this.form.querySelector('input[name="DiagramJson"]');
        this.isFormation = root.getAttribute('data-formation') === 'true';
        this.draftKey = root.getAttribute('data-draft-key');

        this.diagram = Model.parse(this.field.value);
        this.size = null;           // how the canvas was laid out the last time it was drawn

        this.tool = 'select';
        this.selection = null;      // { type: 'player', id } | { type: 'path', playerId, index } | { type: 'drawing', id }
        this.draft = null;          // the route being drawn: { playerId, kind, pts }
        this.pointer = null;        // where the mouse is while a route is being drawn
        this.drag = null;           // what is being dragged

        this.undoStack = [];
        this.redoStack = [];
        this.typing = false;        // true while a box in the properties panel is being typed in

        this.dirty = root.getAttribute('data-dirty') === 'true';
        this.submitting = false;
    }

    Designer.prototype.start = function () {
        var self = this;

        this.field.value = Model.serialize(this.diagram);

        this.bindToolbar();
        this.bindCanvas();
        this.bindKeyboard();
        this.bindPanel();
        this.bindForm();
        this.offerDraft();
        this.refresh();
        Renderer.centre(this.canvas);

        // The canvas takes its width from the page, so it is drawn again when the window changes size
        var timer = null;
        window.addEventListener('resize', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { self.render(); }, 150);
        });
    };

    // ---------- Drawing and keeping the page in step ----------

    Designer.prototype.render = function () {
        this.size = Renderer.draw(this.canvas, this.diagram, {
            mode: 'edit',
            selection: this.selection,
            draft: this.draft,
            pointer: this.draft ? this.pointer : null
        });
    };

    // Call before changing the diagram, so that the change can be undone
    Designer.prototype.remember = function () {
        this.undoStack.push(Model.serialize(this.diagram));
        if (this.undoStack.length > HISTORY_LIMIT) {
            this.undoStack.shift();
        }
        this.redoStack = [];
    };

    // Call after changing the diagram
    Designer.prototype.changed = function (keepPanel) {
        this.dirty = true;
        this.field.value = Model.serialize(this.diagram);
        this.saveDraft();
        this.refresh(keepPanel);
    };

    Designer.prototype.refresh = function (keepPanel) {
        this.render();
        this.updateToolbar();
        this.updateHint();
        if (!keepPanel) {
            this.updatePanel();
        }
    };

    Designer.prototype.undo = function () {
        if (this.undoStack.length === 0) {
            return;
        }
        this.redoStack.push(Model.serialize(this.diagram));
        this.restore(this.undoStack.pop());
    };

    Designer.prototype.redo = function () {
        if (this.redoStack.length === 0) {
            return;
        }
        this.undoStack.push(Model.serialize(this.diagram));
        this.restore(this.redoStack.pop());
    };

    Designer.prototype.restore = function (json) {
        this.diagram = Model.parse(json);
        this.selection = null;
        this.draft = null;
        this.changed();
    };

    Designer.prototype.message = function (text) {
        var element = this.root.querySelector('[data-role="message"]');
        if (element) {
            element.textContent = text || '';
        }
    };

    // ---------- Tools ----------

    Designer.prototype.setTool = function (tool) {
        // A formation has players only
        if (this.isFormation && (PATH_TOOLS.indexOf(tool) >= 0 || tool === 'text')) {
            return;
        }

        this.finishDraft();
        this.tool = tool;
        this.message('');

        // Choosing a drawing tool while a player is selected starts on that player straight away
        if (PATH_TOOLS.indexOf(tool) >= 0 && this.selection && this.selection.type === 'player') {
            this.startDraft(Model.findPlayer(this.diagram, this.selection.id));
        }

        this.canvas.style.cursor = tool === 'select' ? 'default' : 'crosshair';
        this.refresh();
    };

    Designer.prototype.defaultShape = function (team) {
        // Defenders are drawn as triangles, everyone else as circles
        var defence = this.diagram.unit === 'Defence';
        if (team === 'own') {
            return defence ? 'triangle' : 'circle';
        }
        return defence ? 'circle' : 'triangle';
    };

    Designer.prototype.addPlayer = function (point, team) {
        if (Model.countPlayers(this.diagram, team) >= Model.MAX_PLAYERS_PER_SIDE) {
            this.message('A side can have at most ' + Model.MAX_PLAYERS_PER_SIDE + ' players on the field.');
            return;
        }

        var spot = this.snapToField(point);
        this.remember();

        var player = {
            id: Model.newId(this.diagram, 'p'),
            team: team,
            label: '',
            shape: this.defaultShape(team),
            x: spot.x,
            y: spot.y,
            paths: []
        };
        this.diagram.players.push(player);

        // The new player is selected with its label ready to be typed
        this.selection = { type: 'player', id: player.id };
        this.tool = 'select';
        this.canvas.style.cursor = 'default';
        this.changed();
        this.focusProperty('label');
    };

    Designer.prototype.addText = function (point) {
        if (this.diagram.drawings.length >= MAX_TEXTS) {
            this.message('A diagram can have at most ' + MAX_TEXTS + ' text labels.');
            return;
        }

        var spot = this.snapToField(point);
        this.remember();

        var drawing = { id: Model.newId(this.diagram, 't'), type: 'text', x: spot.x, y: spot.y, text: 'Text' };
        this.diagram.drawings.push(drawing);

        this.selection = { type: 'drawing', id: drawing.id };
        this.tool = 'select';
        this.canvas.style.cursor = 'default';
        this.changed();
        this.focusProperty('text');
    };

    Designer.prototype.deleteSelection = function () {
        var selection = this.selection;
        if (!selection) {
            return;
        }

        this.remember();

        if (selection.type === 'player') {
            this.diagram.players = this.diagram.players.filter(function (player) { return player.id !== selection.id; });
        } else if (selection.type === 'path') {
            var owner = Model.findPlayer(this.diagram, selection.playerId);
            if (owner) {
                owner.paths.splice(selection.index, 1);
            }
        } else {
            this.diagram.drawings = this.diagram.drawings.filter(function (drawing) { return drawing.id !== selection.id; });
        }

        this.selection = null;
        this.changed();
    };

    // Removes every route, block and motion of the selected player
    Designer.prototype.clearPaths = function () {
        var selection = this.selection;
        var player = selection && selection.type === 'player' ? Model.findPlayer(this.diagram, selection.id) : null;
        if (!player || player.paths.length === 0) {
            return;
        }

        this.remember();
        player.paths = [];
        this.changed();
    };

    // ---------- Drawing a route, a block or a motion ----------

    Designer.prototype.startDraft = function (player) {
        if (!player) {
            return;
        }

        var kind = this.tool;

        // A player has one motion: drawing it again replaces the old one
        var replacesMotion = kind === 'motion' && player.paths.some(function (path) { return path.kind === 'motion'; });
        if (!replacesMotion && player.paths.length >= Model.MAX_PATHS_PER_PLAYER) {
            this.message('A player can have at most ' + Model.MAX_PATHS_PER_PLAYER + ' routes, blocks or motions. Delete one first.');
            return;
        }

        this.draft = { playerId: player.id, kind: kind, pts: [] };
        this.selection = { type: 'player', id: player.id };
    };

    Designer.prototype.addDraftPoint = function (point) {
        var draft = this.draft;
        var player = Model.findPlayer(this.diagram, draft.playerId);
        var spot = this.snapToField(point);

        // Where the path is right now: its last point, or where it starts
        var soFar = Model.pathPoints(player, draft);
        var last = soFar[soFar.length - 1];

        if (Math.abs(spot.x - last.x) < 0.01 && Math.abs(spot.y - last.y) < 0.01) {
            // Clicking the same place twice ends the path
            if (draft.pts.length > 0) {
                this.finishDraft();
            }
            return;
        }

        draft.pts.push([spot.x - player.x, spot.y - player.y]);

        if (draft.pts.length >= Model.MAX_POINTS_PER_PATH) {
            this.finishDraft();
        } else {
            this.refresh();
        }
    };

    Designer.prototype.finishDraft = function () {
        var draft = this.draft;
        if (!draft) {
            return;
        }

        this.draft = null;
        this.pointer = null;

        var player = Model.findPlayer(this.diagram, draft.playerId);
        if (!player || draft.pts.length === 0) {
            this.refresh();
            return;
        }

        this.remember();

        if (draft.kind === 'motion') {
            // The motion comes first, because routes and blocks start where it ends
            player.paths = player.paths.filter(function (path) { return path.kind !== 'motion'; });
            player.paths.unshift({ kind: 'motion', pts: draft.pts });
            this.selection = { type: 'path', playerId: player.id, index: 0 };
        } else {
            player.paths.push({ kind: draft.kind, pts: draft.pts });
            this.selection = { type: 'path', playerId: player.id, index: player.paths.length - 1 };
        }

        this.changed();
    };

    Designer.prototype.cancelDraft = function () {
        this.draft = null;
        this.pointer = null;
        this.refresh();
    };

    // ---------- Where things are ----------

    // A number of screen pixels, in yards
    Designer.prototype.reach = function (pixels) {
        return pixels / this.size.scale;
    };

    // The nearest half yard that is still between min and max
    function snapInside(value, min, max) {
        var snapped = Model.snap(Math.max(min, Math.min(max, value)));
        if (snapped > max) {
            snapped -= 0.5;
        }
        if (snapped < min) {
            snapped += 0.5;
        }
        return snapped;
    }

    Designer.prototype.snapToField = function (point) {
        var view = this.diagram.view;
        return {
            x: snapInside(point.x, -Model.HALF_WIDTH, Model.HALF_WIDTH),
            y: snapInside(point.y, view.yMin, view.yMax)
        };
    };

    // Where a player ends up when it is moved to (x, y): on the half-yard grid,
    // and never so close to an edge that one of its routes would leave the field.
    Designer.prototype.playerSpot = function (player, x, y) {
        var view = this.diagram.view;
        var left = 0, right = 0, down = 0, up = 0;

        player.paths.forEach(function (path) {
            path.pts.forEach(function (offset) {
                left = Math.min(left, offset[0]);
                right = Math.max(right, offset[0]);
                down = Math.min(down, offset[1]);
                up = Math.max(up, offset[1]);
            });
        });

        return {
            x: snapInside(x, -Model.HALF_WIDTH - left, Model.HALF_WIDTH - right),
            y: snapInside(y, view.yMin - down, view.yMax - up)
        };
    };

    function distance(a, b) {
        return Math.sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }

    // How far a point is from the line between two other points
    function distanceToSegment(point, from, to) {
        var dx = to.x - from.x;
        var dy = to.y - from.y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared === 0) {
            return distance(point, from);
        }

        var along = ((point.x - from.x) * dx + (point.y - from.y) * dy) / lengthSquared;
        along = Math.max(0, Math.min(1, along));
        return distance(point, { x: from.x + along * dx, y: from.y + along * dy });
    }

    // What is under a point of the field, or null
    Designer.prototype.hit = function (point) {
        var diagram = this.diagram;
        var i, j, k;

        // The points of the selected path come first, so they can be grabbed even when they sit on a player
        if (this.selection && this.selection.type === 'path') {
            var owner = Model.findPlayer(diagram, this.selection.playerId);
            var selected = owner ? owner.paths[this.selection.index] : null;
            if (selected) {
                for (i = 0; i < selected.pts.length; i++) {
                    var handle = { x: owner.x + selected.pts[i][0], y: owner.y + selected.pts[i][1] };
                    if (distance(point, handle) <= this.reach(9)) {
                        return { type: 'handle', playerId: owner.id, index: this.selection.index, point: i };
                    }
                }
            }
        }

        // Players: the one drawn last is on top. A small marker still gets a finger-sized target.
        var playerReach = Math.max(Renderer.PLAYER_RADIUS * 1.2, this.reach(12));
        for (i = diagram.players.length - 1; i >= 0; i--) {
            if (distance(point, diagram.players[i]) <= playerReach) {
                return { type: 'player', id: diagram.players[i].id };
            }
        }

        for (i = diagram.drawings.length - 1; i >= 0; i--) {
            var drawing = diagram.drawings[i];
            var width = Renderer.textWidth(this.canvas, this.size, drawing.text);
            if (point.x >= drawing.x - 0.5 && point.x <= drawing.x + width + 0.5 && Math.abs(point.y - drawing.y) <= 1) {
                return { type: 'drawing', id: drawing.id };
            }
        }

        for (i = diagram.players.length - 1; i >= 0; i--) {
            var player = diagram.players[i];
            for (j = 0; j < player.paths.length; j++) {
                var points = Model.pathPoints(player, player.paths[j]);
                for (k = 1; k < points.length; k++) {
                    if (distanceToSegment(point, points[k - 1], points[k]) <= this.reach(7)) {
                        return { type: 'path', playerId: player.id, index: j };
                    }
                }
            }
        }

        return null;
    };

    // The highest and lowest point of everything that is drawn, in yards from the line of scrimmage
    Designer.prototype.extent = function () {
        var lowest = 0, highest = 0;

        function include(y) {
            lowest = Math.min(lowest, y);
            highest = Math.max(highest, y);
        }

        this.diagram.players.forEach(function (player) {
            include(player.y);
            player.paths.forEach(function (path) {
                path.pts.forEach(function (offset) { include(player.y + offset[1]); });
            });
        });
        this.diagram.drawings.forEach(function (drawing) { include(drawing.y); });

        return { lowest: lowest, highest: highest };
    };

    // ---------- Mouse and touch ----------

    Designer.prototype.bindCanvas = function () {
        var self = this;
        var canvas = this.canvas;

        canvas.addEventListener('pointerdown', function (event) { self.onPointerDown(event); });
        canvas.addEventListener('pointermove', function (event) { self.onPointerMove(event); });
        canvas.addEventListener('pointerup', function () { self.onPointerUp(); });
        canvas.addEventListener('pointercancel', function () { self.onPointerUp(); });
    };

    Designer.prototype.pointFromEvent = function (event) {
        var box = this.canvas.getBoundingClientRect();
        return Renderer.toYards(this.size, event.clientX - box.left, event.clientY - box.top);
    };

    Designer.prototype.onPointerDown = function (event) {
        if (event.button !== 0) {
            return;   // only the main mouse button, a finger or a pen
        }

        event.preventDefault();
        this.canvas.focus({ preventScroll: true });   // takes the keyboard away from any box that was being typed in
        this.message('');

        var point = this.pointFromEvent(event);

        if (PATH_TOOLS.indexOf(this.tool) >= 0) {
            if (this.draft) {
                this.addDraftPoint(point);
                return;
            }

            var runner = this.hit(point);
            if (runner && runner.type === 'player') {
                this.startDraft(Model.findPlayer(this.diagram, runner.id));
                this.pointer = this.snapToField(point);
                this.refresh();
            } else {
                this.message('Click a player first.');
            }
            return;
        }

        if (this.tool === 'own' || this.tool === 'opp') {
            this.addPlayer(point, this.tool);
            return;
        }

        if (this.tool === 'text') {
            this.addText(point);
            return;
        }

        // The select tool: pick up what is under the pointer
        var target = this.hit(point);
        if (!target) {
            this.selection = null;
            this.refresh();
            return;
        }

        if (target.type === 'handle') {
            this.drag = { type: 'handle', playerId: target.playerId, index: target.index, point: target.point };
        } else if (target.type === 'player') {
            var player = Model.findPlayer(this.diagram, target.id);
            this.selection = { type: 'player', id: player.id };
            this.drag = { type: 'player', id: player.id, offsetX: point.x - player.x, offsetY: point.y - player.y };
        } else if (target.type === 'drawing') {
            var drawing = Model.findDrawing(this.diagram, target.id);
            this.selection = { type: 'drawing', id: drawing.id };
            this.drag = { type: 'drawing', id: drawing.id, offsetX: point.x - drawing.x, offsetY: point.y - drawing.y };
        } else {
            this.selection = { type: 'path', playerId: target.playerId, index: target.index };
        }

        if (this.drag) {
            this.drag.start = point;
            this.drag.moved = false;
            try {
                this.canvas.setPointerCapture(event.pointerId);   // keeps the drag going outside the canvas
            } catch (error) {
                // Not every browser allows this for every kind of pointer; dragging still works inside the canvas
            }
        }

        this.refresh();
    };

    Designer.prototype.onPointerMove = function (event) {
        if (!this.size) {
            return;
        }

        var point = this.pointFromEvent(event);

        // A route being drawn follows the pointer
        if (this.draft) {
            this.pointer = this.snapToField(point);
            this.render();
            return;
        }

        var drag = this.drag;
        if (!drag) {
            if (this.tool === 'select') {
                this.canvas.style.cursor = this.hit(point) ? 'move' : 'default';
            }
            return;
        }

        if (!drag.moved) {
            // A small movement while clicking is not a drag
            if (distance(point, drag.start) < this.reach(4)) {
                return;
            }
            drag.moved = true;
            this.remember();
        }

        if (drag.type === 'player') {
            var player = Model.findPlayer(this.diagram, drag.id);
            var spot = this.playerSpot(player, point.x - drag.offsetX, point.y - drag.offsetY);
            player.x = spot.x;
            player.y = spot.y;
        } else if (drag.type === 'drawing') {
            var drawing = Model.findDrawing(this.diagram, drag.id);
            var place = this.snapToField({ x: point.x - drag.offsetX, y: point.y - drag.offsetY });
            drawing.x = place.x;
            drawing.y = place.y;
        } else {
            var owner = Model.findPlayer(this.diagram, drag.playerId);
            var corner = this.snapToField(point);
            owner.paths[drag.index].pts[drag.point] = [corner.x - owner.x, corner.y - owner.y];
        }

        this.render();   // the form field is brought up to date when the drag ends
    };

    Designer.prototype.onPointerUp = function () {
        var drag = this.drag;
        this.drag = null;

        if (drag && drag.moved) {
            this.changed();
        }
    };

    // ---------- Keyboard ----------

    Designer.prototype.bindKeyboard = function () {
        var self = this;
        document.addEventListener('keydown', function (event) { self.onKeyDown(event); });
    };

    Designer.prototype.onKeyDown = function (event) {
        // Typing in a box of the page is left alone
        var target = event.target;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable) {
            return;
        }

        var key = event.key;
        var command = event.ctrlKey || event.metaKey;

        if (command && (key === 'z' || key === 'Z')) {
            event.preventDefault();
            if (event.shiftKey) {
                this.redo();
            } else {
                this.undo();
            }
            return;
        }
        if (command && (key === 'y' || key === 'Y')) {
            event.preventDefault();
            this.redo();
            return;
        }
        if (command || event.altKey) {
            return;
        }

        if (key === 'Escape') {
            if (this.draft) {
                this.cancelDraft();
            } else if (this.tool !== 'select') {
                this.setTool('select');
            } else {
                this.selection = null;
                this.refresh();
            }
            return;
        }

        if (key === 'Enter' && this.draft) {
            event.preventDefault();
            this.finishDraft();
            return;
        }

        if (key === 'Backspace' || key === 'Delete') {
            event.preventDefault();
            if (this.draft) {
                this.draft.pts.pop();   // takes back the last point of the route being drawn
                this.render();
            } else {
                this.deleteSelection();
            }
            return;
        }

        if (key.indexOf('Arrow') === 0) {
            if (this.nudge(key)) {
                event.preventDefault();
            }
            return;
        }

        var tool = SHORTCUTS[key.toLowerCase()];
        if (tool) {
            this.setTool(tool);
        }
    };

    // The arrow keys move the selected player or text half a yard
    Designer.prototype.nudge = function (key) {
        var steps = { ArrowLeft: [-0.5, 0], ArrowRight: [0.5, 0], ArrowUp: [0, 0.5], ArrowDown: [0, -0.5] };
        var step = steps[key];
        var selection = this.selection;
        if (!step || !selection || selection.type === 'path' || this.draft) {
            return false;
        }

        this.remember();

        if (selection.type === 'player') {
            var player = Model.findPlayer(this.diagram, selection.id);
            var spot = this.playerSpot(player, player.x + step[0], player.y + step[1]);
            player.x = spot.x;
            player.y = spot.y;
        } else {
            var drawing = Model.findDrawing(this.diagram, selection.id);
            var place = this.snapToField({ x: drawing.x + step[0], y: drawing.y + step[1] });
            drawing.x = place.x;
            drawing.y = place.y;
        }

        this.changed();
        return true;
    };

    // ---------- Toolbar ----------

    Designer.prototype.bindToolbar = function () {
        var self = this;

        this.root.addEventListener('click', function (event) {
            var button = event.target.closest('[data-tool], [data-command]');
            if (!button || button.disabled) {
                return;
            }

            if (button.hasAttribute('data-tool')) {
                self.setTool(button.getAttribute('data-tool'));
            } else {
                self.run(button.getAttribute('data-command'));
            }
        });
    };

    Designer.prototype.run = function (command) {
        this.message('');

        if (command === 'undo') {
            this.undo();
        } else if (command === 'redo') {
            this.redo();
        } else if (command === 'delete') {
            this.deleteSelection();
        } else if (command === 'clear-paths') {
            this.clearPaths();
        } else if (command.indexOf('view-') === 0) {
            this.changeView(command);
        }
    };

    Designer.prototype.updateToolbar = function () {
        var self = this;

        Array.prototype.forEach.call(this.root.querySelectorAll('[data-tool]'), function (button) {
            var active = button.getAttribute('data-tool') === self.tool;
            button.classList.toggle('active', active);
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
        });

        this.setDisabled('undo', this.undoStack.length === 0);
        this.setDisabled('redo', this.redoStack.length === 0);
        this.setDisabled('delete', !this.selection);
    };

    Designer.prototype.setDisabled = function (command, disabled) {
        Array.prototype.forEach.call(this.root.querySelectorAll('[data-command="' + command + '"]'), function (button) {
            button.disabled = disabled;
        });
    };

    Designer.prototype.updateHint = function () {
        var hint = this.root.querySelector('[data-role="hint"]');
        if (hint) {
            var text = this.draft ? HINTS.drawing : HINTS[this.tool];
            if (this.isFormation && this.tool === 'select') {
                text = HINTS.selectPlayer;   // a formation has no routes or text to select
            }
            hint.textContent = text;
        }

        var label = this.root.querySelector('[data-role="view-label"]');
        if (label) {
            var view = this.diagram.view;
            label.textContent = (-view.yMin) + ' yd behind the line, ' + view.yMax + ' yd past it';
        }
    };

    // Shows more or less of the field, five yards at a time
    Designer.prototype.changeView = function (command) {
        var view = this.diagram.view;
        var yMin = view.yMin;
        var yMax = view.yMax;

        if (command === 'view-downfield-more') {
            yMax += 5;
        } else if (command === 'view-downfield-less') {
            yMax -= 5;
        } else if (command === 'view-backfield-more') {
            yMin -= 5;
        } else if (command === 'view-backfield-less') {
            yMin += 5;
        }

        // The same limits the server checks, and the line of scrimmage always stays in view
        if (yMin < -40 || yMax > 70 || yMax - yMin > 90) {
            this.message('That is as much of the field as a diagram can show.');
            return;
        }
        if (yMin > -2 || yMax < 2 || yMax - yMin < 15) {
            this.message('That is as little of the field as a diagram can show.');
            return;
        }

        var extent = this.extent();
        if (extent.lowest < yMin || extent.highest > yMax) {
            this.message('Something is drawn on that part of the field. Move or delete it first.');
            return;
        }

        this.remember();
        view.yMin = yMin;
        view.yMax = yMax;
        this.changed();
    };

    // ---------- The box that shows what is selected ----------

    Designer.prototype.property = function (name) {
        return this.root.querySelector('[data-prop="' + name + '"]');
    };

    Designer.prototype.focusProperty = function (name) {
        var input = this.property(name);
        if (input) {
            input.focus({ preventScroll: true });
            input.select();
        }
    };

    Designer.prototype.updatePanel = function () {
        var selection = this.selection;
        var type = selection ? selection.type : 'none';

        Array.prototype.forEach.call(this.root.querySelectorAll('[data-panel]'), function (panel) {
            panel.classList.toggle('d-none', panel.getAttribute('data-panel') !== type);
        });

        if (type === 'player') {
            var player = Model.findPlayer(this.diagram, selection.id);
            this.property('label').value = player.label;
            this.property('shape').value = player.shape;

            var side = this.root.querySelector('[data-role="player-side"]');
            if (side) {
                side.textContent = player.team === 'opp' ? 'Opponent' : 'Your player';
            }

            var clear = this.root.querySelector('[data-command="clear-paths"]');
            if (clear) {
                clear.classList.toggle('d-none', player.paths.length === 0);
            }
        } else if (type === 'path') {
            var owner = Model.findPlayer(this.diagram, selection.playerId);
            this.property('kind').value = owner.paths[selection.index].kind;
        } else if (type === 'drawing') {
            this.property('text').value = Model.findDrawing(this.diagram, selection.id).text;
        }
    };

    Designer.prototype.bindPanel = function () {
        var self = this;

        this.root.addEventListener('input', function (event) {
            var name = event.target.getAttribute('data-prop');
            if (name) {
                self.applyProperty(name, event.target.value);
            }
        });

        // Everything typed into a box until it loses the keyboard is one step to undo
        this.root.addEventListener('focusout', function (event) {
            if (event.target.hasAttribute('data-prop')) {
                self.typing = false;
            }
        });

        // Enter in a box means "done": the keyboard goes back to the field
        this.root.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && event.target.hasAttribute('data-prop')) {
                event.preventDefault();
                self.canvas.focus({ preventScroll: true });
            }
        });
    };

    Designer.prototype.applyProperty = function (name, value) {
        var selection = this.selection;
        if (!selection) {
            return;
        }

        var before = Model.serialize(this.diagram);

        if (name === 'label' && selection.type === 'player') {
            Model.findPlayer(this.diagram, selection.id).label = value.trim().substring(0, MAX_LABEL_LENGTH);
        } else if (name === 'shape' && selection.type === 'player') {
            Model.findPlayer(this.diagram, selection.id).shape = value;
        } else if (name === 'text' && selection.type === 'drawing') {
            Model.findDrawing(this.diagram, selection.id).text = value.substring(0, MAX_TEXT_LENGTH);
        } else if (name === 'kind' && selection.type === 'path') {
            this.changeKind(selection, value);
        }

        var after = Model.serialize(this.diagram);
        if (after === before) {
            if (name === 'kind') {
                this.updatePanel();   // the change was refused, so the box goes back to what the path is
            }
            return;
        }

        // The state from before this box was typed in is what Undo goes back to
        if (!this.typing) {
            this.undoStack.push(before);
            if (this.undoStack.length > HISTORY_LIMIT) {
                this.undoStack.shift();
            }
            this.redoStack = [];
            this.typing = true;
        }

        this.changed(name !== 'kind');   // the box being typed in is left as it is
    };

    // Turns the selected path into a route, a block or a motion
    Designer.prototype.changeKind = function (selection, kind) {
        var player = Model.findPlayer(this.diagram, selection.playerId);
        var path = player.paths[selection.index];
        if (PATH_TOOLS.indexOf(kind) < 0 || path.kind === kind) {
            return;
        }

        if (kind === 'motion') {
            var hasMotion = player.paths.some(function (other) { return other.kind === 'motion'; });
            if (hasMotion) {
                this.message('This player already has a motion.');
                return;
            }

            // The motion comes first, because routes and blocks start where it ends
            player.paths.splice(selection.index, 1);
            player.paths.unshift(path);
            selection.index = 0;
        }

        path.kind = kind;
    };

    // ---------- Saving, and the unsaved copy kept in this browser ----------

    Designer.prototype.draftFields = function () {
        return this.form.querySelectorAll('[data-draft-field]');
    };

    Designer.prototype.saveDraft = function () {
        if (!this.draftKey) {
            return;
        }

        var fields = {};
        Array.prototype.forEach.call(this.draftFields(), function (input) {
            fields[input.name] = input.value;
        });

        try {
            window.localStorage.setItem(DRAFT_PREFIX + this.draftKey, JSON.stringify({
                savedAt: Date.now(),
                diagram: Model.serialize(this.diagram),
                fields: fields
            }));
        } catch (error) {
            // The browser's storage is full or switched off: the designer works the same, without the safety copy
        }
    };

    Designer.prototype.readDraft = function () {
        try {
            return JSON.parse(window.localStorage.getItem(DRAFT_PREFIX + this.draftKey));
        } catch (error) {
            return null;
        }
    };

    Designer.prototype.forgetDraft = function () {
        try {
            window.localStorage.removeItem(DRAFT_PREFIX + this.draftKey);
        } catch (error) {
            // Nothing to forget
        }
    };

    // Unsaved copies that nobody came back for are removed after two weeks
    function removeOldDrafts() {
        try {
            var storage = window.localStorage;
            var tooOld = Date.now() - DRAFT_DAYS * 24 * 60 * 60 * 1000;
            var expired = [];

            for (var i = 0; i < storage.length; i++) {
                var key = storage.key(i);
                if (key.indexOf(DRAFT_PREFIX) !== 0) {
                    continue;
                }

                var savedAt = 0;
                try {
                    savedAt = JSON.parse(storage.getItem(key)).savedAt || 0;
                } catch (error) {
                    savedAt = 0;
                }
                if (savedAt < tooOld) {
                    expired.push(key);
                }
            }

            expired.forEach(function (key) { storage.removeItem(key); });
        } catch (error) {
            // Storage is not available
        }
    }

    // When this browser still holds a drawing that was never saved (the sign-in ran out, the tab was closed),
    // the page offers to bring it back.
    Designer.prototype.offerDraft = function () {
        var self = this;
        var notice = this.root.querySelector('[data-role="draft-notice"]');

        removeOldDrafts();

        var draft = this.draftKey ? this.readDraft() : null;
        if (!notice || !draft || !draft.diagram) {
            return;
        }

        var fields = draft.fields || {};
        var same = draft.diagram === Model.serialize(this.diagram);
        Array.prototype.forEach.call(this.draftFields(), function (input) {
            if (fields[input.name] !== undefined && fields[input.name] !== input.value) {
                same = false;
            }
        });
        if (same) {
            return;   // the page already shows it
        }

        notice.querySelector('[data-role="draft-time"]').textContent =
            new Date(draft.savedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
        notice.classList.remove('d-none');

        notice.querySelector('[data-role="draft-restore"]').addEventListener('click', function () {
            self.remember();
            self.diagram = Model.parse(draft.diagram);
            Array.prototype.forEach.call(self.draftFields(), function (input) {
                if (fields[input.name] !== undefined) {
                    input.value = fields[input.name];
                }
            });
            self.selection = null;
            self.draft = null;
            self.changed();
            notice.classList.add('d-none');
        });

        notice.querySelector('[data-role="draft-discard"]').addEventListener('click', function () {
            self.forgetDraft();
            notice.classList.add('d-none');
        });
    };

    Designer.prototype.bindForm = function () {
        var self = this;

        this.form.addEventListener('submit', function () {
            // A route that is still being drawn is finished, and a text label left empty is dropped
            self.finishDraft();
            self.diagram.drawings = self.diagram.drawings.filter(function (drawing) { return drawing.text.trim() !== ''; });
            self.field.value = Model.serialize(self.diagram);

            // When the page's own checks stop the form (an empty name, for example) the browser stays here,
            // and the drawing is still unsaved.
            var valid = true;
            if (window.jQuery && window.jQuery.fn.valid) {
                valid = window.jQuery(self.form).valid();
            }
            self.submitting = valid;
        });

        Array.prototype.forEach.call(this.draftFields(), function (input) {
            input.addEventListener('input', function () {
                self.dirty = true;
                self.saveDraft();
            });
        });

        // Leaving the page with an unsaved drawing asks first
        window.addEventListener('beforeunload', function (event) {
            if (self.dirty && !self.submitting) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('[data-designer]');
        if (root) {
            new Designer(root).start();
        }
    });
})();
