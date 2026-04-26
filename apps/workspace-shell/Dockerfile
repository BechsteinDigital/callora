FROM node:22-alpine AS build
WORKDIR /workspace

RUN apk add --no-cache bash

COPY custom/plugins ./custom/plugins
COPY scripts ./scripts
COPY apps/admin-shell/package.json ./apps/admin-shell/package.json
COPY apps/admin-shell/package-lock.json ./apps/admin-shell/package-lock.json
COPY apps/workspace-shell ./apps/workspace-shell

RUN if [ -f apps/admin-shell/package-lock.json ]; then npm ci --prefix apps/admin-shell; else npm install --prefix apps/admin-shell; fi
RUN if [ -f apps/workspace-shell/package-lock.json ]; then npm ci --prefix apps/workspace-shell; else npm install --prefix apps/workspace-shell; fi
RUN ./scripts/build-plugin-ui-assets.sh
RUN npm run --prefix apps/workspace-shell generate

FROM caddy:2.8-alpine AS runtime
WORKDIR /srv
COPY ops/shell-runtime/Caddyfile.static /etc/caddy/Caddyfile
COPY --from=build /workspace/apps/workspace-shell/.output/public /srv

EXPOSE 80
