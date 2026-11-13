using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Extensible gene categories. New categories can be registered without changing the genome container.</summary>
public enum GeneKind : ushort
{
    BodySegment = 1,
    Organ = 2,
    Neuron = 3,
    Synapse = 4,
    Reflex = 5,
    LifeHistory = 6,
}

/// <summary>Body organ kinds used by the first V4 scenario set.</summary>
public enum OrganKindV4 : ushort
{
    Vision = 1,
    SpikeOrgan = 2,
    SpikeBattery = 3,
    HardDigest = 4,
    ToxinResistance = 5,
    Armor = 6,
    Rhythm = 7,
    CreatureSense = 8,
    CarrionSense = 9,
    Sprint = 10,
}

/// <summary>Neural node kinds.</summary>
public enum NeuronKindV4 : byte
{
    Sensor = 1,
    Interneuron = 2,
    Output = 3,
}

/// <summary>Sensor interface identifiers. New sensors extend this registry.</summary>
public enum SensorKindV4 : ushort
{
    FoodAhead = 1,
    FoodLeft = 2,
    FoodRight = 3,
    WallAhead = 4,
    Energy = 5,
    CreatureAhead = 6,
    CorpseAhead = 7,
    Sin = 8,
    Cos = 9,
}

/// <summary>Action interface identifiers. New actions extend this registry.</summary>
public enum ActionKindV4 : ushort
{
    Move = 1,
    Turn = 2,
    Spike = 3,
    Aim = 4,
    Sprint = 5,
}

/// <summary>Activation functions available to V4 neural nodes.</summary>
public enum ActivationKindV4 : byte
{
    Linear = 0,
    Tanh = 1,
    Sigmoid = 2,
    Relu = 3,
}

/// <summary>Environment conditions used by regulatory links.</summary>
public enum ConditionKindV4 : ushort
{
    Always = 1,
    FoodLow = 2,
    PredatorNear = 3,
    EnergyLow = 4,
}

/// <summary>Life-history trait identifiers.</summary>
public enum LifeTraitKindV4 : ushort
{
    Size = 1,
    Speed = 2,
    TurnSpeed = 3,
    SenseRange = 4,
    Metabolism = 5,
    MaxAge = 6,
    SpikeRange = 7,
}

/// <summary>A regulatory condition attached to a gene.</summary>
public sealed record RegulationV4(
    ConditionKindV4 Condition,
    float Threshold);

/// <summary>
///     One stable-identity gene. Payload layout is selected by <see cref="Kind"/> and
///     decoded by <see cref="GenePayloadV4"/>; identity is never renumbered by mutation.
/// </summary>
public sealed class GeneV4
{
    public GeneV4(
        uint id,
        GeneKind kind,
        byte copyCount,
        float expression,
        byte[] payload,
        IEnumerable<RegulationV4>? regulators = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(id);
        ArgumentOutOfRangeException.ThrowIfZero(copyCount);
        ArgumentNullException.ThrowIfNull(payload);
        Id = id;
        Kind = kind;
        CopyCount = copyCount;
        Expression = expression;
        Payload = payload;
        Regulators = [.. regulators ?? []];
    }

    public uint Id { get; }

    public GeneKind Kind { get; }

    public byte CopyCount { get; set; }

    public float Expression { get; set; }

    public byte[] Payload { get; }

    public List<RegulationV4> Regulators { get; }

    public GeneV4 Clone() => new(Id, Kind, CopyCount, Expression, (byte[])Payload.Clone(),
        Regulators.Select(static r => r with { }));
}

/// <summary>Canonical V4 genome: an unordered set of stable-ID genes plus the next ID.</summary>
public sealed class GenomeV4
{
    public const ushort SchemaVersion = 4;

    public GenomeV4(IEnumerable<GeneV4> genes, uint nextGeneId)
    {
        ArgumentNullException.ThrowIfNull(genes);
        Genes = [.. genes];
        NextGeneId = nextGeneId;
    }

    public List<GeneV4> Genes { get; }

    public uint NextGeneId { get; set; }

    public GeneV4? Find(uint id)
    {
        foreach (var gene in Genes)
            if (gene.Id == id)
                return gene;
        return null;
    }

    public uint AllocateId() => NextGeneId++;

    public GenomeV4 Clone()
    {
        var genes = new List<GeneV4>(Genes.Count);
        foreach (var gene in Genes)
            genes.Add(gene.Clone());
        return new GenomeV4(genes, NextGeneId);
    }

    public IReadOnlyList<GeneV4> SortedGenes()
    {
        var sorted = new List<GeneV4>(Genes);
        sorted.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return sorted;
    }
}

/// <summary>Payload codecs for known V4 gene kinds.</summary>
public static class GenePayloadV4
{
    public static byte[] BodySegment(float length, float width) => Write(writer =>
    {
        writer.Write(length);
        writer.Write(width);
    });

    public static (float Length, float Width) ReadBodySegment(byte[] payload) => Read(payload, reader =>
        (reader.ReadSingle(), reader.ReadSingle()));

    public static byte[] Organ(OrganKindV4 kind, float size) => Write(writer =>
    {
        writer.Write((ushort)kind);
        writer.Write(size);
    });

    public static (OrganKindV4 Kind, float Size) ReadOrgan(byte[] payload) => Read(payload, reader =>
        ((OrganKindV4)reader.ReadUInt16(), reader.ReadSingle()));

    public static byte[] Neuron(
        NeuronKindV4 kind,
        ushort interfaceId,
        ActivationKindV4 activation,
        float bias) => Write(writer =>
    {
        writer.Write((byte)kind);
        writer.Write(interfaceId);
        writer.Write((byte)activation);
        writer.Write(bias);
    });

    public static (NeuronKindV4 Kind, ushort InterfaceId, ActivationKindV4 Activation, float Bias)
        ReadNeuron(byte[] payload) => Read(payload, reader => (
            (NeuronKindV4)reader.ReadByte(),
            reader.ReadUInt16(),
            (ActivationKindV4)reader.ReadByte(),
            reader.ReadSingle()));

    public static byte[] Synapse(uint fromGeneId, uint toGeneId, float weight) => Write(writer =>
    {
        writer.Write(fromGeneId);
        writer.Write(toGeneId);
        writer.Write(weight);
    });

    public static (uint FromGeneId, uint ToGeneId, float Weight) ReadSynapse(byte[] payload) =>
        Read(payload, reader => (reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadSingle()));

    public static byte[] Reflex(SensorKindV4 sensor, ActionKindV4 action, float gain) => Write(writer =>
    {
        writer.Write((ushort)sensor);
        writer.Write((ushort)action);
        writer.Write(gain);
    });

    public static (SensorKindV4 Sensor, ActionKindV4 Action, float Gain) ReadReflex(byte[] payload) =>
        Read(payload, reader => ((SensorKindV4)reader.ReadUInt16(), (ActionKindV4)reader.ReadUInt16(),
            reader.ReadSingle()));

    public static byte[] LifeHistory(LifeTraitKindV4 trait, float value) => Write(writer =>
    {
        writer.Write((ushort)trait);
        writer.Write(value);
    });

    public static (LifeTraitKindV4 Trait, float Value) ReadLifeHistory(byte[] payload) =>
        Read(payload, reader => ((LifeTraitKindV4)reader.ReadUInt16(), reader.ReadSingle()));

    private static byte[] Write(Action<BinaryWriter> action)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        action(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T Read<T>(byte[] payload, Func<BinaryReader, T> read)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        return read(reader);
    }
}

/// <summary>Deterministic canonical binary codec for GenomeV4.</summary>
public static class GenomeV4Codec
{
    private static readonly byte[] Magic = "G4"u8.ToArray();

    public static byte[] Serialize(GenomeV4 genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        GenomeV4Validator.Validate(genome);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(GenomeV4.SchemaVersion);
        var genes = genome.SortedGenes();
        writer.Write(genes.Count);
        foreach (var gene in genes)
        {
            writer.Write(gene.Id);
            writer.Write((ushort)gene.Kind);
            writer.Write(gene.CopyCount);
            writer.Write(gene.Expression);
            writer.Write(gene.Regulators.Count);
            foreach (var regulation in gene.Regulators)
            {
                writer.Write((ushort)regulation.Condition);
                writer.Write(regulation.Threshold);
            }

            writer.Write(gene.Payload.Length);
            writer.Write(gene.Payload);
        }

        writer.Write(genome.NextGeneId);
        writer.Flush();
        return stream.ToArray();
    }

    public static GenomeV4 Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Not a GenomeV4 payload.");
        var version = reader.ReadUInt16();
        if (version != GenomeV4.SchemaVersion)
            throw new InvalidDataException($"Unsupported GenomeV4 schema version {version}.");
        var count = reader.ReadInt32();
        if (count < 0)
            throw new InvalidDataException("Negative gene count.");
        var genes = new List<GeneV4>(count);
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadUInt32();
            var kind = (GeneKind)reader.ReadUInt16();
            var copies = reader.ReadByte();
            var expression = reader.ReadSingle();
            var regulationCount = reader.ReadInt32();
            if (regulationCount < 0)
                throw new InvalidDataException("Negative regulation count.");
            var regulations = new List<RegulationV4>(regulationCount);
            for (var r = 0; r < regulationCount; r++)
            {
                var condition = (ConditionKindV4)reader.ReadUInt16();
                var threshold = reader.ReadSingle();
                regulations.Add(new RegulationV4(condition, threshold));
            }

            var payloadLength = reader.ReadInt32();
            if (payloadLength < 0)
                throw new InvalidDataException("Negative payload length.");
            var payload = reader.ReadBytes(payloadLength);
            genes.Add(new GeneV4(id, kind, copies, expression, payload, regulations));
        }

        var nextId = reader.ReadUInt32();
        var genome = new GenomeV4(genes, nextId);
        GenomeV4Validator.Validate(genome);
        return genome;
    }

    public static string ToHex(GenomeV4 genome) => Convert.ToHexString(Serialize(genome));

    public static GenomeV4 FromHex(string hex) => Deserialize(Convert.FromHexString(hex));
}

/// <summary>Structural validation for V4 genomes.</summary>
public static class GenomeV4Validator
{
    public const int MaxGenes = 4096;

    public static void Validate(GenomeV4 genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (genome.Genes.Count == 0)
            throw new InvalidDataException("Genome must contain at least one gene.");
        if (genome.Genes.Count > MaxGenes)
            throw new InvalidDataException($"Genome exceeds {MaxGenes} genes.");
        var ids = new HashSet<uint>();
        var neuronIds = new HashSet<uint>();
        foreach (var gene in genome.Genes)
        {
            if (!ids.Add(gene.Id))
                throw new InvalidDataException($"Duplicate gene id {gene.Id}.");
            if (gene.Id >= genome.NextGeneId)
                throw new InvalidDataException($"Gene id {gene.Id} is not below next id {genome.NextGeneId}.");
            if (gene.Payload.Length == 0)
                throw new InvalidDataException($"Gene {gene.Id} has an empty payload.");
            if (gene.CopyCount == 0)
                throw new InvalidDataException($"Gene {gene.Id} has zero copies.");
            if (!float.IsFinite(gene.Expression))
                throw new InvalidDataException($"Gene {gene.Id} has non-finite expression.");
            foreach (var regulation in gene.Regulators)
                if (!float.IsFinite(regulation.Threshold))
                    throw new InvalidDataException($"Gene {gene.Id} has a non-finite regulation threshold.");
            if (gene.Kind == GeneKind.Neuron)
            {
                var (neuronKind, interfaceId, _, _) = GenePayloadV4.ReadNeuron(gene.Payload);
                if (neuronKind == NeuronKindV4.Sensor
                    && !Enum.IsDefined(interfaceId is > 0 ? (SensorKindV4)interfaceId : 0))
                    throw new InvalidDataException($"Gene {gene.Id} has an unknown sensor interface.");
                if (neuronKind == NeuronKindV4.Output
                    && !Enum.IsDefined(interfaceId is > 0 ? (ActionKindV4)interfaceId : 0))
                    throw new InvalidDataException($"Gene {gene.Id} has an unknown action interface.");
                neuronIds.Add(gene.Id);
            }
        }

        foreach (var gene in genome.Genes)
        {
            if (gene.Kind == GeneKind.Synapse)
            {
                var (fromGeneId, toGeneId, _) = GenePayloadV4.ReadSynapse(gene.Payload);
                if (!neuronIds.Contains(fromGeneId) || !neuronIds.Contains(toGeneId))
                    throw new InvalidDataException($"Synapse gene {gene.Id} references a non-neuron gene.");
            }

            foreach (var regulation in gene.Regulators)
                if (!Enum.IsDefined(regulation.Condition))
                    throw new InvalidDataException($"Gene {gene.Id} has an unknown condition.");
        }
    }
}

/// <summary>Weights for V4 mutation operators.</summary>
public sealed record GenomeMutationSettingsV4
{
    public double Expression { get; init; } = 0.08;
    public double CopyCount { get; init; } = 0.06;
    public double Duplicate { get; init; } = 0.10;
    public double Delete { get; init; } = 0.08;
    public double Payload { get; init; } = 0.18;
    public double AddNeuron { get; init; } = 0.12;
    public double AddSynapse { get; init; } = 0.12;
    public double AddReflex { get; init; } = 0.05;
    public double AddOrgan { get; init; } = 0.05;
    public double AddRegulation { get; init; } = 0.08;
    public double AddSegment { get; init; } = 0.02;
}

/// <summary>Typed random mutation for GenomeV4.</summary>
public static class GenomeV4Mutator
{
    public static GenomeV4 Mutate(
        GenomeV4 parent,
        Random random,
        GenomeMutationSettingsV4? settings = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(random);
        var child = MutateCore(parent, random, settings);
        return CanCompile(child) ? child : parent.Clone();
    }

    private static GenomeV4 MutateCore(
        GenomeV4 parent,
        Random random,
        GenomeMutationSettingsV4? settings)
    {
        settings ??= new GenomeMutationSettingsV4();
        var child = parent.Clone();
        var roll = random.NextDouble();
        var cursor = 0d;
        cursor += settings.Expression;
        if (roll < cursor)
        {
            MutateExpression(child, random);
            return child;
        }

        cursor += settings.CopyCount;
        if (roll < cursor)
        {
            MutateCopyCount(child, random);
            return child;
        }

        cursor += settings.Duplicate;
        if (roll < cursor)
        {
            DuplicateGene(child, random);
            return child;
        }

        cursor += settings.Delete;
        if (roll < cursor)
        {
            DeleteGene(child, random);
            return child;
        }

        cursor += settings.Payload;
        if (roll < cursor)
        {
            MutatePayload(child, random);
            return child;
        }

        cursor += settings.AddNeuron;
        if (roll < cursor)
        {
            AddNeuron(child, random);
            return child;
        }

        cursor += settings.AddSynapse;
        if (roll < cursor)
        {
            AddSynapse(child, random);
            return child;
        }

        cursor += settings.AddReflex;
        if (roll < cursor)
        {
            AddReflex(child, random);
            return child;
        }

        cursor += settings.AddOrgan;
        if (roll < cursor)
        {
            AddOrgan(child, random);
            return child;
        }

        cursor += settings.AddRegulation;
        if (roll < cursor)
        {
            AddRegulation(child, random);
            return child;
        }

        AddSegment(child, random);
        return child;
    }

    private static bool CanCompile(GenomeV4 genome)
    {
        try
        {
            GenomeV4Validator.Validate(genome);
            _ = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void MutateExpression(GenomeV4 genome, Random random)
    {
        var gene = Pick(genome, random);
        if (gene is null)
            return;
        gene.Expression = Math.Clamp(gene.Expression + ((float)random.NextDouble() - 0.5f) * 0.5f, 0f, 4f);
    }

    private static void MutateCopyCount(GenomeV4 genome, Random random)
    {
        var gene = Pick(genome, random);
        if (gene is null)
            return;
        var next = gene.CopyCount + (random.Next(2) == 0 ? -1 : 1);
        gene.CopyCount = (byte)Math.Clamp(next, 1, 32);
    }

    private static void DuplicateGene(GenomeV4 genome, Random random)
    {
        var gene = Pick(genome, random);
        if (gene is null)
            return;
        var copy = gene.Clone();
        var id = genome.AllocateId();
        var duplicate = new GeneV4(id, copy.Kind, copy.CopyCount, copy.Expression, copy.Payload, copy.Regulators);
        genome.Genes.Add(duplicate);
    }

    private static void DeleteGene(GenomeV4 genome, Random random)
    {
        if (genome.Genes.Count <= 1)
            return;
        var index = random.Next(genome.Genes.Count);
        var gene = genome.Genes[index];
        if (gene.Kind == GeneKind.BodySegment && genome.Genes.Count(g => g.Kind == GeneKind.BodySegment) <= 1)
            return;
        genome.Genes.RemoveAt(index);
        var valid = genome.Genes.Where(static g => g.Kind != GeneKind.BodySegment).ToList();
        if (valid.Count == 0)
            return;
        // Removing a neuron can orphan synapses; remove those instead of leaving an invalid genome.
        if (gene.Kind == GeneKind.Neuron)
        {
            var neuronIds = genome.Genes
                .Where(static g => g.Kind == GeneKind.Neuron)
                .Select(static g => g.Id)
                .ToHashSet();
            genome.Genes.RemoveAll(g => g.Kind == GeneKind.Synapse
                && (!neuronIds.Contains(GenePayloadV4.ReadSynapse(g.Payload).FromGeneId)
                    || !neuronIds.Contains(GenePayloadV4.ReadSynapse(g.Payload).ToGeneId)));
        }
    }

    private static void MutatePayload(GenomeV4 genome, Random random)
    {
        var gene = Pick(genome, random);
        if (gene is null)
            return;
        var payload = gene.Payload;
        var offset = gene.Kind switch
        {
            GeneKind.BodySegment => random.Next(2) * sizeof(float),
            GeneKind.Organ => sizeof(ushort),
            GeneKind.Neuron => sizeof(byte) + sizeof(ushort) + sizeof(byte),
            GeneKind.Synapse => sizeof(uint) + sizeof(uint),
            GeneKind.LifeHistory => sizeof(ushort),
            _ => -1,
        };
        if (offset < 0 || offset + sizeof(float) > payload.Length)
            return;
        var value = BitConverter.ToSingle(payload, offset);
        value += ((float)random.NextDouble() - 0.5f) * 0.25f;
        if (!float.IsFinite(value))
            value = 0f;
        BitConverter.GetBytes(value).CopyTo(payload, offset);
    }

    private static void AddNeuron(GenomeV4 genome, Random random)
    {
        var kind = random.Next(3) switch
        {
            0 => NeuronKindV4.Sensor,
            1 => NeuronKindV4.Interneuron,
            _ => NeuronKindV4.Output,
        };
        var sensorCandidates = new[]
        {
            SensorKindV4.FoodAhead, SensorKindV4.FoodLeft, SensorKindV4.FoodRight,
            SensorKindV4.WallAhead, SensorKindV4.Energy, SensorKindV4.CreatureAhead,
            SensorKindV4.CorpseAhead, SensorKindV4.Sin, SensorKindV4.Cos,
        }.Where(GenomeContentGate.IsSensorAllowed).ToArray();
        var actionCandidates = new[]
        {
            ActionKindV4.Move, ActionKindV4.Turn, ActionKindV4.Spike,
            ActionKindV4.Aim, ActionKindV4.Sprint,
        }.Where(GenomeContentGate.IsActionAllowed).ToArray();
        if (kind == NeuronKindV4.Sensor && sensorCandidates.Length == 0)
            return;
        if (kind == NeuronKindV4.Output && actionCandidates.Length == 0)
            return;
        var interfaceId = kind switch
        {
            NeuronKindV4.Sensor => (ushort)sensorCandidates[random.Next(sensorCandidates.Length)],
            NeuronKindV4.Output => (ushort)actionCandidates[random.Next(actionCandidates.Length)],
            _ => (ushort)0,
        };
        AddGene(genome, GeneKind.Neuron, GenePayloadV4.Neuron(
            kind,
            interfaceId,
            (ActivationKindV4)random.Next(4),
            (float)((random.NextDouble() * 2d) - 1d)));
    }

    private static void AddSynapse(GenomeV4 genome, Random random)
    {
        var neurons = genome.Genes.Where(static g => g.Kind == GeneKind.Neuron).ToList();
        if (neurons.Count < 2)
            return;
        var from = neurons[random.Next(neurons.Count)];
        var to = neurons[random.Next(neurons.Count)];
        if (from.Id == to.Id)
            return;
        AddGene(genome, GeneKind.Synapse, GenePayloadV4.Synapse(
            from.Id, to.Id, (float)((random.NextDouble() * 2d) - 1d)));
    }

    private static void AddReflex(GenomeV4 genome, Random random)
    {
        var sensors = new[]
        {
            SensorKindV4.FoodAhead, SensorKindV4.FoodLeft, SensorKindV4.FoodRight,
            SensorKindV4.WallAhead, SensorKindV4.Energy, SensorKindV4.CreatureAhead,
            SensorKindV4.CorpseAhead, SensorKindV4.Sin, SensorKindV4.Cos,
        }.Where(GenomeContentGate.IsSensorAllowed).ToArray();
        var actions = new[]
        {
            ActionKindV4.Move, ActionKindV4.Turn, ActionKindV4.Spike,
            ActionKindV4.Aim, ActionKindV4.Sprint,
        }.Where(GenomeContentGate.IsActionAllowed).ToArray();
        if (sensors.Length == 0 || actions.Length == 0)
            return;
        AddGene(genome, GeneKind.Reflex, GenePayloadV4.Reflex(
            sensors[random.Next(sensors.Length)],
            actions[random.Next(actions.Length)],
            (float)((random.NextDouble() * 2d) - 1d)));
    }

    private static void AddOrgan(GenomeV4 genome, Random random)
    {
        var allowed = new[]
        {
            OrganKindV4.Vision, OrganKindV4.SpikeOrgan, OrganKindV4.SpikeBattery,
            OrganKindV4.HardDigest, OrganKindV4.ToxinResistance, OrganKindV4.Rhythm,
            OrganKindV4.CreatureSense, OrganKindV4.CarrionSense, OrganKindV4.Sprint,
        }.Where(GenomeContentGate.IsOrganAllowed).ToArray();
        if (allowed.Length == 0)
            return;
        AddGene(genome, GeneKind.Organ, GenePayloadV4.Organ(
            allowed[random.Next(allowed.Length)],
            (float)(0.5d + random.NextDouble())));
    }

    private static void AddSegment(GenomeV4 genome, Random random)
    {
        AddGene(genome, GeneKind.BodySegment, GenePayloadV4.BodySegment(
            (float)(0.5d + random.NextDouble()),
            (float)(0.2d + (random.NextDouble() * 0.8d))));
    }

    private static void AddRegulation(GenomeV4 genome, Random random)
    {
        var gene = Pick(genome, random);
        if (gene is null)
            return;
        var condition = (ConditionKindV4)(1 + random.Next(4));
        gene.Regulators.Add(new RegulationV4(
            condition,
            (float)(0.1d + (random.NextDouble() * 0.8d))));
    }

    private static void AddGene(GenomeV4 genome, GeneKind kind, byte[] payload)
    {
        genome.Genes.Add(new GeneV4(
            genome.AllocateId(),
            kind,
            1,
            1f,
            payload));
    }

    private static GeneV4? Pick(GenomeV4 genome, Random random) =>
        genome.Genes.Count == 0 ? null : genome.Genes[random.Next(genome.Genes.Count)];
}
