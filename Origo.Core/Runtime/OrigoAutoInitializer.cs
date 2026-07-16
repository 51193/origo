using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Runtime;

/// <summary>
///     Provides runtime auto-initialization capabilities:
///     reflectively scans <see cref="BaseStrategy" /> subclasses and registers them in the strategy pool,
///     loads SndMetaData arrays from JSON configuration files and automatically spawns entities.
/// </summary>
public static class OrigoAutoInitializer
{
    private const string _logTag = nameof(OrigoAutoInitializer);

    /// <summary>Core runtime assembly name that should always be skipped during strategy scanning.</summary>
    private const string _corLibAssemblyName = "mscorlib";

    /// <summary>Assembly simple name prefixes skipped when scanning for <see cref="BaseStrategy" /> types.</summary>
    private static readonly string[] _defaultSkipPrefixes =
        ["System", "Microsoft", "netstandard"];

    public static int DiscoverAndRegisterStrategies(
        SndWorld world,
        ILogger logger,
        IEnumerable<string>? additionalSkipPrefixes = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);
        var watch = Stopwatch.StartNew();

        var baseType = typeof(BaseStrategy);
        var pool = world.StrategyPool;
        var registered = 0;

        var skipPrefixes = additionalSkipPrefixes is not null
            ? [.. _defaultSkipPrefixes, .. additionalSkipPrefixes]
            : _defaultSkipPrefixes;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (ShouldSkipAssembly(assembly, skipPrefixes))
                continue;

            var types = GetAssemblyTypes(assembly, logger);
            registered += RegisterStrategyTypes(types, baseType, pool, logger, watch);
        }

        watch.Stop();
        logger.Log(LogLevel.Info, _logTag, new LogMessageBuilder()
            .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
            .Build("Strategy auto-discovery complete."));

        return registered;
    }

    /// <summary>
    ///     Reads an array of SndMetaData from a single JSON file and bulk-spawns them into the current session
    ///     via <see cref="ISessionRun" />. Supports both complete SndMetaData objects and template reference shorthands.
    /// </summary>
    public static int LoadAndSpawnFromFile(
        string filePath,
        SndWorld sndWorld,
        ISessionRun session,
        IDataSourceIoGateway dataSourceIo,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(sndWorld);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        var watch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            var ex = new ArgumentException("Config file path cannot be null or whitespace.", nameof(filePath));
            logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                .AddContext("filePath", filePath)
                .Build($"Invalid config path: {ex.Message}"));
            throw ex;
        }

        var root = ReadConfigFile(filePath, dataSourceIo, logger);

        using (root)
        {
            if (root.Kind != DataSourceNodeKind.Array)
            {
                var ex = new InvalidOperationException($"Config file '{filePath}' must be a JSON array.");
                logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                    .AddContext("filePath", filePath)
                    .Build($"Config json root is not array: {ex.Message}"));
                throw ex;
            }

            return SpawnFromJsonArray(root, sndWorld, session, filePath, logger, watch);
        }
    }

    private static Type[] GetAssemblyTypes(Assembly assembly, ILogger logger)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var wrapped = new InvalidOperationException(
                $"Failed to enumerate types from assembly '{assembly.FullName}'.", ex);
            logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                .AddContext("filePath", assembly.FullName)
                .Build($"Discover strategy types failed: {wrapped.Message}"));
            throw wrapped;
        }
    }

    private static int RegisterStrategyTypes(
        Type[] types,
        Type baseType,
        SndStrategyPool pool,
        ILogger logger,
        Stopwatch watch)
    {
        var registered = 0;
        foreach (var type in types)
        {
            if (type.IsAbstract || !baseType.IsAssignableFrom(type))
                continue;
            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                var ex = new InvalidOperationException(
                    $"Strategy type '{type.FullName}' must declare a public parameterless constructor.");
                logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                    .Build($"Invalid strategy constructor: {ex.Message}"));
                throw ex;
            }

            if (!SndStrategyPool.ValidateStrategyType(type, out var invalidMembers))
            {
                var ex = new InvalidOperationException(
                    $"Strategy type '{type.FullName}' declares invalid instance members ({invalidMembers}); " +
                    "shared pooled strategies must be stateless.");
                logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                    .Build($"Strategy state validation failed: {ex.Message}"));
                throw ex;
            }

            var index = ResolveStrategyIndex(type);
            var capturedType = type;
            pool.Register(capturedType, () => (BaseStrategy)Activator.CreateInstance(capturedType)!);
            registered++;

            logger.Log(LogLevel.Debug, _logTag, new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .AddContext("strategyIndex", index)
                .Build("Strategy auto-registered."));
        }

        return registered;
    }

    private static DataSourceNode ReadConfigFile(
        string filePath,
        IDataSourceIoGateway dataSourceIo,
        ILogger logger)
    {
        try
        {
            return dataSourceIo.ReadTree(filePath);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or FileNotFoundException or DirectoryNotFoundException)
        {
            var notFound = new InvalidOperationException($"Config file '{filePath}' not found.", ex);
            logger.Log(LogLevel.Error, _logTag, new LogMessageBuilder()
                .AddContext("filePath", filePath)
                .Build($"Config file not found: {notFound.Message}"));
            throw notFound;
        }
    }

    private static int SpawnFromJsonArray(
        DataSourceNode root,
        SndWorld sndWorld,
        ISessionRun session,
        string filePath,
        ILogger logger,
        Stopwatch watch)
    {
        var metaList = sndWorld.ResolveMetaListFromJsonArray(root);
        session.SpawnMany([.. metaList]);

        watch.Stop();
        logger.Log(LogLevel.Info, _logTag, new LogMessageBuilder()
            .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
            .AddContext("filePath", filePath)
            .Build($"Spawned entities from config: {metaList.Count}."));
        return metaList.Count;
    }

    private static bool ShouldSkipAssembly(Assembly assembly, string[] skipPrefixes)
    {
        var name = assembly.GetName().Name;
        if (name is null) return true;
        if (name == _corLibAssemblyName) return true;

        foreach (var prefix in skipPrefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;

        return false;
    }

    internal static bool IsStatelessStrategyType(Type strategyType, out string mutableFieldNames) =>
        SndStrategyPool.ValidateStrategyType(strategyType, out mutableFieldNames);

    private static string ResolveStrategyIndex(Type strategyType)
    {
        var attr = strategyType.GetCustomAttribute<StrategyIndexAttribute>() ?? throw new InvalidOperationException(
                $"Strategy '{strategyType.FullName}' missing required StrategyIndexAttribute.");
        if (string.IsNullOrWhiteSpace(attr.Index))
            throw new InvalidOperationException(
                $"Strategy '{strategyType.FullName}' has an empty StrategyIndexAttribute value.");
        return attr.Index;
    }
}
