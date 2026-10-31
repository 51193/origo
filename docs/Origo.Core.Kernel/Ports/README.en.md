# Ports

> [↑ Back to Origo.Core.Kernel](../README.en.md)

Internal kernel-shell port namespace. Ports construct and bind runtime/context objects for shell callers, remain invisible to consumer compilation, and are exposed to shell assemblies only through `InternalsVisibleTo`.

## Port Contract

| Port | Reason | Removal condition |
|------|--------|-------------------|
| `HostKernelPort` | The Core shell facade must construct the concrete runtime/SND context while kernel compile assets stay out of the consumer graph; when a file system is supplied it persists the system blackboard at `SaveRootPath/system.json`. | Remove when the shell can construct an equivalent host through stable contracts, or when composition moves to a shared host package. |
| `AdapterHostKernelPort` | The Godot adapter shell must construct its runtime, observer topology, and SND context for real Godot `Node` scene hosts without duplicating Core startup orchestration. | Remove when a scene host can construct runtime/context entirely through stable contracts without an adapter-specific port. |

## Included Files

| File | Responsibility |
|------|----------------|
| `HostKernelPort.cs` | `internal` port the Core shell facade calls to create the runtime and SND context; also defines `IHostKernelPort` and the `OrigoHostBundle` result record |
| `AdapterHostKernelPort.cs` | `internal` port the Godot adapter shell calls to create its runtime over an `ISndSceneHost` and bind the per-scene observer topology; also defines `ISndSceneHostRuntimeBinder`, `AdapterRuntimeBundle`, and `AdapterContextOptions` |
| `HostConstruction.cs` | `internal` shared construction helper: type mapping/converter registry, I/O/metadata/path services, system-blackboard loading, runtime creation, and SND-context creation used by both ports; each port keeps its own entry point and failure contract |
| `HostContextParameters.cs` | `internal` factory both ports share to project their option records onto one `SndContextParameters`, so a context option is wired once and the adapter host path cannot drift from the Core host path |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
