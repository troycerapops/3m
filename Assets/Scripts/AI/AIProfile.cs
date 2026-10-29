using System;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// What the AI assumes about how the OTHER side chooses its replies when
    /// it simulates future turns (attribute 4).
    /// </summary>
    public enum OpponentModel
    {
        /// <summary>Assumes the opponent always plays its best reply (true minimax).</summary>
        Optimal = 0,

        /// <summary>Assumes the opponent grabs whatever looks best right now, with no lookahead.</summary>
        Greedy = 1,

        /// <summary>Assumes the opponent moves at random (averages over its replies). Misses real threats.</summary>
        Random = 2
    }

    /// <summary>Who controls a seat. Human = taps on the board; the rest are AI presets (see AIProfile.FromSeat).</summary>
    public enum SeatType
    {
        Human = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4
    }

    /// <summary>
    /// The 4 adjustable attributes that define one AI player. Plain
    /// serializable class (no Unity dependency) so it shows up in the
    /// Inspector as a field and can also be used by the console test harness.
    ///
    /// STRENGTH (1)
    ///   SearchDepth
    /// HUMAN-FEEL (2-4)  - deliberate imperfection
    ///   TopN, BlunderChance (+ BlunderSeverityCap), Opponent
    ///
    /// (Earlier versions also had four "personality" evaluation weights and a
    /// node/time budget. Testing showed the weights did not measurably change
    /// play on this ruleset, and think time just tracks depth here, so both
    /// were removed; the evaluation now uses fixed values.)
    /// </summary>
    [Serializable]
    public class AIProfile
    {
        public string Name = "Unnamed";

        // ---- 1. Search depth --------------------------------------------
        /// <summary>How many half-moves (plies) ahead the AI simulates. 1 = only its own move.</summary>
        public int SearchDepth = 3;

        /// <summary>
        /// Think deeper when it is cheap (late game, few pieces left). After the normal
        /// search the AI estimates what one more ply would cost; if that is under
        /// BonusDepthNodeLimit it searches one ply deeper, and repeats, up to
        /// BonusDepthMax extra plies. 0 = off. Deterministic: it counts positions
        /// searched, not seconds, so it behaves the same on every device.
        /// </summary>
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Tooltip("LATE-GAME THINKING: the most extra plies (half-moves) the AI may add when a turn is cheap to search. 0 = off. Typical: 2.")]
#endif
        public int BonusDepthMax = 0;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Tooltip("LATE-GAME THRESHOLD: a bonus ply is only added if its estimated cost is under this many positions. Higher = thinks deeper for longer into the game (and takes longer per turn). Typical: 600000. 0 = off.")]
#endif
        public int BonusDepthNodeLimit = 0;

        // ---- 2. Move randomness -----------------------------------------
        /// <summary>Pick uniformly among the top N scored moves. 1 = always the best move.</summary>
        public int TopN = 1;

        // ---- 3. Blunder rate --------------------------------------------
        /// <summary>Chance (0..1) per turn of playing a worse-but-not-losing move instead of the best.</summary>
        public float BlunderChance = 0f;
        /// <summary>A blunder may be at most this many score points worse than the best move (see MinimaxAgent score units).</summary>
        public float BlunderSeverityCap = 20f;

        // ---- 4. Opponent model ------------------------------------------
        public OpponentModel Opponent = OpponentModel.Optimal;

        public AIProfile Copy() => (AIProfile)MemberwiseClone();

        /// <summary>Preset for a seat choice; null means a human controls that seat.</summary>
        public static AIProfile FromSeat(SeatType seat)
        {
            switch (seat)
            {
                case SeatType.Easy: return Easy();
                case SeatType.Medium: return Medium();
                case SeatType.Hard: return Hard();
                case SeatType.Expert: return Expert();
                default: return null;
            }
        }

        // ---- Starting-point presets (tune these after playtesting) -------
        public static AIProfile Easy() => new AIProfile
        {
            Name = "Easy", SearchDepth = 1, TopN = 4, BlunderChance = 0.25f, BlunderSeverityCap = 30f,
            Opponent = OpponentModel.Random
        };

        public static AIProfile Medium() => new AIProfile
        {
            Name = "Medium", SearchDepth = 4, TopN = 1, BlunderChance = 0f,
            Opponent = OpponentModel.Optimal
        };

        public static AIProfile Hard() => new AIProfile
        {
            Name = "Hard", SearchDepth = 6, TopN = 1, BlunderChance = 0f,
            Opponent = OpponentModel.Optimal
        };

        /// <summary>
        /// Deepest preset: depth 8 and up to 2 bonus plies when a
        /// turn is cheap (late game). Depth 9 was tried and judged too slow on a desktop, so 8 it is.
        /// The GameManager logs every AI move's time so phone speed can be checked.
        /// </summary>
        public static AIProfile Expert() => new AIProfile
        {
            Name = "Expert", SearchDepth = 8, TopN = 1, BlunderChance = 0f,
            Opponent = OpponentModel.Optimal,
            BonusDepthMax = 2, BonusDepthNodeLimit = 600000
        };
    }
}
