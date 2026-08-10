import asyncio
from typing import Optional

from loggers.logger import logger
from mt5.market import Market
from services.broadcaster import Broadcaster
from services.snapshots import position_to_dict


class PositionStream:
    """Streams snapshots of open positions when they change."""

    CHANNEL = "positions"

    def __init__(
        self,
        market: Market,
        broadcaster: Broadcaster,
        trade=None,
        poll_interval: float = 1.0,
    ):
        self.market = market
        self.broadcaster = broadcaster
        self.trade = trade
        self.poll_interval = poll_interval
        self._task: asyncio.Task | None = None
        self._last_signature: str | None = None

    async def subscribe(self) -> asyncio.Queue:
        queue = await self.broadcaster.subscribe(self.CHANNEL)
        await self._ensure_running()
        return queue

    async def unsubscribe(self, queue: asyncio.Queue):
        await self.broadcaster.unsubscribe(self.CHANNEL, queue)
        if self.broadcaster.count == 0:
            await self._stop()

    async def _ensure_running(self):
        if self._task is None or self._task.done():
            self._last_signature = None
            self._task = asyncio.create_task(self._run())

    async def _stop(self):
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
            self._task = None
        self._last_signature = None

    async def _run(self):
        while True:
            try:
                positions = await asyncio.to_thread(self._load)
                await self._publish(positions)
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.exception("Position stream error")
            await asyncio.sleep(self.poll_interval)

    def _load(self):
        if self.trade is not None:
            return self.trade.positions()
        return self.market.get_positions() if hasattr(self.market, "get_positions") else []

    async def _publish(self, positions):
        payload = {
            "type": "positions",
            "positions": [position_to_dict(p) for p in (positions or [])],
        }
        import json
        signature = json.dumps(payload, sort_keys=True)
        if signature == self._last_signature:
            return
        self._last_signature = signature
        await self.broadcaster.publish(self.CHANNEL, payload)