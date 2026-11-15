// Arrows and text over the video.
//
// A drawing belongs to one moment of a play. When the video reaches that moment while it plays, it stops there
// for a few seconds with the arrows and text over the picture, and then carries on by itself.
// Standing still on such a moment shows the drawing as well.
//
// The marks are kept as parts of the picture (0 to 1 across and down), so they sit in the same place
// whatever size the video is shown at. A canvas is laid exactly over the picture to draw them on.
//
// WarRoom.FilmOverlay is used by the clip editor, which also draws with it, and by the page that plays a cutup.
(function () {
    'use strict';

    window.WarRoom = window.WarRoom || {};

    var COLOURS = { yellow: '#ffd60a', red: '#ff453a', white: '#ffffff' };

    var NEAR_MS = 60;            // standing this close to a drawing's moment shows it
    var JUMP_MS = 1500;          // a bigger step than this between two looks is a jump, not playing
    var SHORTEST_ARROW = 0.03;   // of the picture: anything shorter was a click, not an arrow
    var MOST_MARKS = 12;         // on one drawing (the API has the same limit)

    function FilmOverlay(player) {
        var self = this;

        this.player = player;
        this.stops = [];           // { atMs, holdMs, marks }, with atMs on the player's own clock
        this.marks = [];           // what is on the canvas now
        this.lastMs = player.timeMs();
        this.heldStop = null;      // the stop the video last stood still on, until it has moved away from it
        this.holdTimer = null;

        // Set while the editor draws: the canvas then shows the editor's marks and takes the pointer
        this.editing = null;

        this.canvas = document.createElement('canvas');
        this.canvas.className = 'wr-film-overlay';
        this.canvas.setAttribute('aria-hidden', 'true');
        player.stage.appendChild(this.canvas);

        // What the texts of a drawing say, for people who do not see the picture
        this.caption = document.createElement('div');
        this.caption.className = 'visually-hidden';
        this.caption.setAttribute('role', 'status');
        player.stage.appendChild(this.caption);

        player.on('time', function () { self.look(); });
        player.on('ready', function () { self.fit(); });
        player.on('holdend', function () {
            window.clearTimeout(self.holdTimer);
            self.holdTimer = null;
            self.lastMs = player.timeMs();
        });

        window.addEventListener('resize', function () { self.fit(); });
        document.addEventListener('fullscreenchange', function () { self.fit(); });
        if (window.ResizeObserver) {
            new ResizeObserver(function () { self.fit(); }).observe(player.stage);
        }

        this.bindPointer();
        this.fit();
    }

    // ---------- Showing drawings while the video plays ----------

    FilmOverlay.prototype.setStops = function (stops) {
        this.stops = stops.slice().sort(function (a, b) { return a.atMs - b.atMs; });
        this.heldStop = null;
        this.look();
    };

    // Runs whenever the video's time changes
    FilmOverlay.prototype.look = function () {
        var player = this.player;
        var now = player.timeMs();

        if (this.editing || player.holding) {
            this.lastMs = now;
            return;
        }

        // Once the video has moved away from the drawing it stood still on, that drawing can stop it again
        if (this.heldStop && Math.abs(now - this.heldStop.atMs) > NEAR_MS) {
            this.heldStop = null;
        }

        if (player.isPlaying()) {
            var passed = now - this.lastMs < JUMP_MS ? this.firstStopBetween(this.lastMs, now) : null;
            if (passed) {
                this.hold(passed);
                return;
            }

            this.show([]);
            this.lastMs = now;
        } else {
            var here = this.stopNear(now);
            this.show(here ? here.marks : []);
            this.lastMs = now - 1;   // a drawing on the very moment the video starts from is still ahead of it
        }
    };

    FilmOverlay.prototype.firstStopBetween = function (afterMs, untilMs) {
        for (var i = 0; i < this.stops.length; i++) {
            var stop = this.stops[i];
            if (stop !== this.heldStop && stop.atMs > afterMs && stop.atMs <= untilMs) {
                return stop;
            }
        }
        return null;
    };

    FilmOverlay.prototype.stopNear = function (milliseconds) {
        for (var i = 0; i < this.stops.length; i++) {
            if (Math.abs(this.stops[i].atMs - milliseconds) <= NEAR_MS) {
                return this.stops[i];
            }
        }
        return null;
    };

    // The video stands still on the drawing's moment, and carries on by itself after the drawing's time
    FilmOverlay.prototype.hold = function (stop) {
        var self = this;

        this.heldStop = stop;
        this.lastMs = stop.atMs;
        this.player.hold(stop.atMs);
        this.show(stop.marks);

        this.holdTimer = window.setTimeout(function () {
            self.holdTimer = null;
            self.player.endHold(true);
        }, stop.holdMs);
    };

    // ---------- The canvas ----------

    // Lays the canvas exactly over the picture. The picture keeps its shape inside the player,
    // so it may have black bars beside it or above and below it, and those are not part of it.
    FilmOverlay.prototype.fit = function () {
        var stage = this.player.stage;
        var video = this.player.video;
        var boxWidth = stage.clientWidth;
        var boxHeight = stage.clientHeight;
        if (!boxWidth || !boxHeight) {
            return;
        }

        var pictureWidth = video.videoWidth || 16;
        var pictureHeight = video.videoHeight || 9;
        var scale = Math.min(boxWidth / pictureWidth, boxHeight / pictureHeight);
        var width = Math.round(pictureWidth * scale);
        var height = Math.round(pictureHeight * scale);

        this.canvas.style.left = Math.round((boxWidth - width) / 2) + 'px';
        this.canvas.style.top = Math.round((boxHeight - height) / 2) + 'px';
        this.canvas.style.width = width + 'px';
        this.canvas.style.height = height + 'px';

        // More dots than the size on the page where the screen has them, so the lines stay sharp
        var ratio = window.devicePixelRatio || 1;
        this.canvas.width = Math.round(width * ratio);
        this.canvas.height = Math.round(height * ratio);

        this.paint();
    };

    FilmOverlay.prototype.show = function (marks) {
        if (marks === this.marks || (marks.length === 0 && this.marks.length === 0)) {
            return;
        }

        this.marks = marks;
        this.paint();

        // The texts are also said, once, for people who use a screen reader
        var texts = marks.filter(function (mark) { return mark.type === 'text'; }).map(function (mark) { return mark.text; });
        this.caption.textContent = texts.length > 0 ? 'On the picture: ' + texts.join('. ') : '';
    };

    FilmOverlay.prototype.paint = function () {
        var context = this.canvas.getContext('2d');
        var width = this.canvas.width;
        var height = this.canvas.height;
        var marks = this.editing ? this.editing.marks.concat(this.editing.rubberBand ? [this.editing.rubberBand] : []) : this.marks;

        context.clearRect(0, 0, width, height);

        // Lines and letters grow with the picture
        var unit = Math.max(1.5, Math.min(width, height) / 220);

        marks.forEach(function (mark) {
            if (mark.type === 'arrow') {
                paintArrow(context, width, height, unit, mark);
            } else {
                paintText(context, width, height, unit, mark);
            }
        });
    };

    // A line with a head at its end. It is drawn twice: wide and black first, then in its colour,
    // so it can be seen on grass, on jerseys and on the lines of the field alike.
    function paintArrow(context, width, height, unit, mark) {
        var fromX = mark.x1 * width;
        var fromY = mark.y1 * height;
        var toX = mark.x2 * width;
        var toY = mark.y2 * height;
        var angle = Math.atan2(toY - fromY, toX - fromX);
        var head = unit * 7;

        // The line stops where the head begins, so the head keeps its point
        var lineEndX = toX - Math.cos(angle) * head * 0.8;
        var lineEndY = toY - Math.sin(angle) * head * 0.8;

        [{ colour: 'rgba(0, 0, 0, 0.85)', extra: unit * 1.6 }, { colour: COLOURS[mark.colour] || COLOURS.yellow, extra: 0 }].forEach(function (pass) {
            context.strokeStyle = pass.colour;
            context.fillStyle = pass.colour;
            context.lineWidth = unit * 2.2 + pass.extra;
            context.lineCap = 'round';
            context.lineJoin = 'round';

            context.beginPath();
            context.moveTo(fromX, fromY);
            context.lineTo(lineEndX, lineEndY);
            context.stroke();

            context.beginPath();
            context.moveTo(toX, toY);
            context.lineTo(toX - Math.cos(angle - 0.45) * head, toY - Math.sin(angle - 0.45) * head);
            context.lineTo(toX - Math.cos(angle + 0.45) * head, toY - Math.sin(angle + 0.45) * head);
            context.closePath();
            context.lineWidth = pass.extra;
            if (pass.extra > 0) {
                context.stroke();
            }
            context.fill();
        });
    }

    function paintText(context, width, height, unit, mark) {
        context.font = 'bold ' + Math.round(unit * 9) + 'px system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif';
        context.textAlign = 'center';
        context.textBaseline = 'middle';
        context.lineJoin = 'round';

        context.lineWidth = unit * 2.4;
        context.strokeStyle = 'rgba(0, 0, 0, 0.85)';
        context.strokeText(mark.text, mark.x1 * width, mark.y1 * height);

        context.fillStyle = COLOURS[mark.colour] || COLOURS.yellow;
        context.fillText(mark.text, mark.x1 * width, mark.y1 * height);
    }

    // ---------- Drawing (used by the clip editor) ----------

    // Starts drawing on the picture the video shows. marks is what is on it already;
    // changed(marks, problem) is called after every change.
    FilmOverlay.prototype.startDrawing = function (marks, changed) {
        this.editing = { marks: marks.slice(), tool: 'arrow', colour: 'yellow', text: '', rubberBand: null, changed: changed };
        this.canvas.classList.add('wr-film-overlay-drawing');
        this.paint();
    };

    FilmOverlay.prototype.stopDrawing = function () {
        this.editing = null;
        this.canvas.classList.remove('wr-film-overlay-drawing');
        this.marks = [];
        this.paint();
        this.look();
    };

    // tool is "arrow" or "text"; text is what the next text mark will say
    FilmOverlay.prototype.setPen = function (tool, colour, text) {
        if (this.editing) {
            this.editing.tool = tool;
            this.editing.colour = colour;
            this.editing.text = text;
        }
    };

    FilmOverlay.prototype.undo = function () {
        if (this.editing && this.editing.marks.length > 0) {
            this.editing.marks.pop();
            this.afterChange(null);
        }
    };

    FilmOverlay.prototype.clearMarks = function () {
        if (this.editing) {
            this.editing.marks = [];
            this.afterChange(null);
        }
    };

    FilmOverlay.prototype.afterChange = function (problem) {
        this.paint();
        this.editing.changed(this.editing.marks.slice(), problem);
    };

    FilmOverlay.prototype.bindPointer = function () {
        var self = this;
        var canvas = this.canvas;
        var from = null;   // where an arrow that is being dragged out started

        // Where the pointer is, as a part of the picture
        function place(event) {
            var box = canvas.getBoundingClientRect();
            return {
                x: clamp((event.clientX - box.left) / box.width),
                y: clamp((event.clientY - box.top) / box.height)
            };
        }

        function full() {
            if (self.editing.marks.length >= MOST_MARKS) {
                self.afterChange('A drawing can have at most ' + MOST_MARKS + ' arrows and texts.');
                return true;
            }
            return false;
        }

        canvas.addEventListener('pointerdown', function (event) {
            if (!self.editing || event.button !== 0) {
                return;
            }
            event.preventDefault();

            var at = place(event);

            if (self.editing.tool === 'text') {
                var text = self.editing.text.trim();
                if (!text) {
                    self.afterChange('Type the text first, then click where it goes.');
                } else if (!full()) {
                    self.editing.marks.push({ type: 'text', x1: at.x, y1: at.y, x2: 0, y2: 0, text: text, colour: self.editing.colour });
                    self.afterChange(null);
                }
                return;
            }

            if (!full()) {
                from = at;
                canvas.setPointerCapture(event.pointerId);
            }
        });

        canvas.addEventListener('pointermove', function (event) {
            if (!self.editing || !from) {
                return;
            }

            var to = place(event);
            self.editing.rubberBand = { type: 'arrow', x1: from.x, y1: from.y, x2: to.x, y2: to.y, colour: self.editing.colour };
            self.paint();
        });

        function finish(event) {
            if (!self.editing || !from) {
                return;
            }

            var to = place(event);
            var start = from;
            from = null;
            self.editing.rubberBand = null;

            if (Math.abs(to.x - start.x) >= SHORTEST_ARROW || Math.abs(to.y - start.y) >= SHORTEST_ARROW) {
                self.editing.marks.push({ type: 'arrow', x1: start.x, y1: start.y, x2: to.x, y2: to.y, text: null, colour: self.editing.colour });
                self.afterChange(null);
            } else {
                self.afterChange('Drag across the picture to draw an arrow: press where it starts and let go where it points.');
            }
        }

        canvas.addEventListener('pointerup', finish);
        canvas.addEventListener('pointercancel', function () {
            from = null;
            if (self.editing) {
                self.editing.rubberBand = null;
                self.paint();
            }
        });
    };

    function clamp(value) {
        return Math.round(Math.max(0, Math.min(1, value)) * 10000) / 10000;
    }

    // Reads the drawings of a play as they are stored: JSON text with a list of stops. Anything else gives no stops.
    FilmOverlay.read = function (json) {
        if (!json) {
            return [];
        }

        try {
            var drawings = JSON.parse(json);
            return drawings && Array.isArray(drawings.stops) ? drawings.stops : [];
        } catch (error) {
            return [];
        }
    };

    // "2 arrows, 1 text"
    FilmOverlay.describe = function (marks) {
        var arrows = marks.filter(function (mark) { return mark.type === 'arrow'; }).length;
        var texts = marks.length - arrows;
        var parts = [];

        if (arrows > 0) {
            parts.push(arrows + (arrows === 1 ? ' arrow' : ' arrows'));
        }
        if (texts > 0) {
            parts.push(texts + (texts === 1 ? ' text' : ' texts'));
        }
        return parts.length > 0 ? parts.join(', ') : 'nothing yet';
    };

    WarRoom.FilmOverlay = FilmOverlay;
})();
