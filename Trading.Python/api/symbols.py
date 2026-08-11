from typing import List

from fastapi import APIRouter, Depends

from loggers.logger import logger
from models.responses.api_response import ApiResponse
from models.responses.symbol_info_response import SymbolInfoResponse
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


@router.get("/{symbol}", response_model=ApiResponse[SymbolInfoResponse])
def get_symbol_info(
    symbol: str,
    market: Market = Depends(get_market)
) -> ApiResponse[SymbolInfoResponse]:
    logger.info(f"GET /api/symbols/{symbol}")
    info = market.get_symbol_info(symbol)
    return ApiResponse(
        success=True,
        data=SymbolInfoResponse(
            symbol=symbol,
            digits=getattr(info, "digits", 0),
            point=float(getattr(info, "point", 0) or 0),
            tick_size=float(getattr(info, "trade_tick_size", 0) or 0),
            tick_value=float(getattr(info, "trade_tick_value", 0) or 0),
            contract_size=float(getattr(info, "trade_contract_size", 0) or 0),
            currency=getattr(info, "currency_profit", "") or "",
            volume_min=float(getattr(info, "volume_min", 0) or 0),
            volume_max=float(getattr(info, "volume_max", 0) or 0),
            volume_step=float(getattr(info, "volume_step", 0) or 0),
        )
    )