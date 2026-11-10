using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Environment values used to evaluate regulatory expression.</summary>
public readonly record struct ExpressionContextV4(
    float FoodLevel,
    float PredatorPressure,
    float EnergyLevel)
{
    /// <summary>A neutral context for unconditional genome templates.</summary>
    public static ExpressionContextV4 Neutral => new(0.5f, 0.5f, 0.5f);
}

/// <summary>One expressed body segment.</summary>
public readonly record struct BodySegmentV4(uint GeneId, float Length, float Width);

/// <summary>One expressed organ instance. Copy count expands this with no fixed slot array.</summary>
public readonly record struct OrganInstanceV4(
    uint GeneId,
    OrganKindV4 Kind,
    int CopyIndex,
    float Size);

/// <summary>The expressed body: arbitrary segment and organ lists.</summary>
public sealed record BodyPlanV4(
    IReadOnlyList<BodySegmentV4> Segments,
    IReadOnlyList<OrganInstanceV4> Organs,
    IReadOnlyDictionary<LifeTraitKindV4, float> LifeTraits);

/// <summary>One node in the expressed neural graph.</summary>
public readonly record struct NeuralNodeV4(
    ulong Id,
    NeuronKindV4 Kind,
    ushort InterfaceId,
    ActivationKindV4 Activation,
    float Bias);

/// <summary>One directed connection in the expressed neural graph.</summary>
public readonly record struct NeuralEdgeV4(ulong From, ulong To, float Weight);

/// <summary>The expressed neural graph before runtime compilation.</summary>
public sealed record NeuralGraphV4(
    IReadOnlyList<NeuralNodeV4> Nodes,
    IReadOnlyList<NeuralEdgeV4> Edges);

/// <summary>Energy accounting for one expressed phenotype.</summary>
public readonly record struct EnergyBudgetV4(
    float BodyCost,
    float BrainCost,
    float GenomeCost,
    float MaintenancePerSecond)
{
    public float TotalComplexityCost => BodyCost + BrainCost + GenomeCost;
}

/// <summary>The full compiled phenotype.</summary>
public sealed record PhenotypeV4(
    GenomeV4 Genome,
    BodyPlanV4 Body,
    NeuralGraphV4 Brain,
    EnergyBudgetV4 Energy,
    IReadOnlyList<ushort> SensorPorts,
    IReadOnlyList<ushort> ActionPorts);

/// <summary>Converts expressed V4 genes into body and neural graphs.</summary>
public static class GenomeV4Compiler
{
    private const int MaxExpressedOrgans = 4096;
    private const int MaxExpressedNeurons = 4096;
    private const int MaxExpressedSynapses = 16384;

    private const ulong SensorNodeBase = 0x1000_0000_0000_0000UL;
    private const ulong ActionNodeBase = 0x2000_0000_0000_0000UL;

    /// <summary>Compiles a genome under one expression context.</summary>
    public static PhenotypeV4 Compile(GenomeV4 genome, ExpressionContextV4 context)
    {
        GenomeV4Validator.Validate(genome);
        var active = genome.Genes.Where(gene => IsActive(gene, context)).ToList();
        var segments = new List<BodySegmentV4>();
        var organs = new List<OrganInstanceV4>();
        var traits = new Dictionary<LifeTraitKindV4, float>();
        foreach (var gene in active)
        {
            switch (gene.Kind)
            {
                case GeneKind.BodySegment:
                    {
                        var (length, width) = GenePayloadV4.ReadBodySegment(gene.Payload);
                        for (var copy = 0; copy < gene.CopyCount; copy++)
                            segments.Add(new BodySegmentV4(gene.Id, length, width));
                        break;
                    }
                case GeneKind.Organ:
                    {
                        var (organKind, organSize) = GenePayloadV4.ReadOrgan(gene.Payload);
                        for (var copy = 0; copy < gene.CopyCount && organs.Count < MaxExpressedOrgans; copy++)
                            organs.Add(new OrganInstanceV4(gene.Id, organKind, copy, organSize));
                        break;
                    }
                case GeneKind.LifeHistory:
                    {
                        var (trait, value) = GenePayloadV4.ReadLifeHistory(gene.Payload);
                        traits[trait] = value;
                        break;
                    }
            }
        }

        if (segments.Count == 0)
            throw new InvalidOperationException("Expressed body has no segment.");

        var (nodes, edges, sensorNodes, actionNodes) = CompileNeural(active);
        var bodyCost = segments.Count * 1.0f
            + organs.Sum(static organ => 0.25f + (0.75f * MathF.Max(0.1f, organ.Size)));
        var brainCost = (nodes.Count * 0.15f) + (edges.Count * 0.03f);
        var genomeCost = genome.Genes.Count * 0.002f;
        var maintenance = 0.5f + bodyCost + brainCost + genomeCost;
        var body = new BodyPlanV4(segments, organs, traits);
        var brain = new NeuralGraphV4(nodes, edges);
        var energy = new EnergyBudgetV4(bodyCost, brainCost, genomeCost, maintenance);
        var sensorPorts = BuildSensorPorts(organs);
        var actionPorts = BuildActionPorts(organs);
        _ = sensorNodes;
        _ = actionNodes;
        return new PhenotypeV4(genome, body, brain, energy, sensorPorts, actionPorts);
    }

    private static (
        List<NeuralNodeV4> Nodes,
        List<NeuralEdgeV4> Edges,
        Dictionary<SensorKindV4, ulong> SensorNodes,
        Dictionary<ActionKindV4, ulong> ActionNodes) CompileNeural(
        IReadOnlyList<GeneV4> active)
    {
        var nodes = new Dictionary<ulong, NeuralNodeV4>();
        var edges = new Dictionary<(ulong From, ulong To), float>();
        var sensorNodes = new Dictionary<SensorKindV4, ulong>();
        var actionNodes = new Dictionary<ActionKindV4, ulong>();
        var nodeByGene = new Dictionary<uint, ulong>();

        ulong SensorNode(SensorKindV4 sensor)
        {
            if (!sensorNodes.TryGetValue(sensor, out var id))
            {
                id = SensorNodeBase | (ushort)sensor;
                sensorNodes[sensor] = id;
                nodes[id] = new NeuralNodeV4(id, NeuronKindV4.Sensor, (ushort)sensor,
                    ActivationKindV4.Linear, 0f);
            }

            return id;
        }

        ulong ActionNode(ActionKindV4 action)
        {
            if (!actionNodes.TryGetValue(action, out var id))
            {
                id = ActionNodeBase | (ushort)action;
                actionNodes[action] = id;
                nodes[id] = new NeuralNodeV4(id, NeuronKindV4.Output, (ushort)action,
                    ActivationKindV4.Linear, 0f);
            }

            return id;
        }

        void AddEdge(ulong from, ulong to, float weight)
        {
            var key = (from, to);
            edges[key] = edges.TryGetValue(key, out var current) ? current + weight : weight;
        }

        foreach (var gene in active)
        {
            if (gene.Kind != GeneKind.Neuron)
                continue;
            if (nodes.Count >= MaxExpressedNeurons)
                throw new InvalidOperationException("Expressed neural graph exceeds the safety cap.");
            var (neuronKind, interfaceId, activation, bias) = GenePayloadV4.ReadNeuron(gene.Payload);
            var nodeId = neuronKind switch
            {
                NeuronKindV4.Sensor => SensorNode(interfaceId is > 0
                    ? (SensorKindV4)interfaceId
                    : throw new InvalidDataException("Sensor neuron has no interface.")),
                NeuronKindV4.Output => ActionNode(interfaceId is > 0
                    ? (ActionKindV4)interfaceId
                    : throw new InvalidDataException("Output neuron has no interface.")),
                _ => gene.Id,
            };
            nodeByGene[gene.Id] = nodeId;
            if (neuronKind != NeuronKindV4.Interneuron
                && nodes.TryGetValue(nodeId, out var existing))
            {
                // Several genes may contribute to the same interface node; merge
                // their expressed bias without consuming a fixed sensor slot.
                nodes[nodeId] = existing with { Bias = existing.Bias + bias };
            }
            else
            {
                nodes[nodeId] = new NeuralNodeV4(
                    nodeId,
                    neuronKind,
                    interfaceId,
                    neuronKind == NeuronKindV4.Interneuron
                        ? activation
                        : ActivationKindV4.Linear,
                    bias);
            }
        }

        foreach (var gene in active)
        {
            if (gene.Kind == GeneKind.Reflex)
            {
                var (sensor, action, gain) = GenePayloadV4.ReadReflex(gene.Payload);
                AddEdge(SensorNode(sensor), ActionNode(action), gain);
                continue;
            }

            if (gene.Kind != GeneKind.Synapse)
                continue;
            if (edges.Count >= MaxExpressedSynapses)
                throw new InvalidOperationException("Expressed neural graph exceeds the safety cap.");
            var (fromGeneId, toGeneId, weight) = GenePayloadV4.ReadSynapse(gene.Payload);
            if (!nodeByGene.TryGetValue(fromGeneId, out var from)
                || !nodeByGene.TryGetValue(toGeneId, out var to))
                throw new InvalidDataException("Synapse references an unexpressed neuron.");
            AddEdge(from, to, weight);
        }

        var edgeList = edges.Select(static pair => new NeuralEdgeV4(pair.Key.From, pair.Key.To, pair.Value))
            .ToList();
        EnsureAcyclic(edgeList);
        var nodeList = nodes.Values
            .OrderBy(static node => node.Id)
            .ToList();
        return (nodeList, edgeList, sensorNodes, actionNodes);
    }

    private static void EnsureAcyclic(IReadOnlyList<NeuralEdgeV4> edges)
    {
        var incoming = new Dictionary<ulong, int>();
        var outgoing = new Dictionary<ulong, List<ulong>>();
        foreach (var edge in edges)
        {
            incoming[edge.To] = incoming.GetValueOrDefault(edge.To) + 1;
            incoming.TryAdd(edge.From, 0);
            if (!outgoing.TryGetValue(edge.From, out var list))
            {
                list = [];
                outgoing[edge.From] = list;
            }

            list.Add(edge.To);
        }

        var ready = new Queue<ulong>(incoming.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
        var visited = 0;
        while (ready.Count > 0)
        {
            var node = ready.Dequeue();
            visited++;
            if (!outgoing.TryGetValue(node, out var targets))
                continue;
            foreach (var target in targets)
            {
                incoming[target]--;
                if (incoming[target] == 0)
                    ready.Enqueue(target);
            }
        }

        if (visited != incoming.Count)
            throw new InvalidDataException("Expressed neural graph contains a cycle.");
    }

    private static List<ushort> BuildSensorPorts(IReadOnlyList<OrganInstanceV4> organs)
    {
        var ports = new List<ushort>
        {
            (ushort)SensorKindV4.WallAhead,
            (ushort)SensorKindV4.Energy,
        };
        if (organs.Any(static organ => organ.Kind == OrganKindV4.Vision))
        {
            ports.Add((ushort)SensorKindV4.FoodAhead);
            ports.Add((ushort)SensorKindV4.FoodLeft);
            ports.Add((ushort)SensorKindV4.FoodRight);
        }

        if (organs.Any(static organ => organ.Kind == OrganKindV4.CreatureSense))
            ports.Add((ushort)SensorKindV4.CreatureAhead);
        if (organs.Any(static organ => organ.Kind == OrganKindV4.Rhythm))
        {
            ports.Add((ushort)SensorKindV4.Sin);
            ports.Add((ushort)SensorKindV4.Cos);
        }

        if (organs.Any(static organ => organ.Kind == OrganKindV4.CarrionSense))
            ports.Add((ushort)SensorKindV4.CorpseAhead);
        return ports;
    }

    private static List<ushort> BuildActionPorts(IReadOnlyList<OrganInstanceV4> organs)
    {
        var ports = new List<ushort>
        {
            (ushort)ActionKindV4.Move,
            (ushort)ActionKindV4.Turn,
        };
        if (organs.Any(static organ => organ.Kind == OrganKindV4.SpikeOrgan))
        {
            ports.Add((ushort)ActionKindV4.Spike);
            ports.Add((ushort)ActionKindV4.Aim);
        }

        return ports;
    }

    /// <summary>Compiles a phenotype's neural graph for repeated runtime evaluation.</summary>
    public static CompiledBrainV4 CompileBrain(PhenotypeV4 phenotype)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        return new CompiledBrainV4(phenotype.Brain);
    }

    private static bool IsActive(GeneV4 gene, ExpressionContextV4 context)
    {
        if (gene.Expression <= 0.001f)
            return false;
        foreach (var regulation in gene.Regulators)
        {
            var passed = regulation.Condition switch
            {
                ConditionKindV4.Always => true,
                ConditionKindV4.FoodLow => context.FoodLevel < regulation.Threshold,
                ConditionKindV4.PredatorNear => context.PredatorPressure > regulation.Threshold,
                ConditionKindV4.EnergyLow => context.EnergyLevel < regulation.Threshold,
                _ => false,
            };
            if (!passed)
                return false;
        }

        return true;
    }

    /// <summary>Canonical sensor node ID for a sensor interface.</summary>
    public static ulong SensorNodeId(SensorKindV4 sensor) => SensorNodeBase | (ushort)sensor;

    /// <summary>Canonical action node ID for an action interface.</summary>
    public static ulong ActionNodeId(ActionKindV4 action) => ActionNodeBase | (ushort)action;
}

/// <summary>Runtime evaluator for a compiled V4 neural graph.</summary>
public sealed class CompiledBrainV4
{
    private readonly NeuralNodeV4[] _nodes;
    private readonly int[] _incomingStart;
    private readonly NeuralEdgeV4[] _incoming;
    private readonly Dictionary<ulong, int> _indexById;
    private readonly float[] _values;

    internal CompiledBrainV4(NeuralGraphV4 graph)
    {
        var inputNodes = graph.Nodes.ToArray();
        var inputEdges = graph.Edges.ToArray();
        _indexById = inputNodes.Select((node, index) => (node.Id, index))
            .ToDictionary(static pair => pair.Id, static pair => pair.index);
        var outgoing = new List<int>[inputNodes.Length];
        var indegree = new int[inputNodes.Length];
        for (var i = 0; i < inputNodes.Length; i++)
            outgoing[i] = [];
        foreach (var edge in inputEdges)
        {
            if (!_indexById.TryGetValue(edge.From, out var fromIndex)
                || !_indexById.TryGetValue(edge.To, out var toIndex))
                throw new InvalidDataException("Neural edge references an unknown node.");
            outgoing[fromIndex].Add(toIndex);
            indegree[toIndex]++;
        }

        var ready = new Queue<int>();
        for (var i = 0; i < indegree.Length; i++)
            if (indegree[i] == 0)
                ready.Enqueue(i);
        var order = new List<int>(inputNodes.Length);
        while (ready.Count > 0)
        {
            var index = ready.Dequeue();
            order.Add(index);
            foreach (var target in outgoing[index])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                    ready.Enqueue(target);
            }
        }

        if (order.Count != inputNodes.Length)
            throw new InvalidDataException("Expressed neural graph contains a cycle.");

        _nodes = new NeuralNodeV4[order.Count];
        var oldToNew = new int[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var oldIndex = order[i];
            _nodes[i] = inputNodes[oldIndex];
            oldToNew[oldIndex] = i;
        }

        var inputIndexById = _indexById;
        var fromIndices = new int[inputEdges.Length];
        var toIndices = new int[inputEdges.Length];
        var incomingCounts = new int[_nodes.Length];
        for (var i = 0; i < inputEdges.Length; i++)
        {
            var edge = inputEdges[i];
            var fromOld = inputIndexById[edge.From];
            var toOld = inputIndexById[edge.To];
            fromIndices[i] = oldToNew[fromOld];
            toIndices[i] = oldToNew[toOld];
            incomingCounts[toIndices[i]]++;
        }

        _indexById = _nodes.Select((node, index) => (node.Id, index))
            .ToDictionary(static pair => pair.Id, static pair => pair.index);
        _values = new float[_nodes.Length];
        _incomingStart = new int[_nodes.Length + 1];
        for (var i = 0; i < _nodes.Length; i++)
            _incomingStart[i + 1] = _incomingStart[i] + incomingCounts[i];
        _incoming = new NeuralEdgeV4[inputEdges.Length];
        var cursor = new int[_nodes.Length];
        Array.Copy(_incomingStart, cursor, _nodes.Length);
        for (var i = 0; i < inputEdges.Length; i++)
        {
            var from = fromIndices[i];
            var to = toIndices[i];
            _incoming[cursor[to]++] = new NeuralEdgeV4((ulong)from, (ulong)to, inputEdges[i].Weight);
        }
    }

    /// <summary>Evaluates one tick. Sensor values are indexed by the sensor interface id.</summary>
    public void Evaluate(ReadOnlySpan<float> sensorValues, Span<float> actionValues)
    {
        var values = _values;
        Array.Clear(values);
        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            float value;
            if (node.Kind == NeuronKindV4.Sensor)
            {
                value = node.InterfaceId < sensorValues.Length
                    ? sensorValues[node.InterfaceId]
                    : 0f;
            }
            else
            {
                var sum = node.Bias;
                for (var e = _incomingStart[i]; e < _incomingStart[i + 1]; e++)
                {
                    var edge = _incoming[e];
                    sum += values[(int)edge.From] * edge.Weight;
                }

                value = Activate(sum, node.Activation);
            }

            values[i] = value;
            if (node.Kind == NeuronKindV4.Output && node.InterfaceId < actionValues.Length)
                actionValues[node.InterfaceId] = value;
        }
    }

    private static float Activate(float value, ActivationKindV4 activation) => activation switch
    {
        ActivationKindV4.Linear => value,
        ActivationKindV4.Tanh => MathF.Tanh(value),
        ActivationKindV4.Sigmoid => 1f / (1f + MathF.Exp(-value)),
        ActivationKindV4.Relu => MathF.Max(0f, value),
        _ => value,
    };
}
