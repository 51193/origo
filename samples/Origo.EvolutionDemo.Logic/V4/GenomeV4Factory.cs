using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Initial V4 genome templates used by mock scenarios and the demo migration.</summary>
public static class GenomeV4Factory
{
    /// <summary>Minimal body plus one direct food-to-move reflex.</summary>
    public static GenomeV4 CreateSimpleReflex()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.Vision, 0.5f)),
            new(3, GeneKind.Reflex, 1, 1f, GenePayloadV4.Reflex(SensorKindV4.FoodAhead, ActionKindV4.Move, 1f)),
        };
        return new GenomeV4(genes, 4);
    }

    /// <summary>
    ///     A small variable graph with a configurable hidden layer count. The
    ///     graph has no fixed hidden-slot array; nodes and edges are genes.
    /// </summary>
    public static GenomeV4 CreateGraphBrain(int hiddenNeurons, int extraSynapses = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hiddenNeurons);
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.Vision, 0.5f)),
            new(3, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Sensor, (ushort)SensorKindV4.FoodAhead,
                    ActivationKindV4.Linear, 0f)),
            new(4, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Output, (ushort)ActionKindV4.Move,
                    ActivationKindV4.Tanh, 0f)),
        };
        var next = 5u;
        var hiddenIds = new List<uint>();
        for (var i = 0; i < hiddenNeurons; i++)
        {
            var id = next++;
            hiddenIds.Add(id);
            genes.Add(new GeneV4(id, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Interneuron, 0, ActivationKindV4.Tanh, 0f)));
            genes.Add(new GeneV4(next++, GeneKind.Synapse, 1, 1f,
                GenePayloadV4.Synapse(3, id, 1f)));
            genes.Add(new GeneV4(next++, GeneKind.Synapse, 1, 1f,
                GenePayloadV4.Synapse(id, 4, 1f)));
        }

        for (var i = 1; i < hiddenIds.Count && i <= extraSynapses; i++)
        {
            var from = hiddenIds[i - 1];
            var to = hiddenIds[i];
            genes.Add(new GeneV4(next++, GeneKind.Synapse, 1, 1f,
                GenePayloadV4.Synapse(from, to, 0.1f * i)));
        }

        return new GenomeV4(genes, next);
    }

    /// <summary>
    ///     A hand-built non-linear task controller: two binary sensor nodes feed
    ///     one ReLU hidden node that implements AND. A pure reflex genome cannot
    ///     represent this threshold with direct sensor-to-output edges.
    /// </summary>
    public static GenomeV4 CreateLogicTaskBrain()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Sensor, (ushort)SensorKindV4.FoodLeft,
                    ActivationKindV4.Linear, 0f)),
            new(3, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Sensor, (ushort)SensorKindV4.FoodRight,
                    ActivationKindV4.Linear, 0f)),
            new(4, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Interneuron, 0, ActivationKindV4.Relu, -1f)),
            new(5, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Output, (ushort)ActionKindV4.Move,
                    ActivationKindV4.Linear, 0f)),
            new(6, GeneKind.Synapse, 1, 1f, GenePayloadV4.Synapse(2, 4, 1f)),
            new(7, GeneKind.Synapse, 1, 1f, GenePayloadV4.Synapse(3, 4, 1f)),
            new(8, GeneKind.Synapse, 1, 1f, GenePayloadV4.Synapse(4, 5, 1f)),
        };
        return new GenomeV4(genes, 9);
    }

    /// <summary>
    ///     The same non-linear AND controller plus redundant hidden neurons.
    ///     This makes brain cost variable while keeping the decision function
    ///     fixed, so the energy trade-off can be measured in isolation.
    /// </summary>
    public static GenomeV4 CreateLargeLogicBrain(int extraHiddenNeurons)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(extraHiddenNeurons);
        var genome = CreateLogicTaskBrain();
        var next = genome.NextGeneId;
        for (var i = 0; i < extraHiddenNeurons; i++)
        {
            genome.Genes.Add(new GeneV4(
                next++,
                GeneKind.Neuron,
                1,
                1f,
                GenePayloadV4.Neuron(NeuronKindV4.Interneuron, 0, ActivationKindV4.Tanh, 0f)));
        }

        genome.NextGeneId = next;
        return genome;
    }

    /// <summary>Two direct sensor-to-move reflex genes: a fair reflex-only control.</summary>
    public static GenomeV4 CreateLogicTaskReflex()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Reflex, 1, 1f,
                GenePayloadV4.Reflex(SensorKindV4.FoodLeft, ActionKindV4.Move, 1f)),
            new(3, GeneKind.Reflex, 1, 1f,
                GenePayloadV4.Reflex(SensorKindV4.FoodRight, ActionKindV4.Move, 1f)),
        };
        return new GenomeV4(genes, 4);
    }

    /// <summary>A conditional spike battery expressed only when food is low.</summary>
    public static GenomeV4 CreateConditionalBattery()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.Vision, 0.5f)),
            new(3, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.SpikeBattery, 1f),
                [new RegulationV4(ConditionKindV4.FoodLow, 0.4f)]),
        };
        return new GenomeV4(genes, 4);
    }

    /// <summary>A body with many organ copies and no fixed organ slot array.</summary>
    public static GenomeV4 CreateLargeBody(int organCopies)
    {
        if (organCopies is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(organCopies));
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(2f, 1f)),
        };
        var next = 2u;
        var copies = organCopies;
        while (copies > 0)
        {
            var batch = Math.Min(copies, 32);
            genes.Add(new GeneV4(next++, GeneKind.Organ, (byte)batch, 1f,
                GenePayloadV4.Organ(OrganKindV4.Armor, 0.5f)));
            copies -= batch;
        }

        return new GenomeV4(genes, next);
    }
}
