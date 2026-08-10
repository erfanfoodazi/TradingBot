from datetime import datetime

from pydantic import BaseModel, Field, field_validator


class PendingOrderRequest(BaseModel):

    symbol: str = Field(..., min_length=1, description="e.g. EURUSD")

    type: str = Field(
        ...,
        description="buy_limit | sell_limit | buy_stop | sell_stop"
    )

    volume: float = Field(..., gt=0, description="Trade volume in lots")

    price: float = Field(..., gt=0, description="Trigger price")

    stop_loss: float | None = Field(default=None, ge=0)

    take_profit: float | None = Field(default=None, ge=0)

    expiration: int | datetime | None = Field(
        default=None,
        description="Optional expiration as epoch seconds or datetime"
    )

    @field_validator("symbol")
    @classmethod
    def _uppercase_symbol(cls, value: str) -> str:
        return value.strip().upper()

    @field_validator("type")
    @classmethod
    def _lowercase_type(cls, value: str) -> str:
        value = value.strip().lower()
        if value not in {"buy_limit", "sell_limit", "buy_stop", "sell_stop"}:
            raise ValueError(f"Unsupported pending order type: {value}")
        return value

    @field_validator("expiration", mode="before")
    @classmethod
    def _coerce_expiration(cls, value):
        if isinstance(value, int) and value > 0:
            return datetime.fromtimestamp(value)
        return value