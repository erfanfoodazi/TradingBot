from typing import List

from fastapi import APIRouter, Depends

from loggers.logger import logger
from models.responses.api_response import ApiResponse
from mt5.market import Market
from dependencies.container import get_market

router = APIRouter(prefix="/api/symbols", tags=["symbols"])


@router.get("", response_model=ApiResponse[List[str]])
def list_symbols(market: Market = Depends(get_market)) -> ApiResponse[List[str]]:
    logger.info("GET /api/symbols")
    return ApiResponse(
        success=True,
        data=market.get_symbols()
    )


@router.get("/{symbol}")
def get_symbol_info(
    symbol: str,
    market: Market = Depends(get_market)
) -> ApiResponse:
    logger.info(f"GET /api/symbols/{symbol}")
    info = market.get_symbol_info(symbol)
    return ApiResponse(
        success=True,
        data={"symbol": symbol, "found": True, **info._asdict()}
    )