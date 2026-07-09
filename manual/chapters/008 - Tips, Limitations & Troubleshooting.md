---
title: Tips, Limitations & Troubleshooting
subtitle: 
numberMode: Auto
numberOverride: 
---

# Tips, Limitations & Troubleshooting

## Where your files live

Every project is a plain directory — nothing is hidden in a proprietary format:

```
<ProjectName>/
  project.ebookproj.json   metadata + chapter order
  book.md                  auto-regenerated master index
  frontmatter/             title-page.md, copyright.md, toc.md
  chapters/                "NNN - Chapter Title.md", one file per chapter
  backmatter/              about-the-author.md, plus any free-form pages you add
  images/                  cover, author photo, publisher logo, chapter images
  output/                  generated .epub / .pdf / .docx / .md land here
```

Because it's just files, a project directory works well under version control (Git, or any other) if you want a full history of your manuscript.

## Known limitations

- Font installation only checks whether a same-named font *file* already exists in your system font directory — it doesn't check the OS's font registry by name, so a font installed under a different file name won't be detected as already present.
- A dropped or imported `.docx` file may split into multiple chapters if it contains internal "Chapter N" headings or Heading 1-styled text, the same as a whole-manuscript import.
- PDF export picks up a template's *body* and *heading* (h1) fonts, not every heading level or the blockquote/emphasis display faces a template might define — a close approximation of the template's look, not a full CSS engine.
- PDF footnotes collect at the end of each chapter rather than at the bottom of the exact physical page that references them (see *Exporting Your Book*).
- PDF front-matter page numbering assumes each front-matter page renders as exactly one physical page, which holds for the auto-generated content this app produces.

## Getting help

eBook Editor is developed at [github.com/JohnDovey/eBookED](https://github.com/JohnDovey/eBookED). This manual is itself an eBook Editor project — if something here is unclear or out of date, it's worth checking the app's own README in that repository for the most current details.
