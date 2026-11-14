# The War Room

Football team management and film analysis for high school football programs.

PROG30000 Enterprise Application, Fall 2026. Group 9, Shawn Pearce (individual project).

The War Room brings a team's depth charts, playbook, game film, schedule and study time into one place.
Head coaches, coordinators, position coaches and players sign in to their team's private workspace,
and each sees and changes what their role allows.

## What it does

| Area | What a team can do |
|---|---|
| Team and depth charts | Add coaches and players, put them in position groups, and keep a depth chart for every week. A player can be the starter at one position and a backup at another. |
| Schedule and attendance | List practices, games and meetings. Coaches record attendance when the head coach has switched tracking on. |
| Playbook | A playbook for offence, defence and special teams, organised by formation. Coaches draw formations and plays on a field. A change waits for the head coach's approval unless the coach has been given the right to publish, and players keep seeing the last approved version in the meantime. |
| Film | Keep the games a team has film of, its own and its opponents'. Cut a video into plays, tag each play (run or pass, down, distance, team on offence), build cutups of chosen plays in a chosen order, and share them with the whole team, with position groups or with single players. |
| Tendencies | Run and pass shares for a team on offence, by game, down and distance, always with the number of plays they come from. |
| Study time | How long each player was signed in, and how long they watched film and read the playbook, as three separate numbers per week. |

Uploading video, playing it and turning a cutup into one video file are not built yet. See "What is not built yet" below.

## How it is built

One solution with three projects:

| Project | What it is |
|---|---|
| `TheWarRoomAPI` | ASP.NET Core Web API. Controllers call services, and the services hold the business rules and do the Entity Framework Core work against a SQLite database. Entities never leave the API: every request and response is a DTO. |
| `TheWarRoomApp` | ASP.NET Core MVC. The site people use. It has no database: every page gets and saves its data by calling the API through `IHttpClientFactory`. Its `Models` folder holds its own copies of the DTOs. |
| `TheWarRoomAPI.Tests` | xUnit tests of the services and rules, run against an in-memory SQLite database. |

The two web projects do not reference each other. They only talk over HTTP.

### How the API answers

| Situation | Answer |
|---|---|
| A record does not exist, or belongs to another team | 404 Not Found |
| A business rule is broken | 409 Conflict, with the reason as text |
| The user's role does not allow it | 403 Forbidden, with the reason as text |
| Nobody is signed in, or the sign-in has ended | 401 Unauthorized |
| A playbook change was published | 200 OK |
| A playbook change is waiting for approval | 202 Accepted |

### Signing in

People sign in on the MVC site, which keeps a cookie. On every call to the API the site adds two headers:
`X-User-Id` (who is acting) and `X-Session-Token` (which sign-in). The API loads that user fresh from the
database on each request, so a changed role or permission takes effect at once.

The API trusts those headers. It must only be reachable by the MVC site, never from the internet.

### In the browser

The browser talks to the MVC site only. JavaScript is used where a page needs it, as plain files with no build step:

| Folder | What |
|---|---|
| `wwwroot/js/field` | The play designer and the drawing of formations and plays on a canvas |
| `wwwroot/js/charts` | The tendency charts (Chart.js) |
| `wwwroot/js/study` | The study timer on playbook pages |

## Running it

You need the .NET 10 SDK.

Start the API first, then the site, each in its own terminal, from the solution folder:

```
dotnet run --project TheWarRoomAPI --launch-profile http
dotnet run --project TheWarRoomApp --launch-profile http
```

| | Address |
|---|---|
| The site | http://localhost:5228 |
| The API's Swagger page | http://localhost:5180/swagger |

In Rider, the run configuration "The War Room (API + Client)" starts both.

The first start creates `TheWarRoomAPI/warroom.db`, applies the migrations and adds the demo data.
To go back to fresh demo data, stop the API, delete `warroom.db` and start the API again.

In Swagger, use the Authorize button and type a user id (1 is the demo head coach) to try the API as that user.

## Demo data

Two teams are added in Development. "Lakeshore Lions" is a full team: eight coaches, 28 players, depth charts,
a schedule with attendance, a playbook with change requests, four games on film with tagged plays, five cutups,
and two weeks of study time. "Northgate Knights" is a small second team that shows one team can never see another's data.

Every demo account has the same password: the `Seed:DemoPassword` value in `TheWarRoomAPI/appsettings.Development.json`.

| Sign in as | Email | Try |
|---|---|---|
| Head coach | `coach.bennett@lakeshorelions.example` | Everything: approvals, permissions, team settings |
| Offence coordinator | `elena.ruiz@lakeshorelions.example` | Publishing playbook changes without approval, inside the offence only |
| Position coach | `tom.gallagher@lakeshorelions.example` | A change that waits for approval; the study time of his own groups only |
| Position coach | `kenji.sato@lakeshorelions.example` | A coach whose depth chart permission is switched off |
| Player | `tyrese.walker@lakeshorelions.example` | "My film" shared through the Receivers group; the study timer on a playbook page |
| Player | `jalen.carter@lakeshorelions.example` | Film shared with one player directly |
| Player | `jordan.lee@lakeshorelions.example` | A first sign-in with a temporary password |
| Other team | `coach.whitfield@northgateknights.example` | A separate workspace with none of the Lions' data |

The other accounts follow the pattern `firstname.lastname@lakeshorelions.example`, with the names on the Roster and Coaching staff pages.

## Business rules

The rules are in the API's services, and each has tests.

| Rule | Where |
|---|---|
| Every record belongs to one team, and a team only ever reaches its own | Every service filters by the current user's team |
| What each role may do, by unit and assigned position groups | `Services/AccessRules.cs` |
| Depth chart: ranks 1, 2, 3 with no gaps; a player once per position; only active players of the position's group | `DepthChartService` |
| Attendance only while the head coach has it switched on, and only by permitted coaches | `AttendanceService` |
| Playbook changes: approval by default, direct publishing only with the right and inside the coach's playbooks, one waiting change per play, a change made on an older version is refused | `PlaybookService`, `ApprovalService`, `PlaybookChangeApplier` |
| A clip lies inside its video and ends after it starts; a clip used in a cutup cannot be deleted | `ClipService` |
| A cutup plays its plays in the chosen order, each starting where the one before ends; its plays are fixed once it is no longer a draft | `CutupService`, `CutupTimeline` |
| A player reaches only film shared with them, their groups or the whole team | `AccessRules.CanWatchCutup` |
| Run % = runs / (runs + passes) to one decimal, pass % is the rest, the sample size always goes with it, and no plays means "No data" | `TendencyCalculator` |
| Login time runs from a sign-in to its last request and ends after 30 minutes of inactivity | `AuthService`, `ActivityService` |
| Viewing time is credited no faster than the clock runs (two tabs do not count twice), at most a minute per report, with film and playbook kept apart | `StudyTimeService` |

## Tests

```
dotnet test
```

About 250 tests cover the rules above, the permission rules by role, and for every area a check that one team
cannot reach another team's records. One test applies all migrations to an empty database and checks that they
match the entity classes.

## Continuous integration

`.github/workflows/ci.yml` restores, builds and runs the tests on every push and pull request.

## What is not built yet

The film pages work on records: games, videos, plays, tags, cutups and sharing. What needs video storage is still to come:

- uploading a game video and playing it in the browser
- marking plays on the video itself (today a play is added by typing its start and end time)
- drawing arrows and text on a clip
- turning a cutup into one video file, and watching it
- counting film time while a video plays (the timer and its rules are in place and are used on playbook pages)

The demo videos are records without a file. The film time in the demo study data is sample data.

## Libraries

Bootstrap, jQuery and jQuery Validation (from the ASP.NET Core template) and Chart.js, all under the MIT licence.
Their licence files are next to them in `TheWarRoomApp/wwwroot/lib`.
