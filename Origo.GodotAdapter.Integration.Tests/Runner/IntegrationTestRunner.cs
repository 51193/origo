using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;

namespace Origo.GodotAdapter.Integration.Tests.Runner;

[GlobalClass]
public partial class IntegrationTestRunner : Node
{
    public override void _Ready()
    {
        var results = RunAllTests();
        OutputResults(results);

        var allPassed = results.All(r => r.Passed);
        var exitCode = allPassed ? 0 : 1;
        GetTree().Quit(exitCode);
    }

    private static List<TestResult> RunAllTests()
    {
        var results = new List<TestResult>();
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<IntegrationTestAttribute>() != null))
            .ToList();

        foreach (var type in types)
        {
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                results.Add(new TestResult
                {
                    Name = $"{type.Name}.ctor",
                    Passed = false,
                    Error = $"Failed to instantiate test fixture: {ex}",
                    DurationMs = 0
                });
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<IntegrationTestAttribute>() != null)
                .ToList();

            foreach (var method in methods)
            {
                if (method.GetParameters().Length > 0)
                {
                    results.Add(new TestResult
                    {
                        Name = $"{type.Name}.{method.Name}",
                        Passed = false,
                        Error = "Integration test methods must not have parameters.",
                        DurationMs = 0
                    });
                    continue;
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    method.Invoke(instance, null);
                    sw.Stop();
                    results.Add(new TestResult
                    {
                        Name = $"{type.Name}.{method.Name}",
                        Passed = true,
                        DurationMs = sw.Elapsed.TotalMilliseconds
                    });
                }
                catch (TargetInvocationException ex)
                {
                    sw.Stop();
                    results.Add(new TestResult
                    {
                        Name = $"{type.Name}.{method.Name}",
                        Passed = false,
                        Error = ex.InnerException?.ToString() ?? ex.ToString(),
                        DurationMs = sw.Elapsed.TotalMilliseconds
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new TestResult
                    {
                        Name = $"{type.Name}.{method.Name}",
                        Passed = false,
                        Error = ex.ToString(),
                        DurationMs = sw.Elapsed.TotalMilliseconds
                    });
                }
            }

            if (instance is IDisposable disposable)
            {
                try { disposable.Dispose(); }
                catch { /* disposal failure does not affect test results */ }
            }
        }

        return results;
    }

    private static void OutputResults(List<TestResult> results)
    {
        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);
        var total = results.Count;

        GD.Print($"INTEGRATION_TEST_RESULTS: {total} total, {passed} passed, {failed} failed");

        foreach (var result in results)
        {
            var status = result.Passed ? "PASS" : "FAIL";
            var line = $"{status} {result.Name} ({result.DurationMs:F1}ms)";
            if (!result.Passed && result.Error != null)
            {
                line += $"\n    {result.Error.Replace("\n", "\n    ")}";
            }
            GD.Print(line);
        }

        GD.Print($"INTEGRATION_TEST_SUMMARY: {passed}/{total} passed");
    }

    public static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {message}");
    }

    public static void AssertNotNull(object? value, string name)
    {
        if (value is null)
            throw new InvalidOperationException($"Assertion failed: {name} should not be null.");
    }

    public static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Assertion failed: {message}. Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new InvalidOperationException(
            $"Assertion failed: {message}. Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    public static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Assertion failed for '{name}': expected {expected}, got {actual}");
    }
}
