import asyncio

from loggers.logger import logger


class Broadcaster:
    """Fan-out helper that delivers payloads to subscribed queues."""

    def __init__(self, maxsize: int = 100):
        self._maxsize = maxsize
        self._subscribers: dict[str, set[asyncio.Queue]] = {}
        self._lock = asyncio.Lock()

    async def subscribe(self, key: str) -> asyncio.Queue:
        queue: asyncio.Queue = asyncio.Queue(maxsize=self._maxsize)
        async with self._lock:
            self._subscribers.setdefault(key, set()).add(queue)
        logger.info(f"Broadcaster: subscribed ({key}).")
        return queue

    async def unsubscribe(self, key: str, queue: asyncio.Queue):
        async with self._lock:
            subscribers = self._subscribers.get(key)
            if subscribers:
                subscribers.discard(queue)
                if not subscribers:
                    self._subscribers.pop(key, None)
        logger.info(f"Broadcaster: unsubscribed ({key}).")

    async def publish(self, key: str, payload: dict):
        async with self._lock:
            queues = list(self._subscribers.get(key, ()))
        for queue in queues:
            if queue.full():
                try:
                    queue.get_nowait()
                except asyncio.QueueEmpty:
                    continue
            try:
                queue.put_nowait(payload)
            except asyncio.QueueFull:
                pass

    @property
    def count(self) -> int:
        return sum(len(v) for v in self._subscribers.values())