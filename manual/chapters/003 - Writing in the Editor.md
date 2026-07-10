---
title: Writing in the Editor
subtitle: 
numberMode: Auto
numberOverride: 
---

# Writing in the Editor

## Editing a chapter or page

Select any chapter (or front/back matter page) in the sidebar and its raw HTML source opens in the editor pane on the right — a text editor with line numbers, word wrap, and HTML syntax highlighting. This includes generated pages (title page, imprint, table of contents, About the Author): you can see and edit their HTML too, though hand-edits there get overwritten the next time that page regenerates from your metadata.

## Previewing your work

The editor doesn't render the HTML inline — click **Open Preview** above it to open a separate window showing the current chapter as it'll actually look (headings, bold/italic text, lists, tables, images). The preview window stays open and in sync as you keep working: its content updates as you edit or switch chapters, and it scrolls to roughly track wherever your cursor is in the source, so you don't have to keep scrolling the preview by hand to see the section you're editing.

Only one preview window is needed at a time — clicking **Open Preview** again just brings the existing one back to the front instead of opening a second copy.

## The right-click menu

Right-click anywhere in the editor for a context menu with:

- **Insert Table…** — opens a visual table builder. See *Inserting Tables the Easy Way*.
- **Insert Image…** — opens a picker starting in the project's `images/` folder (created automatically if it doesn't exist yet). Pick a file from anywhere — if it isn't already inside `images/`, it's copied in for you. The image is inserted as a `<figure>` along with a "Caption text" placeholder underneath, styled by the **Caption** style (see below); edit the caption text and the image's alt text to fit.
- **Insert Footnote…** — type the footnote's text in the dialog that opens; a numbered, clickable reference is inserted at the cursor, and the note itself is added to (or a new one started at the end of) a Notes list at the end of the page.
- **Apply Style** — select some text first, then right-click and choose a style from the list (Small Caps, Underline, Strikethrough, Monospace, Sans-serif, All Caps, Verse, Inset, Attribution, Drop Cap, Caption). It wraps your selection in the HTML needed to hook it to that style, with no syntax to remember. See *HTML Syntax Reference → Styled blocks*, next.

## Formatting your text

The editor doesn't have a formatting toolbar — you write HTML directly, the same markup used by every web page. See *HTML Syntax Reference*, next, for the full set of tags eBook Editor understands, including footnotes and tables.
