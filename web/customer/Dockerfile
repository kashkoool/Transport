# Build context = this folder:  docker build -f web/customer/Dockerfile -t tpx-web web/customer
# syntax=docker/dockerfile:1

# ── Stage 1: build the Angular app ───────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app
# Cache deps on the lockfile; --ignore-scripts blocks postinstall supply-chain vectors.
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY . .
RUN npm run build

# ── Stage 2: serve the static SPA with non-root nginx ────────────────────────────
# nginx-unprivileged runs as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
# The application builder emits the browser bundle under dist/<app>/browser.
COPY --from=build /app/dist/customer/browser /usr/share/nginx/html
EXPOSE 8080
# Probe 127.0.0.1 (not localhost): nginx listens on IPv4 only, but wget resolves localhost to
# IPv6 [::1] first and would get connection-refused, failing the check while the app is fine.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/ >/dev/null 2>&1 || exit 1
