using UnityEngine;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// One computer opponent the player can pick: a name, a difficulty tier,
    /// one or two hand-written adjectives describing how it plays, and the AI
    /// lever values that actually make it play that way.
    ///
    /// Create one via Assets > Create > 5x5 > Opponent, then add it
    /// to "Available Opponents" on MainMenuController. Or run
    /// 5x5 > Create Default Opponents (menu bar) to make the four
    /// starter opponents (one per tier) in Assets/Opponents.
    ///
    /// If Available Opponents is left empty, the menu builds the four starter
    /// opponents in memory, so the game works before any asset exists.
    /// </summary>
    [CreateAssetMenu(fileName = "NewOpponent", menuName = "5x5/Opponent")]
    public class OpponentDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Saved as the player's last choice. Must be unique and should never change once released. Empty = the asset's file name is used.")]
        public string opponentId = "";
        [Tooltip("Shown in the picker and on the Play screen. Leave empty to show just \"Computer\".")]
        public string displayName = "";

        [Header("Difficulty (how the player finds it)")]
        [Tooltip("Easy, Medium, Hard or Expert. (Human is not valid here.)")]
        public SeatType tier = SeatType.Medium;

        [Header("Playing style (hand-written; shown next to the name)")]
        [Tooltip("One or two words, e.g. \"Reckless\", \"Patient\". Describe what the levers below really do.")]
        public string[] styleAdjectives = new string[0];

        [Header("How it plays (the four AI levers)")]
        public AIProfile profile = new AIProfile();

        /// <summary>Stable key for saving the player's choice.</summary>
        public string Id => string.IsNullOrEmpty(opponentId) ? name : opponentId;

        /// <summary>The name to show; "Computer" when none was given.</summary>
        public string DisplayNameOrDefault => string.IsNullOrWhiteSpace(displayName) ? "Computer" : displayName;

        /// <summary>The adjectives joined for display, e.g. "Reckless, Patient". Empty when there are none.</summary>
        public string StyleText
        {
            get
            {
                if (styleAdjectives == null || styleAdjectives.Length == 0) return "";
                var parts = new System.Collections.Generic.List<string>();
                foreach (var a in styleAdjectives)
                    if (!string.IsNullOrWhiteSpace(a)) parts.Add(a.Trim());
                return string.Join(", ", parts);
            }
        }

        /// <summary>A copy of the lever values, safe to hand to an agent (named after this opponent).</summary>
        public AIProfile BuildProfile()
        {
            var copy = profile != null ? profile.Copy() : new AIProfile();
            copy.Name = DisplayNameOrDefault;
            return copy;
        }

        /// <summary>
        /// A starter opponent for a tier, using today's presets. The adjectives are
        /// PLACEHOLDERS describing the preset levers - rewrite them. Names are left
        /// empty on purpose (the UI then says "Computer").
        /// </summary>
        public static OpponentDefinition CreateDefault(SeatType tier)
        {
            var o = CreateInstance<OpponentDefinition>();
            o.name = "Default" + tier;
            o.opponentId = "default-" + tier.ToString().ToLowerInvariant();
            o.tier = tier;
            o.profile = AIProfile.FromSeat(tier) ?? new AIProfile();
            switch (tier)
            {
                case SeatType.Easy: o.styleAdjectives = new[] { "Shortsighted", "Careless" }; break;
                case SeatType.Medium: o.styleAdjectives = new[] { "Impulsive", "Inconsistent" }; break;
                case SeatType.Hard: o.styleAdjectives = new[] { "Calculating", "Steady" }; break;
                case SeatType.Expert: o.styleAdjectives = new[] { "Relentless", "Far-sighted" }; break;
            }
            return o;
        }
    }
}
