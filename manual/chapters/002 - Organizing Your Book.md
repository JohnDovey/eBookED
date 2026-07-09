---
title: Organizing Your Book
subtitle: 
numberMode: Auto
numberOverride: 
---

# Organizing Your Book

## Front matter, chapters, and back matter

Every project's sidebar is organized into three groups, in the order they'll appear in the finished book:

- **Front matter** — the title page, imprint (copyright) page, and table of contents. These three are auto-generated from your metadata; you don't hand-write them (see *Book Metadata*).
- **Chapters** — the book's actual content, in reading order. This is the only group you can freely add to, reorder, and delete from.
- **Back matter** — currently the About the Author page, also auto-generated, always placed after every chapter.

## Adding a chapter

Click **+ Add Chapter** at the top of the sidebar. A new, empty chapter is created, added to the end of the chapter list, and opened in the editor ready for you to start typing.

## Reordering chapters

Chapters can be dragged up and down within the sidebar to reorder them. Front matter and back matter stay fixed in place — you can't drag a chapter above the title page or below the About the Author page.

Whenever chapters are reordered, added, or removed, eBook Editor renames the chapter files on disk to match their new position (e.g. `003 - Chapter Title.md`) so they sort correctly in Finder or Explorer too. This is a rename, not a content rewrite — your chapter's text and any images it references are untouched.

## Setting a chapter's title and number

With a chapter selected, two fields appear above the editor: **Chapter title** and **Chapter subheading**. Edit them and click **Apply** to update the chapter's heading without touching its body text.

## Importing chapters

New chapters can come from three places, all producing the same result:

1. **File → Import Chapters…** — a file picker. Select one or more `.md`, `.docx`, or `.html`/`.htm` files.
2. **Drag files directly onto the sidebar.**
3. **Drop files into the project's `chapters/` folder** using Finder/Explorer while the project is open — eBook Editor notices them the next time the project opens.

`.md` files are used as-is. `.docx` files go through the same chapter-boundary detection as **File → Import DOCX…** (splitting on Heading 1 styles or "Chapter N" text, if present). `.html`/`.htm` files are converted to Markdown automatically.

A file's name can hint at where it belongs: naming a file `"23. What Now.md"` or `"007 - Foo.md"` tells eBook Editor roughly where in the chapter order to place it.

## Importing a whole manuscript

**File → Import DOCX…** takes a single large `.docx` file (a whole manuscript) and splits it into multiple chapters automatically, detecting boundaries by Heading 1 style or "Chapter N" text patterns. Bold/italic formatting, headings, lists, images, tables, and hyperlinks are all converted to Markdown.

## The chapter right-click menu

Right-click any chapter in the sidebar for two actions:

- **Delete Chapter** — asks for confirmation, then removes the chapter and renumbers the ones after it.
- **Export Chapter as Word…** — exports just that one chapter as a standalone `.docx` file (see *Exporting Your Book*).

## Saving

Chapters and metadata edits aren't written to disk until you save — **File → Save Project** or **Cmd/Ctrl+S**. Closing a window with unsaved changes will prompt you first.
