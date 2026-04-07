// TimeTrackerGUI.cs — reusable drawing helpers
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace UnityTimeTracker {

    public static class TimeTrackerGUI {

        // ── Active theme accessors ────────────────────────────────────
        static TimeTrackerThemeData T => TimeTrackerSettings.Current;

        public static Color BgColor     => T.GetBg();
        public static Color BgDark      => T.GetBgDark();
        public static Color BgCard      => new Color(
            Mathf.Clamp01(T.GetBg().r + 0.03f),
            Mathf.Clamp01(T.GetBg().g + 0.03f),
            Mathf.Clamp01(T.GetBg().b + 0.04f));
        public static Color SessionColor => T.GetSession();
        public static Color SessionDim   => new Color(T.GetSession().r, T.GetSession().g, T.GetSession().b, 0.35f);
        public static Color TickColor    => new Color(1f, 1f, 1f, 0.12f);
        public static Color LabelColor   => new Color(T.GetText().r, T.GetText().g, T.GetText().b, 0.40f);
        public static Color TextColor    => T.GetText();
        public static Color AccentColor  => T.GetAccent();
        public static Color DivColor     => new Color(1f, 1f, 1f, 0.06f);

        // ── Styles ───────────────────────────────────────────────────
        public static GUIStyle Style(int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
            => new GUIStyle(EditorStyles.label) {
                fontSize  = size,
                fontStyle = fontStyle,
                alignment = anchor,
                normal    = { textColor = color }
            };

        // ── Components ───────────────────────────────────────────────

        public static void DrawDivider(float pad, float trackW, ref float y) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), DivColor);
            y += 12f;
        }

        public static void DrawSectionLabel(float pad, float y, string text) {
            GUI.Label(new Rect(pad, y, 300, 16), text, Style(9, LabelColor, FontStyle.Bold));
        }

        public static void DrawStatCard(Rect r, string label, string value, string sub = "") {
            EditorGUI.DrawRect(r, BgCard);
            GUI.Label(new Rect(r.x + 10, r.y + 8,  r.width - 20, 14), label, Style(9,  LabelColor));
            GUI.Label(new Rect(r.x + 10, r.y + 20, r.width - 20, 24), value, Style(16, TextColor, FontStyle.Bold));
            if (!string.IsNullOrEmpty(sub))
                GUI.Label(new Rect(r.x + 10, r.y + 38, r.width - 20, 12), sub, Style(9, LabelColor));
        }

        // ── Returns flat bg color for a given hour (no gradient) ─────
        public static Color TimelineColorAt(float hour) {
            var th = T;
            // Two flat zones: work hours = workColor, everything else = offColor
            bool isWork = hour >= th.workStartHour && hour < th.workEndHour;
            return isWork ? th.GetWork() : th.GetOff();
        }

        // ── Draw moon (crescent via two discs) ────────────────────────
        public static void DrawMoon(float cx, float cy, float r, Color moonColor, Color bgColor) {
            int steps = Mathf.Max(4, (int)(r * 2));
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, r * r - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW, cy + fy, halfW * 2f, 1f), moonColor);
            }
            float offset = r * 0.38f;
            float rInner = r * 0.82f;
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, rInner * rInner - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW + offset, cy + fy, halfW * 2f, 1f), bgColor);
            }
        }

        // ── Draw sun (disc + 8 rays) ──────────────────────────────────
        public static void DrawSun(float cx, float cy, float r, Color sunColor) {
            int steps = Mathf.Max(4, (int)(r * 2));
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, r * r - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW, cy + fy, halfW * 2f, 1f), sunColor);
            }
            float rayLen   = r * 0.55f;
            float rayStart = r + 1.5f;
            for (int i = 0; i < 8; i++) {
                float angle = i * (360f / 8f) * Mathf.Deg2Rad;
                float cos   = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                float x0    = cx + cos * rayStart,           y0 = cy + sin * rayStart;
                float x1    = cx + cos * (rayStart + rayLen), y1 = cy + sin * (rayStart + rayLen);
                int   ss    = Mathf.Max(2, (int)rayLen);
                for (int s = 0; s <= ss; s++) {
                    float t = s / (float)ss;
                    EditorGUI.DrawRect(new Rect(Mathf.Lerp(x0,x1,t) - 0.5f, Mathf.Lerp(y0,y1,t) - 0.5f, 1.5f, 1.5f), sunColor);
                }
            }
        }

        // ── Draw emoji label centered at (cx, cy) ─────────────────────
        static void DrawEmoji(float cx, float cy, string emoji) {
            float w = 20f, h = 18f;
            GUI.Label(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), emoji,
                new GUIStyle(EditorStyles.label) {
                    fontSize  = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white }
                });
        }

        // ── Timeline with flat day/off zones ─────────────────────────
        public static void DrawTimeline(float pad, float trackW, ref float y,
                List<(DateTime start, DateTime end)> sessions) {
            float trackH    = 32f;
            int   numSlices = 240;

            // Flat background (no gradient — hard edge at work hours)
            for (int i = 0; i < numSlices; i++) {
                float t0   = i / (float)numSlices;
                float t1   = (i + 1) / (float)numSlices;
                float hour = t0 * 24f;
                EditorGUI.DrawRect(new Rect(pad + t0 * trackW, y, (t1 - t0) * trackW + 1f, trackH),
                    TimelineColorAt(hour));
            }

            // Borders
            EditorGUI.DrawRect(new Rect(pad, y,              trackW, 1), new Color(1f,1f,1f,0.08f));
            EditorGUI.DrawRect(new Rect(pad, y + trackH - 1, trackW, 1), new Color(1f,1f,1f,0.08f));

            // Session blocks
            var sc = T.GetSession();
            foreach (var (start, end) in sessions) {
                const float vPadding = 4;
                bool isLive = (DateTime.Now - end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;

                float x0 = pad + (float)(start.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float x1 = pad + (float)(end.TimeOfDay.TotalMinutes   / 1440.0) * trackW;
                float w  = Mathf.Max(x1 - x0, 3f);

                // 👇 altura normal vs live
                float h = isLive ? trackH - vPadding * 2 : trackH;
                float yOffset = isLive ? vPadding : 0f; // centrado vertical

                // glow
                EditorGUI.DrawRect(new Rect(x0-1, y-1 + yOffset, w+2, h+2),
                    new Color(sc.r, sc.g, sc.b, 0.18f));

                // main bar
                EditorGUI.DrawRect(new Rect(x0, y + yOffset, w, h),
                    new Color(sc.r, sc.g, sc.b, 0.5f));

                // top highlight
                EditorGUI.DrawRect(new Rect(x0, y + yOffset, w, 2),
                    new Color(1f, 1f, 1f, 0.35f));
            }

            // "Now" marker
            float nowX = pad + (float)(DateTime.Now.TimeOfDay.TotalMinutes / 1440.0) * trackW;
            EditorGUI.DrawRect(new Rect(nowX-1, y-6, 2, trackH+12), Color.white);
            EditorGUI.DrawRect(new Rect(nowX-3, y-6, 6, 2),         Color.white);

            y += trackH + 6f;

            // ── Icons below timeline ─────────────────────────────────
            float iconR = 5f;
            float iconY = y + 2f + iconR;

            var th = T;
            TimelineIconStyle style = th.IconStyle;

            if (style == TimelineIconStyle.SunMoon) {
                // Classic: moon left/right, sun center
                if (th.showOffIcon) {
                    Color moonCol   = th.GetMoon();
                    float moonLHour = th.nightEndHour / 2f;
                    float moonRHour = (th.nightStartHour + 24f) / 2f;
                    DrawMoon(pad + (moonLHour / 24f) * trackW,                        iconY, iconR, moonCol, TimelineColorAt(moonLHour));
                    DrawMoon(pad + (Mathf.Min(moonRHour, 23.5f) / 24f) * trackW,      iconY, iconR, moonCol, TimelineColorAt(th.nightStartHour + 1.5f));
                }
                if (th.showWorkIcon) {
                    float sunCenter = (th.dayStartHour + th.dayEndHour) / 2f;
                    DrawSun(pad + (sunCenter / 24f) * trackW, iconY, iconR, th.GetSun());
                }
            } else {
                if (th.showWorkIcon) {
                    float workCenter = (th.workStartHour + th.workEndHour) / 2f;
                    float offLeftCenter = th.workStartHour / 2f;
                    float offRightCenter = (th.workEndHour + 24f) / 2f;

                    // 💤 izquierda (antes del trabajo)
                    DrawEmoji(pad + (offLeftCenter / 24f) * trackW, iconY, "💤");

                    // 💤 derecha (después del trabajo)
                    DrawEmoji(pad + (Mathf.Min(offRightCenter, 23.5f) / 24f) * trackW, iconY, "💤");

                    // 👷 trabajo
                    DrawEmoji(pad + (workCenter / 24f) * trackW, iconY, "👷");
                }
            }

            y += iconR * 2f + 6f;

            // Hour ticks
            int[] hours = { 0, 3, 6, 9, 12, 15, 18, 21, 24 };
            foreach (int h in hours) {
                float tx = pad + (h / 24f) * trackW;
                EditorGUI.DrawRect(new Rect(tx, y, 1, 4), TickColor);
                if (h < 24)
                    GUI.Label(new Rect(tx-13, y+4, 28, 14), $"{h:00}h",
                        Style(9, LabelColor, anchor: TextAnchor.UpperCenter));
            }
            y += 20f;
        }

        // ── Compact timeline (no icons / ticks) for inline day rows ──
        public static void DrawTimelineCompact(float pad, float trackW, ref float y,
                List<(DateTime start, DateTime end)> sessions) {
            float trackH    = 18f;
            int   numSlices = 120;

            for (int i = 0; i < numSlices; i++) {
                float t0   = i / (float)numSlices;
                float t1   = (i + 1) / (float)numSlices;
                float hour = t0 * 24f;
                EditorGUI.DrawRect(new Rect(pad + t0 * trackW, y, (t1 - t0) * trackW + 1f, trackH),
                    TimelineColorAt(hour));
            }

            EditorGUI.DrawRect(new Rect(pad, y,              trackW, 1), new Color(1f,1f,1f,0.08f));
            EditorGUI.DrawRect(new Rect(pad, y + trackH - 1, trackW, 1), new Color(1f,1f,1f,0.08f));

            var sc = T.GetSession();
            foreach (var (start, end) in sessions) {
                float x0 = pad + (float)(start.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float x1 = pad + (float)(end.TimeOfDay.TotalMinutes   / 1440.0) * trackW;
                float w  = Mathf.Max(x1 - x0, 3f);
                EditorGUI.DrawRect(new Rect(x0-1, y-1, w+2, trackH+2), new Color(sc.r, sc.g, sc.b, 0.18f));
                EditorGUI.DrawRect(new Rect(x0,   y,   w,   trackH),   new Color(sc.r, sc.g, sc.b, 0.55f));
                EditorGUI.DrawRect(new Rect(x0,   y,   w,   2),        new Color(1f, 1f, 1f, 0.35f));
            }

            // "Now" marker — only for today (sessions within last gap minutes)
            bool hasLive = sessions.Count > 0 &&
                (DateTime.Now - sessions.Last().end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            if (hasLive) {
                float nowX = pad + (float)(DateTime.Now.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                EditorGUI.DrawRect(new Rect(nowX - 1, y - 2, 2, trackH + 4), Color.white);
            }

            y += trackH + 4f;
        }

        public static void DrawBarChart(float pad, float trackW, ref float y,
                List<(DateTime date, double minutes)> dailyMinutes, string labelFormat = "ddd") {
            if (dailyMinutes == null || dailyMinutes.Count == 0) return;

            float  barAreaH = 64f;
            int    count    = dailyMinutes.Count;
            float  barW     = (trackW - (count - 1) * 4f) / count;
            double maxMins  = dailyMinutes.Max(d => d.minutes);
            if (maxMins <= 0) maxMins = 1;

            EditorGUI.DrawRect(new Rect(pad, y, trackW, barAreaH + 30), BgDark);

            for (int i = 0; i < count; i++) {
                var (date, mins) = dailyMinutes[i];
                float bx      = pad + i * (barW + 4);
                float bh      = (float)(mins / maxMins) * (barAreaH - 10);
                float by      = y + barAreaH - bh;
                bool  isToday = date.Date == DateTime.Today;

                EditorGUI.DrawRect(new Rect(bx, y+4, barW, barAreaH-10), new Color(1f,1f,1f,0.04f));
                if (mins > 0)
                    EditorGUI.DrawRect(new Rect(bx, by, barW, bh), isToday ? AccentColor : SessionDim);

                GUI.Label(new Rect(bx, y+barAreaH-2, barW, 14),
                    date.ToString(labelFormat).ToUpper(), Style(9, LabelColor, anchor: TextAnchor.UpperCenter));
                if (mins > 15)
                    GUI.Label(new Rect(bx, by-14, barW, 14),
                        $"{(int)(mins/60)}h", Style(8, TextColor, anchor: TextAnchor.UpperCenter));
                if (isToday)
                    EditorGUI.DrawRect(new Rect(bx, y+barAreaH+14, barW, 2), AccentColor);
            }
            y += barAreaH + 32f;
        }

        public static void DrawSessionRow(float pad, float trackW, ref float y,
                DateTime start, DateTime end, bool isLast) {
            bool   isLive = isLast && (DateTime.Now - end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            double mins   = (end - start).TotalMinutes;
            EditorGUI.DrawRect(new Rect(pad, y+5, 6, 6), isLive ? AccentColor : SessionDim);
            GUI.Label(new Rect(pad+14,  y, 130, 18), $"{start:HH:mm} → {end:HH:mm}", Style(11, TextColor));
            GUI.Label(new Rect(pad+150, y,  80, 18), TimeTrackerCore.FormatDuration(mins),
                Style(11, isLive ? TextColor : LabelColor));
            if (isLive)
                GUI.Label(new Rect(pad+240, y+2, 50, 14), "● LIVE", Style(9, AccentColor, FontStyle.Bold));
            y += 20f;
        }

        public static void DrawStatsGrid(float pad, float trackW, ref float y, PeriodStats ps) {
            float cardW = (trackW - 8) / 2f;
            float cardH = 52f;

            DrawStatCard(new Rect(pad,         y, cardW, cardH), "AVG / DAY",         TimeTrackerCore.FormatDuration(ps.avgMinutesPerDay));
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "AVG / ACTIVE DAY",  TimeTrackerCore.FormatDuration(ps.avgMinutesPerActiveDay));
            y += cardH + 8f;

            string longestSub = ps.longestSession.start != default ? ps.longestSession.start.ToString("ddd dd HH:mm") : "";
            DrawStatCard(new Rect(pad,         y, cardW, cardH), "LONGEST SESSION",  TimeTrackerCore.FormatDuration(ps.longestSessionMinutes), longestSub);
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "SHORTEST SESSION", TimeTrackerCore.FormatDuration(ps.shortestSessionMinutes));
            y += cardH + 8f;

            string earliest = ps.earliestStartMinutes >= 0 ? $"{(int)(ps.earliestStartMinutes/60):00}:{(int)(ps.earliestStartMinutes%60):00}" : "--";
            string latest   = ps.latestEndMinutes    >= 0 ? $"{(int)(ps.latestEndMinutes   /60):00}:{(int)(ps.latestEndMinutes   %60):00}" : "--";
            DrawStatCard(new Rect(pad,         y, cardW, cardH), "EARLIEST START", earliest);
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "LATEST END",     latest);
            y += cardH + 8f;
        }
    }
}
