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

    public static string Wrap(string css, string bodyHtml, bool editable) =>
        $"""
        <!doctype html>
        <html>
        <head>
        <meta charset="utf-8">
        <style>{css}</style>
        </head>
        <body id="{ContentElementId}"{(editable ? " contenteditable=\"true\" spellcheck=\"false\"" : "")}>
        {bodyHtml}
        <script>
        {BridgeScript}
        </script>
        </body>
        </html>
        """;
}
