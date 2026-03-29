// TimeTrackerWindow.cs — ventana principal con tabs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {

    public class TimeTrackerWindow : EditorWindow {
        // ── Tabs ─────────────────────────────────────────────────────
        enum Tab {
            Today,
            Week,
            Month,
            AllTime
        }
        Tab selectedTab = Tab.Today;
        static readonly string[] TAB_LABELS =
        {
            "TODAY",
            "WEEK",
            "MONTH",
            "ALL TIME"
        };

        // ── State ────────────────────────────────────────────────────
        TimeTrackingData data;

        // Month picker
        int monthYear = DateTime.Today.Year;
        int monthMonth = DateTime.Today.Month;
        static readonly string[] MONTH_NAMES =
        {
            "Jan",
            "Feb",
            "Mar",
            "Apr",
            "May",
            "Jun",
            "Jul",
            "Aug",
            "Sep",
            "Oct",
            "Nov",
            "Dec"
        };

        [MenuItem("Tools/Time Tracker")]
        public static void Open() {
            var w = GetWindow<TimeTrackerWindow>("Time Tracker");
            w.minSize = new Vector2(460, 340);
        }

        void OnEnable() => Refresh();
        void OnFocus() => Refresh();

        void Refresh() {
            data = TimeTrackerCore.LoadData();
            Repaint();
        }

        // ════════════════════════════════════════════════════════════
        //  OnGUI
        // ════════════════════════════════════════════════════════════
        void OnGUI() {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), TimeTrackerGUI.BgColor);

            float pad = 24f;
            float y = 16f;

            // ── Header ──────────────────────────────────────────────
            GUI.Label(new Rect(pad, y, 300, 30), "TIME TRACKER",
                TimeTrackerGUI.Style(20, TimeTrackerGUI.TextColor, FontStyle.Bold));

            if (GUI.Button(new Rect(position.width - pad - 64, y + 4, 64, 20), "↻ refresh",
                    new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 10,
                        normal =
                        {
                            textColor = TimeTrackerGUI.LabelColor
                        }
                    }))
                Refresh();

            y += 38f;

            // ── Tabs ────────────────────────────────────────────────
            DrawTabs(pad, ref y);
            y += 12f;

            // ── Content ─────────────────────────────────────────────
            switch (selectedTab) {
                case Tab.Today: DrawToday(pad, ref y); break;
                case Tab.Week: DrawWeek(pad, ref y); break;
                case Tab.Month: DrawMonth(pad, ref y); break;
                case Tab.AllTime: DrawAllTime(pad, ref y); break;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  TABS BAR
        // ════════════════════════════════════════════════════════════
        void DrawTabs(float pad, ref float y) {
            float tabW = (position.width - pad * 2 - (TAB_LABELS.Length - 1) * 6f) / TAB_LABELS.Length;
            float tabH = 26f;

            for (int i = 0; i < TAB_LABELS.Length; i++) {
                bool active = (int)selectedTab == i;
                Rect r = new Rect(pad + i * (tabW + 6), y, tabW, tabH);

                EditorGUI.DrawRect(r, active ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.BgDark);
                GUI.Label(r, TAB_LABELS[i], TimeTrackerGUI.Style(11,
                    active ? TimeTrackerGUI.BgColor : TimeTrackerGUI.LabelColor,
                    FontStyle.Bold, TextAnchor.MiddleCenter));

                if (!active && GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                    selectedTab = (Tab)i;
                    Repaint();
                }
            }

            y += tabH;
        }

        // ════════════════════════════════════════════════════════════
        //  TODAY
        // ════════════════════════════════════════════════════════════
        void DrawToday(float pad, ref float y) {
            float trackW = position.width - pad * 2;
            var sessions = TimeTrackerCore.GetSessionsForDate(data, DateTime.Today);
            double total = sessions.Sum(s => (s.end - s.start).TotalMinutes);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(total),
                $"{sessions.Count} session{(sessions.Count != 1 ? "s" : "")} today");

            TimeTrackerGUI.DrawTimeline(pad, trackW, ref y, sessions);
            y += 20f;

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "SESSIONS");
            y += 20f;

            foreach (var (i, s) in sessions.Select((s, i) => (i, s)))
                TimeTrackerGUI.DrawSessionRow(pad, trackW, ref y, s.start, s.end, i == sessions.Count - 1);

            if (sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No sessions recorded today",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor, anchor: TextAnchor.MiddleCenter));
                y += 24f;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  WEEK
        // ════════════════════════════════════════════════════════════
        void DrawWeek(float pad, ref float y) {
            float trackW = position.width - pad * 2;
            var (from, to) = TimeTrackerCore.GetWeekRange(DateTime.Today);
            var ps = TimeTrackerCore.ComputeStats(data, from, to);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"this week  ·  {ps.activeDays}/7 days active  ·  {ps.totalSessions} sessions");

            TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, ps.dailyMinutes, "ddd");
            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "AVERAGES & RECORDS");
            y += 20f;
            TimeTrackerGUI.DrawStatsGrid(pad, trackW, ref y, ps);
        }

        // ════════════════════════════════════════════════════════════
        //  MONTH
        // ════════════════════════════════════════════════════════════
        void DrawMonth(float pad, ref float y) {
            float trackW = position.width - pad * 2;

            // ── Month picker ─────────────────────────────────────────
            float pickerY = y;
            if (GUI.Button(new Rect(pad, pickerY, 22, 22), "‹",
                    new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 14,
                        normal =
                        {
                            textColor = TimeTrackerGUI.TextColor
                        }
                    })) {
                monthMonth--;
                if (monthMonth < 1) {
                    monthMonth = 12;
                    monthYear--;
                }
                Repaint();
            }
            if (GUI.Button(new Rect(pad + 28, pickerY, 22, 22), "›",
                    new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 14,
                        normal =
                        {
                            textColor = TimeTrackerGUI.TextColor
                        }
                    })) {
                monthMonth++;
                if (monthMonth > 12) {
                    monthMonth = 1;
                    monthYear++;
                }
                Repaint();
            }

            string monthLabel = $"{MONTH_NAMES[monthMonth - 1]} {monthYear}";
            GUI.Label(new Rect(pad + 56, pickerY + 3, 120, 18), monthLabel,
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));

            y += 34f;

            // ── Stats ────────────────────────────────────────────────
            var (from, to) = TimeTrackerCore.GetMonthRange(monthYear, monthMonth);
            int daysInMonth = (int)(to - from).TotalDays + 1;
            var ps = TimeTrackerCore.ComputeStats(data, from, to);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"{monthLabel}  ·  {ps.activeDays}/{daysInMonth} days active  ·  {ps.totalSessions} sessions");

            // Barras — si el mes tiene más de 14 días, agrupamos por semana para que quepan
            if (daysInMonth <= 14) {
                TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, ps.dailyMinutes, "dd");
            }
            else {
                TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, GroupByWeek(ps.dailyMinutes, from), "dd/MM");
            }

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "AVERAGES & RECORDS");
            y += 20f;
            TimeTrackerGUI.DrawStatsGrid(pad, trackW, ref y, ps);
        }

        // ════════════════════════════════════════════════════════════
        //  ALL TIME
        // ════════════════════════════════════════════════════════════
        void DrawAllTime(float pad, ref float y) {
            float trackW = position.width - pad * 2;

            if (data == null || data.sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No data recorded yet.",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor, anchor: TextAnchor.MiddleCenter));
                return;
            }

            // Rango completo
            DateTime first = data.sessions
                .Select(s => DateTime.TryParse(s.start, out var d) ? d : DateTime.MaxValue)
                .Min();
            DateTime last = DateTime.Today;
            int totalDays = (int)(last - first).TotalDays + 1;

            var ps = TimeTrackerCore.ComputeStats(data, first, last);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"since {first:dd MMM yyyy}  ·  {ps.activeDays}/{totalDays} days active  ·  {ps.totalSessions} sessions");

            // Barras mensuales
            var monthly = GroupByMonth(ps.dailyMinutes);
            TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, monthly, "MMM");

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "AVERAGES & RECORDS");
            y += 20f;
            TimeTrackerGUI.DrawStatsGrid(pad, trackW, ref y, ps);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void DrawBigNumber(float pad, float trackW, ref float y, string value, string sub) {
            GUI.Label(new Rect(pad, y, trackW, 36), value,
                TimeTrackerGUI.Style(26, TimeTrackerGUI.AccentColor, FontStyle.Bold));
            GUI.Label(new Rect(pad, y + 30, trackW, 18), sub,
                TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor));
            y += 56f;
        }

        static List<(DateTime date, double minutes)> GroupByWeek(
            List<(DateTime date, double minutes)> daily, DateTime monthStart) {
            var weeks = new List<(DateTime, double)>();
            int i = 0;
            while (i < daily.Count) {
                double sum = 0;
                DateTime weekLabel = daily[i].date;
                int j = 0;
                while (j < 7 && i + j < daily.Count) {
                    sum += daily[i + j].minutes;
                    j++;
                }
                weeks.Add((weekLabel, sum));
                i += j;
            }
            return weeks;
        }

        static List<(DateTime date, double minutes)> GroupByMonth(
            List<(DateTime date, double minutes)> daily) {
            var dict = new System.Collections.Generic.SortedDictionary<DateTime, double>();
            foreach (var (date, mins) in daily) {
                var key = new DateTime(date.Year, date.Month, 1);
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key] += mins;
            }
            return dict.Select(kv => (kv.Key, kv.Value)).ToList();
        }
    }
}