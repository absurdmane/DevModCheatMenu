using UnityEngine;

namespace DevMenu
{
    /// <summary>Time of day, weekday and time speed.</summary>
    internal sealed class TimeTab : Tab
    {
        public override string Title => "Time";

        private const float MaxSpeed = 100f;

        // TimeOfDayAzure.GetDayOfWeek: 0 = Monday ... 6 = Sunday.
        private static readonly string[] WeekDays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public override void Draw()
        {
            TimeOfDayAzure time = TimeOfDayAzure.instance;
            if (time == null)
            {
                GUILayout.Label("No time of day in this scene.");
                return;
            }
            float now = time.CurrentTimelineValue;

            Gui.CenteredHeading("Time");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // CurrentTimelineValue has an empty setter in the game: SetTimeline is what the console uses.
            foreach (float h in new[] { -12f, -6f, -1f })
                ShiftButton(time, now, h);
            Gui.CenteredLabel($"<b>{Clock(now)}</b>", 70);
            foreach (float h in new[] { 1f, 6f, 12f })
                ShiftButton(time, now, h);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (float h in new[] { 0f, 6f, 12f, 18f })
                if (GUILayout.Button(Clock(h), Gui.Fit("00:00", 80)))
                    Menu.Run("Time", () => time.SetTimeline(h));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int d = 0; d < 7; d++)
            {
                int target = d;
                if (GUILayout.Toggle(time.GetDayOfWeek() == d, WeekDays[d], GUI.skin.button, Gui.Fit("Wed", 48)) && time.GetDayOfWeek() != d)
                    Menu.Run("Day of week", () => SetWeekDay(time, target));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            Gui.CenteredHeading("Speed");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", Gui.Fit("Reset", 70)))
                time.timeSpeedOverrideFactor = 1f;
            Gui.CenteredLabel($"<b>{time.timeSpeedOverrideFactor:0.##}x</b>", 60);
            foreach ((float step, string label) in new[] { (1f, "1>"), (5f, "5>>"), (10f, "10>>>") })
                if (GUILayout.Button(label, Gui.Fit("10>>>", 70)))
                    time.timeSpeedOverrideFactor = Mathf.Min(MaxSpeed, time.timeSpeedOverrideFactor + step);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void ShiftButton(TimeOfDayAzure time, float now, float hours)
        {
            if (GUILayout.Button(hours > 0 ? $"+{hours}" : $"{hours}", Gui.Fit("-12", 48)))
                Menu.Run("Time", () => time.SetTimeline(Mathf.Repeat(now + hours, 24f)));
        }

        private static string Clock(float hours) => $"{Mathf.FloorToInt(hours):00}:{Mathf.FloorToInt(hours % 1f * 60f):00}";

        /// <summary>
        /// The weekday comes from the calendar date, so move the date forward to the wanted day (same time of day).
        /// <c>SetDate</c> also updates the current time record, so the game doesn't count it as days passing (no day counter change).
        /// </summary>
        private static void SetWeekDay(TimeOfDayAzure time, int target)
        {
            int ahead = (target - time.GetDayOfWeek() + 7) % 7;
            if (ahead == 0)
                return;
            System.DateTime date = new System.DateTime(time.GetYear(), time.GetMonth(), time.GetDay()).AddDays(ahead);
            time.SetDate(date.Year, date.Month, date.Day);
            Menu.Notify($"Day: {time.GetDayOfWeekString()}");
        }
    }
}
