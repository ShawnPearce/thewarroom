// The guide page. Three things are added to what the server sent:
//   - the roles at the top switch between the three guides without loading the page again;
//   - the list of a guide's parts marks the part the reader has reached;
//   - a search box shows only the cards that hold what was typed.
// Without this script every link still works: a role at the top loads the page with that guide open.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('[data-guide]');
        if (root) {
            new Guide(root);
        }
    });

    function Guide(root) {
        var self = this;

        this.root = root;
        this.roles = toArray(root.querySelectorAll('[data-guide-role]'));
        this.panels = toArray(root.querySelectorAll('[data-guide-panel]'));
        this.marked = null;      // the entry of the list that is marked now
        this.waiting = false;    // a look at the scroll position is already on its way

        this.roles.forEach(function (link) {
            link.addEventListener('click', function (event) {
                // A click that opens another tab or window is left to the browser
                if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                    return;
                }

                event.preventDefault();
                self.open(link.getAttribute('data-guide-role'));

                try {
                    window.history.replaceState(null, '', link.href);
                } catch (error) {
                    // The address simply stays as it was
                }
            });
        });

        this.panels.forEach(function (panel) {
            self.bindSearch(panel);
        });

        // The address can name a place inside a guide that is not the open one
        this.followAddress();
        window.addEventListener('hashchange', function () {
            self.followAddress();
        });

        window.addEventListener('scroll', function () {
            self.markSoon();
        }, { passive: true });
        window.addEventListener('resize', function () {
            self.markSoon();
        });
        this.mark();
    }

    // Shows one guide and marks its role at the top. Which guide is open is the root's data-open; the stylesheet hides the others.
    Guide.prototype.open = function (role) {
        this.root.setAttribute('data-open', role);

        this.roles.forEach(function (link) {
            var on = link.getAttribute('data-guide-role') === role;

            link.classList.toggle('active', on);
            if (on) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        });

        this.marked = null;
        this.mark();
    };

    Guide.prototype.openPanel = function () {
        var role = this.root.getAttribute('data-open');

        return this.panels.filter(function (panel) {
            return panel.getAttribute('data-guide-panel') === role;
        })[0] || null;
    };

    // Goes to the place the address names, opening its guide first if another one is open
    Guide.prototype.followAddress = function () {
        var id = '';
        try {
            id = decodeURIComponent((window.location.hash || '').slice(1));
        } catch (error) {
            return;
        }

        var target = id ? document.getElementById(id) : null;
        if (!target || !this.root.contains(target)) {
            return;
        }

        // The browser goes to the place by itself, unless the place was not on the page when it tried
        var wasHidden = false;

        var panel = target.closest('[data-guide-panel]');
        if (panel) {
            // What a search has hidden comes back, so the place can be shown
            var input = panel.querySelector('[data-guide-search]');
            if (input && input.value !== '') {
                input.value = '';
                this.search(panel, '');
                wasHidden = true;
            }

            if (panel.getAttribute('data-guide-panel') !== this.root.getAttribute('data-open')) {
                this.open(panel.getAttribute('data-guide-panel'));
                wasHidden = true;
            }
        }

        // A question that is jumped to opens to its answer
        if (target.tagName === 'DETAILS') {
            target.open = true;
        }

        if (wasHidden) {
            target.scrollIntoView({ behavior: 'instant', block: 'start' });
        }
        this.mark();
    };

    // ---------- The list that follows the reader ----------

    Guide.prototype.markSoon = function () {
        var self = this;

        if (this.waiting) {
            return;
        }
        this.waiting = true;

        window.requestAnimationFrame(function () {
            self.waiting = false;
            self.mark();
        });
    };

    // Marks the entry of the part that has reached the top of the screen
    Guide.prototype.mark = function () {
        var panel = this.openPanel();
        if (!panel) {
            return;
        }

        var links = toArray(panel.querySelectorAll('.wr-guide-nav a'));
        var reached = null;
        var first = null;

        links.forEach(function (link) {
            var section = document.getElementById(link.getAttribute('href').slice(1));
            if (!section || section.hidden) {
                return;
            }

            // A part counts as reached a little before its heading meets the place a jump stops at
            var line = (parseFloat(window.getComputedStyle(section).scrollMarginTop) || 0) + 40;

            first = first || link;
            if (section.getBoundingClientRect().top <= line) {
                reached = link;
            }
        });

        // Above the first part, the first part is the one to come
        var current = reached || first;
        if (current === this.marked) {
            return;
        }
        this.marked = current;

        links.forEach(function (link) {
            var on = link === current;

            link.classList.toggle('active', on);
            if (on) {
                link.setAttribute('aria-current', 'true');
            } else {
                link.removeAttribute('aria-current');
            }
        });

        // Where the list is a row that scrolls sideways, the marked entry is kept in sight
        var list = current ? current.closest('ul') : null;
        if (list && list.scrollWidth > list.clientWidth) {
            var left = current.offsetLeft - list.offsetLeft - (list.clientWidth - current.offsetWidth) / 2;
            list.scrollLeft = Math.max(0, left);
        }
    };

    // ---------- Search ----------

    Guide.prototype.bindSearch = function (panel) {
        var self = this;
        var box = panel.querySelector('[data-guide-search-box]');
        var input = panel.querySelector('[data-guide-search]');

        if (!box || !input) {
            return;
        }

        box.hidden = false;   // the box is of use only with this script, so the page keeps it hidden until now

        input.addEventListener('input', function () {
            self.search(panel, input.value);
        });
        input.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && input.value !== '') {
                event.preventDefault();
                input.value = '';
                self.search(panel, '');
            }
        });
    };

    // Shows the cards of a guide that hold every word that was typed and hides the rest.
    // A word in the heading of a part counts for each of its cards, so "film" brings back the whole film part.
    Guide.prototype.search = function (panel, typed) {
        var words = typed.toLowerCase().split(/\s+/).filter(Boolean);
        var searching = words.length > 0;
        var found = 0;

        toArray(panel.querySelectorAll('.wr-guide-section')).forEach(function (section) {
            var heading = section.querySelector('.wr-guide-head');
            var about = heading ? heading.textContent.toLowerCase() : '';
            var shown = 0;

            // The cards of a part are what stands under its heading; cards that stand side by side are taken one by one
            var cards = [];
            var rows = [];
            toArray(section.children).forEach(function (child) {
                if (child === heading) {
                    return;
                }
                if (child.classList.contains('wr-guide-glance')) {
                    rows.push(child);
                    cards = cards.concat(toArray(child.children));
                } else {
                    cards.push(child);
                }
            });

            cards.forEach(function (card) {
                var text = about + ' ' + card.textContent.toLowerCase();
                var matches = words.every(function (word) {
                    return text.indexOf(word) >= 0;
                });

                card.hidden = !matches;
                if (matches) {
                    shown += 1;
                }

                // A question that was found opens to its answer
                if (card.tagName === 'DETAILS') {
                    card.open = searching && matches;
                }
            });

            // A row with none of its cards left goes too
            rows.forEach(function (row) {
                row.hidden = toArray(row.children).every(function (card) {
                    return card.hidden;
                });
            });

            section.hidden = shown === 0;
            found += shown;

            // The list keeps to the parts that are still shown
            var link = panel.querySelector('.wr-guide-nav a[href="#' + section.id + '"]');
            if (link && link.parentNode) {
                link.parentNode.hidden = section.hidden;
            }
        });

        var count = panel.querySelector('[data-guide-count]');
        if (count) {
            count.hidden = !searching || found === 0;
            count.textContent = found === 1 ? '1 card matches.' : found + ' cards match.';
        }

        var empty = panel.querySelector('[data-guide-empty]');
        if (empty) {
            empty.hidden = !searching || found > 0;
        }

        this.marked = null;
        this.mark();
    };

    function toArray(list) {
        return Array.prototype.slice.call(list);
    }
})();
