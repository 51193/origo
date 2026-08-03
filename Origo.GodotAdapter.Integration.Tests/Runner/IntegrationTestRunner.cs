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
    private List<TestResult> _results = [];
    private readonly Queue<DeferredTestEntry> _deferredQueue = new();
    private DeferredTestEntry? _currentDeferred;
    private int _deferredFrameCount;
    private bool _allInstantDone;

    public override void _Ready()
    {
        _results = RunInstantTests();
        _allInstantDone = true;

        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                && t.GetMethods().Any(m => m.GetCustomAttribute<DeferredTestAttribute>() != null))
            .ToList();

        foreach (var type in types)
        {
            object? instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _results.Add(new TestResult
                {
                    Name = $"{type.Name}.ctor",
                    Passed = false,
                    Error = $"Failed to instantiate deferred fixture: {ex}",
                    DurationMs = 0
                });
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<DeferredTestAttribute>() != null)
                .ToList();

            foreach (var method in methods)
            {
                _deferredQueue.Enqueue(new DeferredTestEntry
                {
                    TypeName = type.Name,
                    MethodName = method.Name,
                    Instance = instance!,
                    Method = method
                });
            }
        }

        if (_deferredQueue.Count == 0)
        {
            FlushResults();
            return;
        }

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_currentDeferred == null)
        {
            if (_deferredQueue.Count == 0)
            {
                SetProcess(false);
                FlushResults();
                return;
            }
            _currentDeferred = _deferredQueue.Dequeue();
            _deferredFrameCount = 0;

            if (_currentDeferred.Instance is IDeferredTestFixture fixture)
            {
                try
                {
                    fixture.Setup();
                }
                catch (Exception ex)
                {
                    _results.Add(new TestResult
                    {
                        Name = $"{_currentDeferred.TypeName}.{_currentDeferred.MethodName}.Setup",
                        Passed = false,
                        Error = ex is TargetInvocationException tie ? (tie.InnerException?.ToString() ?? ex.ToString()) : ex.ToString(),
                        DurationMs = 0
                    });
                    _currentDeferred = null;
                    return;
                }
            }

            _deferredFrameCount = 1;
            return;
        }

        _deferredFrameCount++;

        if (_currentDeferred.Instance is IDeferredTestFixture deferredFixture)
        {
            try
            {
                deferredFixture.AdvanceFrame();
            }
            catch (Exception ex)
            {
                _results.Add(new TestResult
                {
                    Name = $"{_currentDeferred.TypeName}.{_currentDeferred.MethodName}.Advance",
                    Passed = false,
                    Error = ex is TargetInvocationException tie ? (tie.InnerException?.ToString() ?? ex.ToString()) : ex.ToString(),
                    DurationMs = 0
                });
                _currentDeferred = null;
                return;
            }

            if (!deferredFixture.IsComplete)
                return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            _currentDeferred.Method.Invoke(_currentDeferred.Instance, null);
            sw.Stop();
            _results.Add(new TestResult
            {
                Name = $"{_currentDeferred.TypeName}.{_currentDeferred.MethodName}",
                Passed = true,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
        catch (TargetInvocationException ex)
        {
            sw.Stop();
            _results.Add(new TestResult
            {
                Name = $"{_currentDeferred.TypeName}.{_currentDeferred.MethodName}",
                Passed = false,
                Error = ex.InnerException?.ToString() ?? ex.ToString(),
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _results.Add(new TestResult
            {
                Name = $"{_currentDeferred.TypeName}.{_currentDeferred.MethodName}",
                Passed = false,
                Error = ex.ToString(),
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }

        if (_currentDeferred.Instance is IDisposable disposable)
        {
            try { disposable.Dispose(); }
            catch (Exception ex) { GD.PrintErr($"IntegrationTestRunner: Dispose failed for deferred test: {ex.Message}"); }
        }

        _currentDeferred = null;
    }

    private static List<TestResult> RunInstantTests()
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
                catch (Exception ex) { GD.PrintErr($"IntegrationTestRunner: Dispose failed for instant test: {ex.Message}"); }
            }
        }

        return results;
    }

    private void FlushResults()
    {
        if (_results.Count == 0)
        {
            GD.PrintErr("INTEGRATION_TEST_RESULTS: 0 total — no tests were discovered. Failing the run.");
            OutputResults(_results);
            GetTree().Quit(1);
            return;
        }

        OutputResults(_results);

        var allPassed = _results.All(r => r.Passed);
        var exitCode = allPassed ? 0 : 1;
        GetTree().Quit(exitCode);
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

    public static void AssertNull(object? value, string name)
    {
        if (value is not null)
            throw new InvalidOperationException($"Assertion failed: {name} should be null, but was {value}.");
    }

    public static void AssertContains(string expectedSubstring, string actual, string name)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Assertion failed for '{name}': expected to contain \"{expectedSubstring}\", but got \"{actual}\".");
    }

    public static void AssertEmpty<T>(IEnumerable<T> collection, string name)
    {
        if (collection.Any())
            throw new InvalidOperationException($"Assertion failed: {name} should be empty, but has {collection.Count()} element(s).");
    }

    public static void AssertNotEmpty<T>(IEnumerable<T> collection, string name)
    {
        if (!collection.Any())
            throw new InvalidOperationException($"Assertion failed: {name} should not be empty.");
    }

    private sealed class DeferredTestEntry
    {
        public string TypeName { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;
        public object Instance { get; init; } = null!;
        public MethodInfo Method { get; init; } = null!;
    }
}
