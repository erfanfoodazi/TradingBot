import MetaTrader5 as mt5

from config import settings
from exceptions.order_exception import OrderException
from loggers.logger import logger


class Trade:

    def __init__(self, connector):
        self.connector = connector

    def _filling_mode(self, symbol: str) -> int:
        info = mt5.symbol_info(symbol)
        logger.info(f"Symbol info ({symbol}): {info}")

        if info is None:
            logger.warning(
                f"Symbol info unavailable ({symbol}): {mt5.last_error()}"
            )
            return mt5.ORDER_FILLING_IOC

        mode = info.filling_mode

        if mode & mt5.ORDER_FILLING_IOC:
            return mt5.ORDER_FILLING_IOC

        if mode & mt5.ORDER_FILLING_FOK:
            return mt5.ORDER_FILLING_FOK

        if mode & mt5.ORDER_FILLING_RETURN:
            return mt5.ORDER_FILLING_RETURN

        logger.warning(
            f"No filling mode advertised for ({symbol}), defaulting to IOC."
        )
        return mt5.ORDER_FILLING_IOC

    def buy(
        self,
        symbol: str,
        volume: float,
        stop_loss: float = 0,
        take_profit: float = 0,
        deviation: int | None = None
    ):
        self.connector.ensure_connected()

        tick = mt5.symbol_info_tick(symbol)

        if tick is None:
            logger.error(f"Buy failed ({symbol}): tick not available - {mt5.last_error()}")
            raise OrderException(f"Buy failed ({symbol}): tick not available")

        request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": symbol,
            "volume": volume,
            "type": mt5.ORDER_TYPE_BUY,
            "price": tick.ask,
            "sl": stop_loss,
            "tp": take_profit,
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": mt5.ORDER_TIME_GTC,
            "type_filling": mt5.ORDER_FILLING_FOK,
        }

        logger.info(f"Sending BUY order ({symbol}, vol={volume}, price={tick.ask})")
        result = mt5.order_send(request)

        if result is None or result.retcode != mt5.TRADE_RETCODE_DONE:
            logger.error(f"BUY order rejected ({symbol}): {result}")
            raise OrderException(f"BUY order rejected ({symbol}): {result}")

        logger.info(f"BUY order filled ({symbol}): ticket={result.order}")
        return result

    def sell(
        self,
        symbol: str,
        volume: float,
        stop_loss: float = 0,
        take_profit: float = 0,
        deviation: int | None = None
    ):
        self.connector.ensure_connected()

        tick = mt5.symbol_info_tick(symbol)

        if tick is None:
            logger.error(f"Sell failed ({symbol}): tick not available - {mt5.last_error()}")
            raise OrderException(f"Sell failed ({symbol}): tick not available")

        request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": symbol,
            "volume": volume,
            "type": mt5.ORDER_TYPE_SELL,
            "price": tick.bid,
            "sl": stop_loss,
            "tp": take_profit,
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": mt5.ORDER_TIME_GTC,
            "type_filling":mt5.ORDER_FILLING_FOK,
        }

        logger.info(f"Sending SELL order ({symbol}, vol={volume}, price={tick.bid})")
        result = mt5.order_send(request)

        if result is None or result.retcode != mt5.TRADE_RETCODE_DONE:
            logger.error(f"SELL order rejected ({symbol}): {result}")
            raise OrderException(f"SELL order rejected ({symbol}): {result}")

        logger.info(f"SELL order filled ({symbol}): ticket={result.order}")
        return result

    def positions(self):
        self.connector.ensure_connected()

        positions = mt5.positions_get()
        if positions is None:
            logger.warning(f"Failed to load positions: {mt5.last_error()}")
            return []
        logger.info(f"Loaded {len(positions)} open position(s).")
        return positions

    def close(self, ticket: int, deviation: int | None = None):
        self.connector.ensure_connected()

        position = mt5.positions_get(ticket=ticket)

        if not position:
            logger.error(f"Close failed: position {ticket} not found - {mt5.last_error()}")
            raise OrderException(f"Position {ticket} not found")

        position = position[0]
        tick = mt5.symbol_info_tick(position.symbol)

        if tick is None:
            logger.error(f"Close failed ({position.symbol}): tick not available - {mt5.last_error()}")
            raise OrderException(f"Close failed ({position.symbol}): tick not available")

        # Closing a position means sending the opposite order type
        is_buy = position.type == mt5.ORDER_TYPE_BUY
        close_type = mt5.ORDER_TYPE_SELL if is_buy else mt5.ORDER_TYPE_BUY
        price = tick.bid if is_buy else tick.ask

        request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": position.symbol,
            "volume": position.volume,
            "type": close_type,
            "position": ticket,
            "price": price,
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": mt5.ORDER_TIME_GTC,
            "type_filling": mt5.ORDER_FILLING_FOK,
        }

        logger.info(f"Closing position ({ticket}, {position.symbol}, vol={position.volume})")
        result = mt5.order_send(request)

        if result is None or result.retcode != mt5.TRADE_RETCODE_DONE:
            logger.error(f"Close rejected ({ticket}): {result}")
            raise OrderException(f"Close rejected ({ticket}): {result}")

        logger.info(f"Position closed ({ticket})")
        return result
