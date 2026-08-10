from datetime import datetime

from pydantic import BaseModel, Field, field_validator


class ModifyRequest(BaseModel):

    ticket: int = Field(..., gt=0, description="Position/order ticket to modify")

    symbol: str = Field(default="", description="e.g. EURUSD (optional, resolved from ticket)")

    stop_loss: float | None = Field(default=None, ge=0)

    take_profit: float | None = Field(default=None, ge=0)


class CancelPendingRequest(BaseModel):

    ticket: int = Field(..., gt=0, description="Pending order ticket to cancel")


class HistoryRequest(BaseModel):

    position_id: int = Field(default=0, ge=0)

    from_time: int | datetime | None = Field(
        default=None,
        description="Start of range (epoch seconds or datetime)"
    )

    to_time: int | datetime | None = Field(
        default=None,
        description="End of range (epoch seconds or datetime)"
    )

    count: int = Field(default=100, ge=1, le=1000)

    @field_validator("from_time", "to_time", mode="before")
    @classmethod
    def _coerce_time(cls, value):
        if isinstance(value, int) and value > 0:
            return datetime.fromtimestamp(value)
        return value