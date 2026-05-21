# TLE_Download

Python batch tool for collecting TLE data from a satellite list.

## Current State

- Input sample is prepared: `Sat_List.xlsx`
- Output sample is prepared: `TLE_260521_123037.txt`
- Non-sensitive profile template is prepared: `profile.template.xml`
- Python source code is implemented under `src/tle_download/`
- Runtime smoke verification requires a real CPython installation

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

Install dependencies:

```powershell
python -m pip install -r requirements.txt
```

Create local credentials:

```powershell
Copy-Item profile.template.xml profile.xml
```

Run:

```powershell
python main.py
```

## Runtime Check

On Windows, `python.exe` may resolve to the Microsoft Store stub. Confirm the actual runtime before running:

```powershell
Get-Command python
where.exe python
python --version
```

Run the smoke check after installing dependencies:

```powershell
.\scripts\smoke_check.ps1
```

If your Python command is not `python`, pass it explicitly:

```powershell
.\scripts\smoke_check.ps1 -Python "C:\Path\To\python.exe"
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
