using eBookEditor.Core.Models;

namespace eBookEditor.Epub.Models;

internal record EpubContentDoc(SpineItem SpineItem, string FileName, string EpubType, string Title);
