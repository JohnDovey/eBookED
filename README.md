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

On first run, the app scaffolds a demo project under `src/eBookEditor.App/sample-project-data/` so there's something to explore immediately. Use **File → New Project…** or **File → Open Project…** to work with a real book — each opens in its own window, so multiple projects can be open at once. **File → Save Project** (Cmd/Ctrl+S) saves everything (metadata and all file contents); closing a window with unsaved changes prompts to save or discard first.

## Solution structure

| Project | Purpose |
|---|---|
| `eBookEditor.Core` | Domain models (`BookMetadata`, `SpineItem`, `EbookProject`, …) and services with no UI or format dependencies: `ProjectService` (create/load/save), `SpineService` (ordering/numbering), `ChapterFileService` (YAML front matter + body), `AppSettingsService` (cross-project autofill/MRU). |
| `eBookEditor.Markdown` | Markdig pipeline, `PageGeneratorService` (title/copyright/TOC/about-the-author page generation), `BookIndexGenerator` (`book.md`), `MarkdownExportService` (whole-book / single-chapter export). |
| `eBookEditor.Epub` | Hand-rolled EPUB 3.3 writer (`EpubBuilder`) — container.xml, package.opf, nav.xhtml, legacy toc.ncx, XHTML content docs, image and font bundling, selectable CSS `TemplateService`, `FontService`/`FontInstallerService` — plus a structural `EpubValidationHelper`. |
| `eBookEditor.DocxImport` | `.docx` → chapters importer built on `DocumentFormat.OpenXml`: detects chapter boundaries (Heading 1 style or "Chapter N" text), converts bold/italic/headings/lists/images/tables/hyperlinks to Markdown. |
| `eBookEditor.App` | The Avalonia desktop shell (MVVM via CommunityToolkit.Mvvm): a title bar menu (File/Project/Meta Data/Export/About), New Project wizard, sidebar with drag-to-reorder chapters, Markdown editor (AvaloniaEdit) with a whole-pane edit/preview toggle (Markdown.Avalonia), five metadata forms under the Meta Data menu (Front Matter, Style, Copyright & Publishing, Genre Tags, About the Author), and the EPUB/Markdown export and DOCX import actions. Projects open in independent windows, so several books can be open simultaneously. |
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

## CSS templates

EPUB export styling comes from a `templates/` directory next to the installed app (not per-project), holding one `.css` file per template — the filename (minus extension) is the display name shown in the Metadata editor's Style picker. A "Default" template is seeded automatically from the built-in stylesheet the first time the picker is opened. The list is rescanned every time the picker opens, so dropping a new `.css` file into `templates/` makes it available immediately. The book's chosen template is saved in its metadata and used whenever that book is exported to EPUB. A "Vellum Serif" template ships alongside "Default", adapted from a Vellum-generated reference EPUB.

## Fonts

A `fonts/` directory ships next to `templates/` and holds the font files a template's `@font-face` rules point at. When a book is exported to EPUB, `EpubBuilder` embeds whichever of those fonts the selected template actually references. When the template picker's selection changes, `FontInstallerService` also copies any of those fonts that aren't already present into the current user's font directory (macOS/Windows/Linux), so the same fonts render correctly outside the EPUB too — e.g. in the app's own editor preview.

## Known limitations

This is a working first pass covering the full pipeline end to end (author → organize → import → export), not a finished product. Notable simplifications:

- Font-installation only checks whether a same-named font *file* already exists in the user's font directory — it doesn't query the OS font registry by family name, which would need a different native API per platform (CoreText/DirectWrite/Fontconfig).
- No automated visual/UI testing was possible in the environment this was built in (a sandboxed session without a real windowing session for the Avalonia/macOS native render timer) — correctness was verified through the full xUnit suite plus running real builds and inspecting generated output, not through interactive UI testing.

## Contact

John Dovey — dovey.john@gmail.com
