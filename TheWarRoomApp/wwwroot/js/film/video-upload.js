// Uploading the video of a game.
//
// 1. The file is tried out in this browser first. A video that does not play here could not be cut into plays here.
// 2. The site is told about the file and answers where to send it:
//    straight to film storage with a signed link, or to the site itself when storage has no signed links.
// 3. The file is sent, with a progress bar.
// 4. The site is asked to check what arrived. Then the video's page opens.
//
// A page gets an uploader by containing a form with data-video-upload (see Views/Film/Upload.cshtml).
(function () {
    'use strict';

    var TRY_OUT_LIMIT_MS = 20000;

    function Uploader(form) {
        this.form = form;
        this.title = form.querySelector('[name="Title"]');
        this.file = form.querySelector('[name="File"]');
        this.token = form.querySelector('input[name="__RequestVerificationToken"]').value;

        this.startButton = form.querySelector('[data-role="upload-start"]');
        this.cancelButton = form.querySelector('[data-role="upload-cancel"]');
        this.errorBox = form.querySelector('[data-role="upload-error"]');
        this.progressBox = form.querySelector('[data-role="upload-progress"]');
        this.statusLine = form.querySelector('[data-role="upload-status"]');
        this.bar = form.querySelector('[data-role="upload-bar"]');
        this.detail = form.querySelector('[data-role="upload-detail"]');

        this.maxBytes = Number(form.getAttribute('data-max-bytes'));
        this.videoId = null;     // set once the site has made the record
        this.request = null;     // the request that is sending the file
        this.busy = false;
        this.cancelled = false;

        var self = this;
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            self.start();
        });
        this.cancelButton.addEventListener('click', function () { self.cancel(); });

        // Leaving the page would stop the upload
        window.addEventListener('beforeunload', function (event) {
            if (self.busy) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
    }

    Uploader.prototype.start = function () {
        var self = this;
        var file = this.file.files[0];

        this.showError(null);

        if (!this.title.value.trim()) {
            this.showError('Give the video a title.');
            return;
        }
        if (!file) {
            this.showError('Choose a video file.');
            return;
        }
        if (!/\.(mp4|m4v|mov)$/i.test(file.name)) {
            this.showError('Only MP4 and MOV videos can be uploaded.');
            return;
        }
        if (file.size > this.maxBytes) {
            this.showError('This file is ' + size(file.size) + '. The largest video that can be uploaded is ' + this.form.getAttribute('data-largest') + '.');
            return;
        }

        this.setBusy(true);
        this.cancelled = false;
        this.videoId = null;
        this.showProgress('Checking that the video plays in this browser...', null);

        tryOut(file)
            .then(function () {
                self.stopIfCancelled();
                self.showProgress('Getting ready to send the file...', null);

                return self.post(self.form.getAttribute('data-start-url'), {
                    GameId: self.form.getAttribute('data-game-id'),
                    Title: self.title.value.trim(),
                    FileName: file.name,
                    SizeBytes: file.size
                });
            })
            .then(function (ticket) {
                self.videoId = ticket.videoId;
                self.stopIfCancelled();
                return self.send(file, ticket);
            })
            .then(function () {
                self.stopIfCancelled();
                self.cancelButton.classList.add('d-none');   // the file is there: nothing is left to cancel
                self.showProgress('The file has arrived. Checking the video...', 100);
                return self.post(self.form.getAttribute('data-finish-url') + '/' + self.videoId, {});
            })
            .then(function (result) {
                self.busy = false;   // the page is left on purpose now
                window.location.href = result.url;
            })
            .catch(function (problem) {
                self.fail(problem);
            });
    };

    // Sends the file and reports how far it has come
    Uploader.prototype.send = function (file, ticket) {
        var self = this;
        var started = Date.now();

        return new Promise(function (resolve, reject) {
            var request = new XMLHttpRequest();
            self.request = request;

            request.open('PUT', ticket.url);
            request.setRequestHeader('Content-Type', ticket.contentType);
            if (ticket.throughSite) {
                // The site checks this token on everything that changes data. Film storage has no use for it.
                request.setRequestHeader('RequestVerificationToken', self.token);
            }

            request.upload.onprogress = function (event) {
                if (!event.lengthComputable) {
                    return;
                }

                var percent = event.total > 0 ? (event.loaded / event.total) * 100 : 0;
                var text = size(event.loaded) + ' of ' + size(event.total);

                // After a few seconds the speed so far gives a fair idea of the time left
                var seconds = (Date.now() - started) / 1000;
                if (seconds > 3 && event.loaded > 0 && event.loaded < event.total) {
                    text += ', about ' + duration((event.total - event.loaded) / (event.loaded / seconds)) + ' left';
                }

                self.showProgress('Sending the file...', percent, text);
            };

            request.onload = function () {
                self.request = null;

                if (request.status >= 200 && request.status < 300) {
                    resolve();
                } else if (ticket.throughSite && saysWhy(request.status) && request.responseText) {
                    reject(new Error(request.responseText));
                } else {
                    reject(new Error('The file was not accepted (answer ' + request.status + '). Try again.'));
                }
            };
            request.onerror = function () {
                self.request = null;
                reject(new Error('The connection was lost while the file was being sent. Try again.'));
            };
            request.onabort = function () {
                self.request = null;
                reject(new Error('The upload was cancelled.'));
            };

            self.showProgress('Sending the file...', 0, '0 MB of ' + size(file.size));
            request.send(file);
        });
    };

    Uploader.prototype.cancel = function () {
        this.cancelled = true;

        if (this.request) {
            this.request.abort();   // ends in fail(), which tidies up
        }
    };

    // Between two steps: an upload that was cancelled while the last step ran goes no further
    Uploader.prototype.stopIfCancelled = function () {
        if (this.cancelled) {
            throw new Error('The upload was cancelled.');
        }
    };

    // Something went wrong, or the upload was cancelled: say so, and leave no half-made record behind
    Uploader.prototype.fail = function (problem) {
        var self = this;
        var videoId = this.videoId;

        this.videoId = null;
        this.setBusy(false);
        this.progressBox.classList.add('d-none');
        if (this.cancelled) {
            this.showError('The upload was cancelled. Nothing was kept.');
        } else if (problem && problem.checked) {
            this.showError(problem.message + ' The upload is listed on the game\'s page with this reason, and can be deleted there.');
        } else {
            this.showError((problem && problem.message) || 'The upload did not work. Try again.');
        }

        // A video the server looked at and refused keeps its record: the game's page shows the reason.
        // Anything that stopped before that is removed.
        if (videoId && !(problem && problem.checked)) {
            this.post(this.form.getAttribute('data-cancel-url') + '/' + videoId, {}).catch(function () {
                // The record stays listed as "Uploading" on the game's page, where it can be deleted
                self.showError(self.errorBox.textContent + ' The unfinished upload is still listed on the game\'s page and can be deleted there.');
            });
        }
    };

    // Sends a small form to the site and reads its answer
    Uploader.prototype.post = function (url, fields) {
        var body = new FormData();
        Object.keys(fields).forEach(function (name) { body.append(name, fields[name]); });
        body.append('__RequestVerificationToken', this.token);

        var isFinish = url.indexOf(this.form.getAttribute('data-finish-url')) === 0;

        return fetch(url, { method: 'POST', body: body, credentials: 'same-origin' }).then(function (response) {
            if (response.redirected || response.status === 401) {
                throw new Error('You are no longer signed in. Reload the page, sign in and upload the file again.');
            }
            if (response.status === 204) {
                return {};
            }
            if (response.ok) {
                return response.json();
            }

            return response.text().then(function (text) {
                var problem = new Error(saysWhy(response.status) && text ? text : 'That could not be done (answer ' + response.status + '). Try again.');
                problem.checked = isFinish && response.status === 409;   // the server looked at the file and refused it
                throw problem;
            });
        });
    };

    // ---------- What the page shows ----------

    Uploader.prototype.setBusy = function (busy) {
        this.busy = busy;
        this.startButton.disabled = busy;
        this.title.disabled = busy;
        this.file.disabled = busy;
        this.cancelButton.classList.toggle('d-none', !busy);
    };

    Uploader.prototype.showError = function (message) {
        this.errorBox.textContent = message || '';
        this.errorBox.classList.toggle('d-none', !message);
    };

    // percent is null while there is nothing to measure: the bar then shows that work is going on
    Uploader.prototype.showProgress = function (status, percent, detail) {
        this.progressBox.classList.remove('d-none');
        this.statusLine.textContent = status;
        this.detail.textContent = detail || '';

        if (percent === null) {
            this.bar.removeAttribute('value');
        } else {
            this.bar.value = Math.round(percent);
        }
    };

    // ---------- Helpers ----------

    // The site answers these with a sentence for the person. Any other failure comes with a page, which is of no use here.
    function saysWhy(status) {
        return status === 400 || status === 403 || status === 409;
    }

    // Opens the file in a video element that is never shown, to see whether this browser can play it
    function tryOut(file) {
        return new Promise(function (resolve, reject) {
            var video = document.createElement('video');
            var address = URL.createObjectURL(file);
            var done = false;

            function finish(problem) {
                if (done) {
                    return;
                }
                done = true;

                video.removeAttribute('src');
                video.load();
                URL.revokeObjectURL(address);

                if (problem) {
                    reject(new Error(problem));
                } else {
                    resolve();
                }
            }

            video.preload = 'metadata';
            video.muted = true;

            video.onloadedmetadata = function () {
                if (!video.videoWidth || !isFinite(video.duration) || video.duration <= 0) {
                    finish('This browser cannot show the picture of this file. Videos have to be MP4 or MOV files with H.264 video. ' +
                        'Export the video as H.264 (often called "most compatible") and try again.');
                } else {
                    finish(null);
                }
            };
            video.onerror = function () {
                finish('This browser cannot play this file. Videos have to be MP4 or MOV files with H.264 video. ' +
                    'Export the video as H.264 (often called "most compatible") and try again.');
            };
            window.setTimeout(function () {
                finish('This file could not be opened in this browser. Check that it is a video that plays on this computer.');
            }, TRY_OUT_LIMIT_MS);

            video.src = address;
        });
    }

    // 367001600 -> "350 MB", 1288490188 -> "1.2 GB"
    function size(bytes) {
        var megabytes = bytes / (1024 * 1024);
        if (megabytes >= 1024) {
            return (megabytes / 1024).toFixed(1).replace(/\.0$/, '') + ' GB';
        }
        return Math.max(megabytes < 1 && bytes > 0 ? 1 : 0, Math.round(megabytes)) + ' MB';
    }

    // 75 -> "1 min 15 s", 3700 -> "1 h 2 min"
    function duration(seconds) {
        seconds = Math.max(1, Math.round(seconds));

        if (seconds >= 3600) {
            return Math.floor(seconds / 3600) + ' h ' + Math.floor((seconds % 3600) / 60) + ' min';
        }
        if (seconds >= 60) {
            return Math.floor(seconds / 60) + ' min ' + (seconds % 60) + ' s';
        }
        return seconds + ' s';
    }

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.querySelector('[data-video-upload]');
        if (form) {
            new Uploader(form);
        }
    });
})();
