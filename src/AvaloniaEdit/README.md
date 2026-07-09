# AvaloniaEdit (vendored)

This is the core [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) text-editing
library (tag `11.4.1`), vendored into this repo instead of referenced as the
`Avalonia.AvaloniaEdit` NuGet package, so the editor's source lives alongside the rest of
eBook Editor. It's licensed under the MIT License — see `LICENSE` in this folder for the
original copyright notice, which is preserved unmodified as required by that license.

Only the core `AvaloniaEdit` project from the upstream repo is included; `AvaloniaEdit.TextMate`
and `AvaloniaEdit.Demo` aren't used here and were left out. `AvaloniaEdit.csproj` was rewritten
to build under this repo's `net10.0` target (upstream multi-targets `netstandard2.0;net6.0`)
and pinned to this repo's `Avalonia` package version (11.3.18).

**The C# namespace and assembly name were renamed from `AvaloniaEdit` to `AvaloniaEditCore`**
throughout every source file here — otherwise unmodified from upstream. This was necessary,
not cosmetic: `Markdown.Avalonia.SyntaxHigh` (a mandatory dependency of `Markdown.Avalonia`,
used for the preview pane's code-block syntax highlighting) itself depends on the real
`Avalonia.AvaloniaEdit` NuGet package, and `Markdown.Avalonia`'s plugin loader reflection-loads
it by assembly name at runtime. An earlier attempt to fix the resulting `AvaloniaEdit` vs.
`AvaloniaEdit` assembly-identity clash by excluding that NuGet package's build assets made the
app crash on every launch with a `FileNotFoundException` the moment any `MarkdownScrollViewer`
was constructed — `Markdown.Avalonia` has no graceful fallback when a plugin assembly it
expects to find is missing. Renaming the vendored copy instead lets both assemblies coexist.

Not intended to track upstream changes automatically; if a fix or feature from a newer
AvaloniaEdit release is needed, pull the specific change in manually (remember to reapply the
namespace rename to whatever you pull in).
