using eBookEditor.Core.Models;
using eBookEditor.DocxImport.Services;

namespace eBookEditor.Tests.DocxImport;

public class SpecialPageClassifierTests
{
    [Theory]
    [InlineData("Part One")]
    [InlineData("Part 1")]
    [InlineData("Part I")]
    [InlineData("PART ONE")]
    [InlineData("part one")]
    [InlineData("  Part Two  ")]
    public void Classify_PartHeading_IsAnUnnumberedChapterDivider(string title)
    {
        var (type, numberMode) = SpecialPageClassifier.Classify(title);

        Assert.Equal(SpineItemType.Chapter, type);
        Assert.Equal(ChapterNumberMode.None, numberMode);
    }

    [Theory]
    [InlineData("Acknowledgements")]
    [InlineData("Acknowledgments")]
    [InlineData("Preface")]
    [InlineData("Dedication")]
    [InlineData("Foreword")]
    [InlineData("preface")]
    public void Classify_FrontMatterHeading_IsFrontMatter(string title)
    {
        var (type, numberMode) = SpecialPageClassifier.Classify(title);

        Assert.Equal(SpineItemType.FrontMatter, type);
        Assert.Equal(ChapterNumberMode.Auto, numberMode);
    }

    [Theory]
    [InlineData("Afterword")]
    [InlineData("Postscript")]
    [InlineData("Epilogue")]
    [InlineData("Index")]
    [InlineData("Also by the Author")]
    [InlineData("About the Author")]
    public void Classify_BackMatterHeading_IsBackMatter(string title)
    {
        var (type, numberMode) = SpecialPageClassifier.Classify(title);

        Assert.Equal(SpineItemType.BackMatter, type);
    }

    [Theory]
    [InlineData("Introduction")]
    [InlineData("Prologue")]
    [InlineData("Chapter One")]
    [InlineData("Part Man, Part Machine")]
    public void Classify_AmbiguousOrOrdinaryHeading_StaysARegularNumberedChapter(string title)
    {
        var (type, numberMode) = SpecialPageClassifier.Classify(title);

        Assert.Equal(SpineItemType.Chapter, type);
        Assert.Equal(ChapterNumberMode.Auto, numberMode);
    }
}
