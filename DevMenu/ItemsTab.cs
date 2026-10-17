using UnityEngine;

namespace DevMenu
{
    /// <summary>Regular items into the inventory (overflow drops at the feet) and key/quest items (passport and such).</summary>
    internal sealed class ItemsTab : Tab
    {
        public override string Title => "Items";

        private bool keyItems;
        private string amount = "1";

        private readonly SearchList<Item> items = new SearchList<Item>(
            () => ItemDatabase.database,
            i => $"{i.Title}  [{i.ID}]");

        private readonly SearchList<TaskItem> taskItems = new SearchList<TaskItem>(
            KeyItems.All,
            t => string.IsNullOrEmpty(t.itemName) || t.itemName == t.name ? t.name : $"{t.itemName}  ({t.name})");

        public override void Draw()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!keyItems, "Items", GUI.skin.button))
                keyItems = false;
            if (GUILayout.Toggle(keyItems, "Key items (passport, ...)", GUI.skin.button))
                keyItems = true;
            GUILayout.EndHorizontal();

            if (keyItems)
            {
                GUILayout.Label("Click gives one. They go to the key items list, not the inventory.");
                TaskItem t = taskItems.Draw();
                if (t != null)
                    Menu.Run("Key item", () =>
                    {
                        TaskItemsManager.instance.AddTaskItem(t, showNotification: true);
                        Menu.Notify($"Added {t.name}");
                    });
                return;
            }

            GUILayout.BeginHorizontal();
            Gui.InlineLabel("Amount");
            amount = GUILayout.TextField(amount, Gui.Fit("999", 60, GUI.skin.textField));
            // Right end of the amount row, as "Clear build inventory" on the Furniture tab.
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear inventory", GUILayout.ExpandWidth(false)))
                Menu.Run("Clear inventory", ClearInventory);
            GUILayout.EndHorizontal();
            Item item = items.Draw();
            if (item != null)
                Menu.Run("Item", () =>
                {
                    int n = int.TryParse(amount, out int parsed) && parsed > 0 ? parsed : 1;
                    ItemOperations.AddItemsAndDropRemaining(item, amount: n);
                    Menu.Notify($"Added {n} × {item.Title}");
                });
        }

        /// <summary>Empties the player's slots and the backpack's; worn items (character slots) stay.</summary>
        private static void ClearInventory()
        {
            int stacks = 0;
            foreach (SlotController slot in Inventory.instance.AllSlots(ignoreCharacterSlots: true))
            {
                if (slot == null || slot.itemStack == null || slot.itemStack.itemAmount <= 0)
                    continue;
                slot.itemStack.RemoveStack();
                slot.slot?.UpdateSlot();
                stacks++;
            }
            Inventory.instance.UpdateAllSlots();
            Menu.Notify($"Removed {stacks} stacks");
        }
    }
}
