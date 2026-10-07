# Pantrix Browser: the Chromium that Pantrix drives to look aisles up on store websites.
# Built for linux/amd64 and linux/arm64 by .github/workflows/browser.yml.
#
# It is linuxserver's Chromium container (a real browser with a screen you can open from another computer, to
# choose your store, sign in, or pass a human check) plus the one thing Pantrix needs: Chromium's remote-control
# port made reachable from another container.
FROM lscr.io/linuxserver/chromium:latest
ARG VERSION=0.0.0-dev

LABEL org.opencontainers.image.title="Pantrix Browser" \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.source="https://github.com/007darkmatter5/pantrix"

RUN if command -v apt-get >/dev/null; then \
        apt-get update && apt-get install -y --no-install-recommends socat && rm -rf /var/lib/apt/lists/*; \
    else \
        apk add --no-cache socat; \
    fi

# The forwarder service (see root/etc/s6-overlay/s6-rc.d/svc-pantrix-forward).
COPY --chmod=755 root/ /

# Chromium takes remote control on 9222, on its own loopback address only, and only when given a profile folder
# other than its default. The forwarder offers that port to other containers as 9223.
ENV CHROME_CLI="--remote-debugging-port=9222 --user-data-dir=/config/pantrix-profile" \
    TITLE="Pantrix Browser"

# 3000/3001: the browser's screen (http/https). 9223: remote control, for Pantrix only; don't publish it.
EXPOSE 3000 3001 9223
