// The clip editor on a video's page: plays are marked on the video itself.
//
// The start and end of a play are taken from the player (keys I and O), the play is tagged
// (R run, P pass, 1-4 the down, T the other team, D the yards to go) and saved with Enter.
// Saving sends the play to the site; the answer is the list of plays as the server draws it,
// which replaces the list on the page. The server stays the judge of what a valid play is.
//
// A play that has been saved can be drawn on: the video is stopped on a moment of the play,
// arrows and text are put on the picture, and the video will stand still there for a few seconds when the play is shown.
//
// A page gets an editor by containing an element with data-clip-editor (see Views/Film/Video.cshtml).
(function () {
    'use strict';

    var NUDGE_MS = 100;          // the - and + buttons move a mark by a tenth of a second
    var SHORTEST_MS = 200;       // anything shorter is a slip of the finger, not a play
    var MOST_STOPS = 5;          // drawings on one play (the API has the same limit)
    var SAME_MOMENT_MS = 100;    // drawing this close to an existing drawing changes that one

    function ClipEditor(root, player) {
        this.root = root;
        this.player = player;
        this.videoId = root.getAttribute('data-video-id');
        this.token = root.querySelector('input[name="__RequestVerificationToken"]').value;

        this.form = root.querySelector('[data-role="play-form"]');
        this.list = root.querySelector('[data-role="play-list"]');
        this.timeline = root.querySelector('[data-role="timeline"]');
        this.message = root.querySelector('[data-role="editor-message"]');

        this.fields = {
            type: this.form.querySelector('[name="PlayType"]'),
            team: this.form.querySelector('[name="OffenceTeamId"]'),
            down: this.form.querySelector('[name="Down"]'),
            distance: this.form.querySelector('[name="Distance"]'),
            note: this.form.querySelector('[name="Title"]')
        };

        // The play in the editor: a new one (id 0) or one that was opened from the list
        this.play = { id: 0, startMs: null, endMs: null };
        this.saving = false;

        // Drawings: the canvas over the picture, the drawings of the play in the editor,
        // and the one that is being drawn right now (null when none is)
        this.overlay = new window.WarRoom.FilmOverlay(player);
        this.drawingsCard = root.querySelector('[data-role="drawings-card"]');
        this.stops = [];
        this.drawing = null;

        this.bind();
        this.show();
        this.showAllDrawings();
    }

    // ---------- Wiring ----------

    ClipEditor.prototype.bind = function () {
        var self = this;

        Array.prototype.forEach.call(this.form.querySelectorAll('[data-command]'), function (button) {
            button.addEventListener('click', function () { self.run(button.getAttribute('data-command')); });
        });

        this.form.addEventListener('submit', function (event) {
            event.preventDefault();
            self.save();
        });

        // The buttons of the drawings card; the list of drawings in it is drawn again after every change
        this.drawingsCard.addEventListener('click', function (event) {
            var button = event.target.closest ? event.target.closest('[data-command]') : null;
            if (button) {
                self.run(button.getAttribute('data-command'), button);
            }
        });
        ['change', 'input'].forEach(function (name) {
            self.drawingsCard.addEventListener(name, function () { self.takePen(); });
        });

        // The list is replaced after every change, so its buttons are listened for on the box around it
        this.list.addEventListener('click', function (event) {
            var button = event.target.closest ? event.target.closest('[data-command]') : null;
            var row = button ? button.closest('[data-play-id]') : null;
            if (!row || self.drawing) {
                return;   // while a drawing is being made, the play it is on stays in the editor
            }

            if (button.getAttribute('data-command') === 'load-play') {
                self.open(row);
            } else if (button.getAttribute('data-command') === 'delete-play') {
                self.remove(row);
            }
        });

        this.timeline.addEventListener('click', function (event) {
            var box = self.timeline.getBoundingClientRect();
            var duration = self.player.durationMs();
            if (box.width > 0 && duration > 0) {
                self.player.seek(((event.clientX - box.left) / box.width) * duration);
            }
        });

        this.player.on('ready', function () { self.drawTimeline(); });
        this.player.on('time', function () { self.follow(); });

        document.addEventListener('keydown', function (event) { self.onKey(event); });
    };

    ClipEditor.prototype.run = function (command, button) {
        // While a drawing is being made, the play it is on stays as it is
        if (this.drawing && command.indexOf('draw-') !== 0) {
            return;
        }

        switch (command) {
            case 'draw-here': this.drawHere(); break;
            case 'draw-undo': this.overlay.undo(); break;
            case 'draw-clear': this.overlay.clearMarks(); break;
            case 'draw-save': this.saveDrawing(); break;
            case 'draw-cancel': this.stopDrawing(); break;
            case 'stop-show': this.showStop(Number(button.getAttribute('data-index'))); break;
            case 'stop-remove': this.removeStop(Number(button.getAttribute('data-index'))); break;
            case 'mark-start': this.markStart(); break;
            case 'mark-end': this.markEnd(); break;
            case 'start-earlier': this.nudge('startMs', -NUDGE_MS); break;
            case 'start-later': this.nudge('startMs', NUDGE_MS); break;
            case 'end-earlier': this.nudge('endMs', -NUDGE_MS); break;
            case 'end-later': this.nudge('endMs', NUDGE_MS); break;
            case 'play-selection': this.playSelection(); break;
            case 'clear': this.clear(); break;
        }
    };

    ClipEditor.prototype.onKey = function (event) {
        if (event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) {
            return;
        }

        var target = event.target;
        var tag = target && target.tagName;
        var typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';

        // While a drawing is being made the editor's keys rest. Esc leaves the drawing without saving it.
        if (this.drawing) {
            if (event.key === 'Escape') {
                this.stopDrawing();
                event.preventDefault();
            }
            return;
        }

        // Esc gives the keys back to the editor when a box of the form has them. Enter in a box saves, as in any form.
        if (typing) {
            if (event.key === 'Escape' && this.form.contains(target)) {
                target.blur();
                event.preventDefault();
            }
            return;
        }

        // Enter on a button presses that button
        if (tag === 'BUTTON' && (event.key === 'Enter' || event.key === ' ')) {
            return;
        }

        switch (event.key) {
            case 'i': case 'I': this.markStart(); break;
            case 'o': case 'O': this.markEnd(); break;
            case 'r': case 'R': this.fields.type.value = 'Run'; break;
            case 'p': case 'P': this.fields.type.value = 'Pass'; break;
            case 'u': case 'U':
                this.fields.type.value = '';
                this.fields.down.value = '';
                this.fields.distance.value = '';
                break;
            case '1': case '2': case '3': case '4': this.fields.down.value = event.key; break;
            case 't': case 'T': this.otherTeam(); break;
            case 'd': case 'D':
                this.fields.distance.focus();
                this.fields.distance.select();
                break;
            case 'Enter': this.save(); break;
            case 'Escape': this.clear(); break;
            default:
                return;   // not one of the editor's keys
        }

        event.preventDefault();
    };

    // ---------- Marking ----------

    ClipEditor.prototype.markStart = function () {
        this.play.startMs = this.player.timeMs();

        // An end that is now before the start no longer means anything
        if (this.play.endMs !== null && this.play.endMs <= this.play.startMs) {
            this.play.endMs = null;
        }

        this.say(null);
        this.show();
    };

    ClipEditor.prototype.markEnd = function () {
        var now = this.player.timeMs();

        if (this.play.startMs === null) {
            this.say('Mark where the play starts first (I).', true);
            return;
        }
        if (now <= this.play.startMs) {
            this.say('The end of a play comes after its start. Let the video run on, then mark the end.', true);
            return;
        }

        // The video stops at the end of the play, so the play can be tagged before the next one begins
        this.player.pause();
        this.play.endMs = now;
        this.say(null);
        this.show();
    };

    ClipEditor.prototype.nudge = function (which, milliseconds) {
        if (this.play[which] === null) {
            return;
        }

        var duration = this.player.durationMs();
        var moved = Math.max(0, this.play[which] + milliseconds);
        if (duration > 0) {
            moved = Math.min(moved, duration);
        }

        // The start stays before the end
        if (which === 'startMs' && this.play.endMs !== null && moved >= this.play.endMs) {
            return;
        }
        if (which === 'endMs' && moved <= this.play.startMs) {
            return;
        }

        this.play[which] = moved;
        this.player.pause();
        this.player.seek(moved);   // the picture shows where the mark is now
        this.show();
    };

    ClipEditor.prototype.playSelection = function () {
        if (this.play.startMs === null) {
            this.say('Mark where the play starts first (I).', true);
        } else if (this.play.endMs === null) {
            this.player.seek(this.play.startMs);
            this.player.play();
        } else {
            this.player.playRange(this.play.startMs, this.play.endMs);
        }
    };

    ClipEditor.prototype.otherTeam = function () {
        var team = this.fields.team;
        team.selectedIndex = (team.selectedIndex + 1) % team.options.length;
    };

    // ---------- Opening, saving and deleting ----------

    // Opens a play from the list in the editor. With stayPut the video is left where it is.
    ClipEditor.prototype.open = function (row, stayPut) {
        this.play = {
            id: Number(row.getAttribute('data-play-id')),
            startMs: Number(row.getAttribute('data-start')),
            endMs: Number(row.getAttribute('data-end'))
        };
        this.stops = window.WarRoom.FilmOverlay.read(row.getAttribute('data-annotations'));

        this.fields.type.value = row.getAttribute('data-type') || '';
        this.fields.down.value = row.getAttribute('data-down') || '';
        this.fields.distance.value = row.getAttribute('data-distance') || '';
        this.fields.note.value = row.getAttribute('data-title') || '';
        if (row.getAttribute('data-team')) {
            this.fields.team.value = row.getAttribute('data-team');
        }

        this.player.pause();
        if (!stayPut) {
            this.player.seek(this.play.startMs);
        }
        this.say(null);
        this.show();
    };

    // Empties the editor for the next play. The team on offence stays: most plays follow one by the same team.
    ClipEditor.prototype.clear = function () {
        this.play = { id: 0, startMs: null, endMs: null };
        this.stops = [];
        this.fields.type.value = '';
        this.fields.down.value = '';
        this.fields.distance.value = '';
        this.fields.note.value = '';
        this.say(null);
        this.show();
    };

    ClipEditor.prototype.save = function () {
        var self = this;
        var play = this.play;

        if (this.saving) {
            return;
        }
        if (play.startMs === null || play.endMs === null) {
            this.say('Mark where the play starts and where it ends first (I and O).', true);
            return;
        }
        if (play.endMs - play.startMs < SHORTEST_MS) {
            this.say('The play is too short. Mark its end later.', true);
            return;
        }

        var wasNew = play.id === 0;

        this.send(this.root.getAttribute('data-save-url'), {
            Id: play.id,
            VideoId: this.videoId,
            StartMs: play.startMs,
            EndMs: play.endMs,
            Title: this.fields.note.value.trim(),
            PlayType: this.fields.type.value,
            OffenceTeamId: this.fields.team.value,
            Down: this.fields.down.value,
            Distance: this.fields.distance.value
        }).then(function (listHtml) {
            self.list.innerHTML = listHtml;
            self.showAllDrawings();
            self.clear();
            self.say(wasNew ? 'The play was added.' : 'The play was saved.', false);

            // The keys go back to the editor, ready for the next play
            if (document.activeElement && self.form.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        }).catch(function (problem) {
            self.say(problem.message, true);
        });
    };

    ClipEditor.prototype.remove = function (row) {
        var self = this;
        var id = Number(row.getAttribute('data-play-id'));

        if (this.saving || !window.confirm('Delete this play? This cannot be undone.')) {
            return;
        }

        this.send(this.root.getAttribute('data-delete-url'), { id: id, videoId: this.videoId }).then(function (listHtml) {
            self.list.innerHTML = listHtml;
            self.showAllDrawings();

            if (self.play.id === id) {
                self.clear();   // the play that was open in the editor is gone
            }
            self.say('The play was deleted.', false);
            self.show();
        }).catch(function (problem) {
            self.say(problem.message, true);
        });
    };

    // ---------- Drawing on a play ----------

    // Stops the video where it is and starts a drawing on that picture.
    // On a moment that already has a drawing, that drawing is changed.
    ClipEditor.prototype.drawHere = function () {
        var self = this;
        var play = this.play;
        var clock = window.WarRoom.filmClock;
        var now = this.player.timeMs();

        if (play.id === 0) {
            return;
        }
        if (now < play.startMs || now > play.endMs) {
            this.sayDrawing('Move the video to a moment inside the play first: between ' +
                clock(play.startMs, true) + ' and ' + clock(play.endMs, true) + '.', true);
            return;
        }

        var index = -1;
        this.stops.forEach(function (stop, i) {
            if (Math.abs(stop.atMs - now) <= SAME_MOMENT_MS) {
                index = i;
            }
        });

        if (index < 0 && this.stops.length >= MOST_STOPS) {
            this.sayDrawing('A play can have at most ' + MOST_STOPS + ' drawings. Remove one first.', true);
            return;
        }

        var existing = index >= 0 ? this.stops[index] : null;
        this.drawing = { index: index, atMs: existing ? existing.atMs : now, marks: existing ? existing.marks.slice() : [] };

        // The video stays on this picture until the drawing is saved or left
        this.player.seek(this.drawing.atMs);
        this.player.setLocked(true);

        this.drawingsCard.querySelector('[name="HoldSeconds"]').value = existing ? Math.round(existing.holdMs / 1000) : 3;
        this.overlay.startDrawing(this.drawing.marks, function (marks, problem) {
            self.drawing.marks = marks;
            self.sayDrawing(problem, true);
            self.showDrawings();
        });
        this.takePen();
        this.sayDrawing(null);
        this.showDrawings();
    };

    // Passes what the tool boxes say on to the canvas
    ClipEditor.prototype.takePen = function () {
        if (!this.drawing) {
            return;
        }

        var card = this.drawingsCard;
        var tool = card.querySelector('[name="DrawTool"]:checked').value;

        this.overlay.setPen(tool, card.querySelector('[name="DrawColour"]').value, card.querySelector('[name="DrawText"]').value);
        card.querySelector('[data-role="draw-text-box"]').classList.toggle('d-none', tool !== 'text');
    };

    ClipEditor.prototype.stopDrawing = function () {
        this.drawing = null;
        this.overlay.stopDrawing();
        this.player.setLocked(false);
        this.sayDrawing(null);
        this.showDrawings();
    };

    ClipEditor.prototype.saveDrawing = function () {
        var drawing = this.drawing;
        var seconds = Number(this.drawingsCard.querySelector('[name="HoldSeconds"]').value);

        if (!(seconds >= 1 && seconds <= 10)) {
            this.sayDrawing('The video can stop for 1 to 10 seconds.', true);
            return;
        }
        if (drawing.marks.length === 0 && drawing.index < 0) {
            this.sayDrawing('Draw an arrow or put a text on the picture first.', true);
            return;
        }

        // The play's drawings with this one added, changed, or (when everything on it was cleared) taken out
        var stops = this.stops.slice();
        var stop = { atMs: drawing.atMs, holdMs: Math.round(seconds * 1000), marks: drawing.marks };

        if (drawing.index >= 0 && drawing.marks.length === 0) {
            stops.splice(drawing.index, 1);
        } else if (drawing.index >= 0) {
            stops[drawing.index] = stop;
        } else {
            stops.push(stop);
        }

        this.sendDrawings(stops, 'The drawing was saved. The video will stop here when the play is shown.');
    };

    ClipEditor.prototype.removeStop = function (index) {
        if (this.drawing || !window.confirm('Remove this drawing from the play?')) {
            return;
        }

        var stops = this.stops.slice();
        stops.splice(index, 1);
        this.sendDrawings(stops, 'The drawing was removed.');
    };

    // Goes to a drawing's moment. Standing still there shows the drawing.
    ClipEditor.prototype.showStop = function (index) {
        if (!this.drawing && this.stops[index]) {
            this.player.pause();
            this.player.seek(this.stops[index].atMs);
        }
    };

    // Sends all the drawings of the play in the editor. The answer is the list of plays, as after any change.
    ClipEditor.prototype.sendDrawings = function (stops, done) {
        var self = this;
        var playId = this.play.id;

        if (this.saving) {
            return;
        }

        this.send(this.root.getAttribute('data-drawings-url'), {
            id: playId,
            videoId: this.videoId,
            annotationsJson: stops.length > 0 ? JSON.stringify({ schemaVersion: 1, stops: stops }) : ''
        }).then(function (listHtml) {
            self.list.innerHTML = listHtml;
            self.showAllDrawings();

            if (self.drawing) {
                self.drawing = null;
                self.overlay.stopDrawing();
                self.player.setLocked(false);
            }

            // The play stays open in the editor, with its drawings as the server kept them
            var row = self.list.querySelector('[data-play-id="' + playId + '"]');
            if (row) {
                self.open(row, true);
            } else {
                self.clear();
            }
            self.say(null);
            self.sayDrawing(done, false);
        }).catch(function (problem) {
            self.say(null);
            self.sayDrawing(problem.message, true);
        });
    };

    // While the video plays in the editor, the drawings of every play are shown the way a cutup will show them
    ClipEditor.prototype.showAllDrawings = function () {
        var stops = [];

        Array.prototype.forEach.call(this.rows(), function (row) {
            stops = stops.concat(window.WarRoom.FilmOverlay.read(row.getAttribute('data-annotations')));
        });

        this.overlay.setStops(stops);
    };

    // The drawings card: shown for a play that has been saved
    ClipEditor.prototype.showDrawings = function () {
        var self = this;
        var card = this.drawingsCard;
        var clock = window.WarRoom.filmClock;
        var list = card.querySelector('[data-role="stop-list"]');

        card.classList.toggle('d-none', this.play.id === 0);
        card.querySelector('[data-role="draw-tools"]').classList.toggle('d-none', !this.drawing);
        card.querySelector('[data-role="draw-start"]').classList.toggle('d-none', !!this.drawing);

        if (this.drawing) {
            card.querySelector('[data-role="draw-summary"]').textContent =
                'On the picture at ' + clock(this.drawing.atMs, true) + ': ' + window.WarRoom.FilmOverlay.describe(this.drawing.marks) + '.';
        }

        // The list is built with elements, never with HTML text, because the texts on a drawing are typed by people
        list.innerHTML = '';
        this.stops.forEach(function (stop, index) {
            var item = document.createElement('li');
            item.className = 'd-flex flex-wrap align-items-center gap-2 mb-1';

            var text = document.createElement('span');
            text.textContent = 'At ' + clock(stop.atMs, true) + ', for ' + Math.round(stop.holdMs / 1000) + ' s: ' +
                window.WarRoom.FilmOverlay.describe(stop.marks);
            item.appendChild(text);

            [['stop-show', 'Show'], ['stop-remove', 'Remove']].forEach(function (pair) {
                var button = document.createElement('button');
                button.type = 'button';
                button.className = 'btn btn-link btn-sm p-0';
                button.setAttribute('data-command', pair[0]);
                button.setAttribute('data-index', index);
                button.textContent = pair[1];
                button.disabled = !!self.drawing;
                item.appendChild(button);
            });

            list.appendChild(item);
        });

        card.querySelector('[data-role="no-stops"]').classList.toggle('d-none', this.stops.length > 0);
    };

    ClipEditor.prototype.sayDrawing = function (text, isProblem) {
        var message = this.drawingsCard.querySelector('[data-role="drawings-message"]');
        message.textContent = text || '';
        message.className = 'small ' + (isProblem ? 'text-danger' : 'text-success');
    };

    // Posts a small form to the site. The answer to a change is the list of plays, as HTML.
    ClipEditor.prototype.send = function (url, fields) {
        var self = this;
        var body = new FormData();

        Object.keys(fields).forEach(function (name) {
            if (fields[name] !== '' && fields[name] !== null) {
                body.append(name, fields[name]);
            }
        });
        body.append('__RequestVerificationToken', this.token);

        this.saving = true;
        this.say('Saving...', false);

        return fetch(url, { method: 'POST', body: body, credentials: 'same-origin' })
            .then(function (response) {
                if (response.redirected || response.status === 401) {
                    throw new Error('You are no longer signed in. Reload the page and sign in to carry on.');
                }

                return response.text().then(function (text) {
                    if (!response.ok) {
                        // 400, 403 and 409 come with a sentence for the coach; anything else comes with a page
                        var saysWhy = response.status === 400 || response.status === 403 || response.status === 409;
                        throw new Error(saysWhy && text ? text : 'That could not be saved (answer ' + response.status + '). Try again.');
                    }
                    return text;
                });
            })
            .then(function (text) {
                self.saving = false;
                return text;
            }, function (problem) {
                self.saving = false;
                throw problem;
            });
    };

    // ---------- What the page shows ----------

    ClipEditor.prototype.show = function () {
        var play = this.play;
        var clock = window.WarRoom.filmClock;

        this.set('start-text', play.startMs !== null ? clock(play.startMs, true) : 'not marked');
        this.set('end-text', play.endMs !== null ? clock(play.endMs, true) : 'not marked');
        this.set('length-text', play.startMs !== null && play.endMs !== null
            ? ((play.endMs - play.startMs) / 1000).toFixed(1) + ' s'
            : '–');
        this.set('form-title', play.id === 0 ? 'New play' : 'The play at ' + clock(play.startMs, false));
        this.form.querySelector('[data-role="save"]').value = play.id === 0 ? 'Save play' : 'Save changes';

        var count = this.list.querySelector('[data-play-count]');
        this.set('play-count', count ? count.getAttribute('data-play-count') : '0', this.root);

        // The row of the play that is open in the editor is marked in the list
        Array.prototype.forEach.call(this.rows(), function (row) {
            var isOpen = play.id !== 0 && Number(row.getAttribute('data-play-id')) === play.id;
            row.classList.toggle('table-active', isOpen);
            if (isOpen && row.scrollIntoView) {
                row.scrollIntoView({ block: 'nearest' });
            }
        });

        this.drawTimeline();
        this.showDrawings();
    };

    ClipEditor.prototype.set = function (role, text, within) {
        var element = (within || this.form).querySelector('[data-role="' + role + '"]');
        if (element) {
            element.textContent = text;
        }
    };

    ClipEditor.prototype.say = function (text, isProblem) {
        this.message.textContent = text || '';
        this.message.className = 'small ' + (isProblem ? 'text-danger' : 'text-success');
    };

    ClipEditor.prototype.rows = function () {
        return this.list.querySelectorAll('[data-play-id]');
    };

    // The strip under the player: every play as a block at its place in the video,
    // the play in the editor on top of them, and a line where the video is now
    ClipEditor.prototype.drawTimeline = function () {
        var self = this;
        var duration = this.player.durationMs();

        this.timeline.innerHTML = '';
        if (duration <= 0) {
            return;
        }

        function block(className, startMs, endMs) {
            var element = document.createElement('div');
            element.className = className;
            element.style.left = (startMs / duration) * 100 + '%';
            element.style.width = (Math.max(0, endMs - startMs) / duration) * 100 + '%';
            self.timeline.appendChild(element);
            return element;
        }

        Array.prototype.forEach.call(this.rows(), function (row) {
            var type = row.getAttribute('data-type');
            block('wr-film-mark' + (type === 'Run' ? ' wr-play-run' : type === 'Pass' ? ' wr-play-pass' : ''),
                Number(row.getAttribute('data-start')), Number(row.getAttribute('data-end')));
        });

        if (this.play.startMs !== null) {
            block('wr-film-selection', this.play.startMs, this.play.endMs !== null ? this.play.endMs : this.play.startMs);
        }

        this.head = block('wr-film-head', this.player.timeMs(), this.player.timeMs());
    };

    // Runs as the video moves: the line on the strip follows, and the play being shown is marked in the list
    ClipEditor.prototype.follow = function () {
        var now = this.player.timeMs();
        var duration = this.player.durationMs();

        if (this.head && duration > 0) {
            this.head.style.left = (now / duration) * 100 + '%';
        }

        Array.prototype.forEach.call(this.rows(), function (row) {
            var inside = now >= Number(row.getAttribute('data-start')) && now < Number(row.getAttribute('data-end'));
            row.classList.toggle('wr-play-now', inside);
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('[data-clip-editor]');

        // film-player.js is loaded before this file, so the player is already there
        if (root && window.WarRoom && window.WarRoom.filmPlayer && window.WarRoom.FilmOverlay) {
            new ClipEditor(root, window.WarRoom.filmPlayer);
        }
    });
})();
