from loggers.logger import logger
from mt5.connector import MT5Connector

connector = MT5Connector()

logger.info(f"connect(): {connector.connect()}")

logger.info(f"health(): {connector.health()}")

logger.info(f"is_connected: {connector.is_connected}")

logger.info(f"last_error(): {connector.last_error()}")

connector.disconnect()

logger.info(f"is_connected after disconnect: {connector.is_connected}")