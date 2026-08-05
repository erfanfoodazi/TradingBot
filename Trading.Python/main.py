from contextlib import asynccontextmanager

import uvicorn

from fastapi import Depends, FastAPI, HTTPException
from fastapi.exceptions import RequestValidationError

from config import settings
from dependencies.container import get_connection_monitor, get_price_feed
from loggers.logger import logger
from api.candles import router as candle_router
from api.health import router as health_router
from api.orders import router as orders_router
from api.price import router as price_router
from api.symbols import router as symbols_router
from api.account import router as account_router
from dependencies.auth import verify_api_key
from exceptions.base_exception import TradingException
from exceptions.exception_handler import (
    trading_exception_handler,
    http_exception_handler,
    validation_exception_handler,
    generic_exception_handler,
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    connection_monitor = get_connection_monitor()
    price_feed = get_price_feed()
    await connection_monitor.start()
    await price_feed.start()
    yield
    await price_feed.stop()
    await connection_monitor.stop()


app = FastAPI(title="TradingBot Connector", lifespan=lifespan)

# Health check stays open (useful for monitoring/uptime checks).
app.include_router(health_router)

# Everything that touches MT5 data or places real orders requires the API key.
app.include_router(candle_router, dependencies=[Depends(verify_api_key)])
app.include_router(orders_router, dependencies=[Depends(verify_api_key)])
app.include_router(symbols_router, dependencies=[Depends(verify_api_key)])
app.include_router(account_router, dependencies=[Depends(verify_api_key)])

# Live price feed (auth via ?api_key= query param when API_KEY is set).
app.include_router(price_router)

app.add_exception_handler(
    TradingException,
    trading_exception_handler
)

app.add_exception_handler(
    HTTPException,
    http_exception_handler
)

app.add_exception_handler(
    RequestValidationError,
    validation_exception_handler
)

app.add_exception_handler(
    Exception,
    generic_exception_handler
)

logger.info("TradingBot application started.")


if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.api_host,
        port=settings.api_port,
        reload=True,
    )