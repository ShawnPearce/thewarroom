// The page of a cutup.
//
// Once the cutup has been made into a video: the plays in the list are places to jump to, the play being shown
// is marked, and what a coach drew on a play is shown over the picture, with the video standing still for it.
//
// While the video is being made (coaches only): the page asks the site every few seconds how far it has come,
// and loads itself again when it is finished.
(function () {
    'use strict';

    var ASK_EVERY_MS = 2000;
    var STOP_ASKING_AFTER_MS = 30 * 60 * 1000;   // a page left open does not ask for ever
    var BEFORE_THE_END_MS = 50;                  // a drawing on a play's last moment still belongs to that play

    function watchVideo() {
        var root = document.querySelector('[data-cutup-watch]');
        var player = window.WarRoom && window.WarRoom.filmPlayer;
        if (!root || !player || !window.WarRoom.FilmOverlay) {
            return;
        }

        var nowShowing = document.querySelector('[data-role="now-showing"]');

        var plays = Array.prototype.map.call(root.querySelectorAll('[data-play-offset]'), function (row) {
            return {
                row: row,
                position: row.getAttribute('data-play-position'),
                title: row.getAttribute('data-play-title') || '',
                offsetMs: Number(row.getAttribute('data-play-offset')),        // where the play starts in the cutup
                durationMs: Number(row.getAttribute('data-play-duration')),
                startMs: Number(row.getAttribute('data-play-start'))           // where it started in its game video
            };
        });

        // A drawing was made on a moment of the game video. In the cutup that moment is
        // the play's place in the cutup plus how far into the play the drawing is.
        var stops = [];
        plays.forEach(function (play) {
            window.WarRoom.FilmOverlay.read(play.row.getAttribute('data-annotations')).forEach(function (stop) {
                var intoThePlay = Math.max(0, Math.min(stop.atMs - play.startMs, play.durationMs - BEFORE_THE_END_MS));
                stops.push({ atMs: play.offsetMs + intoThePlay, holdMs: stop.holdMs, marks: stop.marks });
            });
        });

        new window.WarRoom.FilmOverlay(player).setStops(stops);

        // A click on a play's time plays the cutup from there
        root.addEventListener('click', function (event) {
            var button = event.target.closest ? event.target.closest('[data-command="go-to-play"]') : null;
            var row = button ? button.closest('[data-play-offset]') : null;
            if (row) {
                player.seek(Number(row.getAttribute('data-play-offset')));
                player.play();
            }
        });

        // The play being shown is marked in the list and named under the video
        var showing = null;
        player.on('time', function () {
            var now = player.timeMs();
            var current = null;

            plays.forEach(function (play) {
                if (now >= play.offsetMs) {
                    current = play;   // the plays are in order, so the last one that has started is the one on screen
                }
            });

            if (current === showing) {
                return;
            }
            showing = current;

            plays.forEach(function (play) {
                play.row.classList.toggle('table-active', play === current);
            });
            if (nowShowing && current) {
                nowShowing.textContent = 'Play ' + current.position + ' of ' + plays.length + (current.title ? ': ' + current.title : '');
            }
        });
    }

    function waitForVideo() {
        var box = document.querySelector('[data-cutup-status]');
        if (!box) {
            return;
        }

        var url = box.getAttribute('data-url');
        var text = box.querySelector('[data-role="status-text"]');
        var bar = box.querySelector('progress');
        var started = Date.now();

        function ask() {
            if (Date.now() - started > STOP_ASKING_AFTER_MS) {
                text.textContent = 'Still being made. Reload the page to see how far it has come.';
                return;
            }

            fetch(url, { credentials: 'same-origin' })
                .then(function (response) {
                    return response.ok && !response.redirected ? response.json() : null;
                })
                .then(function (answer) {
                    if (!answer) {
                        return;   // signed out, or the cutup is gone: there is nothing more to ask
                    }

                    if (answer.status !== 'Queued' && answer.status !== 'Rendering') {
                        window.location.reload();   // ready, or failed: the page shows which
                        return;
                    }

                    bar.value = answer.progressPercent;
                    text.textContent = answer.status === 'Queued'
                        ? 'Waiting for its turn...'
                        : 'Making the video: ' + answer.progressPercent + '%';
                    window.setTimeout(ask, ASK_EVERY_MS);
                })
                .catch(function () {
                    window.setTimeout(ask, ASK_EVERY_MS * 3);   // the connection dropped for a moment
                });
        }

        window.setTimeout(ask, ASK_EVERY_MS);
    }

    document.addEventListener('DOMContentLoaded', function () {
        watchVideo();
        waitForVideo();
    });
})();
