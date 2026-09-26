<!-- docsync-pair: Origo.Core.Kernel/Ports/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
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
| `HostKernelPort.cs` | See source documentation and API comments. |
| `AdapterHostKernelPort.cs` | See source documentation and API comments; also defines `ISndSceneHostRuntimeBinder`, `AdapterRuntimeBundle`, and `AdapterContextOptions`. |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
