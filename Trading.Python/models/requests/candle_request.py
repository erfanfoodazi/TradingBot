from pydantic import BaseModel, Field, field_validator

from config import settings
from enums.timeframe import TimeFrame


class CandleRequest(BaseModel):

    symbol: str = Field(..., min_length=1, description="e.g. EURUSD")

    timeframe: str = Field(
        default=settings.default_timeframe,
        description="e.g. M1, M5, M15, H1, D1"
    )

    count: int = Field(
        default=settings.default_candles,
        gt=0,
        le=settings.max_candles,
        description="Number of candles to fetch"
    )

    @field_validator("symbol")
    @classmethod
    def _uppercase_symbol(cls, value: str) -> str:
        return value.strip().upper()

    @field_validator("timeframe")
    @classmethod
    def _validate_timeframe(cls, value: str) -> str:
        value = value.upper()
        if value not in TimeFrame.supported():
            raise ValueError(f"Unsupported timeframe: {value}")
        return value
