from __future__ import annotations

from datetime import datetime
from pathlib import Path

from .models import TleRecord


def timestamped_output_path(project_root: Path, now: datetime | None = None) -> Path:
    current = now or datetime.now()
    return project_root / f"TLE_{current:%y%m%d_%H%M%S}.txt"


def write_tle_output(output_path: Path, records: list[TleRecord]) -> None:
    lines: list[str] = []
    for record in records:
        lines.extend([record.sat_name, record.line1, record.line2])

    content = "\n".join(lines)
    if content:
        content += "\n"

    output_path.write_text(content, encoding="utf-8")
