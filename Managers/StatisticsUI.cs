using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace UniqueLootHelper.Managers;

public static class StatisticsUI
{
    public static void ShowStatisticsWindow(ref bool isOpen, StatisticsManager statisticsManager)
    {
        if (!isOpen || statisticsManager == null)
        {
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Drop Statistics", ref isOpen, ImGuiWindowFlags.None))
        {
            var currentSession = statisticsManager.CurrentSession;
            var lifetime = statisticsManager.Lifetime;

            // Update averages before displaying
            statisticsManager.UpdateAverages();

            // Create tab bar for session vs lifetime
            if (ImGui.BeginTabBar("StatsTabBar"))
            {
                // Current Session Tab
                if (ImGui.BeginTabItem("Current Session"))
                {
                    DrawStatistics(currentSession, "Current Session");

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    if (ImGui.Button("Reset Session Statistics"))
                    {
                        ImGui.OpenPopup("ConfirmResetSession");
                    }

                    // Confirmation popup for session reset
                    bool sessionPopupOpen = true;
                    if (ImGui.BeginPopupModal("ConfirmResetSession", ref sessionPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.Text("Are you sure you want to reset current session statistics?");
                        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "This action cannot be undone!");
                        ImGui.Spacing();

                        if (ImGui.Button("Yes, Reset", new Vector2(120, 0)))
                        {
                            statisticsManager.ResetSessionStatistics();
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();

                        if (ImGui.Button("Cancel", new Vector2(120, 0)))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.EndTabItem();
                }

                // Lifetime Tab
                if (ImGui.BeginTabItem("Lifetime"))
                {
                    DrawStatistics(lifetime, "Lifetime");

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Warning: Resetting lifetime statistics will delete ALL historical data!");

                    if (ImGui.Button("Reset Lifetime Statistics"))
                    {
                        ImGui.OpenPopup("ConfirmResetLifetime");
                    }

                    // Confirmation popup for lifetime reset
                    bool lifetimePopupOpen = true;
                    if (ImGui.BeginPopupModal("ConfirmResetLifetime", ref lifetimePopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.Text("Are you sure you want to reset ALL lifetime statistics?");
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: This will delete ALL historical data!");
                        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "This action cannot be undone!");
                        ImGui.Spacing();

                        if (ImGui.Button("Yes, Reset Everything", new Vector2(180, 0)))
                        {
                            statisticsManager.ResetLifetimeStatistics();
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();

                        if (ImGui.Button("Cancel", new Vector2(120, 0)))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.EndTabItem();
                }

                // Current Map Tab
                if (ImGui.BeginTabItem("Current Map"))
                {
                    DrawCurrentMapSession(statisticsManager.CurrentMapSession, statisticsManager.IsInMap);
                    ImGui.EndTabItem();
                }

                // Currency & Valuable Items Tab
                if (ImGui.BeginTabItem("Currency & Items"))
                {
                    DrawCurrencyStatistics(currentSession, "Current Session");
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                    DrawCurrencyStatistics(lifetime, "Lifetime");
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }

    private static void DrawStatistics(StatisticsData stats, string title)
    {
        if (stats == null)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No statistics data available");
            return;
        }

        // Session info
        ImGui.TextColored(new Vector4(0.5f, 1, 0.5f, 1), $"Session Duration: {FormatTimeSpan(stats.SessionDuration)}");
        ImGui.Text($"Session Start: {stats.SessionStartTime:yyyy-MM-dd HH:mm:ss}");

        // Show time breakdown: session time vs map time
        if (stats.TotalMapTime.TotalSeconds > 0 && stats.SessionDuration.TotalSeconds > 0)
        {
            double mapTimePercent = (stats.TotalMapTime.TotalSeconds / stats.SessionDuration.TotalSeconds) * 100.0;
            ImGui.Text($"Time in Maps: {FormatTimeSpan(stats.TotalMapTime)} ({mapTimePercent:F1}%)");

            var timeInTownHideout = stats.SessionDuration - stats.TotalMapTime;
            if (timeInTownHideout.TotalSeconds > 0)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1),
                    $"Time in Town/Hideout: {FormatTimeSpan(timeInTownHideout)} ({(100 - mapTimePercent):F1}%)");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Show unique percentage vs base rarities (Normal, Magic, Rare, Unique)
        int totalBaseRarityItems = stats.GetTotalBaseRarityItems();
        if (totalBaseRarityItems > 0)
        {
            double uniquePercent = stats.GetUniquePercentageVsBaseRarities();
            ImGui.TextColored(new Vector4(1, 0.8f, 0.2f, 1),
                $"Unique Rate: {uniquePercent:F2}% ({stats.TotalUniques} of {totalBaseRarityItems} base items)");

            // Show breakdown
            ImGui.Indent();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Base Rarity Breakdown:");

            if (stats.ItemsByRarity.TryGetValue("Normal", out int normalCount) && normalCount > 0)
            {
                double normalPercent = (double)normalCount / totalBaseRarityItems * 100.0;
                ImGui.Text($"Normal: {normalCount:N0} ({normalPercent:F1}%)");
            }

            if (stats.ItemsByRarity.TryGetValue("Magic", out int magicCount) && magicCount > 0)
            {
                double magicPercent = (double)magicCount / totalBaseRarityItems * 100.0;
                ImGui.TextColored(new Vector4(0.3f, 0.5f, 1, 1), $"Magic: {magicCount:N0} ({magicPercent:F1}%)");
            }

            if (stats.ItemsByRarity.TryGetValue("Rare", out int rareCount) && rareCount > 0)
            {
                double rarePercent = (double)rareCount / totalBaseRarityItems * 100.0;
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Rare: {rareCount:N0} ({rarePercent:F1}%)");
            }

            if (stats.ItemsByRarity.TryGetValue("Unique", out int uniqueCount) && uniqueCount > 0)
            {
                ImGui.TextColored(new Vector4(1, 0.5f, 0.2f, 1), $"Unique: {uniqueCount:N0} ({uniquePercent:F1}%)");
            }

            ImGui.Unindent();
        }

        if (stats.SessionDuration.TotalHours > 0)
        {
            double hours = stats.SessionDuration.TotalHours;
            ImGui.Text($"Total Items: {stats.TotalItems:N0}  ({(stats.TotalItems / hours):N0}/hr)");
            ImGui.Text($"Uniques: {stats.TotalUniques:N0}  ({(stats.TotalUniques / hours):N0}/hr)");
        }

        ImGui.Spacing();

        // Map statistics
        if (stats.TotalMapsRun > 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 1, 1, 1), "Map Statistics");
            ImGui.Text($"Total Maps Run: {stats.TotalMapsRun:N0}");
            ImGui.Text($"Average Uniques per Map: {stats.AverageUniquesPerMap:N0}");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), "Time in Maps");
            ImGui.Text($"Total Time in Maps: {FormatTimeSpan(stats.TotalMapTime)}");
            ImGui.Text($"Average Map Duration: {stats.AverageMapDurationMinutes:N0} minutes");

            // Calculate and show efficiency
            if (stats.TotalMapTime.TotalMinutes > 0)
            {
                double mapEfficiency = stats.TotalUniquesInMaps / stats.TotalMapTime.TotalHours;
                ImGui.Text($"Overall Efficiency: {mapEfficiency:N0} uniques/hour");
            }

            ImGui.Spacing();

            if (stats.BestMapSession != null && stats.BestMapSession.UniqueCount > 0)
            {
                ImGui.Text($"Best Map: {stats.BestMapSession.MapName} ({stats.BestMapSession.UniqueCount} uniques)");
            }

            if (stats.FastestMapSession != null && stats.FastestMapSession.UniqueCount > 0)
            {
                ImGui.Text($"Fastest Map: {stats.FastestMapSession.MapName} ({FormatTimeSpan(stats.FastestMapSession.Duration)})");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Unique Items by Tier
        if (stats.UniqueTierStats.Any())
        {
            ImGui.TextColored(new Vector4(1, 0.8f, 0.2f, 1), "Unique Items by Tier");

            // Show T0 chance prominently if it exists
            if (stats.UniqueTierStats.TryGetValue("T0", out var t0Stats) && stats.TotalUniques > 0)
            {
                ImGui.Spacing();
                Vector4 t0Color = new Vector4(1.0f, 0.3f, 0.3f, 1.0f);
                ImGui.TextColored(t0Color, $"T0 Chance: {t0Stats.Percentage:F2}% ({t0Stats.Count} out of {stats.TotalUniques})");

                if (t0Stats.Count > 0)
                {
                    double inverseChance = 100.0 / t0Stats.Percentage;
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"(~1 in {inverseChance:F0} uniques)");

                    ImGui.Spacing();
                    ImGui.TextColored(t0Color, "T0 Drops:");
                    ImGui.Spacing();

                    if (ImGui.BeginTable("##T0DropsTable", 2,
                        ImGuiTableFlags.Borders |
                        ImGuiTableFlags.RowBg |
                        ImGuiTableFlags.ScrollY,
                        new Vector2(0, 138)))
                    {
                        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 80);
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();

                        foreach (var name in t0Stats.ItemNames.OrderBy(n => n))
                        {
                            stats.TopUniqueDrops.TryGetValue(name, out int count);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextColored(t0Color, name);
                            ImGui.TableNextColumn();
                            ImGui.Text(count.ToString());
                        }

                        ImGui.EndTable();
                    }
                }
                ImGui.Spacing();
            }

            if (ImGui.BeginTable("##TierStatsTable", 3,
                ImGuiTableFlags.Borders |
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.ScrollY |
                ImGuiTableFlags.Resizable,
                new Vector2(0, 200)))
            {
                ImGui.TableSetupColumn("Tier", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                // Sort tiers: T0, T1, T2, T3, T4, then others
                var sortedTiers = stats.UniqueTierStats
                    .OrderBy(kvp => kvp.Key.StartsWith("T") && kvp.Key.Length == 2
                        ? int.Parse(kvp.Key.Substring(1))
                        : 999)
                    .ThenBy(kvp => kvp.Key);

                foreach (var tierStat in sortedTiers)
                {
                    ImGui.TableNextRow();

                    // Tier column with color coding
                    ImGui.TableNextColumn();
                    Vector4 tierColor = GetTierColor(tierStat.Key);
                    ImGui.TextColored(tierColor, tierStat.Key);

                    // Count column
                    ImGui.TableNextColumn();
                    ImGui.Text(tierStat.Value.Count.ToString("N0"));

                    // Chance (percentage) column
                    ImGui.TableNextColumn();
                    Vector4 percentColor = tierStat.Value.Percentage switch
                    {
                        < 1.0 => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),    // Red - ultra rare
                        < 5.0 => new Vector4(1.0f, 0.6f, 0.0f, 1.0f),    // Orange - very rare
                        < 15.0 => new Vector4(1.0f, 1.0f, 0.0f, 1.0f),   // Yellow - rare
                        < 30.0 => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),   // Green - common
                        _ => new Vector4(0.8f, 0.8f, 0.8f, 1.0f)         // Gray - very common
                    };
                    ImGui.TextColored(percentColor, $"{tierStat.Value.Percentage:F2}%");
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Recent map sessions
        if (stats.RecentMapSessions != null && stats.RecentMapSessions.Any())
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.7f, 0.9f, 1, 1), "Recent Map Sessions");

            if (ImGui.BeginTable("##RecentMapsTable", 5,
                ImGuiTableFlags.Borders |
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.ScrollY,
                new Vector2(0, 150)))
            {
                ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Uniques", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("U/min", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 140);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                foreach (var mapSession in stats.RecentMapSessions.AsEnumerable().Reverse())
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(mapSession.MapName);

                    if (ImGui.IsItemHovered() && mapSession.UniqueNames.Any())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("T0 items:");
                        foreach (var uniqueName in mapSession.UniqueNames.Take(15))
                        {
                            ImGui.Text($"• {uniqueName}");
                        }
                        if (mapSession.UniqueNames.Count > 15)
                        {
                            ImGui.Text($"... and {mapSession.UniqueNames.Count - 15} more");
                        }
                        ImGui.EndTooltip();
                    }

                    ImGui.TableNextColumn();
                    Vector4 uniqueColor = mapSession.UniqueCount >= 5 ? new Vector4(0, 1, 0, 1) :
                                         mapSession.UniqueCount >= 3 ? new Vector4(1, 1, 0, 1) :
                                         new Vector4(1, 1, 1, 1);
                    ImGui.TextColored(uniqueColor, mapSession.UniqueCount.ToString());

                    ImGui.TableNextColumn();
                    ImGui.Text(FormatTimeSpan(mapSession.Duration));

                    ImGui.TableNextColumn();
                    ImGui.Text(mapSession.UniquesPerMinute.ToString("N0"));

                    ImGui.TableNextColumn();
                    ImGui.Text(mapSession.StartTime.ToString("HH:mm:ss"));
                }

                ImGui.EndTable();
            }
        }
    }

    private static void DrawCurrentMapSession(StatisticsData.MapSessionData mapSession, bool isInMap)
    {
        if (!isInMap || mapSession == null || string.IsNullOrEmpty(mapSession.MapName))
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Not currently in a map");
            ImGui.Text("Statistics are only tracked in maps (not in towns, hideouts, or peaceful areas)");
            return;
        }

        ImGui.TextColored(new Vector4(0.5f, 1, 0.5f, 1), $"Current Map: {mapSession.MapName}");
        ImGui.Text($"Started: {mapSession.StartTime:HH:mm:ss}");

        // Calculate current duration (live)
        var currentDuration = DateTime.Now - mapSession.StartTime;
        ImGui.Text($"Duration: {FormatTimeSpan(currentDuration)}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Uniques Found: {mapSession.UniqueCount}");

        // Show uniques per minute (live calculation)
        if (mapSession.UniqueCount > 0 && currentDuration.TotalMinutes > 0)
        {
            double currentRate = mapSession.UniqueCount / currentDuration.TotalMinutes;
            ImGui.Text($"Rate: {currentRate:N0} uniques/min");

            // Show projection for hourly rate
            if (currentDuration.TotalMinutes >= 1) // Only show after 1+ minute
            {
                double projectedPerHour = currentRate * 60;
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 1, 1), $"Projected: {projectedPerHour:N0} uniques/hour");
            }
        }

        if (mapSession.TierBreakdown.Any())
        {
            ImGui.Spacing();
            ImGui.Text("By Tier:");

            foreach (var tier in mapSession.TierBreakdown.OrderBy(kvp => kvp.Key))
            {
                Vector4 tierColor = GetTierColor(tier.Key);
                ImGui.TextColored(tierColor, $"  {tier.Key}: {tier.Value}");
            }
        }

        ImGui.Spacing();

        if (mapSession.UniqueNames.Any())
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "T0 Items Found:");

            if (ImGui.BeginTable("##CurrentMapUniques", 1,
                ImGuiTableFlags.Borders |
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.ScrollY,
                new Vector2(0, 300)))
            {
                ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var uniqueName in mapSession.UniqueNames)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text($"• {uniqueName}");
                }

                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No uniques found yet in this map");
        }
    }

    private static Vector4 GetTierColor(string tier)
    {
        return tier switch
        {
            "T0" => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),    // Red - ultra rare
            "T1" => new Vector4(1.0f, 0.6f, 0.0f, 1.0f),    // Orange - very rare
            "T2" => new Vector4(1.0f, 1.0f, 0.0f, 1.0f),    // Yellow - rare
            "T3" => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),    // Green - uncommon
            "T4" => new Vector4(0.8f, 0.8f, 0.8f, 1.0f),    // Gray - common
            "T5" => new Vector4(0.6f, 0.6f, 0.6f, 1.0f),    // Dark gray - very common
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)        // White - unknown
        };
    }

    private static string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";

        return $"{(int)span.TotalSeconds}s";
    }

    private static void DrawCurrencyStatistics(StatisticsData stats, string title)
    {
        if (stats == null)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No statistics data available");
            return;
        }

        // Overall currency stats
        ImGui.TextColored(new Vector4(1, 0.8f, 0.2f, 1), $"{title} - Currency & Valuable Items");
        ImGui.Spacing();

        if (stats.TotalCurrency > 0)
        {
            ImGui.Text($"Total Currency: {stats.TotalCurrency:N0}");
        }
        if (stats.TotalDivinationCards > 0)
        {
            ImGui.Text($"Divination Cards: {stats.TotalDivinationCards:N0}");
        }
        if (stats.TotalFragments > 0)
        {
            ImGui.Text($"Fragments: {stats.TotalFragments:N0}");
        }
        if (stats.TotalScarabs > 0)
        {
            ImGui.Text($"Scarabs: {stats.TotalScarabs:N0}");
        }
        if (stats.TotalEssences > 0)
        {
            ImGui.Text($"Essences: {stats.TotalEssences:N0}");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Top Currency Drops
        if (stats.CurrencyDrops.Any())
        {
            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Top Currency Drops");

            if (ImGui.BeginTable("##TopCurrencyTable", 2,
                ImGuiTableFlags.Borders |
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.ScrollY,
                new Vector2(0, 200)))
            {
                ImGui.TableSetupColumn("Currency", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                foreach (var currency in stats.CurrencyDrops.OrderBy(kvp => kvp.Value))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(currency.Key);

                    ImGui.TableNextColumn();
                    ImGui.Text(currency.Value.ToString("N0"));
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Top Divination Card Drops
        if (stats.DivinationCardDrops.Any())
        {
            ImGui.TextColored(new Vector4(0.8f, 0.5f, 1, 1), "Top Divination Card Drops");

            if (ImGui.BeginTable("##TopCardsTable", 2,
                ImGuiTableFlags.Borders |
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.ScrollY,
                new Vector2(0, 200)))
            {
                ImGui.TableSetupColumn("Card", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                foreach (var card in stats.DivinationCardDrops.OrderByDescending(kvp => kvp.Value).Take(20))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(card.Key);

                    ImGui.TableNextColumn();
                    ImGui.Text(card.Value.ToString("N0"));
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Items by Type
        if (stats.ValueableItemsByType.Any())
        {
            ImGui.TextColored(new Vector4(0.5f, 1, 0.8f, 1), "Valuable Items by Type");

            foreach (var itemType in stats.ValueableItemsByType.OrderByDescending(kvp => kvp.Value))
            {
                ImGui.BulletText($"{itemType.Key}: {itemType.Value:N0}");
            }
        }
    }
}
