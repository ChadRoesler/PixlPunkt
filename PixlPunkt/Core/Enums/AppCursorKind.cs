namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the types of custom cursors used in the application.
    /// </summary>
    /// <remarks>
    /// This enum specifies the visual cursor types that can be displayed during different tool operations.
    /// Additional cursor types are commented out for future expansion (movement, rotation, panning, etc.).
    /// </remarks>
    public enum AppCursorKind
    {
        /// <summary>
        /// Standard arrow pointer cursor.
        /// </summary>
        Arrow = 0,

        /// <summary>
        /// Crosshair cursor for precise pixel selection and drawing.
        /// </summary>
        Crosshair,
        //Move,
        //NorthSouthArrow,
        //EastWestArrow,
        //NorthEastSouthWestArrow,
        //NorthWestSouthEastArrow,
        //SouthWestRotate,
        //NorthWestRotate,
        //SouthEastRotate,
        //NorthEastRotate,
        //PanHand,
        //MagnifyingGlass,
    }
}
