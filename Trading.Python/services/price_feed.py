import asyncio
from typing import Dict, Set

from loggers.logger import logger
from mt5.market import Market


class PriceFeed:

    def __init__(self, market: Market, poll_interval: float = 0.5):
        self.market = market
        self.poll_interval = poll_interval
        self._subscribers: Dict[str, Set[asyncio.Queue]] = {}
        self._last_payload: Dict[str, dict] = {}
        self._task: asyncio.Task | None = None

    async def start(self):
        if self._task is None or self._task.done():
            self._task = asyncio.create_task(self._run())
            logger.info(f"Price feed started (poll interval={self.poll_interval}s).")

    async def stop(self):
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
            self._task = None
            logger.info("Price feed stopped.")

    async def subscribe(self, symbol: str) -> asyncio.Queue:
        symbol = symbol.upper()
        queue: asyncio.Queue = asyncio.Queue(maxsize=100)
        self._subscribers.setdefault(symbol, set()).add(queue)
        logger.info(f"Subscribed to price feed ({symbol}).")
        return queue

    async def unsubscribe(self, symbol: str, queue: asyncio.Queue):
        symbol = symbol.upper()
        subscribers = self._subscribers.get(symbol)
        if subscribers:
            subscribers.discard(queue)
            if not subscribers:
                self._subscribers.pop(symbol, None)
        logger.info(f"Unsubscribed from price feed ({symbol}).")

    async def _run(self):
        while True:
            try:
                await self._publish_ticks()
            except Exception:
                logger.exception("Price feed error")
            await asyncio.sleep(self.poll_interval)

    async def _publish_ticks(self):
        for symbol, queues in list(self._subscribers.items()):
            try:
                tick = await asyncio.to_thread(self.market.get_tick, symbol)
            except Exception:
                logger.warning(f"Tick unavailable ({symbol})")
                continue

            payload = {
                "symbol": symbol,
                "time_msc": int(tick.time_msc),
                "time": int(tick.time),
                "bid": float(tick.bid),
                "ask": float(tick.ask),
                "last": float(tick.last),
                "volume": int(tick.volume),
                "volume_real": float(tick.volume_real),
                "flags": int(tick.flags),
            }

            if payload == self._last_payload.get(symbol):
                continue

            self._last_payload[symbol] = payload

            for queue in list(queues):
                self._put_or_drop_oldest(queue, payload)

    @staticmethod
    def _put_or_drop_oldest(queue: asyncio.Queue, payload: dict):
        if queue.full():
            try:
                queue.get_nowait()
            except asyncio.QueueEmpty:
                return
        try:
            queue.put_nowait(payload)
        except asyncio.QueueFull:
            pass
