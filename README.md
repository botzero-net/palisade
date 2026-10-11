# Palisade

**A user-first privacy & security hardening wizard for Windows 11.**

Palisade walks you through the questions Windows setup should have asked — the
ones that decide how much of you leaves the machine and how exposed the box is —
then applies your choices with documented system settings. Every change is
explained before it happens, journaled with its previous value, and reversible
with one click.

Free, open source (GPL-3.0), no telemetry, no upsell, no "pro" tier.

---

## Download

Grab `Palisade.exe` from the latest
[GitHub Release](https://github.com/botzero-net/palisade/releases).
It's a single self-contained file — no installer and no .NET runtime needed.

Windows will show a UAC prompt (the tool needs administrator rights to change
system policy) and possibly a SmartScreen warning while the binary builds a
reputation — the entire build is reproducible from this repository via GitHub
Actions, so you can verify exactly what went into it.

## What it does

1. **Welcome** — what Palisade changes and how rollback works.
2. **Checkup** — read-only snapshot: antivirus products registered, Defender
   real-time/tamper/signature status, disk encryption, elevation.
3. **Protections** — a grouped checklist of ~24 hardening items across AI
   features, telemetry, ads/tracking, search, antivirus and system/network
   hardening. Every item expands to show *exactly* what it changes.
4. **Apply** — per-item progress with a live journal.
5. **Done** — summary, log location, one-click rollback.

### Highlights

- **AI & training data**: disables Windows Copilot, Recall/AI data analysis,
  inking & typing personalization, tailored experiences, online speech
  recognition.
- **Telemetry**: diagnostic data minimized (AllowTelemetry policies + DiagTrack
  / dmwappushservice disabled), feedback prompts, activity history.
- **Ads & tracking**: advertising ID, consumer features / suggested apps,
  Settings upsell cards, app-launch tracking, language-list profiling, widgets.
- **Search**: Bing/web results out of Start, Cortana policy off.
- **Antivirus**: verifies *some* AV is running; when Defender is the AV it
  ensures real-time protection, PUA blocking, cloud lookups and
  block-at-first-sight (with samples set to *ask*, not *send*). SmartScreen on.
- **System**: SMBv1 off, AutoRun/AutoPlay off, Guest account off, hardened UAC,
  LLMNR off — plus optional RDP, Remote Registry, HVCI, LSA protection and
  Office macro blocking.

The full list with rationale and the exact keys/services touched lives in
[docs/hardening-items.md](docs/hardening-items.md).

## Safety model

- **Nothing happens silently.** You see every change before applying.
- **Journal**: every applied action records what it replaced in
  `%ProgramData%\Palisade\journal.json`, plus a human-readable log per run.
- **Rollback**: the Rollback page (linked in the sidebar, always reachable)
  restores previous values — per session, or everything at once.
- **Read-only probe**: the checkup screen changes nothing.
- **Detection**: items already in the desired state are marked *already on* and
  skipped; items that don't apply to your device are marked *n/a*.

## Build it yourself

Requires Windows + the .NET 8 SDK:

```powershell
dotnet publish src/Palisade/Palisade.csproj -c Release -r win-x64 `
    --self-contained -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -o publish
```

CI builds the same way on `windows-latest` and runs
`Palisade.exe --self-test` (a non-UI pass that probes the system and runs
detection across the whole catalog without changing anything).

Other flags:

- `Palisade.exe --self-test` — JSON system/catalog report, exits 0/1.
- `Palisade.exe --list` — print the tweak catalog.

## Project layout

```
src/Palisade/
  Core/            tweak catalog, actions, engine, journal, probe
  Pages/           wizard pages (Welcome → Done) + Rollback
  Themes/          Windows 11-style theme
docs/              hardening item reference
.github/workflows/ CI build + release
```

## Contributing

Issues and PRs welcome — especially new hardening items. A good item is: safe
to apply on a home machine, explainable in two sentences, and cleanly
reversible. See the `Tweak` model in `src/Palisade/Core/`.

## Disclaimer

Palisade changes documented Windows settings on your own machine at your
request. It's shipped as-is (GPL-3.0); review the items, keep the journal, and roll
back anything that misbehaves.
