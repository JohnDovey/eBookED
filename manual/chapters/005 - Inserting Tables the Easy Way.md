---
title: Inserting Tables the Easy Way
subtitle: 
numberMode: Auto
numberOverride: 
---

# Inserting Tables the Easy Way

HTML tables aren't hard to write by hand (see *HTML Syntax Reference*), but typing out `<table>`/`<tr>`/`<td>` tags for anything bigger than a couple of columns gets tedious fast. Right-click anywhere in the editor and choose **Insert Table…** for a visual builder instead.

## Building a table

The Insert Table window opens with a starting 3-column grid — the top row is the table's header, every row below it is data. Type directly into any cell.

- **+ Row** / **− Row** — add or remove a data row (there's always at least one header row and one data row).
- **+ Column** / **− Column** — add or remove a column, including its header.
- The dropdown above each column sets that column's alignment: **Default**, **Left**, **Center**, or **Right**. This controls each header cell's `text-align` style.

An **HTML preview** panel at the bottom always shows exactly what will be inserted, updated live as you type or change alignment — so you can see the real markup before committing to it.

![Illustrative mockup of the Insert Table window: row/column controls, alignment dropdowns, grid, and live HTML preview](../images/insert-table-window.png)

## Inserting or discarding

- **Insert** writes the generated HTML table at wherever your cursor was in the editor, then closes the window.
- **Discard** closes the window without changing anything.

## Example

Filling in a 3×2 table (Feature / Format / Notes headers, two data rows, right column right-aligned) produces:

```
<table>
<thead>
<tr><th>Feature</th><th>Format</th><th style="text-align: right">Notes</th></tr>
</thead>
<tbody>
<tr><td>Chapters</td><td>EPUB</td><td style="text-align: right">Reflowable</td></tr>
<tr><td>Chapters</td><td>PDF</td><td style="text-align: right">Fixed page layout</td></tr>
</tbody>
</table>
```

Cell text is automatically HTML-escaped, so a literal `<`, `>`, or `&` in what you type is preserved as text rather than being mistaken for markup, and line breaks inside a cell become `<br>` tags.
