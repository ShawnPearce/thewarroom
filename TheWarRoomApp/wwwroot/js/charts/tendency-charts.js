// Draws the run/pass tendency charts with Chart.js.
//
// The page writes each chart as a canvas with its bars in data-tendency-chart (Razor encodes the JSON):
//   <canvas data-tendency-chart='[{"label":"1st down","runs":12,"passes":8,"plays":20,"runPercent":60.0,"passPercent":40.0}]'></canvas>
//
// Every bar is as long as 100%: the run share first, the pass share after it. A group with no plays has no bar.
(function () {
    'use strict';

    // The same two colours the Run and Pass badges use
    var RUN_COLOUR = '#1e3a66';
    var PASS_COLOUR = '#f2a900';

    document.addEventListener('DOMContentLoaded', function () {
        if (!window.Chart) {
            return;   // the chart library did not load; the table on the page still has every number
        }

        window.Chart.defaults.font.family = 'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';
        window.Chart.defaults.color = '#212529';

        var canvases = document.querySelectorAll('canvas[data-tendency-chart]');
        Array.prototype.forEach.call(canvases, draw);
    });

    function draw(canvas) {
        var groups = JSON.parse(canvas.getAttribute('data-tendency-chart'));

        new window.Chart(canvas, {
            type: 'bar',
            data: {
                // The sample size goes with every bar
                labels: groups.map(function (group) {
                    return group.plays > 0 ? group.label + '  (n = ' + group.plays + ')' : group.label + '  (no data)';
                }),
                datasets: [
                    {
                        label: 'Run',
                        data: groups.map(function (group) { return group.runPercent; }),
                        backgroundColor: RUN_COLOUR
                    },
                    {
                        label: 'Pass',
                        data: groups.map(function (group) { return group.passPercent; }),
                        backgroundColor: PASS_COLOUR
                    }
                ]
            },
            options: {
                indexAxis: 'y',               // bars run from left to right
                responsive: true,
                maintainAspectRatio: false,   // the chart fills the box the page gives it
                animation: false,
                scales: {
                    x: {
                        stacked: true,
                        min: 0,
                        max: 100,
                        ticks: {
                            callback: function (value) { return value + '%'; }
                        }
                    },
                    y: {
                        stacked: true
                    }
                },
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 14 }
                    },
                    tooltip: {
                        callbacks: {
                            // "Run: 60.0% (12 of 20 plays)"
                            label: function (context) {
                                var group = groups[context.dataIndex];
                                var count = context.datasetIndex === 0 ? group.runs : group.passes;
                                return context.dataset.label + ': ' + context.parsed.x.toFixed(1) + '% (' + count + ' of ' + group.plays + ' plays)';
                            }
                        }
                    }
                }
            }
        });
    }
})();
