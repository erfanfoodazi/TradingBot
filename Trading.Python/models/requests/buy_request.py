from pydantic import BaseModel, Field, field_validator

from config import settings


class BuyRequest(BaseModel):

    symbol: str = Field(..., min_length=1, description="e.g. EURUSD")

    volume: float = Field(
        ...,
        gt=0,
        description="Trade volume in lots"
    )

    stop_loss: float | None = Field(
        default=None,
        ge=0,
        description="Stop loss price (0 or None = no stop loss)"
    )

    take_profit: float | None = Field(
        default=None,
        ge=0,
        description="Take profit price (0 or None = no take profit)"
    )

    deviation: int = Field(
        default=settings.deviation,
        ge=0,
        description="Max price deviation in points"
    )

    @field_validator("symbol")
    @classmethod
    def _uppercase_symbol(cls, value: str) -> str:
        return value.strip().upper()
