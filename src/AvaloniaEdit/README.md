# AvaloniaEdit (vendored)

This is the core [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) text-editing
library (tag `11.4.1`), vendored into this repo instead of referenced as the
`Avalonia.AvaloniaEdit` NuGet package, so the editor's source lives alongside the rest of
eBook Editor. It's licensed under the MIT License — see `LICENSE` in this folder for the
original copyright notice, which is preserved unmodified as required by that license.

Only the core `AvaloniaEdit` project from the upstream repo is included; `AvaloniaEdit.TextMate`
and `AvaloniaEdit.Demo` aren't used here and were left out. `AvaloniaEdit.csproj` was rewritten
to build under this repo's `net10.0` target (upstream multi-targets `netstandard2.0;net6.0`)
and pinned to this repo's `Avalonia` package version (11.3.18) — the source files themselves
are unmodified from upstream.

Not intended to track upstream changes automatically; if a fix or feature from a newer
AvaloniaEdit release is needed, pull the specific change in manually.
