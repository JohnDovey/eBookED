using Avalonia.Interactivity;
using AvaloniaEditCore.Document;

namespace AvaloniaEditCore.Utils;

public static class DataObjectEx
{
    /// <summary>
    /// Shim for WPF's DataObject.CopyingEvent which is not available in Avalonia.
    /// </summary>
    public static readonly RoutedEvent<DataObjectCopyingEventArgs> DataObjectCopyingEvent =
        RoutedEvent.Register<DataObjectCopyingEventArgs>(
            nameof(DataObjectCopyingEvent),
            RoutingStrategies.Bubble,
            typeof(DataObjectEx));
}
