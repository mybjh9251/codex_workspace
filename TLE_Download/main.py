from __future__ import annotations

import sys
from pathlib import Path


def _add_src_to_path() -> None:
    root = Path(__file__).resolve().parent
    src = root / "src"
    if str(src) not in sys.path:
        sys.path.insert(0, str(src))


def main() -> int:
    _add_src_to_path()

    from tle_download.app import run

    return run()


if __name__ == "__main__":
    raise SystemExit(main())
