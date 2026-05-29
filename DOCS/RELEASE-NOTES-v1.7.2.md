# Release notes — NDI Intercom v1.7.2

> Headline: **fix for incoming NDI stereo audio**. Stereo flows received from the network played back one octave higher and metallic; mono flows were unaffected. The receive path now correctly decodes the planar (FLTP) layout used by every modern NDI v3 sender.

This is a small bug-fix release on top of [v1.7.1](RELEASE-NOTES-v1.7.1.md). No behavior changes outside the NDI receive path; nothing else from 1.7.1 (24/7 stability fixes, identity-aware routing, suffix mode) is altered.

---

## What was wrong

`NDIChannel.DrainAllToBuffer` in `Core/NDIManager.cs` decoded incoming `NDIlib_audio_frame_v3_t` frames assuming **interleaved** sample layout (`L0,R0,L1,R1,...`). NDI v3 by default delivers audio in **planar** (FLTP) layout (`L0,L1,...,Ln-1, padding, R0,R1,...,Rn-1, ...`), and the struct already exposes `channel_stride_in_bytes` precisely for that reason.

The mismatch produced this exact behavior on a stereo 48 kHz feed:

- The mono buffer ended up containing the L channel decimated 2:1 followed by the R channel decimated 2:1, all written into a buffer of length `no_samples` that was then played back at the original 48 kHz.
- Net result: pitch shifted up an octave (frequency × 2) plus aliasing artifacts (the metallic edge).
- A click at the L → R boundary every 40 ms (one frame) added the buzzy texture.

Mono flows (`no_channels == 1`) hit a separate branch that just passed `samples` through unchanged, so they kept playing correctly — which matches the symptom report exactly.

This bug was **latent in every previous release** (it predates v1.7.0). It only became visible to operators who actually subscribed an NDI receiver to a stereo source. Workflows where every NDI flow on the network is mono (common for intercom / commentary / radio production) never tripped over it.

---

## What's fixed

`NDIChannel.DrainAllToBuffer`:

- Reads `audioFrame.FourCC` and treats the frame as planar by default (FLTP, `FourCC == 0`), with explicit branch for the rare interleaved case (FLTp, `FourCC == 1`).
- Uses `audioFrame.channel_stride_in_bytes` to locate the second channel plane in memory, instead of assuming `no_samples * sizeof(float)`. The two values usually coincide in practice, but the field exists to allow the SDK to insert padding for SIMD alignment, and respecting it makes the code correct even on senders that pad.
- Stereo (`no_channels == 2`) now produces the proper L+R average:
  - planar: `(samples[i] + samples[i + strideFloats]) / 2` for `i in [0, frames)`
  - interleaved: `(samples[i*2] + samples[i*2+1]) / 2` (preserved as a fallback)
- Multi-channel (`no_channels > 2`): channel 0 is taken (matches the previous behavior; mixing 5.1 / 7.1 to mono is rarely what an intercom needs and is left as a future improvement).
- The `Marshal.Copy` size is computed correctly for both layouts, so a sender that packs channel planes contiguously (the common case) and one that pads them are both handled.

The fix is local to the receive path; no API surface change, no config schema change, no NDI metadata change.

---

## Breaking changes

**None.** This is a pure bug fix. Operators who previously had stereo NDI sources and worked around the issue (e.g. asking the sender to publish mono) no longer need to: stereo on receive now sounds correct.

---

## Migration

Just install the new build. No configuration change required.

---

## Files changed

- `Core/NDIManager.cs` — `NDIChannel.DrainAllToBuffer` rewritten to honor `FourCC` and `channel_stride_in_bytes`.

Versioning:

- All three csproj at `<Version>1.7.2</Version>` (+ matching AssemblyVersion / FileVersion / InformationalVersion).
- `Models/IntercomProductOptions.cs` — `NdiProductVersion = "1.7.2"` (both products).
- `installer-script-intercom16.iss` `MyAppVersion = "1.7.2"`.
- `installer-script-intercom2.iss` `AppVersion = "1.7.2"`.

---

## Upgrade checklist

- [ ] Stop the running v1.7.1 (or earlier) Intercom app.
- [ ] Install the new build.
- [ ] On a channel configured to receive a stereo NDI flow at 48 kHz (or any sample rate), enable **Listen** and verify the audio is at the correct pitch with no metallic / aliased edge.
- [ ] Verify mono flows still play correctly (regression check; they were unaffected by the fix but worth a glance).
