using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>spawn &lt;name&gt; &lt;template&gt;</c> — spawn an SND entity from a template.</summary>
internal sealed class SpawnTemplateCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public SpawnTemplateCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "spawn";
    public override string HelpText => "spawn <name> <template> — spawn an SND entity from a template. Accepts positional or named arguments name=... template=...";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        if (!TryGetSpawnArgs(invocation, out var entityName, out var templateKey, out var err))
        {
            errorMessage = err;
            return false;
        }

        var template = _runtime.SndWorld.ResolveTemplate(templateKey);
        template.Name = entityName;

        var session = _runtime.SessionManager.ForegroundSession;
        if (session is null)
        {
            errorMessage = "No foreground session — cannot spawn entities.";
            return false;
        }

        session.Spawn(template);

        var msg = $"Spawned '{entityName}' from template '{templateKey}'.";
        outputChannel.Publish(msg);
        errorMessage = null;
        return true;
    }

    private static bool TryGetSpawnArgs(
        CommandInvocation invocation,
        out string entityName,
        out string templateKey,
        out string? error)
    {
        entityName = string.Empty;
        templateKey = string.Empty;

        if (invocation.NamedArgs.Count > 0)
            return TryParseNamedSpawnArgs(invocation, out entityName, out templateKey, out error);

        if (invocation.PositionalArgs.Count != 2)
        {
            error = "Usage: spawn <name> <template>  OR  spawn name=<name> template=<template>";
            return false;
        }

        return TryParsePositionalSpawnArgs(invocation, out entityName, out templateKey, out error);
    }

    private static bool TryParseNamedSpawnArgs(
        CommandInvocation invocation,
        out string entityName,
        out string templateKey,
        out string? error)
    {
        if (invocation.PositionalArgs.Count > 0)
        {
            entityName = string.Empty;
            templateKey = string.Empty;
            error = "Cannot mix named and positional arguments for 'spawn'.";
            return false;
        }

        if (!invocation.NamedArgs.TryGetValue("name", out var n) ||
            string.IsNullOrWhiteSpace(n))
        {
            entityName = string.Empty;
            templateKey = string.Empty;
            error = "Missing or invalid 'name=' for 'spawn'.";
            return false;
        }

        if (!invocation.NamedArgs.TryGetValue("template", out var t) ||
            string.IsNullOrWhiteSpace(t))
        {
            entityName = string.Empty;
            templateKey = string.Empty;
            error = "Missing or invalid 'template=' for 'spawn'.";
            return false;
        }

        entityName = n.Trim();
        templateKey = t.Trim();
        error = null;
        return true;
    }

    private static bool TryParsePositionalSpawnArgs(
        CommandInvocation invocation,
        out string entityName,
        out string templateKey,
        out string? error)
    {
        entityName = invocation.PositionalArgs[0].Trim();
        templateKey = invocation.PositionalArgs[1].Trim();

        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(templateKey))
        {
            error = "Name and template must be non-empty.";
            return false;
        }

        error = null;
        return true;
    }
}
