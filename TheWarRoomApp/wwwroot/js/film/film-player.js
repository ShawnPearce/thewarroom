// The video player of the film pages: a video with controls of its own.
//
// Keys, when no text box has the focus:
//   Space or K       play / pause
//   Left / Right     5 seconds back / forward (with Shift: 1 second)
//   , and .          one picture back / forward
//   F                full screen
//
// A page gets a player by containing the _FilmPlayer partial. Other scripts reach it as
// WarRoom.filmPlayer and listen to it with on('time' | 'state' | 'ready' | 'holdend', handler).
(function () {
    'use strict';

    window.WarRoom = window.WarRoom || {};

    var FRAME_MS = 1000 / 30;          // film is shown at 30 pictures a second
    var LOADING_AFTER_MS = 400;        // a short wait is not worth a message

    function FilmPlayer(root) {
        this.root = root;
        this.video = root.querySelector('video');
        this.stage = root.querySelector('[data-role="stage"]');
        this.seekBar = root.querySelector('[data-role="seek"]');
        this.timeText = root.querySelector('[data-role="time"]');
        this.message = root.querySelector('[data-role="film-message"]');
        this.playButton = root.querySelector('[data-command="play"]');
        this.speed = root.querySelector('[data-role="speed"]');

        this.handlers = {};
        this.stopAtMs = null;      // set while one play is played on its own: the video pauses there
        this.dragging = false;     // true while the seek bar is being dragged
        this.holding = false;      // true while the video stands still on a drawing and will carry on by itself
        this.locked = false;       // true while something is being drawn on the picture: the video stays where it is
        this.loadingTimer = null;
        this.failed = false;

        this.bind();
        this.show();
    }

    // ---------- For other scripts ----------

    FilmPlayer.prototype.on = function (name, handler) {
        (this.handlers[name] = this.handlers[name] || []).push(handler);
    };

    FilmPlayer.prototype.fire = function (name) {
        var self = this;
        (this.handlers[name] || []).forEach(function (handler) { handler(self); });
    };

    // Where the video is, in whole milliseconds
    FilmPlayer.prototype.timeMs = function () {
        return Math.round(this.video.currentTime * 1000);
    };

    // The length of the video in milliseconds; 0 until the browser knows it
    FilmPlayer.prototype.durationMs = function () {
        return isFinite(this.video.duration) ? Math.floor(this.video.duration * 1000) : 0;
    };

    // Playing, as the person watching sees it: also while the video stands still for a moment on a drawing
    FilmPlayer.prototype.isPlaying = function () {
        return this.holding || (!this.video.paused && !this.video.ended);
    };

    FilmPlayer.prototype.seek = function (milliseconds) {
        var last = this.durationMs();
        var wasHolding = this.holding;

        this.endHold(false);
        this.stopAtMs = null;
        this.video.currentTime = Math.max(0, last > 0 ? Math.min(milliseconds, last) : milliseconds) / 1000;

        // Jumping away from a drawing that was being shown does not stop the film
        if (wasHolding) {
            this.play();
        }
        this.show();
    };

    // Stops the video on one picture without ending the playing: the button still says Pause,
    // and whoever asked for the hold ends it with endHold(true) to let the video carry on.
    FilmPlayer.prototype.hold = function (atMs) {
        this.holding = true;
        this.video.setAttribute('data-holding', 'true');   // the study timer goes on counting while a drawing is shown
        this.video.pause();
        this.video.currentTime = atMs / 1000;
        this.show();
    };

    FilmPlayer.prototype.endHold = function (carryOn) {
        if (!this.holding) {
            return;
        }

        this.holding = false;
        this.video.removeAttribute('data-holding');
        this.fire('holdend');

        if (carryOn) {
            this.play();
        } else {
            this.show();
            this.fire('state');
        }
    };

    // While locked, the video cannot be moved or played: something is being drawn on the picture it shows
    FilmPlayer.prototype.setLocked = function (locked) {
        this.locked = locked;
        if (locked) {
            this.pause();
        }

        Array.prototype.forEach.call(this.root.querySelectorAll('.wr-film-controls button, .wr-film-controls input, .wr-film-controls select'), function (control) {
            control.disabled = locked;
        });
    };

    FilmPlayer.prototype.seekBy = function (milliseconds) {
        this.seek(this.video.currentTime * 1000 + milliseconds);
    };

    // One picture forward (1) or back (-1). The video is paused first, so the picture can be looked at.
    FilmPlayer.prototype.step = function (pictures) {
        this.pause();
        this.seekBy(pictures * FRAME_MS);
    };

    FilmPlayer.prototype.play = function () {
        var self = this;
        if (this.locked) {
            return;
        }

        var started = this.video.play();

        if (started && started.catch) {
            started.catch(function () {
                // The browser refused to start the video, or it was paused again straight away: the button shows which
                self.show();
            });
        }
    };

    FilmPlayer.prototype.pause = function () {
        this.endHold(false);
        this.stopAtMs = null;
        this.video.pause();
    };

    FilmPlayer.prototype.toggle = function () {
        if (this.isPlaying()) {
            this.pause();
        } else {
            this.play();
        }
    };

    // Plays one part of the video and pauses at its end
    FilmPlayer.prototype.playRange = function (startMs, endMs) {
        this.seek(startMs);
        this.stopAtMs = endMs;
        this.play();
    };

    FilmPlayer.prototype.toggleFullScreen = function () {
        if (document.fullscreenElement) {
            document.exitFullscreen();
        } else if (this.root.requestFullscreen) {
            // The whole player goes full screen, not the video alone, so its controls come along
            this.root.requestFullscreen();
        }
    };

    // ---------- Wiring ----------

    FilmPlayer.prototype.bind = function () {
        var self = this;
        var video = this.video;

        video.addEventListener('loadedmetadata', function () {
            self.seekBar.max = video.duration;
            self.show();
            self.fire('ready');
        });

        ['timeupdate', 'seeking', 'seeked'].forEach(function (name) {
            video.addEventListener(name, function () { self.show(); });
        });

        video.addEventListener('play', function () {
            self.watch();
            self.show();
            self.fire('state');
        });
        ['pause', 'ended'].forEach(function (name) {
            video.addEventListener(name, function () {
                self.show();
                self.fire('state');
            });
        });

        // A message while the browser is fetching the next part of the video
        video.addEventListener('waiting', function () {
            window.clearTimeout(self.loadingTimer);
            self.loadingTimer = window.setTimeout(function () { self.say('Loading...'); }, LOADING_AFTER_MS);
        });
        ['playing', 'canplay', 'seeked'].forEach(function (name) {
            video.addEventListener(name, function () {
                window.clearTimeout(self.loadingTimer);
                if (!self.failed) {
                    self.say(null);
                }
            });
        });

        video.addEventListener('error', function () {
            self.failed = true;
            window.clearTimeout(self.loadingTimer);
            self.say('The video could not be loaded. Reload the page to try again. If you were away for a while, you may have to sign in first.');
        });

        video.addEventListener('click', function () {
            if (!self.locked) {
                self.toggle();
            }
        });

        // The seek bar: dragging it moves the video, and the video moves it while it plays
        this.seekBar.addEventListener('pointerdown', function () { self.dragging = true; });
        this.seekBar.addEventListener('input', function () {
            self.stopAtMs = null;
            video.currentTime = Number(self.seekBar.value);
            self.show();
        });
        ['change', 'pointerup', 'blur'].forEach(function (name) {
            self.seekBar.addEventListener(name, function () { self.dragging = false; });
        });

        this.speed.addEventListener('change', function () {
            video.playbackRate = Number(self.speed.value);
        });

        Array.prototype.forEach.call(this.root.querySelectorAll('[data-command]'), function (button) {
            button.addEventListener('click', function () { self.run(button.getAttribute('data-command')); });
        });

        document.addEventListener('keydown', function (event) { self.onKey(event); });
    };

    FilmPlayer.prototype.run = function (command) {
        if (this.locked) {
            return;
        }

        switch (command) {
            case 'play': this.toggle(); break;
            case 'back': this.seekBy(-5000); break;
            case 'forward': this.seekBy(5000); break;
            case 'frame-back': this.step(-1); break;
            case 'frame-forward': this.step(1); break;
            case 'fullscreen': this.toggleFullScreen(); break;
        }
    };

    FilmPlayer.prototype.onKey = function (event) {
        if (this.locked || event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) {
            return;
        }

        // Keys typed into a text box or a drop-down belong to it. The seek bar is the exception:
        // its own arrow keys would move a twentieth of a second at a time.
        var target = event.target;
        var tag = target && target.tagName;
        var onSeekBar = target === this.seekBar;
        if (!onSeekBar && (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (target && target.isContentEditable))) {
            return;
        }

        // Space on a button presses that button
        if (tag === 'BUTTON' && (event.key === ' ' || event.key === 'Enter')) {
            return;
        }

        switch (event.key) {
            case ' ':
            case 'k':
            case 'K':
                this.toggle();
                break;
            case 'ArrowLeft':
                this.seekBy(event.shiftKey ? -1000 : -5000);
                break;
            case 'ArrowRight':
                this.seekBy(event.shiftKey ? 1000 : 5000);
                break;
            case ',':
                this.step(-1);
                break;
            case '.':
                this.step(1);
                break;
            case 'f':
            case 'F':
                this.toggleFullScreen();
                break;
            default:
                return;   // not one of the player's keys
        }

        event.preventDefault();   // Space would scroll the page otherwise
    };

    // While the video plays, the time is followed picture by picture:
    // the readout runs smoothly, and a play that is played on its own stops where it ends.
    FilmPlayer.prototype.watch = function () {
        var self = this;

        function look() {
            if (!self.isPlaying()) {
                return;
            }

            if (self.stopAtMs !== null && self.video.currentTime * 1000 >= self.stopAtMs) {
                var stopAt = self.stopAtMs;
                self.pause();
                self.video.currentTime = stopAt / 1000;
            }

            self.show();
            window.requestAnimationFrame(look);
        }

        window.requestAnimationFrame(look);
    };

    // ---------- What the player shows ----------

    FilmPlayer.prototype.show = function () {
        var playing = this.isPlaying();

        this.playButton.textContent = playing ? 'Pause' : 'Play';
        this.timeText.textContent = clock(this.video.currentTime * 1000, true) + ' / ' + clock(this.durationMs(), false);

        if (!this.dragging) {
            this.seekBar.value = this.video.currentTime;
        }
        this.seekBar.setAttribute('aria-valuetext', clock(this.video.currentTime * 1000, true));

        this.fire('time');
    };

    FilmPlayer.prototype.say = function (text) {
        this.message.textContent = text || '';
        this.message.classList.toggle('d-none', !text);
    };

    // 754500 -> "12:34.5", 4530000 -> "1:15:30.0". Without tenths: "12:34".
    function clock(milliseconds, withTenths) {
        var tenths = Math.floor(Math.max(0, milliseconds) / 100);
        var seconds = Math.floor(tenths / 10);
        var hours = Math.floor(seconds / 3600);
        var minutes = Math.floor((seconds % 3600) / 60);
        var rest = seconds % 60;

        var text = hours > 0
            ? hours + ':' + two(minutes) + ':' + two(rest)
            : minutes + ':' + two(rest);

        return withTenths ? text + '.' + (tenths % 10) : text;
    }

    function two(number) {
        return number < 10 ? '0' + number : String(number);
    }

    WarRoom.FilmPlayer = FilmPlayer;
    WarRoom.filmClock = clock;

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('[data-film-player]');
        if (root) {
            WarRoom.filmPlayer = new FilmPlayer(root);
        }
    });
})();
