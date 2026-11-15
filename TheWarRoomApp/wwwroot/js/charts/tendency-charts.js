// Draws the run/pass tendency charts with Chart.js.
//
// The page writes each chart as a canvas with its bars in data-tendency-chart (Razor encodes the JSON):
//   <canvas data-tendency-chart='[{"label":"1st down","runs":12,"passes":8,"plays":20,"runPercent":60.0,"passPercent":40.0}]'></canvas>
//
// Every bar is as long as 100%: the run share first, the pass share after it. A group with no plays has no bar.
(function () {
    'use strict';

    // The same two colours the Run and Pass badges use (--wr-run and --wr-pass in site.css).
    // They can be told apart without seeing colour the usual way, and each share is also printed on its part of the bar.
    var RUN_COLOUR = '#1f51e6';
    var PASS_COLOUR = '#eda100';

    var SURFACE = '#ffffff';   // what the charts stand on: the gap between the two parts of a bar is this colour
    var INK = '#0e1627';
    var TEXT = '#354155';
    var MUTED = '#5b6980';
    var GRID = '#eef2f7';
    var FONT = '-apple-system, BlinkMacSystemFont, "Segoe UI Variable Text", "Segoe UI", system-ui, Roboto, "Helvetica Neue", Arial, sans-serif';

    // Prints each part's share on it, where the part is wide enough to hold it.
    // A part that is too narrow goes without: the pointer and the table on the page still give its number.
    var shareLabels = {
        id: 'shareLabels',
        afterDatasetsDraw: function (chart) {
            var context = chart.ctx;

            context.save();
            context.font = '600 11px ' + FONT;
            context.textBaseline = 'middle';
            context.textAlign = 'left';

            chart.data.datasets.forEach(function (dataset, datasetIndex) {
                if (!chart.isDatasetVisible(datasetIndex)) {
                    return;
                }

                chart.getDatasetMeta(datasetIndex).data.forEach(function (bar, index) {
                    var value = dataset.data[index];
                    if (typeof value !== 'number' || value <= 0) {
                        return;
                    }

                    var place = bar.getProps(['x', 'y', 'base'], true);
                    var left = Math.min(place.x, place.base);
                    var width = Math.abs(place.x - place.base);
                    var text = value.toFixed(1) + '%';

                    if (context.measureText(text).width + 16 > width) {
                        return;
                    }

                    context.fillStyle = datasetIndex === 0 ? '#ffffff' : INK;
                    context.fillText(text, left + 8, place.y + 0.5);
                });
            });

            context.restore();
        }
    };

    document.addEventListener('DOMContentLoaded', function () {
        if (!window.Chart) {
            return;   // the chart library did not load; the table on the page still has every number
        }

        window.Chart.defaults.font.family = FONT;
        window.Chart.defaults.color = TEXT;

        var canvases = document.querySelectorAll('canvas[data-tendency-chart]');
        Array.prototype.forEach.call(canvases, draw);
    });

    function draw(canvas) {
        var groups = JSON.parse(canvas.getAttribute('data-tendency-chart'));

        new window.Chart(canvas, {
            type: 'bar',
            plugins: [shareLabels],
            data: {
                // The sample size goes with every bar
                labels: groups.map(function (group) {
                    return group.plays > 0 ? group.label + '  (n = ' + group.plays + ')' : group.label + '  (no data)';
                }),
                datasets: [
                    {
                        label: 'Run',
                        data: groups.map(function (group) { return group.runPercent; }),
                        backgroundColor: RUN_COLOUR,
                        // The two parts of a bar do not touch: a sliver of the surface shows between them
                        borderColor: SURFACE,
                        borderWidth: { top: 0, right: 2, bottom: 0, left: 0 }
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
                datasets: {
                    bar: {
                        maxBarThickness: 24,
                        borderRadius: 4,
                        borderSkipped: false
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        min: 0,
                        max: 100,
                        border: { display: false },
                        grid: { color: GRID, drawTicks: false },
                        ticks: {
                            stepSize: 25,
                            padding: 8,
                            color: MUTED,
                            font: { size: 11 },
                            callback: function (value) { return value + '%'; }
                        }
                    },
                    y: {
                        stacked: true,
                        border: { display: false },
                        grid: { display: false },
                        ticks: {
                            color: TEXT,
                            font: { size: 12 }
                        }
                    }
                },
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 10,
                            boxHeight: 10,
                            useBorderRadius: true,
                            borderRadius: 3,
                            padding: 16,
                            color: TEXT,
                            font: { size: 12, weight: 600 }
                        }
                    },
                    tooltip: {
                        backgroundColor: INK,
                        padding: 10,
                        cornerRadius: 8,
                        boxPadding: 4,
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
