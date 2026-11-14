// The study timer: counts the time a player actively spends on a film or playbook page.
//
// Time counts only while this page is the visible tab, and
//   on a playbook page: the player has moved the mouse, scrolled, typed or touched the page in the last minute
//   on a film page:     the video is playing
// It pauses when the tab is hidden, when the video is paused, and when the player goes quiet.
//
// Every 15 seconds the counted time is reported to the site. The server decides how much of it to credit:
// never more than the clock allows, so two tabs cannot count the same minute twice.
//
// A page gets a timer by containing an element with data-study-timer (see the _StudyTimer partial).
(function () {
    'use strict';

    var REPORT_EVERY_MS = 15000;
    var QUIET_AFTER_MS = 60000;    // a playbook page stops counting after a minute without activity
    var TICK_MS = 1000;

    function StudyTimer(root) {
        this.root = root;
        this.kind = root.getAttribute('data-kind');   // "Film" or "Playbook"
        this.url = root.getAttribute('data-report-url');

        var videoSelector = root.getAttribute('data-video');
        this.video = videoSelector ? document.querySelector(videoSelector) : null;

        this.pageKey = newKey();        // identifies this visit to the page in every report
        this.pendingMs = 0;             // counted, not yet reported
        this.creditedMs = 0;            // what the server has counted for this visit
        this.lastTick = Date.now();
        this.lastActivity = Date.now();
        this.counting = false;
        this.stopped = false;           // true once the server says the reports are not accepted any more
    }

    StudyTimer.prototype.start = function () {
        var self = this;

        // Anything the player does on the page counts as activity
        ['mousemove', 'mousedown', 'keydown', 'scroll', 'wheel', 'touchstart'].forEach(function (name) {
            window.addEventListener(name, function () { self.lastActivity = Date.now(); }, { passive: true, capture: true });
        });

        setInterval(function () { self.tick(); }, TICK_MS);
        setInterval(function () { self.report(false); }, REPORT_EVERY_MS);

        // When the tab is hidden or the page is left, what was counted so far is sent straight away
        document.addEventListener('visibilitychange', function () {
            self.tick();
            if (document.visibilityState === 'hidden') {
                self.report(true);
            }
        });
        window.addEventListener('pagehide', function () { self.report(true); });

        this.tick();
        this.show();
    };

    // Is viewing time being counted right now?
    StudyTimer.prototype.isCounting = function (now) {
        if (this.stopped || document.visibilityState !== 'visible') {
            return false;
        }

        if (this.kind === 'Film') {
            var video = this.video;
            return !!video && !video.paused && !video.ended && video.readyState > 2;
        }

        return now - this.lastActivity < QUIET_AFTER_MS;
    };

    // Runs every second and adds the time since the last run, when it counts
    StudyTimer.prototype.tick = function () {
        var now = Date.now();
        var elapsed = now - this.lastTick;
        this.lastTick = now;

        // The state at the end of the second decides. A long gap means the computer slept
        // or the browser froze the tab, and that is not viewing time.
        var counting = this.isCounting(now);
        if (counting && this.counting && elapsed <= 3 * TICK_MS) {
            this.pendingMs += elapsed;
        }

        if (counting !== this.counting) {
            this.counting = counting;
            this.show();
        }
    };

    // Sends what was counted since the last report. With leaving = true the browser is asked to deliver it
    // even while the page is closing.
    StudyTimer.prototype.report = function (leaving) {
        var self = this;
        var activeMs = Math.round(this.pendingMs);
        if (this.stopped || activeMs < 1000) {
            return;
        }
        this.pendingMs = 0;

        var body = new FormData();
        body.append('Kind', this.kind);
        body.append('PageKey', this.pageKey);
        body.append('ActiveMs', activeMs);
        appendIfSet(body, 'PlaybookId', this.root.getAttribute('data-playbook-id'));
        appendIfSet(body, 'CutupId', this.root.getAttribute('data-cutup-id'));
        body.append('__RequestVerificationToken', this.root.querySelector('input[name="__RequestVerificationToken"]').value);

        if (leaving && navigator.sendBeacon) {
            navigator.sendBeacon(this.url, body);
            return;
        }

        fetch(this.url, { method: 'POST', body: body, credentials: 'same-origin' })
            .then(function (response) {
                if (response.redirected || response.status === 401 || response.status === 403) {
                    // The sign-in has ended (or this is not a player): there is nobody to count time for
                    self.stopped = true;
                    self.show();
                    return null;
                }
                return response.ok ? response.json() : null;
            })
            .then(function (credit) {
                if (credit) {
                    self.creditedMs = credit.pageTotalMs;
                    self.show();
                }
            })
            .catch(function () {
                // The report did not arrive. Its time is kept and goes out with the next one.
                self.pendingMs += activeMs;
            });
    };

    // The badge on the page: running or paused, and how much has been counted on this visit
    StudyTimer.prototype.show = function () {
        var state = this.root.querySelector('[data-role="study-state"]');
        var total = this.root.querySelector('[data-role="study-total"]');

        if (state) {
            state.textContent = this.stopped ? 'Study timer stopped' : (this.counting ? 'Study timer running' : 'Study timer paused');
            state.className = 'badge ' + (this.counting ? 'text-bg-success' : 'text-bg-secondary');
        }
        if (total) {
            if (this.stopped) {
                total.textContent = 'This page is no longer counting. Reload it to carry on.';
            } else {
                total.textContent = this.creditedMs >= 1000 ? 'Counted on this visit: ' + inWords(this.creditedMs) : '';
            }
        }
    };

    function appendIfSet(body, name, value) {
        if (value) {
            body.append(name, value);
        }
    }

    // "45 s", "2 min 5 s", "1 h 5 min"
    function inWords(milliseconds) {
        var seconds = Math.floor(milliseconds / 1000);
        var hours = Math.floor(seconds / 3600);
        var minutes = Math.floor((seconds % 3600) / 60);

        if (hours > 0) {
            return hours + ' h ' + minutes + ' min';
        }
        if (minutes > 0) {
            return minutes + ' min ' + (seconds % 60) + ' s';
        }
        return seconds + ' s';
    }

    // 32 random letters and digits
    function newKey() {
        if (window.crypto && window.crypto.randomUUID) {
            return window.crypto.randomUUID().replace(/-/g, '');
        }

        var key = '';
        while (key.length < 32) {
            key += Math.random().toString(36).substring(2);
        }
        return key.substring(0, 32);
    }

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('[data-study-timer]');
        if (root) {
            new StudyTimer(root).start();
        }
    });
})();
