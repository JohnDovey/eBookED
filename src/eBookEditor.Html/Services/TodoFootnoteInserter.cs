using AngleSharp.Dom;

namespace eBookEditor.Html.Services;

/// <summary>
/// Inserts a real new footnote (this app's own convention — see MainWindow.OnInsertFootnoteClick:
/// <c>&lt;sup id="fnref:N"&gt;&lt;a href="#fn:N" class="footnote-ref"&gt;N&lt;/a&gt;&lt;/sup&gt;</c>
/// paired with a <c>&lt;div class="footnotes"&gt;</c> entry) immediately after some element, with
/// a "TODO: ...please review...manually" note — the shared mechanic behind every import path's
/// "couldn't confidently convert this automatically" fallback. Originally built for
/// EpubFootnoteConverter's own unresolvable footnote references; reused as-is by DOCX/HTML
/// import for an internal link that can't be confidently converted, so unconvertible things are
/// flagged for a human to fix rather than silently dropped or left broken.
///
/// One instance handles every insertion for a single chapter body — the shared "footnotes" div
/// is found or created once and cached, not recreated (and re-found via a fresh query) on every
/// call.
/// </summary>
public class TodoFootnoteInserter
{
    private readonly IElement _body;
    private readonly IDocument _document;
    private IElement? _footnotesDiv;

    public TodoFootnoteInserter(IElement body)
    {
        _body = body;
        _document = body.Owner!;
        _footnotesDiv = body.QuerySelector("div.footnotes");
    }

    /// <summary>Inserts a new numbered footnote reference immediately after
    /// <paramref name="afterElement"/> and adds a note reading <paramref name="message"/> to
    /// this body's Notes list — the caller owns/increments <paramref name="number"/> since it
    /// may be interleaving these with real (non-TODO) footnote numbers of its own.</summary>
    public void InsertAfter(IElement afterElement, int number, string message)
    {
        var reference = BuildReferenceElement(_document, number);
        afterElement.Parent!.InsertBefore(reference, afterElement.NextSibling);

        var p = _document.CreateElement("p");
        p.TextContent = message + " ";
        AppendBackRef(_document, p, number);

        var li = _document.CreateElement("li");
        li.SetAttribute("id", $"fn:{number}");
        li.AppendChild(p);
        GetOrCreateFootnotesDiv().QuerySelector("ol")!.AppendChild(li);
    }

    /// <summary>Exposed so a caller building a real (non-TODO) footnote definition of its own
    /// can append into the same "footnotes" div this instance already found/created, rather
    /// than each independently searching for or creating a second one.</summary>
    public IElement GetOrCreateFootnotesDiv() => _footnotesDiv ??= FindOrCreateFootnotesDiv(_body, _document);

    public static IElement BuildReferenceElement(IDocument document, int number)
    {
        var sup = document.CreateElement("sup");
        sup.SetAttribute("id", $"fnref:{number}");
        var anchor = document.CreateElement("a");
        anchor.SetAttribute("href", $"#fn:{number}");
        anchor.ClassList.Add("footnote-ref");
        anchor.TextContent = number.ToString();
        sup.AppendChild(anchor);
        return sup;
    }

    public static void AppendBackRef(IDocument document, IElement paragraph, int number)
    {
        var backRef = document.CreateElement("a");
        backRef.SetAttribute("href", $"#fnref:{number}");
        backRef.ClassList.Add("footnote-back-ref");
        backRef.TextContent = "↩";
        paragraph.AppendChild(backRef);
    }

    /// <summary>The highest existing "fnref:N" id in <paramref name="body"/>, plus one — so a
    /// caller inserting its first new footnote continues this chapter's existing numbering
    /// rather than starting over at 1 and colliding with real references already there.</summary>
    public static int SeedNextNumber(IElement body)
    {
        var max = 0;
        foreach (var element in body.QuerySelectorAll("[id]"))
        {
            if (element.Id is { } id && id.StartsWith("fnref:", StringComparison.Ordinal) && int.TryParse(id["fnref:".Length..], out var n))
                max = Math.Max(max, n);
        }

        return max + 1;
    }

    private static IElement FindOrCreateFootnotesDiv(IElement body, IDocument document)
    {
        var existing = body.QuerySelector("div.footnotes");
        if (existing is not null)
            return existing;

        var div = document.CreateElement("div");
        div.ClassList.Add("footnotes");
        div.AppendChild(document.CreateElement("hr"));
        div.AppendChild(document.CreateElement("ol"));
        body.AppendChild(div);
        return div;
    }
}
