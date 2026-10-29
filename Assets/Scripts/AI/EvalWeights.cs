using System;
using System.Text;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// The numbers the AI's static evaluation multiplies by. Everything is
    /// written from OFFENSE's point of view: positive = good for Offense.
    /// A Defense AI uses the same features and just flips the sign of the total.
    ///
    /// Each side has its own tuned set (see ForSide), so retuning one side
    /// never changes how the other plays. Every AI opponent and the hint
    /// engine use these tuned sets; the original numbers (Baseline) are kept
    /// only so tests can compare against the first version of the AI.
    /// </summary>
    [Serializable]
    public class EvalWeights
    {
        // ---- Original features (the v1 evaluation) ----
        public float Capture = 10f;        // per Defense piece captured so far
        public float Mobility = 2f;        // per Offense capture option
        public float AlignedPair = -15f;   // per pair of Offense pieces sharing a row/column
        public float AligningMove = -15f;  // per Offense move that would line all three up
        public float Exposed = 3f;         // per Defense piece touching an Offense piece

        // ---- Newer features (0 = off) ----
        public float SafeMoves = 0f;       // per Offense move that does NOT line all three up
        public float MovablePieces = 0f;   // per Offense piece that has at least one move
        public float Edge = 0f;            // per Offense piece on the board edge
        public float Corner = 0f;          // per Offense piece in a corner (on top of Edge)
        public float Spread = 0f;          // per unit of Manhattan distance summed over the 3 pairs

        /// <summary>
        /// At a search leaf with Offense to move: if EVERY legal Offense move
        /// lines all three pieces up, score it as a Defense win instead of guessing.
        /// </summary>
        public bool DetectForcedAlignment = false;

        public EvalWeights Copy() => (EvalWeights)MemberwiseClone();

        /// <summary>The original (first-version) evaluation. No longer used by any AI or hint; kept for test comparisons.</summary>
        public static EvalWeights Baseline() => new EvalWeights();

        /// <summary>
        /// The tuned Defense evaluation (found by self-play hill-climbing, see the
        /// results notes). Compared with the original it ignores raw capture-option
        /// count, cares less about Offense pieces merely sharing a line, cares more
        /// about moves that would complete a line, and likes Offense pieces that
        /// are stuck (few can move) or close together. In test games it roughly doubles Defense's win
        /// rate against the original Offense AI at equal search depth.
        /// </summary>
        public static EvalWeights TunedDefense() => new EvalWeights
        {
            Mobility = 0f,
            AlignedPair = -5f,
            AligningMove = -20f,
            MovablePieces = 3f,
            Spread = 1f
        };

        /// <summary>
        /// The tuned Offense evaluation (found by self-play against the tuned Defense). Used by the
        /// Offense AI at every difficulty and by the hint engine. In test games it adds about
        /// 12-14 win-rate points at depths 4-6 and little or nothing at depth 8.
        /// </summary>
        public static EvalWeights TunedOffense() => new EvalWeights
        {
            AlignedPair = -10f,
            AligningMove = -20f,
            Exposed = 5f,
            Edge = 2f
        };

        /// <summary>The weights an AI (or hint) uses for a side: always the tuned set for that side.</summary>
        public static EvalWeights ForSide(Player side)
        {
            return side == Player.Defense ? TunedDefense() : TunedOffense();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var f in typeof(EvalWeights).GetFields())
                sb.Append(f.Name).Append('=').Append(f.GetValue(this)).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }
}
