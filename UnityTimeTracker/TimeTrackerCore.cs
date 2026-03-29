// TimeTrackerCore.cs — modelos, carga/guardado, cálculos
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// ── Modelos ──────────────────────────────────────────────────────────────────
namespace UnityTimeTracker {

    [Serializable]
    public class WorkSession {
        public string start;
        public string lastCheck;
    }

    [Serializable]
    public class TimeTrackingData {
        public List<WorkSession> sessions = new List<WorkSession>();
    }

    public struct PeriodStats {
        public double totalMinutes;
        public double avgMinutesPerDay;
        public double avgMinutesPerActiveDay;
        public double longestSessionMinutes;
        public double shortestSessionMinutes;
        public int activeDays;
        public int totalSessions;
        public List<(DateTime date, double minutes)> dailyMinutes;
        public (DateTime start, DateTime end) longestSession;
        public double earliestStartMinutes;
        public double latestEndMinutes;
    }

// ── Core estático ─────────────────────────────────────────────────────────────

    public static class TimeTrackerCore {
        public const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
        public const double SESSION_GAP_MINUTES = 15;

        public static string FilePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TimeTracking.json")
        );

        // ── Carga / Guardado ─────────────────────────────────────────

        public static TimeTrackingData LoadData() {
            if (!File.Exists(FilePath)) return new TimeTrackingData();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new TimeTrackingData();
            return JsonUtility.FromJson<TimeTrackingData>(json) ?? new TimeTrackingData();
        }

        public static void SaveData(TimeTrackingData data) {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
        }

        // ── Queries ──────────────────────────────────────────────────

        public static List<(DateTime start, DateTime end)> GetSessionsForDate(TimeTrackingData data, DateTime date) {
            var result = new List<(DateTime, DateTime)>();
            if (data == null) return result;

            foreach (var session in data.sessions) {
                if (!DateTime.TryParse(session.start, out DateTime start)) continue;
                if (start.Date != date.Date) continue;

                DateTime end = DateTime.Now;
                if (!string.IsNullOrEmpty(session.lastCheck) &&
                    DateTime.TryParse(session.lastCheck, out DateTime lc))
                    end = lc;

                result.Add((start, end));
            }
            return result;
        }

        public static List<(DateTime start, DateTime end)> GetSessionsInRange(TimeTrackingData data, DateTime from, DateTime to) {
            var result = new List<(DateTime, DateTime)>();
            if (data == null) return result;

            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                result.AddRange(GetSessionsForDate(data, d));

            return result;
        }

        // ── Stats ────────────────────────────────────────────────────

        public static PeriodStats ComputeStats(TimeTrackingData data, DateTime from, DateTime to) {
            var ps = new PeriodStats();
            ps.dailyMinutes = new List<(DateTime, double)>();
            ps.longestSessionMinutes = 0;
            ps.shortestSessionMinutes = double.MaxValue;
            ps.earliestStartMinutes = double.MaxValue;
            ps.latestEndMinutes = -1;

            int totalDays = (int)(to.Date - from.Date).TotalDays + 1;

            for (int d = 0; d < totalDays; d++) {
                DateTime day = from.Date.AddDays(d);
                var sessions = GetSessionsForDate(data, day);
                double dayMins = sessions.Sum(s => (s.end - s.start).TotalMinutes);

                ps.dailyMinutes.Add((day, dayMins));
                ps.totalMinutes += dayMins;
                if (dayMins > 0) ps.activeDays++;

                foreach (var (start, end) in sessions) {
                    double mins = (end - start).TotalMinutes;
                    ps.totalSessions++;

                    if (mins > ps.longestSessionMinutes) {
                        ps.longestSessionMinutes = mins;
                        ps.longestSession = (start, end);
                    }
                    if (mins < ps.shortestSessionMinutes)
                        ps.shortestSessionMinutes = mins;

                    double sm = start.TimeOfDay.TotalMinutes;
                    double em = end.TimeOfDay.TotalMinutes;
                    if (sm < ps.earliestStartMinutes) ps.earliestStartMinutes = sm;
                    if (em > ps.latestEndMinutes) ps.latestEndMinutes = em;
                }
            }

            ps.avgMinutesPerDay = ps.totalMinutes / totalDays;
            ps.avgMinutesPerActiveDay = ps.activeDays > 0 ? ps.totalMinutes / ps.activeDays : 0;

            if (ps.shortestSessionMinutes == double.MaxValue) ps.shortestSessionMinutes = 0;
            if (ps.earliestStartMinutes == double.MaxValue) ps.earliestStartMinutes = -1;

            return ps;
        }

        // ── Helpers ──────────────────────────────────────────────────

        public static string FormatDuration(double minutes) {
            int h = (int)(minutes / 60);
            int m = (int)(minutes % 60);
            return h > 0 ? $"{h}h {m:00}m" : $"{m}m";
        }

        public static (DateTime from, DateTime to) GetWeekRange(DateTime reference) {
            int dow = (int)reference.DayOfWeek;
            int toMon = (dow == 0) ? 6 : dow - 1;
            DateTime mon = reference.Date.AddDays(-toMon);
            return (mon, mon.AddDays(6));
        }

        public static (DateTime from, DateTime to) GetMonthRange(int year, int month) {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            return (from, to);
        }
    }
}