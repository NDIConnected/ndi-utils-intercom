# Release notes — NDI Intercom v1.7.0

> Headline: **24/7 stability hardening** on top of the identity-aware NDI routing introduced in the previous milestone. This is the recommended target for unattended deployments.

Full identity / aggregation specification: [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

---

## What's new

### 24/7 stability hardening

Six classes of issues that compound over long uptimes were addressed:

- **Native NDI handle race fixes** — every `NDIChannel` now serializes audio I/O against sender/receiver lifecycle changes via a per-channel lock. The previous design allowed an audio callback to dereference a freed `IntPtr` if the operator changed an Application ID, channel label or NDI source while audio was flowing — typically triggering an access violation under load. The lock is per-channel so different channels never contend.
- **Graceful shutdown** — `IntercomEngine.Stop()` now waits up to 2 seconds for the dedicated audio thread to exit before `Dispose` tears down NDI handles. Self-wait is detected so a Stop call from inside the audio thread no longer deadlocks. The tray exit path uses a 5-second bounded host shutdown.
- **ASIO subscription leak** — switching ASIO device used to leave the previous `AudioAvailable` lambda subscribed, keeping the old `AsioAudioEngine` alive and risking a callback on a stale instance. The handler is now captured and unsubscribed symmetrically.
- **ArrayPool leak in `AsioAudioEngine.Read()`** — per-channel mix buffers rented from `ArrayPool<float>.Shared` were only returned when their length exceeded the requested sample count, which almost never happens. All rented buffers (per-channel mixes + cross-mode NDI snapshots) are now returned in a `finally` block, eliminating a per-callback GC pressure source.
- **Audio device watchdog** — both the WASAPI engine (Windows) and the parec/pacat engine (Linux) now subscribe to disconnect/exit signals and re-arm capture/playback automatically with bounded exponential backoff (1s → 16s, retrying forever as long as capture/playback is desired). USB unplug, PipeWire restart, driver replacement and other transient device losses now self-heal.
- **Atomic configuration save** — `ConfigManager.SaveConfig` writes to a temp file and renames atomically. A power loss or kill-9 mid-save can no longer corrupt `config.json`. Concurrent saves from the UI thread and the audio path are serialized via a static lock.

### Persistent rolling log file

- New `FileLoggerProvider` (in-house, no NuGet dependency) writes daily-rolling files to `%PROGRAMDATA%\NDI Intercom16\logs\` (Windows) or `~/.local/share/NDI/NDI Intercom16/logs/` (Linux). Default retention: 14 days.
- All `Console.WriteLine` paths in `NDIManager`, the Linux PipeWire/PulseAudio engine, and the watchdog hooks now route through `ILogger`. Critical init failures (NDI runtime missing, sender/receiver creation refused) are logged at `Critical` / `Warning` instead of being silently swallowed.
- Discovery-event log spam is at `Debug` level, so a busy NDI network doesn't fill the log file.

### `/healthz` endpoint

- `GET /healthz` returns 200 once `IntercomEngine` is initialized AND the audio thread is running, otherwise 503. Payload includes `product`, `version`, `initialized`, `running`, `healthy`. Suitable for external monitoring (Nagios, Prometheus blackbox exporter, etc.).
- Does not touch disk, so a tight monitoring loop is safe.

### GC pressure reduction in the audio path

- `Core/MixingEngine.cs` `BytesToFloats` / `FloatsToBytes`: replaced `BitConverter.GetBytes(float)` (which allocates a 4-byte array per sample) with `Buffer.BlockCopy`. On a 16-channel intercom with many active LISTEN/TALK channels the previous code generated millions of small allocations per second.
- `Core/AudioRingBuffer.cs`: rewritten to do a single bulk `Buffer.BlockCopy` per Read/Write (with at most one wrap-around split) instead of one float-to-bytes conversion per sample. Internal "conversion buffers" deleted.
- `Core/IntercomEngine.cs DedicatedAudioThread`: scratch `Dictionary` and `List` instances reused across frames instead of being reallocated each iteration. LINQ `.Any()` replaced with a manual scan to avoid the closure allocation.

### Listener cleanup (no unbounded growth)

- `Core/NDISendListener.cs`: `_subscribedSenders` and `_senderEvents` are now pruned of UUIDs no longer present on the Discovery Server each poll. On a network with devices coming and going these collections previously grew indefinitely.
- `Core/NDIDiscoveryService.cs`: `_receiverSourceNames` cache pruned the same way — receivers that vanish without a clean disconnect event no longer leak.

### VU service hardening

- `VUMeterBackgroundService`: throttled to ~10 Hz (vs the previous ~50 Hz fire-and-forget pump), wraps every SignalR `SendAsync` in try/catch so a misbehaving client can't take down the level pump, observes faulted tasks (no more `UnobservedTaskException` accumulation), and detaches its event handler on shutdown.

### Identity-aware NDI routing

(Carried over from the previous milestone — same behaviour, now stable on 24/7.)

- **`ApplicationId`** field in `AppConfig` (default `"Intercom_A"`, regex `^[A-Za-z0-9_.-]{1,64}$`). Editable from the Web UI under *Settings → Application Identity*.
- **`DeviceId`** auto-generated as a fresh GUID on first run and persisted to `config.json`.
- Every NDI source name carries an identity suffix:
  ```
  <friendly>  [app=<APP_ID>;device=<DEVICE_ID>;role=(sender|receiver);ch=<N>]
  ```
- Senders ship `<ndi_manager app_id=… device_id=… channel_id=ch-N channel_index=N role="sender" schema="1" />` connection metadata.
- Senders also ship `<ndi_capabilities web_control="http://%IP%:<port>/" />` so NDI Studio Monitor displays a *Web Control* deep-link to the Intercom's Settings UI.
- Receivers are persistent: created at startup, advertised on the Discovery Server with `allow_controlling=true, allow_monitoring=true`, source switches done with `NDIlib_recv_connect`.

### Build / packaging

- All three csproj files (Intercom16 Windows, Intercom2 Windows, Intercom16 Linux) bumped to `<Version>1.7.0</Version>`.
- `<ndi_product version=…>` reports `1.7.0` for both products.
- `installer-script-intercom16.iss` + `installer-script-intercom2.iss` updated to `MyAppVersion=1.7.0`.

---

## Breaking changes

**None at the API surface vs the previous build.** The changes are internal stability hardening; existing REST/SignalR contracts, NDI source name shape, and config file schema are unchanged.

If you are upgrading from a v1.5.x or earlier build that was using NDI groups for routing/aggregation, the *previous* milestone removed group support. Migrate to `(application_id, device_id)` as documented in [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

---

## Migration

A v1.x `config.json` is forward-compatible:

- Missing `applicationId` → injected with `"Intercom_A"`.
- Missing `deviceId` → injected with a fresh `Guid.NewGuid().ToString("N")`.
- Both writes are persisted on the first save after upgrade.

To force a clean start, delete:

- `%PROGRAMDATA%\NDI Intercom16\config.json` (16-channel build), or
- `%PROGRAMDATA%\NDI Intercom2\config.json` (2-channel build),
- `~/.local/share/NDI/NDI Intercom16/config.json` (Linux).

---

## Files changed (high level)

24/7 hardening:

- `Core/NDIManager.cs` — per-channel lifetime lock around all native NDI calls.
- `Core/IntercomEngine.cs` — Stop/Dispose ordering, ASIO subscription unsubscribe, audio-thread scratch buffers, ILogger injection, `IsInitialized`.
- `Core/AudioEngine.cs` (Windows) — WASAPI watchdog with `RecordingStopped`/`PlaybackStopped` re-arm, MMDevice dispose, defensive Invoke.
- `Core/Linux/AudioEngine.cs` — parec/pacat watchdog with bounded exponential backoff and ILogger.
- `Core/AsioAudioEngine.cs` — `Read()` ArrayPool ownership fix and defensive snapshot of input buffers.
- `Core/AudioRingBuffer.cs` — Buffer.BlockCopy fast-path, internal conversion buffers removed.
- `Core/MixingEngine.cs` — BytesToFloats/FloatsToBytes via Buffer.BlockCopy.
- `Core/ConfigManager.cs` — atomic save (temp + rename), thread-safe lock, ILogger.
- `Core/NDISendListener.cs` + `Core/NDIDiscoveryService.cs` — listener cleanup of disappeared UUIDs.
- `Core/Logging/FileLoggerProvider.cs` — new file logger.
- `Hubs/IntercomHub.cs` — VU service hardening, throttle, observed tasks.
- `IntercomAppHost.cs` — file logger DI, static logger wiring, `/healthz`, structured startup logging.
- `TrayApplicationContext.cs` — bounded `StopAsync` + `DisposeAsync`.

Versioning:

- `NDI Intercom16.csproj`, `NDI Intercom2.csproj` — `<Version>1.7.0</Version>`.
- `Models/IntercomProductOptions.cs` — `NdiProductVersion = "1.7.0"`.
- `installer-script-intercom16.iss`, `installer-script-intercom2.iss` — `MyAppVersion = "1.7.0"`.

---

## Upgrade checklist

- [ ] Stop the running Intercom app.
- [ ] Install the new build (or `dotnet build` from source).
- [ ] Start the app — the first run normalizes `config.json` (idempotent for v1.x configs).
- [ ] (Optional) Open *Settings → Application Identity* and pick a meaningful `Application ID` if you operate multiple instances on the network.
- [ ] Verify the rolling log file is created under `%PROGRAMDATA%\NDI Intercom16\logs\` (or the Linux equivalent).
- [ ] Hit `http://localhost:5016/healthz` — should return 200 with `"healthy": true` once initialization completes.
- [ ] (Optional) Confirm in NDI Studio Monitor that each sender shows a *Web Control* link that opens this Intercom's Settings UI.
