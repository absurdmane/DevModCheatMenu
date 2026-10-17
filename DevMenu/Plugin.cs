using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using UnityEngine;

namespace DevMenu
{
    /// <summary>Dev, Mod &amp; Cheat Menu: cheats, every mod's settings, dev tools. Called DevMenu in the sources.</summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "obenseuer.absurdmane.devmenu";
        public const string Name = "Dev, Mod & Cheat Menu";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyCode> MenuKey;
        internal static ConfigEntry<KeyCode> InspectKey;
        internal static ConfigEntry<KeyCode> ReloadKey;

        private void Awake()
        {
            Log = Logger;
            MenuKey = Config.Bind("Keys", "Menu", KeyCode.F9, "Opens and closes the cheat menu.");
            InspectKey = Config.Bind("Keys", "Inspect", KeyCode.None,
                "None by default. Inspects what's under the crosshair: shows it on screen and writes it (only the latest) to BepInEx\\DevMenu\\inspect-latest.txt.");
            ReloadKey = Config.Bind("Keys", "ReloadScripts", KeyCode.F10,
                "Reloads the mods in BepInEx\\scripts (hot reload; also a button on the Tools tab).");

            Theme.Bind(Config);
            gameObject.AddComponent<Menu>();
            gameObject.AddComponent<FpsWindow>();
            gameObject.AddComponent<ScriptLoader>();
            Log.LogInfo($"{Name} {Version} loaded. {MenuKey.Value}: menu, {InspectKey.Value}: inspect");
        }
    }
}
