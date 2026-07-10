---
title: Markdown Syntax Reference
subtitle: 
numberMode: Auto
numberOverride: 
---

# Markdown Syntax Reference

Every chapter, and every front/back matter page, is written in **Markdown** — plain text with a small set of symbols that mean "make this bold," "make this a heading," and so on. This chapter covers everything eBook Editor understands, with a "how you type it" example next to each one. Click **Open Preview** at any time to see how your own text will actually look.

## Headings

Start a line with one to six `#` characters to make it a heading. eBook Editor maps the first three levels to a chapter's own structure, so keep `#` for a chapter's main title and use `##`/`###` for sections within it.

```
# Chapter Title
## A Section
### A Sub-section
```

### Linking to a specific heading

Give any heading its own ID by adding `{#your-id}` at the end of the line, then link to it from anywhere in the same file with `[link text](#your-id)`. This is how you'd build an in-chapter index or a "back to top" link.

```
## Further Reading {#further-reading}

See the [Further Reading](#further-reading) section below.
```

You can add a CSS class the same way with a dot (`{.myclass}`), or combine several: `{.myclass #further-reading lang=fr}`. This same `{...}` attribute syntax also works on fenced code blocks, links, and images — right after the closing parenthesis for links/images, e.g. `[link](url){.myclass}`.

## Emphasis

```
Plain text, *italic text*, **bold text**, and ***bold italic text***.
```

Plain text, *italic text*, **bold text**, and ***bold italic text***.

### Strikethrough, highlight, subscript, and superscript

```
~~struck through~~, ==highlighted==, H~2~O, and E=mc^2^.
```

~~struck through~~, ==highlighted==, H~2~O, and E=mc^2^.

## Lists

Bullet lists start each line with `-` (or `*`); numbered lists start each line with a number and a period.

```
- First item
- Second item
- Third item

1. Step one
2. Step two
3. Step three
```

### Task lists

```
- [x] Done task
- [ ] Not done yet
```

- [x] Done task
- [ ] Not done yet

## Definition lists

A term, then a colon and at least three spaces before its definition on the next line. Multiple `:` lines under the same term become multiple definitions.

```
Markdown
:   A lightweight markup language for formatted plain text.
```

Markdown
:   A lightweight markup language for formatted plain text.

## Links

```
[eBook Editor on GitHub](https://github.com/JohnDovey/eBookED)
```

The text in square brackets is what the reader sees; the URL in parentheses is where it goes.

## Images

```
![A description of the image](../images/cover.jpg)
```

Image paths are relative to the file that references them — a chapter's images are typically stored in the project's `images/` folder and referenced as `../images/filename.jpg`.

## Blockquotes

Start a line with `>` to set it off as a quotation.

```
> The scariest moment is always just before you start.
> — Stephen King
```

## Footnotes

Reference a footnote inline with `[^label]`, then define its text anywhere in the same file with `[^label]: text`. The label just has to match — it doesn't have to be a number, and the definition doesn't have to sit right next to where it's used.

```
Markdown was created by John Gruber[^gruber], with input from Aaron Swartz.

[^gruber]: Gruber is also known for the blog Daring Fireball.
```

Markdown was created by John Gruber[^gruber], with input from Aaron Swartz.

[^gruber]: Gruber is also known for the blog Daring Fireball.

Where a footnote ends up depends on the export format:

- **EPUB** — a clickable superscript number in the text jumps to the note, with a "return" link back where you were.
- **PDF** — the same superscript number in the text, with the full text collected into a "Notes" section at the end of the chapter that references it.
- **Word** — a real, native Word footnote, visible in Word's own footnote pane at the bottom of the page.

You don't need to do anything differently for any of these — the same `[^label]` syntax produces the right result in all three.

## Tables

A table is a row of column headers, a row of dashes separating the headers from the data, then one row per data row — every row starts and ends with `|`, with `|` separating each cell.

```
| Format | Where footnotes go               |
| ------ | --------------------------------- |
| EPUB   | Clickable inline link              |
| PDF    | "Notes" section at chapter end     |
| Word   | Native Word footnote               |
```

| Format | Where footnotes go               |
| ------ | --------------------------------- |
| EPUB   | Clickable inline link              |
| PDF    | "Notes" section at chapter end     |
| Word   | Native Word footnote               |

### Aligning columns

Add colons to the dashed separator row to control alignment: `:---` for left, `---:` for right, `:---:` for center. Without any colon, a column defaults to left alignment.

```
| Left | Center | Right |
| :--- | :----: | ----: |
| a    |   b    |     c |
```

You don't have to type tables by hand, either — right-click anywhere in the editor and choose **Insert Table…** for a visual table builder that writes this syntax for you. See *Inserting Tables the Easy Way*, next.

## Code

Wrap a word or short phrase in single backticks for inline code, like `` `this` ``. For a longer, multi-line block, use a fenced block: three backticks on their own line, your code, then three more backticks.

````
```
def greet(name):
    return f"Hello, {name}!"
```
````

## Styled blocks

Wrap a paragraph (or several) in a fenced block of colons with a class name to apply a named style, hooked to a matching CSS rule in the book's template:

```
::: {.smallcaps}
This paragraph renders in small caps.
:::
```

You don't have to remember this syntax — right-click any selected text in the editor and choose **Apply Style** for a menu of the styles eBook Editor ships with (Small Caps, Underline, Strikethrough, Monospace, Sans-serif, All Caps, Verse, Inset, Attribution, Drop Cap, Caption), and it writes the `:::` block around your selection for you. See *Book Metadata → Style* for how the underlying CSS classes are defined.

### Inserting images with a caption

Right-click the editor and choose **Insert Image…** to pick a picture and add it with a caption underneath, styled half-size and italic (the **Caption** style above). Behind the scenes it's a nested block:

```
::::
![Alt text](../images/photo.jpg)

::: {.caption}
Caption text
:::
::::
```

This isn't a Markdown table — attaching a style directly to a table breaks the table entirely, so the image and its caption are two nested blocks instead, which the **Insert Image…** menu writes for you. See *Writing in the Editor → The right-click menu*.

## Line breaks

A blank line between two lines of text starts a new paragraph. To force a line break *within* the same paragraph (without starting a new one), end the line with two trailing spaces, or an explicit `<br>`.
