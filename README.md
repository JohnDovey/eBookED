# eBook Editor

A cross-platform .NET desktop application for authoring, organizing, and publishing eBooks. Each book lives in its own project directory of HTML chapter files (`.ebhtml`) plus a metadata file, with title page, imprint (copyright) page, table of contents, and about-the-author page generated automatically from that metadata. Chapters can be reordered by drag-and-drop, imported by dragging or picking `.ebhtml`/`.docx`/`.html` files, and exported as EPUB 3.3, a print-formatted PDF, or a Word document (whole book or a single chapter).

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
| `eBookEditor.Core` | Domain models (`BookMetadata`, `SpineItem`, `EbookProject`, …) and services with no UI or format dependencies: `ProjectService` (create/load/save), `SpineService` (ordering/numbering), `ChapterFileService` (YAML front matter + body, chapter file renumbering), `ChapterFileNaming` (chapter filename ↔ number/title parsing), `AppSettingsService` (cross-project autofill/MRU). |
| `eBookEditor.Html` | Format-neutral HTML generation and rendering support with no UI dependency: `PageGeneratorService` (title/copyright/TOC/about-the-author page generation), `BookIndexGenerator` (`book.ebhtml`), `HtmlBookAssembler` (whole-book concatenation for Word export), `HtmlPageShell` (wraps a chapter body + the book's CSS template into a standalone document for the Preview window and WYSIWYG editor pane), and an `AngleSharp.Css`-based cascade/specificity/inheritance resolver (`HtmlStyleDocument`, `CssValueParser`, `CssColor`) shared by the PDF and Word renderers. |
| `eBookEditor.Migration` | `ProjectMigrator` — the one-time, user-triggered "Upgrade Project to HTML…" batch conversion for projects created before the HTML content-model refactor, still stored as Markdown `.md` files. Keeps Markdig as a NuGet-only, migration-only dependency purely for this (`MarkdownToHtmlConverter`/`MarkdownPipelineFactory`) — the only place in the app that still touches Markdown. Converts every spine item's body verbatim, including hand-edited generated pages (title/imprint/TOC/about-author), rather than regenerating them from metadata, so hand-edits aren't silently discarded. |
| `eBookEditor.Epub` | Hand-rolled EPUB 3.3 writer (`EpubBuilder`) — container.xml, package.opf, nav.xhtml, legacy toc.ncx, XHTML content docs (with internal chapter links rewritten so the in-book TOC page is clickable), image and font bundling, selectable CSS `TemplateService`, `FontService`/`FontInstallerService` — plus a structural `EpubValidationHelper`. `EpubNavDocumentWriter` also emits an EPUB3 `<nav epub:type="landmarks">` block (titlepage/toc/`bodymatter`) so Kindle and other reading systems open straight to Chapter 1 instead of the title page, and the OPF's legacy `<meta name="cover">` correctly references the cover image's actual manifest item id. |
| `eBookEditor.DocxImport` | `.docx` → chapters importer built on `DocumentFormat.OpenXml`: `ChapterBoundaryDetector` finds chapter boundaries (Heading 1 style or "Chapter N" text), `OpenXmlToHtmlConverter` converts bold/italic/headings/lists/images/tables/hyperlinks to HTML. Also the reverse: `HtmlToDocxConverter` exports a chapter's (or, fed `HtmlBookAssembler.AssembleWholeBook`'s output, the whole book's) HTML body back to `.docx` (headings, bold/italic/strikethrough/underline/highlight/sub/superscript, links, lists, tables, images, code blocks, and `<hr>` thematic breaks as page breaks), resolving the selected CSS template's real cascade via `eBookEditor.Html`'s `HtmlStyleDocument` so `EditorStyleCatalog` classes and arbitrary template CSS actually render (including true small-caps/letter-spacing/all-caps, which only Word has a native primitive for). |
| `eBookEditor.ChapterImport` | Imports a dropped or picked file as one or more chapters: `.ebhtml`/`.md` used as-is, `.docx` via `eBookEditor.DocxImport`, `.html`/`.htm` via `HtmlImportSanitizer` (an AngleSharp-based sanitizing pass-through — strips `<script>`/`<style>`/`javascript:` URLs/inline event handlers — rather than a format conversion, since the target format is already HTML). `OrphanChapterScanner` finds chapter files (any of the extensions above) sitting in a project's `chapters/` folder that aren't in the spine yet (e.g. dropped in via Finder/Explorer) so they get picked up automatically. |
| `eBookEditor.Pdf` | Print-formatted PDF export via [QuestPDF](https://www.questpdf.com/) (Community license). `PdfBuilder` starts every chapter and front/back-matter page on its own page, numbers front matter with lowercase roman numerals and the body with arabic numerals (`PdfPageNumberFormatter`), and renders a table of contents with real, clickable, correctly-formatted page numbers via QuestPDF's Section/SectionLink support. Running headers (`PdfPageHeader`) and footers (`FrontMatterAwarePageNumberFooter`) follow the standard print-book left/right (verso/recto) layout: left pages show the page number and book title in the header, page number and author in the footer; right pages show the current chapter's number/name and page number in the header, and just the page number in the footer. The imprint page is rendered directly from metadata (not through the generic renderer) so its copyright statement can be pinned to the bottom of the page via `ExtendVertical().AlignBottom()`. `PdfTemplateFonts` reuses the Epub project's `TemplateService`/`FontService` to register the book's selected CSS template's `@font-face` fonts with QuestPDF and resolve its body/heading font families, so the PDF uses (and embeds) the same typography as the EPUB. `PdfPageSizeCatalog` (in Core, so the settings UI doesn't need a PDF library reference) defines the selectable page sizes. |
| `eBookEditor.App` | The Avalonia desktop shell (MVVM via CommunityToolkit.Mvvm): a title bar menu (File/Project/Meta Data/Export/About), New Project wizard, sidebar with drag-to-reorder chapters rendered as an ordered list (right-click a chapter for Delete/Export as Word), and an editor pane with two interchangeable modes over the same HTML content — raw HTML (AvaloniaEdit, with HTML syntax highlighting) and Rich Text (a `NativeWebView`-based WYSIWYG pane, `contenteditable`, styled with the book's actual CSS template), toggled by a **Rich Text** button. An **Open Preview** button opens a standalone `PreviewWindow`, also `NativeWebView`-based, kept in sync with the editor's content and, in raw HTML mode, approximate cursor position. A toolbar (working in either editor mode) offers **Insert Table…** (a visual table builder — grid, alignment dropdowns, live HTML preview, Discard/Insert), **Insert Image…** (picks/copies an image into the project's `images/` folder and inserts it as a captioned `<figure>`), **Insert Footnote…**, and **Apply Style ▾** (wraps the current selection in an `EditorStyleCatalog`-listed style's `<span>`/`<div>` and class). Six metadata forms live under the Meta Data menu (Front Matter, Style, Copyright & Publishing, Genre Tags, About the Author, PDF Settings), alongside the EPUB/PDF/Word export and DOCX/chapter import actions. EPUB and PDF exports finish with a result modal (`GenerationResultWindow`) showing word/page counts and links to open the file or its folder. Also accepts dragged-in chapter files directly onto the sidebar. Projects open in independent windows, so several books can be open simultaneously. |
| `tests/eBookEditor.Tests` | xUnit coverage across all of the above, including `.docx` fixtures built programmatically with the OpenXml SDK (no binary test fixtures needed) and generated EPUBs validated both by `EpubValidationHelper` and, during development, `unzip`/`xmllint`. |

## Project directory layout

Each book is a directory, not a single file:

```
<ProjectName>/
  project.ebookproj.json   metadata + ordered "spine" (front matter / chapters / back matter)
  book.ebhtml              auto-regenerated master index linking every file in spine order
  frontmatter/             title-page.ebhtml, copyright.ebhtml, toc.ebhtml — auto-generated from metadata
  chapters/                "NNN-Chapter-Title.ebhtml" (e.g. "023-What-Now.ebhtml"), each with a
                            small YAML front-matter block (title, subtitle, number: auto|override|none)
  backmatter/               about-the-author.ebhtml (auto-generated) plus free-form pages
  images/                   cover, author photo, publisher logo, chapter images
  output/                   generated .epub / .pdf / .docx land here
```

Chapter files are renamed to match their resolved position after any add/import/reorder, so they sort correctly in Finder/Explorer the same way they're ordered in the book — this is a rename, not a content rewrite, so a chapter's own body content and images are unaffected.

## Importing chapters

New chapters can come from three places, all producing the same result: **File → Import Chapters…** (a file picker), dragging files directly onto the sidebar, or files already sitting in a project's `chapters/` folder that the app doesn't know about yet (picked up automatically on open). `.ebhtml` files (and legacy `.md` files) are used as-is, `.docx` files go through the same chapter-boundary detection as the whole-manuscript DOCX import, and `.html`/`.htm` files are sanitized (scripts and event handlers stripped) and kept as HTML — there's no conversion step, since HTML is already this app's native storage format. A file's name supplies its title and, if it starts with a number (`"23. What Now.ebhtml"`, `"007 - Foo.docx"`), a hint for where it belongs in the chapter order.

## PDF export

**File → Export → PDF…** produces a print-formatted PDF: every chapter (and front/back matter page) starts on its own page, front matter is numbered i/ii/iii while the body counts 1/2/3, and the table of contents is clickable with real, correctly-formatted page numbers. Page size is set per-book via **Meta Data → PDF Settings…** (A5, A4, US Letter, US Trade, Digest, or Mass Market Paperback). The imprint page shows the cover thumbnail, contributors, publisher, and ISBN(s) near the top with the copyright statement and disclaimer pinned to the bottom — something only a fixed-layout format like PDF can do; the same content on the EPUB imprint page just reads top-to-bottom in that order instead. Both EPUB and PDF exports finish with a result window showing word/page counts and buttons to open the generated file or its folder.

Every page also gets a running header and footer, following the standard print-book left/right (verso/recto) layout: even-numbered ("left") pages show the page number and the book's title in the header, and the page number and the author's name in the footer; odd-numbered ("right") pages show the current chapter's number and name and the page number in the header, and just the page number in the footer.

Every chapter also starts on a new page in the EPUB (`page-break-before`/`break-before: page` in the template CSS), matching the PDF's pagination as closely as a reflowable format allows.

### Kindle (KDP) support

The generated EPUB includes the markers Kindle Direct Publishing's own ingestion pipeline looks for: an EPUB3 `<nav epub:type="landmarks">` block in `nav.xhtml` with a `bodymatter` entry pointing at the first real chapter (so "Go to Beginning" and KDP's converted `.mobi`/`.azw3` open on Chapter 1, not the title page — the modern replacement for the deprecated OPF `<guide>` element), and a legacy `<meta name="cover" content="...">` entry in the OPF that correctly references the cover image's actual manifest item id (not a placeholder string), which older Kindle tooling still reads to identify the cover.

## HTML syntax

Every chapter, and every front/back-matter page, is written as an HTML fragment — the same tags used by any web page (`<p>`, `<h1>`–`<h6>`, `<em>`/`<strong>`, `<ul>`/`<ol>`/`<li>`, `<a href="...">`, `<img src="...">`, `<blockquote>`, `<table>`/`<thead>`/`<tbody>`, `<code>`/`<pre>`, `<s>`/`<mark>`/`<sub>`/`<sup>` for strikethrough/highlight/subscript/superscript). Any tag can carry an `id` (so headings can be linked to from elsewhere in the same file with `<a href="#my-id">`, for in-book "back to top"/index-style navigation) and a `class` (to hook it to a CSS rule in the book's template — see "Applying styles" below). A table header cell's `style="text-align: left/center/right"` controls that column's alignment. Right-click the editor → **Insert Footnote…** writes a numbered `<sup id="fnref:N"><a href="#fn:N" class="footnote-ref">` reference plus a matching entry in a `<div class="footnotes">` list at the end of the page. EPUB (a full browser-grade rendering engine) makes that a real clickable jump-and-return link, styled via the `.footnote-ref`/`.footnotes`/`.footnote-back-ref` rules in `DefaultStylesheet.cs`. Word export recognizes the same shape and converts it to a real native footnote (`FootnotesPart`/`FootnoteReference`) instead — see "Known limitations" for PDF, which has no per-page footnote placement primitive.

### Applying styles

Wrap a span of text in `<span class="...">` (inline styles) or a paragraph in `<div class="...">` (block styles) to hook it to a named CSS class. Rather than typing that by hand, select text in the editor and right-click → **Apply Style** for a menu of eleven styles curated from a Vellum-generated reference EPUB's stylesheet plus one added for image captions (Small Caps, Underline, Strikethrough, Monospace, Sans-serif, All Caps, Verse, Inset, Attribution, Drop Cap, Caption) — see `EditorStyleCatalog` in `eBookEditor.Core`. Matching CSS rules ship in both the built-in stylesheet and "Vellum Serif.css" (`EditorStyleCatalogTests` asserts every catalog entry has a rule in both, so the two can't silently drift apart); a custom template that doesn't define one of these classes just renders the wrapped text unstyled. PDF and Word export resolve the selected template's real CSS cascade too (via `eBookEditor.Html`'s `HtmlStyleDocument`, an `AngleSharp.Css`-based specificity/inheritance resolver), so these styles render there as well, not just in EPUB.

### Inserting images

Right-click the editor → **Insert Image…** opens a file picker rooted at the project's `images/` folder (created if missing); picking a file from anywhere else copies it into `images/` first (auto-renamed on a name collision), so a chapter's images always end up self-contained in the project. The image is inserted as a real `<figure>`, with a `<figcaption class="caption">` for the caption text:

```
<figure>
<img src="../images/photo.jpg" alt="Alt text">
<figcaption class="caption">Caption text</figcaption>
</figure>
```

### Preview rendering vs. export rendering

The Open Preview window and the Rich Text editor pane both render via `NativeWebView` (a real browser engine — WKWebView on macOS) wrapping the current chapter's HTML in the book's actual selected CSS template (`HtmlPageShell`, `eBookEditor.Html`). Unlike the pre-refactor Markdown.Avalonia-based preview, there's no separate parser and no gap between what Preview shows and what EPUB export produces — both are the same HTML styled with the same CSS.

## Word export

Right-click a chapter in the sidebar and choose **Export Chapter as Word…** to export just that chapter, or use **File → Export → Word (Whole Book)…** to export every front matter page, chapter, and back matter page as one `.docx` file (assembled by `HtmlBookAssembler`), each section starting on its own page (matching the EPUB/PDF pagination convention, via `<hr>` thematic breaks converted to page breaks). Both go through the same `HtmlToDocxConverter`, so headings, formatting, tables, images, and code blocks all round-trip the same way regardless of which export you use, and the selected CSS template's real cascade is resolved the same way PDF export resolves it.

## CSS templates

EPUB export styling comes from a `templates/` directory next to the installed app (not per-project), holding one `.css` file per template — the filename (minus extension) is the display name shown in the Metadata editor's Style picker. A "Default" template is seeded automatically from the built-in stylesheet the first time the picker is opened. The list is rescanned every time the picker opens, so dropping a new `.css` file into `templates/` makes it available immediately. The book's chosen template is saved in its metadata and used whenever that book is exported to EPUB. A "Vellum Serif" template ships alongside "Default", adapted from a Vellum-generated reference EPUB.

## Fonts

A `fonts/` directory ships next to `templates/` and holds the font files a template's `@font-face` rules point at. When a book is exported to EPUB, `EpubBuilder` embeds whichever of those fonts the selected template actually references. When the template picker's selection changes, `FontInstallerService` also copies any of those fonts that aren't already present into the current user's font directory (macOS/Windows/Linux), so the same fonts render correctly outside the EPUB too — e.g. in the app's own editor preview.

## User Manual

`manual/` at the repo root is itself an eBook Editor project — the end-user manual, written and maintained using the app it documents. Open it like any other project (**File → Open Project…**, pointed at `manual/`) to read or edit it, or export it (EPUB/PDF/Word) the same way you would any book. Its chapters cover getting started, organizing a book, the editor, a full HTML syntax reference (headings, emphasis, lists, links, images, footnotes, tables, code), the Insert Table modal, metadata, and exporting. `manual/output/` (generated exports) isn't tracked in git — regenerate it from the app whenever you need a fresh copy to bundle with a release.

**Keep it in sync**: when app behavior or UI changes, update the relevant `manual/chapters/*.md` file(s) in the same change — the manual documents the app's actual current behavior, not a snapshot from whenever it was written.

## Known limitations

This is a working first pass covering the full pipeline end to end (author → organize → import → export), not a finished product. Notable simplifications:

- Font-installation only checks whether a same-named font *file* already exists in the user's font directory — it doesn't query the OS font registry by family name, which would need a different native API per platform (CoreText/DirectWrite/Fontconfig).
- No automated visual/UI testing was possible in the environment this was built in (a sandboxed session without a real windowing session for the Avalonia/macOS native render timer) — correctness was verified through the full xUnit suite plus running real builds and inspecting generated output, not through interactive UI testing. Drag-and-drop file import in particular could only be verified at the service-logic level (unit tests), not by actually dragging a file onto the running app.
- A dropped/imported `.docx` file may still split into multiple chapters if it contains internal "Chapter N" headings or Heading-1-styled text — same detection as the whole-manuscript DOCX import. If it does, any position hint from the file's own name is dropped (it wouldn't make sense across several resulting chapters).
- PDF export uses the book's selected CSS template's body/heading fonts (embedding the same font files the EPUB embeds) when the template defines custom `@font-face` fonts; otherwise it falls back to Times New Roman. Only the body and h1 font are picked up (not every heading level or blockquote/emphasis face) — a reasonable approximation of the template's typography, not a full CSS engine.
- Footnotes (inserted via **Insert Footnote…**) get a real clickable jump/return link in EPUB, styled via the built-in `.footnote-ref`/`.footnotes`/`.footnote-back-ref` CSS rules. Word gets a real native footnote (`FootnotesPart`/`FootnoteReference`, visible in Word's own footnote pane) — the reference becomes a real footnote mark rather than literal superscript text, and the note's content moves out of the body entirely. PDF collects notes into a "Notes" section at the end of each chapter/page rather than at the bottom of the exact physical page that references them — true per-page-bottom footnotes would need a custom pagination engine QuestPDF doesn't offer.
- PDF front-matter roman numerals assume each front-matter page (title/imprint/TOC) renders as exactly one physical page, which holds for the short generated content this app produces; a very long hand-authored front-matter page would throw off the numbering for pages after it.
- PDF and Word export resolve the selected template's real CSS cascade (specificity, inheritance, and all) via `eBookEditor.Html`'s `AngleSharp.Css`-based `HtmlStyleDocument`, so `EditorStyleCatalog` classes (Small Caps, Drop Cap, etc.) and arbitrary template CSS actually render, not just structurally survive unstyled. A handful of effects still have no primitive in one or both engines: PDF has no small-caps or letter-spacing primitive (Word does, via `w:smallCaps`/`w:spacing`), and neither engine supports CSS `::first-letter` drop caps — see the doc comments atop `HtmlToPdfRenderer`/`HtmlToDocxConverter`.
- The preview window's "scroll to follow the cursor" sync (while editing in raw HTML mode) is proportional (cursor's line number ÷ total lines, mapped onto the preview's scroll-height fraction), not an exact mapping from a source line to the rendered element it produced. It lands you in roughly the right place, not the exact line, and doesn't track the cursor at all while editing in Rich Text mode (there's no comparable "line number" there).

## Contact

John Dovey — dovey.john@gmail.com
