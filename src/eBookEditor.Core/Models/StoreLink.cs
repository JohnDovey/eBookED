namespace eBookEditor.Core.Models;

public enum StoreName
{
    KindleStore,
    AppleBooks,
    KoboStore,
    GooglePlayBooks,
    BarnesAndNoble,
    Smashwords,
    Other
}

public record StoreLink(StoreName Store, string Url, string? DisplayLabel = null);
