import MetaTrader5 as mt5
from exceptions.symbol_not_found_exception import SymbolNotFoundException
from loggers.logger import logger

class Market:

    def __init__(self, connector):
        self.connector = connector

    def get_symbols(self):
        self.connector.ensure_connected()
        symbols = mt5.symbols_get()

        if symbols is None:
            logger.warning(f"Failed to load symbols: {mt5.last_error()}")
            return []

        names = [symbol.name for symbol in symbols]
        logger.info(f"Loaded {len(names)} symbols.")
        return names

    def get_symbol_info(self, symbol: str):
        self.connector.ensure_connected()
        info = mt5.symbol_info(symbol)
        if info is None:
            logger.warning(f"Symbol info not found for ({symbol}): {mt5.last_error()}")
            raise SymbolNotFoundException(symbol)
        logger.info(f"Loaded symbol info ({symbol}).")
        return info

    def get_tick(self, symbol: str):
        self.connector.ensure_connected()
        tick = mt5.symbol_info_tick(symbol)
        if tick is None:
            logger.warning(f"Tick not available for ({symbol}): {mt5.last_error()}")
            raise SymbolNotFoundException(symbol)
        return tick

    def get_candles(
        self,
        symbol: str,
        timeframe: int,
        start_position: int,
        count: int
    ):
        self.connector.ensure_connected()
        logger.info(
            f"Loading candles ({symbol}, tf={timeframe}, pos={start_position}, count={count})"
        )
        candles = mt5.copy_rates_from_pos(
            symbol,
            timeframe,
            start_position,
            count
        )
        if candles is None:
            logger.warning(f"Failed to load candles ({symbol}): {mt5.last_error()}")
            raise SymbolNotFoundException(symbol)
        return candles