namespace ThreeMusketeers.Core
{
    /// <summary>
    /// A single move: a piece slides from one cell to an orthogonally adjacent one.
    /// Under the classic ruleset, an Offense move's destination always holds a
    /// Defense piece that gets captured, and a Defense move's destination is
    /// always empty (see ClassicRuleSet) -- a future mechanic pack's IRuleSet
    /// could relax this.
    /// </summary>
    public readonly struct Move
    {
        public readonly Coord From;
        public readonly Coord To;

        public Move(Coord from, Coord to)
        {
            From = from;
            To = to;
        }

        public override string ToString() => $"{From} -> {To}";
    }
}
