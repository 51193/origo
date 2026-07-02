using System;
using System.Globalization;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Runtime.Console;

internal static class ConsoleCommandHelper
{
    public static bool TryFindEntity(
        OrigoRuntime runtime,
        string entityName,
        out ISndEntity? entity,
        out string? errorMessage)
    {
        entity = runtime.SessionManager.ForegroundSession?.FindByName(entityName.Trim());
        if (entity is null)
        {
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static IBlackboard ResolveBlackboardLayer(OrigoRuntime runtime, string layer)
    {
        return layer.Trim().ToLowerInvariant() switch
        {
            "system" => runtime.SystemBlackboard,
            _ => throw new ArgumentException($"Unsupported blackboard layer '{layer}'. Expected 'system'.", nameof(layer))
        };
    }

    public static void SetDataWithTypeInference(ISndEntity entity, string key, string raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            entity.SetData(key, iv);
        else if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
            entity.SetData(key, fv);
        else if (bool.TryParse(raw, out var bv))
            entity.SetData(key, bv);
        else
            entity.SetData(key, raw);
    }

    public static void SetDataPreservingExistingType(ISndEntity entity, string key, string raw)
    {
        var (existing, existingObj) = entity.TryGetData<object>(key);
        if (existing && existingObj != null)
            SetByExistingType(entity, key, raw, existingObj.GetType());
        else
            SetDataWithTypeInference(entity, key, raw);
    }

    public static void SetBlackboardWithTypeInference(IBlackboard bb, string key, string raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            bb.SetValue(key, iv);
        else if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
            bb.SetValue(key, fv);
        else if (bool.TryParse(raw, out var bv))
            bb.SetValue(key, bv);
        else
            bb.SetValue(key, raw);
    }

    private static void SetByExistingType(ISndEntity entity, string key, string raw, Type targetType)
    {
        if (targetType == typeof(int) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            entity.SetData(key, iv);
        else if (targetType == typeof(float) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
            entity.SetData(key, fv);
        else if (targetType == typeof(bool) && bool.TryParse(raw, out var bv))
            entity.SetData(key, bv);
        else if (targetType == typeof(string))
            entity.SetData(key, raw);
    }
}
