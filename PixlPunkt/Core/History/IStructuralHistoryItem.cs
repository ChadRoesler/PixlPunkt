namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Marks a history item whose undo or redo can change the canvas dimensions or the shape of the
    /// layer tree, so the view has to re-sync its zoom, selection region and composite afterwards.
    /// </summary>
    /// <remarks>
    /// The canvas host also recognises a fixed list of older item types. This interface exists so a
    /// new item declares the fact itself: an item defined here cannot see that list, and forgetting
    /// to extend it leaves undo looking as though it only half worked.
    /// </remarks>
    public interface IStructuralHistoryItem
    {
    }
}
