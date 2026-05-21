from __future__ import annotations

from datetime import datetime
from pathlib import Path

from .models import TleRecord


def timestamped_output_path(root: Path, now: datetime | None = None) -> Path:
    current = now or datetime.now()
    return root / f"TLE_{current:%y%m%d_%H%M%S}.txt"


def write_tle_records(path: Path, records: list[TleRecord]) -> None:
    lines: list[str] = []
    for record in records:
        lines.extend([record.sat_name, record.line1, record.line2])
    path.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
