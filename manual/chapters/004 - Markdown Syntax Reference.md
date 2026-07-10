---
title: HTML Syntax Reference
subtitle: 
numberMode: Auto
numberOverride: 
---

# HTML Syntax Reference

Every chapter, and every front/back matter page, is written in **HTML** — the same markup used by every web page, wrapping your text in tags like `<p>` and `</p>` to say "this is a paragraph." This chapter covers everything eBook Editor understands, with a "how you type it" example next to each one. Click **Open Preview** at any time to see how your own text will actually look.

## Paragraphs and headings

Wrap each paragraph in `<p>...</p>`. Wrap a heading in `<h1>` through `<h6>`. eBook Editor maps the first three levels to a chapter's own structure, so keep `<h1>` for a chapter's main title and use `<h2>`/`<h3>` for sections within it.

```
<h1>Chapter Title</h1>
<p>An opening paragraph.</p>
<h2>A Section</h2>
<h3>A Sub-section</h3>
```

### Linking to a specific heading

Give any heading its own ID with `id="..."`, then link to it from anywhere in the same file with `<a href="#your-id">`. This is how you'd build an in-chapter index or a "back to top" link.

```
<h2 id="further-reading">Further Reading</h2>

<p>See the <a href="#further-reading">Further Reading</a> section below.</p>
```

Any tag can carry a `class="..."` attribute to hook it to a CSS rule in the book's template — this is what **Apply Style** (below) does automatically for you.

## Emphasis

```
<p>Plain text, <em>italic text</em>, <strong>bold text</strong>, and <strong><em>bold italic text</em></strong>.</p>
```

Plain text, *italic text*, **bold text**, and ***bold italic text***.

### Strikethrough, highlight, subscript, and superscript

```
<p><s>struck through</s>, <mark>highlighted</mark>, H<sub>2</sub>O, and E=mc<sup>2</sup>.</p>
```

~~struck through~~, ==highlighted==, H~2~O, and E=mc^2^.

## Lists

Bullet lists use `<ul>`; numbered lists use `<ol>`. Either way, each item is an `<li>`.

```
<ul>
<li>First item</li>
<li>Second item</li>
<li>Third item</li>
</ul>

<ol>
<li>Step one</li>
<li>Step two</li>
<li>Step three</li>
</ol>
```

## Links

```
<a href="https://github.com/JohnDovey/eBookED">eBook Editor on GitHub</a>
```

The text between the tags is what the reader sees; the `href` is where it goes.

## Images

```
<img src="../images/cover.jpg" alt="A description of the image">
```

Image paths are relative to the file that references them — a chapter's images are typically stored in the project's `images/` folder and referenced as `../images/filename.jpg`.

## Blockquotes

Wrap a quotation in `<blockquote>`.

```
<blockquote>
<p>The scariest moment is always just before you start.</p>
<p>— Stephen King</p>
</blockquote>
```

## Footnotes

Right-click the editor and choose **Insert Footnote…** (see *Writing in the Editor → The right-click menu*) rather than typing this by hand — it writes both halves of the convention below and keeps the numbering consistent for you.

A footnote reference is a superscript link pointing at the note's id; the note itself lives in a "Notes" list at the end of the page, with a return link back to where it was referenced:

```
<p>Markdown was created by John Gruber<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup>, with input from Aaron Swartz.</p>

<div class="footnotes">
<hr>
<ol>
<li id="fn:1"><p>Gruber is also known for the blog Daring Fireball. <a href="#fnref:1" class="footnote-back-ref">↩</a></p></li>
</ol>
</div>
```

Where a footnote ends up depends on the export format:

- **EPUB** — a clickable superscript number in the text jumps to the note, with a "return" link back where you were.
- **PDF** — the same superscript number in the text, with the full text collected into a "Notes" section at the end of the chapter that references it.
- **Word** — a real, native Word footnote, visible in Word's own footnote pane at the bottom of the page.

You don't need to write any of this by hand — **Insert Footnote…** produces the right result in all three export formats.

## Tables

A table is `<table>` containing a `<thead>` of header cells (`<th>`) and a `<tbody>` of data rows (`<tr>` of `<td>`).

```
<table>
<thead>
<tr><th>Format</th><th>Where footnotes go</th></tr>
</thead>
<tbody>
<tr><td>EPUB</td><td>Clickable inline link</td></tr>
<tr><td>PDF</td><td>"Notes" section at chapter end</td></tr>
<tr><td>Word</td><td>Native Word footnote</td></tr>
</tbody>
</table>
```

| Format | Where footnotes go               |
| ------ | --------------------------------- |
| EPUB   | Clickable inline link              |
| PDF    | "Notes" section at chapter end     |
| Word   | Native Word footnote               |

### Aligning columns

Add a `style="text-align: left/center/right"` attribute to a header cell to control that column's alignment.

```
<table>
<thead>
<tr><th style="text-align: left">Left</th><th style="text-align: center">Center</th><th style="text-align: right">Right</th></tr>
</thead>
<tbody>
<tr><td>a</td><td>b</td><td>c</td></tr>
</tbody>
</table>
```

You don't have to type tables by hand, either — right-click anywhere in the editor and choose **Insert Table…** for a visual table builder that writes this markup for you. See *Inserting Tables the Easy Way*, next.

## Code

Wrap a word or short phrase in `<code>` for inline code, like `<code>this</code>`. For a longer, multi-line block, wrap it in `<pre><code>`.

```
<pre><code>def greet(name):
    return f"Hello, {name}!"</code></pre>
```

## Styled blocks

Wrap a span of text in `<span class="...">` (for inline styles) or a paragraph in `<div class="...">` (for block styles) to apply a named style, hooked to a matching CSS rule in the book's template:

```
<span class="smallcaps">This text renders in small caps.</span>
```

You don't have to remember this syntax — right-click any selected text in the editor and choose **Apply Style** for a menu of the styles eBook Editor ships with (Small Caps, Underline, Strikethrough, Monospace, Sans-serif, All Caps, Verse, Inset, Attribution, Drop Cap, Caption), and it wraps your selection in the right tag and class for you. See *Book Metadata → Style* for how the underlying CSS classes are defined.

### Inserting images with a caption

Right-click the editor and choose **Insert Image…** to pick a picture and add it with a caption underneath, styled by the **Caption** style above. Behind the scenes it's a `<figure>`:

```
<figure>
<img src="../images/photo.jpg" alt="Alt text">
<figcaption class="caption">Caption text</figcaption>
</figure>
```

See *Writing in the Editor → The right-click menu*.

## Line breaks

A new `<p>` starts a new paragraph. To force a line break *within* the same paragraph (without starting a new one), use `<br>`.
