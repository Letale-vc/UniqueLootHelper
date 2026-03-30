using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace UniqueLootHelper.Managers;

public class Profiler
{
    private const int MaxHistorySize = 100;
    private const int UpdateInterval = 30; // Update UI every 30 frames (~0.5 sec at 60 FPS)
    private static readonly List<ProfilerEntry> _profilerHistory = new();
    private static int _frameCounter = 0;
    private static List<ProfilerEntry> _cachedRecentEntries = new();

    public static void RecordMetrics(
        Stopwatch getItems,
        Stopwatch filtering,
        Stopwatch drawing,
        Stopwatch total
    )
    {
        var entry = new ProfilerEntry
        {
            Timestamp = DateTime.Now,
            GetItemsTicks = getItems?.ElapsedTicks ?? 0,
            FilteringTicks = filtering?.ElapsedTicks ?? 0,
            DrawingTicks = drawing?.ElapsedTicks ?? 0,
            TotalTicks = total?.ElapsedTicks ?? 0,
        };

        _profilerHistory.Add(entry);

        if (_profilerHistory.Count > MaxHistorySize)
        {
            _profilerHistory.RemoveAt(0);
        }
    }

    public static void ShowProfilerWindow(ref bool isOpen)
    {
        if (!isOpen)
        {
            return;
        }

        // Increment frame counter and check if we should update the UI
        _frameCounter++;
        bool shouldUpdateUI = _frameCounter >= UpdateInterval;
        if (shouldUpdateUI)
        {
            _frameCounter = 0;
        }

        ImGui.SetNextWindowSize(new Vector2(700, 500), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Performance Profiler", ref isOpen, ImGuiWindowFlags.None))
        {
            if (_profilerHistory.Count == 0)
            {
                ImGui.TextColored(
                    new Vector4(1, 1, 0, 1),
                    "No profiler data yet. Enable profiler in settings."
                );
                ImGui.End();
                return;
            }

            // Calculate averages
            var avgGetItems = _profilerHistory.Average(e => e.GetItemsMs);
            var avgFiltering = _profilerHistory.Average(e => e.FilteringMs);
            var avgDrawing = _profilerHistory.Average(e => e.DrawingMs);
            var avgTotal = _profilerHistory.Average(e => e.TotalMs);

            // Calculate max values
            var maxGetItems = _profilerHistory.Max(e => e.GetItemsMs);
            var maxFiltering = _profilerHistory.Max(e => e.FilteringMs);
            var maxDrawing = _profilerHistory.Max(e => e.DrawingMs);
            var maxTotal = _profilerHistory.Max(e => e.TotalMs);

            ImGui.Text(
                $"Samples: {_profilerHistory.Count} | Latest update: {_profilerHistory[^1].Timestamp:HH:mm:ss}"
            );
            ImGui.Separator();

            // Display summary table
            ImGui.Text("Performance Summary (Average / Max):");
            ImGui.Separator();

            if (
                ImGui.BeginTable(
                    "ProfilerSummary",
                    3,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                )
            )
            {
                ImGui.TableSetupColumn("Operation", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Average (ms)", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("Max (ms)", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableHeadersRow();

                AddTableRow("Get Ground Items", avgGetItems, maxGetItems);
                AddTableRow("Filtering & Processing", avgFiltering, maxFiltering);
                AddTableRow("Drawing Operations", avgDrawing, maxDrawing);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Total Render Time");
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"{avgTotal:F3}");
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"{maxTotal:F3}");

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"Recent Measurements (updates every {UpdateInterval} frames):");
            ImGui.Separator();

            // Update cached entries only every N frames
            if (shouldUpdateUI)
            {
                _cachedRecentEntries = _profilerHistory
                    .TakeLast(10) // Show only 10 entries instead of 20
                    .Reverse()
                    .ToList();
            }

            // Display recent history
            if (
                ImGui.BeginTable(
                    "ProfilerHistory",
                    6,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY
                )
            )
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Get Items", ImGuiTableColumnFlags.WidthFixed, 85);
                ImGui.TableSetupColumn("Filtering", ImGuiTableColumnFlags.WidthFixed, 85);
                ImGui.TableSetupColumn("Drawing", ImGuiTableColumnFlags.WidthFixed, 85);
                ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 85);
                ImGui.TableHeadersRow();

                // Use cached entries instead of calculating every frame
                foreach (var entry in _cachedRecentEntries)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.Timestamp.ToString("HH:mm:ss"));
                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.GetItemsMs:F3}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.FilteringMs:F3}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.DrawingMs:F3}");
                    ImGui.TableNextColumn();

                    // Highlight total if it's too slow (60 FPS = 16.67ms per frame)
                    if (entry.TotalMs > 16.0) // More than 16ms = drops below 60 FPS
                    {
                        ImGui.TextColored(
                            new System.Numerics.Vector4(1, 0, 0, 1),
                            $"{entry.TotalMs:F3}"
                        );
                    }
                    else if (entry.TotalMs > 10.0) // 10-16ms = acceptable
                    {
                        ImGui.TextColored(
                            new System.Numerics.Vector4(1, 1, 0, 1),
                            $"{entry.TotalMs:F3}"
                        );
                    }
                    else // Less than 10ms = good
                    {
                        ImGui.TextColored(
                            new System.Numerics.Vector4(0, 1, 0, 1),
                            $"{entry.TotalMs:F3}"
                        );
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            if (ImGui.Button("Clear History"))
            {
                _profilerHistory.Clear();
            }
        }

        ImGui.End();
    }

    private static void AddTableRow(string name, double avg, double max)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(name);
        ImGui.TableNextColumn();
        ImGui.Text($"{avg:F3}");
        ImGui.TableNextColumn();
        ImGui.Text($"{max:F3}");
    }

    public static void LogPerformanceMetrics(
        Action<string> logAction,
        params (string Name, Stopwatch Stopwatch)[] profilers
    )
    {
        List<string> logLines = new()
        {
            "Profiler | Ticks | Nanoseconds (ns) | Milliseconds (ms)",
            new string('-', 60),
        };

        foreach ((string name, Stopwatch stopwatch) in profilers)
        {
            if (stopwatch != null)
            {
                stopwatch.Stop();
                AddProfilerResult(name, stopwatch);
            }
        }

        int[] columnWidths = CalculateColumnWidths(logLines);

        foreach (string line in logLines)
        {
            logAction?.Invoke(FormatLine(line, columnWidths));
        }

        void AddProfilerResult(string profilerName, Stopwatch profiler)
        {
            long ticks = profiler.ElapsedTicks;
            double nanoseconds = (double)ticks / Stopwatch.Frequency * 1_000_000_000;
            double milliseconds = profiler.Elapsed.TotalMilliseconds;
            logLines.Add($"{profilerName} | {ticks} | {nanoseconds:N0} | {milliseconds:N2}");
        }

        int[] CalculateColumnWidths(IEnumerable<string> lines)
        {
            return lines
                .Select(line => line.Split('|'))
                .Where(columns => columns.Length == 4)
                .Select(columns => columns.Select(c => c.Trim().Length).ToList())
                .Aggregate(
                    new[] { 0, 0, 0, 0 },
                    (max, columns) => max.Zip(columns, Math.Max).ToArray()
                );
        }

        string FormatLine(string line, IReadOnlyList<int> widths)
        {
            string[] columns = line.Split('|');

            return columns.Length == 4
                ? $"{columns[0].Trim().PadRight(widths[0])} | {columns[1].Trim().PadLeft(widths[1])} | {columns[2].Trim().PadLeft(widths[2])} | {columns[3].Trim().PadLeft(widths[3])}"
                : line;
        }
    }

    public class ProfilerEntry
    {
        public DateTime Timestamp { get; set; }
        public long GetItemsTicks { get; set; }
        public long FilteringTicks { get; set; }
        public long DrawingTicks { get; set; }
        public long TotalTicks { get; set; }

        public double GetItemsMs => TicksToMs(GetItemsTicks);
        public double FilteringMs => TicksToMs(FilteringTicks);
        public double DrawingMs => TicksToMs(DrawingTicks);
        public double TotalMs => TicksToMs(TotalTicks);

        private static double TicksToMs(long ticks)
        {
            return (double)ticks / Stopwatch.Frequency * 1000.0;
        }
    }
}
