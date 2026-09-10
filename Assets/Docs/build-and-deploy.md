# Build and Deploy — Client

Producing a WebGL build and putting it on the VPS. The server's own deployment (PM2, nginx,
MySQL, TLS) lives in the server repo's `docs/deployment.md` and `deploy/README.md`.

## Build

Two menu items, both in `Assets/Scripts/Editor/BuildWebGL.cs`:

| Menu | Endpoint | Exceptions | Use |
|---|---|---|---|
| `Museum/Build/WebGL (Development)` | `ServerConfig.devEndpoint` | full | local debugging in a browser |
| `Museum/Build/WebGL (Production)` | `ServerConfig.prodEndpoint` | explicitly thrown only | anything that ships |

Headless:

```bash
Unity -quit -batchmode -projectPath . \
      -executeMethod Museum.Build.Editor.BuildWebGL.Production
```

Output: `Builds/WebGL`.

Always build through the menu item, not the Build Settings window. The window's output folder
name becomes the payload filename (`Builds/WebGL` → `WebGL.data.br`), and a folder with a space
in it ships URLs the nginx regex blocks were never written for. Player Settings are also
persisted as Brotli / no decompression fallback, so a window build matches — but only the menu
item validates the endpoint and the video catalog.

### What the production build enforces

- **Every enabled scene in Build Settings** is included; zero scenes is a hard failure.
- `ServerConfig` must exist at `Resources/ServerConfig`, and `prodEndpoint` must start with
  `wss://` — the page is served over `https://` and browsers block mixed-content
  WebSockets. The script flips `useDevEndpoint` for the duration of the build and **restores
  it afterwards**, so a build cannot leave Play mode pointed at production.
- **Every `VideoCatalog` entry must be a non-empty `https://` URL.** A blank or `http://`
  entry fails the build rather than shipping a screen that silently never plays.
- Brotli compression, data caching on, explicitly-thrown exceptions only.

Builds set `decompressionFallback = false`, which makes nginx's `Content-Encoding: br`
**mandatory** rather than an optimisation.

### The `Wiraga` template

`BuildWebGL.cs` sets `PlayerSettings.WebGL.template = "PROJECT:Wiraga"` in code at build
time, the same way it sets the endpoint — rather than trusting whatever `ProjectSettings` has
saved. `Assets/WebGLTemplates/Wiraga/` (`index.html` + `style.css`) is what ships instead of
Unity's stock template: a viewport meta tag that blocks pinch-zoom and scroll,
`touch-action: none` so a joystick or look-area drag doesn't also scroll the page, a
`devicePixelRatio` cap of 2 for high-density phone screens, and a `Putar HP kamu ke samping`
overlay for portrait orientation. It also sets `window.unityInstance`, which
`MuseumPlatform.jslib`'s fullscreen request depends on. See
[input-and-platform.md](input-and-platform.md) for the full picture, including the phone
performance gap this template does not address.

### The ANGLE → D3D11 shader budget

**Symptom:** `Shader Hidden/Universal Render Pipeline/UberPost: GLSL compilation failed, no
infolog provided` — on Windows only, in a build that is fine on macOS.

An **empty info log is the tell**. A real GLSL syntax error always comes with a message; an
empty one means the browser rejected the program with no diagnostic, which happens either
because ANGLE's HLSL translator hit a hard D3D11 limit or because the GL context was **already
lost** and every later compile fails the same way. Windows browsers run WebGL2 through
ANGLE → D3D11; macOS runs it through Metal. Developing on a Mac means never seeing this.

**Diagnose before tuning.** If `UberPost` is the *only* failing shader it is a genuine
translation failure. If `Bloom`, `Skybox/Procedural` and `Lit` fail alongside it, the context
died and `UberPost` is a bystander — check `chrome://gpu` for a software/SwiftShader fallback
and watch for `CONTEXT_LOST_WEBGL` ahead of the shader errors.

**What was cut on 2026-09-10** to fit the D3D11 budget:

| Setting | Was | Now | Why |
|---|---|---|---|
| `Mobile_RPAsset.m_ColorGradingMode` | HDR | **LDR** | The heaviest `UberPost` path. Costs highlight roll-off — see [lighting.md](lighting.md#post-processing) |
| `m_ColorGradingLutSize` | 32 | **16** | Smaller LUT, less sampling |
| `m_SupportsLightCookies` | on | **off** | No light in any scene has a cookie |
| `m_SupportsLightLayers` | on | **off** | Every `m_RenderingLayerMask` in the project is `1` |
| `m_SupportDataDrivenLensFlare` | on | **off** | No `LensFlareComponentSRP` anywhere |
| `m_SupportScreenSpaceLensFlare` | on | **off** | Never overridden away from intensity 0 |
| `Mobile_Renderer.m_UseNativeRenderPass` | on | **off** | Inert on GL backends; one less path |
| URP Global `m_StripUnusedPostProcessingVariants` | off | **on** | Was shipping every `UberPost` keyword combination |
| `webGLPowerPreference` | high-performance | **default** | Stops forcing the discrete GPU on hybrid-GPU Windows laptops |
| `webGLMemorySize` / `webGLInitialMemorySize` | 32 | **256** | Heap growth churn during the Museum load is a context-loss route |

**What must not be cut, and why.** `m_RenderingMode: 2` (**Forward+**) is load-bearing — the
71 generated realtime lights need it, and dropping to Forward caps additional lights per
object. `m_SupportsHDR: 1` is load-bearing too: the Museum's bloom threshold is **1.15**, which
an LDR buffer clamps away entirely, so turning HDR off deletes the bloom rather than cheapening
it. `m_SupportsTerrainHoles` stays on — five scenes carry real `Terrain` components.

Already minimal before this pass: MSAA off, `m_RenderScale` 0.8, camera antialiasing `0` in
every scene, `m_SoftShadowsSupported: 0`, one shadow cascade.

**Still unverified:** none of this was measured on the actual Windows kiosk hardware. The
graphics API list is left on **auto** (`m_BuildTargetGraphicsAPIs: []`), which resolves to
WebGL2; if a future Unity makes WebGPU an auto candidate, pin it explicitly.

## Deploy

### Current host — `museumethnofun.com` (since 2026-09-10)

The client now lives on a Hostinger VPS, `root@212.85.25.177`, behind the **Traefik** that
Hostinger's Docker stack ships (`/docker/traefik-3z6t/`, host network, ports 80/443, Let's
Encrypt via HTTP-01). There is no host nginx and no `/var/www`; the site is one container:

```
/docker/museum/
├── docker-compose.yml   nginx:alpine, Traefik labels for Host(`museumethnofun.com`)
├── nginx.conf           the .br location blocks (Content-Encoding: br, gzip off)
└── site/                = Builds/WebGL, served read-only at /usr/share/nginx/html
    ├── index.html  style.css
    └── Build/  WebGL.data.br  WebGL.wasm.br  WebGL.framework.js.br  WebGL.loader.js
```

Deploy is an SFTP upload of `Builds/WebGL/` into `/docker/museum/site/`, **each file to a
`.uploading` name and then `mv`'d over the old one**, so the live file is never half-written
and nothing is deleted first; then `chmod -R a+rX /docker/museum/site` and a SHA-256 of each
remote file against the local one. Password auth, so `rsync`/`scp` from a script need
`sshpass` or a `paramiko` helper; the container does not need restarting — nginx reads the
bind mount.

The certificate covers `museumethnofun.com` **and** `www.museumethnofun.com`, and a
`redirectregex` middleware on the router sends `www` to the apex with a 308, so the game
has exactly one origin. The `www` A record arrived after the first deploy: while it was
missing, a router rule naming both hosts made the ACME order fail for the *pair* (the log
says `EOF` from the API, which is a failed authorisation, not a network fault) — never add a
host to the rule before its DNS resolves, or the apex loses its certificate too.

Cache headers are `max-age=0, must-revalidate` on the `Build/` files, **not** `immutable`
as the old host used: the payload filenames never change between builds, so an immutable
year-long cache would pin a visitor to the first build they ever loaded. The bytes are
identical between builds only when nothing changed, and nginx's ETag makes revalidation a
304.

The **game server is still the old VPS** — `wss://api.museum.fajrsyauqi.com` at
`101.32.239.188` — and the build's `prodEndpoint` points there. Moving it is a server-repo
job; when it happens, `ServerConfig.prodEndpoint` changes and this client is rebuilt.

### Previous host — `museum.fajrsyauqi.com` (nginx on `101.32.239.188`)

Kept for the day the server moves too; every trap below was earned there.

```bash
chmod -R a+rX Builds/WebGL      # EVERY build, not once — Unity rewrites .br as 600
rsync -avz --partial --exclude='.DS_Store' Builds/WebGL/ museumvps:/var/www/museum/
ssh museumvps 'chmod -R a+rX /var/www/museum'
```

**The `chmod` is per build, not per folder.** Unity writes the `.br` files mode `600` every
single time it builds. `rsync -a` preserves the mode, nginx (`www-data`) cannot read them,
and it serves its 403 HTML page — which the Unity loader then parses as game data and dies
with `Unknown data format (id="<html>\n<head><t")`. Chmod-ing the output folder once does
not inoculate it; the next build undoes it. Both ends, every time.

**No `--delete`.** It removes the old `Build/` files *before* the replacements finish
arriving, so a transfer that dies partway leaves an `index.html` pointing at four files
that 404. A 142 MB payload over a `ControlMaster` socket is long enough for that to
happen — it did, on 2026-09-09, and took the site down. Upload first; if stale files need
clearing, do it as a separate step *after* the new build is confirmed serving. This matters
more than it used to because the payload is **renamed** depending on how it was built:
`BuildWebGL` emits `WebGL.data.br`, the Build Profile window emits `<Product Name>.data.br`
(e.g. `Museum Wiraga.data.br`), so the two schemes do not overwrite each other.

**`--partial`** keeps what already transferred, so a dropped connection resumes rather than
restarting from zero.

`museumvps` is a `~/.ssh/config` entry for `ubuntu@101.32.239.188` with `ControlMaster auto`
/ `ControlPersist 4h`. The deploy key `~/.ssh/id_ed25519` carries a passphrase, so a
non-interactive shell cannot sign with it — open the master connection once from a real
terminal (`ssh museumvps`, type the passphrase) and every later `ssh`/`rsync` rides that
socket without prompting.

`index.html` is served `no-cache` and `Build/` is immutable-cached, so a redeploy takes
effect on the next load without a hard refresh.

### Traps that have all been hit for real

- **macOS ships openrsync** (`rsync 2.6.9 compatible`) with **no `--chmod`**. Fix
  permissions locally before sending, or `brew install rsync`.
- **Never run `rsync --delete` with a missing destination argument.** openrsync reads the
  lone path as the destination and empties it. That is how this project was destroyed on
  2026-08-19.
- The live nginx config is `/etc/nginx/sites-available/museum.fajrsyauqi.com.conf`. A
  second, disabled `museum` file rooted at `/var/www/museum-client` exists — do **not**
  switch the symlink to it.
- That config once had `location ^~ /Build/`. `^~` makes nginx skip all regex locations, so
  the `.br` blocks never ran and the browser got raw Brotli as JavaScript
  (`SyntaxError: Invalid character '<27>'`). Use `location /Build/`. Regex beats a *plain*
  prefix, never `^~`.
- **Every `Build/` URL carries a `?v=` cache buster, and it is load-bearing.** The four
  payload filenames never change between builds and nginx serves them
  `immutable, max-age=31536000`, so a returning visitor will pair a **cached
  `framework.js.br` with a freshly downloaded `.wasm`**. That is fine until a build adds or
  removes a `.jslib` function, at which point the stale framework cannot satisfy an import
  the new wasm declares and the page dies at instantiate:

  ```
  abort(LinkError: WebAssembly.instantiate(): Import #129 "env" "MuseumIsTouchDevice":
  function import requires a callable)
  ```

  It looks like a broken build and is not — the deployed bytes hash correctly. No reload
  clears it, because the cache was explicitly told to keep that response for a year. It hit
  on 2026-09-10, the first deploy after `MuseumPlatform.jslib` landed.

  `Assets/WebGLTemplates/Wiraga/index.html` writes `?v=BUILDSTAMP` onto all four URLs and
  `BuildWebGL.StampCacheBuster` rewrites the placeholder to the build time, logging it as
  the `Cache:` line. `index.html` itself is `no-cache`, so a new build invalidates all four
  on the next visit. **Do not remove the placeholder from the template** — the build warns
  if it goes missing, and the warning is easy to miss. The query does not affect nginx's
  `location` matching, which only sees the path, so the `.br` blocks still fire.

- **Test header fixes in a private window.** `Build/` files carry
  `Cache-Control: public, max-age=31536000, immutable`; a browser that cached a broken
  response keeps serving it and Cmd+Shift+R will not evict it. Clear via DevTools →
  Application → Clear site data.
- Do not diagnose `.br` files with `strings` — "UnityWeb Compressed Content (brotli)"
  appears inside the compressed literals and is not a wrapper header. macOS `curl
  --compressed` has no brotli support and returns empty. Decode for real:

  ```bash
  node -e 'console.log(require("zlib").brotliDecompressSync(require("fs").readFileSync("<f>.br")).length)'
  ```

### Verify

```bash
curl -sI https://museumethnofun.com/Build/WebGL.framework.js.br | grep -i content-encoding   # → br
curl -s  https://api.museum.fajrsyauqi.com/hi
```

Confirm the upload byte-for-byte rather than trusting rsync's summary. Fetch with
`Accept-Encoding: br` so nginx hands back the stored file unchanged — **never**
`curl --compressed`, which on macOS silently returns an empty body and hashes to
`e3b0c442…855`, the SHA-256 of nothing:

```bash
for f in WebGL.data.br WebGL.wasm.br WebGL.framework.js.br; do
  L=$(shasum -a256 "Builds/WebGL/Build/$f" | awk '{print $1}')
  R=$(curl -s -H 'Accept-Encoding: br' "https://museumethnofun.com/Build/$f" -o - \
      | shasum -a256 | awk '{print $1}')
  [ "$L" = "$R" ] && echo "$f MATCH" || echo "$f MISMATCH"
done
```

Then open `https://museumethnofun.com` in two tabs and play a room through.

## Payload

Production is **41.6 MB** as of 2026-09-10 evening — `WebGL.data.br` 34.3 MB, `WebGL.wasm.br`
7.2 MB, `WebGL.framework.js.br` 0.08 MB, `WebGL.loader.js` 0.03 MB. It was 29.5 MB that
morning (the museum-decor pass — 28 props, the lobby gallery, the signage canvases — is the
difference) and **144.6 MB** the day before.
See [asset-budget.md](asset-budget.md) for what was cut and the rules that keep it there.

The single largest historical cause was **uncompressed textures**: the default platform
import setting was `Uncompressed`, so 54 textures shipped as RGBA32 and Texture2D alone
accounted for 1024 MB of the 1058 MB packed payload. The second was **terrain alphamaps**
painted at 2048 across 200 m terrains with 11-12 layers assigned.

## Video hosting prerequisites

Before a production build the footage must be reachable:

- mp4, H.264 + AAC, faststart-muxed (`ffmpeg -movflags +faststart`).
- The host must send CORS headers covering `https://museumethnofun.com`,
  `Accept-Ranges: bytes` (206 responses — without it the browser downloads the whole file
  before the first frame), and `Content-Type: video/mp4`.
- One `https://` URL per key pasted into `Assets/Resources/VideoCatalog.asset`.

All 16 URLs point at `ik.imagekit.io/altara/…` with `tr=orig-true`, which serves the stored
file and stays clear of the `403 Video transformations limit exceeded` that any
transformation hits on the free plan. Verified 2026-09-10 from origin
`https://museumethnofun.com`: every URL answers a `Range` GET with **206**,
`Access-Control-Allow-Origin: *`, `Content-Type: video/mp4`; the preflight `OPTIONS` is 200
with `*` for methods and headers. ImageKit has no per-origin allow-list to configure — it
is wildcard — so a domain change never needs a change on the video side. Moving hosts
(VPS, R2, Bunny) would be 16 URL edits and no code change.

## Kiosk

Same URL, opened full-screen: `chrome --kiosk https://museumethnofun.com`. The venue
needs internet — there is no offline build, and the Museum scene is the only part that would
work without a server.
