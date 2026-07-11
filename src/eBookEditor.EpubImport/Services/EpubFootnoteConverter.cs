using AngleSharp.Dom;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Best-effort conversion of a source EPUB chapter's footnote markup into this app's own
/// convention — the shape MainWindow's "Insert Footnote…" command itself authors:
/// <c>&lt;sup id="fnref:N"&gt;&lt;a href="#fn:N" class="footnote-ref"&gt;N&lt;/a&gt;&lt;/sup&gt;</c>
/// paired with a <c>&lt;div class="footnotes"&gt;&lt;hr&gt;&lt;ol&gt;&lt;li id="fn:N"&gt;...&lt;/li&gt;&lt;/ol&gt;&lt;/div&gt;</c>
/// at the end of the chapter, its &lt;li&gt; carrying a "footnote-back-ref" return link.
///
/// Detects two source shapes, in document order, treating either as a candidate reference: a
/// &lt;sup&gt; wrapping exactly one same-page fragment link (the common Word/Vellum-ish
/// convention this app's own DOCX importer also recognizes, under different id/class names),
/// and an EPUB3-native `epub:type~="noteref"` anchor (its &lt;sup&gt; ancestor, if any, is
/// still preferred as the element actually replaced, to preserve any surrounding wrapper).
/// A candidate only converts when its href resolves to an element with a matching id that
/// appears *later* in the body than the reference itself — a definition can't precede its own
/// reference in normal usage — and that target hasn't already been claimed by another
/// reference. Anything that looks like a footnote reference but doesn't resolve this way is
/// left untouched, with a real new footnote inserted immediately after it whose text tells the
/// user to review it manually, rather than silently leaving something broken.
///
/// Must run before whatever strips `epub:*` attributes from the body — this is the one pass
/// that still needs to see them.
/// </summary>
public static class EpubFootnoteConverter
{
    private const string TodoNoteText = "TODO: This footnote could not be automatically converted from the source EPUB — please review and fix it manually.";

    /// <summary>Mutates <paramref name="body"/> in place. Returns how many footnote-shaped
    /// references couldn't be confidently converted and got a TODO footnote inserted instead.</summary>
    public static int RewriteFootnotes(IElement body)
    {
        var document = body.Owner!;
        var nextNumber = SeedNextNumber(body);
        IElement? footnotesDiv = body.QuerySelector("div.footnotes");

        var idIndex = BuildIdIndex(body);
        var docOrder = BuildDocumentOrderIndex(body);
        var candidates = FindFootnoteReferenceCandidates(body);
        var consumedTargets = new HashSet<IElement>();
        var todoCount = 0;

        foreach (var (referenceContainer, targetId) in candidates)
        {
            var target = idIndex.GetValueOrDefault(targetId);
            var resolvable = target is not null
                && !consumedTargets.Contains(target)
                && docOrder.TryGetValue(target, out var targetPosition)
                && docOrder.TryGetValue(referenceContainer, out var referencePosition)
                && targetPosition > referencePosition;

            if (resolvable)
            {
                consumedTargets.Add(target!);
                ConvertFootnote(referenceContainer, target!, ref nextNumber, ref footnotesDiv, body, document);
            }
            else
            {
                InsertTodoFootnote(referenceContainer, ref nextNumber, ref footnotesDiv, body, document);
                todoCount++;
            }
        }

        return todoCount;
    }

    private static List<(IElement ReferenceContainer, string TargetId)> FindFootnoteReferenceCandidates(IElement body)
    {
        var results = new List<(IElement, string)>();
        var seen = new HashSet<IElement>();

        foreach (var sup in body.QuerySelectorAll("sup"))
        {
            var anchors = sup.Children.Where(c => c.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)).ToList();
            if (anchors.Count != 1)
                continue;

            if (TryGetFragmentTarget(anchors[0], out var targetId) && seen.Add(sup))
                results.Add((sup, targetId));
        }

        foreach (var anchor in body.QuerySelectorAll("a"))
        {
            var epubType = anchor.GetAttribute("epub:type");
            if (epubType is null || !epubType.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("noteref"))
                continue;
            if (!TryGetFragmentTarget(anchor, out var targetId))
                continue;

            var container = ClosestSup(anchor) ?? anchor;
            if (seen.Add(container))
                results.Add((container, targetId));
        }

        return results;
    }

    private static IElement? ClosestSup(IElement element)
    {
        for (var current = element.ParentElement; current is not null; current = current.ParentElement)
        {
            if (current.TagName.Equals("SUP", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        return null;
    }

    private static bool TryGetFragmentTarget(IElement anchor, out string targetId)
    {
        var href = anchor.GetAttribute("href");
        if (href is { Length: > 1 } && href[0] == '#')
        {
            targetId = href[1..];
            return true;
        }

        targetId = "";
        return false;
    }

    private static void ConvertFootnote(IElement referenceContainer, IElement target, ref int nextNumber, ref IElement? footnotesDiv, IElement body, IDocument document)
    {
        var number = nextNumber++;

        var newSup = BuildReferenceElement(document, number);
        referenceContainer.Parent!.ReplaceChild(newSup, referenceContainer);

        footnotesDiv ??= FindOrCreateFootnotesDiv(body, document);
        var p = document.CreateElement("p");
        p.InnerHtml = target.InnerHtml.Trim();
        AppendBackRef(document, p, number);

        var li = document.CreateElement("li");
        li.SetAttribute("id", $"fn:{number}");
        li.AppendChild(p);
        footnotesDiv.QuerySelector("ol")!.AppendChild(li);

        target.Remove();
    }

    private static void InsertTodoFootnote(IElement referenceContainer, ref int nextNumber, ref IElement? footnotesDiv, IElement body, IDocument document)
    {
        var number = nextNumber++;

        var newSup = BuildReferenceElement(document, number);
        referenceContainer.Parent!.InsertBefore(newSup, referenceContainer.NextSibling);

        footnotesDiv ??= FindOrCreateFootnotesDiv(body, document);
        var p = document.CreateElement("p");
        p.TextContent = TodoNoteText + " ";
        AppendBackRef(document, p, number);

        var li = document.CreateElement("li");
        li.SetAttribute("id", $"fn:{number}");
        li.AppendChild(p);
        footnotesDiv.QuerySelector("ol")!.AppendChild(li);
    }

    private static IElement BuildReferenceElement(IDocument document, int number)
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

    private static void AppendBackRef(IDocument document, IElement paragraph, int number)
    {
        var backRef = document.CreateElement("a");
        backRef.SetAttribute("href", $"#fnref:{number}");
        backRef.ClassList.Add("footnote-back-ref");
        backRef.TextContent = "↩";
        paragraph.AppendChild(backRef);
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

    private static int SeedNextNumber(IElement body)
    {
        var max = 0;
        foreach (var element in body.QuerySelectorAll("[id]"))
        {
            if (element.Id is { } id && id.StartsWith("fnref:", StringComparison.Ordinal) && int.TryParse(id["fnref:".Length..], out var n))
                max = Math.Max(max, n);
        }

        return max + 1;
    }

    private static Dictionary<string, IElement> BuildIdIndex(IElement body)
    {
        var map = new Dictionary<string, IElement>(StringComparer.Ordinal);
        foreach (var element in body.QuerySelectorAll("[id]"))
        {
            if (element.Id is { Length: > 0 } id)
                map.TryAdd(id, element);
        }

        return map;
    }

    private static Dictionary<IElement, int> BuildDocumentOrderIndex(IElement body)
    {
        var all = body.QuerySelectorAll("*");
        var map = new Dictionary<IElement, int> { [body] = -1 };
        for (var i = 0; i < all.Length; i++)
            map[all[i]] = i;

        return map;
    }
}
