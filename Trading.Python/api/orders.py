from typing import List

from fastapi import APIRouter, Depends, HTTPException

from exceptions.base_exception import TradingException
from loggers.logger import logger
from models.requests.buy_request import BuyRequest
from models.requests.close_request import CloseRequest
from models.requests.modify_request import (
    CancelPendingRequest,
    HistoryRequest,
    ModifyRequest,
)
from models.requests.pending_order_request import PendingOrderRequest
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
            request.stop_loss or 0,
            request.take_profit or 0,
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
            request.stop_loss or 0,
            request.take_profit or 0,
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


@router.post("/pending", status_code=201)
def place_pending(
    request: PendingOrderRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        result = service.pending(
            request.symbol,
            request.type,
            request.volume,
            request.price,
            request.stop_loss or 0,
            request.take_profit or 0,
            int(request.expiration.timestamp()) if request.expiration else 0,
        )
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to place pending ({request.symbol}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(f"Pending order placed ({request.type} {request.symbol}): ticket={result.order}")
    return ApiResponse(
        success=True,
        data={
            "ticket": result.order,
            "symbol": request.symbol,
            "type": request.type,
            "volume": request.volume,
            "price": request.price,
            "sl": request.stop_loss or 0,
            "tp": request.take_profit or 0,
        },
    )


@router.post("/modify", status_code=200)
def modify_sltp(
    request: ModifyRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        service.modify_sltp(
            request.ticket,
            request.symbol,
            request.stop_loss or 0,
            request.take_profit or 0,
        )
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to modify ({request.ticket}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(f"Position modified ({request.ticket})")
    return ApiResponse(
        success=True,
        data={"ticket": request.ticket}
    )


@router.get("/pending-orders")
def get_pending_orders(
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    logger.info("GET /api/orders/pending-orders")
    orders = service.pending_orders()
    return ApiResponse(
        success=True,
        data=[
            {
                "ticket": order.ticket,
                "symbol": order.symbol,
                "type": _order_type_name(order.type),
                "volume": order.volume,
                "price": order.price_open,
                "sl": order.sl,
                "tp": order.tp,
                "state": _order_state_name(order.state),
                "expiration": int(order.expiration),
            }
            for order in orders
        ],
    )


@router.post("/cancel-pending", status_code=200)
def cancel_pending(
    request: CancelPendingRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    try:
        service.cancel_pending(request.ticket)
    except TradingException:
        raise
    except Exception as exc:
        logger.error(f"Failed to cancel pending ({request.ticket}): {exc}")
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info(f"Pending order cancelled ({request.ticket})")
    return ApiResponse(
        success=True,
        data={"ticket": request.ticket}
    )


@router.post("/history")
def get_history(
    request: HistoryRequest,
    service: TradeService = Depends(get_trade_service),
) -> ApiResponse:
    logger.info("Fetching trading history")
    deals = service.history(
        request.position_id,
        request.from_time,
        request.to_time,
    )
    return ApiResponse(
        success=True,
        data=[
            _deal_to_dict(deal)
            for deal in (deals[:request.count] if deals else [])
        ],
    )


def _order_type_name(order_type: int) -> str:
    import MetaTrader5 as mt5
    mapping = {
        mt5.ORDER_TYPE_BUY_LIMIT: "buy_limit",
        mt5.ORDER_TYPE_SELL_LIMIT: "sell_limit",
        mt5.ORDER_TYPE_BUY_STOP: "buy_stop",
        mt5.ORDER_TYPE_SELL_STOP: "sell_stop",
        mt5.ORDER_TYPE_BUY: "buy",
        mt5.ORDER_TYPE_SELL: "sell",
    }
    return mapping.get(order_type, "unknown")


def _order_state_name(state: int) -> str:
    from mt5.trade import ORDER_STATE_NAMES
    return ORDER_STATE_NAMES.get(state, "unknown")


def _deal_to_dict(deal):
    import MetaTrader5 as mt5
    return {
        "ticket": deal.ticket,
        "position_id": deal.position_id,
        "symbol": deal.symbol,
        "type": "buy" if deal.type == mt5.DEAL_TYPE_BUY else "sell",
        "volume": deal.volume,
        "price": deal.price,
        "profit": deal.profit,
        "commission": deal.commission,
        "swap": deal.swap,
        "fee": deal.fee,
        "time": int(deal.time),
        "comment": deal.comment or "",
    }