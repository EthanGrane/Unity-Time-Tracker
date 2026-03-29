// TimeTrackerGUI.cs — helpers de dibujo reutilizables
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace UnityTimeTracker {

    public static class TimeTrackerGUI {
        // ── Palette ──────────────────────────────────────────────────
        public static readonly Color BgColor = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color BgDark = new Color(0.08f, 0.08f, 0.10f);
        public static readonly Color BgCard = new Color(0.16f, 0.16f, 0.19f);
        public static readonly Color SessionColor = new Color(0.20f, 0.80f, 0.55f);
        public static readonly Color SessionDim = new Color(0.20f, 0.80f, 0.55f, 0.35f);
        public static readonly Color TickColor = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.35f);
        public static readonly Color TextColor = new Color(1f, 1f, 1f, 0.85f);
        public static readonly Color AccentColor = new Color(0.20f, 0.80f, 0.55f);
        public static readonly Color DivColor = new Color(1f, 1f, 1f, 0.06f);

        // ── Estilos ──────────────────────────────────────────────────
        public static GUIStyle Style(int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
            => new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = anchor,
                normal =
                {
                    textColor = color
                }
            };

        // ── Componentes ──────────────────────────────────────────────

        public static void DrawDivider(float pad, float trackW, ref float y) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), DivColor);
            y += 12f;
        }

        public static void DrawSectionLabel(float pad, float y, string text) {
            GUI.Label(new Rect(pad, y, 300, 16), text, Style(9, LabelColor, FontStyle.Bold));
        }

        public static void DrawStatCard(Rect r, string label, string value, string sub = "") {
            EditorGUI.DrawRect(r, BgCard);
            GUI.Label(new Rect(r.x + 10, r.y + 8, r.width - 20, 14), label, Style(9, LabelColor));
            GUI.Label(new Rect(r.x + 10, r.y + 20, r.width - 20, 24), value, Style(16, TextColor, FontStyle.Bold));
            if (!string.IsNullOrEmpty(sub))
                GUI.Label(new Rect(r.x + 10, r.y + 38, r.width - 20, 12), sub, Style(9, LabelColor));
        }

        public static void DrawTimeline(float pad, float trackW, ref float y, List<(DateTime start, DateTime end)> sessions) {
            float trackH = 28f;

            EditorGUI.DrawRect(new Rect(pad, y, trackW, trackH), BgDark);

            foreach (var (start, end) in sessions) {
                float x0 = pad + (float)(start.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float x1 = pad + (float)(end.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float w = Mathf.Max(x1 - x0, 3f);
                EditorGUI.DrawRect(new Rect(x0 - 1, y - 1, w + 2, trackH + 2), SessionDim);
                EditorGUI.DrawRect(new Rect(x0, y, w, trackH), SessionColor);
            }

            // Now marker
            float nowX = pad + (float)(DateTime.Now.TimeOfDay.TotalMinutes / 1440.0) * trackW;
            EditorGUI.DrawRect(new Rect(nowX - 1, y - 5, 2, trackH + 10), Color.white);

            y += trackH + 6f;

            // Hour ticks
            int[] hours =
            {
                0,
                3,
                6,
                9,
                12,
                15,
                18,
                21,
                24
            };
            foreach (int h in hours) {
                float tx = pad + (h / 24f) * trackW;
                EditorGUI.DrawRect(new Rect(tx, y, 1, 4), TickColor);
                if (h < 24)
                    GUI.Label(new Rect(tx - 13, y + 4, 28, 14), $"{h:00}h", Style(9, LabelColor, anchor: TextAnchor.UpperCenter));
            }

            y += 20f;
        }

        public static void DrawBarChart(float pad, float trackW, ref float y,
            List<(DateTime date, double minutes)> dailyMinutes, string labelFormat = "ddd") {
            if (dailyMinutes == null || dailyMinutes.Count == 0) return;

            float barAreaH = 64f;
            int count = dailyMinutes.Count;
            float barW = (trackW - (count - 1) * 4f) / count;
            double maxMins = dailyMinutes.Max(d => d.minutes);
            if (maxMins <= 0) maxMins = 1;

            EditorGUI.DrawRect(new Rect(pad, y, trackW, barAreaH + 30), BgDark);

            for (int i = 0; i < count; i++) {
                var (date, mins) = dailyMinutes[i];
                float bx = pad + i * (barW + 4);
                float bh = (float)(mins / maxMins) * (barAreaH - 10);
                float by = y + barAreaH - bh;
                bool isToday = date.Date == DateTime.Today;

                EditorGUI.DrawRect(new Rect(bx, y + 4, barW, barAreaH - 10), new Color(1f, 1f, 1f, 0.04f));

                if (mins > 0)
                    EditorGUI.DrawRect(new Rect(bx, by, barW, bh), isToday ? AccentColor : SessionDim);

                GUI.Label(new Rect(bx, y + barAreaH - 2, barW, 14),
                    date.ToString(labelFormat).ToUpper(), Style(9, LabelColor, anchor: TextAnchor.UpperCenter));

                if (mins > 15)
                    GUI.Label(new Rect(bx, by - 14, barW, 14),
                        $"{(int)(mins / 60)}h", Style(8, TextColor, anchor: TextAnchor.UpperCenter));

                if (isToday)
                    EditorGUI.DrawRect(new Rect(bx, y + barAreaH + 14, barW, 2), AccentColor);
            }

            y += barAreaH + 32f;
        }

        public static void DrawSessionRow(float pad, float trackW, ref float y, DateTime start, DateTime end, bool isLast) {
            bool isLive = isLast && (DateTime.Now - end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            double mins = (end - start).TotalMinutes;

            EditorGUI.DrawRect(new Rect(pad, y + 5, 6, 6), isLive ? AccentColor : SessionDim);
            GUI.Label(new Rect(pad + 14, y, 130, 18), $"{start:HH:mm} → {end:HH:mm}", Style(11, TextColor));
            GUI.Label(new Rect(pad + 150, y, 80, 18), TimeTrackerCore.FormatDuration(mins),
                Style(11, isLive ? TextColor : LabelColor));

            if (isLive)
                GUI.Label(new Rect(pad + 240, y + 2, 50, 14), "● LIVE", Style(9, AccentColor, FontStyle.Bold));

            y += 20f;
        }

        public static void DrawStatsGrid(float pad, float trackW, ref float y, PeriodStats ps) {
            float cardW = (trackW - 8) / 2f;
            float cardH = 52f;

            DrawStatCard(new Rect(pad, y, cardW, cardH), "AVG / DAY", TimeTrackerCore.FormatDuration(ps.avgMinutesPerDay));
            DrawStatCard(new Rect(pad + cardW + 8, y, cardW, cardH), "AVG / ACTIVE DAY", TimeTrackerCore.FormatDuration(ps.avgMinutesPerActiveDay));
            y += cardH + 8f;

            string longestSub = ps.longestSession.start != default ? ps.longestSession.start.ToString("ddd dd HH:mm") : "";
            DrawStatCard(new Rect(pad, y, cardW, cardH), "LONGEST SESSION", TimeTrackerCore.FormatDuration(ps.longestSessionMinutes), longestSub);
            DrawStatCard(new Rect(pad + cardW + 8, y, cardW, cardH), "SHORTEST SESSION", TimeTrackerCore.FormatDuration(ps.shortestSessionMinutes));
            y += cardH + 8f;

            string earliest = ps.earliestStartMinutes >= 0 ? $"{(int)(ps.earliestStartMinutes / 60):00}:{(int)(ps.earliestStartMinutes % 60):00}" : "--";
            string latest = ps.latestEndMinutes >= 0 ? $"{(int)(ps.latestEndMinutes / 60):00}:{(int)(ps.latestEndMinutes % 60):00}" : "--";
            DrawStatCard(new Rect(pad, y, cardW, cardH), "EARLIEST START", earliest);
            DrawStatCard(new Rect(pad + cardW + 8, y, cardW, cardH), "LATEST END", latest);
            y += cardH + 8f;
        }
    }
}