import asyncio

from loggers.logger import logger
from mt5.connector import MT5Connector


class ConnectionMonitor:

    def __init__(self, connector: MT5Connector, check_interval: float = 5.0):
        self.connector = connector
        self.check_interval = check_interval
        self._task: asyncio.Task | None = None
        self._was_connected: bool | None = None

    async def start(self):
        if self._task is None or self._task.done():
            self._task = asyncio.create_task(self._run())
            logger.info(
                f"Connection monitor started (check interval={self.check_interval}s)."
            )

    async def stop(self):
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
            self._task = None
            logger.info("Connection monitor stopped.")

    async def _run(self):
        while True:
            await self._check()
            await asyncio.sleep(self.check_interval)

    async def _check(self):
        connected = self.connector.is_connected

        healthy = False
        if connected:
            try:
                healthy = await asyncio.to_thread(self.connector.health)
            except Exception:
                healthy = False

        if connected and healthy:
            if self._was_connected is not True:
                logger.info("MT5 connection is healthy.")
            self._was_connected = True
            return

        logger.warning("MT5 connection lost, attempting reconnect...")
        try:
            ok = await asyncio.to_thread(self.connector.reconnect)
        except Exception as exc:
            logger.error(f"Reconnect attempt raised: {exc}")
            ok = False

        if ok:
            logger.info("MT5 reconnected successfully.")
            self._was_connected = True
        else:
            logger.error(f"MT5 reconnect failed: {self.connector.last_error()}")
            self._was_connected = False