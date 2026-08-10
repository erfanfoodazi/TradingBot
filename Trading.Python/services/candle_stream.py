import asyncio
from typing import Dict, Optional, Tuple

from enums.timeframe import TimeFrame
from loggers.logger import logger
from mt5.market import Market
from services.broadcaster import Broadcaster


class CandleStream:
    """Streams the latest (forming) candle bar for subscribed symbol/timeframe pairs."""

    def __init__(
        self,
        market: Market,
        broadcaster: Broadcaster,
        poll_interval: float = 0.5,
    ):
        self.market = market
        self.broadcaster = broadcaster
        self.poll_interval = poll_interval
        self._tasks: Dict[Tuple[str, str], asyncio.Task] = {}
        self._last: Dict[Tuple[str, str], Optional[dict]] = {}

    def _key(self, symbol: str, timeframe: str) -> Tuple[str, str]:
        return symbol.upper(), timeframe.upper()

    async def subscribe(self, symbol: str, timeframe: str) -> asyncio.Queue:
        key = self._key(symbol, timeframe)
        queue = await self.broadcaster.subscribe(f"candle:{symbol}:{timeframe}")
        await self._ensure_running(key, symbol, timeframe)
        return queue

    async def unsubscribe(self, symbol: str, timeframe: str, queue: asyncio.Queue):
        key = self._key(symbol, timeframe)
        await self.broadcaster.unsubscribe(
            f"candle:{symbol}:{timeframe}", queue
        )
        if self.broadcaster.count == 0:
            await self._stop(key)

    async def _ensure_running(self, key, symbol, timeframe):
        task = self._tasks.get(key)
        if task is None or task.done():
            self._last[key] = None
            self._tasks[key] = asyncio.create_task(self._run(key, symbol, timeframe))

    async def _stop(self, key):
        task = self._tasks.pop(key, None)
        if task is not None:
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass
        self._last.pop(key, None)

    async def _run(self, key: Tuple[str, str], symbol: str, timeframe: str):
        while True:
            try:
                payload = await asyncio.to_thread(self._sample, symbol, timeframe)
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.exception(f"Candle stream error ({symbol}/{timeframe})")
                await asyncio.sleep(self.poll_interval)
                continue

            if payload is not None:
                if payload != self._last.get(key):
                    self._last[key] = payload
                    logger.info(
                        f"Candle publish {payload['symbol']} {payload['timeframe']} "
                        f"close={payload['close']:.5f}"
                    )
                    await self.broadcaster.publish(
                        f"candle:{symbol}:{timeframe}", payload
                    )

            await asyncio.sleep(self.poll_interval)

    def _sample(self, symbol: str, timeframe: str) -> Optional[dict]:
        tf = TimeFrame.to_mt5(timeframe)
        candles = self.market.get_candles(symbol, tf, 0, 1)
        if candles is None or len(candles) == 0:
            return None
        row = candles[-1]
        return {
            "type": "candle",
            "symbol": symbol,
            "timeframe": timeframe,
            "time": int(row["time"]),
            "open": float(row["open"]),
            "high": float(row["high"]),
            "low": float(row["low"]),
            "close": float(row["close"]),
            "tick_volume": int(row["tick_volume"]),
        }