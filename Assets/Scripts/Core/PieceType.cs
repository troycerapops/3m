namespace ThreeMusketeers.Core
{
    /// <summary>
    /// What occupies a board cell. None means the cell is empty.
    ///
    /// Deliberately theme-neutral names: this engine is reused across theme
    /// packs (prison break, and whatever comes after), so it never hardcodes
    /// "Musketeer" or "Convict". Offense = the minority side that moves only
    /// by capturing. Defense = the majority side that moves into empty cells
    /// and wins by aligning. A ThemeDefinition maps these to per-theme
    /// models/labels (e.g. Offense -> "Escaped Convict" in the prison theme).
    /// </summary>
    public enum PieceType
    {
        None = 0,
        Offense = 1,
        Defense = 2
    }
}
