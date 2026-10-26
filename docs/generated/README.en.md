# Generated documentation

This directory contains generated, committed documentation artifacts.

- [contract-api.en.md](contract-api.en.md) — canonical public shell API
  signatures, generated from `tools/ApiInventoryTool/shell-api-baseline.json`.

Regenerate locally, then run the check and commit the result:

```bash
bash scripts/bash/docs.sh generate
bash scripts/bash/docs.sh check
```

The normal local lint gate runs the check. Generated files are committed so the
documentation remains available without a remote generation service.
