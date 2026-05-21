from __future__ import annotations

import sys
from pathlib import Path


def bootstrap() -> int:
    project_root = _get_project_root()

    if not getattr(sys, "frozen", False):
        src_dir = project_root / "src"
        if str(src_dir) not in sys.path:
            sys.path.insert(0, str(src_dir))

    from tle_download.app import run

    return run(project_root)


def _get_project_root() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parent


if __name__ == "__main__":
    raise SystemExit(bootstrap())
