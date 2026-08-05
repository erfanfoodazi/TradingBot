from datetime import datetime

from pydantic import BaseModel


class CandleResponse(BaseModel):

    time: datetime

    open: float

    high: float

    low: float

    close: float

    tick_volume: int

    spread: int

    real_volume: int