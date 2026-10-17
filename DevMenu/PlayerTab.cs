using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    internal sealed class PlayerTab : Tab
    {
        public override string Title => "Player";

        private const int SkillColumns = 3;

        // What's typed in each field, kept while it's being edited.
        private readonly Dictionary<PlayerStats.PlayerSkillType, string> skillText = new Dictionary<PlayerStats.PlayerSkillType, string>();
        private readonly Dictionary<Money.Currency, string> moneyText = new Dictionary<Money.Currency, string>();

        public override void Draw()
        {
            PlayerStats stats = PlayerStats.instance;
            if (stats == null)
            {
                GUILayout.Label("No player in this scene.");
                return;
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            Gui.RowLabel("God mode:");
            stats.Godmode = Gui.CheckButton("Enabled", stats.Godmode, 150);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Gui.RowLabel("Needs:");
            stats.StatsProgressDisabled = Gui.CheckButton("Frozen", stats.StatsProgressDisabled, 150);
            if (GUILayout.Button("Refill", Gui.Fit("Refill", 150)))
                Menu.Run("Refill needs", () =>
                {
                    stats.ClearAllNeeds();
                    Menu.Notify("Needs and health refilled");
                });
            GUILayout.EndHorizontal();

            if (Money.instance != null)
            {
                MoneyRow(Money.Currency.OC, 200000);
                MoneyRow(Money.Currency.RM, 10000);
            }

            DrawSkills(stats);
        }

        /// <summary>"OC:  (amount) [Add N]". The field shows the current amount; type a new one and press Enter to set it.</summary>
        private void MoneyRow(Money.Currency currency, int add)
        {
            Money money = Money.instance;
            float current = currency == Money.Currency.RM ? money.RMoneyAmount : money.OCMoneyAmount;
            string control = "devmenu.money." + currency;

            GUILayout.BeginHorizontal();
            Gui.RowLabel($"{currency}:");
            bool editing = GUI.GetNameOfFocusedControl() == control;
            if (!editing || !moneyText.TryGetValue(currency, out string text))
                text = current.ToString(currency == Money.Currency.RM ? "0.##" : "0", CultureInfo.InvariantCulture);
            bool enter = Gui.EnterPressed(control);
            GUI.SetNextControlName(control);
            moneyText[currency] = new string(GUILayout.TextField(text, 12, Gui.Fit("999999999", 150, GUI.skin.textField)).Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (enter && float.TryParse(moneyText[currency], NumberStyles.Float, CultureInfo.InvariantCulture, out float target))
                Menu.Run("Money", () =>
                {
                    // AddMoney takes a negative amount too, and updates the money panels and the dialogue variables.
                    money.AddMoney(target - current, currency);
                    GUI.FocusControl(null);
                    Menu.Notify($"{currency}: {target}");
                });
            if (GUILayout.Button($"Add {add:N0}", Gui.Fit($"Add {add:N0}", 150)))
                Menu.Run("Money", () =>
                {
                    money.AddMoney(add, currency);
                    Menu.Notify($"+{add:N0} {currency}");
                });
            GUILayout.EndHorizontal();
        }

        /// <summary>Skills in a grid: name on top, "[-] (value) [+]" under it.</summary>
        private void DrawSkills(PlayerStats stats)
        {
            Gui.CenteredHeading($"Skills (1–{PlayerStats.maxLevel})");
            if (stats.Skills == null)
                return;
            List<PlayerStats.PlayerSkill> skills = stats.Skills
                .Where(s => s != null && s.Type != PlayerStats.PlayerSkillType.None)
                .ToList();
            for (int i = 0; i < skills.Count; i += SkillColumns)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < SkillColumns; c++)
                {
                    GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
                    if (i + c < skills.Count)
                        DrawSkill(skills[i + c]);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawSkill(PlayerStats.PlayerSkill skill)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(skill.Type.ToString(), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("-", Gui.Fit("-", 30)))
                SetSkill(skill, skill.Skill - 1f);
            if (!skillText.TryGetValue(skill.Type, out string text))
                text = Format(skill.Skill);
            string typed = GUILayout.TextField(text, 6, Gui.Fit("10.00", 60, GUI.skin.textField));
            typed = new string(typed.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
            skillText[skill.Type] = typed;
            if (float.TryParse(typed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                !Mathf.Approximately(value, skill.Skill) && value >= 1f && value <= PlayerStats.maxLevel)
                skill.Skill = value;
            if (GUILayout.Button("+", Gui.Fit("+", 30)))
                SetSkill(skill, skill.Skill + 1f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void SetSkill(PlayerStats.PlayerSkill skill, float value)
        {
            skill.Skill = Mathf.Clamp(value, 1f, PlayerStats.maxLevel);
            skillText[skill.Type] = Format(skill.Skill);
        }

        private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
