FROM node:22-alpine AS build
WORKDIR /workspace

RUN apk add --no-cache bash

COPY custom/plugins ./custom/plugins
COPY scripts ./scripts
COPY apps/admin-shell ./apps/admin-shell

RUN if [ -f apps/admin-shell/package-lock.json ]; then npm ci --prefix apps/admin-shell; else npm install --prefix apps/admin-shell; fi
RUN ./scripts/build-plugin-ui-assets.sh
RUN npm run --prefix apps/admin-shell generate

FROM caddy:2.8-alpine AS runtime
WORKDIR /srv
COPY ops/shell-runtime/Caddyfile.static /etc/caddy/Caddyfile
COPY --from=build /workspace/apps/admin-shell/.output/public /srv

EXPOSE 80
