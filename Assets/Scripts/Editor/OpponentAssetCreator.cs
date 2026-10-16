#if UNITY_EDITOR
using ThreeMusketeers.AI;
using UnityEditor;
using UnityEngine;

namespace ThreeMusketeers.EditorTools
{
    /// <summary>
    /// Menu command: 5x5 > Create Default Opponents.
    /// Makes the four starter opponents (Easy, Medium, Hard, Expert) as assets in
    /// Assets/Opponents, skipping any that already exist. Then drag them into
    /// "Available Opponents" on MainMenuController.
    /// </summary>
    public static class OpponentAssetCreator
    {
        private const string Folder = "Assets/Opponents";

        [MenuItem("5x5/Create Default Opponents")]
        public static void CreateDefaults()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Opponents");

            var tiers = new[] { SeatType.Easy, SeatType.Medium, SeatType.Hard, SeatType.Expert };
            int created = 0;
            foreach (var tier in tiers)
            {
                string path = Folder + "/Default" + tier + ".asset";
                if (AssetDatabase.LoadAssetAtPath<OpponentDefinition>(path) != null) continue;
                AssetDatabase.CreateAsset(OpponentDefinition.CreateDefault(tier), path);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Create Default Opponents: " + created + " created in " + Folder + " (existing ones were left alone).");
        }
    }
}
#endif
