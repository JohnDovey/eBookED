---
title: Writing in the Editor
subtitle: 
numberMode: Auto
numberOverride: 
---

# Writing in the Editor

## Editing a chapter or page

Select any chapter (or front/back matter page) in the sidebar and it opens in the editor pane on the right. This includes generated pages (title page, imprint, table of contents, About the Author): you can see and edit their content too, though hand-edits there get overwritten the next time that page regenerates from your metadata.

There are two ways to edit: **raw HTML** (the default) and **Rich Text**, toggled with the button of that name above the editor.

- **Raw HTML** is a text editor with line numbers, word wrap, and HTML syntax highlighting — you type the markup directly, the same as *HTML Syntax Reference* (next) describes.
- **Rich Text** shows the page as it'll actually look — styled with your book's selected CSS template, images inline, tables as real tables — and lets you type and select directly in that rendered view, the same way a word processor works. Both views edit the exact same underlying HTML; switching between them mid-edit doesn't lose anything.

## Previewing your work

Click **Open Preview** above the editor to open a separate window showing the current chapter as it'll actually look, styled with your book's selected CSS template — useful for checking a chapter at a larger size than Rich Text mode's editing pane, or while working in raw HTML mode. The preview window stays open and in sync as you keep working: its content updates as you edit or switch chapters, and (while in raw HTML mode) it scrolls to roughly track wherever your cursor is in the source, so you don't have to keep scrolling the preview by hand to see the section you're editing.

Only one preview window is needed at a time — clicking **Open Preview** again just brings the existing one back to the front instead of opening a second copy.

## The toolbar

A row of buttons above the editor works the same in either raw HTML or Rich Text mode:

- **Insert Table…** — opens a visual table builder. See *Inserting Tables the Easy Way*.
- **Insert Image…** — opens a picker starting in the project's `images/` folder (created automatically if it doesn't exist yet). Pick a file from anywhere — if it isn't already inside `images/`, it's copied in for you. The image is inserted as a `<figure>` along with a "Caption text" placeholder underneath, styled by the **Caption** style (see below); edit the caption text and the image's alt text to fit.
- **Insert Footnote…** — type the footnote's text in the dialog that opens; a numbered, clickable reference is inserted at the cursor, and the note itself is added to (or a new one started at the end of) a Notes list at the end of the page.
- **Apply Style ▾** — select some text first, then choose a style from the list (Small Caps, Underline, Strikethrough, Monospace, Sans-serif, All Caps, Verse, Inset, Attribution, Drop Cap, Caption). It wraps your selection in the HTML needed to hook it to that style, with no syntax to remember. See *HTML Syntax Reference → Styled blocks*, next.

Raw HTML mode also has all four of these on its right-click context menu, if you prefer that over the toolbar.

## Formatting your text

In raw HTML mode you write HTML directly, the same markup used by every web page — see *HTML Syntax Reference*, next, for the full set of tags eBook Editor understands, including footnotes and tables. In Rich Text mode you generally don't need to think about the underlying markup at all, aside from using the toolbar for the handful of things (tables, images, footnotes, styles) that don't have an obvious "just type it" equivalent.
