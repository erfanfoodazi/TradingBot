import MetaTrader5 as mt5

from config import settings
from exceptions.connection_exception import ConnectionException
from loggers.logger import logger


class MT5Connector:

    def __init__(
        self,
        path: str | None = None,
        login: int | None = None,
        password: str | None = None,
        server: str | None = None,
        timeout_ms: int | None = None,
    ):
        self._path = path or settings.mt5_path
        self._login = login or settings.mt5_login
        self._password = password or settings.mt5_password
        self._server = server or settings.mt5_server
        self._timeout_ms = timeout_ms or settings.mt5_timeout_ms
        self._connected = False

    @property
    def is_connected(self) -> bool:
        return self._connected

    def connect(self) -> bool:
        logger.info("Connecting to MetaTrader...")
        if self._connected:
            logger.info("Already connected.")
            return True

        init_kwargs = {"timeout": self._timeout_ms}

        # Only pass login/password/server when they're actually configured,
        # otherwise fall back to whatever account is already logged in
        # on the running terminal.
        if self._login:
            init_kwargs.update(
                login=self._login,
                password=self._password,
                server=self._server,
            )

        if self._path:
            self._connected = mt5.initialize(self._path, **init_kwargs)
        else:
            self._connected = mt5.initialize(**init_kwargs)

        if self._connected:
            logger.info(
                f"Connected successfully "
                f"(login={self._login or 'current terminal session'})."
            )
        else:
            logger.error(f"Failed to connect: {mt5.last_error()}")

        return self._connected

    def disconnect(self):

        if self._connected:
            mt5.shutdown()
            self._connected = False
            logger.info("Disconnected from MetaTrader.")

    def reconnect(self) -> bool:

        logger.info("Reconnecting to MetaTrader...")
        self.disconnect()

        return self.connect()

    def ensure_connected(self):

        if not self.health():
            logger.warning("MetaTrader connection lost, reconnecting...")
            if not self.reconnect():
                logger.error(f"Cannot connect to MetaTrader5: {self.last_error()}")
                raise ConnectionException(
                    f"Cannot connect to MetaTrader5 : {self.last_error()}"
                )

    def health(self) -> bool:

        healthy = mt5.terminal_info() is not None
        if not healthy:
            logger.warning(f"MetaTrader terminal not healthy: {mt5.last_error()}")
        return healthy

    def last_error(self):

        return mt5.last_error()