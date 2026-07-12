namespace eBookEditor.Html.Services;

/// <summary>
/// Wraps a body-fragment HTML string (the same fragment stored in a .ebhtml chapter file) into
/// a standalone document with the given CSS inlined, plus a small JS bridge — used by both the
/// read-only Preview window and the WYSIWYG editor pane, so they render identically off one
/// shared template CSS. The bridge is injected either way (harmless when read-only, since
/// nothing calls its editing functions and contenteditable="false" means no real "input" event
/// can fire); NativeWebView auto-injects the "invokeCSharpAction" global the bridge posts
/// through, no host-side setup needed beyond handling WebMessageReceived.
/// </summary>
public static class HtmlPageShell
{
    public const string ContentElementId = "content";

    private const string BridgeScript =
        $$"""
        (function () {
          var content = document.getElementById('{{ContentElementId}}');
          var changeTimer = null;

          function notifyChange() {
            if (changeTimer) clearTimeout(changeTimer);
            changeTimer = setTimeout(function () {
              invokeCSharpAction(JSON.stringify({ event: 'change', html: content.innerHTML }));
            }, 250);
          }

          content.addEventListener('input', notifyChange);

          // Right-click support for WYSIWYG mode (see MainWindow.ShowWysiwygContextMenu): the
          // embedded native WebView's own context menu is suppressed only while editable (never
          // in read-only Preview, so that still gets its normal browser menu), and this app's
          // own menu is opened from the host side instead — a real Avalonia ContextMenu can't be
          // reliably wired to fire from a native WebView's own right-click the way it can for
          // ordinary Avalonia controls, so this app drives it itself via the same message-bridge
          // every other WYSIWYG command already uses. When the click landed on/inside a
          // &lt;figure&gt;, its current id/size/alignment/flow/caption are read back out (the
          // same &lt;figure style="..."&gt; shape ImagePlacement.ToFigureStyle produces) so
          // "Edit Image…" can pre-fill InsertImageWindow with them.
          content.addEventListener('contextmenu', function (e) {
            if (!content.isContentEditable) return;
            e.preventDefault();
            var figureEl = e.target.closest ? e.target.closest('figure') : null;
            var figure = null;
            if (figureEl && content.contains(figureEl)) {
              var img = figureEl.querySelector('img');
              var figcaption = figureEl.querySelector('figcaption');
              var style = figureEl.getAttribute('style') || '';
              var flowMatch = /float\s*:\s*(left|right)/.exec(style);
              var alignMatch = /text-align\s*:\s*(left|center|right)/.exec(style);
              figure = {
                id: figureEl.id,
                width: img ? (parseInt(img.getAttribute('width'), 10) || 0) : 0,
                height: img ? (parseInt(img.getAttribute('height'), 10) || 0) : 0,
                alignment: flowMatch ? flowMatch[1] : (alignMatch ? alignMatch[1] : 'center'),
                flow: !!flowMatch,
                caption: figcaption ? figcaption.textContent : ''
              };
            }
            invokeCSharpAction(JSON.stringify({ event: 'contextmenu', figure: figure }));
          });

          window.ebookEditor = {
            setContent: function (html) {
              content.innerHTML = html;
            },
            // Inserts a parsed HTML fragment at the current selection (replacing it, if any),
            // without document.execCommand — deliberately, per the plan's own reasoning: relying
            // on execCommand bets on legacy/deprecated browser behavior that real DOM
            // manipulation doesn't.
            insertHtml: function (html) {
              content.focus();
              var sel = window.getSelection();
              var range;
              if (sel.rangeCount && content.contains(sel.anchorNode)) {
                range = sel.getRangeAt(0);
              } else {
                range = document.createRange();
                range.selectNodeContents(content);
                range.collapse(false);
              }
              range.deleteContents();
              var template = document.createElement('template');
              template.innerHTML = html;
              var frag = template.content;
              var lastNode = frag.lastChild;
              range.insertNode(frag);
              if (lastNode) {
                var newRange = document.createRange();
                newRange.setStartAfter(lastNode);
                newRange.collapse(true);
                sel.removeAllRanges();
                sel.addRange(newRange);
              }
              notifyChange();
            },
            // Wraps the current selection in a new <tag class="className"> — how Apply Style,
            // Bold/Italic, and Insert Element all work in WYSIWYG mode. className is omitted
            // (falsy, e.g. null) for a plain semantic wrap like "strong" or a heading tag with
            // no class attribute at all. Falls back to extract-and-reinsert when the selection
            // boundary doesn't cleanly bracket a set of whole nodes (surroundContents throws in
            // that case, e.g. a selection spanning part of one paragraph and part of another).
            wrapSelection: function (tag, className) {
              var sel = window.getSelection();
              if (!sel.rangeCount) return;
              var range = sel.getRangeAt(0);
              if (range.collapsed || !content.contains(range.commonAncestorContainer)) return;
              var wrapper = document.createElement(tag);
              if (className) wrapper.className = className;
              try {
                range.surroundContents(wrapper);
              } catch (e) {
                var extracted = range.extractContents();
                wrapper.appendChild(extracted);
                range.insertNode(wrapper);
              }
              sel.removeAllRanges();
              var newRange = document.createRange();
              newRange.selectNodeContents(wrapper);
              sel.addRange(newRange);
              notifyChange();
            },
            // Wraps the current selection in <span id="id">, the WYSIWYG-mode half of "Mark
            // Link Destination" — same surroundContents/extract-and-reinsert shape as
            // wrapSelection above, but setting an id (a link target) rather than a class.
            wrapSelectionWithId: function (id) {
              var sel = window.getSelection();
              if (!sel.rangeCount) return;
              var range = sel.getRangeAt(0);
              if (range.collapsed || !content.contains(range.commonAncestorContainer)) return;
              var wrapper = document.createElement('span');
              wrapper.id = id;
              try {
                range.surroundContents(wrapper);
              } catch (e) {
                var extracted = range.extractContents();
                wrapper.appendChild(extracted);
                range.insertNode(wrapper);
              }
              sel.removeAllRanges();
              var newRange = document.createRange();
              newRange.selectNodeContents(wrapper);
              sel.addRange(newRange);
              notifyChange();
            },
            // The WYSIWYG-mode half of "Insert Internal Link": wraps the current selection in
            // <a href="href">, or — since there's nothing to wrap when nothing is selected —
            // inserts a brand new <a href="href">fallbackText</a> at the caret instead, mirroring
            // insertHtml's own "no real selection" fallback position (end of #content, or the
            // caret if one exists there).
            insertOrWrapLink: function (href, fallbackText) {
              var sel = window.getSelection();
              var hasSelection = sel.rangeCount && !sel.getRangeAt(0).collapsed && content.contains(sel.getRangeAt(0).commonAncestorContainer);
              if (hasSelection) {
                var range = sel.getRangeAt(0);
                var wrapper = document.createElement('a');
                wrapper.setAttribute('href', href);
                try {
                  range.surroundContents(wrapper);
                } catch (e) {
                  var extracted = range.extractContents();
                  wrapper.appendChild(extracted);
                  range.insertNode(wrapper);
                }
                sel.removeAllRanges();
              } else {
                content.focus();
                var insertRange;
                if (sel.rangeCount && content.contains(sel.anchorNode)) {
                  insertRange = sel.getRangeAt(0);
                } else {
                  insertRange = document.createRange();
                  insertRange.selectNodeContents(content);
                  insertRange.collapse(false);
                }
                var link = document.createElement('a');
                link.setAttribute('href', href);
                link.textContent = fallbackText;
                insertRange.deleteContents();
                insertRange.insertNode(link);
              }
              notifyChange();
            },
            // The WYSIWYG-mode half of "Mark as Index Entry…"'s plain (non-"mark all")
            // path — wraps the current selection in <span class="index-entry"
            // data-index-term="term" id="id">, the same shape wrapSelectionWithId uses but
            // with both a class and a data attribute set alongside the id.
            wrapSelectionAsIndexEntry: function (term, id) {
              var sel = window.getSelection();
              if (!sel.rangeCount) return;
              var range = sel.getRangeAt(0);
              if (range.collapsed || !content.contains(range.commonAncestorContainer)) return;
              var wrapper = document.createElement('span');
              wrapper.className = 'index-entry';
              wrapper.setAttribute('data-index-term', term);
              wrapper.id = id;
              try {
                range.surroundContents(wrapper);
              } catch (e) {
                var extracted = range.extractContents();
                wrapper.appendChild(extracted);
                range.insertNode(wrapper);
              }
              sel.removeAllRanges();
              var newRange = document.createRange();
              newRange.selectNodeContents(wrapper);
              sel.addRange(newRange);
              notifyChange();
            },
            // The WYSIWYG-mode half of "Mark as Index Entry…"'s "automatically mark all
            // occurrences of this text" checkbox — walks every text node under #content
            // (skipping text already inside an existing index-entry span, so re-running this
            // after some occurrences are already marked doesn't double-wrap them) and wraps
            // every case-insensitive match of term in its own new index-entry marker. See
            // IndexEntryMarker for the raw-mode, string-based equivalent of this same operation.
            markAllOccurrences: function (term) {
              if (!term) return;
              var lowerTerm = term.toLowerCase();
              var walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT, {
                acceptNode: function (node) {
                  var p = node.parentNode;
                  while (p && p !== content) {
                    if (p.classList && p.classList.contains('index-entry')) return NodeFilter.FILTER_REJECT;
                    p = p.parentNode;
                  }
                  return NodeFilter.FILTER_ACCEPT;
                }
              });
              var textNodes = [];
              var n;
              while ((n = walker.nextNode())) textNodes.push(n);

              var counter = 0;
              textNodes.forEach(function (node) {
                var text = node.data;
                var lowerText = text.toLowerCase();
                var matchStarts = [];
                var idx = 0;
                while ((idx = lowerText.indexOf(lowerTerm, idx)) !== -1) {
                  matchStarts.push(idx);
                  idx += term.length;
                }
                if (matchStarts.length === 0) return;

                var frag = document.createDocumentFragment();
                var lastEnd = 0;
                matchStarts.forEach(function (start) {
                  var end = start + term.length;
                  if (start > lastEnd) frag.appendChild(document.createTextNode(text.slice(lastEnd, start)));
                  var span = document.createElement('span');
                  span.className = 'index-entry';
                  span.setAttribute('data-index-term', term);
                  span.id = 'idx:' + Date.now().toString(36) + '-' + (counter++);
                  span.textContent = text.slice(start, end);
                  frag.appendChild(span);
                  lastEnd = end;
                });
                if (lastEnd < text.length) frag.appendChild(document.createTextNode(text.slice(lastEnd)));

                node.parentNode.replaceChild(frag, node);
              });

              notifyChange();
            },
            // The WYSIWYG-mode half of Delete: removes the current (non-collapsed) selection
            // outright, or — since there's nothing to "delete" out of a mere caret position —
            // walks up from the caret to the nearest block-level ancestor (figure/table/list
            // item/paragraph/heading/blockquote/pre, stopping at #content itself) and removes
            // that whole element instead, mirroring right-click "Edit Image…"'s figure-lookup
            // shape but for deletion rather than editing.
            deleteSelection: function () {
              var sel = window.getSelection();
              if (!sel.rangeCount || !content.contains(sel.anchorNode)) return;
              var range = sel.getRangeAt(0);
              if (!range.collapsed) {
                range.deleteContents();
                notifyChange();
                return;
              }
              var blockTags = { FIGURE: 1, TABLE: 1, LI: 1, P: 1, BLOCKQUOTE: 1, PRE: 1,
                H1: 1, H2: 1, H3: 1, H4: 1, H5: 1, H6: 1 };
              var node = range.startContainer;
              while (node && node !== content) {
                if (node.nodeType === 1 && blockTags[node.tagName]) {
                  node.remove();
                  notifyChange();
                  return;
                }
                node = node.parentNode;
              }
            },
            // Returns the current selection's HTML (empty string if nothing/collapsed) — the
            // WYSIWYG-mode half of Copy/Cut, read by the host via InvokeScript's own return
            // value rather than a posted bridge message, since this needs to be awaited inline
            // rather than reacted to asynchronously.
            getSelectionHtml: function () {
              var sel = window.getSelection();
              if (!sel.rangeCount) return '';
              var range = sel.getRangeAt(0);
              if (range.collapsed || !content.contains(range.commonAncestorContainer)) return '';
              var wrapper = document.createElement('div');
              wrapper.appendChild(range.cloneContents());
              return wrapper.innerHTML;
            },
            // Right-click "Edit Image…"'s WYSIWYG-mode half: updates an existing <figure> (found
            // by the id the contextmenu listener above captured) in place instead of inserting a
            // new one — its own <img>'s width/height, the <figure>'s own style (see
            // ImagePlacement.ToFigureStyle — same shape this writes), and its <figcaption>'s
            // text. A no-op if the figure isn't found (e.g. deleted between right-click and
            // confirming the dialog).
            updateFigure: function (id, width, height, figureStyle, captionText) {
              var figure = document.getElementById(id);
              if (!figure || !content.contains(figure)) return;
              figure.setAttribute('style', figureStyle);
              var img = figure.querySelector('img');
              if (img) {
                img.setAttribute('width', width);
                img.setAttribute('height', height);
              }
              var figcaption = figure.querySelector('figcaption');
              if (figcaption) figcaption.textContent = captionText;
              notifyChange();
            },
            scrollToFraction: function (fraction) {
              var max = document.documentElement.scrollHeight - window.innerHeight;
              window.scrollTo(0, Math.max(0, max) * fraction);
            },
            // Appends a footnote's <li> to the page's existing ".footnotes ol", or creates that
            // block if this is the page's first footnote — the WYSIWYG-mode half of Insert
            // Footnote (see MainWindow.InsertOrAppendFootnoteDefinition for the raw-mode,
            // string-based equivalent). noteHtml is already HTML-encoded by the caller.
            appendFootnoteDefinition: function (number, noteHtml) {
              var li = document.createElement('li');
              li.id = 'fn:' + number;
              li.innerHTML = '<p>' + noteHtml + ' <a href="#fnref:' + number + '" class="footnote-back-ref">↩</a></p>';
              var ol = content.querySelector('.footnotes ol');
              if (ol) {
                ol.appendChild(li);
              } else {
                var div = document.createElement('div');
                div.className = 'footnotes';
                div.appendChild(document.createElement('hr'));
                var newOl = document.createElement('ol');
                newOl.appendChild(li);
                div.appendChild(newOl);
                content.appendChild(div);
              }
              notifyChange();
            }
          };

          // Told the host the bridge is actually ready to receive script calls — the WebView's
          // own "navigation completed" event fires once the document itself has loaded, which
          // isn't the same moment window.ebookEditor becomes callable (this script runs as part
          // of that same document, but nothing guarantees the two host-visible signals land in
          // the order the host assumes). Toolbar commands gate on this instead.
          invokeCSharpAction(JSON.stringify({ event: 'ready' }));
        })();
        """;

    /// <summary>
    /// headingHtml, if given, renders as a sibling BEFORE the #content element rather than
    /// inside it — a chapter's synthesized "&lt;h1&gt;Chapter N: Title&lt;/h1&gt;" (see
    /// ChapterHeadingHtml) needs to be visible without becoming part of what the WYSIWYG bridge
    /// reads back as the editable body on every change (chapter files store the title/subtitle
    /// only in front matter, never in the body itself — folding the heading into #content would
    /// permanently duplicate it into the saved file the next time WYSIWYG edits synced back).
    /// </summary>
    /// <summary>
    /// Writes a wrapped page's HTML to a real file on disk and returns its file:// URI, so a
    /// WebView can Navigate(uri) to it directly instead of NavigateToString(html, baseUri).
    /// This is the actual fix for relative images not loading — NavigateToString's underlying
    /// platform call (loadHTMLString(_:baseURL:) on macOS) hands the HTML content to a heavily
    /// sandboxed WebKit content process that, per Apple's own documentation, needs an explicit
    /// allowingReadAccessTo grant to read anything from disk at all; this app's cross-platform
    /// WebView wrapper doesn't expose that, so file reads were silently denied regardless of
    /// how correct the base URI/relative path math was (confirmed directly: an earlier fix's
    /// own BuildFileBaseUri helper computed the mathematically right absolute path, and the
    /// image still failed to load — that helper is gone now, superseded by this one).
    /// A genuine Navigate(fileUri) call doesn't have that problem — it's the same as
    /// double-clicking the file in Finder, with no separate read-access grant needed.
    /// The file lives in a new ".eb-preview" directory, one level below the project root —
    /// the exact same depth every real front/back-matter or chapter file already lives at, so
    /// the stored "../images/foo.jpg" convention resolves correctly completely unchanged, no
    /// path rewriting needed anywhere. Never picked up as an orphan chapter (OrphanChapterScanner
    /// only ever looks in chapters/), and overwritten on every call rather than uniquely named,
    /// since only ever one Preview/WYSIWYG navigation is "current" at a time per window.
    /// </summary>
    public static Uri WritePreviewFile(string projectDirectory, string html)
    {
        var previewDir = Path.Combine(projectDirectory, ".eb-preview");
        Directory.CreateDirectory(previewDir);
        var path = Path.Combine(previewDir, "preview.html");
        File.WriteAllText(path, html);
        return new Uri(path);
    }

    public static string Wrap(string css, string bodyHtml, bool editable, string? headingHtml = null) =>
        $"""
        <!doctype html>
        <html>
        <head>
        <meta charset="utf-8">
        <style>{css}</style>
        </head>
        <body>
        {(headingHtml is { Length: > 0 } ? headingHtml + "\n" : "")}<div id="{ContentElementId}"{(editable ? " contenteditable=\"true\" spellcheck=\"false\"" : "")}>
        {bodyHtml}
        </div>
        <script>
        {BridgeScript}
        </script>
        </body>
        </html>
        """;
}
