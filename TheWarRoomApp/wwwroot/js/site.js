// Small behaviours shared by every page of The War Room.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        showLocalTimes();
        clearSavedDrafts();
        bindAutoSubmit();
        bindCheckAll();
    });

    // The server writes a moment in time as <time datetime="..." data-local>. It is sent as UTC
    // and shown here in the time zone of the person who is looking at the page.
    function showLocalTimes() {
        var times = document.querySelectorAll('time[data-local]');

        Array.prototype.forEach.call(times, function (element) {
            var moment = new Date(element.getAttribute('datetime'));
            if (!isNaN(moment.getTime())) {
                element.textContent = moment.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
            }
        });
    }

    // After a drawing was saved, the copy the play designer kept in this browser is no longer needed.
    // The page that follows a successful save names it in data-clear-draft.
    function clearSavedDrafts() {
        var markers = document.querySelectorAll('[data-clear-draft]');

        Array.prototype.forEach.call(markers, function (marker) {
            try {
                window.localStorage.removeItem('warroom.draft.' + marker.getAttribute('data-clear-draft'));
            } catch (error) {
                // The browser's storage is switched off: there was no copy to remove
            }
        });
    }

    // A tick box marked data-check-all ticks or clears every other tick box of its form that can be changed
    function bindCheckAll() {
        var switches = document.querySelectorAll('[data-check-all]');

        Array.prototype.forEach.call(switches, function (all) {
            all.addEventListener('change', function () {
                var boxes = all.form ? all.form.querySelectorAll('input[type="checkbox"]:not([data-check-all])') : [];

                Array.prototype.forEach.call(boxes, function (box) {
                    if (!box.disabled) {
                        box.checked = all.checked;
                    }
                });
            });
        });
    }

    // A drop-down marked data-auto-submit sends its form as soon as another choice is made
    function bindAutoSubmit() {
        var inputs = document.querySelectorAll('[data-auto-submit]');

        Array.prototype.forEach.call(inputs, function (input) {
            input.addEventListener('change', function () {
                if (input.form) {
                    input.form.submit();
                }
            });
        });
    }
})();
