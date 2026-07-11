using System.Text.RegularExpressions;
using eBookEditor.Core.Models;

namespace eBookEditor.DocxImport.Services;

/// <summary>Recognizes a heading's plain text as one of a small set of non-chapter page kinds
/// — an unnumbered mid-book divider ("Part One"), or a named optional front/back-matter page
/// (Acknowledgements, Preface… / Afterword, Index…) — instead of a regular numbered chapter.
/// Deliberately leaves ambiguous headings (Introduction, Prologue, Epilogue) unclassified:
/// these are commonly numbered as real chapters in published books, and guessing wrong
/// silently is worse than leaving the safe default (a regular chapter).</summary>
public static partial class SpecialPageClassifier
{
    // "Part One", "Part 1", "Part I" — case-insensitive, anchored so it doesn't false-positive
    // on prose that merely starts with the word "Part".
    [GeneratedRegex(@"^Part\s+([0-9]+|[A-Za-z]+|[IVXLCDM]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PartDividerRegex();

    private static readonly HashSet<string> FrontMatterTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acknowledgements", "Acknowledgments", "Preface", "Dedication", "Foreword"
    };

    private static readonly HashSet<string> BackMatterTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Afterword", "Postscript", "Epilogue", "Index", "Also by the Author", "Also By the Author",
        "Also by This Author", "About the Author"
    };

    public static (SpineItemType Type, ChapterNumberMode NumberMode) Classify(string title)
    {
        var trimmed = title.Trim();

        if (PartDividerRegex().IsMatch(trimmed))
            return (SpineItemType.Chapter, ChapterNumberMode.None);

        if (FrontMatterTitles.Contains(trimmed))
            return (SpineItemType.FrontMatter, ChapterNumberMode.Auto);

        if (BackMatterTitles.Contains(trimmed))
            return (SpineItemType.BackMatter, ChapterNumberMode.Auto);

        return (SpineItemType.Chapter, ChapterNumberMode.Auto);
    }
}
