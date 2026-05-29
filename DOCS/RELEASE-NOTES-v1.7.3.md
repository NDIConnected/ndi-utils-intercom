# Release notes — NDI Intercom v1.7.3

> Headline: **cosmetic fix for the Settings page layout**. After v1.7.1 added the *NDI source name format* dropdown to the *Application Identity* card, the height imbalance between the left and right columns of the Settings page produced an empty band below short cards (typically *ASIO Device*) before the next short card (*Channels Feedback Gate*). Pure CSS change, no behavior or audio path is affected.

This is a small UI-only follow-up to [v1.7.2](RELEASE-NOTES-v1.7.2.md). All the audio decoding, identity routing, suffix mode and 24/7 stability work from the previous releases is unchanged.

---

## What was wrong

`wwwroot/css/settings.css` laid the Settings cards out with CSS Grid:

```css
.settings-container {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
}
```

CSS Grid with `auto-flow: row` aligns the cards row by row: when *ASIO Device* (short) and *Microphone Noise Gate* (5 sliders, tall) shared a row, the row took the height of the taller card. The shorter card stretched to fill the cell, but visually it left a large empty area below its content, and the next card (*Channels Feedback Gate*) only started in the next row — i.e. below the bottom of *Microphone Noise Gate*. Net effect: a visible gap on the left column.

The growing height of *Application Identity* (after v1.7.1 added the suffix mode dropdown) made the imbalance worse and the gap more obvious.

## What's fixed

The container now uses **CSS multi-column layout** instead of a row-aligned grid:

```css
.settings-container {
    column-count: 2;
    column-gap: 20px;
}

.settings-section {
    break-inside: avoid;
    margin-bottom: 20px;
}

.settings-section.full-width {
    column-span: all;
}
```

Multicol packs each card into the next available vertical slot in its column, with no row alignment between left and right — the browser's natural masonry-like flow. The two full-width cards (*NDI Bridge Service*, *Channels Configuration*) keep their original behavior via `column-span: all`. The `@media (max-width: 1000px)` breakpoint still collapses to a single column on narrow viewports.

Browser support: `column-count`, `break-inside: avoid` and `column-span: all` are supported by every Chrome / Edge / Firefox / Safari version released in the last several years; the embedded WebView2 used by the system tray's *Open Web Interface* is fine.

## Breaking changes

None.

## Files changed

- `wwwroot/css/settings.css` — `.settings-container`, `.settings-section`, `.settings-section.full-width`.

Versioning:

- All three csproj at `<Version>1.7.3</Version>` (+ matching AssemblyVersion / FileVersion / InformationalVersion).
- `Models/IntercomProductOptions.cs` — `NdiProductVersion = "1.7.3"`.
- `installer-script-intercom16.iss`, `installer-script-intercom2.iss` — version strings updated.

## Upgrade checklist

- [ ] Install the new build.
- [ ] Open *Settings*. The left column (*Application Identity* → *ASIO Device* → *Channels Feedback Gate*) should pack vertically with no large gaps. The right column (*Audio Devices* → *Microphone Noise Gate*) should follow the same packing.
- [ ] Resize the window narrow (< 1000 px) to confirm the layout collapses cleanly to a single column.
