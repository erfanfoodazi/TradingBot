import os
from dataclasses import dataclass

from dotenv import load_dotenv

load_dotenv()


def _env(key: str, default: str = "") -> str:
    return os.getenv(key, default)


def _env_int(key: str, default: int) -> int:
    value = os.getenv(key, str(default))
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def _env_float(key: str, default: float) -> float:
    value = os.getenv(key, str(default))
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


@dataclass
class Settings:
    mt5_path: str = _env("MT5_PATH", r"C:\Program Files\MetaTrader 5\terminal64.exe")
    mt5_login: int = _env_int("MT5_LOGIN", 0)
    mt5_password: str = _env("MT5_PASSWORD")
    mt5_server: str = _env("MT5_SERVER")
    mt5_timeout_ms: int = _env_int("MT5_TIMEOUT_MS", 5000)

    api_host: str = _env("API_HOST", "127.0.0.1")
    api_port: int = _env_int("API_PORT", 8000)
    api_key: str = _env("API_KEY", "")

    default_symbol: str = _env("DEFAULT_SYMBOL", "EURUSD")
    default_timeframe: str = _env("DEFAULT_TIMEFRAME", "M15")
    default_candles: int = _env_int("DEFAULT_CANDLES", 100)
    max_candles: int = _env_int("MAX_CANDLES", 5000)

    magic_number: int = _env_int("MAGIC_NUMBER", 1001)
    deviation: int = _env_int("DEVIATION", 20)

    mt5_monitor_interval: float = _env_float("MT5_MONITOR_INTERVAL", 5.0)


settings = Settings()
