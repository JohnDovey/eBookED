# eBook Editor

A cross-platform .NET desktop application for authoring, organizing, and publishing eBooks. Each book lives in its own project directory of plain Markdown files plus a metadata file, with title page, imprint (copyright) page, table of contents, and about-the-author page generated automatically from that metadata. Chapters can be reordered by drag-and-drop, imported by dragging or picking `.md`/`.docx`/`.html` files, and exported as EPUB 3.3, a print-formatted PDF, or Markdown.

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
| `AvaloniaEdit` | Vendored copy of the core [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) text-editing library (MIT license, see its own README) instead of a NuGet package, so the editor's source lives in this repo. Built as assembly/namespace `AvaloniaEditCore` (renamed from upstream's `AvaloniaEdit`) so it doesn't collide with the real `Avalonia.AvaloniaEdit` NuGet package that Markdown.Avalonia's syntax-highlighting extension depends on internally. |
| `eBookEditor.Core` | Domain models (`BookMetadata`, `SpineItem`, `EbookProject`, …) and services with no UI or format dependencies: `ProjectService` (create/load/save), `SpineService` (ordering/numbering), `ChapterFileService` (YAML front matter + body, chapter file renumbering), `ChapterFileNaming` (chapter filename ↔ number/title parsing), `AppSettingsService` (cross-project autofill/MRU). |
| `eBookEditor.Markdown` | Markdig pipeline, `PageGeneratorService` (title/copyright/TOC/about-the-author page generation), `BookIndexGenerator` (`book.md`), `MarkdownExportService` (whole-book / single-chapter export). |
| `eBookEditor.Epub` | Hand-rolled EPUB 3.3 writer (`EpubBuilder`) — container.xml, package.opf, nav.xhtml, legacy toc.ncx, XHTML content docs (with internal chapter links rewritten so the in-book TOC page is clickable), image and font bundling, selectable CSS `TemplateService`, `FontService`/`FontInstallerService` — plus a structural `EpubValidationHelper`. |
| `eBookEditor.DocxImport` | `.docx` → chapters importer built on `DocumentFormat.OpenXml`: detects chapter boundaries (Heading 1 style or "Chapter N" text), converts bold/italic/headings/lists/images/tables/hyperlinks to Markdown. Also the reverse: `MarkdownToDocxConverter` exports a single chapter's Markdown back to `.docx` (headings, bold/italic, links, lists, tables, images, and real Word footnotes via `FootnotesPart`). |
| `eBookEditor.ChapterImport` | Imports a dropped or picked file as one or more chapters: `.md` used as-is, `.docx` via `eBookEditor.DocxImport`, `.html`/`.htm` via `HtmlToMarkdownConverter` (wraps the `ReverseMarkdown` package). `OrphanChapterScanner` finds `.md` files sitting in a project's `chapters/` folder that aren't in the spine yet (e.g. dropped in via Finder/Explorer) so they get picked up automatically. |
| `eBookEditor.Pdf` | Print-formatted PDF export via [QuestPDF](https://www.questpdf.com/) (Community license). `PdfBuilder` starts every chapter and front/back-matter page on its own page, numbers front matter with lowercase roman numerals and the body with arabic numerals (`FrontMatterAwarePageNumberFooter`), and renders a table of contents with real, clickable, page-numbered entries via QuestPDF's Section/SectionLink support. The imprint page is rendered directly from metadata (not through the generic renderer) so its copyright statement can be pinned to the bottom of the page. `PdfPageSizeCatalog` (in Core, so the settings UI doesn't need a PDF library reference) defines the selectable page sizes. |
| `eBookEditor.App` | The Avalonia desktop shell (MVVM via CommunityToolkit.Mvvm): a title bar menu (File/Project/Meta Data/Export/About), New Project wizard, sidebar with drag-to-reorder chapters rendered as an ordered list (right-click a chapter for Delete/Export as Word), Markdown editor (AvaloniaEdit) with a whole-pane edit/preview toggle (Markdown.Avalonia), six metadata forms under the Meta Data menu (Front Matter, Style, Copyright & Publishing, Genre Tags, About the Author, PDF Settings), and the EPUB/PDF/Markdown export and DOCX/chapter import actions. EPUB and PDF exports finish with a result modal (`GenerationResultWindow`) showing word/page counts and links to open the file or its folder. Also accepts dragged-in chapter files directly onto the sidebar. Projects open in independent windows, so several books can be open simultaneously. |
| `tests/eBookEditor.Tests` | xUnit coverage across all of the above, including `.docx` fixtures built programmatically with the OpenXml SDK (no binary test fixtures needed) and generated EPUBs validated both by `EpubValidationHelper` and, during development, `unzip`/`xmllint`. |

## Project directory layout

Each book is a directory, not a single file:

```
<ProjectName>/
  project.ebookproj.json   metadata + ordered "spine" (front matter / chapters / back matter)
  book.md                  auto-regenerated master index linking every file in spine order
  frontmatter/             title-page.md, copyright.md, toc.md — auto-generated from metadata
  chapters/                "NNN - Chapter Title.md" (e.g. "023 - What Now.md"), each with a
                            small YAML front-matter block (title, subtitle, number: auto|override|none)
  backmatter/               about-the-author.md (auto-generated) plus free-form pages
  images/                   cover, author photo, publisher logo, chapter images
  output/                   generated .epub / exported .md land here
```

Chapter files are renamed to match their resolved position after any add/import/reorder, so they sort correctly in Finder/Explorer the same way they're ordered in the book — this is a rename, not a content rewrite, so a chapter's own body content and images are unaffected.

## Importing chapters

New chapters can come from three places, all producing the same result: **File → Import Chapters…** (a file picker), dragging files directly onto the sidebar, or files already sitting in a project's `chapters/` folder that the app doesn't know about yet (picked up automatically on open). `.md` files are used as-is, `.docx` files go through the same chapter-boundary detection as the whole-manuscript DOCX import, and `.html`/`.htm` files are converted to Markdown. A file's name supplies its title and, if it starts with a number (`"23. What Now.md"`, `"007 - Foo.md"`), a hint for where it belongs in the chapter order.

## PDF export

**File → Export → PDF…** produces a print-formatted PDF: every chapter (and front/back matter page) starts on its own page, front matter is numbered i/ii/iii while the body counts 1/2/3, and the table of contents is clickable with real page numbers. Page size is set per-book via **Meta Data → PDF Settings…** (A5, A4, US Letter, US Trade, Digest, or Mass Market Paperback). The imprint page shows the cover thumbnail, contributors, publisher, and ISBN(s) near the top with the copyright statement and disclaimer pinned to the bottom — something only a fixed-layout format like PDF can do; the same content on the EPUB imprint page just reads top-to-bottom in that order instead. Both EPUB and PDF exports finish with a result window showing word/page counts and buttons to open the generated file or its folder.

Every chapter also starts on a new page in the EPUB (`page-break-before`/`break-before: page` in the template CSS), matching the PDF's pagination as closely as a reflowable format allows.

## CSS templates

EPUB export styling comes from a `templates/` directory next to the installed app (not per-project), holding one `.css` file per template — the filename (minus extension) is the display name shown in the Metadata editor's Style picker. A "Default" template is seeded automatically from the built-in stylesheet the first time the picker is opened. The list is rescanned every time the picker opens, so dropping a new `.css` file into `templates/` makes it available immediately. The book's chosen template is saved in its metadata and used whenever that book is exported to EPUB. A "Vellum Serif" template ships alongside "Default", adapted from a Vellum-generated reference EPUB.

## Fonts

A `fonts/` directory ships next to `templates/` and holds the font files a template's `@font-face` rules point at. When a book is exported to EPUB, `EpubBuilder` embeds whichever of those fonts the selected template actually references. When the template picker's selection changes, `FontInstallerService` also copies any of those fonts that aren't already present into the current user's font directory (macOS/Windows/Linux), so the same fonts render correctly outside the EPUB too — e.g. in the app's own editor preview.

## Known limitations

This is a working first pass covering the full pipeline end to end (author → organize → import → export), not a finished product. Notable simplifications:

- Font-installation only checks whether a same-named font *file* already exists in the user's font directory — it doesn't query the OS font registry by family name, which would need a different native API per platform (CoreText/DirectWrite/Fontconfig).
- No automated visual/UI testing was possible in the environment this was built in (a sandboxed session without a real windowing session for the Avalonia/macOS native render timer) — correctness was verified through the full xUnit suite plus running real builds and inspecting generated output, not through interactive UI testing. Drag-and-drop file import in particular could only be verified at the service-logic level (unit tests), not by actually dragging a file onto the running app.
- A dropped/imported `.docx` file may still split into multiple chapters if it contains internal "Chapter N" headings or Heading-1-styled text — same detection as the whole-manuscript DOCX import. If it does, any position hint from the file's own name is dropped (it wouldn't make sense across several resulting chapters).
- PDF export doesn't yet use a book's selected CSS template/fonts — it renders with a fixed default typeface. Code blocks aren't rendered in the PDF or the Word export yet (headings, paragraphs, bold/italic, links, lists, tables, images, and footnotes all are).
- Footnotes render as an endnotes-style "Notes" section at the end of each chapter in the PDF (reference numbers as superscript, full text collected at chapter end) rather than at the bottom of the exact physical page that references them — genuine per-page footnote placement would need a custom pagination engine. Word gets real native footnotes (visible in Word's own footnote pane); EPUB gets standard clickable footnote/backlink HTML, both via Markdig's footnote support.
- PDF front-matter roman numerals assume each front-matter page (title/imprint/TOC) renders as exactly one physical page, which holds for the short generated content this app produces; a very long hand-authored front-matter page would throw off the numbering for pages after it.

## Contact

John Dovey — dovey.john@gmail.com
