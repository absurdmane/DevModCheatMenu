using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>Reloads, the FPS window switch.</summary>
    internal sealed class ToolsTab : Tab
    {
        public override string Title => "Tools";

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
            if (GUILayout.Button($"Reload scripts [{Plugin.ReloadKey.Value}]", GUILayout.ExpandWidth(false)))
                ScriptLoader.ReloadAll();
            GUILayout.Label("mods in BepInEx\\scripts (build them with -p:Deploy=scripts)");
            GUILayout.EndHorizontal();
            foreach ((string name, string file, System.DateTime built) in ScriptLoader.Plugins)
                GUILayout.Label($"   {name}   <i>{file}, built {built:HH:mm:ss}</i>");

            Gui.Heading("FPS");
            // A short label on the check button (it doesn't wrap); the details go in a wrapping label under it.
            FpsWindow.Visible = Gui.Toggle("FPS window", FpsWindow.Visible);
            GUILayout.Label("Stays open with the menu closed.");
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
