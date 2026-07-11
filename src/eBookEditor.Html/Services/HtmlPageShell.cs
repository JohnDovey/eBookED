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
            // Wraps the current selection in a new <tag class="className"> — how Apply Style
            // works in WYSIWYG mode. Falls back to extract-and-reinsert when the selection
            // boundary doesn't cleanly bracket a set of whole nodes (surroundContents throws in
            // that case, e.g. a selection spanning part of one paragraph and part of another).
            wrapSelection: function (tag, className) {
              var sel = window.getSelection();
              if (!sel.rangeCount) return;
              var range = sel.getRangeAt(0);
              if (range.collapsed || !content.contains(range.commonAncestorContainer)) return;
              var wrapper = document.createElement(tag);
              wrapper.className = className;
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
    /// <summary>The base URI a WebView navigation needs for a wrapped page's relative
    /// <c>&lt;img src="../images/foo.jpg"&gt;</c> references to actually resolve — "about:blank"
    /// (this app's previous default) has no filesystem context for a relative path to resolve
    /// against, so every relative image failed to load regardless of how correct its path was.
    /// Returns "about:blank" unchanged when there's genuinely no project directory to resolve
    /// against (e.g. wrapping generated, path-independent content).</summary>
    public static Uri BuildFileBaseUri(string? projectDirectory) =>
        projectDirectory is { Length: > 0 }
            ? new Uri(Path.GetFullPath(projectDirectory) + Path.DirectorySeparatorChar)
            : new Uri("about:blank");

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
