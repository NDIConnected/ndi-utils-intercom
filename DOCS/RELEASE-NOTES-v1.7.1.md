# Release notes — NDI Intercom v1.7.1

> Headline: **NDI source name suffix mode** — selectable Full / Compact / Off — to fix legacy and embedded NDI receivers that fail to subscribe to flows whose name contains the v1.7.0 identity suffix `[app=…;device=…;role=…;ch=…]`.

This is a small follow-up to [v1.7.0](RELEASE-NOTES-v1.7.0.md). All the 24/7 stability hardening from 1.7.0 is unchanged. Only the **shape of the published NDI source name** can now be tuned to match what your downstream NDI clients can digest.

---

## Why this exists

In v1.7.0 every NDI sender / receiver name carries a full identity suffix:

```
Channel 1 [app=Intercom_A;device=9f8c1d3a4b5e4f7a8c0d2e1f3a4b5c6d;role=sender;ch=1]
```

That works perfectly with Studio Monitor and most modern NDI tools. **But** some legacy / embedded receivers (older hardware encoders, certain mobile NDI apps, a few third-party tools) reject these names: too long, brackets / semicolons confuse their parser, mDNS encoding chokes on extra characters. The result is a sender that's discovered but cannot be subscribed to.

v1.7.1 lets the operator pick a less aggressive suffix shape so those clients work again, **without losing the management-app aggregation feature** (the `<ndi_manager>` XML connection metadata still carries the full identity in every mode).

---

## What's new

### `IdentitySuffixMode` setting (Full / Compact / Off)

New field in `AppConfig` (`Models/AppConfig.cs`) and in the Web UI (Settings → Application Identity → "NDI source name format"):

| Mode      | Published NDI name                                                | When to use                                                                     |
|-----------|-------------------------------------------------------------------|---------------------------------------------------------------------------------|
| `Full`    | `Channel 1 [app=Intercom_A;device=9f8c1d3a…;role=sender;ch=1]`    | Management application that **only parses the name** (no metadata reading).     |
| `Compact` | `Channel 1 (Intercom_A)`                                          | **Default since v1.7.1.** Maximum compatibility with all NDI clients while still hinting which Intercom owns the source. |
| `Off`     | `Channel 1`                                                        | Legacy receivers that fail with any extra characters in the source name.        |

Validation (server side, mirrored client side): unknown values silently fall back to `Compact`.

### `<ndi_manager>` metadata is unchanged in every mode

Every sender keeps publishing the v1.7.0 connection metadata XML:

```xml
<ndi_manager app_id="Intercom_A" device_id="9f8c1d3a…" channel_id="ch-1" channel_index="1" role="sender" schema="1" />
```

A management application that reads `<ndi_manager>` (the recommended consumption strategy — see [`IDENTITY-AWARE-ROUTING.md` § 6.2](IDENTITY-AWARE-ROUTING.md#62-identity-extraction-strategy-recommended-priority-order)) keeps aggregating instances by `(application_id, device_id)` regardless of the suffix mode.

### Hot-swap

Changing the mode in the UI walks every channel under the existing per-channel `_ndiLifetimeLock` (introduced in v1.7.0 for crash-free identity changes), recreates the sender + receiver under the new name, and re-connects each receiver to its previous source. No service restart required.

---

## Breaking changes

### Default suffix mode changed: `Full` → `Compact`

Operators upgrading from v1.7.0 **without editing config.json** will see the published NDI source names change shape:

```
v1.7.0:  Channel 1 [app=Intercom_A;device=9f8c1d3a…;role=sender;ch=1]
v1.7.1:  Channel 1 (Intercom_A)
```

This is by design — the new default fixes the most common compatibility complaints with older NDI clients. **If your management application or any external tooling regex-matches the v1.7.0 name format**, you have two options:

1. **Recommended**: switch your aggregator to read the `<ndi_manager>` connection metadata XML instead of parsing the name. This is the path that's been documented as preferred since v1.7.0 and is unaffected by this change.
2. **Quick fix**: set `IdentitySuffixMode = "Full"` in `config.json` (or pick *Full* in Settings) to keep the v1.7.0 name shape. The `Compact` regex described in `IDENTITY-AWARE-ROUTING.md § 3.1` matches **either** form, so a forward-thinking aggregator can already handle both.

Hard-coded equality checks against the literal v1.7.0 name (e.g. `name == "Channel 1 [app=…]"`) will break under the new default. Use `StartsWith("Channel 1 ")` or the regex from § 3.1 of the spec instead.

---

## Migration

Existing v1.7.0 `config.json` is **forward-compatible**:

- Missing `identitySuffixMode` → injected with `"Compact"` (the new default) and persisted on the first save after upgrade.
- An invalid value → silently coerced to `"Compact"`.

No manual migration steps are required.

---

## Files changed

- `Models/AppConfig.cs` — new `IdentitySuffixMode` field with full doc-comment.
- `Core/NDIManager.cs` — `SuffixMode*` constants, `SanitizeSuffixMode`, `SetSuffixMode` on both `NDIManager` and `NDIChannel`; `BuildEndpointName` switches on the mode; `IdentitySuffixRegex` extended to strip both `[…]` and `(…)` forms so a runtime mode change can't accumulate suffixes on the friendly name.
- `Core/IntercomEngine.cs` — `ApplyConfig` validates the new field, persists the normalized value, and propagates to `NDIManager.SetSuffixMode`.
- `wwwroot/settings.html` — new `<select id="identitySuffixMode">` under Application Identity.
- `wwwroot/js/settings.js` — load + clamp + send the new field.
- `DOCS/IDENTITY-AWARE-ROUTING.md` — § 3.1 rewritten with the three-mode table, the updated strip regex, and § 6.2 / § 8 cheat-sheet adjusted for the new reality.

Versioning:

- All three csproj at `<Version>1.7.1</Version>` (+ matching AssemblyVersion / FileVersion / InformationalVersion).
- `Models/IntercomProductOptions.cs` — `NdiProductVersion = "1.7.1"` (both products).
- `installer-script-intercom16.iss` `MyAppVersion = "1.7.1"`.
- `installer-script-intercom2.iss` `AppVersion = "1.7.1"`.

---

## Upgrade checklist

- [ ] Stop the running Intercom app.
- [ ] Install the new build.
- [ ] On first launch, open *Settings → Application Identity*. The "NDI source name format" dropdown shows **Compact** (the new default).
- [ ] If you operate a management application that **parses the NDI name** for identity:
  - either switch it to read `<ndi_manager>` connection metadata (preferred), or
  - flip the dropdown back to **Full** to keep the v1.7.0 name shape.
- [ ] Verify that the receivers that previously failed to subscribe now work. If `Compact` is still rejected by an exotic client, switch to **Off**.
- [ ] (Optional) Confirm in NDI Studio Monitor that each sender still shows a *Web Control* link — that link is unaffected by the suffix mode.
