#!/usr/bin/env bash
#
# Sets up and updates The War Room on its server. It runs on the server itself, as root,
# and is called by the "deploy" job in .github/workflows/ci.yml:
#
#   server.sh prepare             installs what the site needs, if it is not there yet, and makes its folders
#   server.sh activate RELEASE    switches to the release that was just uploaded and checks that it answers;
#                                 when it does not, the release before it is put back
#
# Every step first looks at what is already there, so running it again is always safe.
#
# What ends up where:
#   /opt/thewarroom/releases/...  every release that was uploaded (the newest three are kept)
#   /opt/thewarroom/current       points at the release in use
#   /var/lib/thewarroom           the database, the keys of the sign-in cookies, and film when it is not kept in S3
#   /etc/thewarroom               the settings of the two programs
#
# The two programs run as the user "thewarroom" under systemd:
#   thewarroom-api    reachable from this server only, on port 5180
#   thewarroom-site   on port 5228, behind Caddy, which answers the internet over HTTPS

set -euo pipefail

# THEWARROOM_TEST_ROOT is only set when this script is tried out away from a server: everything is then written under that folder
ROOT="${THEWARROOM_TEST_ROOT:-}"
APP_DIR="$ROOT/opt/thewarroom"
DATA_DIR="$ROOT/var/lib/thewarroom"
CONFIG_DIR="$ROOT/etc/thewarroom"
SYSTEMD_DIR="$ROOT/etc/systemd/system"
CADDY_FILE="$ROOT/etc/caddy/Caddyfile"

# Where the deploy job put this script, the files beside it, and the settings
UPLOAD_DIR="$(cd "$(dirname "$0")" && pwd)"

SERVICE_USER=thewarroom
API_PORT=5180
SITE_PORT=5228
KEEP_RELEASES=3
DEMO_HEAD_COACH=coach.bennett@lakeshorelions.example

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

# ---------- prepare ----------

prepare() {
    install_what_is_missing
    add_swap_on_a_small_server
    show_memory

    id -u "$SERVICE_USER" >/dev/null 2>&1 \
        || useradd --system --home-dir "$DATA_DIR" --shell /usr/sbin/nologin "$SERVICE_USER"

    mkdir -p "$APP_DIR/releases" "$DATA_DIR/keys" "$DATA_DIR/film-storage" "$CONFIG_DIR"

    # Whoever deploys uploads the releases. The service user only reads them, and owns the data.
    chown "${SUDO_USER:-root}" "$APP_DIR" "$APP_DIR/releases"
    chown -R "$SERVICE_USER:$SERVICE_USER" "$DATA_DIR"
    chmod 750 "$DATA_DIR"
    chown "root:$SERVICE_USER" "$CONFIG_DIR"
    chmod 750 "$CONFIG_DIR"

    echo "The server is prepared."
}

install_what_is_missing() {
    local missing="" program libraries

    for program in ffmpeg caddy rsync curl python3; do
        command -v "$program" >/dev/null 2>&1 || missing="$missing $program"
    done

    # The two programs bring .NET with them, but .NET uses the system's ICU library for dates and names.
    # The whole list is read before it is searched: a search that stops at the first match cuts ldconfig off,
    # and that would count as a failure here.
    libraries="$(ldconfig -p 2>/dev/null || true)"
    case "$libraries" in
        *libicuuc*) ;;
        *) missing="$missing libicu" ;;
    esac

    if [ -z "$missing" ]; then
        return
    fi

    echo "Installing:$missing"
    export DEBIAN_FRONTEND=noninteractive
    apt_get update -qq

    for program in $missing; do
        if [ "$program" = "libicu" ]; then
            # The package carries its version in its name (libicu74 on Ubuntu 24.04)
            program="$(apt-cache search --names-only '^libicu[0-9]+$' | sort -V | tail -n 1 | cut -d' ' -f1)"
            [ -n "$program" ] || fail "No ICU library package was found for this version of Ubuntu."
        fi
        apt_get install -y -qq "$program"
    done
}

# Ubuntu installs its own updates in the background now and then, and only one installer can work at a time.
# When it is busy, or a download does not get through, this waits and tries again.
apt_get() {
    local attempt

    for attempt in $(seq 1 10); do
        if apt-get -o DPkg::Lock::Timeout=60 "$@"; then
            return 0
        fi
        echo "apt-get $1 did not get through (try $attempt of 10). Trying again in 15 seconds."
        sleep 15
    done

    fail "apt-get $1 kept failing. The lines above say why."
}

# Making a cutup's video needs a few hundred megabytes for a moment.
# On a server with little memory a swap file keeps that from stopping the programs.
add_swap_on_a_small_server() {
    if [ -n "$ROOT" ] || [ -f /swapfile ]; then
        return
    fi

    local memory_kb size=1G
    memory_kb="$(awk '/MemTotal/ {print $2}' /proc/meminfo)"

    # With less than 1 GB of memory the two programs fill it by themselves, so there the swap file is twice as large
    if [ "$memory_kb" -lt 900000 ]; then
        size=2G
    fi

    if [ "$memory_kb" -lt 3000000 ] && [ -z "$(swapon --show --noheadings)" ]; then
        echo "Adding a swap file of $size."
        fallocate -l "$size" /swapfile
        chmod 600 /swapfile
        mkswap /swapfile >/dev/null
        swapon /swapfile
        echo '/swapfile none swap sw 0 0' >> /etc/fstab
    fi
}

# One line for the deploy job's log: the first thing to look at when the server turns out to be short of memory
show_memory() {
    if [ -z "$ROOT" ] && [ -r /proc/meminfo ]; then
        awk '/^MemTotal:/ {memory = $2} /^SwapTotal:/ {swap = $2}
             END {printf "The server has %d MB of memory and %d MB of swap.\n", memory / 1024, swap / 1024}' /proc/meminfo
    fi
}

# ---------- activate ----------

activate() {
    local release="$1"
    local release_dir="$APP_DIR/releases/$release"

    case "$release" in
        *[!A-Za-z0-9._-]*|"") fail "\"$release\" is not the name of a release." ;;
    esac
    if [ ! -x "$release_dir/api/TheWarRoomAPI" ] || [ ! -x "$release_dir/site/TheWarRoomApp" ]; then
        fail "The release $release was not uploaded completely."
    fi

    # The settings that were sent hold passwords: they are removed again however this ends
    trap 'rm -f "$UPLOAD_DIR/settings.env"' EXIT

    read_settings "$UPLOAD_DIR/settings.env"
    write_settings
    write_services
    write_web_server

    local previous=""
    if [ -L "$APP_DIR/current" ]; then
        previous="$(readlink "$APP_DIR/current")"
    fi

    switch_to "releases/$release"

    if start_and_check; then
        echo "Release $release is running."
    else
        echo "Release $release does not answer. This is what the programs wrote last:"
        show_logs

        if [ -n "$previous" ] && [ "$previous" != "releases/$release" ]; then
            echo "Putting $previous back."
            switch_to "$previous"
            start_and_check || echo "The release before it does not answer either."
        fi
        exit 1
    fi

    check_demo_sign_in
    remove_old_releases
}

# Reads the settings the deploy job sent: one NAME=value on each line, the value taken exactly as it is written
read_settings() {
    local file="$1" line name

    SETTING_SITE_DOMAIN=""
    SETTING_SITE_TIMEZONE=""
    SETTING_ALLOW_REGISTRATION=""
    SETTING_DEMO_PASSWORD=""
    SETTING_FILM_S3_BUCKET=""
    SETTING_FILM_S3_REGION=""
    SETTING_AWS_ACCESS_KEY_ID=""
    SETTING_AWS_SECRET_ACCESS_KEY=""

    [ -f "$file" ] || fail "The settings were not sent along."

    while IFS= read -r line || [ -n "$line" ]; do
        name="${line%%=*}"
        case "$name" in
            SITE_DOMAIN|SITE_TIMEZONE|ALLOW_REGISTRATION|DEMO_PASSWORD|FILM_S3_BUCKET|FILM_S3_REGION|AWS_ACCESS_KEY_ID|AWS_SECRET_ACCESS_KEY)
                printf -v "SETTING_$name" '%s' "${line#*=}"
                ;;
        esac
    done < "$file"

    # A space or a line break that came along when a value was pasted into the repository's settings is dropped
    for name in SITE_DOMAIN SITE_TIMEZONE ALLOW_REGISTRATION DEMO_PASSWORD FILM_S3_BUCKET FILM_S3_REGION AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY; do
        name="SETTING_$name"
        printf -v "$name" '%s' "$(without_spaces_around "${!name}")"
    done

    # The address goes into the web server's settings, so it has to look like an address and nothing else
    case "$SETTING_SITE_DOMAIN" in
        ""|*[!A-Za-z0-9.-]*|.*|*.|-*) fail "SITE_DOMAIN has to be the site's address, such as warroom.example.com, without https:// or a slash." ;;
        *.*) ;;
        *) fail "SITE_DOMAIN has to be a full address, such as warroom.example.com." ;;
    esac

    SETTING_SITE_TIMEZONE="${SETTING_SITE_TIMEZONE:-America/Toronto}"
    case "$SETTING_SITE_TIMEZONE" in
        *[!A-Za-z0-9/_+-]*|/*|*..*) fail "SITE_TIMEZONE has to be a time zone name such as America/Toronto." ;;
    esac
    [ -f "/usr/share/zoneinfo/$SETTING_SITE_TIMEZONE" ] || fail "The time zone $SETTING_SITE_TIMEZONE is not known on this server."

    case "${SETTING_ALLOW_REGISTRATION:-false}" in
        true|True|TRUE|yes|on) SETTING_ALLOW_REGISTRATION=true ;;
        *) SETTING_ALLOW_REGISTRATION=false ;;
    esac

    # Every demo account gets this password, and the site is open to the internet
    if [ "${#SETTING_DEMO_PASSWORD}" -lt 8 ]; then
        fail "DEMO_PASSWORD has to be at least 8 characters long."
    fi

    if [ -n "$SETTING_FILM_S3_BUCKET" ]; then
        if [ -z "$SETTING_FILM_S3_REGION" ] || [ -z "$SETTING_AWS_ACCESS_KEY_ID" ] || [ -z "$SETTING_AWS_SECRET_ACCESS_KEY" ]; then
            fail "Film is to be kept in the S3 bucket $SETTING_FILM_S3_BUCKET, but FILM_S3_REGION, AWS_ACCESS_KEY_ID or AWS_SECRET_ACCESS_KEY is missing."
        fi
    fi
}

# The value without the spaces, tabs and line-break characters at its start and at its end
without_spaces_around() {
    local value="$1"

    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    printf '%s' "$value"
}

# One line of a systemd EnvironmentFile. The value goes in double quotes with \ and " escaped,
# the form systemd reads back unchanged whatever characters a password has.
env_line() {
    local value="$2"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    printf '%s="%s"\n' "$1" "$value"
}

# The settings of the two programs. Both read them as environment variables (see the EnvironmentFile lines of the services).
write_settings() {
    {
        echo "# Written by deploy/server.sh at every deploy. Changes made here by hand are lost."
        env_line ASPNETCORE_ENVIRONMENT Production
        env_line ASPNETCORE_URLS "http://127.0.0.1:$API_PORT"
        env_line DOTNET_gcServer 0
        env_line TZ "$SETTING_SITE_TIMEZONE"
        env_line ConnectionStrings__DefaultConnection "Data Source=$DATA_DIR/warroom.db"
        env_line Site__AllowRegistration "$SETTING_ALLOW_REGISTRATION"
        env_line Seed__DemoData true
        env_line Seed__DemoPassword "$SETTING_DEMO_PASSWORD"
        env_line Film__LocalFolder "$DATA_DIR/film-storage"

        if [ -n "$SETTING_FILM_S3_BUCKET" ]; then
            env_line Film__Storage S3
            env_line Film__S3Bucket "$SETTING_FILM_S3_BUCKET"
            env_line Film__S3Region "$SETTING_FILM_S3_REGION"
            env_line AWS_ACCESS_KEY_ID "$SETTING_AWS_ACCESS_KEY_ID"
            env_line AWS_SECRET_ACCESS_KEY "$SETTING_AWS_SECRET_ACCESS_KEY"
        else
            env_line Film__Storage Local
        fi
    } | put_in_place "$CONFIG_DIR/api.env"

    {
        echo "# Written by deploy/server.sh at every deploy. Changes made here by hand are lost."
        env_line ASPNETCORE_ENVIRONMENT Production
        env_line ASPNETCORE_URLS "http://127.0.0.1:$SITE_PORT"
        # Caddy passes on that the visitor came over HTTPS; the site believes it, since only Caddy can reach it
        env_line ASPNETCORE_FORWARDEDHEADERS_ENABLED true
        env_line DOTNET_gcServer 0
        env_line TZ "$SETTING_SITE_TIMEZONE"
        env_line ApiSettings__BaseUrl "http://127.0.0.1:$API_PORT/"
        env_line Site__AllowRegistration "$SETTING_ALLOW_REGISTRATION"
        env_line Site__KeysFolder "$DATA_DIR/keys"
    } | put_in_place "$CONFIG_DIR/site.env"
}

# Writes what it is given to a settings file that only root and the service user can read
put_in_place() {
    local file="$1"

    (umask 077 && cat > "$file.new")
    chown "root:$SERVICE_USER" "$file.new"
    chmod 640 "$file.new"
    mv -f "$file.new" "$file"
}

write_services() {
    cp "$UPLOAD_DIR/thewarroom-api.service" "$UPLOAD_DIR/thewarroom-site.service" "$SYSTEMD_DIR/"
    systemctl daemon-reload
    systemctl enable --quiet thewarroom-api thewarroom-site
}

# Caddy answers the internet for the site's address and passes everything on to the site
write_web_server() {
    local wanted="$UPLOAD_DIR/Caddyfile.ready"

    sed "s/^SITE_DOMAIN /$SETTING_SITE_DOMAIN /" "$UPLOAD_DIR/Caddyfile" > "$wanted"

    if [ -f "$CADDY_FILE" ] && cmp -s "$wanted" "$CADDY_FILE"; then
        return   # nothing changed, so Caddy is left alone
    fi

    # Whatever was there before this script's first run is kept beside it
    if [ -f "$CADDY_FILE" ] && ! grep -q "deploy/server.sh" "$CADDY_FILE" && [ ! -f "$CADDY_FILE.before-thewarroom" ]; then
        cp "$CADDY_FILE" "$CADDY_FILE.before-thewarroom"
    fi

    caddy validate --adapter caddyfile --config "$wanted" >/dev/null 2>&1 \
        || fail "The web server would not accept its settings for $SETTING_SITE_DOMAIN."

    mkdir -p "$(dirname "$CADDY_FILE")"
    cp "$wanted" "$CADDY_FILE"
    systemctl enable --quiet caddy
    systemctl reload caddy 2>/dev/null || systemctl restart caddy
}

switch_to() {
    ln -sfn "$1" "$APP_DIR/current"
}

start_and_check() {
    systemctl restart thewarroom-api
    # 401 is the right answer here: the API is up and asks who is calling
    wait_for "http://127.0.0.1:$API_PORT/api/Auth/me" 401 || return 1

    systemctl restart thewarroom-site
    wait_for "http://127.0.0.1:$SITE_PORT/Account/Login" 200 || return 1
}

# Asks an address until it gives the expected answer: up to 30 times, two seconds apart.
# The first start takes longest, because the database is made and filled then.
wait_for() {
    local url="$1" expected="$2" status="" attempt

    for attempt in $(seq 1 30); do
        status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$url" || true)"
        if [ "$status" = "$expected" ]; then
            return 0
        fi
        sleep 2
    done

    echo "$url answered \"$status\" instead of $expected."
    return 1
}

show_logs() {
    journalctl -u thewarroom-api -n 40 --no-pager || true
    journalctl -u thewarroom-site -n 40 --no-pager || true
}

# Signing in as the demo head coach shows that the demo password arrived here exactly as it was set.
# It is only a note, not a failure: the password may have been changed on the site since the database was first filled.
check_demo_sign_in() {
    local body status

    body="$(DEMO_EMAIL="$DEMO_HEAD_COACH" DEMO_PASSWORD="$SETTING_DEMO_PASSWORD" python3 -c \
        'import json, os; print(json.dumps({"email": os.environ["DEMO_EMAIL"], "password": os.environ["DEMO_PASSWORD"]}))')"

    status="$(printf '%s' "$body" | curl -s -o /dev/null -w '%{http_code}' --max-time 10 \
        -H 'Content-Type: application/json' --data-binary @- "http://127.0.0.1:$API_PORT/api/Auth/login" || true)"

    if [ "$status" = "200" ]; then
        echo "The demo head coach can sign in with DEMO_PASSWORD."
    else
        echo "NOTE: the demo head coach could not sign in with DEMO_PASSWORD (answer $status)."
        echo "      The demo accounts get their password when the database is first filled; changing DEMO_PASSWORD later does not change them."
    fi
}

# The newest releases are kept, so one can be put back; older ones are removed
remove_old_releases() {
    local in_use count=0 name

    in_use="$(basename "$(readlink "$APP_DIR/current")")"

    for name in $(ls -1 "$APP_DIR/releases" | sort -r); do
        count=$((count + 1))
        if [ "$count" -gt "$KEEP_RELEASES" ] && [ "$name" != "$in_use" ]; then
            rm -rf "${APP_DIR:?}/releases/$name"
        fi
    done
}

# ---------- main ----------

if [ -z "$ROOT" ] && [ "$(id -u)" -ne 0 ]; then
    fail "This script has to be run as root: sudo bash $0 $*"
fi

case "${1:-}" in
    prepare)
        prepare
        ;;
    activate)
        [ $# -eq 2 ] || fail "Say which release to switch to: $0 activate RELEASE"
        activate "$2"
        ;;
    *)
        fail "Use: $0 prepare | $0 activate RELEASE"
        ;;
esac
