import asyncio
from typing import Optional

from loggers.logger import logger
from mt5.account import Account
from services.broadcaster import Broadcaster


class AccountStream:
    """Streams account info snapshots when the values change."""

    CHANNEL = "account"

    def __init__(
        self,
        account: Account,
        broadcaster: Broadcaster,
        poll_interval: float = 1.0,
    ):
        self.account = account
        self.broadcaster = broadcaster
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
                payload = await asyncio.to_thread(self._snapshot)
                await self._publish(payload)
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.exception("Account stream error")
            await asyncio.sleep(self.poll_interval)

    def _snapshot(self) -> dict:
        info = self.account.get_info()
        data = self.account.as_dict(info)
        return {"type": "account", **data}

    async def _publish(self, payload: dict):
        import json
        signature = json.dumps(payload, sort_keys=True,
                               default=str)
        if signature == self._last_signature:
            return
        self._last_signature = signature
        await self.broadcaster.publish(self.CHANNEL, payload)