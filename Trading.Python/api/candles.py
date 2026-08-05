from typing import List

from fastapi import APIRouter, Depends

from loggers.logger import logger
from models.requests.candle_request import CandleRequest

from services.candle_service import CandleService

from dependencies.container import get_candle_service
from models.responses.api_response import ApiResponse
from models.responses.candle_response import CandleResponse

router = APIRouter(prefix="/api/candles", tags=["candles"])


@router.post("", response_model=ApiResponse[List[CandleResponse]])
def candles(
    request: CandleRequest,
    service: CandleService = Depends(get_candle_service),
) -> ApiResponse[List[CandleResponse]]:

    logger.info(
        f"POST /api/candles ({request.symbol}, tf={request.timeframe}, count={request.count})"
    )
    return ApiResponse(
        success=True,
        data=service.get_candles(
            request.symbol,
            request.timeframe,
            request.count
        )
    )