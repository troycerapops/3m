using System;
using System.Text;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.AI
{
    /// <summary>Which static evaluation an AI uses. Original = the first version; Tuned = self-play-tuned numbers (Defense only; Offense AI always uses Original).</summary>
    public enum EvaluationSet
    {
        Original = 0,
        Tuned = 1
    }

    /// <summary>
    /// The numbers the AI's static evaluation multiplies by. Everything is
    /// written from OFFENSE's point of view: positive = good for Offense.
    /// A Defense AI uses the same features and just flips the sign of the total.
    ///
    /// Each side gets its own set (see ForSide), so tuning Defense play can
    /// never change how Offense plays.
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

        /// <summary>The original evaluation, unchanged. Offense always uses this.</summary>
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
        /// A tuned Offense evaluation (found against the tuned Defense). NOT used by any AI
        /// opponent: Offense AI deliberately keeps the original numbers. Kept here because it is
        /// the natural evaluation for a future HINT engine, which should suggest good Offense
        /// moves without making the opponent any harder.
        /// </summary>
        public static EvalWeights TunedOffense() => new EvalWeights
        {
            AlignedPair = -10f,
            AligningMove = -20f,
            Exposed = 5f,
            Edge = 2f
        };

        /// <summary>
        /// Weights an AI uses for a side. Tuned only changes DEFENSE play; Offense AI always uses
        /// the original numbers, whichever set is chosen.
        /// </summary>
        public static EvalWeights ForSide(Player side, EvaluationSet set)
        {
            return set == EvaluationSet.Tuned && side == Player.Defense ? TunedDefense() : Baseline();
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
