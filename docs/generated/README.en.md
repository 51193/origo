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

## Adding another generated artifact

1. Add a renderer/output to `scripts/python/docs_generate.py`.
2. Add the artifact to the `docs.sh generate/check` path.
3. Document it here and link it from the manual index.
4. Add a focused test to `scripts/python/docs_generate_test.py`.
5. Add the script to `scripts/README.md` if a new entry point is introduced.
6. Run `bash scripts/bash/docs.sh generate`, review the diff, run
   `bash scripts/bash/docs.sh check`, and commit the generated file with the
   generator change.

Do not treat generated output as hand-maintained prose. If a signature or
inventory is wrong, fix its source and regenerate.
