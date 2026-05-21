# TLE_Download

Python batch tool for collecting TLE data from a satellite list.

## Current State

- Input sample is prepared: `Sat_List.xlsx`
- Output sample is prepared: `TLE_260521_123037.txt`
- Non-sensitive profile template is prepared: `profile.template.xml`
- Python source code is not implemented yet

## File Contract

### Input

`Sat_List.xlsx`

- Sheet: `Sheet1`
- Required columns:
  - `SAT_Name`
  - `NORAD ID`

### Profile

`profile.xml` is the real local credential file and must not be committed.

Create it from the template:

```powershell
Copy-Item profile.template.xml profile.xml
```

Then fill in the Space-Track account values in `profile.xml`.

### Output

TLE output files use this format:

```text
TLE_{YYMMDD_HHmmss}.txt
```

Each satellite is written as a 3-line block:

```text
SAT_Name
TLE line 1
TLE line 2
```

## Planned Run Command

After implementation:

```powershell
python main.py
```

## Sensitive Files

Do not commit:

- `profile.xml`
- files containing passwords, tokens, or API keys
- build outputs such as `dist/` or `build/`

## Related Docs

See the `Codex` repository:

```text
../Codex/project_docs/TLE_Download/
```
