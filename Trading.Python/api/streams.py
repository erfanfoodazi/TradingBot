from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from config import settings
from dependencies.container import (
    get_account_stream,
    get_candle_stream,
    get_position_stream,
)
from loggers.logger import logger

router = APIRouter(tags=["streams"])


def _authorized(websocket: WebSocket) -> bool:
    if not settings.api_key:
        return True
    return websocket.query_params.get("api_key") == settings.api_key


async def _broadcast_loop(websocket: WebSocket, queue, unsubscribe, *args):
    await websocket.accept()
    try:
        while True:
            payload = await queue.get()
            await websocket.send_json(payload)
    except WebSocketDisconnect:
        logger.info("Stream disconnected.")
    except Exception:
        logger.exception("Stream error")
    finally:
        if args:
            await unsubscribe(*args, queue)
        else:
            await unsubscribe(queue)


@router.websocket("/ws/candles/{symbol}/{timeframe}")
async def candle_stream(websocket: WebSocket, symbol: str, timeframe: str):
    if not _authorized(websocket):
        await websocket.close(code=1008)
        return
    stream = get_candle_stream()
    queue = await stream.subscribe(symbol, timeframe)
    await _broadcast_loop(websocket, queue, stream.unsubscribe, symbol, timeframe)


@router.websocket("/ws/positions")
async def positions_stream(websocket: WebSocket):
    if not _authorized(websocket):
        await websocket.close(code=1008)
        return
    stream = get_position_stream()
    queue = await stream.subscribe()
    await _broadcast_loop(websocket, queue, stream.unsubscribe)


@router.websocket("/ws/account")
async def account_stream(websocket: WebSocket):
    if not _authorized(websocket):
        await websocket.close(code=1008)
        return
    stream = get_account_stream()
    queue = await stream.subscribe()
    await _broadcast_loop(websocket, queue, stream.unsubscribe)