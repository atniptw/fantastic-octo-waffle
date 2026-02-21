using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnityAssetParser;

internal static class UnityYamlParser
{
    public static void Parse(byte[] data, string sourceName, BaseAssetsContext context)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Containers.Add(new ParsedContainer(sourceName, ContainerKind.Unknown, data.Length));

        var text = Encoding.UTF8.GetString(data);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var currentTypeId = -1;
        var currentFileId = 0L;
        var gameObjectName = string.Empty;
        var gameObjectActive = true;
        var gameObjectLayer = 0;

        var transformGameObjectId = 0L;
        var transformParentId = (long?)null;
        var transformChildren = new List<long>();
        var transformLocalPosition = new SemanticVector3(0, 0, 0);
        var transformLocalRotation = new SemanticQuaternion(1, 0, 0, 0);
        var transformLocalScale = new SemanticVector3(1, 1, 1);
        var inChildren = false;

        void Flush()
        {
            if (currentTypeId == 1 && currentFileId != 0)
            {
                var name = string.IsNullOrWhiteSpace(gameObjectName) ? sourceName : gameObjectName;
                context.SemanticGameObjects.Add(new SemanticGameObjectInfo(currentFileId, name, gameObjectActive, gameObjectLayer));

                var anchorTag = TryResolveAnchorTag(name);
                if (anchorTag is not null)
                {
                    context.SemanticAnchorPoints.Add(new SemanticAnchorPointInfo(currentFileId, anchorTag, name));
                }
            }
            else if (currentTypeId == 4 && currentFileId != 0)
            {
                context.SemanticTransforms.Add(new SemanticTransformInfo(
                    currentFileId,
                    transformGameObjectId,
                    transformParentId,
                    transformChildren.ToArray(),
                    transformLocalPosition,
                    transformLocalRotation,
                    transformLocalScale));
            }
        }

        void ResetState(int typeId, long fileId)
        {
            currentTypeId = typeId;
            currentFileId = fileId;
            gameObjectName = string.Empty;
            gameObjectActive = true;
            gameObjectLayer = 0;
            transformGameObjectId = 0;
            transformParentId = null;
            transformChildren = new List<long>();
            transformLocalPosition = new SemanticVector3(0, 0, 0);
            transformLocalRotation = new SemanticQuaternion(1, 0, 0, 0);
            transformLocalScale = new SemanticVector3(1, 1, 1);
            inChildren = false;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                Flush();
                var header = line.Substring("--- !u!".Length);
                var ampersandIndex = header.IndexOf('&');
                if (ampersandIndex > 0
                    && int.TryParse(header.Substring(0, ampersandIndex).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeId)
                    && long.TryParse(header.Substring(ampersandIndex + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId))
                {
                    ResetState(typeId, fileId);
                }
                else
                {
                    ResetState(-1, 0);
                }

                continue;
            }

            if (currentTypeId == -1)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (currentTypeId == 1)
            {
                if (TryReadSimpleValue(trimmed, "m_Name:", out var nameValue))
                {
                    gameObjectName = nameValue;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_IsActive:", out var activeValue)
                    && int.TryParse(activeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var activeInt))
                {
                    gameObjectActive = activeInt != 0;
                    continue;
                }

                if (TryReadSimpleValue(trimmed, "m_Layer:", out var layerValue)
                    && int.TryParse(layerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var layer))
                {
                    gameObjectLayer = layer;
                    continue;
                }
            }
            else if (currentTypeId == 4)
            {
                if (trimmed.StartsWith("m_Children:", StringComparison.Ordinal))
                {
                    inChildren = true;
                    continue;
                }

                if (inChildren && trimmed.StartsWith("- fileID:", StringComparison.Ordinal))
                {
                    var idText = trimmed.Substring("- fileID:".Length).Trim();
                    if (long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var childId))
                    {
                        transformChildren.Add(childId);
                    }
                    continue;
                }

                if (!trimmed.StartsWith("- fileID:", StringComparison.Ordinal) && trimmed.Contains(":", StringComparison.Ordinal))
                {
                    inChildren = false;
                }

                if (TryReadFileIdValue(trimmed, "m_GameObject:", out var gameObjectId))
                {
                    transformGameObjectId = gameObjectId;
                    continue;
                }

                if (TryReadFileIdValue(trimmed, "m_Father:", out var fatherId))
                {
                    transformParentId = fatherId == 0 ? null : fatherId;
                    continue;
                }

                if (TryReadVector3(trimmed, "m_LocalPosition:", out var localPosition))
                {
                    transformLocalPosition = localPosition;
                    continue;
                }

                if (TryReadQuaternion(trimmed, "m_LocalRotation:", out var localRotation))
                {
                    transformLocalRotation = localRotation;
                    continue;
                }

                if (TryReadVector3(trimmed, "m_LocalScale:", out var localScale))
                {
                    transformLocalScale = localScale;
                    continue;
                }
            }
        }

        Flush();
    }

    public static bool LooksLikeYaml(byte[] data)
    {
        if (data is null || data.Length < 5)
        {
            return false;
        }

        var startIndex = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            startIndex = 3;
        }

        while (startIndex < data.Length && data[startIndex] <= 0x20)
        {
            startIndex++;
        }

        if (startIndex >= data.Length)
        {
            return false;
        }

        var prefixLength = Math.Min(64, data.Length - startIndex);
        var prefix = Encoding.ASCII.GetString(data, startIndex, prefixLength);
        return prefix.StartsWith("%YAML", StringComparison.Ordinal)
            || prefix.StartsWith("--- !u!", StringComparison.Ordinal);
    }

    private static bool TryReadSimpleValue(string line, string key, out string value)
    {
        if (line.StartsWith(key, StringComparison.Ordinal))
        {
            value = line.Substring(key.Length).Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadFileIdValue(string line, string key, out long value)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            value = 0;
            return false;
        }

        var braceIndex = line.IndexOf('{');
        if (braceIndex < 0)
        {
            value = 0;
            return false;
        }

        var fileIdIndex = line.IndexOf("fileID", StringComparison.Ordinal);
        if (fileIdIndex < 0)
        {
            value = 0;
            return false;
        }

        var colonIndex = line.IndexOf(':', fileIdIndex);
        if (colonIndex < 0)
        {
            value = 0;
            return false;
        }

        var endIndex = line.IndexOf('}', colonIndex);
        if (endIndex < 0)
        {
            endIndex = line.Length;
        }

        var valueText = line.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
        return long.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadVector3(string line, string key, out SemanticVector3 value)
    {
        if (!TryReadInlineMap(line, key, out var map))
        {
            value = default;
            return false;
        }

        value = new SemanticVector3(
            GetFloat(map, "x"),
            GetFloat(map, "y"),
            GetFloat(map, "z"));
        return true;
    }

    private static bool TryReadQuaternion(string line, string key, out SemanticQuaternion value)
    {
        if (!TryReadInlineMap(line, key, out var map))
        {
            value = default;
            return false;
        }

        value = new SemanticQuaternion(
            GetFloat(map, "w"),
            GetFloat(map, "x"),
            GetFloat(map, "y"),
            GetFloat(map, "z"));
        return true;
    }

    private static bool TryReadInlineMap(string line, string key, out Dictionary<string, float> map)
    {
        map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var braceIndex = line.IndexOf('{');
        var endIndex = line.IndexOf('}', braceIndex + 1);
        if (braceIndex < 0 || endIndex < 0)
        {
            return false;
        }

        var content = line.Substring(braceIndex + 1, endIndex - braceIndex - 1);
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var tokens = part.Split(':', 2);
            if (tokens.Length != 2)
            {
                continue;
            }

            var keyName = tokens[0].Trim();
            var valueText = tokens[1].Trim();
            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                map[keyName] = numeric;
            }
        }

        return map.Count > 0;
    }

    private static float GetFloat(Dictionary<string, float> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : 0f;
    }

    private static string? TryResolveAnchorTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = NormalizeName(name);
        if (normalized.Contains("head decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "head";
        }
        if (normalized.Contains("neck decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "neck";
        }
        if (normalized.Contains("body decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }
        if (normalized.Contains("hip decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "hip";
        }
        if (normalized.Contains("l-arm decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("leftarm decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "leftarm";
        }
        if (normalized.Contains("r-arm decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rightarm decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "rightarm";
        }
        if (normalized.Contains("l-leg decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("leftleg decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "leftleg";
        }
        if (normalized.Contains("r-leg decoration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rightleg decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "rightleg";
        }
        if (normalized.Contains("world decoration", StringComparison.OrdinalIgnoreCase))
        {
            return "world";
        }

        return null;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        return trimmed;
    }
}
