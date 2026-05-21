# Codex_Workspace

This repository stores actual project implementation files used with the `Codex` documentation repository.

## Repository Role

- `Codex`: documentation, rules, templates, and history
- `Codex_Workspace`: source code, run files, and non-sensitive project input/output samples

Project implementations live under:

```text
<project-name>/
```

Example:

```text
TLE_Download/
starlink/
```

## Git Include / Exclude Policy

Commit:

- source code
- run scripts
- project README files
- non-sensitive input/output samples
- template or example configuration files

Do not commit:

- `profile.xml`
- `.env`
- tokens, passwords, API keys, or license keys
- build outputs such as `build/`, `dist/`, `bin/`, `obj/`
- virtual environments and caches

Use template files for sensitive configuration:

```text
profile.template.xml
.env.example
```

## Current Projects

| Path | Status |
| --- | --- |
| `TLE_Download/` | Python implementation and file samples prepared; runtime verification pending |

## Related Documentation

See the `Codex` repository for project docs, rules, and history:

```text
../Codex/
```
