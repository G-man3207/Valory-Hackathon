<!-- This file is automatically updated by ikon tool commands. User edits are preserved only below the ikon-user-content-below marker. -->

# Ikon AI App Development Guidelines
<!-- checked-against: e607edfd461f2a46 -->
**Detailed API references are in docs/Ikon.Agent.Docs/guides/. See the guide index at the bottom of this document.**

## Architecture

Ikon AI App is a cloud-native platform for building interactive, AI-powered multi-user experiences.

**App structure:** C# app runs in the cloud, streams reactive UI to browser clients via WebSocket. The app persists even when all clients disconnect. Multiple clients connect to the same app instance for real-time collaboration, games, and shared experiences.

**AI services (no setup required — the platform handles all API keys and connections):**
- `Emerge.Run<T>()` — LLM text generation and structured JSON output (Claude, GPT, Gemini, Grok, and more)
- `ImageGenerator.GenerateAsync(prompt)` — AI image generation (Gemini, GPT Image, Flux, etc.)
- `SpeechGenerator.GenerateAsync(text)` — text-to-speech (ElevenLabs, Azure, OpenAI, Google)
- `SpeechRecognizer.RecognizeAsync(samples, sampleRate)` — speech-to-text (Whisper)
- `VideoGenerator.GenerateAsync(prompt)`, `EmbeddingGenerator.EmbedAsync(texts)`, `WebSearcher.SearchAsync(query)` — other AI services

Every AI service has a static one-shot like the above with a sensible default model; the `new Xxx(model)` constructor + `XxxConfig` form exists for advanced options (batching, input images, custom sizes, timeouts).

**UI system:** Declared in C# inside `UI.Root(content: view => { ... })`. The `view` parameter type is `UIView` (a class), and `IView` is a global type alias for `UIView` (defined in `GlobalUsings.cs`), so a helper can be declared with either name — `static void Render(UIView view, ...)` and `static void Render(IView view, ...)` both work. There is no `ViewContext` type, though — declaring a helper as `(ViewContext view, ...)` fails with `CS0246: type 'ViewContext' could not be found`. `UIView` provides component methods: `view.Text()`, `view.Button()`, `view.ScrollArea()`, `view.TextField()`, etc. Nested content lambdas receive their own `UIView view` parameter.

**State:** `Reactive<T>` (shared across all clients), `ClientReactive<T>` (per-client), `UserReactive<T>` (per-user). Changes automatically trigger UI updates — only the diff is streamed to clients.

**Styling:** Crosswind — Tailwind-like utility classes in C# string arrays, with a motion animation system for declarative keyframe animations (`motion-[...]`). Three layers, all valid in the same style array:

1. **Semantic theme-aware classes** (`bg-card`, `text-primary`, `bg-brand-solid`, `border-secondary`, `bg-background`) — the default path. They resolve through CSS variables that auto-flip between light and dark, and respond to per-app `IkonTheme` overrides, so light/dark mode switching and brand re-skins just work without touching individual style arrays.
2. **`Ikon.Parallax.Theming` token classes** (`Button.PrimaryMd`, `Card.Default`, `Text.H1`, `Layout.Page`, …) — pre-composed bundles of #1 above. Ergonomic shortcuts with tested defaults; use them when they fit, ignore them when they don't. They follow the theme because they're built from semantic classes.
3. **Hardcoded Tailwind palette classes** (`bg-amber-400`, `text-zinc-950`) and **raw hex** (`#1A2B3C`) — full custom control for looks that intentionally shouldn't change with the theme. These bypass the IkonTheme system entirely. The trade-off: if you later add light/dark switching or a brand re-skin, every fixed-color site needs to be revisited.

The three compose freely in the same array (`[Button.PrimaryMd, "mt-4 self-center"]`, `["bg-card border border-secondary p-6 rounded-2xl"]`).

**A `style:` array is EXACTLY what renders — it does NOT merge the component's themed default (opt-IN model).** Every styled component ships a themed default (`view.TextField` → `Input.Default`, `view.Button` → `Button.Default`, …). With NO `style:` array you get that full themed default. The moment you pass a `style:` array, you get exactly those classes and nothing else — so `view.Button(["px-6"], …)` is a bare button with only `px-6`, not a themed pill. To ALSO layer the themed default underneath your classes, add the literal `"default"` marker as the first entry: `view.Button(["default", "px-6"], …)` renders the pill plus your `px-6`, your classes winning on conflict. This is why a chrome button written bare (`["bg-transparent hover:bg-secondary …"]`) can't accidentally inherit the pill default's brand `hover:` fill and flood on hover. Platform theme token COMPOSITES (`Button.PrimaryMd`, `Button.ErrorMd`, `Card.Interactive`, `Badge.SuccessMd`, …) are complete styles — pass them as-is (`[Button.OutlineMd]` renders exactly the outline button); add classes freely (`[Button.OutlineMd, "mt-2 w-full"]`) and never write `"default"` before a composite (it already is the full style). `"unstyled"` is now a no-op (there is no base to opt out of) and can be dropped. Slot-style params with themed defaults (`contentStyle:` on Popover/Tooltip/HoverCard, Dialog's `titleStyle:`/`descriptionStyle:`/`headerStyle:`) follow the same opt-in rule — add `"default"` to keep the slot's theme.

**Theming:** Per-app brand commitment goes in `new IkonTheme { ["primary"] = "amber-400", ["background"] = "zinc-950", ... }` at the `UI` initializer — an indexer-keyed object initializer where each entry sets a CSS variable — or, for the vocabulary aliases (`primary`, `card`, `radius`, `density`, …), commits that alias's whole variable cluster. There are NO named init properties for colors (no `Brand =`, no `Background =`) and no fan-out beyond an alias's documented cluster. The only named init properties are `Mode` and the scheme pairs — `DarkMode` (also spelled `Dark`) and `Light`, each taking a whole `IkonTheme` of its own. The platform `IkonTheme` (in `Ikon.Parallax.Theming`) is the only configurable surface — there is no local `IkonTheme.cs` to edit. See the **Ikon Theming Guide** in `docs/Ikon.Agent.Docs/guides/ikon-theming-guide.md` for the canonical reference (palette/font/radius/motion overrides, dark-mode pairing).

**Audio:** `private Audio Audio { get; } = new(app);` — audio synthesis (Ikon.Resonance), effects, playback, and client microphone capture. Client camera/screen capture is the separate `Video` class (`private Video Video { get; } = new(app);`).

**Namespaces:** All Ikon namespaces are auto-imported via `GlobalUsings.cs` — no manual `using` statements needed for Ikon types.

## Development Focus

- Most development happens in the C# app, including styling
- LLM text generation uses `Emerge.Run<T>()`. Other AI services (image, speech, video) are standalone classes — call their static one-shot (`ImageGenerator.GenerateAsync(prompt)`) or instantiate with a config for advanced options
- UI is declared inside `UI.Root([Page.Default], content: view => { ... })` — components are methods on the `view` lambda parameter
- Crosswind styling: Tailwind-like syntax in style arrays, with motion animation system
- `Reactive<T>` for shared state, `ClientReactive<T>` for per-client state — changes trigger UI updates automatically. Lists go in `ReactiveList<T>` / `ClientReactiveList<T>` — every mutation method notifies on its own
- All Ikon namespaces are auto-imported via `GlobalUsings.cs`
- Only modify frontend-node when integrating custom React UI components
- Developers can freely add any NuGet packages to C# app or npm packages to frontend

## Running and checking the app

Run it from the app root with `ikon app run` (add `--log-debug` for the server's own detail); the
URLs land in `build/app/artifacts/bin/<App>/debug/ikon-server-info.json`. Then check it as text
before reaching for a browser:

```bash
ikon app browse --steps "observe"                                   # screen text + [id] action handles
ikon app browse --steps "fill 1 hello; tap Send; wait 1500; look after.png"
ikon app browse --text-only --format json < steps.txt               # one step per line, one JSON object per step
```

`observe` prints what the screen says and which actions exist; `tap <id|label>` and
`fill <id|label> <text>` act through the SDK and print only what changed; `look [file.png]`
screenshots a headless browser that has joined the **same client session**, so the pixels always
match the text. `click <selector>`, `eval <js>` and `console` reach the DOM for custom React
components. An observation costs a few hundred tokens where a screenshot costs thousands — read the
text first and screenshot only when the question is about pixels. `ikon app stop` ends the run.

## What the app must contain

Before writing UI, name what KIND of product this is and what its ordinary workflow needs. Users
notice a missing standard control faster than a missing feature, because every product they have
used has it — and being asked for one after the first build is the failure this section prevents.

Established convention decides these, not the request:

| The app is… | It needs, from the first build |
|---|---|
| records people accumulate (contacts, invoices, tasks, recipes) | search · sort · filters on the high-value fields · a date range when time changes which records matter · export where taking data out is a real task |
| messaging or comments | reply · copy · edit and delete for the author · a per-message action row that works on touch |
| a board or pipeline | move by drag AND a non-drag path · a card detail |
| a dashboard | the summary numbers first · a chart matched to the question · drill-down to the rows |
| an editor or canvas | undo/redo · select · delete · duplicate · zoom by wheel and pinch where the surface scales |
| media playback | play/pause · seek · elapsed and total time |
| a queue someone reviews | the review verbs themselves — Approve · Reject · Request changes — never a bare Yes/No · the evidence needed to decide, in one place |
| a builder of multi-step automations | one stated trigger · labelled branches with a default path · per-step test · a run log |

**Reversal is not an editor's privilege.** Anything that changes user-visible state either undoes —
including AI-made changes, on the same terms as the user's own — or says plainly, before the act,
that it cannot be undone. Where an action can be deferred, a time-bounded Undo beats a confirmation
dialog; keep the dialog for what is genuinely irreversible.

This is the conventional MINIMUM for the workflow already asked for — **not a feature list**.
Anything the request did not ask for and the workflow runs fine without is a feature: leave it out.
Three controls a user would miss beats ten that pad the screen.

**Only build a control that works.** A Filter chip that filters nothing, an Export button with an
empty handler, a Sort header that reorders nothing — each ships as a promise the app breaks. A
visible control either works, is disabled with a visible reason, or does not exist. A control with
no distinct meaning is dead too: an app that saves on every change does not also get a Save button.
Where the user's own permission is what blocks it, explain that and offer Request access if the app
has somewhere to send the request — asking is not being granted.

**A control that narrows a collection must not lose data.** Derive filter and grouping options from
the values actually present, show a value with no current items as empty with a count rather than
dropping it, and make Clear always return the whole collection. This failure is silent — the control
works, results render, and the missing item produces no state the user can see.

**Reconcile before you add.** On a later pass, an equivalent control that already exists is repaired
in place — never given a second implementation beside it. Keep its value, bindings and permissions,
and remove the one it replaces only once the replacement works.

**Completeness is not clutter.** High-frequency controls visible; selection-specific ones
contextual; the rest in a menu or overflow. On a narrow viewport they move into a compact surface —
they do not vanish.

**Separate the surfaces.** How content is made does not decide how it is read. Authoring controls
(drag handles, resize grips, edit affordances) belong to the authoring surface and must not leak
into the reading one, and interactive does not mean editable — a reader may filter a chart or run a
simulation without gaining the right to change it.

**Consequential actions need a human act.** When the app can reach outside itself — `app.Payments`,
`app.Email`, `app.Telephony`, `app.Notifications`, a publish, a permanent delete — a person presses
something that names the action. Never as a side effect of an AI turn: an LLM may draft the email,
price the order, or pick what to delete, but a person confirms the send, the charge, or the
deletion. Reversible in-app work (generate, draft, preview, rearrange) needs no confirmation and
should not ask for one.

Recipes for the common cases are in the pattern corpus — `record-list-toolbar`,
`message-action-row`, `board-move-without-drag`, `form-field-discipline`, `chart-for-the-question`,
`zero-results-state`, `overlay-selection`.

## How the app must look

A working app that looks like a wireframe is not done. Every app ships polished on the first
build, without being asked — the user judges the first screenshot before they read a line of code.
Commit to a look and execute it fully; never present a bare default and offer polish as a
follow-up.

**Pick a visual direction before the first `view.*` call**, in one sentence: what this product
feels like (a calm finance tool, a loud party game, a dense operator console, a warm journal), the
palette family, the type character, the corner radius, the density. Then commit it with
`new IkonTheme { … }` in the app constructor — brand colour, background mood, font, radius — so
the whole UI carries the direction rather than the platform default. The default theme is a
starting point for the platform, not a finish for any product. Two apps built from different
requests must not look like the same app.

**Execute the direction everywhere it shows:**

- **Hierarchy.** One clear primary action per screen in the brand colour (`Button.PrimaryMd`);
  everything else neutral, outline or ghost. One display-size headline (`Text.Display`/`Text.H1`),
  then a real type scale down to `Text.Caption` — never five lines of the same body size.
- **Surfaces with depth.** Vary the card language — `Card.Elevated` for the thing that matters,
  `Card.Subtle`/`Card.Flat` for the rest, `Card.Glass` over an image or gradient. A page of
  identical `Card.Default` boxes on `bg-background` reads as unfinished.
- **Space and rhythm.** Generous page padding (`p-6`/`p-8`), a consistent gap scale
  (`Layout.Column.Lg` between sections, `Layout.Column.Sm` inside a group), max-width containers
  (`Container.Lg`/`Container.Xl`) so text never runs edge to edge on a wide screen.
- **A backdrop, not a void.** Give the page a mood: a soft gradient (`bg-gradient-to-br from-… to-…`),
  a tinted background, a hero image or accent panel. Icon tiles with a tinted background
  (`bg-brand-solid/10 rounded-lg`) beside stat values and list rows.
- **Motion where state changes.** Fade/slide in new content (`motion-[…]`), hover and press
  feedback on everything clickable (`State.Pressable`, `Transition.Fast`), skeletons while loading,
  a spinner on a busy button. No animation for decoration only.
- **Designed empty, loading and error states** (`EmptyState.*`, `Skeleton.*`, `Alert.*`) — a blank
  panel or a raw exception string is never acceptable.
- **Every viewport.** Check the narrow layout: rows wrap or stack, touch targets are ≥ 40px, nothing
  overflows. `h-screen` root, internal scroll regions.

**Keep it theme-safe while doing this.** Semantic classes and tokens carry the direction across
light/dark; hardcoded palette classes are for deliberate fixed-brand surfaces only (see
"Color tokens and theming" below). Small text stays at AA contrast.

The `styling-guide` and `ikon-theming-guide` guides carry the full pattern library — gradients,
glass, icon containers, hero layouts, motion recipes. Consult them for the execution; the decision to build polished is already
made.

## Common Pitfalls

Recurring hallucination + footgun classes from generated Ikon apps. Each compiles or runs cleanly in the wrong shape and silently breaks the app. Rules only — each with its canonical form.

**THE TWO MOST COMMON DRAFT COMPILE FAILURES — check your code against these before finishing:**
1. Collection state MUST use the reactive collection types: `ReactiveList<T>` / `ReactiveDictionary<TKey,TValue>` (+ `Client*`/`User*`/`Persistent*` variants). Wrapping a mutable collection — `Reactive<List<T>>`, `PersistentReactive<Dictionary<K,V>>` — is build error IKON002.
2. There is NO `view.Keyed(...)` and no React-style key prop (CS1061) — render list items directly inside the loop; the reactive diff tracks identity itself.

### Reactive state

- **Re-render is implicit.** Reading `_x.Value` inside a UI lambda registers the dependency; the subtree re-renders on change. There is NO `view.Dynamic`/`Watch`/`Bind`/`Observe`/`When`/`Live`/`Reactive` wrapper (CS1061). `UIView` renders components only — it has no reactive-state methods.
- **Reactives are `private readonly` fields, `new(defaultValue)` — ONE positional arg.** `private readonly ClientReactive<int> _count = new(0);`. No `app`/key ctor args, no `app.ClientReactive(...)` (CS1503/CS7036). The initial value is the argument: `new("")`, `new(0)`, `new(new())`. The `*ReactiveList<T>` family starts empty — declare `= new();` with no argument.
- **List state is `ReactiveList<T>`, dictionary state is `ReactiveDictionary<TKey,TValue>`** (with `Client*` / `User*` / `Mount*` / `Persistent*` / `PersistentUser*` variants of BOTH; a `Reactive<T>` wrapping a mutable collection — `Reactive<List<T>>`, `PersistentReactive<Dictionary<K,V>>`, etc. — is build error IKON002, the most common draft compile failure): mutate on the reactive itself (`_completions[key] = true`, `.Remove(key)`, `.ContainsKey`, enumeration are tracked). For a list: `private readonly ClientReactiveList<TodoItem> _todos = new();`. Mutate on the reactive itself — `_todos.Add(item)`, `.Remove`, `.RemoveAll`, `.RemoveAt`, `.Insert`, `.Clear`, `.ReplaceAll`, `.Sort`, `.AddRange`, or `_todos.Update(list => …)` for a whole-list transform — one change notification each. Enumerate the reactive directly (`foreach (var t in _todos)`; `.Count` and `[i]` are tracked reads). `.Value.Add` does not compile (CS1061 — `Value` reads are `IReadOnlyList<T>`); `.Value = newList` replaces the whole content, same as `ReplaceAll`.
- **There is NO `view.Keyed(...)` and no React-style key prop** (CS1061, a recurring draft compile failure). List items need no key wrapper — render them directly inside the loop (`foreach (var item in _todos) { view.Row(...) }`); the reactive UI diff tracks identity itself.
- **Style arrays hold ONLY strings — never enums** (CS0029 `cannot convert 'IconSize' to 'string'`, a recurring draft compile failure). Size ENUMS (`IconSize`, `BadgeSize`, `SpinnerSize`, …) go in the `size:` parameter: `view.Icon("check", size: IconSize.Sm)`. Inside a style array use the Theming CLASS strings: `view.Icon([Icon.Sm], name: "check")` — `Icon.Sm` (a string constant), not `IconSize.Sm`.
- **Field initializers cannot reference other instance members** (CS0236). The primary-constructor `app` param is fine (`private Audio Audio { get; } = new(app);`); another field is not. Event wiring (`app.OnClientJoined(...)`) goes in `Main()`.
- **Drafts and typed-in-progress text bind USER scope, not client scope.** A reload mints a new client session, so `ClientReactive` drafts vanish with it; `UserReactive<string>` (or `PersistentUserReactive`) survives reloads and follows the user across tabs. Never patch this with browser storage.
- **Per-client state is implicit.** In UI lambdas and handlers just read/write `_clientReactive.Value` — no client-id lookup, no `_x.Get(clientId)` (CS1061). Client/session ids are `int`, never `string`. Outside a scope — a `Task.Run` loop, a timer, an endpoint handler, or a write aimed at *another* client — name the target instead: `_x.SetFor(clientSessionId, value)` / `_x.ValueFor(clientSessionId)` (`UserReactive` takes the `string` userId, `MountReactive` the mountId). Capture the id while the scope exists (`var cid = ReactiveScope.ClientId;` inside the callback, or `ctx.ClientSessionId`). A scopeless `.Value` throws with the fix in the message — it never silently writes to nowhere. `using (ReactiveScope.Use(new ClientScope(cid))) { … }` is still the tool for scoping a whole *region* (several reads and writes); it takes an `IScopeKey` — `new ClientScope(id)`, never the bare int (CS1503); there is no `.Open`/`.Enter` (CS0117). Plain shared `Reactive<T>` needs none of this.
- **Mount-scoped state is the `Mount*` family.** An app that declares Parallax mounts — an embedded `aiCanvas` beside the `ikon-ui` page — partitions state per mount with `MountReactive<T>`, `MountReactiveList<T>`, `MountReactiveDictionary<TKey,TValue>` and `MountReactiveHashSet<T>`: same contract as the unscoped types, one value per mount. `.Value` resolves against the mount being rendered and throws outside a render iteration, so background work names the mount instead (`SetFor(mountId, …)` / `ValueFor(mountId)`). For state shared across one client's mounts use `ClientReactive<T>`; across all clients, plain `Reactive<T>`.
- **A side effect that follows state is `ReactiveEffect`.** `new ReactiveEffect(async ct => …, _a, _b)` re-runs the body whenever a listed dep changes and cancels the token when one changes mid-run; keep it in a field and `Dispose()` to stop it. The scoped variants — `ClientReactiveEffect`, `UserReactiveEffect`, `MountReactiveEffect` — give each client, user or mount its own runner, and unlike the global one they do NOT fire at construction: the first dep change inside that scope starts the runner.
- **`Cells` is advanced and rarely needed.** For cross-client shared state use a plain `Reactive<T>` field; durable → `PersistentReactive<T>`. If you do use it: `Ikon.App.Cells` is NOT in `GlobalUsings.cs` (it is the one legitimate `using Ikon.*` an app adds — without it the name `Cells` doesn't resolve at all, CS0103). Everything on `Cells` is an INSTANCE member reached through `Cells.Instance` (`Cells : AsyncLocalInstance<Cells>`), so a bare `Cells.Connect<IFoo>(…)` is CS0120. Canonical: `using Ikon.App.Cells;` then `[Cell]` + `Cells.Instance.Connect<IFoo>(new SessionIdentity(key))` — only when the plan explicitly needs keyed shared sessions across isolated instances. `Cells.Instance.Current` is the installed `CellHost?` (null until startup installs one); `Initialize` is internal — the platform calls it, apps never do. A connected cell is an `ICell<TSessionIdentity>`, exposing the `Identity` it was keyed by. A reactive read through a cell interface is a `MirrorReactive<T>` whose `Status` (`MirrorStatus.Connecting`/`Live`/`Failed`) and `Error` say whether the value is the cell's or still the seed; render the status, never the seed as an answer. In cell-host mode `AppServices.Instance` is how code outside the app class reaches the host: `Secrets`, `DatabaseAsync`/`OpenDatabaseAsync`, `WhenReadyAsync`, and `HostApp` — which is set ONLY in cell-host mode, where the session serves exactly one cell, and is null in an ordinary app instance.

### AI services

- **`Emerge` is a static class** — `Emerge.AskAsync(prompt)` / `Emerge.Run<T>(model, pass => …)` directly. No `IEmerge`, no DI. Same for `Audio` (`new(app)` field), `UI`.
- **`await Emerge.Run<T>(…)` IS the one-shot — no terminal call.** Canonical — copy whole: `var result = await Emerge.Run<MyResult>(LLMModel.Claude46Sonnet, pass => { pass.Command = userPrompt; });`. It returns a NON-NULL `T` and THROWS `EmergenceStoppedException` when generation fails — try/catch for a friendly error state (`catch (EmergenceStoppedException) { _error.Value = "Generation failed"; }`). The prompt goes in `pass.Command`, never a positional arg (CS1660); deconstructing the awaited result into a tuple is CS8130. Pass a `KernelContext` second only when seeding input (images, prior turns): `Emerge.Run<T>(model, ctx, pass => …)`. Seeded input is a `MessageBlock` of parts — `TextPart`, `ImagePart`/`ImageUrlPart`, `PdfPart`/`PdfUrlPart`, `AudioPart`, `VideoPart`/`VideoUrlPart`/`VideoAssetPart` — where the `*UrlPart` forms reference content by url instead of carrying bytes, and `AudioIdPart` replays audio the model generated earlier by its provider-side id. Add `.FinalAsync()` only when you need the updated context back — its `(T? result, KernelContext ctx)` result stays nullable and keeps the guard. Streaming: `await foreach (var ev in Emerge.Run<T>(…))` — the same run is awaitable OR enumerable, pick ONE and stay with it. Awaiting the same run object twice is fine (the second await returns the cached result), but MIXING the two — awaiting a run you enumerated, or enumerating one you awaited (or enumerating twice) — throws `InvalidOperationException`; start a new run instead.
- **`Emerge.AskAsync` is PROMPT-FIRST** — `AskAsync(userPrompt)` or `AskAsync(userPrompt, LLMModel.Claude45Haiku)`; model-first is CS1503. `AskAsync(prompt)` returns a `string`; `AskAsync<T>(prompt)` returns a `T` built from JSON matching `T`'s schema (a `public record`/`class`, never a `struct`) and is the one-call typed form — `Run<T>(model, pass => …)` is for when you need the pass's controls. Both throw `EmergenceStoppedException` on a failed run; catch it and show an honest error state.
- **`LLMModel.Default` is `Claude45Haiku`** — the cheap+fast model the one-shots (`Emerge.AskAsync`) already run on; pass it wherever a model is asked for and you have not decided. Name a concrete value when the task warrants: `Claude46Sonnet` (reasoning/chat), `Claude45Haiku` (fast/cheap), `Gemini25Flash`, `Gemini25Pro`, `Gpt5Mini`, `Gpt5`, `Grok420Reasoning`.
- **AI one-shots never return null — no guards, use try/catch (CS8602 never applies).** Every one-shot (`ImageGenerator.GenerateAsync`, `SpeechGenerator.GenerateAsync`, `VideoGenerator.GenerateAsync`, `MusicGenerator.GenerateAsync`, `WebSearcher.SearchAsync`, …) returns non-null and throws on failure. Two DISTINCT exception types: the standalone AI services throw `AIException`; `await Emerge.Run<T>(…)` throws `EmergenceStoppedException`, which does NOT derive from `AIException` — `catch (AIException)` will not catch it. Catch the matching type when the app should continue without the result: `catch (AIException) { _error.Value = "Generation failed"; }` for the services, `catch (EmergenceStoppedException) { … }` for Emerge. Use `?.`/`??` when rendering from state.
- **What the catch RENDERS is part of the rule — never the exception.** A cloud AI call fails at runtime on a perfectly built app, and the person looking at it is not a developer. Render a short human sentence plus a way to try again (`_imageError.Value = "Couldn't create the image — try again."` beside a retry button); never `ex.Message`, `ex.ToString()`, a provider or model name (`google.gemini-25-flash-image.global`), or a socket/HTTP error. Observed shipped failure: a post studio drew `SocketException: Connection reset by peer` across the half of the canvas where the photograph belonged, with no empty state behind it. Every surface that can show a generated result needs all three renderings — waiting, failed-with-retry, and empty. **A failure must not destroy the work that produced it**: the prompt, the uploaded file, the form the user filled and any earlier output all survive, so Retry means press-again, never type-it-all-again — clearing the input on failure is the same defect as swallowing the error. When one part of a multi-part generation fails, retry only that part.
- **`WebSearcher.SearchAsync(q, maxResults: 5)` is the one-shot — static AND instance.** `await WebSearcher.SearchAsync(q, maxResults: 5)` with no instance; `await searcher.SearchAsync(q, maxResults: 5)` when you hold one. `SearchPagesAsync(new SearchConfig { ... })` is for site-restricted / country / language targeting; `SearchImagesAsync` for images. `SearchResult` has `Url`, `Title`, `Content` (no `Snippet`). The other AI facades mirror the same pair (`ImageGenerator.GenerateAsync`, `OCR.AnalyzeAsync`, `SpeechRecognizer.RecognizeAsync`, …).

### The view-call shape

- **ONE shape for every `view.*` call: the leading `string[]` style array is the ONLY positional argument; name everything else.** `view.Column(["flex gap-4"], content: v => …)`, `view.Text(["text-lg"], text: "Hi")`, `view.Button(["px-4 py-2"], text: "Save", onClick: async () => …)`. (One true exception: `view.Slider`'s scalar convenience overload is value-FIRST — see the next bullet.) Never write `style:` when the array is already positional (CS1744); never put a named arg before a positional (CS8323); never pass a second positional. The display string is `text:` on `view.Text`, `view.Button`, `view.Link`, and `view.ActionButton` alike — not `value:` or `label:` (CS1739). `bind:` is a named parameter on the same canonical overload, so bound fields follow the identical shape: `view.TextField(["flex-1"], bind: _input)`.
- **`bind:` is the two-way form for form controls** — TextField/TextArea/Select/RadioGroup bind `Reactive<string>`, Checkbox/Switch `Reactive<bool>`, Slider `Reactive<double>` (on its canonical style-first overload). Prefer it for typed input. When you have no Reactive, omit `bind:` and use `value:` + `onValueChange:`; if both are passed, `bind:` wins. A `value:` with NEITHER renders a genuinely read-only control (`aria-readonly`) — it no longer pretends to accept edits, so use that shape only when you mean a display field. Manual pairs: Select/RadioGroup = `value:`(string) + `onValueChange:`; Checkbox/Switch/Toggle = `value:`(bool) + `onValueChange:` (`formValue:` is the HTML form attr on Checkbox/Switch ONLY — Toggle takes neither `formValue:` nor `bind:`, CS1739). Slider has TWO overloads and mixing their shapes is the classic CS1503: on the canonical style-first overload `value:` is a LIST of thumb positions — `view.Slider(["w-full"], value: [50.0], onValueChange: async v => …)` (multi-thumb capable; `bind:` lives here), so `value: 50` there does not compile; the scalar convenience overload is value-FIRST — `view.Slider(50, style: ["w-full"], onValueChange: async v => …)` — the ONE exception to the style-array-first rule. `view.Select` options are `IReadOnlyList<SelectOption>` — `new SelectOption(value, label)` per choice; no `labels:` array.
- **TextField `disabled:` can be reactive** — the renderer maps it to `readOnly` + `aria-disabled`, so flipping `disabled: _busy.Value` mid-typing doesn't remount the input or drop keyboard focus. Gate async work with a disabled input, a disabled Button, or both.
- **`onSubmit:` lambdas must be `async`** (`Func<string, Task>` — CS0126/CS1643 otherwise). `onClick:` accepts sync, async, and expression lambdas alike but must be PARAMETERLESS.
- **Only `view.Box`, `view.Button`, `view.Card`, `view.Image`, `view.Link`, `view.TableRow`, `view.ToolbarButton` take a parameterless `onClick:`** — `view.Text` and layout primitives (`Column`/`Row`/`Grid`) don't (CS1739). (The charts take an `onClick:` too, but it hands you `ChartClickArgs` — a different shape.) Prefer `view.Button` with a content lambda for clickable text/tiles; a clickable `view.Box` is fine too — it carries button semantics automatically (`role="button"`, `tabIndex=0`, Enter/Space activation). Give an icon-only clickable Box an accessible name: `props: new Dictionary<string, object> { ["aria-label"] = "Remove" }`.
- **`props:` needs the concrete type**: `props: new Dictionary<string, object> { ["aria-label"] = …, ["title"] = … }` — target-typed `new()` is CS0144+CS0200. Layout primitives (`Column`/`Row`/`Grid`) take no `props:` at all (CS1739).
- **The `style` parameter is CLASSES, not CSS — and there is no `style:` or `inlineStyle:` argument to pass.** The leading `string[]` IS the style parameter, so naming it again is CS1744 ("a positional argument has already been given") and `inlineStyle:` is CS1739. The confusion is worth naming because React trains the opposite instinct: there, `style` is inline CSS and the normal way to set a computed size. **A runtime-computed value goes in the array as an arbitrary value** — `$"w-[{pct:0.#}%]"`, `$"left-[{x:0.#}%]"`, `$"h-[{n}px]"` — which is also the ONLY form that survives every renderer: Crosswind resolves it into a token the Flutter client understands, whereas an inline `["style"]` prop is web-only and silently does nothing on a native controller.
- **Reserved words as lambda params are syntax errors** — `onValueChange: async checked => …` is CS1003 cascade; name it `isChecked`. Same for `event`, `params`, `lock`, `default`, `base`, `new`, `object`, `string`.
- **A ternary of two whole style arrays needs a target type** — `var style = cond ? [...] : [...];` is CS0173 (no common type). Give it one: a typed local (`string[] style = cond ? [...] : [...];`), passing the ternary directly into the style parameter (`view.Box(cond ? [...] : [...], …)` compiles — the parameter supplies the type), or make the ELEMENT conditional: `view.Box(["rounded p-2", isActive ? "bg-active" : "bg-card"], …)`. Diagnostic: this pitfall is CS0173 on a `var` local; CS1003/CS1525/CS1026 near a style array is an unescaped quote, unbalanced brace, or reserved-word param instead.
- **`onOpenChange` hands you a plain `bool`** — `async open => { _open.Value = open; }`; `open ?? false` is CS0019.
- **Convenient param shapes exist — don't hunt or invent.** Dialog takes `title:`/`description:` AND a `content:` lambda (+ `titleStyle:`/`descriptionStyle:`/`headerStyle:`). AlertDialog takes `title:`/`description:`/`content:` too, plus `cancelLabel:`/`actionLabel:`/`onAction:` — its slot styles are `titleStyle:`/`descriptionStyle:`/`footerStyle:`/`cancelStyle:`/`actionStyle:`, and NO `headerStyle:` (CS1739; that param is Dialog-only). Link/Button/ActionButton take `text:` for the display string (with a `content:` lambda, `text:` becomes the aria-label). The whole field family takes `label:` (renders above the control); Checkbox/Switch/Toggle render `label:` as the clickable trailing text. `view.Button("Save", onClick: …)` (text-first), `view.Heading("…")`, and `view.Markdown("# hi")` work positionally — but lambdas are ALWAYS named (a positional one is CS1744/CS1503, or build warning IKON003 where it compiles). TextField/TextArea take `debounceMs:`.
- **Size constants**: `Icon` takes `size:` like Spinner does (`view.Icon(name: "check", size: IconSize.Lg)`); the token form (`view.Icon([Icon.Sm], name: "check")`) still works and wins when both are given. Buttons bake variant+size into one constant: `Button.PrimaryMd`, `Button.ErrorMd` (= `Button.DestructiveMd`), `Button.OutlineErrorMd` (outlined destructive), `Button.GhostErrorMd` — no `Button.Danger*`, CS0117; no `variant:`/`size:` params, CS1739). Text uses semantic constants (`Text.Body`, `Text.Caption`, `Text.H1`…) or Tailwind classes — no `Text.Xs`/`Sm`/`Button.Sm` (CS0117).
- **`view.Spinner()` is built in** — `if (_loading.Value) { view.Spinner(); }` (optional `size: SpinnerSize.Lg`, style array to recolor).
- **`ScrollArea`**: bare `style:` = outer wrapper; `viewportStyle:` = scrollable content area. `autoScrollKey:` takes the thing that changes — the reactive collection itself (`autoScrollKey: _messages`), a count (`autoScrollKey: _messages.Count`), or a composite string; no `.ToString()` ceremony.
- **`view.Image`: style array first, source NAMED** — `view.Image(["w-72"], src: url, alt: "…")` or `(…, data: bytes, mimeType: "image/png")`. URL-first is CS1503; no `classes:` param.
- **Copy/share/download are declarative `view.ActionButton` actions**, not `ClientFunctions` calls (there is no `ClientFunctions.CopyToClipboardAsync`/`RunJavaScriptAsync` — CS0117): `view.ActionButton([…], action: ActionKind.CopyToClipboard, options: new CopyToClipboardActionOptions { Text = theText }, content: …)`. Same shape for `ActionKind.Share` and `ActionKind.DownloadFile` — `DownloadFileActionOptions { Filename = "notes.md", Data = bytes }` (`Data` is `byte[]`; `Filename` lowercase n — anything else is CS0117). Notifications are server-initiated via `app.Notifications`, never an ActionButton.
- **Interactive elements need findable handles** — `label:`/`placeholder:` on fields, a clear label on buttons, `key:` on dynamic list rows. Real users with assistive tech AND the app validator's element marking depend on them. If a slider's value matters, pair it with labeled +/- buttons.
- **`hover:` never fires on touch devices** — hover-REVEALED controls (`opacity-0 group-hover:opacity-100`) are unreachable on mobile. Pair with `pointer-coarse:opacity-100` or `focus-within:opacity-100`. Plain hover feedback needs no fallback.

### Custom frontend and real-time

- **Native `view.*` first; a custom component when Parallax lacks the piece — and then just build it.** The built-in set covers most product UI, and the build type-checks it, so start there. But when the product needs something Parallax does not have — a rich text or code editor, a map, a 3D view, a specific chart library, a timeline, a signature pad — do not substitute a worse native approximation or drop the feature: read the `frontend-fundamentals` guide and the `custom-react-node-embed` pattern, pull in the npm package, and write all four parts (React component, resolver, `registerModule` in `app.tsx`, C# extension method) in the same change. A missing part never renders blank — the client shows a red "Unregistered node type" placeholder and logs which of the four is missing — so there is nothing to fear in the round-trip. Static charts and diagrams that need no interaction can also be rendered server-side into `view.Image([…], data: bytes, mimeType: "image/svg+xml")`.
- **THE exception: real-time MULTI-USER surfaces** (shared whiteboard, per-frame multiplayer game) use a custom canvas + a `schema/<Name>.tp` message type — native `view.*` has no client→client live channel, and driving a playfield through `Reactive` UI diffs floods the channel. Pattern: (a) `schema/<Name>.tp` at the app root — `type = "<Name>"`, `version = 1`, `unreliable = true` for drop-tolerant streams, then `[fields]` (`"string"`, `"int32"`, `"float32"`, `"bool"`, `"Point[]"`; a `[nested.Point]` table for a sub-record); NO `opcode` (assigned) and NO `[namespaces]` (the default `<App>.Protocol` is right — naming it after your class is CS0101). It generates `app/<App>/Generated/Protocol/<Name>.cs` — reference the type directly; (b) server is the router: `app.OnMessage<T>((payload, senderId) => …)` (ONE arg, no name string) to receive, `app.SendMessageAsync(payload, targets)` (TWO args — message + `app.Clients.Ids.ToList()` or an int id) to fan out; tick loop via `_ = Task.Run(async () => { while (running) { Tick(); await app.SendMessageAsync(Snapshot(), app.Clients.Ids.ToList()); await Task.Delay(100); } })` (`app.BackgroundWork` is a property, not a method); (c) the canvas mounts ONCE via `AddNode` — it is NOT the per-tick data channel; per-tick state flows over `.tp`, rendered locally. Keep lobby/score/controls in native `view.*`. Reference app: **Ikon.App.Arena**; read the `multi-user-game` pattern before writing it.
- **Real-time frontend wiring**: `appMessaging` from `@ikonai/sdk`; renderer types (`UiComponentRendererProps`, `useUiNode`, …) from `@ikonai/sdk-react-ui` (there is NO `@ikonai/sdk-react`); generated codecs from `'../generated/protocol/<name>'`. Inside a node component get the client from renderer props (`const client = context.client;`) — `useIkonApp()` there does NOT throw; it silently spins up a SECOND IkonClient connection (the hook owns the entire connection lifecycle), which is worse than a crash. `.on()`/`.send()` take `AppMessageType<T>` descriptors built from the generated codec (`{ opcode: X_OPCODE, toProtocolMessage: …, fromProtocolMessage: … }`) — a string opcode name silently never fires. Unsubscribe with `sub.close()`. Use the TYPED server API — never `app.OnMessageReceived(msg => app.SendMessageAsync(msg))` blind re-broadcast.
- **Single-user drawing/annotation = native `view.ImageEditorCanvas`** (renderer pre-registered): `view.ImageEditorCanvas(style: […], src: url, brushColor: …, brushWidth: …, tool: ImageEditorTool.Brush, onSave: async args => …, onHistoryChange: async args => …)`. `tool:` is the `ImageEditorTool` ENUM — `Brush`, `Eraser`, `Text`, `Arrow`, `Region` — never a string (CS1503). Undo/redo/save fire by bumping the `triggerUndo:`/`triggerRedo:`/`triggerSave:` int props.

### Audio, files, media

- **Audio is synthesized SERVER-side and streamed** — no browser Web Audio, no client JS synthesis. Build PCM `float[]` (by hand or `Ikon.Resonance.Synth`) and stream: `private Audio Audio { get; } = new(app);` then `await Audio.PlayClipAsync(MediaTargets.Everyone, samples, sampleRate, channels, streamId: id)` (real-time paced — safe for clips of any length; `Audio.SendFrameAsync(targets, ...)` is the unpaced variant for short one-shot clips and audio already produced in real time). `AudioChunk` carries `float[] Samples`/`SampleRate`/`ChannelCount` (no `.Data`/`.MimeType`); speak text with `await Audio.SpeakAsync(MediaTargets.Everyone, text)` (one call, interrupts the previous utterance; optional `model`, `voice:`, `instructions:`, `speed:`) and play hand-rolled `SpeechGenerator` chunks with `Audio.SpeakChunk(targets, audio)` only for generator config `SpeakAsync` does not expose, or raw sample access. `ClientFunctions.PlaySoundAsync(byte[], mime)` is only for already-encoded MP3/WAV.
- **Two audio lanes, and the method name says which.** `Speak*` (`SpeakAsync`, `SpeakAndWaitAsync`, `SpeakChunk`) goes through the app's single speech mixer: one utterance at a time for the WHOLE app, and starting another fades out the one playing — it interrupts even when the two calls target different clients. `Send*`/`Play*` (`SendFrameAsync`, `PlayClipAsync`, `Video.SendFrameAsync`) go straight to the wire as independent streams keyed by `streamId:`, and overlap freely, including with speech. So two voices at once is `PlayClipAsync` on two stream ids — NOT two `SpeakChunk` calls, which interrupt each other. Both lanes send only when somebody is connected to hear them: with no client in the session the call returns without transmitting, and `SpeakAsync` returns without buying the speech, so an app left running does not narrate to an empty room at full price.
- **`FileUpload`'s `accept:` is a string ARRAY** — `accept: ["image/*"]`, never the HTML-attribute string form `accept: "image/*"` (CS1503: cannot convert from 'string' to 'string[]?'). Multiple types are separate elements: `accept: ["image/*", ".pdf"]`.
- **File upload: `onUploadStart` decides WHERE the bytes land — the two destinations are exclusive.** `FileUploadCompleteArgs` carries both `LocalTempFilePath` and `AssetUri`, and exactly ONE of them is non-null. (`onChunkReceived` fires per chunk with a `FileUploadChunkArgs` — `UploadId`, `FileName`, `MimeType`, the announced `Size`, this chunk's `Data`, and `BytesWritten` so far — for a progress bar. Its `Data` is valid only for the duration of the callback; copy it if you keep it. `onUploadComplete` fires only after the byte count and a recomputed SHA-256 both match.)
  - **Asset storage (the path you want for anything you keep, and mandatory for media):** return an `AssetUri` from `onUploadStart` and the bytes stream straight into asset storage. `onUploadStart` returns `Task<FileUploadResult>`: `onUploadStart: async args => new FileUploadResult { AssetUri = new AssetUri(AssetClass.CloudFilePublic, path, app.GlobalState.SpaceId) }`. Then `onUploadComplete` gives you `args.AssetUri` (and `args.LocalTempFilePath` is null) — the same struct every `Asset.Instance.*` op takes, nothing to parse: `if (args.AssetUri is { } assetUri) { var bytes = await Asset.Instance.GetBytesAsync(assetUri); }`. Store it as `Reactive<AssetUri?>`.
  - **Local temp file (fine for a small image/document you consume once):** omit `onUploadStart` (auto-accept) — or accept without an asset with `onUploadStart: async args => true` (never `Task.FromResult(true)` — CS0029). The file is written to a temp directory: `args.AssetUri` is NULL and `args.LocalTempFilePath` has the path — `var bytes = await File.ReadAllBytesAsync(args.LocalTempFilePath!);`. The temp directory is deleted when the app stops, so copy anything you want to keep.
  - Reading `args.AssetUri` with no `onUploadStart` supplying one is the classic silent no-op: the `if (args.AssetUri is { } …)` body simply never runs. Unwrap an `AssetUri?` with a pattern or `.Value` after a null check, never `!`.
- **Never copy video/audio files into the app container** (disk/RAM far too small). Upload straight to asset storage via an `AssetUri` from `onUploadStart`, then analyze through `(await Asset.Instance.GetMetadataAsync(uri)).Url` — a signed URL the preinstalled `ffprobe`/`ffmpeg` read over HTTP (the parens matter: without the await, `.Url` on the `Task` is CS1061). Never `GetBytesAsync` a media asset.
- **App files: root `public/` (shown, by URL path) and `data/` (private) — `app.Files` is the runtime API.** A static file a browser loads lives in the app root's `public/` folder and is referenced by its URL path (`view.Image(["w-full"], src: "/hero.png")`); a private file the code reads lives in root `data/`. At runtime `await app.Files.Data.ReadTextAsync("rules.md")` reads a seeded or runtime-written file, and `await app.Files.Public.WriteBytesAsync($"thumbnails/{id}.png", bytes, "image/png")` + `await app.Files.Public.GetUrlAsync(...)` stores and serves generated media — runtime writes persist across deploys. Never write into `app.DataDirectory` (read-only in cloud), and never regenerate a file that already exists in `public/` or `data/`.

### App structure and platform

- **`IApp` is generic; the records are `public`.** Canonical: `[App] public class MyApp(IApp<SessionIdentity, ClientParameters> app)` with `public record SessionIdentity(string? UserId)` / `public record ClientParameters(string Name = "Anonymous")`. Bare `IApp` is CS0305; non-public records are CS0051. The scaffold already has this shape — preserve it.
- **`Main()` returns after `UI.Root(...)`** — no `await Task.Delay(Timeout.Infinite)` (a Main that never returns fails the boot at the server start timeout with a TimeoutException naming `<YourApp>.Main()`; ongoing work belongs in event callbacks or background tasks).
- **Demo/seed content**: shared state (`Reactive`/`PersistentReactive`) seeds in `app.OnStarting` when the store is empty; per-user state (`UserReactive`/`PersistentUserReactive`) MUST seed in `app.OnClientJoined` when that user's store is empty — touching a user-scoped field in `OnStarting` crashes the boot (no user scope exists yet).
- **A value the app presents as unique is GENERATED, never a literal.** Room codes, join codes, invite codes, share slugs and ids are computed at runtime (`Random.Shared`, `Guid`) — a code hardcoded to a readable placeholder ships as the real thing, because the first draft IS the demo and this audience cannot read the code to catch it. Observed shipped failure: a party game whose join code read `GAME`, beside two demo players with the same name. Seeded demo identities are distinct from each other, and demo rows look like real data, not `Item 1` / `Item 2`.
- **Lifecycle via the extension helpers**: `app.OnStarting(async () => …)`, `app.OnStopping`, `app.OnClientJoined(async ctx => …)` (or `(ctx, parameters)` typed overload), `app.OnClientLeft`, `app.OnMessageReceived(async msg => …)`. The `(sender, args)` prior doesn't match (CS1593); the arg types omit the `App` prefix.
- **Database access opens a connection PER operation** — `await using var conn = await app.DatabaseAsync("mydb"); …` per method, never a `DbConnection` field. Create tables in `app.OnStarting` (`CREATE TABLE IF NOT EXISTS`). Beyond a flat table or two prefer EF Core — same per-operation rule for the `DbContext`, `MigrateAsync` in `OnStarting`. Database `"X"` requires `ikon app db create --name X`; the built-in one needs nothing.
- **Do not INVENT a `using Ikon.*;`** — the common namespaces are already in `GlobalUsings.cs` and a made-up one is CS0234. But a real nested namespace is NOT imported by its parent, so a capability that lives in one needs its own line, and the symptom is CS0246/CS0103 on a type that plainly exists. The ones a `GlobalUsings.cs` does not cover: `Ikon.AI.Emergence.Tree` (TreeIndex, tree search), `Ikon.AI.Emergence.Structured` (StructuredTagParser), `Ikon.AI.ImageUpscaling`, `Ikon.AI.MusicGeneration`, `Ikon.AI.ImageSegmentation`, `Ikon.AI.DepthEstimation`, `Ikon.AI.MeshGeneration`, `Ikon.AI.Reranking`, `Ikon.AI.Retrieving`, `Ikon.AI.Database`, `Ikon.AI.Storage`, `Ikon.AI.Provenance`, `Ikon.AI.Shader`, `Ikon.AI.Utils`, `Ikon.App.Cells`, `Ikon.App.Cron`, `Ikon.App.Triggers` (TriggerContext), `Ikon.Common.Core.Protocol` (TriggerEventType), `Ikon.App.Mcp`, `Ikon.Connectors`, `Ikon.Connectors.Google`, `Ikon.Connectors.Browser`, `Ikon.AI.Shader.Scriban`, `Ikon.Crosswind`, `Ikon.Common.Core.Email` (EmailSendRequest and friends), `Ikon.Common.Assets`, `Ikon.Sdk`, and the pipeline family — `Ikon.Pipeline`, `Ikon.Pipeline.Items`, `Ikon.Pipeline.Spec`, `Ikon.Pipeline.State`, `Ikon.Pipeline.ContentCache`, `Ikon.Pipeline.Remote.Bus`, `Ikon.Pipelines.Public.Examples`, `Ikon.Pipelines.Public.Processors.Json`, `Ikon.Pipelines.Public.Processors.OCR`, `Ikon.Pipelines.Public.Processors.Pdf`, `Ikon.Pipelines.Public.UniversalRag`, `Ikon.Pipelines.Public.UniversalRag.Processors`, `Ikon.Pipelines.Public.UniversalRag.Shaders`, `Ikon.Pipelines.Public.UniversalRag.Utils`, `Ikon.Pipelines.Public.VideoImageSafety`, `Ikon.Pipelines.Public.VideoImageSafety.Shaders`. Third-party NuGet packages DO still need their own `using`.
- **The message author type is `Author`, not `MessageAuthor`** (CS0246 — `MessageAuthor` does not exist). It is a flat `readonly record struct Author(AuthorKind Kind, string? Name = null)` with equality by (Kind, Name): compare `msg.Author == Author.User` (static field) or switch on `msg.Author.Kind` (`enum AuthorKind { User, Agent }`); an agent author comes from the factory `Author.Agent("researcher")`. Appending takes TWO args and the text rides in `Parts`: `await storage.AppendMessageAsync(threadId, new Message(Author.User, [new Content.Text(reply)]));`.
- **`using` directives precede the top-level `return await App.Run(args);`** (CS1529) — and never delete that entry-point line (CS5001), never move it into a `Program.cs` of your own, and never add a `namespace` to the app files: the scaffold's types live in the GLOBAL namespace, and inside a `namespace Ikon.App.<Anything>` the name `App` resolves to the `Ikon.App` namespace, not the `App` class — `App.Run` and `App<…>` become CS0234/CS0305/CS8956 in a spiral of fixes. New partial files go beside the main one, namespace-free. Prefer `GlobalUsings.cs` for shared namespaces.

### Color tokens and theming

- **`bg-primary` is NOT the brand fill** — in the Ikon semantic set it is the PAGE SURFACE token (white in light, near-black in dark; shadcn instinct is wrong here). Brand-colored fill is `bg-brand-solid`, tone-tinted text is `text-brand-secondary`, tone border is `border-brand`; `text-primary` is the READING color and `border-primary` the neutral hairline. Using `bg-primary` for a button renders invisible on one scheme and looks fine on the other — the worst kind of bug. Crosswind logs a warning when it sees it.
- **A hardcoded palette needs `Mode = ThemeMode.Fixed`.** Pinning surface colors (`["background"]`, `["card"]`, `["foreground"]`) while the theme stays Adaptive with no `DarkMode` pair leaves a dark-preference client with the platform's dark TEXT defaults on top of your light surfaces — light-gray on white, unreadable, and invisible on a light-mode dev machine. One committed look → `ThemeMode.Fixed`; both schemes → provide `DarkMode`.
- **Semantic tokens are role names; a numeric shade on a role derives from it.** `text-brand-solid`, `bg-error-solid`, `text-tertiary` are the tokens. `primary-600`, `brand-100`, `error-700`, `success-50` (roles: primary/brand, accent, error/destructive, success, warning, info) are the role's colour mixed toward white below 500 and black above — the shadcn ladder, without a palette to define. Other names take no shade (`text-tertiary-600` is IKON006). Any semantic token works in any colour utility (`text-brand-solid`, `bg-muted-foreground/20`), as in Tailwind 4.
- **ALWAYS author Tailwind v4 class names.** Crosswind tracks the v4 scale; the baseline is value-identical to stock Tailwind for spacing, type, and every radius rung, so v4-authored classes render exactly their Tailwind meaning with zero theme keys. Most training-data Tailwind is v3, where several utilities sat one step up — writing from v3 instinct renders heavier shadows and rounder corners than intended. The mapping:

  | v3 wrote | v4 writes | v3 wrote | v4 writes |
  |---|---|---|---|
  | `shadow-sm` | `shadow-xs` | `rounded-sm` (any corner form) | `rounded-xs` |
  | `shadow` | `shadow-sm` | `rounded` | `rounded-sm` |
  | `blur-sm` / `blur` | `blur-xs` / `blur-sm` | `drop-shadow-sm` / `drop-shadow` | `drop-shadow-xs` / `drop-shadow-sm` |
  | `backdrop-blur-sm` / `backdrop-blur` | `backdrop-blur-xs` / `backdrop-blur-sm` | `outline-none` | `outline-hidden` |
  | `ring` | `ring-3` | `bg-opacity-50` (removed) | `bg-black/50` slash syntax |

  When porting a v3-era Tailwind codebase (Replit/Lovable/Base44/v0 exports), apply this mapping ON THE FLY as you transcribe — every class you WRITE is v4, whatever the source said. The same applies to any source files you copy in verbatim: translate their class strings as part of bringing them over. Verify by rendering, not by trust: compare computed styles against the original running side by side. Shadow rungs are themable (`["shadow-lg"] = "0 2px 4px rgb(0 0 0 / 0.07)"`, at most two layers) and `shadow-lg shadow-red-500` recoloring keeps working on top of the themed rung.
- **TextField `label:` renders a VISIBLE label above the field.** When the design has no label, omit it — the `placeholder:` already provides the findable handle; passing `label:` anyway paints stray text the design never asked for (the classic symptom: a floating "Message" above a chat composer).
- **Keep small text at AA contrast.** On light surfaces, `text-muted-foreground` at reduced opacity (`/60`–`/80`) and mid-amber text like `#C8791A` fall under the 4.5:1 floor at 9–13px. Use full-strength muted for small labels, and darken accent TEXT (e.g. amber `#8F5710`) while keeping the brighter accent for borders, dots and fills; white-on-accent button fills need the darker step too (e.g. `#A9640D`).

### C# string literals

- **ASCII `"` delimiters only** — a curly `“”‘’` used AS a string/char delimiter is CS1056 ("Unexpected character", with CS1525 cascades). Inside a properly `"`-delimited literal, curly quotes are just characters and compile fine.
- **Escape inner quotes in `$"…"`** — `$"Add card to \"{col}\""` or drop them; an unescaped `"` desyncs the whole file into a CS1002/CS1010/CS1026 cascade (fix the one quote, not the phantom lines). Many literal quotes → raw string `$"""…"""`.
- **Raw-string `$` count matches brace nesting**: interpolations + literal `{}` (JSON in prompts) → `$$"""…"""` with `{{x}}` interpolations; interpolations only → `$"""…"""` with `{x}`; static → `"""…"""`. Mismatch is CS9006/CS9007.

### Runtime blank-screen triage

**Build-clean but the page shows nothing** — check in order: (1) the root doesn't fill the viewport → `UI.Root([Page.Default], content: view => { view.Column(["min-h-screen w-full …"], …); })` (don't rely on an arbitrary `h-[100dvh]` on an inner div); (2) white-on-white → commit a theme (`new IkonTheme { ["background"]=…, … }`) and paint on `bg-background`/`bg-card`; (3) a custom `view.AddNode` whose resolver is missing now renders a red "Unregistered node type" placeholder, not a blank — if you see one, register the module or switch to native components. Fix root/height/background first — don't re-style individual cards chasing it.

## API Reference Guides

Topic guides live in `docs/Ikon.Agent.Docs/guides/`. Each covers one subject:

- **app-structure** (`docs/Ikon.Agent.Docs/guides/app-structure.md`): app file structure, session identity, client parameters, partial class, global usings, lifecycle, host services, navigation, background work, client functions, messages, minimal app template, viewport layout, auto-scroll, QR code, join URL, multi-user session, invite link
- **csharp-primer** (`docs/Ikon.Agent.Docs/guides/csharp-primer.md`): C# 14, modern C#, dictionary literal, collection expression, primary constructor, raw string literal, async, await, ValueTask, IAsyncEnumerable, target typing, nullable reference types, records, pattern matching, file-scoped namespace, top-level statements, modern idioms, enterprise patterns, abstractions, factory, IUnitOfWork, dependency injection, mock, interface, abstract base class, syntax error, CS1003, CS1525, CS1026, CS0173, CS8917, CS0234
- **app-api-reference** (`docs/Ikon.Agent.Docs/guides/app-api-reference.md`): IApp, host services, server API, navigation, session, common utilities
- **reactive-state** (`docs/Ikon.Agent.Docs/guides/reactive-state.md`): reactive, client reactive, user reactive, persistent reactive, persistent session reactive, persistent user reactive, persistence backend, postgres backend, public asset backend, reactive scope, value mutation
- **ui-components** (`docs/Ikon.Agent.Docs/guides/ui-components.md`): layout, overlays, inputs, display, navigation, drag-and-drop, text, button, dialog, tabs, accordion, scroll area, toast, popover, chat interface, message bubbles
- **ui-api-reference** (`docs/Ikon.Agent.Docs/guides/ui-api-reference.md`): component parameters, shared parameters, props, style, styleId, key, ariaLabel, content, value, defaultValue, forceMount, loop, controlled, uncontrolled, where are the signatures, method signatures
- **parallax-overview** (`docs/Ikon.Agent.Docs/guides/parallax-overview.md`): parallax, server-driven UI, reactive UI, UI.Root, setting up a UI, component catalogue, which component, two-way binding, styling with crosswind, scroll area, auto-scroll, panzoom, boot snapshot, skeleton, SEO, architecture
- **component-vocabulary** (`docs/Ikon.Agent.Docs/guides/component-vocabulary.md`): component enum, component record, event args type, callback argument type, ActionEvent, CaptureImageMode, DragEndArgs, ToastItem, CarouselBreakpoint, CellType, CheckedState, FormMessageMatch, ExpandedSet, ScrollNearEndArgs, LineCurve, LegendConfig, ChartClickArgs, UIViewNode
- **charts** (`docs/Ikon.Agent.Docs/guides/charts.md`): chart, charts, pie chart, bar chart, line chart, donut chart, data visualization, graph, plot, sparkline, analytics dashboard, PieChart, BarChart, LineChart, PieChartDatum, LineChartSeries, LineChartPoint, series, axis, legend
- **styling-and-motion** (`docs/Ikon.Agent.Docs/guides/styling-and-motion.md`): crosswind, tailwind, theme constants, style arrays, motion, animation, UI guidelines, theme customization
- **ikon-theming-guide** (`docs/Ikon.Agent.Docs/guides/ikon-theming-guide.md`): theming, IkonTheme, brand palette, color scale, dark mode, design tokens, indexer, theme customization, brand commitment, mood, paint a fresh app, palette by hand, dual theme
- **styling-guide** (`docs/Ikon.Agent.Docs/guides/styling-guide.md`): crosswind guide, UI design patterns, common pitfalls, sophisticated UI, layout patterns, gradient, overlay, CRT, scanline
- **motion-reference** (`docs/Ikon.Agent.Docs/guides/motion-reference.md`): motion spec, keyframe, animation, timing, staggered text, 3D transform, filter animation, animatable properties, motion syntax grammar
- **tailwind-reference** (`docs/Ikon.Agent.Docs/guides/tailwind-reference.md`): tailwind spec, utility classes, layout, flexbox, grid, spacing, typography, backgrounds, borders, effects, shadows, transitions
- **emergence** (`docs/Ikon.Agent.Docs/guides/emergence.md`): emergence, emerge run, structured output, json, tools, agent, bestof, mapreduce, patterns, cancellation, timeout
- **agent-threads** (`docs/Ikon.Agent.Docs/guides/agent-threads.md`): agent, AgentThread, Orchestrator, Persona, AgentApp, AgentPlan, sub-agent, spawn, AgentCall, RunSubAgentAsync, AgentCallSpec, artifact, Artifact, attachment, budget, BudgetRemaining, ThreadOptions, user decision, ask the user, long-running agent, multi-turn agent
- **agent-stages-and-history** (`docs/Ikon.Agent.Docs/guides/agent-stages-and-history.md`): stage machine, IStageMachine, SkillSet, StageTransitionResult, FSM, agent stages, pass record, PassMessage, PassToolCall, ContextSnapshot, StreamedContent, journal, JournalEntry, JournalCodec, checkpoint, ThreadCheckpoint, Checkpoints, ThreadSnapshot, PlanSnapshot, AppSnapshot, resume, replay
- **emergence-patterns** (`docs/Ikon.Agent.Docs/guides/emergence-patterns.md`): mapreduce, treesearch, solver critic, refine, ensemble, bestof, advanced patterns
- **ai-models** (`docs/Ikon.Agent.Docs/guides/ai-models.md`): LLM model, model selection, Claude, Gemini, GPT, Grok, model enum, KernelContext, AI connection
- **ai-image** (`docs/Ikon.Agent.Docs/guides/ai-image.md`): image generation, ImageGenerator, AI image, photo, generate image, ImageGeneratorConfig, upscale, upscaling, super resolution, ImageUpscaler
- **ai-speech-and-audio** (`docs/Ikon.Agent.Docs/guides/ai-speech-and-audio.md`): speech, TTS, STT, voice, transcribe, whisper, sound effect, SpeechGenerator, SpeechRecognizer, SoundEffectGenerator
- **ai-video** (`docs/Ikon.Agent.Docs/guides/ai-video.md`): video generation, video enhancement, AI video, VideoGenerator, VideoEnhancer, video playback, video player, play video, display video, inline video, VideoUrlPlayer, autoplay, poster, show a video
- **ai-web-and-data** (`docs/Ikon.Agent.Docs/guides/ai-web-and-data.md`): web search, scrape, crawl, classify, OCR, embedding, vector, retrieve, rerank, file convert, WebSearcher, EmbeddingGenerator
- **ai-advanced** (`docs/Ikon.Agent.Docs/guides/ai-advanced.md`): database AI, vector store, storage
- **audio-video** (`docs/Ikon.Agent.Docs/guides/audio-video.md`): audio, video, capture, speech, stream, effects, reverb, delay, mixer, synthesizer, oscillator, filter
- **asset-system** (`docs/Ikon.Agent.Docs/guides/asset-system.md`): asset, cloud file, local file, cloud json, storage, metadata, URI, optimistic concurrency
- **media-analysis** (`docs/Ikon.Agent.Docs/guides/media-analysis.md`): ffmpeg, ffprobe, video analysis, audio analysis, media metadata, duration, codec, transcode, extract frames, extract audio, thumbnail, waveform, large file, video upload, audio upload, streaming analysis, media file, video file, audio file
- **endpoints-webhooks** (`docs/Ikon.Agent.Docs/guides/endpoints-webhooks.md`): endpoints, webhooks, HTTP, HTTPS, REST, MCP, HttpGet, HttpPost, HttpPut, Mcp, WebSocket, TCP, TLS, UDP, public URL, tunneling, AppEndpointHost, function
- **databases** (`docs/Ikon.Agent.Docs/guides/databases.md`): databases, PostgreSQL, SQL, db, app.Database, AppDatabaseConnection, EF Core, Entity Framework, DbContext, migrations, db migrate, LINQ
- **secrets** (`docs/Ikon.Agent.Docs/guides/secrets.md`): secrets, tokens, API keys, credentials, passwords, app.Secrets, ikon app secret
- **payments** (`docs/Ikon.Agent.Docs/guides/payments.md`): payments, charge end users, monetize, paywall, subscription, recurring, one-off payment, refund, Stripe, Surfboard, Mollie, app.Payments, CreatePaymentLinkAsync, ListOffersAsync, offers, GetEntitlementAsync, entitlement, PaymentsRequireEntitlement, CreateOfferAsync, create offer, PaymentEventReceived, payment events, cancel subscription
- **notifications** (`docs/Ikon.Agent.Docs/guides/notifications.md`): notifications, push, push notification, app.Notifications, NotificationContent, SendToUserAsync, SendToSessionAsync, BroadcastAsync, permission, offline push, web push, FCM, alert, toast, NotificationInbox, inbox, in-app notifications, unread, NotificationRoute, NotificationReach, all devices, multidevice, INotificationChannel, email notification, SMS notification, Telegram, WhatsApp, mute
- **email** (`docs/Ikon.Agent.Docs/guides/email.md`): email, send email, app.Email, EmailSendRequest, EmailAttachment, sender identity, senderLocalPart, senderDisplayName, senderDomain, verified sending domain, reply-to, attachments, inbox, inbound email, EmailSenderNotAvailableException
- **telephony** (`docs/Ikon.Agent.Docs/guides/telephony.md`): sms, text message, send sms, phone call, voice call, app.Telephony, SmsSendResult, PlacedCall, TelephonyStatus, phone number, E.164, replyable, hang up, inbound sms
- **location** (`docs/Ikon.Agent.Docs/guides/location.md`): location, gps, geolocation, app.Locations, LocationService, StartTrackingAsync, StopTrackingAsync, OnUpdate, LocationUpdate, LocationTrackingOptions, background location, continuous location, live tracking, tracker app, courier tracking, delivery tracking, ride tracking, foreground service, background mode, GetLocationAsync
- **function-registry** (`docs/Ikon.Agent.Docs/guides/function-registry.md`): function registry, registration, attribute, visibility, LLM tools, callable functions
- **profiles-and-roles** (`docs/Ikon.Agent.Docs/guides/profiles-and-roles.md`): profile, user profile, roles, admin, moderator, RequireRole, ClientProfiles, ClientProfile, ProfileData, UserRole, RoleRequiredException, custom attributes, IProfileAttributes, MintedUserToken, name, email, address
- **costs** (`docs/Ikon.Agent.Docs/guides/costs.md`): cost, costs, credits, spend, budget, usage, billing, app.Costs, CostsService, CostQuery, DailyCost, CostScopeFilter, how much did this cost, per-user cost, attribution
- **platform-runtime-types** (`docs/Ikon.Agent.Docs/guides/platform-runtime-types.md`): Toml, TOML config, ServerRunType, local or cloud, SdkType, ProtocolVersion, NameConversions, kebab case, slug, camel case, ExtendedCast, UserException, BackendQuotaExceededException, quota, IMessageChannel, SnapshotCapture, boot snapshot, UIStreamBegin, VideoStreamBegin
- **utilities** (`docs/Ikon.Agent.Docs/guides/utilities.md`): retry, Retrier, backoff, transient failure, embedded resource, Resources, Levenshtein, StringDistance, fuzzy match, ILoggerProvider, IkonLoggerProvider
- **logging** (`docs/Ikon.Agent.Docs/guides/logging.md`): log, logging, debug, warning, error, diagnostics
- **pipelines** (`docs/Ikon.Agent.Docs/guides/pipelines.md`): pipeline, background processing, transform, processor, scheduled, cron
- **pipelines-reference** (`docs/Ikon.Agent.Docs/guides/pipelines-reference.md`): pipeline API, transform, processor, pipeline guide
- **frontend-fundamentals** (`docs/Ikon.Agent.Docs/guides/frontend-fundamentals.md`): frontend, SDK, auth, connection, i18n, styling, query params, custom UI component, module, resolver, React, magic link login email template, emails folder
- **config-and-cli** (`docs/Ikon.Agent.Docs/guides/config-and-cli.md`): ikon-config.toml, CLI commands, build, deploy, run, auth, activation, targets


## API Signatures

Exact signatures for every public platform type live in `docs/Ikon.Agent.Docs/api/`, one file per namespace (`Ikon.Parallax.Components.Standard.md`, `Ikon.App.md`, …), with a `~2`, `~3` suffix where one namespace needs several. These are generated from the compiled assemblies, so they are the authority on a parameter list, a default value or a return type whenever a guide and a signature disagree.

Grep that directory by type or member name before guessing a signature — it is what turns a CS1739 or CS1061 into a one-line fix.


## Code Patterns

`docs/Ikon.Agent.Docs/patterns/` holds short, self-contained patterns lifted from production Ikon AI Apps, each one compiled against the real assemblies and render tested, so a pattern cannot drift from the API it uses.

Start at `docs/Ikon.Agent.Docs/patterns/_index.md`, which groups every pattern by the task it solves. Adapting the closest pattern is faster and safer than writing a screen or a flow from scratch.


<!-- ikon-user-content-below -->
