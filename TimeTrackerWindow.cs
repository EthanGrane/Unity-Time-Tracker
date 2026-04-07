// TimeTrackerWindow.cs — main window with tabs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {

    public class TimeTrackerWindow : EditorWindow {

        // ── Tabs ─────────────────────────────────────────────────────
        enum Tab { Today, Week, Month, AllTime, Settings }

        Tab selectedTab = Tab.Today;
        static readonly string[] TAB_LABELS = { "TODAY", "WEEK", "MONTH", "ALL TIME", "⚙" };

        // ── State ────────────────────────────────────────────────────
        TimeTrackingData data;

        // Week picker  (0 = current week, -1 = last week, etc.)
        int weekOffset = 0;

        // Month picker
        int monthYear  = DateTime.Today.Year;
        int monthMonth = DateTime.Today.Month;

        // Day inspector  (null = none selected)
        DateTime? inspectedDay = null;

        static readonly string[] MONTH_NAMES = {
            "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"
        };

        // Scroll positions
        Vector2 weekScroll;
        Vector2 monthScroll;
        Vector2 settingsScroll;

        [MenuItem("Tools/Time Tracker")]
        public static void Open() {
            var w = GetWindow<TimeTrackerWindow>("Time Tracker");
            w.minSize = new Vector2(460, 400);
        }

        void OnEnable()  { TimeTrackerSettings.Load(); Refresh(); }
        void OnFocus()   => Refresh();

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
            float y   = 16f;

            // ── Header ──────────────────────────────────────────────
            GUI.Label(new Rect(pad, y, 300, 30), "TIME TRACKER",
                TimeTrackerGUI.Style(20, TimeTrackerGUI.TextColor, FontStyle.Bold));

            if (selectedTab != Tab.Settings) {
                if (GUI.Button(new Rect(position.width - pad - 64, y + 4, 64, 20), "↻ refresh",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10,
                            normal = { textColor = TimeTrackerGUI.LabelColor }
                        }))
                    Refresh();
            }

            y += 38f;

            // ── Tabs ────────────────────────────────────────────────
            DrawTabs(pad, ref y);
            y += 12f;

            // ── Content ─────────────────────────────────────────────
            switch (selectedTab) {
                case Tab.Today:    DrawToday(pad, ref y);    break;
                case Tab.Week:     DrawWeek(pad, ref y);     break;
                case Tab.Month:    DrawMonth(pad, ref y);    break;
                case Tab.AllTime:  DrawAllTime(pad, ref y);  break;
                case Tab.Settings: DrawSettings(pad, ref y); break;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  TABS BAR
        // ════════════════════════════════════════════════════════════
        void DrawTabs(float pad, ref float y) {
            int   mainCount = TAB_LABELS.Length - 1;
            float gearW     = 30f;
            float gap       = 6f;
            float mainW     = (position.width - pad * 2 - (mainCount - 1) * gap - gap - gearW) / mainCount;
            float tabH      = 26f;

            for (int i = 0; i < TAB_LABELS.Length; i++) {
                bool  active = (int)selectedTab == i;
                float tw     = i < mainCount ? mainW : gearW;
                float tx     = pad + (i < mainCount
                    ? i * (mainW + gap)
                    : mainCount * (mainW + gap));

                Rect r = new Rect(tx, y, tw, tabH);
                EditorGUI.DrawRect(r, active ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.BgDark);
                GUI.Label(r, TAB_LABELS[i], TimeTrackerGUI.Style(
                    i < mainCount ? 11 : 14,
                    active ? TimeTrackerGUI.BgColor : TimeTrackerGUI.LabelColor,
                    FontStyle.Bold, TextAnchor.MiddleCenter));

                if (!active && GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                    selectedTab = (Tab)i;
                    inspectedDay = null;
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
        //  WEEK  (with per-day timelines + week navigation)
        // ════════════════════════════════════════════════════════════
        void DrawWeek(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            float scrollH = position.height - y - 8f;

            // ── Week picker ──────────────────────────────────────────
            var navStyle = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 14,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            float pickerY = y;
            if (GUI.Button(new Rect(pad, pickerY, 22, 22), "‹", navStyle)) {
                weekOffset--;
                Repaint();
            }
            if (GUI.Button(new Rect(pad + 28, pickerY, 22, 22), "›", navStyle)) {
                if (weekOffset < 0) weekOffset++;
                Repaint();
            }
            // "Today" jump button
            if (weekOffset != 0) {
                if (GUI.Button(new Rect(pad + 60, pickerY, 50, 22), "today",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10,
                            normal   = { textColor = TimeTrackerGUI.AccentColor }
                        })) {
                    weekOffset = 0;
                    Repaint();
                }
            }

            DateTime refDay     = DateTime.Today.AddDays(weekOffset * 7);
            var (from, to)      = TimeTrackerCore.GetWeekRange(refDay);
            bool isCurrentWeek  = weekOffset == 0;

            string weekLabel = isCurrentWeek
                ? $"This week  ·  {from:dd MMM} – {to:dd MMM yyyy}"
                : $"{from:dd MMM} – {to:dd MMM yyyy}";
            GUI.Label(new Rect(pad + (weekOffset != 0 ? 120 : 60), pickerY + 3, 260, 18), weekLabel,
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));

            y += 34f;

            // ── Stats header ─────────────────────────────────────────
            var ps = TimeTrackerCore.ComputeStats(data, from, to);
            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"{ps.activeDays}/7 days active  ·  {ps.totalSessions} sessions");

            // ── Scroll area for 7-day timelines ─────────────────────
            // Estimate content height: bar chart + 7 day rows
            float dayRowH  = 110f; // per day: label + timeline + sessions mini
            float contentH = 100f + 7 * dayRowH + 200f; // barchart + days + stats

            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, contentH);
            weekScroll      = GUI.BeginScrollView(scrollView, weekScroll, content);

            float cw = content.width;
            float sy = 0f;

            // ── Bar chart (compact) ───────────────────────────────────
            TimeTrackerGUI.DrawBarChart(0, cw, ref sy, ps.dailyMinutes, "ddd");

            // ── Divider ───────────────────────────────────────────────
            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "DAILY TIMELINES",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;

            // ── One row per day ───────────────────────────────────────
            string[] dayNames = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
            for (int d = 0; d < 7; d++) {
                DateTime day      = from.AddDays(d);
                bool     isToday  = day.Date == DateTime.Today;
                var      daySess  = TimeTrackerCore.GetSessionsForDate(data, day);
                double   dayMins  = daySess.Sum(s => (s.end - s.start).TotalMinutes);

                // Day header
                Color dayLabelCol = isToday ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.TextColor;
                string dayHeader  = $"{dayNames[d]}  {day:dd MMM}";
                string dayTotal   = dayMins > 0 ? TimeTrackerCore.FormatDuration(dayMins) : "—";

                GUI.Label(new Rect(0, sy, cw - 80, 16), dayHeader,
                    TimeTrackerGUI.Style(11, dayLabelCol, isToday ? FontStyle.Bold : FontStyle.Normal));
                GUI.Label(new Rect(cw - 80, sy, 80, 16), dayTotal,
                    TimeTrackerGUI.Style(11, isToday ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.LabelColor,
                        anchor: TextAnchor.UpperRight));

                if (isToday)
                    EditorGUI.DrawRect(new Rect(0, sy + 15, cw, 1),
                        new Color(TimeTrackerGUI.AccentColor.r, TimeTrackerGUI.AccentColor.g,
                                  TimeTrackerGUI.AccentColor.b, 0.3f));
                sy += 20f;

                // Timeline for this day (compact: no icons below)
                TimeTrackerGUI.DrawTimelineCompact(0, cw, ref sy, daySess);

                // Sessions mini-list (only if sessions exist, up to 4)
                if (daySess.Count > 0) {
                    sy += 4f;
                    int showCount = Mathf.Min(daySess.Count, 4);
                    for (int si = 0; si < showCount; si++) {
                        var (ss, se) = daySess[si];
                        bool isLast = si == daySess.Count - 1;
                        TimeTrackerGUI.DrawSessionRow(0, cw, ref sy, ss, se, isLast && isToday);
                    }
                    if (daySess.Count > 4) {
                        GUI.Label(new Rect(0, sy, cw, 14),
                            $"  + {daySess.Count - 4} more sessions",
                            TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                        sy += 16f;
                    }
                }

                sy += 10f;

                // Separator between days (not after last)
                if (d < 6) {
                    EditorGUI.DrawRect(new Rect(0, sy, cw, 1), TimeTrackerGUI.DivColor);
                    sy += 10f;
                }
            }

            // ── Stats grid ────────────────────────────────────────────
            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "AVERAGES & RECORDS",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;
            TimeTrackerGUI.DrawStatsGrid(0, cw, ref sy, ps);

            GUI.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════
        //  MONTH  (with day inspector)
        // ════════════════════════════════════════════════════════════
        void DrawMonth(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            float scrollH = position.height - y - 8f;

            // ── Month picker ─────────────────────────────────────────
            float pickerY = y;
            var navStyle = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 14,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            if (GUI.Button(new Rect(pad, pickerY, 22, 22), "‹", navStyle)) {
                monthMonth--;
                if (monthMonth < 1) { monthMonth = 12; monthYear--; }
                inspectedDay = null;
                Repaint();
            }
            if (GUI.Button(new Rect(pad + 28, pickerY, 22, 22), "›", navStyle)) {
                monthMonth++;
                if (monthMonth > 12) { monthMonth = 1; monthYear++; }
                inspectedDay = null;
                Repaint();
            }
            // Jump to current month
            bool isCurrentMonth = monthYear == DateTime.Today.Year && monthMonth == DateTime.Today.Month;
            if (!isCurrentMonth) {
                if (GUI.Button(new Rect(pad + 60, pickerY, 50, 22), "today",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10,
                            normal   = { textColor = TimeTrackerGUI.AccentColor }
                        })) {
                    monthYear  = DateTime.Today.Year;
                    monthMonth = DateTime.Today.Month;
                    inspectedDay = null;
                    Repaint();
                }
            }

            string monthLabel = $"{MONTH_NAMES[monthMonth - 1]} {monthYear}";
            GUI.Label(new Rect(pad + (!isCurrentMonth ? 120 : 60), pickerY + 3, 160, 18), monthLabel,
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));
            y += 34f;

            var (from, to)   = TimeTrackerCore.GetMonthRange(monthYear, monthMonth);
            int daysInMonth  = (int)(to - from).TotalDays + 1;
            var ps           = TimeTrackerCore.ComputeStats(data, from, to);

            // ── If a day is being inspected ───────────────────────────
            if (inspectedDay.HasValue) {
                DrawDayInspector(pad, trackW, ref y, inspectedDay.Value, scrollH);
                return;
            }

            // ── Normal month view ─────────────────────────────────────
            float contentH = 80f + 100f + 200f + 200f; // header + chart + calendar grid + stats
            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, contentH);
            monthScroll     = GUI.BeginScrollView(scrollView, monthScroll, content);

            float cw = content.width;
            float sy = 0f;

            DrawBigNumber(0, cw, ref sy, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"{monthLabel}  ·  {ps.activeDays}/{daysInMonth} days active  ·  {ps.totalSessions} sessions");

            // Bar chart by week
            if (daysInMonth <= 14)
                TimeTrackerGUI.DrawBarChart(0, cw, ref sy, ps.dailyMinutes, "dd");
            else
                TimeTrackerGUI.DrawBarChart(0, cw, ref sy, GroupByWeek(ps.dailyMinutes, from), "dd/MM");

            // ── Clickable day grid ────────────────────────────────────
            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "TAP A DAY TO INSPECT",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;

            DrawMonthDayGrid(0, cw, ref sy, ps, from, daysInMonth);

            // Stats
            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "AVERAGES & RECORDS",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;
            TimeTrackerGUI.DrawStatsGrid(0, cw, ref sy, ps);

            GUI.EndScrollView();
        }

        // ── Clickable calendar day grid ───────────────────────────────
        void DrawMonthDayGrid(float x, float cw, ref float sy,
                PeriodStats ps, DateTime from, int daysInMonth) {

            string[] dayLabels = { "M", "T", "W", "T", "F", "S", "S" };
            float cellW  = (cw - 6 * 4) / 7f;
            float cellH  = 44f;
            float gap    = 4f;

            // Header row
            for (int col = 0; col < 7; col++) {
                GUI.Label(new Rect(x + col * (cellW + gap), sy, cellW, 14),
                    dayLabels[col],
                    TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor,
                        anchor: TextAnchor.UpperCenter));
            }
            sy += 16f;

            // First day column offset (Monday = 0)
            int firstDow = (int)from.DayOfWeek;
            int startCol = firstDow == 0 ? 6 : firstDow - 1;

            double maxDayMins = ps.dailyMinutes.Count > 0
                ? ps.dailyMinutes.Max(d => d.minutes) : 1;
            if (maxDayMins <= 0) maxDayMins = 1;

            int col2 = startCol;
            float rowY = sy;

            for (int d = 0; d < daysInMonth; d++) {
                DateTime day    = from.AddDays(d);
                double   mins   = ps.dailyMinutes[d].minutes;
                bool     today  = day.Date == DateTime.Today;
                bool     future = day.Date > DateTime.Today;

                float cx = x + col2 * (cellW + gap);

                // Background cell
                Color cellBg = today
                    ? new Color(TimeTrackerGUI.AccentColor.r, TimeTrackerGUI.AccentColor.g,
                                TimeTrackerGUI.AccentColor.b, 0.15f)
                    : TimeTrackerGUI.BgCard;
                EditorGUI.DrawRect(new Rect(cx, rowY, cellW, cellH), cellBg);

                // Activity fill bar at bottom
                if (mins > 0 && !future) {
                    float fillH = Mathf.Max(2f, (float)(mins / maxDayMins) * (cellH - 2));
                    Color fillCol = today ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.SessionDim;
                    EditorGUI.DrawRect(new Rect(cx, rowY + cellH - fillH, cellW, fillH), fillCol);
                }

                // Day number
                Color numCol = today ? TimeTrackerGUI.AccentColor
                    : (future ? TimeTrackerGUI.LabelColor : TimeTrackerGUI.TextColor);
                GUI.Label(new Rect(cx + 2, rowY + 3, cellW - 4, 14),
                    day.Day.ToString(),
                    TimeTrackerGUI.Style(10, numCol,
                        today ? FontStyle.Bold : FontStyle.Normal,
                        TextAnchor.UpperCenter));

                // Duration label
                if (mins > 0) {
                    GUI.Label(new Rect(cx + 1, rowY + cellH - 16, cellW - 2, 13),
                        TimeTrackerCore.FormatDuration(mins),
                        TimeTrackerGUI.Style(8, TimeTrackerGUI.TextColor,
                            anchor: TextAnchor.UpperCenter));
                }

                // Clickable overlay
                if (!future && GUI.Button(new Rect(cx, rowY, cellW, cellH),
                        GUIContent.none, GUIStyle.none)) {
                    inspectedDay = day;
                    Repaint();
                }

                // Today border
                if (today)
                    DrawCellBorder(cx, rowY, cellW, cellH, TimeTrackerGUI.AccentColor);

                col2++;
                if (col2 >= 7) {
                    col2 = 0;
                    rowY += cellH + gap;
                }
            }

            // Advance sy past the grid
            int totalCells = startCol + daysInMonth;
            int rows       = (int)Math.Ceiling(totalCells / 7.0);
            sy += rows * (cellH + gap) + 10f;
        }

        // ── Day inspector (month → day drill-down) ────────────────────
        void DrawDayInspector(float pad, float trackW, ref float y,
                DateTime day, float scrollH) {

            // Back button
            if (GUI.Button(new Rect(pad, y, 22, 22), "‹",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 14,
                        normal   = { textColor = TimeTrackerGUI.TextColor }
                    })) {
                inspectedDay = null;
                Repaint();
                return;
            }

            // Day navigation (prev / next)
            if (GUI.Button(new Rect(pad + 28, y, 22, 22), "›",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 14,
                        normal   = { textColor = TimeTrackerGUI.TextColor }
                    })) {
                DateTime next = day.AddDays(1);
                if (next.Date <= DateTime.Today) {
                    inspectedDay = next;
                    if (next.Month != monthMonth || next.Year != monthYear) {
                        monthMonth = next.Month;
                        monthYear  = next.Year;
                    }
                }
                Repaint();
                return;
            }
            // Prev day
            Rect prevR = new Rect(pad - 2, y, 22, 22); // re-use left side
            // We already drew ‹ as "back to month". For prev/next inside inspector,
            // let's put them to the right of the date label.
            float labelX = pad + 56;
            GUI.Label(new Rect(labelX, y + 3, 200, 18),
                day.ToString("dddd, dd MMM yyyy"),
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));

            // Prev / next arrows beside the label
            float arrowX = labelX + 210;
            if (day.Date > DateTime.MinValue.Date) {
                if (GUI.Button(new Rect(arrowX, y, 22, 22), "←",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 11,
                            normal   = { textColor = TimeTrackerGUI.LabelColor }
                        })) {
                    inspectedDay = day.AddDays(-1);
                    DateTime nd = inspectedDay.Value;
                    if (nd.Month != monthMonth || nd.Year != monthYear) {
                        monthMonth = nd.Month; monthYear = nd.Year;
                    }
                    Repaint();
                    return;
                }
            }
            if (day.Date < DateTime.Today) {
                if (GUI.Button(new Rect(arrowX + 28, y, 22, 22), "→",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 11,
                            normal   = { textColor = TimeTrackerGUI.LabelColor }
                        })) {
                    inspectedDay = day.AddDays(1);
                    DateTime nd = inspectedDay.Value;
                    if (nd.Month != monthMonth || nd.Year != monthYear) {
                        monthMonth = nd.Month; monthYear = nd.Year;
                    }
                    Repaint();
                    return;
                }
            }

            y += 34f;

            var sessions = TimeTrackerCore.GetSessionsForDate(data, day);
            double total = sessions.Sum(s => (s.end - s.start).TotalMinutes);
            bool   isToday = day.Date == DateTime.Today;

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(total),
                $"{sessions.Count} session{(sessions.Count != 1 ? "s" : "")} · {day:dddd}");

            // Timeline
            TimeTrackerGUI.DrawTimeline(pad, trackW, ref y, sessions);
            y += 12f;

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "SESSIONS");
            y += 20f;

            if (sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No sessions recorded",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor,
                        anchor: TextAnchor.MiddleCenter));
            } else {
                foreach (var (i, s) in sessions.Select((s, i) => (i, s)))
                    TimeTrackerGUI.DrawSessionRow(pad, trackW, ref y,
                        s.start, s.end, isToday && i == sessions.Count - 1);
            }
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

            DateTime first = data.sessions
                .Select(s => DateTime.TryParse(s.start, out var d) ? d : DateTime.MaxValue).Min();
            DateTime last     = DateTime.Today;
            int      totalDays = (int)(last - first).TotalDays + 1;
            var ps = TimeTrackerCore.ComputeStats(data, first, last);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"since {first:dd MMM yyyy}  ·  {ps.activeDays}/{totalDays} days active  ·  {ps.totalSessions} sessions");

            var monthly = GroupByMonth(ps.dailyMinutes);
            TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, monthly, "MMM");
            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "AVERAGES & RECORDS");
            y += 20f;
            TimeTrackerGUI.DrawStatsGrid(pad, trackW, ref y, ps);
        }

        // ════════════════════════════════════════════════════════════
        //  SETTINGS
        // ════════════════════════════════════════════════════════════
        void DrawSettings(float pad, ref float y) {
            float trackW   = position.width - pad * 2;
            float scrollH  = position.height - y - 8f;
            var   theme    = TimeTrackerSettings.Current;
            bool  changed  = false;

            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, 860f);
            settingsScroll  = GUI.BeginScrollView(scrollView, settingsScroll, content);

            float sy = 0f;
            float cw = content.width;

            // ── Section: General colors ──────────────────────────────
            DrawSettingsHeader(0, sy, cw, "GENERAL COLORS");
            sy += 24f;

            changed |= ColorRow(0, ref sy, cw, "Accent color",          theme.GetAccent(),  c => theme.SetAccent(c));
            changed |= ColorRow(0, ref sy, cw, "Background",            theme.GetBg(),      c => theme.SetBg(c));
            changed |= ColorRow(0, ref sy, cw, "Background (dark)",     theme.GetBgDark(),  c => theme.SetBgDark(c));
            changed |= ColorRow(0, ref sy, cw, "Text",                  theme.GetText(),    c => theme.SetText(c));
            changed |= ColorRow(0, ref sy, cw, "Session bars",          theme.GetSession(), c => theme.SetSession(c));

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            // ── Section: Timeline colors ─────────────────────────────
            DrawSettingsHeader(0, sy, cw, "TIMELINE — ZONE COLORS");
            sy += 24f;

            changed |= ColorRow(0, ref sy, cw, "Off-hours color",  theme.GetOff(),  c => theme.SetOff(c));
            changed |= ColorRow(0, ref sy, cw, "Work-hours color", theme.GetWork(), c => theme.SetWork(c));
            changed |= ColorRow(0, ref sy, cw, "Icon color A (moon / 💤)", theme.GetMoon(), c => theme.SetMoon(c));
            changed |= ColorRow(0, ref sy, cw, "Icon color B (sun / 👷)",  theme.GetSun(),  c => theme.SetSun(c));

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            // ── Section: Transition hours ────────────────────────────
            DrawSettingsHeader(0, sy, cw, "TIMELINE — TRANSITION HOURS");
            sy += 24f;

            changed |= HourSlider(0, ref sy, cw, "Work starts", ref theme.workStartHour, 0f, 24f);
            changed |= HourSlider(0, ref sy, cw, "Work ends",   ref theme.workEndHour,   0f, 24f);

            if (theme.workEndHour <= theme.workStartHour)
                theme.workEndHour = theme.workStartHour + 0.25f;
            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            // ── Section: Timeline icons ──────────────────────────────
            DrawSettingsHeader(0, sy, cw, "TIMELINE — ICONS");
            sy += 24f;

            GUI.Label(new Rect(0, sy, cw - 160f, 18), "Icon style",
                TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
            TimelineIconStyle newStyle = (TimelineIconStyle)EditorGUI.EnumPopup(
                new Rect(cw - 156f, sy, 156f, 18), theme.IconStyle);
            if (newStyle != theme.IconStyle) { theme.IconStyle = newStyle; changed = true; }
            sy += 26f;

            string previewLabel = theme.IconStyle == TimelineIconStyle.SunMoon
                ? "🌙 moon (off hours)  ·  ☀ sun (work hours)"
                : "💤 zzz (off hours)  ·  👷 worker (work hours)";
            GUI.Label(new Rect(0, sy, cw, 16), previewLabel,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 22f;

            bool newOff = GUI.Toggle(new Rect(0, sy, 220, 20), theme.showOffIcon,
                "  Show off-hours icon", new GUIStyle(EditorStyles.toggle) {
                    normal   = { textColor = TimeTrackerGUI.TextColor },
                    fontSize = 11
                });
            if (newOff != theme.showOffIcon) { theme.showOffIcon = newOff; changed = true; }
            sy += 26f;

            bool newWork = GUI.Toggle(new Rect(0, sy, 220, 20), theme.showWorkIcon,
                "  Show work-hours icon", new GUIStyle(EditorStyles.toggle) {
                    normal   = { textColor = TimeTrackerGUI.TextColor },
                    fontSize = 11
                });
            if (newWork != theme.showWorkIcon) { theme.showWorkIcon = newWork; changed = true; }
            sy += 30f;

            // ── Preview ──────────────────────────────────────────────
            DrawSettingsDivider(0, ref sy, cw);
            DrawSettingsHeader(0, sy, cw, "PREVIEW");
            sy += 24f;

            var previewSessions = new List<(DateTime, DateTime)> {
                (DateTime.Today.AddHours(9), DateTime.Today.AddHours(11.5))
            };
            TimeTrackerGUI.DrawTimeline(0, cw, ref sy, previewSessions);

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            // ── Buttons ──────────────────────────────────────────────
            float btnW = (cw - 8f) / 2f;
            GUIStyle btnStyle = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 11,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            if (GUI.Button(new Rect(0, sy, btnW, 26), "💾  Save changes", btnStyle)) {
                TimeTrackerSettings.Save();
                Repaint();
            }
            if (GUI.Button(new Rect(btnW + 8f, sy, btnW, 26), "↺  Reset to defaults", btnStyle)) {
                TimeTrackerSettings.Reset();
                Repaint();
            }
            sy += 34f;

            GUI.EndScrollView();

            if (changed) Repaint();
        }

        // ── Settings helpers ─────────────────────────────────────────

        void DrawSettingsHeader(float x, float y, float w, string text) {
            GUI.Label(new Rect(x, y, w, 18), text,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
        }

        void DrawSettingsDivider(float x, ref float y, float w) {
            EditorGUI.DrawRect(new Rect(x, y, w, 1), TimeTrackerGUI.DivColor);
            y += 14f;
        }

        bool ColorRow(float x, ref float y, float w, string label, Color current, System.Action<Color> setter) {
            GUI.Label(new Rect(x, y + 2, w - 60, 18),
                label, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
            Color next = EditorGUI.ColorField(new Rect(x + w - 56, y, 56, 18), current);
            y += 26f;
            if (next != current) { setter(next); return true; }
            return false;
        }

        bool HourSlider(float x, ref float y, float w, string label, ref float value, float min, float max) {
            GUI.Label(new Rect(x, y, w - 80, 18),
                label, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

            int   h    = (int)value;
            int   m    = (int)((value - h) * 60);
            string lbl = $"{h:00}:{m:00}";
            GUI.Label(new Rect(x + w - 76, y, 36, 18),
                lbl, TimeTrackerGUI.Style(11, TimeTrackerGUI.AccentColor, anchor: TextAnchor.UpperRight));

            float next = GUI.HorizontalSlider(new Rect(x, y + 18, w - 40, 14), value, min, max);
            next = Mathf.Round(next * 4f) / 4f;
            y += 38f;

            if (Mathf.Abs(next - value) > 0.01f) { value = next; return true; }
            return false;
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void DrawBigNumber(float pad, float trackW, ref float y, string value, string sub) {
            GUI.Label(new Rect(pad, y,      trackW, 36), value,
                TimeTrackerGUI.Style(26, TimeTrackerGUI.AccentColor, FontStyle.Bold));
            GUI.Label(new Rect(pad, y + 30, trackW, 18), sub,
                TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor));
            y += 56f;
        }

        void DrawInlineDiv(float x, float w, ref float sy) {
            EditorGUI.DrawRect(new Rect(x, sy, w, 1), TimeTrackerGUI.DivColor);
            sy += 12f;
        }

        void DrawCellBorder(float cx, float cy, float cw, float ch, Color col) {
            EditorGUI.DrawRect(new Rect(cx,          cy,          cw, 1),  col);
            EditorGUI.DrawRect(new Rect(cx,          cy + ch - 1, cw, 1),  col);
            EditorGUI.DrawRect(new Rect(cx,          cy,          1,  ch), col);
            EditorGUI.DrawRect(new Rect(cx + cw - 1, cy,          1,  ch), col);
        }

        static List<(DateTime date, double minutes)> GroupByWeek(
                List<(DateTime date, double minutes)> daily, DateTime monthStart) {
            var weeks = new List<(DateTime, double)>();
            int i = 0;
            while (i < daily.Count) {
                double   sum       = 0;
                DateTime weekLabel = daily[i].date;
                int      j         = 0;
                while (j < 7 && i + j < daily.Count) { sum += daily[i + j].minutes; j++; }
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
