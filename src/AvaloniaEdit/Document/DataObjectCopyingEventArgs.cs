using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEditCore.Utils;

namespace AvaloniaEditCore.Document;

public class DataObjectCopyingEventArgs : RoutedEventArgs
{
    public bool CommandCancelled { get; private set; }
    public IDataObject DataObject { get; private set; }
    public bool IsDragDrop { get; private set; }

    public DataObjectCopyingEventArgs(IDataObject dataObject, bool isDragDrop) :
        base(DataObjectEx.DataObjectCopyingEvent)
    {
        DataObject = dataObject;
        IsDragDrop = isDragDrop;
    }

    public void CancelCommand() => CommandCancelled = true;
}