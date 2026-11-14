using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4PhenotypeTests
{
    [Fact]
    public void SimpleReflex_CompilesToCheapDirectBrain()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateSimpleReflex(),
            ExpressionContextV4.Neutral);
        Assert.Single(phenotype.Body.Segments);
        Assert.Single(phenotype.Body.Organs);
        Assert.Equal(2, phenotype.Brain.Nodes.Count);
        Assert.Single(phenotype.Brain.Edges);
        Assert.True(phenotype.Energy.MaintenancePerSecond < 2.5f);
    }

    [Fact]
    public void SimpleReflex_EvaluatesDirectSensorToAction()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateSimpleReflex(),
            ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var sensors = new float[16];
        var actions = new float[16];
        sensors[(int)SensorKindV4.FoodAhead] = 0.75f;
        brain.Evaluate(sensors, actions);
        Assert.Equal(0.75f, actions[(int)ActionKindV4.Move], 4);
    }

    [Fact]
    public void NonLinearTask_RequiresAndSucceedsWithHiddenNode()
    {
        var reflex = GenomeV4Factory.CreateLogicTaskReflex();
        var graph = GenomeV4Factory.CreateLogicTaskBrain();
        Assert.Equal(0.5, EvaluateAndAccuracy(reflex), 3);
        Assert.Equal(1.0, EvaluateAndAccuracy(graph), 3);
    }

    private static double EvaluateAndAccuracy(GenomeV4 genome)
    {
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var sensors = new float[16];
        var actions = new float[16];
        var cases = new (float Left, float Right, bool Expected)[]
        {
            (0f, 0f, false),
            (1f, 0f, false),
            (0f, 1f, false),
            (1f, 1f, true),
        };
        var correct = 0;
        foreach (var (Left, Right, Expected) in cases)
        {
            Array.Clear(sensors);
            Array.Clear(actions);
            sensors[(int)SensorKindV4.FoodLeft] = Left;
            sensors[(int)SensorKindV4.FoodRight] = Right;
            brain.Evaluate(sensors, actions);
            var predicted = actions[(int)ActionKindV4.Move] > 0.5f;
            if (predicted == Expected)
                correct++;
        }

        return correct / 4d;
    }

    [Fact]
    public void VariableGraphBrain_HasMoreNodesAndHigherBrainCost()
    {
        var simple = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateSimpleReflex(), ExpressionContextV4.Neutral);
        var complex = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateGraphBrain(32, 32), ExpressionContextV4.Neutral);
        Assert.True(complex.Brain.Nodes.Count > simple.Brain.Nodes.Count + 20);
        Assert.True(complex.Brain.Edges.Count > simple.Brain.Edges.Count);
        Assert.True(complex.Energy.BrainCost > simple.Energy.BrainCost * 4f);
    }

    [Fact]
    public void BodyPlan_HasNoFixedSlotLimit()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateLargeBody(512), ExpressionContextV4.Neutral);
        Assert.Equal(512, phenotype.Body.Organs.Count);
        Assert.All(phenotype.Body.Organs, organ => Assert.Equal(OrganKindV4.Armor, organ.Kind));
    }

    [Fact]
    public void RegulatoryExpression_ChangesOrganUnderDifferentContexts()
    {
        var genome = GenomeV4Factory.CreateConditionalBattery();
        var lowFood = GenomeV4Compiler.Compile(genome, new ExpressionContextV4(0.2f, 0.5f, 0.5f));
        var highFood = GenomeV4Compiler.Compile(genome, new ExpressionContextV4(0.8f, 0.5f, 0.5f));
        Assert.Contains(lowFood.Body.Organs, static organ => organ.Kind == OrganKindV4.SpikeBattery);
        Assert.DoesNotContain(highFood.Body.Organs, static organ => organ.Kind == OrganKindV4.SpikeBattery);
    }

    [Fact]
    public void CyclicBrain_IsRejected()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Interneuron, 0, ActivationKindV4.Tanh, 0f)),
            new(3, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Interneuron, 0, ActivationKindV4.Tanh, 0f)),
            new(4, GeneKind.Synapse, 1, 1f, GenePayloadV4.Synapse(2, 3, 1f)),
            new(5, GeneKind.Synapse, 1, 1f, GenePayloadV4.Synapse(3, 2, 1f)),
        };
        var genome = new GenomeV4(genes, 6);
        Assert.Throws<System.IO.InvalidDataException>(() =>
            GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral));
    }
}
