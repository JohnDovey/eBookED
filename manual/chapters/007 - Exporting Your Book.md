---
title: Exporting Your Book
subtitle: 
numberMode: Auto
numberOverride: 
---

# Exporting Your Book

The **Export** menu turns your project into a finished file. Every export runs against whatever's currently saved on disk, so save your project first if you've just made changes.

## EPUB

**Export → EPUB…**

Produces a standards-compliant EPUB 3.3 file — the format read by Apple Books, Kobo, most e-readers, and (via conversion) Kindle. Every chapter starts on a new page, styled using the book's selected CSS template (see *Book Metadata → Style*), with that template's fonts embedded directly into the file so it looks the same on any device. Tables, images, and footnotes all render natively as clickable, reflowable HTML.

## PDF

**Export → PDF…**

Produces a print-formatted PDF: every chapter (and front/back matter page) starts on its own physical page, front matter is numbered with lowercase roman numerals (i, ii, iii…) while the body counts up in arabic numerals (1, 2, 3…), and the table of contents has real, clickable, page-numbered entries. Page size comes from **Meta Data → PDF Settings…**.

PDF export uses the same CSS template as EPUB: it picks up the template's body and heading fonts and embeds them into the PDF, so headings and body text render in the same typeface as the EPUB rather than a generic default. Tables and images render too. Footnotes appear as a superscript reference number in the text with the full note collected into a "Notes" section at the end of the chapter that references it — a deliberate choice, since placing a note at the bottom of the exact physical page that references it (the way a printed book does) would need a much more complex layout engine than this app aims for.

The imprint page in the PDF is laid out slightly differently than in the EPUB: the cover, contributors, publisher, and ISBN sit near the top, with the copyright statement pinned to the very bottom of the page — something only a fixed page layout can do.

## Word (a single chapter)

Right-click a chapter in the sidebar and choose **Export Chapter as Word…** to export just that chapter as a standalone `.docx` file. Headings map to Word's built-in heading styles, tables become real Word tables, images are embedded at their real size, and footnotes become genuine native Word footnotes (visible in Word's own footnote pane, not just text at the bottom of the page).

## Markdown

**Export → Markdown (Whole Book)…** concatenates every chapter (front matter stripped) into one plain `.md` file, in reading order — useful for handing your manuscript to a tool or person that just wants plain text.

**Export → Markdown (This Chapter)…** exports only the currently selected chapter the same way. This option is only enabled when a chapter (not front/back matter) is selected.

## After export

EPUB and PDF exports finish with a small result window showing the word count (and, for PDF, page count), with **Open File** and **Open Folder** buttons so you can jump straight to the result.
