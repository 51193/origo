> [↑ Back to Origo Manual](../README.en.md)

# Generated documentation

This directory contains generated, committed documentation artifacts.

- [contract-api.en.md](contract-api.en.md) — canonical public shell API
  signatures, generated from `tools/ApiInventoryTool/shell-api-baseline.json`.

Regenerate locally, then run the check and commit the result:

```bash
bash scripts/bash/docs.sh generate
bash scripts/bash/docs.sh check
```

The Bash and Windows local lint gates run the check. Link validation covers the
root Markdown files, the manual under `docs/`, the script map
([`scripts/README.md`](../../scripts/README.md)), and `.github/` Markdown
(templates and repository configuration) so broken links in maintained docs are
reported locally. Generated files are committed so the documentation remains
available without a remote generation service.

## Adding another generated artifact

1. Add a renderer/output to `scripts/python/docs_generate.py` and register it
   in `GENERATED_ARTIFACTS`.
2. Both `docs.sh` and `docs.ps1` dispatch over `GENERATED_ARTIFACTS`; no per-artifact wrapper edit is needed.
3. Document it here and link it from the manual index.
4. Add a focused test to `scripts/python/docs_generate_test.py`.
5. Add the script to `scripts/README.md` if a new entry point is introduced.
6. Run `bash scripts/bash/docs.sh generate`, review the diff, run
   `bash scripts/bash/docs.sh check`, and commit the generated file with the
   generator change.

Do not treat generated output as hand-maintained prose. If a signature or
inventory is wrong, fix its source and regenerate.
