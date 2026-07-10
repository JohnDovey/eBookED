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

The file also includes the markers Kindle Direct Publishing looks for when you upload it: the cover image is correctly linked so it shows up as the book's cover (not just its first page), and the file tells the reading system exactly where the "real" first page of the book is — so readers land on Chapter 1, not the title page, when they open it or tap "Go to Beginning."

## PDF

**Export → PDF…**

Produces a print-formatted PDF: every chapter (and front/back matter page) starts on its own physical page, front matter is numbered with lowercase roman numerals (i, ii, iii…) while the body counts up in arabic numerals (1, 2, 3…), and the table of contents has real, clickable, page-numbered entries. Page size comes from **Meta Data → PDF Settings…**.

PDF export uses the same CSS template as EPUB: it picks up the template's body and heading fonts and embeds them into the PDF, so headings and body text render in the same typeface as the EPUB rather than a generic default. Tables and images render too, and any style from **Apply Style** (see *Writing in the Editor*) — small caps, verse, attribution, and the rest — actually applies to the exported PDF now, not just the EPUB.

The imprint page in the PDF is laid out slightly differently than in the EPUB: the cover, contributors, publisher, and ISBN sit near the top, with the copyright statement pinned to the very bottom of the page — something only a fixed page layout can do.

Every page also gets a running header and footer, following the same left/right (verso/recto) convention printed books use. Even-numbered ("left") pages show the page number and the book's title in the header, and the page number and the author's name in the footer. Odd-numbered ("right") pages show the current chapter's number and name and the page number in the header, and just the page number in the footer.

## Word

Right-click a chapter in the sidebar and choose **Export Chapter as Word…** to export just that chapter as a standalone `.docx` file, or use **Export → Word (Whole Book)…** to export the whole book — front matter, every chapter, and back matter — as one `.docx` file, each section starting on its own page. Either way, headings map to Word's built-in heading styles, tables become real Word tables, images are embedded at their real size, and **Apply Style** classes come through using real Word formatting — small caps and letter spacing use Word's own primitives for them, for instance, not just an approximation.

## After export

EPUB and PDF exports finish with a small result window showing the word count (and, for PDF, page count), with **Open File** and **Open Folder** buttons so you can jump straight to the result.
