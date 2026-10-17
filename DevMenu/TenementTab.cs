using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Sets building and apartment upgrade levels at once, through the game's own <c>TenementGeneral.Update(upgrade)</c> /
    /// <c>TenementApartment.Update(upgrade)</c>: they set the level and the upgrade reference and rebuild the visuals.
    /// </summary>
    internal sealed class TenementTab : Tab
    {
        public override string Title => "Tenement";

        private int apartmentNumber = 1;

        private sealed class Line
        {
            public string Section;
            public string Label;
            public Func<TenementUpgradeGroup> Group;
            public Func<object> Current;
            /// <summary>Back to no upgrade; null where not supported.</summary>
            public Action Reset;
        }

        public override void Draw()
        {
            TenementController tc = TenementController.instance;
            if (tc == null || tc.general == null)
            {
                GUILayout.Label("No Tenement in this scene.");
                return;
            }

            DrawShop(tc);

            TenementApartment apartment = tc.GetApartmentFromNumber(apartmentNumber);

            Gui.CenteredHeading("Building");
            DrawSections("building", BuildingLines(tc.general), apartment);

            Gui.CenteredHeading("Apartment");
            // A dozen and more apartments: they wrap onto more rows instead of widening the page.
            List<TenementApartment> all = tc.apartments.OrderBy(a => a.apartmentNumber).ToList();
            int picked = Gui.WrapToggles(
                all.Select(a => a.apartmentNumber == 1 ? "Player apartment" : a.apartmentNumber == 2 ? "Shop" : a.apartmentNumber.ToString()).ToList(),
                all.FindIndex(a => a.apartmentNumber == apartmentNumber));
            if (picked >= 0)
                apartmentNumber = all[picked].apartmentNumber;
            if (apartment == null)
                GUILayout.Label("No such apartment.");
            else
                DrawSections("apartment " + apartment.apartmentNumber, ApartmentLines(apartment), apartment);
        }

        /// <summary>Lines under small section headings, in the order given.</summary>
        // scope: whose lines these are; it keeps dropdown ids apart (Building and Apartment both have Utilities → Electricity).
        private void DrawSections(string scope, IEnumerable<Line> lines, TenementApartment apartment)
        {
            string section = null;
            foreach (Line line in lines)
            {
                if (line.Section != section)
                {
                    section = line.Section;
                    GUILayout.Space(4);
                    GUILayout.Label(Gui.Big(section));
                }
                DrawLine(scope, line, apartment);
            }
        }

        /// <summary>
        /// The shop's plywood "Start Blocker" is its <c>cleaningTimerBlocker</c>. The cleaning timer is started from a dialogue
        /// (<c>StartApartmentCleaningTimer</c>): -1 = not started, > 0 = cleaning until that day, 0 = done (the blocker is destroyed).
        /// </summary>
        private static void DrawShop(TenementController tc)
        {
            TenementApartment shop = tc.GetApartmentFromNumber(2);
            if (shop == null)
                return;
            if (shop.cleaningTimerCompleteDay != 0)
            {
                if (GUILayout.Button("Open the shop (remove the plywood)", GUILayout.ExpandWidth(false)))
                    Menu.Run("Open the shop", () =>
                    {
                        shop.cleaningTimerCompleteDay = 0;
                        shop.prefabs?.UpdateCleaningTimerState();
                        Menu.Notify("Shop opened");
                    });
            }
            // Undo: back to "not started". The destroyed plywood is part of the scene, so it's back after the next load.
            else if (GUILayout.Button("Close the shop again (plywood back after a reload)", GUILayout.ExpandWidth(false)))
            {
                shop.cleaningTimerCompleteDay = -1;
                Menu.Notify("Shop closed: save and reload to see the plywood");
            }
        }

        /// <summary>"Label  [current]"; a click pops up None and the levels under the button, ✓ on the current one.</summary>
        private void DrawLine(string scope, Line line, TenementApartment apartment)
        {
            TenementUpgrade[] levels = Levels(line);

            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            Gui.RowLabel(line.Label, 120);
            if (levels.Length == 0 && line.Reset == null)
                GUILayout.Label($"{line.Current()}  (no upgrades)");
            else
            {
                var actions = new List<Func<string>>();
                Dropdown.Button(
                    $"{scope}.{line.Section}.{line.Label}",
                    line.Current()?.ToString() ?? "",
                    () =>
                    {
                        TenementUpgrade current = CurrentLevel(levels, apartment);
                        var options = new List<string>();
                        actions.Clear();
                        if (line.Reset != null)
                        {
                            options.Add((current == null ? "✓ " : "") + "None");
                            actions.Add(() =>
                            {
                                line.Reset();
                                return $"{line.Label}: none";
                            });
                        }
                        foreach (TenementUpgrade upgrade in levels)
                        {
                            options.Add((upgrade == current ? "✓ " : "") + NameOf(upgrade, apartment));
                            actions.Add(() => Apply(upgrade, apartment));
                        }
                        return options.ToArray();
                    },
                    i => Menu.Run(line.Label, () =>
                    {
                        string done = actions[i]();
                        List<string> lowered = Enforce();
                        Menu.Notify(lowered.Count == 0 ? done : $"{done}. Also lowered, their requirements are gone: {string.Join(", ", lowered)}");
                    }),
                    120f);
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>The levels the game offers on a line, lowest first. Level 0 entries (Off / None / Bare) are placeholders: "None" stands for them.</summary>
        private static TenementUpgrade[] Levels(Line line)
        {
            TenementUpgradeGroup group = line.Group();
            TenementUpgrade[] levels = group?.updateGroup?.Where(u => u != null && SafeTier(u) != 0).ToArray() ?? new TenementUpgrade[0];
            if (levels.Length == 0 && group?.updateGroup != null)
                levels = group.updateGroup.Where(u => u != null).ToArray();
            return levels.OrderBy(SafeTier).ToArray();
        }

        /// <summary>The current level: the highest one reached (reaching a level counts every one below it as reached too). Null = none.</summary>
        private static TenementUpgrade CurrentLevel(TenementUpgrade[] levels, TenementApartment apartment) =>
            levels.LastOrDefault(u => SafeIsUpgraded(u, apartment));

        private static bool RequirementsMet(TenementUpgrade upgrade, TenementApartment apartment) =>
            upgrade.requiredUpgrades == null || upgrade.requiredUpgrades.All(r => r == null || SafeIsUpgraded(r, apartment));

        /// <summary>
        /// Upgrading pulls its requirements in (<see cref="Apply(TenementUpgrade, TenementApartment)"/>); this is the other way round.
        /// After a change, every line of the building and of every apartment whose current level no longer has its requirements
        /// (e.g. apartment electricity above the building's) goes down to the highest level that still has them, or to none.
        /// Repeated until nothing changes, since one drop can break another. Returns what was lowered.
        /// </summary>
        private List<string> Enforce()
        {
            TenementController tc = TenementController.instance;
            var lowered = new List<string>();
            TenementApartment player = tc.GetApartmentFromNumber(1);
            for (int pass = 0; pass < 10; pass++)
            {
                bool changed = false;
                foreach (Line line in BuildingLines(tc.general))
                    changed |= Lower(line, player, "Building", lowered);
                foreach (TenementApartment a in tc.apartments)
                    foreach (Line line in ApartmentLines(a))
                        changed |= Lower(line, a, $"apartment {a.apartmentNumber}", lowered);
                if (!changed)
                    break;
            }
            return lowered;
        }

        private bool Lower(Line line, TenementApartment apartment, string where, List<string> lowered)
        {
            TenementUpgrade[] levels = Levels(line);
            TenementUpgrade current = CurrentLevel(levels, apartment);
            if (current == null || RequirementsMet(current, apartment))
                return false;
            TenementUpgrade fallback = levels.Where(u => SafeTier(u) < SafeTier(current) && RequirementsMet(u, apartment)).LastOrDefault();
            if (fallback != null)
                SetLevel(fallback, apartment);
            else if (line.Reset != null)
                line.Reset();
            else
                return false;
            lowered.Add($"{where} {line.Label} → {(fallback != null ? NameOf(fallback, apartment) : "none")}");
            return true;
        }

        private string Apply(TenementUpgrade upgrade, TenementApartment apartment)
        {
            var applied = new List<string>();
            Apply(upgrade, apartment, applied, new HashSet<TenementUpgrade>());
            return "Applied: " + string.Join(", ", applied);
        }
        private void Apply(TenementUpgrade upgrade, TenementApartment apartment, List<string> applied, HashSet<TenementUpgrade> visiting)
        {
            if (!visiting.Add(upgrade))
                return;
            if (upgrade.requiredUpgrades != null)
                foreach (TenementUpgrade required in upgrade.requiredUpgrades)
                    if (required != null && !SafeIsUpgraded(required, apartment))
                        Apply(required, apartment, applied, visiting);
            SetLevel(upgrade, apartment);
            applied.Add(NameOf(upgrade, apartment));
        }

        /// <summary>Sets this one level (up or down) through the game's own Update, without touching requirements.</summary>
        private static void SetLevel(TenementUpgrade upgrade, TenementApartment apartment)
        {
            TenementController tc = TenementController.instance;
            // A finished-but-unpaid or running upgrade of the same kind would fight with ours.
            tc.currentUpgradeProgresses.RemoveAll(p => p != null && p.upgrade == upgrade);
            if (IsApartmentUpgrade(upgrade))
            {
                if (apartment == null)
                    throw new InvalidOperationException($"{upgrade.name} needs an apartment");
                apartment.Update(upgrade);
            }
            else
            {
                tc.general.Update(upgrade);
            }
        }

        private static bool IsApartmentUpgrade(TenementUpgrade u) =>
            u.upgradeElectricity || u.upgradeWater || u.upgradeHeatingMethod || u.upgradeBathroom || u.upgradeWalls ||
            u.upgradeBalcony || u.upgradeBalcony2 || u.upgradeKitchen;

        private static int SafeTier(TenementUpgrade upgrade)
        {
            try
            {
                return upgrade.GetTier();
            }
            catch
            {
                return -1;
            }
        }

        private static bool SafeIsUpgraded(TenementUpgrade upgrade, TenementApartment apartment)
        {
            try
            {
                // The game's IsUpgraded compares apartment heating with heatingType (the building's field), not heatingMethod.
                if (upgrade.upgradeHeatingMethod && apartment != null)
                    return (int)apartment.currentHeating >= (int)upgrade.heatingMethod;
                return upgrade.IsUpgraded(apartment);
            }
            catch
            {
                return false;
            }
        }

        private static string NameOf(TenementUpgrade upgrade, TenementApartment apartment)
        {
            try
            {
                string name = upgrade.GetName(apartment?.apartmentNumber ?? 1);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            catch
            {
                // fall back to the asset name
            }
            return upgrade.name;
        }

        private static IEnumerable<Line> BuildingLines(TenementGeneral g)
        {
            (string section, string label, Func<TenementUpgradeGroup> group, string level, string upgrade)[] lines =
            {
                ("Utilities", "Electricity", () => g.electricityUpgrades, "currentElectricity", "currentElectricityUpgrade"),
                ("Utilities", "Water", () => g.waterUpgrades, "currentWater", "currentWaterUpgrade"),
                ("Utilities", "Heating", () => g.heatingUpgrades, "currentHeating", "currentHeatingUpgrade"),
                ("Structure", "Stairs", () => g.stairsUpgrades, "currentStairs", "currentStairsUpgrade"),
                ("Structure", "Elevator", () => g.elevatorUpgrades, "currentElevator", "currentElevatorUpgrade"),
                ("Structure", "Facade", () => g.facadeUpgrades, "currentFacade", "currentFacadeUpgrade"),
                ("Structure", "Roof", () => g.roofUpgrades, "currentRoof", "currentRoofUpgrade"),
                ("Indoor rooms", "Basement", () => g.basementUpgrades, "currentBasement", "currentBasementUpgrade"),
                ("Indoor rooms", "Workshop", () => g.workshopUpgrades, "currentWorkshop", "currentWorkshopUpgrade"),
                ("Indoor rooms", "Shrooms", () => g.shroomsUpgrades, "currentShrooms", "currentShroomsUpgrade"),
                ("Indoor rooms", "Sauna", () => g.saunaUpgrades, "currentSauna", "currentSaunaUpgrade"),
                ("Indoor rooms", "Mine", () => g.mineUpgrades, "currentMine", "currentMineUpgrade"),
                ("Outdoors", "Courtyard", () => g.courtyardUpgrades, "currentCourtyard", "currentCourtyardUpgrade"),
                ("Outdoors", "Fence", () => g.fenceUpgrades, "currententFence", "currentFenceUpgrade"),
                ("Outdoors", "Greenhouse", () => g.greenhouseUpgrades, "currententGreenhouse", "currentGreenhouseUpgrade"),
                ("Outdoors", "Sauna & pier", () => g.saunaAndPierUpgrades, "currentSaunaAndPier", "currentSaunaAndPierUpgrade")
            };
            foreach (var (section, label, group, level, upgrade) in lines)
                yield return new Line
                {
                    Section = section,
                    Label = label,
                    Group = group,
                    Current = () => Field(g, level)?.GetValue(g),
                    Reset = () =>
                    {
                        ResetFields(g, level, upgrade);
                        g.prefabs?.UpdateGeneralPrefabs(g);
                    }
                };
        }

        private static IEnumerable<Line> ApartmentLines(TenementApartment a)
        {
            TenementApartmentType t = a.type;
            (string section, string label, Func<TenementUpgradeGroup> group, string level, string upgrade)[] lines =
            {
                ("Utilities", "Electricity", () => t?.electricity, "currentElectricity", "currentElectricityUpgrade"),
                ("Utilities", "Water", () => t?.water, "currentWater", "currentWaterUpgrade"),
                ("Utilities", "Heating", () => t?.heating, "currentHeating", "currentHeatingUpgrade"),
                ("Rooms", "Kitchen", () => t?.kitchen, "currentKitchen", "currentKitchenUpgrade"),
                ("Rooms", "Bathroom", () => t?.bathroom, "currentBathroom", "currentBathroomUpgrade"),
                ("Rooms", "Walls", () => t?.walls, "currentWall", "currentWallUpgrade"),
                ("Rooms", "Balcony", () => t?.balcony, "currentBalcony", "currentBalconyUpgrade"),
                ("Rooms", "Balcony 2", () => t?.balcony2, "currentBalcony2", "currentBalcony2Upgrade")
            };
            foreach (var (section, label, group, level, upgrade) in lines)
                yield return new Line
                {
                    Section = section,
                    Label = label,
                    Group = group,
                    Current = () => Field(a, level)?.GetValue(a),
                    Reset = () =>
                    {
                        ResetFields(a, level, upgrade);
                        a.prefabs?.UpdateApartmentPrefabs(a);
                    }
                };
        }

        // Level and upgrade fields by name: every line works the same way ("None" = level 0, no upgrade asset, visuals rebuilt).
        private static System.Reflection.FieldInfo Field(object owner, string name) =>
            owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static void ResetFields(object owner, string level, string upgrade)
        {
            System.Reflection.FieldInfo levelField = Field(owner, level)
                ?? throw new InvalidOperationException($"No field {level} on {owner.GetType().Name}");
            levelField.SetValue(owner, Activator.CreateInstance(levelField.FieldType));
            Field(owner, upgrade)?.SetValue(owner, null);
        }
    }
}
