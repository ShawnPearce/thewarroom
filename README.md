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
| Film | Keep the games a team has film of, its own and its opponents'. Upload a game's video and play it in the browser. Mark plays on the video itself, tag each play (run or pass, down, distance, team on offence) and draw arrows and text on it. Build cutups of chosen plays in a chosen order, have each made into one video with the time between plays gone, and share it with the whole team, with position groups or with single players. |
| Tendencies | Run and pass shares for a team on offence, by game, down and distance, always with the number of plays they come from. |
| Study time | How long each player was signed in, and how long they watched film and read the playbook, as three separate numbers per week. |

## How it is built

One solution with three projects:

| Project | What it is |
|---|---|
| `TheWarRoomAPI` | ASP.NET Core Web API. Controllers call services, and the services hold the business rules and do the Entity Framework Core work against a SQLite database. Entities never leave the API: every request and response is a DTO. It also keeps the video files, and a background worker in it makes cutups into videos with ffmpeg. |
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
| A cutup is in the queue to be made into a video | 202 Accepted |
| A part of a video file was asked for | 206 Partial Content, or 302 Found to a signed link when film is kept in Amazon S3 |

### Signing in

People sign in on the MVC site, which keeps a cookie. On every call to the API the site adds two headers:
`X-User-Id` (who is acting) and `X-Session-Token` (which sign-in). The API loads that user fresh from the
database on each request, so a changed role or permission takes effect at once.

The API trusts those headers. It must only be reachable by the MVC site, never from the internet.
On the live server it listens on the server's own address (127.0.0.1) only.

### In the browser

The browser talks to the MVC site only. The one exception is video in Amazon S3: those files travel straight
between the browser and the bucket, over links the API signs. JavaScript is used where a page needs it, as plain files with no build step:

| Folder | What |
|---|---|
| `wwwroot/js/field` | The play designer and the drawing of formations and plays on a canvas |
| `wwwroot/js/film` | The upload page, the video player, the clip editor, the drawings over the video, and the page that plays a cutup |
| `wwwroot/js/charts` | The tendency charts (Chart.js) |
| `wwwroot/js/study` | The study timer on playbook pages and on film |

## Running it

You need the .NET 10 SDK. For video you also need ffmpeg (with ffprobe) on the computer the API runs on:
`brew install ffmpeg` on a Mac, `sudo apt-get install ffmpeg` on Ubuntu, `winget install ffmpeg` on Windows.
Without it everything else works; the pages that upload video or make a cutup's video say that ffmpeg is missing.

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

## Film storage

Video files are kept apart from the database. The `Film` section of `TheWarRoomAPI/appsettings.json` says where:

| `Film:Storage` | Where the files are | How they travel |
|---|---|---|
| `Local` (the default) | The folder `TheWarRoomAPI/film-storage` (`Film:LocalFolder`). It is not in the repository. | Through the site and the API |
| `S3` | An Amazon S3 bucket (`Film:S3Bucket`, `Film:S3Region`, and optionally `Film:S3Profile`) | Straight between the browser and the bucket, over signed links that stop working after a while |

Either way the database holds only each file's key, a name the API makes up, and a file is handed out only after
the API has checked who is asking: coaches for game video, and for a cutup also the players it is shared with.

To keep film in S3:

1. Make a general purpose bucket that is not public: leave "Block all public access" on and ACLs disabled.
   Keep versioning and Object Lock off, so that a deleted video is really gone, and keep the default encryption (SSE-S3).
   Give it a name without dots, and note its region: the API has to be told the same one.
2. Give the bucket this CORS rule, with the address people open the site at, so their browsers may send files to it.
   While the site runs on your own computer that address is `http://localhost:5228`; several addresses can be listed.

   ```json
   [
     {
       "AllowedOrigins": ["https://your-site.example"],
       "AllowedMethods": ["PUT", "GET", "HEAD"],
       "AllowedHeaders": ["*"],
       "ExposeHeaders": ["ETag"],
       "MaxAgeSeconds": 3000
     }
   ]
   ```

3. Give the API credentials that may read, write and delete objects in that bucket and nothing else.
   An IAM user with no console access and this policy is enough:

   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
         "Resource": "arn:aws:s3:::your-bucket/*"
       },
       {
         "Effect": "Allow",
         "Action": "s3:ListBucket",
         "Resource": "arn:aws:s3:::your-bucket"
       }
     ]
   }
   ```

   The second statement is what lets S3 answer "not found" for a file that has not arrived yet.
   Without it S3 answers "forbidden" instead, and the upload page cannot tell the two apart.

   On your own computer: `aws configure --profile warroom`, then set `Film:S3Profile` to `warroom`.
   On a server: environment variables or the server's own role. Keys never go into this repository.
4. Set the bucket outside the repository, for example with user secrets:

   ```
   dotnet user-secrets init --project TheWarRoomAPI
   dotnet user-secrets set "Film:Storage" "S3" --project TheWarRoomAPI
   dotnet user-secrets set "Film:S3Bucket" "your-bucket" --project TheWarRoomAPI
   dotnet user-secrets set "Film:S3Region" "ca-central-1" --project TheWarRoomAPI
   ```

   On a server the same settings are environment variables: `Film__Storage`, `Film__S3Bucket`, `Film__S3Region`.

## Demo data

Two teams are added in Development. "Lakeshore Lions" is a full team: eight coaches, 28 players, depth charts,
a schedule with attendance, a playbook with change requests, four games on film with tagged plays, five cutups,
and two weeks of study time. "Northgate Knights" is a small second team that shows one team can never see another's data.

The demo games are records without a video file: their plays can be listed, tagged, charted and put in cutups,
but there is nothing to play. To try the film pages, sign in as a coach, open a game under Film, upload any MP4 or MOV
video with H.264 picture, cut a few plays on it, put them in a new cutup and choose "Make the video".

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
| Only MP4 and MOV files with H.264 video are taken, up to 5 GB and six hours; the server looks at the file itself with ffprobe and does not trust what the browser said about it | `VideoRules`, `VideoService` |
| A clip lies inside its video and ends after it starts; a clip used in a cutup cannot be deleted, and neither can a video whose clips are | `ClipService`, `VideoService` |
| A drawing belongs to a moment inside its play: at most five per play, each with up to twelve arrows and texts, shown for one to ten seconds; trimming a play removes the drawings that were cut off | `FilmDrawingsValidator`, `ClipService` |
| A cutup plays its plays in the chosen order, each starting where the one before ends; its plays are fixed once it is no longer a draft | `CutupService`, `CutupTimeline` |
| A cutup's video can be made only when every play comes from a video with a file; cutups are made one at a time, the one that waited longest first; where each play starts is measured on the finished video; turning a cutup back into a draft throws its video away | `CutupService`, `CutupRenderer` |
| A player reaches only film shared with them, their groups or the whole team | `AccessRules.CanWatchCutup` |
| Run % = runs / (runs + passes) to one decimal, pass % is the rest, the sample size always goes with it, and no plays means "No data" | `TendencyCalculator` |
| Login time runs from a sign-in to its last request and ends after 30 minutes of inactivity | `AuthService`, `ActivityService` |
| Viewing time is credited no faster than the clock runs (two tabs do not count twice), at most a minute per report, with film and playbook kept apart; film time counts while the video plays or stands still on a coach's drawing | `StudyTimeService`, `view-tracker.js` |

## Tests

```
dotnet test
```

About 350 tests cover the rules above, the permission rules by role, and for every area a check that one team
cannot reach another team's records. One test applies all migrations to an empty database and checks that they
match the entity classes.

Film storage and ffmpeg are stood in for in most film tests, so they run anywhere. A few tests use the real
ffmpeg and ffprobe on videos that ffmpeg makes itself: they read, cut and join real files, and one takes a video
all the way from upload to a finished cutup. Those are skipped on a computer without ffmpeg.

## Continuous integration

`.github/workflows/ci.yml` restores, builds and runs the tests on every push and pull request.
It installs ffmpeg first, so the tests that use it run there too.

## Deploying

A push to `main` that passes the tests is then put on the live server by the `deploy` job of the same workflow.
The job is skipped until the repository has been told where the server is (the `DEPLOY_HOST` variable below).

### The server

One Ubuntu server, for example an AWS Lightsail or EC2 instance with 1 GB of memory or more:

| What | Where | Reachable from |
|---|---|---|
| Caddy, the web server | Ports 80 and 443. It gets the HTTPS certificate for the site's address by itself and renews it. | The internet |
| The site (`TheWarRoomApp`) | `127.0.0.1:5228`, as the service `thewarroom-site` | Caddy only |
| The API (`TheWarRoomAPI`) | `127.0.0.1:5180`, as the service `thewarroom-api` | The site only |
| The database, the keys of the sign-in cookies | `/var/lib/thewarroom` | The two programs |
| Film | The S3 bucket, or `/var/lib/thewarroom/film-storage` when no bucket is set | |

Nothing is installed by hand. The files in `deploy/` do it: `server.sh` installs ffmpeg and Caddy when they are missing,
writes the settings of the two programs, and puts the two service files and the Caddy settings in place.

### What a deploy does

1. Publishes the API and the site with .NET inside them, for the kind of processor the server has.
2. Uploads them to a new folder under `/opt/thewarroom/releases`. Files that did not change are not sent again.
3. Runs `deploy/server.sh` on the server: it writes the settings, switches `/opt/thewarroom/current` to the new release,
   restarts both programs and asks each whether it answers. If one does not, the release before it is put back and the job fails.
4. Asks the site for its sign-in page from the outside, over HTTPS.

### Setting it up, once

1. Make the server (Ubuntu 24.04), give it a fixed IP address, and open ports 22, 80 and 443.
2. Point the site's address at it with a DNS record of type A.
3. In the repository on GitHub, under Settings, Secrets and variables, Actions, add:

   | Variable | Value |
   |---|---|
   | `DEPLOY_HOST` | The server's IP address |
   | `DEPLOY_KNOWN_HOSTS` | The line `ssh-keyscan -t ed25519 <the IP address>` prints: the server's own key, so the job only ever talks to this server |
   | `SITE_DOMAIN` | The site's address, such as `warroom.example.com` |
   | `FILM_S3_BUCKET`, `FILM_S3_REGION` | The bucket for film and its region. Leave both out to keep film on the server's disk. |
   | `DEPLOY_USER` | Only when the server's user is not `ubuntu` |
   | `SITE_TIMEZONE` | Only when the team is not on Toronto time (`America/Toronto`) |
   | `ALLOW_REGISTRATION` | `true` lets visitors register new teams. Left out, only the demo teams and the accounts their head coaches make exist. |

   | Secret | Value |
   |---|---|
   | `DEPLOY_SSH_KEY` | The private key that can sign in to the server |
   | `DEMO_PASSWORD` | The password of the demo accounts on the live site, at least 8 characters |
   | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | An access key that may use the bucket (only with `FILM_S3_BUCKET`) |

4. Add the site's address, as `https://warroom.example.com`, to the bucket's CORS rule (see "Film storage").

The next push to `main` deploys.

### Looking after it

- What the programs write: `sudo journalctl -u thewarroom-api -n 100` and `sudo journalctl -u thewarroom-site -n 100`.
- The demo teams are added when the database is first made. Changing `DEMO_PASSWORD` later does not change their passwords.
- To keep a copy of the data, copy `/var/lib/thewarroom/warroom.db` while the API is stopped (`sudo systemctl stop thewarroom-api`, then `start`).
- To go back to an earlier release: `sudo ln -sfn releases/<its folder> /opt/thewarroom/current`, then
  `sudo systemctl restart thewarroom-api thewarroom-site`. The newest three releases are kept.

## Limits, and what is not done yet

- A cutup's video has no sound, and is always 1280 x 720 at 30 pictures a second.
- Cutups are made one at a time by the one running copy of the API.
- A video is sent in one piece, so an upload that is interrupted starts again from the beginning.
- The deploy job has been tried out in parts (the server script with stand-in system commands, both programs
  run the way the server runs them), but it has not yet run against a real server.

## Libraries

Bootstrap, jQuery and jQuery Validation (from the ASP.NET Core template) and Chart.js, all under the MIT licence.
Their licence files are next to them in `TheWarRoomApp/wwwroot/lib`.

The API uses the AWS SDK for .NET (`AWSSDK.S3`, Apache 2.0 licence) for S3 storage, and runs ffmpeg as a separate
program that has to be installed on its own. ffmpeg is not part of this repository.
