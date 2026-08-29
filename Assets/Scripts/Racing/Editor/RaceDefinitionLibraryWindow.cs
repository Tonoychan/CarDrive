using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Racing.EditorTools
{
    public class RaceDefinitionLibraryWindow : EditorWindow
    {
        const string RaceFolder = "Assets/Racing/Races";
        const string PresetFolder = "Assets/Racing/OpponentPresets";

        Vector2 raceScroll;
        Vector2 presetScroll;

        [MenuItem("Racing/Race Definitions")]
        public static void Open()
        {
            GetWindow<RaceDefinitionLibraryWindow>("Race Definitions");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Race Definitions", EditorStyles.boldLabel);
            if (GUILayout.Button("New Race Definition"))
                CreateAsset<RaceDefinition>(RaceFolder, "NewRaceDefinition");

            raceScroll = EditorGUILayout.BeginScrollView(raceScroll, GUILayout.Height(200));
            foreach (var def in FindAssets<RaceDefinition>())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(def, typeof(RaceDefinition), false);
                EditorGUILayout.LabelField($"opp:{def.opponentCount} presets:{(def.opponentPresets?.Length ?? 0)}", GUILayout.Width(140));
                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                    EditorGUIUtility.PingObject(def);
                if (GUILayout.Button("Delete", GUILayout.Width(55)))
                    DeleteAssetWithConfirm(def);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Opponent Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("New Opponent Preset"))
                CreateAsset<RaceOpponentPreset>(PresetFolder, "NewOpponentPreset");

            presetScroll = EditorGUILayout.BeginScrollView(presetScroll, GUILayout.Height(200));
            foreach (var preset in FindAssets<RaceOpponentPreset>())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(preset, typeof(RaceOpponentPreset), false);
                EditorGUILayout.LabelField(preset.displayName, GUILayout.Width(140));
                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                    EditorGUIUtility.PingObject(preset);
                if (GUILayout.Button("Delete", GUILayout.Width(55)))
                    DeleteAssetWithConfirm(preset);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        static T[] FindAssets<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToArray();
        }

        static void DeleteAssetWithConfirm(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Asset",
                $"Delete '{asset.name}'?\n\n{path}\n\nThis cannot be undone.",
                "Delete",
                "Cancel");
            if (!confirmed) return;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
        }

        static void CreateAsset<T>(string folder, string defaultName) where T : ScriptableObject
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{defaultName}.asset");
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
