using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    /// <summary>Furniture into the build inventory, the same way the console's add_furniture does.</summary>
    internal sealed class FurnitureTab : Tab
    {
        public override string Title => "Furniture";

        private string amount = "1";
        private string skin = "0";

        private readonly SearchList<Furniture> list = new SearchList<Furniture>(All, f =>
            string.IsNullOrEmpty(f.title) || f.title == f.name ? $"{f.name}  [{f.category}]" : $"{f.title}  ({f.name})  [{f.category}]");

        /// <summary>The console's list when it exists, plus every furniture asset loaded right now.</summary>
        private static IEnumerable<Furniture> All()
        {
            var result = new HashSet<Furniture>();
            DeveloperConsole console = DeveloperConsole.Instance;
            if (console != null && console.furniture != null)
                result.UnionWith(console.furniture.furnitures.Where(f => f != null));
            result.UnionWith(Resources.FindObjectsOfTypeAll<Furniture>());
            return result.Where(f => f.category != Furniture.Category.None);
        }

        public override void Draw()
        {
            if (BuildingSystem.instance == null)
            {
                GUILayout.Label("No building system in this scene.");
                return;
            }
            GUILayout.BeginHorizontal();
            Gui.InlineLabel("Amount");
            amount = GUILayout.TextField(amount, Gui.Fit("999", 60, GUI.skin.textField));
            Gui.InlineLabel("Skin (0 = default)");
            skin = GUILayout.TextField(skin, Gui.Fit("99", 40, GUI.skin.textField));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear build inventory", GUILayout.ExpandWidth(false)))
                Menu.Run("Clear build inventory", ClearBuildInventory);
            GUILayout.EndHorizontal();

            Furniture f = list.Draw();
            if (f == null)
                return;
            Menu.Run("Furniture", () =>
            {
                int n = int.TryParse(amount, out int a) && a > 0 ? a : 1;
                BuildingSystem.FurnitureInfo.Meta meta = null;
                if (int.TryParse(skin, out int s) && s > 0)
                    meta = new BuildingSystem.FurnitureInfo.Meta { currentSkin = s };
                BuildingSystem.instance.AddFurniture(f, n, meta);
                Menu.Notify($"Added {n} × {f.name} to the build inventory");
            });
        }

        /// <summary>Empties the build (furniture) inventory.</summary>
        private static void ClearBuildInventory()
        {
            BuildingSystem bs = BuildingSystem.instance;
            int count = 0;
            foreach (BuildingSystem.FurnitureInfo f in bs.availableFurnitures.ToArray())
            {
                count += f.amount;
                bs.RemoveFurniture(f, f.amount, updateMenu: true);
            }
            Menu.Notify($"Removed {count} furniture");
        }
    }
}
