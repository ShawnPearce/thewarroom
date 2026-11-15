// The data of a formation or play diagram, and small helpers for working with it.
// Nothing in this file draws anything or touches the page.
//
// A diagram is measured in yards:
//   x  runs across the field from the centre:  -26.67 (left sideline) to +26.67 (right sideline)
//      on an American field, and -32.5 to +32.5 on the wider Canadian one
//   y  runs up the field from the line of scrimmage: negative is the offence's backfield
//
// {
//   schemaVersion: 1,
//   unit: "Offence",
//   rules: "American"|"Canadian",           the game it is drawn for (set by the server from the playbook)
//   view: { yMin: -12, yMax: 18 },          how much of the field is shown
//   ballX: 0,
//   players: [ { id, team: "own"|"opp", label, shape: "circle"|"square"|"triangle", x, y,
//                paths: [ { kind: "route"|"block"|"motion"|"rush"|"zone"|"man", pts: [[dx, dy], ...] } ] } ],
//   drawings: [ { id, type: "text", x, y, text } ]
// }
//
// A path's points are offsets from its player, so moving the player carries the path along.
// Routes and blocks are the offence's; a defender rushes, drops into a zone or covers a man. Motion is anybody's.
(function () {
    'use strict';

    window.WarRoom = window.WarRoom || {};

    // What differs between the two games a diagram can be drawn for: the width of the field in yards,
    // how many players a side has on it, and how far the hash marks are from the centre of the field.
    // (High school hashes divide an American field in three; on a Canadian field they are 24 yards in from each sideline.)
    var GAMES = {
        American: { fieldWidth: 53.33, playersPerSide: 11, hashX: 8.89 },
        Canadian: { fieldWidth: 65, playersPerSide: 12, hashX: 8.5 }
    };

    var MAX_PATHS_PER_PLAYER = 3;
    var MAX_POINTS_PER_PATH = 12;

    // Turns the JSON text from the server into a diagram object, filling in anything that is missing
    function parse(json) {
        var diagram = {};
        try {
            diagram = JSON.parse(json) || {};
        } catch (error) {
            diagram = {};
        }

        diagram.schemaVersion = 1;
        diagram.unit = diagram.unit || 'Offence';
        diagram.rules = diagram.rules === 'Canadian' ? 'Canadian' : 'American';   // a diagram from before there was a choice is American
        diagram.view = diagram.view || { yMin: -12, yMax: 18 };
        diagram.ballX = diagram.ballX || 0;
        diagram.players = diagram.players || [];
        diagram.drawings = diagram.drawings || [];

        diagram.players.forEach(function (player) {
            player.paths = player.paths || [];
        });

        return diagram;
    }

    function serialize(diagram) {
        return JSON.stringify(diagram);
    }

    // The numbers of the game a diagram is drawn for
    function game(diagram) {
        return GAMES[diagram.rules] || GAMES.American;
    }

    function fieldWidth(diagram) {
        return game(diagram).fieldWidth;
    }

    function halfWidth(diagram) {
        return game(diagram).fieldWidth / 2;
    }

    function maxPlayersPerSide(diagram) {
        return game(diagram).playersPerSide;
    }

    function hashX(diagram) {
        return game(diagram).hashX;
    }

    function clone(diagram) {
        return JSON.parse(JSON.stringify(diagram));
    }

    function findPlayer(diagram, id) {
        for (var i = 0; i < diagram.players.length; i++) {
            if (diagram.players[i].id === id) {
                return diagram.players[i];
            }
        }
        return null;
    }

    function findDrawing(diagram, id) {
        for (var i = 0; i < diagram.drawings.length; i++) {
            if (diagram.drawings[i].id === id) {
                return diagram.drawings[i];
            }
        }
        return null;
    }

    // An id that no player or text in the diagram is using yet: "p1", "p2", ... or "t1", "t2", ...
    function newId(diagram, prefix) {
        var number = 1;
        while (findPlayer(diagram, prefix + number) || findDrawing(diagram, prefix + number)) {
            number++;
        }
        return prefix + number;
    }

    // Positions snap to half a yard, which keeps drawings tidy
    function snap(value) {
        return Math.round(value * 2) / 2;
    }

    // Keeps a point inside the part of the field the diagram shows
    function clampToField(diagram, x, y) {
        var half = halfWidth(diagram);

        return {
            x: Math.max(-half, Math.min(half, x)),
            y: Math.max(diagram.view.yMin, Math.min(diagram.view.yMax, y))
        };
    }

    // Where a player's motion ends, or null when the player has no motion.
    // A route or block is drawn starting from there, because that is where the player is at the snap.
    function motionEnd(player) {
        for (var i = 0; i < player.paths.length; i++) {
            var path = player.paths[i];
            if (path.kind === 'motion' && path.pts.length > 0) {
                var last = path.pts[path.pts.length - 1];
                return { x: player.x + last[0], y: player.y + last[1] };
            }
        }
        return null;
    }

    // The points of a path in field yards, starting with where it begins
    function pathPoints(player, path) {
        var start = { x: player.x, y: player.y };
        if (path.kind !== 'motion') {
            start = motionEnd(player) || start;
        }

        var points = [start];
        path.pts.forEach(function (offset) {
            points.push({ x: player.x + offset[0], y: player.y + offset[1] });
        });
        return points;
    }

    function countPlayers(diagram, team) {
        return diagram.players.filter(function (player) { return player.team === team; }).length;
    }

    // Compares two versions of a diagram player by player.
    //   changed: ids in "after" that are new, moved, relabelled, or whose routes changed
    //   removed: ids in "before" that are no longer in "after"
    function diff(before, after) {
        var result = { changed: [], removed: [] };
        if (!before || !after) {
            return result;
        }

        after.players.forEach(function (player) {
            var earlier = findPlayer(before, player.id);
            if (!earlier || JSON.stringify(earlier) !== JSON.stringify(player)) {
                result.changed.push(player.id);
            }
        });

        before.players.forEach(function (player) {
            if (!findPlayer(after, player.id)) {
                result.removed.push(player.id);
            }
        });

        return result;
    }

    window.WarRoom.DiagramModel = {
        fieldWidth: fieldWidth,
        halfWidth: halfWidth,
        maxPlayersPerSide: maxPlayersPerSide,
        hashX: hashX,
        MAX_PATHS_PER_PLAYER: MAX_PATHS_PER_PLAYER,
        MAX_POINTS_PER_PATH: MAX_POINTS_PER_PATH,
        parse: parse,
        serialize: serialize,
        clone: clone,
        findPlayer: findPlayer,
        findDrawing: findDrawing,
        newId: newId,
        snap: snap,
        clampToField: clampToField,
        pathPoints: pathPoints,
        countPlayers: countPlayers,
        diff: diff
    };
})();
