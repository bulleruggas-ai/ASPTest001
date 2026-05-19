# Exxaro Stack Test — Project Specification

Version: 0.1
Last updated: 2026-05-19
Repository: https://github.com/bulleruggas-ai/ASPTest001

This document is the authoritative specification of the project. It is the
source of truth for specification-driven development: if the code and this
spec diverge, one of them is wrong and must be reconciled.

---

## 1. Overview

A small ASP.NET Core Razor Pages site that exercises a representative slice
of a modern web stack:

- Server-rendered marketing/landing pages with a custom design system
- A contact form that writes to a Supabase Postgres table via the Supabase
  REST API (PostgREST)
- A demo page that fetches a public REST API server-side and renders the
  response
- Optional client-side sign-in with Google via Supabase Auth (no
  user database of our own; auth is purely identity-on-display)
- Docker-based deployment to a Linux host

The project is not production-grade. Its purpose is to demonstrate the
stack end-to-end and provide a reference point for further work.

---

## 2. Technology Stack

| Layer            | Choice                                                     |
|------------------|------------------------------------------------------------|
| Runtime          | .NET 10 (`net10.0`, `Nullable=enable`, `ImplicitUsings=enable`) |
| Web framework    | ASP.NET Core Razor Pages                                   |
| Frontend         | Tailwind CSS via the Play CDN (no Node build step)         |
| Frontend font    | Inter, loaded from Google Fonts                            |
| Client libraries | jQuery + jQuery Unobtrusive Validation (form validation only); `@supabase/supabase-js` v2 (auth) |
| Storage          | Supabase Postgres, accessed over PostgREST                 |
| Auth             | Supabase Auth (Google OAuth, client-side via supabase-js)  |
| External demo API| https://api.restful-api.dev/objects                        |
| Container runtime| Docker; `mcr.microsoft.com/dotnet/sdk:10.0` for build, `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime |

Notable libraries explicitly NOT used: ASP.NET Core Identity, Entity
Framework Core, Microsoft.Identity.Web, the Supabase C# SDK, Bootstrap
(was removed in favor of Tailwind; the static assets remain at
`wwwroot/lib/bootstrap` but are not referenced).

---

## 3. Application Architecture

```
ASPTest001/
├── Program.cs               # Minimal hosting + named HttpClient
├── StartupSite.csproj       # net10.0, UserSecretsId
├── Pages/
│   ├── _ViewImports.cshtml
│   ├── _ViewStart.cshtml
│   ├── Index.cshtml(+.cs)   # Landing page
│   ├── Objects.cshtml(+.cs) # Demo API list
│   ├── Query.cshtml(+.cs)   # Contact form → Supabase
│   ├── Privacy.cshtml(+.cs) # Static
│   ├── Error.cshtml(+.cs)   # Default error page
│   └── Shared/
│       ├── _Layout.cshtml             # Chrome + Tailwind config + auth JS
│       ├── _Layout.cshtml.css         # Intentionally empty
│       └── _ValidationScriptsPartial.cshtml
├── wwwroot/
│   ├── css/site.css         # Minimal overrides
│   ├── js/site.js
│   └── lib/                 # jQuery, jQuery Validation (Bootstrap unused)
├── Dockerfile               # Multi-stage SDK→aspnet
├── docker-compose.yml       # Single 'web' service, port 8080
├── .env.example             # Placeholders only — copied to .env on host
├── .gitignore
├── appsettings.json         # Non-secret config only
├── appsettings.Development.json
└── Properties/launchSettings.json # http profile, port 5097
```

### 3.1 Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("Supabase", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url is not configured.");
    var key = config["Supabase:Key"] ?? throw new InvalidOperationException("Supabase:Key is not configured.");
    client.BaseAddress = new Uri(url.TrimEnd('/') + "/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", key);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
});

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
```

- One named HttpClient `"Supabase"` is registered. Pages that need to call
  Supabase resolve `IHttpClientFactory` and use `.CreateClient("Supabase")`.
  The factory presets the base address (`<SupabaseUrl>/rest/v1/`) and the
  `apikey` + `Authorization` headers.
- For external (non-Supabase) HTTP calls, pages use an unnamed client from
  the same factory and pass the full URL.
- `UseAuthorization` is registered but no authentication scheme is wired.
  Authentication is entirely client-side; the server is anonymous.

---

## 4. Pages and Routes

| Route        | Page model           | Purpose                                         |
|--------------|----------------------|-------------------------------------------------|
| `/`          | `IndexModel`         | Hero + mission + offerings                      |
| `/Objects`   | `ObjectsModel`       | Server-fetch + render the demo REST API         |
| `/Query`     | `QueryModel`         | Contact form, posts to Supabase via REST        |
| `/Privacy`   | `PrivacyModel`       | Static privacy placeholder                      |
| `/Error`     | `ErrorModel`         | Default error page                              |

### 4.1 `/Index` (Home)

`IndexModel` is a pure data carrier — no I/O. Properties:

- `CompanyName: string` = `"Exxaro Stack Test"`
- `Tagline: string`
- `Mission: string`
- `FoundedYear: int` = `2026`
- `Headquarters: string` = `"Pretoria, South Africa"`
- `TeamSize: int` = `7`
- `ContactEmail: string` = `"hello@example.com"`
- `Offerings: (string Title, string Body, string Icon)[]` — three entries.

The view renders a hero glass card, a two-column "About" section with the
properties list, and a grid of three offering cards.

### 4.2 `/Objects`

`ObjectsModel.OnGetAsync` fetches `https://api.restful-api.dev/objects`
server-side via `IHttpClientFactory.CreateClient()` and
`GetFromJsonAsync<List<ApiObject>>`. Failures are caught, logged, and
surface as `ErrorMessage`. The view renders:

- An error alert if the call failed
- An empty-state card if the list is empty
- A 2-column grid of glass cards otherwise, each showing the name, a
  `#<id>` pill, and a `<dl>` of the variable `data` dictionary

`ApiObject` shape:

```csharp
public class ApiObject
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, JsonElement>? Data { get; set; }
}
```

Deserialization uses `PropertyNameCaseInsensitive = true`.

### 4.3 `/Query` (Contact form)

`QueryModel.OnGet` is empty. `OnPostAsync`:

1. Validates `ModelState`. Failures re-render the page.
2. Builds a JSON payload with `name`, `email`, `subject`, `message`.
3. POSTs to the `queries` PostgREST endpoint via the `"Supabase"` named
   HttpClient with header `Prefer: return=minimal`.
4. On non-2xx, logs the response body and sets `ErrorMessage`.
5. On success, sets `SuccessMessage` (TempData) and `RedirectToPage()` —
   PRG pattern.

Form fields (all bound via `[BindProperty] public QueryInput Input`):

| Field    | Type   | Required | Constraints                          |
|----------|--------|----------|--------------------------------------|
| Name     | string | yes      | StringLength(120)                    |
| Email    | string | yes      | EmailAddress, StringLength(200)      |
| Subject  | string | no       | StringLength(200)                    |
| Message  | string | yes      | StringLength(4000, MinimumLength=5)  |

Client-side: when a Supabase auth session is present, JS pre-fills
`Input_Name` from `user.user_metadata.full_name || .name` and
`Input_Email` from `user.email`. The user can edit the pre-filled values.

### 4.4 `/Privacy`

Static glass card with placeholder text. No logic.

### 4.5 `/Error`

Default ASP.NET error model. Shows the request ID when available and a
note about the Development environment.

---

## 5. Data Model

### 5.1 Supabase `queries` table

Expected schema (created by the user out-of-band, not in migrations):

| Column      | Type           | Notes                                       |
|-------------|----------------|---------------------------------------------|
| `id`        | `bigint`/`uuid`| Auto-generated primary key                  |
| `created_at`| `timestamptz`  | Default `now()`                             |
| `name`      | `text`         | Required (matches `Input.Name`)             |
| `email`     | `text`         | Required                                    |
| `subject`   | `text`         | Optional                                    |
| `message`   | `text`         | Required                                    |

Column names are snake_case to match the JSON payload (PostgREST is
case-sensitive).

#### Row Level Security

The `queries` table is the public attack surface for the publishable key.
It must have RLS enabled with **only** an insert policy for the `anon`
role:

```sql
alter table public.queries enable row level security;

create policy "anon can insert queries"
on public.queries for insert
to anon
with check (true);
```

No `select`, `update`, or `delete` policies for `anon` exist. Adding any
of them would let any visitor read or modify all submitted queries —
the publishable key ships in the page source on every request.

### 5.2 `ApiObject` (read model only)

See section 4.2. No local persistence.

---

## 6. External Integrations

### 6.1 Supabase

- Project URL: `https://uxpmjqynxeinulkjpifc.supabase.co`
- Auth key type: **publishable** (`sb_publishable_…`). The previous
  generation's "anon" key. Safe to publish on the client.
- Used for two independent purposes:
  - **PostgREST inserts** to `public.queries` from the server (named
    HttpClient `"Supabase"`).
  - **Auth** from the browser (supabase-js v2).
- One Supabase project is shared between dev, prod, and any environment.
  No multi-environment separation currently.

### 6.2 restful-api.dev

- URL: `https://api.restful-api.dev/objects`
- Public, no auth. Used for the `/Objects` demo only.
- Each page render fires a fresh request — no caching layer.

---

## 7. Authentication and Authorization

### 7.1 Stack

- Provider: Google OAuth, brokered by Supabase Auth.
- Mechanism: `@supabase/supabase-js` v2 in the browser. The Supabase
  client is created in `_Layout.cshtml` from `Config["Supabase:Url"]`
  and `Config["Supabase:Key"]` (both injected via `IConfiguration`),
  and stored on `window._supabase` so other pages can read the session.
- Server is anonymous. There is no ASP.NET auth scheme, no auth cookie,
  no `[Authorize]` attribute on any page model.

### 7.2 Flow

1. Layout JS calls `supabase.auth.getSession()` on load.
2. If a session exists, the `auth-slot` nav element is rendered with the
   user's name + a "Sign out" button.
3. Otherwise, a "Sign in with Google" button is rendered.
4. Clicking "Sign in" calls
   `supabase.auth.signInWithOAuth({ provider: 'google', options: { redirectTo: <current-page> } })`.
5. Supabase handles the redirect to Google and the callback at
   `https://uxpmjqynxeinulkjpifc.supabase.co/auth/v1/callback`.
6. supabase-js detects the auth callback on return, persists the session
   in `localStorage`, and `onAuthStateChange` fires — the nav re-renders.
7. Sign out calls `supabase.auth.signOut()` then `location.reload()`.

### 7.3 What sign-in does and doesn't do

| Capability                  | Status |
|-----------------------------|--------|
| Identity-on-display in nav  | Yes   |
| Pre-fill Query form         | Yes   |
| Associate query rows with user | No |
| Gate any page behind auth   | No    |
| Server-side knowledge of user | No |

### 7.4 Provider-side setup

These steps live outside the repo and are required for sign-in to work:

1. Google Cloud Console: create OAuth 2.0 Client ID. Authorized redirect
   URI: `https://uxpmjqynxeinulkjpifc.supabase.co/auth/v1/callback`.
2. Supabase Dashboard → Authentication → Providers → Google: paste the
   Client ID and Client Secret.
3. Supabase Dashboard → Authentication → URL Configuration: allow
   `http://localhost:5097` for dev and the production origin once known.

---

## 8. Design System

### 8.1 Tailwind setup

Tailwind ships via the Play CDN (`https://cdn.tailwindcss.com`) with an
inline `tailwind.config` registering the `ocean` palette and Inter font.
Custom utilities are defined in a `<style type="text/tailwindcss">` block
inside `_Layout.cshtml`, which the Play CDN processes (`@apply` works).

This is intentionally not a Node-driven build to keep the project
dependency-free. The cost is a small FOUC on first paint and a ~50 KB
JIT script. A future switch to `@tailwindcss/cli` is on the roadmap.

### 8.2 Color palette

`ocean` is the only custom palette. Values:

| Step | Hex       |
|------|-----------|
| 50   | `#eef6ff` |
| 100  | `#d9eaff` |
| 200  | `#b8d6ff` |
| 300  | `#8bbcff` |
| 400  | `#5b97ff` |
| 500  | `#3776ff` |
| 600  | `#1f57f0` |
| 700  | `#1a44c5` |
| 800  | `#19389a` |
| 900  | `#0e2360` |
| 950  | `#08153a` |

Body background gradient: `bg-gradient-to-br from-[#020617] via-ocean-950 to-ocean-800`.
Three floating orbs sit behind everything (`opacity-0.40`, `blur(90px)`,
colors `#3776ff`, `#1a44c5`, `#5b97ff`).

### 8.3 Component utilities (iOS Liquid Glass)

Defined as `@layer components` inside the Tailwind block:

| Class             | Purpose                                                 |
|-------------------|---------------------------------------------------------|
| `.glass`          | Standard frosted surface — `backdrop-blur(28px) saturate(180%)`, soft inner highlight, deep-blue drop shadow |
| `.glass-strong`   | Higher opacity + heavier blur; used on primary form card |
| `.glass-highlight`| Adds a top-right radial specular gleam                  |
| `.btn-primary`    | Capsule gradient button (ocean-400 → ocean-600) with glow |
| `.btn-ghost`      | Translucent secondary capsule                           |
| `.input-glass`    | iOS-style rounded translucent input with focus ring     |
| `.label-glass`    | Form label                                              |
| `.nav-pill`       | Hover-highlighted pill nav item (desktop)               |
| `.nav-pill-mobile`| Full-width block nav item with ≥44px touch target (mobile menu) |
| `.alert-success`  | Emerald-tinted glass alert                              |
| `.alert-danger`   | Rose-tinted glass alert                                 |

### 8.4 Validation styling

ASP.NET tag helpers emit `.field-validation-error` on spans and
`.input-validation-error` on inputs. These are restyled (in the
inline CSS, not Tailwind layer) to use rose-300 text and a translucent
red background on the invalid input.

### 8.5 Layout chrome

- Sticky glass navbar centered at the top. Rounded as a pill on `sm+`,
  and as a 3xl-rounded card on mobile (to accommodate the expanded
  hamburger menu below the top row).
- **Desktop (≥640px):** brand (emoji + "Exxaro Stack Test"), the four
  page links rendered as `.nav-pill`s, then a vertical divider and the
  JS-managed `auth-slot`.
- **Mobile (<640px):** brand collapses to the emoji only on `<420px`
  (`min-[420px]:inline` for the text). The auth slot stays visible.
  A hamburger button toggles a vertical menu below the top row,
  containing the four page links as `.nav-pill-mobile` items (block,
  ~44px touch targets). The menu auto-closes on link tap and when the
  viewport crosses the `sm` breakpoint.
- The toggle wires up `aria-expanded` / `aria-controls` and swaps a
  three-line / X icon on state change.
- Footer: small centered text, current year, link to Privacy.

---

## 9. Configuration and Secrets

### 9.1 Config sources, in precedence order (low → high)

1. `appsettings.json` — non-secret defaults.
2. `appsettings.Development.json` — Development overrides.
3. **User Secrets** (`UserSecretsId` set in csproj) — Development only.
4. **Environment variables** — Production. ASP.NET uses `__` as the
   section separator.

### 9.2 Configuration keys

| Key                | Type   | Required | Source (dev / prod)                              |
|--------------------|--------|----------|--------------------------------------------------|
| `Supabase:Url`     | URL    | yes      | User Secrets / `Supabase__Url` env var           |
| `Supabase:Key`     | string | yes      | User Secrets / `Supabase__Key` env var           |
| `Logging:LogLevel`*| object | no       | `appsettings.json`                               |
| `AllowedHosts`     | string | no       | `appsettings.json` (default `"*"`)               |

`appsettings.json` deliberately contains no `Supabase` section. If
`Supabase:Url` or `Supabase:Key` is missing at startup, the HttpClient
factory throws on first resolution.

### 9.3 What may be committed

- `appsettings.json` — non-secret only
- `.env.example` — placeholder values only
- The publishable Supabase key is *technically* safe to commit but is
  not, by convention, to avoid grooving a bad habit. RLS is the security
  boundary, not the key.

### 9.4 What must NOT be committed (covered by `.gitignore`)

- `.env`
- `appsettings.*.local.json`
- `secrets.json`
- `.claude/`
- `bin/`, `obj/`
- Editor metadata (`.vs/`, `.vscode/`, `.idea/`)

---

## 10. Build and Deployment

### 10.1 Local development

- Run `dotnet user-secrets set "Supabase:Url" "…"` and
  `dotnet user-secrets set "Supabase:Key" "…"` once.
- `dotnet run` (or via the `http` launch profile) serves on
  `http://localhost:5097`.
- No HTTPS profile in `launchSettings.json` by design. Browser dev
  tools, Supabase OAuth (which permits `http://localhost`), and the
  cookie defaults all work over HTTP locally.

### 10.2 Docker

`Dockerfile` is multi-stage:

1. **build**: `mcr.microsoft.com/dotnet/sdk:10.0`, copies the csproj
   and restores first (cache-friendly), then copies the rest and
   `dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false`.
2. **runtime**: `mcr.microsoft.com/dotnet/aspnet:10.0`, runs as the
   non-root `app` user (`USER $APP_UID`), listens on `ASPNETCORE_HTTP_PORTS=8080`.

`docker-compose.yml`:

```yaml
services:
  web:
    build: .
    image: exxaro/stack-test:latest
    container_name: startup-site
    ports: ["8080:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Supabase__Url: ${SUPABASE_URL}
      Supabase__Key: ${SUPABASE_KEY}
    restart: unless-stopped
```

`.env` on the host supplies `SUPABASE_URL` and `SUPABASE_KEY`. Compose
loads it automatically.

### 10.3 Linux server deployment

1. Install Docker + Compose plugin (`apt-get install docker.io docker-compose-plugin`)
2. Add the user to the `docker` group, log out and back in.
3. Clone the repo (HTTPS public or SSH deploy key for private).
4. `cp .env.example .env`, edit, `chmod 600 .env`.
5. `docker compose up -d --build`.
6. Open port 8080 in UFW and any cloud firewall.

Re-deploy: `git pull && docker compose up -d --build`.

### 10.4 Production HTTPS

Currently not provided. Real-world production requires TLS termination
(Caddy or Nginx + Let's Encrypt) in front of the container — both for
basic site security and because Supabase Auth callbacks require HTTPS
for non-localhost origins.

---

## 11. Operational Concerns

### 11.1 Logging

Default ASP.NET console logging. Levels in `appsettings.json`. Supabase
HTTP failures inside `QueryModel.OnPostAsync` are logged at `Error` with
the response body. In Docker, `docker compose logs -f web` tails them.

### 11.2 Caching

None. Every `/Objects` page hit calls the external API. Every page hit
serializes the Tailwind config inline. No `ResponseCaching`, no
`OutputCache`, no `IMemoryCache`.

### 11.3 Error handling

- Form failures (`/Query`): caught, logged, surfaced via `ErrorMessage`.
- External API failures (`/Objects`): caught, logged, surfaced via
  `ErrorMessage`.
- Anything uncaught in Production goes through `UseExceptionHandler("/Error")`.

### 11.4 Static assets

`app.MapStaticAssets()` (the .NET 10 static asset endpoint pipeline) plus
`MapRazorPages().WithStaticAssets()`. Asset fingerprints and compression
are handled by the framework.

---

## 12. Constraints and Roadmap

### 12.1 Known constraints

- **No production HTTPS.** Required for both real users and Supabase Auth
  callbacks. Caddy reverse-proxy is the planned answer.
- **Tailwind Play CDN.** Adds runtime JS; not how a real production site
  ships. Switch to a Node-built `site.tw.css` when ready.
- **No backwards integration of Supabase Auth with the queries table.**
  The signed-in user's identity is never written into the `queries`
  row. Adding a `user_id` column + writing `session.user.id` to it is
  ~10 lines of work.
- **The publishable Supabase key sits in dev-machine User Secrets and on
  the production server's `.env`.** Rotating it is a Supabase dashboard
  action plus a redeploy.
- **Microsoft Entra ID is not used** and shouldn't be re-attempted in
  this project — the user does not have permission to register apps in
  the Exxaro Entra tenant.

### 12.2 Planned, not started

- Reverse proxy + automatic Let's Encrypt cert (Caddy preferred).
- Move Tailwind to a build step.
- Output caching on `/Objects` (60s memory cache, perhaps).
- Real privacy content.
- Real contact email if/when the site becomes user-facing.

---

## 13. How to evolve this spec

When making a change:

1. Update the code.
2. Update the relevant section of this spec in the same commit.
3. Bump the version in the header if the change is significant
   (new page, new external dependency, auth model change). For small
   tweaks (copy edits, palette tweaks), leave the version alone but
   refresh "Last updated".
