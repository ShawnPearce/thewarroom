// Draws every read-only diagram on a page.
//
// A page shows a diagram by writing a canvas like this (Razor encodes the JSON for the attribute):
//   <canvas class="wr-diagram" data-diagram="@Model.DiagramJson"></canvas>
//
// Optional attributes:
//   data-mode="thumb"                 a small preview without labels
//   data-highlight="x,z"              ring these players (used by the before/after comparison)
//   data-highlight-colour="#198754"   the colour of that ring
(function () {
    'use strict';

    var Model = window.WarRoom.DiagramModel;
    var Renderer = window.WarRoom.DiagramRenderer;

    function drawAll() {
        var canvases = document.querySelectorAll('canvas[data-diagram]');

        Array.prototype.forEach.call(canvases, function (canvas) {
            var diagram = Model.parse(canvas.getAttribute('data-diagram'));
            var highlight = canvas.getAttribute('data-highlight');

            Renderer.draw(canvas, diagram, {
                mode: canvas.getAttribute('data-mode') || 'view',
                highlightIds: highlight ? highlight.split(',') : [],
                highlightColour: canvas.getAttribute('data-highlight-colour') || '#eda100'
            });
            Renderer.centre(canvas);
        });
    }

    // The comparison page marks what differs between its two diagrams.
    // Both canvases sit inside an element with data-diagram-compare; one is "before" and one is "after".
    // data-diagram-compare="addition" is a new play beside its formation: nothing is replaced,
    // so only the players that were given something to do are marked, on the new play.
    function markDifferences() {
        var groups = document.querySelectorAll('[data-diagram-compare]');

        Array.prototype.forEach.call(groups, function (group) {
            var before = group.querySelector('canvas[data-role="before"]');
            var after = group.querySelector('canvas[data-role="after"]');
            if (!before || !after) {
                return;
            }

            var difference = Model.diff(
                Model.parse(before.getAttribute('data-diagram')),
                Model.parse(after.getAttribute('data-diagram')));

            // Players that are gone or different are ringed in red on the old version...
            if (group.getAttribute('data-diagram-compare') !== 'addition') {
                before.setAttribute('data-highlight', difference.removed.concat(difference.changed).join(','));
                before.setAttribute('data-highlight-colour', '#dc3545');
            }

            // ...and what is new or different is ringed in green on the proposed version
            after.setAttribute('data-highlight', difference.changed.join(','));
            after.setAttribute('data-highlight-colour', '#198754');
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        markDifferences();
        drawAll();
    });

    // The canvases take their width from the page, so they are drawn again when the window changes size
    var resizeTimer = null;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(drawAll, 150);
    });
})();
