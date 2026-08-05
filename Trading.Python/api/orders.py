from typing import List

from fastapi import APIRouter, Depends, HTTPException

from exceptions.base_exception import TradingException
from loggers.logger import logger
from models.requests.buy_request import BuyRequest
from models.requests.close_request import CloseRequest
from models.responses.api_response import ApiResponse
from services.trade_service import TradeService
from dependencies.container import get_trade_service

router = APIRouter(prefix="/api/orders", tags=["orders"])


@router.post("/buy", status_code=201)
def buy(
    request: BuyRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        result = service.buy(
            request.symbol,
            request.volume,
            request.stop_loss,
            request.take_profit,
        )
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to buy ({request.symbol}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(
        f"Buy order placed ({request.symbol}): ticket={getattr(result, 'order', None)}"
    )
    return ApiResponse(
        success=True,
        data={
            "symbol": request.symbol,
            "ticket": getattr(result, "order", None),
        },
    )


@router.post("/sell", status_code=201)
def sell(
    request: BuyRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        result = service.sell(
            request.symbol,
            request.volume,
            request.stop_loss,
            request.take_profit,
        )
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to sell ({request.symbol}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(
        f"Sell order placed ({request.symbol}): ticket={getattr(result, 'order', None)}"
    )
    return ApiResponse(
        success=True,
        data={
            "symbol": request.symbol,
            "ticket": getattr(result, "order", None),
        },
    )


@router.post("/close", status_code=200)
def close(
    request: CloseRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        result = service.close(request.ticket)
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to close ({request.ticket}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(f"Position closed ({request.ticket})")
    return ApiResponse(
        success=True,
        data={"ticket": request.ticket}
    )


@router.get("/positions")
def get_positions(
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    logger.info("GET /api/orders/positions")
    positions = service.positions()
    return ApiResponse(
        success=True,
        data=[
            {
                "ticket": position.ticket,
                "symbol": position.symbol,
                "volume": position.volume,
                "type": "buy" if position.type == 0 else "sell",
                "price_open": position.price_open,
                "sl": position.sl,
                "tp": position.tp,
                "profit": position.profit,
            }
            for position in positions
        ],
    )