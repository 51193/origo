<!-- docsync-pair: Origo.Core.Kernel/Ports/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Ports

> [↑ Back to Origo.Core.Kernel](../README.en.md)

Internal kernel-shell port namespace. Ports construct and bind runtime/context objects for shell callers, remain invisible to consumer compilation, and are exposed to shell assemblies only through `InternalsVisibleTo`.

## Port Contract

| Port | Reason | Removal condition |
|------|--------|-------------------|
| `HostKernelPort` | The Core shell facade must construct the concrete runtime/SND context while kernel compile assets stay out of the consumer graph. | Remove when the shell can construct an equivalent host through stable contracts, or when composition moves to a shared host package. |

## Included Files

| File | Responsibility |
|------|----------------|
| `HostKernelPort.cs` | See source documentation and API comments. |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
