# eBook Editor

A cross-platform .NET desktop application for authoring, organizing, and publishing eBooks. Each book lives in its own project directory of plain Markdown files plus a metadata file, with title page, copyright page, table of contents, and about-the-author page generated automatically from that metadata. Chapters can be reordered by drag-and-drop, imported from `.docx` manuscripts, and exported as EPUB 3.3 or Markdown.

## Requirements

- .NET SDK 10.0.301 or later (pinned via `global.json`)
- macOS, Windows, or Linux (built on [Avalonia UI](https://avaloniaui.net/), which runs natively on all three)

## Building and running

```bash
dotnet build eBookEditor.slnx
dotnet run --project src/eBookEditor.App
dotnet test tests/eBookEditor.Tests
```

On first run, the app scaffolds a demo project under `src/eBookEditor.App/sample-project-data/` so there's something to explore immediately — a real "New Project" flow is future work (see [Known limitations](#known-limitations)).

## Solution structure

| Project | Purpose |
|---|---|
| `eBookEditor.Core` | Domain models (`BookMetadata`, `SpineItem`, `EbookProject`, …) and services with no UI or format dependencies: `ProjectService` (create/load/save), `SpineService` (ordering/numbering), `ChapterFileService` (YAML front matter + body), `AppSettingsService` (cross-project autofill/MRU). |
| `eBookEditor.Markdown` | Markdig pipeline, `PageGeneratorService` (title/copyright/TOC/about-the-author page generation), `BookIndexGenerator` (`book.md`), `MarkdownExportService` (whole-book / single-chapter export). |
| `eBookEditor.Epub` | Hand-rolled EPUB 3.3 writer (`EpubBuilder`) — container.xml, package.opf, nav.xhtml, legacy toc.ncx, XHTML content docs, image bundling — plus a structural `EpubValidationHelper`. |
| `eBookEditor.DocxImport` | `.docx` → chapters importer built on `DocumentFormat.OpenXml`: detects chapter boundaries (Heading 1 style or "Chapter N" text), converts bold/italic/headings/lists/images to Markdown. |
| `eBookEditor.App` | The Avalonia desktop shell (MVVM via CommunityToolkit.Mvvm): sidebar with drag-to-reorder chapters, Markdown editor (AvaloniaEdit) with a whole-pane edit/preview toggle (Markdown.Avalonia), metadata editor, and the EPUB/Markdown export and DOCX import actions. |
| `tests/eBookEditor.Tests` | xUnit coverage across all of the above, including `.docx` fixtures built programmatically with the OpenXml SDK (no binary test fixtures needed) and generated EPUBs validated both by `EpubValidationHelper` and, during development, `unzip`/`xmllint`. |

## Project directory layout

Each book is a directory, not a single file:

```
<ProjectName>/
  project.ebookproj.json   metadata + ordered "spine" (front matter / chapters / back matter)
  book.md                  auto-regenerated master index linking every file in spine order
  frontmatter/             title-page.md, copyright.md, toc.md — auto-generated from metadata
  chapters/                <slug>-<id>.md, each with a small YAML front-matter block
                            (title, subtitle, number: auto|override|none)
  backmatter/               about-the-author.md (auto-generated) plus free-form pages
  images/                   cover, author photo, publisher logo, chapter images
  output/                   generated .epub / exported .md land here
```

Chapter filenames are stable once created — reordering only changes `Spine[].Order` in `project.ebookproj.json`, never the filesystem, so image references and git history stay intact.

## Known limitations

This is a working first pass covering the full pipeline end to end (author → organize → import → export), not a finished product. Notable simplifications:

- No New Project wizard yet — the app opens a fixed demo project (`SampleProjectFactory`); creating additional projects currently requires calling `ProjectService.CreateProject` directly.
- The metadata editor uses comma-separated text fields for lists (authors, tags, social/store links) rather than dynamic add/remove rows.
- DOCX import doesn't convert tables, and hyperlink targets are dropped (link text is kept).
- No automated visual/UI testing was possible in the environment this was built in (a sandboxed session without a real windowing session for the Avalonia/macOS native render timer) — correctness was verified through the full xUnit suite plus running real builds and inspecting generated output, not through interactive UI testing.
