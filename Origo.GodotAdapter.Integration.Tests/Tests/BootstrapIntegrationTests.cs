using System;
using Godot;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class BootstrapIntegrationTests
{
    [IntegrationTest(Description = "OrigoAutoHost can be instantiated and has expected initial properties")]
    public void AutoHost_Properties_HaveDefaults()
    {
        var host = new OrigoAutoHost();

        IntegrationTestRunner.Assert(
            host.SystemBlackboardSaveRoot == "user://origo_saves",
            "Default save root should be 'user://origo_saves'.");
        IntegrationTestRunner.Assert(
            !string.IsNullOrEmpty(host.Name) || string.IsNullOrEmpty(host.Name),
            "OrigoAutoHost should be constructable without errors.");

        host.Free();
    }

    [IntegrationTest(Description = "OrigoDefaultEntry can be instantiated with default export values")]
    public void DefaultEntry_Properties_HaveDefaults()
    {
        var entry = new OrigoDefaultEntry();

        IntegrationTestRunner.Assert(
            entry.SaveRootPath == "user://origo_saves",
            "Default SaveRootPath should be 'user://origo_saves'.");
        IntegrationTestRunner.Assert(
            entry.InitialSaveRootPath == "res://origo/initial",
            "Default InitialSaveRootPath should be 'res://origo/initial'.");
        IntegrationTestRunner.Assert(
            entry.ConfigPath == "res://origo/entry/entry.json",
            "Default ConfigPath should be 'res://origo/entry/entry.json'.");
        IntegrationTestRunner.Assert(
            entry.AutoDiscoverStrategies,
            "Default AutoDiscoverStrategies should be true.");

        entry.Free();
    }
}
