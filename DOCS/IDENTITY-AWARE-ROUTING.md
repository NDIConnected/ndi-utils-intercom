# Identity-Aware NDI Routing (v1.7+)

> **Status**: Authoritative specification.
> **Schema version**: `1` (see `<ndi_manager schema="1" />`).
> **Source of truth**: `Core/NDIManager.cs` (class `NDIChannel`), `Models/AppConfig.cs`, `Models/IntercomProductOptions.cs`.

This document specifies how each NDI Intercom instance exposes its identity on the NDI network so that an external **management / aggregation application** can group the 16 senders + 16 receivers belonging to the same instance, distinguish multiple instances on the same network, and open the per‑instance Web UI.

It also documents the **persistent receiver** lifecycle introduced in v1.7, which allows external NDI tools to control the receivers (connect / disconnect a source) via the Discovery Server.

---

## 1. Why identity is needed

A single NDI Intercom16 process publishes:

- **16 NDI senders** — one per intercom channel (microphone audio out on the network).
- **16 NDI receivers** — one per intercom channel, *persistent* (always advertised, even when not connected to any source).

Without identity, a receiver of these flows on the network sees 32 unrelated NDI endpoints. With identity, a management app can:

| Goal | How |
|---|---|
| Group all 32 endpoints of one instance | Same `(application_id, device_id)` |
| Distinguish multiple instances on the same machine | Distinct `device_id` (auto‑generated GUID) |
| Group "logically equivalent" instances on different machines | Same `application_id` (e.g. `Intercom_A`) |
| Distinguish sender vs receiver of the same channel | `role` attribute |
| Address a specific channel | `channel_index` (integer 1..N) or `channel_id` (`ch-N`) |
| Open the per‑instance Web UI from an NDI app (e.g. Studio Monitor) | `<ndi_capabilities web_control="…" />` |

NDI groups are **deliberately not used** for routing or aggregation in v1.7+. See [§ 7. Compatibility & migration](#7-compatibility--migration).

---

## 2. The two identity fields

Both are persisted in `AppConfig` (`%PROGRAMDATA%\NDI Intercom16\config.json` for the 16‑channel build, `…\NDI Intercom2\…` for the 2‑channel build).

### 2.1 `ApplicationId`

- JSON: `"applicationId"` (camelCase over the wire, PascalCase in C#).
- Purpose: **logical** application label. Operators can set the same value on multiple machines that should be grouped together (e.g. `Intercom_A`, `Intercom_B`, `Studio1_Director`).
- **Default**: `"Intercom_A"`.
- **Validation**: regex `^[A-Za-z0-9_.-]{1,64}$` (1–64 chars; ASCII alphanum, underscore, dot, hyphen). Whitespace is trimmed before validation.
- **Sanitization**: an invalid or empty value is silently replaced by the default `Intercom_A`. The sanitized value is written back to `config.json`.
- **Editable from the Web UI** (Settings → Application Identity → Application ID).

### 2.2 `DeviceId`

- JSON: `"deviceId"`.
- Purpose: **stable, unique** identifier of this Intercom instance on this machine.
- **Auto‑generated** on first run as `Guid.NewGuid().ToString("N")` (32‑char hex, no separators) if absent or whitespace.
- **Persistent**: written back to `config.json` immediately and never regenerated unless the config file is deleted or the field is wiped manually.
- **Validation on load**: same regex as `ApplicationId`. If it fails, the in‑memory value is normalized to `unknown-device` (the persisted value is left untouched in this case — sanitization happens at the boundary in `NDIManager`).
- **Not exposed in the Web UI** by design. Treat it as an opaque token managed by the application.

### 2.3 Worked example of `config.json`

```json
{
  "applicationId": "Intercom_A",
  "deviceId": "9f8c1d3a4b5e4f7a8c0d2e1f3a4b5c6d",
  "selectedMicrophone": "…",
  "selectedSpeaker": "…",
  …
}
```

---

## 3. How identity propagates to the NDI layer

For each of the N channels (N=16 for Intercom16, N=2 for Intercom2), the application creates one sender and one receiver. Identity is exposed via **three independent, redundant mechanisms** so an aggregator can pick the simplest one that fits its capabilities.

### 3.1 Mechanism A — NDI source name suffix

The shape of the suffix is controlled by `AppConfig.IdentitySuffixMode` (since v1.7.1):

| Mode      | Example                                                                       | When to use                                                                     |
|-----------|-------------------------------------------------------------------------------|---------------------------------------------------------------------------------|
| `Full`    | `Channel 1 [app=Intercom_A;device=9f8c1d3a…;role=sender;ch=1]`                | A management application that **only parses the name** (no metadata reading).   |
| `Compact` | `Channel 1 (Intercom_A)`                                                      | **Default since v1.7.1.** Maximum compatibility with NDI clients (legacy receivers, embedded encoders, hardware decoders) while still hinting which Intercom owns the source. |
| `Off`     | `Channel 1`                                                                   | Pure friendly name; for receivers that fail with any extra characters.          |

In every mode the [`<ndi_manager>` connection metadata](#32-mechanism-b--ndi_manager-connection-metadata-preferred-for-management-apps) and the [`<ndi_capabilities web_control>`](#33-mechanism-c--ndi_capabilities-web-control-link-ux-integration) link are unchanged, so a management app that reads metadata can keep aggregating by `(application_id, device_id)` regardless of the suffix mode.

#### `Full` mode (legacy v1.7.0 default)

```
<friendly>  [app=<APP_ID>;device=<DEVICE_ID>;role=<ROLE>;ch=<N>]
```

Where:

- `<friendly>` is the user‑editable channel label (default `"Channel <N>"` for senders, `"Intercom RX ch-<N>"` for receivers when no label is set).
- `<APP_ID>` is `ApplicationId` after sanitization.
- `<DEVICE_ID>` is `DeviceId` after sanitization.
- `<ROLE>` is exactly `sender` or `receiver` (lowercase).
- `<N>` is the channel number, decimal, no padding (1..N).

**Examples**:

```
Channel 1 [app=Intercom_A;device=9f8c1d3a…;role=sender;ch=1]
Director RX [app=Intercom_A;device=9f8c1d3a…;role=receiver;ch=3]
```

#### `Compact` mode (default)

```
<friendly>  (<APP_ID>)
```

The parens-style suffix follows the convention used by NDI Studio Monitor and most NDI tooling, so every NDI client we tested (including legacy / embedded receivers that reject the `Full` form) accepts it.

#### `Off` mode

The published name is just `<friendly>` — no suffix at all. Use this when even the `Compact` form trips up a receiver (very rare; some old hardware encoders strip everything after the first space).

#### Suffix grammar (regex)

The strip regex used internally — to remove **either** the `Full` bracket suffix **or** the `Compact` parens suffix before re-emitting the name — is:

```
\s*(\[app=[^;\]]+;device=[^;\]]+;role=(sender|receiver);ch=\d+\]|\([A-Za-z0-9_.\-]{1,64}\))\s*$
```

The Compact alternative `\([A-Za-z0-9_.\-]{1,64}\)` is intentionally tight: it only matches a trailing `(…)` whose content matches the `ApplicationId` regex, so user-friendly names that legitimately end with parens (e.g. `Channel 1 (Studio A)`) are **not** stripped.

Reference: `Core/NDIManager.cs` → `IdentitySuffixRegex`.

#### Recommended regex for an aggregator

If you want to parse the suffix from a discovered NDI source name, use:

```
\[app=(?<app>[^;\]]+);device=(?<device>[^;\]]+);role=(?<role>sender|receiver);ch=(?<ch>\d+)\]\s*$
```

Stability guarantees:

- The order of the four key/value pairs is **fixed**: `app`, `device`, `role`, `ch`.
- The separator between pairs is `;` (semicolon), no whitespace.
- The suffix is always the **last** non‑whitespace element of the name, after a single space.
- Keys are lowercase ASCII; values for `app` / `device` may not contain `;` or `]` (regex enforced upstream).

Breaking this grammar requires a bump to schema version (see § 3.2).

### 3.2 Mechanism B — `<ndi_manager>` connection metadata (preferred for management apps)

When a receiver connects to a sender, the sender ships an XML connection‑metadata frame. In addition to the standard `<ndi_product>` frame, NDI Intercom emits:

```xml
<ndi_manager
  app_id="Intercom_A"
  device_id="9f8c1d3a4b5e4f7a8c0d2e1f3a4b5c6d"
  channel_id="ch-1"
  channel_index="1"
  role="sender"
  schema="1" />
```

#### Attribute reference

| Attribute       | Type                       | Description                                             |
|-----------------|----------------------------|---------------------------------------------------------|
| `app_id`        | string (regex § 2.1)       | Logical application label (= `ApplicationId`).          |
| `device_id`     | string (regex § 2.1)       | Stable instance UID (= `DeviceId`).                     |
| `channel_id`    | string                     | `ch-<N>` form (matches the `ch=<N>` token in the name). |
| `channel_index` | integer                    | Channel number, 1..N.                                   |
| `role`          | enum: `sender`, `receiver` | Endpoint role. Currently only `sender` is emitted.      |
| `schema`        | integer                    | Metadata schema version. Currently `1`.                 |

All attribute values are XML‑escaped with `System.Security.SecurityElement.Escape` before emission.

**Schema versioning policy**: any backwards‑incompatible change to attribute names, values, or semantics increments `schema`. A v2 management application MUST tolerate higher schema versions by ignoring unknown attributes and falling back to mechanism A (name suffix) if it does not understand `schema`.

**Note**: receivers currently do **not** emit `<ndi_manager>` themselves. They emit `<ndi_product>` only (see § 3.4). To enumerate receivers from a management app, use Mechanism A or use the Discovery Server `recv_advertiser` events.

### 3.3 Mechanism C — `<ndi_capabilities>` web control link (UX integration)

Every sender additionally emits:

```xml
<ndi_capabilities web_control="http://%IP%:5016/" />
```

The `%IP%` token is **resolved by the NDI SDK** to the local IP address of the machine when shipped to a receiver. The port matches the embedded Web UI:

- `5016` for **NDI Intercom16**.
- `5017` for **NDI Intercom2**.
- Any value the operator sets in `appsettings.json` → `WebServer:Port`.

Compatible NDI apps (e.g. NDI Studio Monitor) display a "Web Control" link that opens the per‑instance Settings UI. This is the recommended way to wire deep links from a management dashboard back to the Intercom Web UI.

### 3.4 `<ndi_product>` (informational)

Both senders and receivers emit a standard `<ndi_product>` metadata frame for NDI tooling compatibility:

```xml
<ndi_product
  long_name="NDI Intercom16 - Professional Audio Intercom System"
  short_name="NDI Intercom16"
  manufacturer="NDI"
  version="1.7.0"
  model_name="Intercom-16CH"
  serial="IC-01"
  session_name="<full NDI name including suffix>" />
```

| Attribute      | Source                                                                     |
|----------------|----------------------------------------------------------------------------|
| `long_name`    | `IntercomProductOptions.NdiProductLongName`                                |
| `short_name`   | `IntercomProductOptions.NdiProductShortName`                               |
| `manufacturer` | constant `"NDI"`                                                           |
| `version`      | `IntercomProductOptions.NdiProductVersion` (`1.7.0` for both products)     |
| `model_name`   | `IntercomProductOptions.NdiModelName` (`Intercom-16CH` / `Intercom-2CH`)   |
| `serial`       | `IC-NN` where `NN` is the 2‑digit zero‑padded channel number (e.g. `IC-03`) |
| `session_name` | full NDI endpoint name including the identity suffix                       |

> **Heads‑up for aggregators**: do **not** rely on `serial` as a unique device id. It encodes the channel number only and is identical across instances. Use `device_id` from `<ndi_manager>` or from the suffix.

---

## 4. Persistent receivers and Discovery Server

In v1.7+ each channel owns a **single, long‑lived receiver instance** created during `NDIManager.Initialize` and disposed only at process exit.

| Behavior         | Pre‑v1.7 (legacy)                       | v1.7+ (current)                                                                     |
|------------------|------------------------------------------|-------------------------------------------------------------------------------------|
| Receiver life    | Created on connect, destroyed on disconnect | Created at startup, lives until app exit                                           |
| Source change    | Destroy + `NDIlib_recv_create_v3`        | `NDIlib_recv_connect(_, ref source)` (no recreate)                                  |
| Disconnection    | Destroy receiver                         | `NDIlib_recv_connect(_, IntPtr.Zero)` — receiver remains alive, just unconnected    |
| Discovery Server | Visible only when connected              | **Always visible**, with `allow_controlling=true` and `allow_monitoring=true`       |

This is what enables a management app to:

1. **List all 32 endpoints** of an Intercom instance even when none of its receivers are currently connected to a source.
2. **Issue connect / disconnect commands to receivers** through Discovery Server tooling without touching the local Web UI / REST API.
3. **Track receiver state** via the Discovery Server's recv listener events.

The relevant calls live in `Core/NDIManager.cs`:

- `NDIChannel.CreatePersistentReceiver()` — creates the receiver with no source and registers it on `recv_advertiser` with `allow_controlling=true, allow_monitoring=true`.
- `NDIChannel.ConnectReceiver(NDIlib_source_t)` — `NDIlib_recv_connect` with a real source.
- `NDIChannel.DisconnectReceiver()` — `NDIlib_recv_connect` with `IntPtr.Zero`.
- `NDIManager.SetReceiveSource(channelNumber, sourceName)` — high‑level entry point used by the Web UI / API.

The receiver name suffix is updated on identity change via `RefreshReceiverName()` (which destroys and recreates the receiver under the same `recv_advertiser`).

---

## 5. Reference: identity lifecycle inside the app

```text
┌──────────────────────┐                ┌──────────────────────┐
│  config.json on disk │ ◄────────────► │   ConfigManager      │
└──────────────────────┘                └──────────┬───────────┘
                                                   │ AppConfig (incl. ApplicationId, DeviceId)
                                                   ▼
                                          IntercomEngine.ApplyConfig
                                          ├─ sanitize ApplicationId (regex, fallback "Intercom_A")
                                          ├─ generate DeviceId (Guid "N") if missing
                                          ├─ persist back if mutated
                                          └─ NDIManager.SetIdentity(app, dev)
                                                   │
                          ┌────────────────────────┼────────────────────────┐
                          ▼                        ▼                        ▼
                   NDIChannel #1            NDIChannel #2          …    NDIChannel #N
                          │ SetIdentity            │                        │
                          ▼                        ▼                        ▼
            ┌─────── RecreateSender ─────┐     (idem)                   (idem)
            │  • build sender name        │
            │    "<label> [app=…;…;ch=1]" │
            │  • <ndi_product …/>         │
            │  • <ndi_manager … />        │
            │  • <ndi_capabilities …/>    │
            │  • register on send_advertiser
            ▼
            RecreateReceiver
            • build recv name suffix
            • create receiver (unconnected)
            • <ndi_product …/>
            • register on recv_advertiser
              (allow_controlling=true, allow_monitoring=true)
            • re‑connect to current source if any
```

When the operator changes Application ID from the Web UI:

1. The browser sends `applyConfiguration` over SignalR with the full `AppConfig`.
2. `IntercomHub.ApplyConfiguration` → `ConfigManager.SaveConfig` → `IntercomEngine.ApplyConfig`.
3. `NDIManager.SetIdentity` walks all channels.
4. Each `NDIChannel` recreates its sender (new name + new metadata) and recreates its receiver (new name); it then re‑connects to its previous `ReceiveSource` if any.

`DeviceId` is **never** mutated by the UI — only auto‑generated once on first boot.

---

## 6. How a management application should consume identity

This section is a recipe for an external aggregator. It is non‑normative (any subset that fits your use case is fine) but reflects the design intent.

### 6.1 Discovery

Connect to the configured **NDI Discovery Server** and enumerate:

- Senders via the SDK's `NDIlib_send_advertiser` / sender listener API.
- Receivers via `NDIlib_recv_advertiser` / receiver listener API.

This yields the full set of NDI endpoints. Each entry has at minimum a name string, which already encodes identity (Mechanism A).

### 6.2 Identity extraction strategy (recommended priority order)

1. **Try Mechanism B first**: if you can open a connection (or read cached connection metadata) and find `<ndi_manager schema="1" …/>`, take `app_id`, `device_id`, `role`, `channel_index` from there. **This is the only mechanism that works in every suffix mode** (including `Compact` and `Off`), so a 24/7‑grade aggregator should rely on it.
2. **Fall back to Mechanism A** (only useful if the operator left the suffix mode at `Full`): parse the suffix `[app=…;device=…;role=…;ch=…]` from the source name with the regex from § 3.1.
3. **If neither matches**: the source either uses `Compact` / `Off` mode (no parsable identity in the name) or is not an Intercom v1.7+ endpoint. In `Compact` mode the trailing `(<APP_ID>)` is a *hint* but not a reliable source of `device_id`, so again Mechanism B is required.

### 6.3 Aggregation

Group endpoints by `(app_id, device_id)`. Each group should yield exactly:

- N senders (one per channel, role = `sender`).
- N receivers (one per channel, role = `receiver`).

For Intercom16: `N == 16`. For Intercom2: `N == 2`.

A group with strictly less than N endpoints is in transient state (e.g. an instance booting up) — re‑poll after a few seconds.

### 6.4 Cross‑machine grouping

If you want to show all `Intercom_A` instances together regardless of machine, group by `app_id` and sub‑group by `device_id` to reveal the per‑machine breakdown.

### 6.5 Open the per‑instance Web UI

Read the `<ndi_capabilities web_control="…">` URL from any sender of the group and use it as a deep link. The `%IP%` token is already resolved by the SDK by the time the receiver sees the metadata.

If you cannot capture connection metadata (e.g. you only see Discovery Server records), build the URL yourself:

- Resolve the device's IP from any sender's `p_url_address`.
- Use port `5016` (Intercom16) or `5017` (Intercom2) as a default. Operators may override; if you need certainty, parse `<ndi_capabilities>` from a real connection.

### 6.6 Controlling a receiver remotely

Receivers are advertised with `allow_controlling=true`. A Discovery Server‑aware control surface can therefore connect / disconnect any of the N receivers without going through the Intercom REST/SignalR API. Use this for "studio takeover" scenarios or central routing matrices.

### 6.7 Pseudocode

```python
# Pseudocode: aggregate Intercom instances visible on the network.
import re
suffix_re = re.compile(
    r"\[app=(?P<app>[^;\]]+);device=(?P<device>[^;\]]+);"
    r"role=(?P<role>sender|receiver);ch=(?P<ch>\d+)\]\s*$"
)

instances = {}  # (app, device) -> {"senders": {...}, "receivers": {...}, "web": str|None}

for ndi_source in discovery.list_senders() + discovery.list_receivers():
    name = ndi_source.name
    # Mechanism B: prefer connection metadata if available
    meta = ndi_source.connection_metadata  # SDK-specific
    info = parse_ndi_manager(meta) if meta else None
    if info is None:
        m = suffix_re.search(name)
        if m is None:
            continue  # foreign endpoint, ignore
        info = {
            "app": m["app"], "device": m["device"],
            "role": m["role"], "ch": int(m["ch"]),
        }
    key = (info["app"], info["device"])
    bucket = instances.setdefault(key, {"senders": {}, "receivers": {}, "web": None})
    bucket[info["role"] + "s"][info["ch"]] = ndi_source
    if info["role"] == "sender" and bucket["web"] is None:
        bucket["web"] = parse_web_control(meta)  # may stay None
```

---

## 7. Compatibility & migration

### 7.1 Removed: NDI groups

Up to v1.5, NDI groups (`p_groups` on send / receive) were partially used. **In v1.7+ they are deliberately ignored**:

- Senders are created with `p_groups = IntPtr.Zero`.
- The send listener no longer extracts the `Groups` field (`Core/NDISendListener.cs` — *"Groups intentionally ignored: identity/routing does not rely on NDI groups."*).

If you previously relied on NDI groups for routing/aggregation, switch to `(app_id, device_id)`.

### 7.2 Old configurations

A `config.json` written by v1.x is forward‑compatible:

- `applicationId` is missing → defaults to `"Intercom_A"` and is written back on first save.
- `deviceId` is missing → a fresh GUID is generated and written back.

No manual migration is required. The first run on v1.7+ will normalize the file.

### 7.3 NDI source names

Names emitted by older v1.x clients (pre‑v1.7) have **no** `[app=…;…;ch=…]` suffix. A v1.7‑aware management app must therefore handle the case where Mechanism A fails for legacy peers and fall through to the foreign‑endpoint path.

---

## 8. Quick reference cheat‑sheet

| Question                            | Answer                                                                                                                 |
|-------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| What identifies an instance?        | Pair `(application_id, device_id)`.                                                                                    |
| What's the schema version?          | `1` (`<ndi_manager schema="1" />`).                                                                                    |
| Where is identity stored?           | `%PROGRAMDATA%\NDI Intercom16\config.json` (or `…\NDI Intercom2\…`).                                                  |
| How many senders / receivers?       | 16 each for Intercom16, 2 each for Intercom2. **Receivers are persistent** (always advertised).                       |
| Can NDI tools control receivers?    | Yes — registered with `allow_controlling=true, allow_monitoring=true`.                                                |
| How to deep‑link the Web UI?        | `<ndi_capabilities web_control="http://%IP%:<port>/" />` on every sender.                                              |
| Suffix mode default?                | `Compact` since v1.7.1 (was `Full` in v1.7.0). Configurable in Settings → Application Identity → "NDI source name format". |
| What works in every mode?           | `<ndi_manager>` connection metadata + `<ndi_capabilities>` web_control. Use these for management apps.                 |
| Are NDI groups used?                | **No**, removed.                                                                                                       |
| Source code reference               | `Core/NDIManager.cs` (NDIChannel + BuildEndpointName + BuildSenderManagerMetadataXml) and `Models/AppConfig.cs`.      |

---

## 9. Related documentation

- [README.md](../README.md) — repository overview.
- [ARCHITECTURE.md](ARCHITECTURE.md) — identity-aware routing and discovery (see also §9 in this document).
- [API_REST_GUIDE.md](API_REST_GUIDE.md) — REST endpoints (channel state, presets) and how to read identity via SignalR `GetConfiguration`.
- [INTERCOM USER_MANUAL.md](INTERCOM%20USER_MANUAL.md) — § *Application Identity* in the Settings panel.
- [CHANGELOG.md](../CHANGELOG.md) — release history for v1.7.x
