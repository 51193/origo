using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.V4Bench;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("# Genome v4 mock benchmark");
        CapabilityBenchmark();
        TaskBenchmark();
        EnergyTradeoffBenchmark();
        RegulationBenchmark();
        MutationBenchmark();
    }

    private static void CapabilityBenchmark()
    {
        Console.WriteLine("\n## Capability and cost");
        var rows = new List<(string Name, GenomeV4 Genome)>
        {
            ("simple reflex", GenomeV4Factory.CreateSimpleReflex()),
            ("graph 32 hidden", GenomeV4Factory.CreateGraphBrain(32, 32)),
            ("large body 512 organs", GenomeV4Factory.CreateLargeBody(512)),
            ("large logic brain 32 redundant", GenomeV4Factory.CreateLargeLogicBrain(32)),
        };
        Console.WriteLine("| variant | genes | nodes | edges | organs | maintenance/s | compile ms | codec bytes |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (Name, Genome) in rows)
        {
            var watch = Stopwatch.StartNew();
            var phenotype = GenomeV4Compiler.Compile(Genome, ExpressionContextV4.Neutral);
            watch.Stop();
            var bytes = GenomeV4Codec.Serialize(Genome).Length;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {Name} | {Genome.Genes.Count} | {phenotype.Brain.Nodes.Count} | {phenotype.Brain.Edges.Count} | {phenotype.Body.Organs.Count} | {phenotype.Energy.MaintenancePerSecond:0.000} | {watch.Elapsed.TotalMilliseconds:0.000} | {bytes} |"));
        }
    }

    private static void TaskBenchmark()
    {
        Console.WriteLine("\n## Mock non-linear decision task (AND of two sensors)");
        var cases = new (float Left, float Right, float Expected)[]
        {
            (0f, 0f, 0f),
            (1f, 0f, 0f),
            (0f, 1f, 0f),
            (1f, 1f, 1f),
        };
        var (Accuracy, Nodes, Edges, Maintenance) = EvaluateTask(GenomeV4Factory.CreateLogicTaskReflex(), cases);
        var graph = EvaluateTask(GenomeV4Factory.CreateLogicTaskBrain(), cases);
        Console.WriteLine("| controller | accuracy | nodes | edges | maintenance/s |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        Console.WriteLine($"| reflex-only | {Accuracy:P0} | {Nodes} | {Edges} | {Maintenance:0.000} |");
        Console.WriteLine($"| variable graph | {graph.Accuracy:P0} | {graph.Nodes} | {graph.Edges} | {graph.Maintenance:0.000} |");
    }

    private static (double Accuracy, int Nodes, int Edges, float Maintenance) EvaluateTask(
        GenomeV4 genome,
        (float Left, float Right, float Expected)[] cases)
    {
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var sensors = new float[16];
        var actions = new float[16];
        var correct = 0;
        foreach (var (Left, Right, Expected) in cases)
        {
            Array.Clear(sensors);
            Array.Clear(actions);
            sensors[(int)SensorKindV4.FoodLeft] = Left;
            sensors[(int)SensorKindV4.FoodRight] = Right;
            brain.Evaluate(sensors, actions);
            var predicted = actions[(int)ActionKindV4.Move] > 0.5f ? 1f : 0f;
            if (MathF.Abs(predicted - Expected) < 0.001f)
                correct++;
        }

        return (correct / (double)cases.Length, phenotype.Brain.Nodes.Count,
            phenotype.Brain.Edges.Count, phenotype.Energy.MaintenancePerSecond);
    }

    private static void EnergyTradeoffBenchmark()
    {
        Console.WriteLine("\n## Mock energy economy (decisions produce food; brain costs maintenance)");
        Console.WriteLine("Assumptions: one food opportunity per simulated second at the given rate;");
        Console.WriteLine("correct AND decision yields 18 energy; wrong decision yields 0; energy starts at 100.");
        var simple = GenomeV4Factory.CreateLogicTaskReflex();
        var large = GenomeV4Factory.CreateLargeLogicBrain(32);
        var rows = new List<(string Rate, double SimpleNet, double LargeNet, double SimpleEnd, double LargeEnd)>();
        foreach (var rate in new[] { 0.2, 0.5, 1.0, 2.0 })
        {
            var (Net, End) = SimulateDecisionEconomy(simple, rate, 3600);
            var largeResult = SimulateDecisionEconomy(large, rate, 3600);
            rows.Add(($"{rate:0.0}/s", Net, largeResult.Net,
                End, largeResult.End));
        }

        Console.WriteLine("| opportunity rate | reflex net/s | large-brain net/s | reflex end energy | large end energy |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach (var (Rate, SimpleNet, LargeNet, SimpleEnd, LargeEnd) in rows)
        {
            Console.WriteLine($"| {Rate} | {SimpleNet:0.000} | {LargeNet:0.000} | {SimpleEnd:0.0} | {LargeEnd:0.0} |");
        }
    }

    private static (double Net, double End) SimulateDecisionEconomy(
        GenomeV4 genome,
        double opportunityRate,
        int seconds)
    {
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var random = new Random(20261110);
        var sensors = new float[16];
        var actions = new float[16];
        var energy = 100d;
        var startEnergy = energy;
        for (var tick = 0; tick < seconds; tick++)
        {
            energy -= phenotype.Energy.MaintenancePerSecond;
            if (random.NextDouble() < opportunityRate)
            {
                var left = random.Next(2);
                var right = random.Next(2);
                Array.Clear(sensors);
                Array.Clear(actions);
                sensors[(int)SensorKindV4.FoodLeft] = left;
                sensors[(int)SensorKindV4.FoodRight] = right;
                brain.Evaluate(sensors, actions);
                var predicted = actions[(int)ActionKindV4.Move] > 0.5f;
                var correct = (left == 1 && right == 1) == predicted;
                if (correct)
                    energy += 18d;
            }

            if (energy < 0d)
                return ((energy - startEnergy) / seconds, energy);
        }

        return ((energy - startEnergy) / seconds, energy);
    }

    private static void RegulationBenchmark()
    {
        Console.WriteLine("\n## Regulatory expression");
        var genome = GenomeV4Factory.CreateConditionalBattery();
        var low = GenomeV4Compiler.Compile(genome, new ExpressionContextV4(0.2f, 0.5f, 0.5f));
        var high = GenomeV4Compiler.Compile(genome, new ExpressionContextV4(0.8f, 0.5f, 0.5f));
        Console.WriteLine($"low-food battery organs: {CountOrgans(low.Body, OrganKindV4.SpikeBattery)}");
        Console.WriteLine($"high-food battery organs: {CountOrgans(high.Body, OrganKindV4.SpikeBattery)}");
    }

    private static int CountOrgans(BodyPlanV4 body, OrganKindV4 kind)
    {
        var count = 0;
        foreach (var organ in body.Organs)
            if (organ.Kind == kind)
                count++;
        return count;
    }

    private static void MutationBenchmark()
    {
        Console.WriteLine("\n## Mutation growth");
        var random = new Random(20261110);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var maxGenes = genome.Genes.Count;
        var maxNeurons = 0;
        var maxOrgans = 0;
        var valid = 0;
        for (var i = 0; i < 2000; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random);
            maxGenes = Math.Max(maxGenes, genome.Genes.Count);
            maxNeurons = Math.Max(maxNeurons,
                genome.Genes.Count(gene => gene.Kind == GeneKind.Neuron));
            maxOrgans = Math.Max(maxOrgans,
                genome.Genes.Where(gene => gene.Kind == GeneKind.Organ)
                    .Sum(gene => gene.CopyCount));
            try
            {
                _ = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
                valid++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.IO.InvalidDataException)
            {
                // Some random graphs are not viable; mutation is allowed to
                // produce inviable offspring in an evolutionary simulation.
            }
        }

        Console.WriteLine($"after 2000 mutations: genes={genome.Genes.Count}, max genes={maxGenes}, " +
            $"max neurons={maxNeurons}, max organ copies={maxOrgans}, viable={valid}/2000, " +
            $"codec bytes={GenomeV4Codec.Serialize(genome).Length}");
    }
}
