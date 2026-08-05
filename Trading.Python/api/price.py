from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from config import settings
from dependencies.container import get_price_feed
from loggers.logger import logger

router = APIRouter(tags=["price"])


@router.websocket("/ws/price/{symbol}")
async def price_stream(websocket: WebSocket, symbol: str):
    if settings.api_key and websocket.query_params.get("api_key") != settings.api_key:
        await websocket.close(code=1008)
        return

    feed = get_price_feed()

    await websocket.accept()
    queue = await feed.subscribe(symbol)

    try:
        while True:
            payload = await queue.get()
            await websocket.send_json(payload)
    except WebSocketDisconnect:
        logger.info(f"Price stream disconnected ({symbol}).")
    except Exception:
        logger.exception(f"Price stream error ({symbol})")
    finally:
        await feed.unsubscribe(symbol, queue)
