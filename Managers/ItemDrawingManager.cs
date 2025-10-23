using System;
using System.Collections.Generic;
using ExileCore;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    ///     Manages drawing of item labels, outlines, and count boxes
    /// </summary>
    public class ItemDrawingManager(Func<Graphics> getGraphics, Func<Settings> getSettings)
    {
        private readonly TextMeasurementCache _textCache = new();

    /// <summary>
    ///     Draws outline around an item label if enabled
    /// </summary>
    public void DrawLabelOutline(dynamic element, UniqueItemSettings uniqueSettings)
    {
        Settings settings = getSettings();
        if (
            !settings.LabelDrawingSettings.EnableOutlineLebel
            || !uniqueSettings.DrawLabelOutline
        )
        {
            return;
        }

        RectangleF labelFrame = element.GetClientRect();
        getGraphics()
            .DrawFrame(
                labelFrame,
                settings.LabelDrawingSettings.OutlineLabelColor,
                settings.LabelDrawingSettings.LabelFrameThickness
            );
    }

    /// <summary>
    ///     Draws label name on unidentified items if enabled
    /// </summary>
    public void DrawLabelName(
        dynamic element,
        string labelText,
        bool isIdentified,
        UniqueItemSettings uniqueSettings
    )
    {
        Settings settings = getSettings();
        if (
            !settings.LabelDrawingSettings.EnableLabelName
            || !uniqueSettings.DrawLabelName
            || isIdentified
        )
        {
            return;
        }

        RectangleF labelFrame = element.GetClientRect();
        Vector2 textSize = _textCache.GetOrMeasure(getGraphics(), labelText);
        float scale =
            Math.Min(labelFrame.Width / textSize.X, (labelFrame.Height - 2) / textSize.Y)
            - 0.2f;

        ImGui.SetWindowFontScale(scale);
        Vector2 newTextSize = ImGui.CalcTextSize(labelText);
        Vector2 textPosition = labelFrame.Center.ToVector2Num() - newTextSize / 2;

        ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();

        // Draw background
        drawList.AddRectFilled(
            labelFrame.TopLeft.ToVector2Num(),
            labelFrame.BottomRight.ToVector2Num(),
            settings.LabelDrawingSettings.BackgroundLabel.Value.ToImgui()
        );

        if (settings.LabelDrawingSettings.TextOutlineEnabled.Value)
        {
            float thickness = settings.LabelDrawingSettings.TextOutlineThickness.Value;
            uint outlineColor = settings.LabelDrawingSettings.TextOutlineColor.Value.ToImgui();

            drawList.AddText(
                textPosition + new Vector2(-thickness, 0),
                outlineColor,
                labelText
            );
            drawList.AddText(textPosition + new Vector2(thickness, 0), outlineColor, labelText);
            drawList.AddText(
                textPosition + new Vector2(0, -thickness),
                outlineColor,
                labelText
            );
            drawList.AddText(textPosition + new Vector2(0, thickness), outlineColor, labelText);
        }

        // Draw main text on top
        drawList.AddText(
            textPosition,
            settings.LabelDrawingSettings.LabelTextColor.Value.ToImgui(),
            labelText
        );
    }

    /// <summary>
    ///     Draws a line on the map from player to item
    /// </summary>
    public void DrawMapLine(Vector2 itemPosition, Vector2 playerPosition)
    {
        Settings settings = getSettings();
        getGraphics()
            .DrawLine(
                itemPosition,
                playerPosition,
                settings.MapDrawingSettings.MapLineThickness,
                settings.MapDrawingSettings.MapLineColor
            );
    }

    /// <summary>
    ///     Draws a line in the world from player to item
    /// </summary>
    public void DrawWorldLine(Vector2 itemPosition, Vector2 playerPosition)
    {
        Settings settings = getSettings();
        getGraphics()
            .DrawLine(
                playerPosition,
                itemPosition,
                settings.MapDrawingSettings.WorldMapLineThickness,
                settings.MapDrawingSettings.WorldMapLineColor
            );
    }

    /// <summary>
    ///     Draws the item count information box
    /// </summary>
    public void DrawItemCountBox(List<string> countList)
    {
        if (countList.Count == 0)
        {
            return;
        }

        Settings settings = getSettings();

        // Optimization: pre-allocate dictionary capacity
        Dictionary<string, int> labelCount = new(countList.Count / 2);

        // Manual grouping is more efficient than LINQ GroupBy + ToDictionary
        foreach (string item in countList)
        {
            if (labelCount.ContainsKey(item))
            {
                labelCount[item]++;
            }
            else
            {
                labelCount[item] = 1;
            }
        }

        float posX = settings.BoxSettings.BoxPositionX.Value;
        float posY = settings.BoxSettings.BoxPositionY.Value;
        int height = labelCount.Count * 20 + 20;
        RectangleF rect = new(posX, posY, 230, height);

        getGraphics().DrawBox(rect, settings.BoxSettings.BoxBackgroundColor);

        if (settings.BoxSettings.BoxOutline.Value)
        {
            getGraphics().DrawFrame(rect, settings.BoxSettings.BoxOutlineColor, 2);
        }

        posX += 10;
        posY += 10;

        foreach (KeyValuePair<string, int> item in labelCount)
        {
            getGraphics()
                .DrawText(
                    $"{item.Key}: {item.Value}",
                    new Vector2(posX, posY),
                    settings.BoxSettings.BoxTextColor
                );
            posY += 20;
        }
    }

    /// <summary>
    ///     Clears the text measurement cache. Should be called when font settings change.
    /// </summary>
    public void ClearTextCache()
    {
        _textCache.Clear();
    }

    /// <summary>
    ///     Gets the current size of the text measurement cache
    /// </summary>
    public int GetCacheSize()
    {
        return _textCache.CacheSize;
    }
}
}
