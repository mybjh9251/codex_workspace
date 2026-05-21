from __future__ import annotations

import logging
import sys
from datetime import date
from pathlib import Path


def setup_daily_logger(log_dir: Path) -> tuple[logging.Logger, Path]:
    log_dir.mkdir(parents=True, exist_ok=True)
    log_path = log_dir / f"tle_download_{date.today():%Y-%m-%d}.log"

    logger = logging.getLogger("tle_download")
    logger.setLevel(logging.INFO)
    logger.propagate = False

    while logger.handlers:
        handler = logger.handlers.pop()
        handler.close()

    formatter = logging.Formatter(
        fmt="%(asctime)s | %(levelname)s | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )

    file_handler = logging.FileHandler(log_path, mode="a", encoding="utf-8")
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    return logger, log_path
