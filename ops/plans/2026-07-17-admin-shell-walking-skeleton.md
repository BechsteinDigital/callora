# Admin-Shell Walking Skeleton — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine lauffähige, colocatete Vue-3-Admin-SPA im Administration-Modul, die per Static-Web-Assets ausgeliefert wird, sich über den Phase-B-Login anmeldet und ein Kontext-Dashboard als Durchstich zeigt.

**Architecture:** Frontend-Quelle liegt colocated unter `src/Administration/Resources/app/administration` (Vue 3 + Vite + TS). Der Vite-Build landet in `src/Administration/wwwroot/admin` und wird vom Web-SDK als Static-Web-Asset mit BasePath `admin` gebündelt; Callora-Production erbt die Assets über die Referenz. Der Host serviert `/admin/*` (SPA-Fallback). Auth läuft über die bestehenden `/api/auth/login`- und `/api/admin/context`-Endpunkte (Cookie-Session).

**Tech Stack:** Vue 3.5, Vite, TypeScript 5, vue-router 4, radix-vue (Reka UI), SCSS; Vitest + @vue/test-utils; .NET 10 Web-SDK Static-Web-Assets; xUnit (Backend-Serving-Test).

---

## File Structure

**Frontend (neu, unter `src/Administration/Resources/app/administration/`):**
- `package.json`, `vite.config.ts`, `tsconfig.json`, `index.html` — Toolchain + Einstieg
- `src/main.ts`, `src/App.vue` — Bootstrap
- `src/core/http.ts` — fetch-Wrapper (credentials: include, 401-Hook)
- `src/core/auth/adminContext.ts` — Typ + Parser für `/api/admin/context`
- `src/core/auth/authStore.ts` — reaktiver Auth-State (login/logout/loadContext)
- `src/core/design/tokens.scss` — CSS-Custom-Properties (White-Label-Achse)
- `src/core/ui/BaseButton.vue`, `src/core/ui/BaseInput.vue` — eigene Primitives (SCSS)
- `src/core/ui/UserMenu.vue` — Radix DropdownMenu (Accessibility-Fall)
- `src/app/router.ts` — Routen + Auth-Guard
- `src/app/AppShell.vue` — Sidebar + Topbar Layout
- `src/modules/auth/LoginView.vue` — Login-Formular
- `src/modules/dashboard/DashboardView.vue` — Durchstich (zeigt Kontext)
- Tests: `src/**/<name>.test.ts` (Vitest, colocated)

**Backend (modifiziert):**
- `src/Administration/Callora.Administration.csproj` — StaticWebAssetBasePath + npm-Build-Target
- `src/Administration/CalloraAdministrationExtensions.cs` — SPA-Fallback-Mapping
- `tests/Callora.Core.Tests/Api/AdminSpaServingTests.cs` — Serving-Integrationstest (neu)

---

## Task 1: Frontend-Projekt-Gerüst

**Files:**
- Create: `src/Administration/Resources/app/administration/package.json`
- Create: `src/Administration/Resources/app/administration/tsconfig.json`
- Create: `src/Administration/Resources/app/administration/vite.config.ts`
- Create: `src/Administration/Resources/app/administration/index.html`
- Create: `src/Administration/Resources/app/administration/src/main.ts`
- Create: `src/Administration/Resources/app/administration/src/App.vue`
- Create: `src/Administration/Resources/app/administration/.gitignore`

- [ ] **Step 1: package.json**

```json
{
  "name": "@callora/administration-shell",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vue-tsc -b && vite build",
    "test": "vitest run",
    "test:watch": "vitest"
  },
  "dependencies": {
    "vue": "^3.5.0",
    "vue-router": "^4.4.0",
    "radix-vue": "^1.9.0"
  },
  "devDependencies": {
    "@vitejs/plugin-vue": "^5.1.0",
    "@vue/test-utils": "^2.4.6",
    "happy-dom": "^15.0.0",
    "sass": "^1.80.0",
    "typescript": "^5.6.0",
    "vite": "^6.0.0",
    "vitest": "^2.1.0",
    "vue-tsc": "^2.1.0"
  }
}
```

Verifikations-Notiz: exakte Minor-Versionen beim Bau gegen die Registry prüfen; die Ranges sind bewusst großzügig. Falls `radix-vue` bereits als `reka-ui` publiziert ist, den Paketnamen anpassen (API identisch).

- [ ] **Step 2: tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "jsx": "preserve",
    "resolveJsonModule": true,
    "esModuleInterop": true,
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["vitest/globals"],
    "paths": { "@/*": ["./src/*"] },
    "baseUrl": "."
  },
  "include": ["src/**/*.ts", "src/**/*.vue"]
}
```

- [ ] **Step 3: vite.config.ts** — Build nach `../../wwwroot/admin`, base `/admin/`, Dev-Proxy, Vitest.

```ts
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  base: '/admin/',
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: {
    outDir: fileURLToPath(new URL('../../wwwroot/admin', import.meta.url)),
    emptyOutDir: true,
  },
  server: {
    port: 5273,
    proxy: {
      '/api': 'http://localhost:5000',
      '/workspace': 'http://localhost:5000',
    },
  },
  test: { environment: 'happy-dom', globals: true },
})
```

Verifikations-Notiz: den Dev-Proxy-Zielport (`5000`) beim Bau gegen den tatsächlichen Host-Port prüfen (siehe Memory: Frontdoor 8080 / Shells 0.0.0.0).

- [ ] **Step 4: index.html**

```html
<!doctype html>
<html lang="de">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Callora Administration</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 5: src/App.vue und src/main.ts (minimal, Router folgt in Task 6)**

`src/App.vue`:
```vue
<template>
  <router-view />
</template>
```

`src/main.ts`:
```ts
import { createApp } from 'vue'
import App from './App.vue'
import '@/core/design/tokens.scss'
import { router } from '@/app/router'

createApp(App).use(router).mount('#app')
```

Hinweis: `router` und `tokens.scss` entstehen in Task 4/6 — dieser Import kompiliert erst, wenn die Dateien existieren. Reihenfolge beim Ausführen beachten oder main.ts erst am Ende auf den finalen Stand bringen. Für Step 6 (Smoke-Build) main.ts vorübergehend ohne Router/SCSS-Import halten.

- [ ] **Step 6: .gitignore + Smoke-Install/Build**

`.gitignore`:
```
node_modules/
```

Run:
```bash
cd src/Administration/Resources/app/administration && npm install && npm run build
```
Expected: `src/Administration/wwwroot/admin/index.html` + gehashte Assets entstehen.

- [ ] **Step 7: Commit**

```bash
git add src/Administration/Resources/app/administration/ src/Administration/wwwroot/.gitignore 2>/dev/null; \
git add src/Administration/Resources/app/administration/
git commit -m "chore(admin-shell): scaffold colocated Vue 3 + Vite frontend"
```

Hinweis zum Build-Output: `wwwroot/admin/` ist generiert. Entscheidung beim Bau: entweder `wwwroot/admin/` per `.gitignore` ausschließen (Build erzeugt es) — empfohlen — oder einchecken. Empfohlen: ignorieren, da das MSBuild-Target (Task 2) es bei jedem Build erzeugt.

---

## Task 2: MSBuild-Integration + Static-Web-Assets

**Files:**
- Modify: `src/Administration/Callora.Administration.csproj`

- [ ] **Step 1: StaticWebAssetBasePath + Frontend-Build-Target hinzufügen**

In `Callora.Administration.csproj` in die `<PropertyGroup>` aufnehmen:
```xml
<StaticWebAssetBasePath>admin</StaticWebAssetBasePath>
```

Und ein neues Target (vor dem C#-Build) ergänzen:
```xml
<PropertyGroup>
  <AdminFrontendDir>$(MSBuildProjectDirectory)/Resources/app/administration</AdminFrontendDir>
</PropertyGroup>

<Target Name="BuildAdminFrontend"
        BeforeTargets="AssignTargetPaths"
        Condition="'$(SkipAdminFrontend)' != 'true'"
        Inputs="$(AdminFrontendDir)/package.json"
        Outputs="$(MSBuildProjectDirectory)/wwwroot/admin/index.html">
  <Message Importance="high" Text="Building admin shell frontend…" />
  <Exec Command="npm ci" WorkingDirectory="$(AdminFrontendDir)" />
  <Exec Command="npm run build" WorkingDirectory="$(AdminFrontendDir)" />
</Target>
```

Verifikations-Notiz beim Bau: (a) das `Inputs/Outputs`-Incrementality-Paar ggf. erweitern (nur package.json als Input triggert nicht bei src-Änderungen — für zuverlässige Rebuilds evtl. `Inputs="@(AdminFrontendSource)"` mit einem Glob über `Resources/app/administration/src/**`); (b) `AssignTargetPaths` ist der Punkt, an dem das Web-SDK Static-Web-Assets einsammelt — falls die Assets nicht gebündelt werden, das Target auf `BeforeTargets="ResolveStaticWebAssetsInputs"` umstellen.

- [ ] **Step 2: Build mit übersprungenem Frontend verifizieren**

Run:
```bash
dotnet build src/Administration/Callora.Administration.csproj -p:SkipAdminFrontend=true 2>&1 | tail -3
```
Expected: 0 Fehler (kein Node nötig).

- [ ] **Step 3: Build mit Frontend verifizieren**

Run:
```bash
dotnet build src/Administration/Callora.Administration.csproj 2>&1 | tail -5
```
Expected: „Building admin shell frontend…" erscheint, 0 Fehler, `wwwroot/admin/index.html` aktuell.

- [ ] **Step 4: Commit**

```bash
git add src/Administration/Callora.Administration.csproj
git commit -m "build(admin-shell): bundle Vite output as static web assets under /admin"
```

---

## Task 3: Backend SPA-Serving + Fallback (TDD)

**Files:**
- Test: `tests/Callora.Core.Tests/Api/AdminSpaServingTests.cs`
- Modify: `src/Administration/CalloraAdministrationExtensions.cs`

- [ ] **Step 1: Failing test — Host liefert /admin/index.html**

Neue Datei `tests/Callora.Core.Tests/Api/AdminSpaServingTests.cs`. Der Test baut einen minimalen Host mit einem temporären WebRoot, legt eine `admin/index.html` an, mappt den Fallback und prüft, dass eine SPA-Route `/admin/settings` die index.html zurückgibt.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Api;

public sealed class AdminSpaServingTests
{
    [Fact]
    public async Task AdminDeepLink_ReturnsSpaIndexHtml()
    {
        var webRoot = Directory.CreateTempSubdirectory("callora-admin-spa");
        var adminDir = Directory.CreateDirectory(Path.Combine(webRoot.FullName, "admin"));
        await File.WriteAllTextAsync(Path.Combine(adminDir.FullName, "index.html"), "<!doctype html><title>callora-admin</title>");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot.FullName });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseStaticFiles();
        app.MapAdminSpaFallback();
        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/admin/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("callora-admin", body);

        await app.StopAsync();
        webRoot.Delete(recursive: true);
    }
}
```

- [ ] **Step 2: Test ausführen — schlägt fehl (MapAdminSpaFallback fehlt)**

Run:
```bash
dotnet test tests/Callora.Core.Tests/Callora.Core.Tests.csproj --filter "FullyQualifiedName~AdminSpaServingTests" 2>&1 | tail -5
```
Expected: FAIL (Compile-Fehler `MapAdminSpaFallback` nicht gefunden).

- [ ] **Step 3: MapAdminSpaFallback implementieren**

In `src/Administration/CalloraAdministrationExtensions.cs` eine neue Methode ergänzen und im `MapCalloraAdministration` (am Ende, nach den API-Mappings) aufrufen:

```csharp
/// <summary>
/// Serves the admin SPA: any non-API GET under /admin/* that does not match a
/// static file falls back to the SPA entry document so the client-side router
/// handles deep links. /admin does not collide with any reserved API prefix.
/// </summary>
public static IEndpointRouteBuilder MapAdminSpaFallback(this IEndpointRouteBuilder endpoints)
{
    endpoints.MapFallbackToFile("/admin/{**path}", "admin/index.html");
    return endpoints;
}
```

Im `MapCalloraAdministration(this WebApplication app)` ganz am Ende ergänzen:
```csharp
app.MapAdminSpaFallback();
```

Verifikations-Notiz (der bekannte Caveat aus der Spec): `MapFallbackToFile` löst gegen den physischen WebRoot auf. Im Test funktioniert das (echte Datei im WebRoot). In der echten Distribution kommt die index.html als **Static-Web-Asset** (nicht als physische WebRoot-Datei) — dort ggf. auf einen Custom-Fallback umstellen, der den Static-Web-Asset-Content ausliefert (z. B. via `IWebHostEnvironment.WebRootFileProvider.GetFileInfo("admin/index.html")`, das Static-Web-Assets einschließt, oder `.NET 10 MapStaticAssets` + expliziter Fallback-Endpoint). Der Unit-Test deckt die Route-Logik ab; die SWA-Auflösung wird in Task 8 (Publish-Smoke) end-to-end verifiziert.

- [ ] **Step 4: Test ausführen — grün**

Run:
```bash
dotnet test tests/Callora.Core.Tests/Callora.Core.Tests.csproj --filter "FullyQualifiedName~AdminSpaServingTests" 2>&1 | tail -5
```
Expected: PASS.

- [ ] **Step 5: PublicAPI-Baseline für Administration aktualisieren**

`MapAdminSpaFallback` ist neues public Symbol. Baseline-Pattern:
```bash
dotnet build src/Administration/Callora.Administration.csproj -p:SkipAdminFrontend=true -p:TreatWarningsAsErrors=false 2>&1 | grep -oP "(?<=Symbol ')[^']+(?=' is not part)" | sort -u >> src/Administration/PublicAPI.Unshipped.txt
```
Dann scharfen Build prüfen:
```bash
dotnet build src/Administration/Callora.Administration.csproj -p:SkipAdminFrontend=true 2>&1 | tail -3
```
Expected: 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/Administration/CalloraAdministrationExtensions.cs src/Administration/PublicAPI.Unshipped.txt tests/Callora.Core.Tests/Api/AdminSpaServingTests.cs
git commit -m "feat(admin-shell): serve SPA under /admin with client-routing fallback"
```

---

## Task 4: Design-Tokens + UI-Primitives

**Files:**
- Create: `src/.../src/core/design/tokens.scss`
- Create: `src/.../src/core/ui/BaseButton.vue`
- Create: `src/.../src/core/ui/BaseInput.vue`

- [ ] **Step 1: tokens.scss — White-Label-Achse als CSS-Custom-Properties**

```scss
:root {
  --cal-color-bg: #0f1115;
  --cal-color-surface: #1a1d24;
  --cal-color-text: #e7e9ee;
  --cal-color-muted: #9aa1ad;
  --cal-color-accent: #4c8dff;
  --cal-color-danger: #ff5c5c;
  --cal-radius: 8px;
  --cal-space: 8px;
  --cal-font: system-ui, -apple-system, "Segoe UI", sans-serif;
}

* { box-sizing: border-box; }
body { margin: 0; background: var(--cal-color-bg); color: var(--cal-color-text); font-family: var(--cal-font); }
```

- [ ] **Step 2: BaseButton.vue**

```vue
<template>
  <button class="cal-btn" :type="type"><slot /></button>
</template>

<script setup lang="ts">
defineProps<{ type?: 'button' | 'submit' }>()
</script>

<style scoped lang="scss">
.cal-btn {
  padding: calc(var(--cal-space) * 1.25) calc(var(--cal-space) * 2);
  border: 0;
  border-radius: var(--cal-radius);
  background: var(--cal-color-accent);
  color: #fff;
  font: inherit;
  cursor: pointer;
}
.cal-btn:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
```

- [ ] **Step 3: BaseInput.vue (v-model-fähig)**

```vue
<template>
  <input class="cal-input" :value="modelValue" :type="type"
         @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)" />
</template>

<script setup lang="ts">
defineProps<{ modelValue: string; type?: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<style scoped lang="scss">
.cal-input {
  width: 100%;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}
</style>
```

- [ ] **Step 4: Build-Smoke**

Run: `cd src/Administration/Resources/app/administration && npm run build`
Expected: Build grün.

- [ ] **Step 5: Commit**

```bash
git add src/Administration/Resources/app/administration/src/core/
git commit -m "feat(admin-shell): design tokens + base UI primitives"
```

---

## Task 5: HTTP-Client + Auth-Context (TDD)

**Files:**
- Create: `src/.../src/core/auth/adminContext.ts`
- Create: `src/.../src/core/auth/adminContext.test.ts`
- Create: `src/.../src/core/http.ts`
- Create: `src/.../src/core/auth/authStore.ts`
- Create: `src/.../src/core/auth/authStore.test.ts`

- [ ] **Step 1: Failing test — Context-Parser**

`src/core/auth/adminContext.test.ts`:
```ts
import { describe, it, expect } from 'vitest'
import { parseAdminContext } from './adminContext'

describe('parseAdminContext', () => {
  it('maps the API shape to the store model', () => {
    const ctx = parseAdminContext({
      userId: 'u1', displayName: 'Max', email: 'max@x.de',
      roles: ['workspace-admin'], permissions: ['workspace.read'],
      scope: 'workspace', workspaceKey: 'sales-de', isOperator: false,
    })
    expect(ctx.userId).toBe('u1')
    expect(ctx.isOperator).toBe(false)
    expect(ctx.permissions).toContain('workspace.read')
  })

  it('defaults arrays to empty when absent', () => {
    const ctx = parseAdminContext({ userId: 'u2', isOperator: true } as any)
    expect(ctx.roles).toEqual([])
    expect(ctx.permissions).toEqual([])
  })
})
```

- [ ] **Step 2: Test ausführen — schlägt fehl**

Run: `cd src/Administration/Resources/app/administration && npx vitest run src/core/auth/adminContext.test.ts`
Expected: FAIL (`parseAdminContext` nicht definiert).

- [ ] **Step 3: adminContext.ts implementieren**

```ts
export interface AdminContext {
  userId: string
  displayName: string | null
  email: string | null
  roles: string[]
  permissions: string[]
  scope: string | null
  workspaceKey: string | null
  isOperator: boolean
}

export function parseAdminContext(raw: any): AdminContext {
  return {
    userId: raw.userId,
    displayName: raw.displayName ?? null,
    email: raw.email ?? null,
    roles: raw.roles ?? [],
    permissions: raw.permissions ?? [],
    scope: raw.scope ?? null,
    workspaceKey: raw.workspaceKey ?? null,
    isOperator: raw.isOperator ?? false,
  }
}
```

- [ ] **Step 4: Test grün**

Run: `npx vitest run src/core/auth/adminContext.test.ts`
Expected: PASS.

- [ ] **Step 5: http.ts (fetch-Wrapper) — kein eigener Test, wird via authStore getestet**

```ts
export type UnauthorizedHandler = () => void

let onUnauthorized: UnauthorizedHandler = () => {}
export function setUnauthorizedHandler(h: UnauthorizedHandler) { onUnauthorized = h }

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const res = await fetch(path, { credentials: 'include', ...init })
  if (res.status === 401) onUnauthorized()
  return res
}
```

- [ ] **Step 6: Failing test — authStore login/loadContext**

`src/core/auth/authStore.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAuthStore } from './authStore'

beforeEach(() => { useAuthStore().reset() })

describe('authStore', () => {
  it('loads context after successful login', async () => {
    const store = useAuthStore()
    global.fetch = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 200 })) // login
      .mockResolvedValueOnce(new Response(JSON.stringify({ userId: 'u1', isOperator: true }), { status: 200 })) // context
    const ok = await store.login('root', 'pass', null)
    expect(ok).toBe(true)
    expect(store.context.value?.userId).toBe('u1')
  })

  it('returns false and no context on rejected login', async () => {
    const store = useAuthStore()
    global.fetch = vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 }))
    const ok = await store.login('x', 'y', null)
    expect(ok).toBe(false)
    expect(store.context.value).toBeNull()
  })
})
```

- [ ] **Step 7: Test ausführen — schlägt fehl**

Run: `npx vitest run src/core/auth/authStore.test.ts`
Expected: FAIL (`useAuthStore` nicht definiert).

- [ ] **Step 8: authStore.ts implementieren**

```ts
import { ref } from 'vue'
import { apiFetch } from '@/core/http'
import { parseAdminContext, type AdminContext } from '@/core/auth/adminContext'

const context = ref<AdminContext | null>(null)

async function loadContext(): Promise<boolean> {
  const res = await apiFetch('/api/admin/context')
  if (!res.ok) { context.value = null; return false }
  context.value = parseAdminContext(await res.json())
  return true
}

async function login(login: string, password: string, workspaceKey: string | null): Promise<boolean> {
  const res = await apiFetch('/api/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ login, password, workspaceKey }),
  })
  if (!res.ok) return false
  return loadContext()
}

async function logout(): Promise<void> {
  await apiFetch('/api/auth/logout', { method: 'POST' })
  context.value = null
}

function reset() { context.value = null }

export function useAuthStore() {
  return { context, login, logout, loadContext, reset }
}
```

- [ ] **Step 9: Alle Auth-Tests grün**

Run: `npx vitest run src/core/auth/`
Expected: PASS (4 Tests).

- [ ] **Step 10: Commit**

```bash
git add src/Administration/Resources/app/administration/src/core/
git commit -m "feat(admin-shell): http client + auth store against phase-B endpoints"
```

---

## Task 6: Router + Auth-Guard (TDD für die Guard-Logik)

**Files:**
- Create: `src/.../src/app/routeGuard.ts`
- Create: `src/.../src/app/routeGuard.test.ts`
- Create: `src/.../src/app/router.ts`

- [ ] **Step 1: Failing test — Guard leitet ohne Kontext auf /login**

`src/app/routeGuard.test.ts`:
```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { authGuard } from './routeGuard'
import { useAuthStore } from '@/core/auth/authStore'

beforeEach(() => useAuthStore().reset())

describe('authGuard', () => {
  it('redirects to /login when no context and route is protected', () => {
    const result = authGuard({ path: '/', meta: {} } as any)
    expect(result).toBe('/login')
  })

  it('allows the login route through without context', () => {
    const result = authGuard({ path: '/login', meta: { public: true } } as any)
    expect(result).toBe(true)
  })

  it('allows protected routes when a context is present', () => {
    useAuthStore().context.value = { userId: 'u1' } as any
    const result = authGuard({ path: '/', meta: {} } as any)
    expect(result).toBe(true)
  })
})
```

- [ ] **Step 2: Test ausführen — schlägt fehl**

Run: `npx vitest run src/app/routeGuard.test.ts`
Expected: FAIL.

- [ ] **Step 3: routeGuard.ts implementieren**

```ts
import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'

export function authGuard(to: RouteLocationNormalized): true | string {
  if (to.meta?.public) return true
  return useAuthStore().context.value ? true : '/login'
}
```

- [ ] **Step 4: Test grün**

Run: `npx vitest run src/app/routeGuard.test.ts`
Expected: PASS.

- [ ] **Step 5: router.ts (History-Mode base /admin, Guard + 401-Hook verdrahten)**

```ts
import { createRouter, createWebHistory } from 'vue-router'
import { authGuard } from '@/app/routeGuard'
import { setUnauthorizedHandler } from '@/core/http'

export const router = createRouter({
  history: createWebHistory('/admin/'),
  routes: [
    { path: '/login', component: () => import('@/modules/auth/LoginView.vue'), meta: { public: true } },
    { path: '/', component: () => import('@/app/AppShell.vue'), children: [
      { path: '', component: () => import('@/modules/dashboard/DashboardView.vue') },
    ] },
  ],
})

router.beforeEach((to) => authGuard(to))
setUnauthorizedHandler(() => router.push('/login'))
```

- [ ] **Step 6: Commit**

```bash
git add src/Administration/Resources/app/administration/src/app/
git commit -m "feat(admin-shell): router with auth guard and 401 redirect"
```

---

## Task 7: Login-Screen (Component-Test)

**Files:**
- Create: `src/.../src/modules/auth/LoginView.vue`
- Create: `src/.../src/modules/auth/LoginView.test.ts`

- [ ] **Step 1: Failing component test — Submit ruft login auf**

`src/modules/auth/LoginView.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import LoginView from './LoginView.vue'
import { useAuthStore } from '@/core/auth/authStore'

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))
beforeEach(() => useAuthStore().reset())

describe('LoginView', () => {
  it('calls authStore.login with the entered credentials on submit', async () => {
    const spy = vi.spyOn(useAuthStore(), 'login').mockResolvedValue(true)
    const wrapper = mount(LoginView)
    await wrapper.find('input[name="login"]').setValue('root')
    await wrapper.find('input[name="password"]').setValue('pass')
    await wrapper.find('form').trigger('submit.prevent')
    expect(spy).toHaveBeenCalledWith('root', 'pass', null)
  })
})
```

- [ ] **Step 2: Test ausführen — schlägt fehl**

Run: `npx vitest run src/modules/auth/LoginView.test.ts`
Expected: FAIL (LoginView existiert nicht).

- [ ] **Step 3: LoginView.vue implementieren**

```vue
<template>
  <form class="login" @submit.prevent="onSubmit">
    <h1>Callora Administration</h1>
    <label>Login <input name="login" v-model="loginName" /></label>
    <label>Passwort <input name="password" type="password" v-model="password" /></label>
    <label>Workspace (optional) <input name="workspaceKey" v-model="workspaceKey" /></label>
    <p v-if="error" class="error">Anmeldung fehlgeschlagen.</p>
    <BaseButton type="submit">Anmelden</BaseButton>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import BaseButton from '@/core/ui/BaseButton.vue'

const loginName = ref('')
const password = ref('')
const workspaceKey = ref('')
const error = ref(false)
const router = useRouter()

async function onSubmit() {
  error.value = false
  const ok = await useAuthStore().login(loginName.value, password.value, workspaceKey.value || null)
  if (ok) router.push('/')
  else error.value = true
}
</script>

<style scoped lang="scss">
.login { max-width: 360px; margin: 10vh auto; display: flex; flex-direction: column; gap: var(--cal-space); }
.login label { display: flex; flex-direction: column; gap: 4px; color: var(--cal-color-muted); }
.error { color: var(--cal-color-danger); }
</style>
```

- [ ] **Step 4: Test grün**

Run: `npx vitest run src/modules/auth/LoginView.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Administration/Resources/app/administration/src/modules/auth/
git commit -m "feat(admin-shell): login view against unified admin login"
```

---

## Task 8: App-Shell + Dashboard-Durchstich + End-to-End-Verifikation

**Files:**
- Create: `src/.../src/core/ui/UserMenu.vue`
- Create: `src/.../src/app/AppShell.vue`
- Create: `src/.../src/modules/dashboard/DashboardView.vue`
- Create: `src/.../src/modules/dashboard/DashboardView.test.ts`

- [ ] **Step 1: Failing test — Dashboard zeigt den Kontext**

`src/modules/dashboard/DashboardView.test.ts`:
```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import DashboardView from './DashboardView.vue'
import { useAuthStore } from '@/core/auth/authStore'

beforeEach(() => useAuthStore().reset())

describe('DashboardView', () => {
  it('renders identity, scope and permissions from the context', () => {
    useAuthStore().context.value = {
      userId: 'root', displayName: 'Root', email: null, roles: ['superadmin'],
      permissions: [], scope: 'platform', workspaceKey: null, isOperator: true,
    }
    const wrapper = mount(DashboardView)
    expect(wrapper.text()).toContain('root')
    expect(wrapper.text()).toContain('platform')
  })
})
```

- [ ] **Step 2: Test ausführen — schlägt fehl**

Run: `npx vitest run src/modules/dashboard/DashboardView.test.ts`
Expected: FAIL.

- [ ] **Step 3: DashboardView.vue implementieren**

```vue
<template>
  <section class="dashboard">
    <h1>Übersicht</h1>
    <dl v-if="ctx">
      <dt>Benutzer</dt><dd>{{ ctx.displayName ?? ctx.userId }} ({{ ctx.userId }})</dd>
      <dt>Scope</dt><dd>{{ ctx.scope ?? '—' }}{{ ctx.workspaceKey ? ` / ${ctx.workspaceKey}` : '' }}</dd>
      <dt>Rollen</dt><dd>{{ ctx.roles.join(', ') || '—' }}</dd>
      <dt>Operator</dt><dd>{{ ctx.isOperator ? 'ja' : 'nein' }}</dd>
      <dt>Permissions</dt><dd>{{ ctx.permissions.length }}</dd>
    </dl>
  </section>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/core/auth/authStore'
const ctx = useAuthStore().context
</script>

<style scoped lang="scss">
.dashboard { padding: calc(var(--cal-space) * 3); }
dl { display: grid; grid-template-columns: auto 1fr; gap: var(--cal-space) calc(var(--cal-space) * 2); }
dt { color: var(--cal-color-muted); }
</style>
```

- [ ] **Step 4: Test grün**

Run: `npx vitest run src/modules/dashboard/DashboardView.test.ts`
Expected: PASS.

- [ ] **Step 5: UserMenu.vue (Radix DropdownMenu) + AppShell.vue**

`src/core/ui/UserMenu.vue` — Radix DropdownMenu mit Logout (etabliert das Radix-Muster; Accessibility geschenkt):
```vue
<template>
  <DropdownMenuRoot>
    <DropdownMenuTrigger class="user-trigger">{{ label }}</DropdownMenuTrigger>
    <DropdownMenuPortal>
      <DropdownMenuContent class="user-menu">
        <DropdownMenuItem @select="onLogout">Abmelden</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenuPortal>
  </DropdownMenuRoot>
</template>

<script setup lang="ts">
import { DropdownMenuRoot, DropdownMenuTrigger, DropdownMenuPortal, DropdownMenuContent, DropdownMenuItem } from 'radix-vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'

defineProps<{ label: string }>()
const router = useRouter()
async function onLogout() { await useAuthStore().logout(); router.push('/login') }
</script>

<style scoped lang="scss">
.user-trigger { background: transparent; border: 1px solid var(--cal-color-muted); color: var(--cal-color-text); border-radius: var(--cal-radius); padding: 6px 12px; cursor: pointer; }
.user-menu { background: var(--cal-color-surface); border: 1px solid var(--cal-color-muted); border-radius: var(--cal-radius); padding: 4px; }
</style>
```

Verifikations-Notiz: die exakten Radix-Vue-Komponenten-Namen/Imports beim Bau gegen die installierte Version prüfen (bei `reka-ui` identische Komponenten, anderer Paketname).

`src/app/AppShell.vue` — Sidebar + Topbar + `<router-view>`, lädt beim Mount den Kontext nach (Reload-Fähigkeit):
```vue
<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">Callora</div>
      <nav><RouterLink to="/">Übersicht</RouterLink></nav>
    </aside>
    <div class="main">
      <header class="topbar">
        <UserMenu :label="ctx?.displayName ?? ctx?.userId ?? 'Konto'" />
      </header>
      <main><RouterView /></main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import UserMenu from '@/core/ui/UserMenu.vue'

const store = useAuthStore()
const ctx = store.context
const router = useRouter()

onMounted(async () => {
  if (!ctx.value) {
    const ok = await store.loadContext()
    if (!ok) router.push('/login')
  }
})
</script>

<style scoped lang="scss">
.shell { display: grid; grid-template-columns: 220px 1fr; min-height: 100vh; }
.sidebar { background: var(--cal-color-surface); padding: calc(var(--cal-space) * 2); }
.brand { font-weight: 700; margin-bottom: calc(var(--cal-space) * 2); }
.sidebar nav a { color: var(--cal-color-text); text-decoration: none; display: block; padding: var(--cal-space) 0; }
.topbar { display: flex; justify-content: flex-end; padding: var(--cal-space) calc(var(--cal-space) * 2); border-bottom: 1px solid var(--cal-color-surface); }
</style>
```

- [ ] **Step 6: main.ts finalisieren (Router + Tokens aktiv)**

Sicherstellen, dass `src/main.ts` den finalen Stand aus Task 1/Step 5 hat (Router + tokens.scss importiert).

- [ ] **Step 7: Volle Frontend-Suite + Build**

Run:
```bash
cd src/Administration/Resources/app/administration && npx vitest run && npm run build
```
Expected: alle Vitest-Tests grün; Build erzeugt `wwwroot/admin`.

- [ ] **Step 8: End-to-End-Serving-Verifikation (der Distributions-Durchstich)**

Run (Host starten und /admin abrufen — genaue Startzeile beim Bau an die Repo-Konventionen anpassen, siehe Memory Dev-Stack):
```bash
dotnet build src/Administration/Callora.Administration.csproj 2>&1 | tail -3
# Host starten (Callora-Production oder Host.Cli), dann:
curl -sS -o /dev/null -w "%{http_code}\n" http://localhost:5000/admin/
```
Expected: `200` und ausgelieferte SPA. **Falls hier der SWA-Fallback-Caveat greift** (index.html kommt als Static-Web-Asset, nicht physisch): `MapAdminSpaFallback` (Task 3) auf einen Custom-Fallback umstellen, der `env.WebRootFileProvider.GetFileInfo("admin/index.html")` ausliefert; Unit-Test bleibt gültig. Dieses Ergebnis dokumentieren.

- [ ] **Step 9: Publish-Smoke (Callora-Production erbt die Assets)**

Run:
```bash
dotnet publish ../Callora-Production/Callora.Production.csproj -o /tmp/callora-pub 2>&1 | tail -3
find /tmp/callora-pub/wwwroot/admin -name index.html
```
Expected: `index.html` liegt im Publish-Output — bestätigt, dass Callora-Production die Assets ohne eigenes Ablegen erbt.

- [ ] **Step 10: Commit**

```bash
git add src/Administration/Resources/app/administration/src/
git commit -m "feat(admin-shell): app shell + context dashboard walking-skeleton"
```

---

## Definition of Done (gegen die Spec-Erfolgskriterien)

1. `dotnet build` baut das Frontend mit; `-p:SkipAdminFrontend=true` überspringt es (Task 2).
2. Host liefert `/admin`; unangemeldet → `/admin/login` (Task 3 + 6 + 8).
3. Login über `/api/auth/login` → Dashboard zeigt echten `/api/admin/context` (Task 5, 7, 8).
4. Callora-Production erbt die Assets ohne eigenes Ablegen — Publish-Smoke (Task 8/Step 9).
5. Vitest-Suite + Backend-Serving-Integrationstest grün (Task 3, 5, 6, 7, 8).

## Bewusste Verifikations-Punkte beim Bau (kein Platzhalter, sondern Realität)

- **SWA-Fallback:** `MapFallbackToFile` vs. Custom-Fallback für Static-Web-Assets (Task 3/8) — end-to-end in Task 8 entschieden.
- **MSBuild-Incrementality:** Input-Glob für zuverlässige Rebuilds (Task 2).
- **Paketnamen/Versionen:** `radix-vue`↔`reka-ui`, Vite/Vitest-Minor (Task 1, 8).
- **Dev-Proxy-Port + Host-Startzeile:** gegen die Repo-Dev-Konventionen (Task 1, 8).
```
