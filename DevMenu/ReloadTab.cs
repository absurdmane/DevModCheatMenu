using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>Reloading the game (with or without saving) and hot reload of the mods and scripts in BepInEx\scripts.</summary>
    internal sealed class ReloadTab : Tab
    {
        public override string Title => "Reload";

        private const string QuickSave = "DevQuick";

        public override void Draw()
        {
            Gui.Heading("Save / load");
            GUILayout.Label($"Character: {SaveController.CharacterName}   save: {SaveController.SaveName}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload game", GUILayout.ExpandWidth(false)))
                Menu.Run("Reload game", () => Reload(save: true));
            GUILayout.Label($"saves as '{QuickSave}' and loads it: changes are kept");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Reload last save \"{SaveController.SaveName}\"", GUILayout.ExpandWidth(false)))
                Menu.Run("Reload last save", () => Reload(save: false));
            GUILayout.Label("without saving: changes since then are lost");
            GUILayout.EndHorizontal();

            Gui.Heading("Hot reload");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Plugin.ReloadKey.Value == KeyCode.None ? "Reload changed" : $"Reload changed [{Plugin.ReloadKey.Value}]", GUILayout.ExpandWidth(false)))
                ScriptLoader.Reload(all: false);
            if (GUILayout.Button("Reload all", GUILayout.ExpandWidth(false)))
                ScriptLoader.Reload(all: true);
            GUILayout.Label("mods and debug scripts in BepInEx\\scripts (build them with -p:Deploy=scripts)");
            GUILayout.EndHorizontal();
            foreach ((string name, string file, System.DateTime built) in ScriptLoader.Plugins)
                GUILayout.Label($"   {name}   <i>{file}, built {built:HH:mm:ss}</i>");
        }

        private static void Reload(bool save)
        {
            string character = SaveController.CharacterName;
            string name = SaveController.SaveName;
            if (string.IsNullOrEmpty(character))
            {
                Menu.Notify("No save loaded yet");
                return;
            }
            if (save)
            {
                SaveController.SaveGame(QuickSave);
                name = QuickSave;
            }
            Menu.Instance.Close();
            Menu.Instance.StartCoroutine(Load(character, name));
        }

        // Same steps as the game's load menu: fade out, then load.
        private static IEnumerator Load(string character, string save)
        {
            LoadingScreen screen = LoadingScreen.instance;
            if (screen != null && !screen.FadeActive)
            {
                screen.Fade(Fadein: true, soundsMuted: true, 0f, changeHint: true, SceneManager.GetActiveScene().name);
                if (GameController.instance != null)
                    GameController.instance.GameIsPaused = false;
                while (screen.Fading)
                    yield return new WaitForSecondsRealtime(0.001f);
                yield return null;
            }
            Plugin.Log.LogInfo($"Loading {character}/{save}");
            yield return SaveController.LoadGameWithMigration(character, save, fromMenu: true);
        }
    }
}
