from datetime import datetime

import MetaTrader5 as mt5

from config import settings
from exceptions.order_exception import OrderException
from loggers.logger import logger


ORDER_TYPES = {
    "buy_limit": mt5.ORDER_TYPE_BUY_LIMIT,
    "sell_limit": mt5.ORDER_TYPE_SELL_LIMIT,
    "buy_stop": mt5.ORDER_TYPE_BUY_STOP,
    "sell_stop": mt5.ORDER_TYPE_SELL_STOP,
}

ORDER_STATE_NAMES = {
    mt5.ORDER_STATE_STARTED: "Started",
    mt5.ORDER_STATE_PLACED: "Placed",
    mt5.ORDER_STATE_CANCELED: "Canceled",
    mt5.ORDER_STATE_PARTIAL: "Partial",
    mt5.ORDER_STATE_FILLED: "Filled",
    mt5.ORDER_STATE_REJECTED: "Rejected",
    mt5.ORDER_STATE_EXPIRED: "Expired",
    mt5.ORDER_STATE_REQUEST_ADD: "Requested",
    mt5.ORDER_STATE_REQUEST_MODIFY: "ModifyRequested",
    mt5.ORDER_STATE_REQUEST_CANCEL: "CancelRequested",
}

RETCODE_NAMES = {
    10004: "Requote",
    10006: "Request Rejected",
    10007: "Request Cancelled",
    10008: "Order Placed",
    10009: "Request Done",
    10010: "Modification Done",
    10011: "Partial Done",
    10012: "Request Processed",
    10013: "Request Cancelled",
    10014: "Invalid Request",
    10015: "Invalid Volume",
    10016: "Invalid Price",
    10017: "Invalid Stops",
    10018: "Trade Disabled",
    10019: "Market Closed",
    10020: "No Money",
    10021: "Price Changed",
    10022: "Price Off",
    10023: "Invalid Expiration",
    10024: "Order Changed",
    10025: "Too Many Requests",
    10026: "No Changes",
    10027: "Server Busy",
    10028: "No Connection",
    10029: "Too Many Retries",
    10030: "Invalid Filling",
    10031: "Invalid Order Type",
    10032: "Invalid Position",
    10033: "Modification Denied",
    10034: "Order Locked",
    10035: "Invalid Close Volume",
    10036: "Position Closed",
    10037: "Invalid Close Price",
    10038: "Pending Order Closed",
}


def _retcode_name(code: int) -> str:
    return RETCODE_NAMES.get(code, "Unknown")


FILLING_MODES = (
    mt5.ORDER_FILLING_FOK,
    mt5.ORDER_FILLING_IOC,
    mt5.ORDER_FILLING_RETURN,
)

TRADE_RETCODE_INVALID_FILLING = 10030


def _reject_reason(result) -> str:
    if result is None:
        return f"unknown error - {mt5.last_error()}"
    return (
        f"retcode={result.retcode} ({_retcode_name(result.retcode)}) "
        f"comment='{result.comment}'"
    )


class Trade:

    def __init__(self, connector):
        self.connector = connector

    @staticmethod
    def _round_price(symbol: str, value: float) -> float:
        """Rounds a price/SL/TP to the symbol's tick size."""
        info = mt5.symbol_info(symbol)
        if value is None or value <= 0 or info is None:
            return value
        tick_size = getattr(info, "trade_tick_size", None) or getattr(
            info, "point", None
        )
        if tick_size is None:
            return value
        return round(value / tick_size) * tick_size

    @staticmethod
    def _stops(symbol: str, sl, tp) -> dict:
        """Returns a dict that only includes SL/TP when they are actually set.

        Passing 0 (or None) explicitly for a stop on a market order makes
        MetaTrader raise 'Invalid "sl"/"tp" argument', so we omit them.
        """
        result = {}
        if sl:
            result["sl"] = Trade._round_price(symbol, float(sl))
        if tp:
            result["tp"] = Trade._round_price(symbol, float(tp))
        return result

    def _place_order(self, request: dict, symbol: str, action: str):
        """Sends an order, retrying the other filling modes when the broker
        reports the chosen mode as unsupported (retcode 10030).
        """
        self.connector.ensure_connected()

        advertised = self._filling_mode(symbol)
        modes = [advertised] + [m for m in FILLING_MODES if m != advertised]

        last_reason = None
        for mode in modes:
            request["type_filling"] = mode
            result = mt5.order_send(request)

            if result is not None and result.retcode == mt5.TRADE_RETCODE_DONE:
                return result

            last_reason = (
                _reject_reason(result)
                if result is not None
                else f"unknown error - {mt5.last_error()}"
            )
            logger.warning(
                f"{action} order with filling={mode} rejected ({symbol}): {last_reason}"
            )

            # If MT5 accepted the filling mode but rejected the order for
            # another reason, there is no point trying different modes.
            if (
                result is not None
                and result.retcode != TRADE_RETCODE_INVALID_FILLING
            ):
                raise OrderException(f"{action} order rejected ({symbol}): {last_reason}")

        raise OrderException(
            f"{action} order rejected ({symbol}): unsupported filling mode. "
            f"Last error: {last_reason}"
        )

    def _ensure_trade_allowed(self) -> None:
        self.connector.ensure_connected()

        terminal = mt5.terminal_info()
        if terminal is not None and not terminal.trade_allowed:
            raise OrderException(
                "Trading is disabled in the MT5 terminal. "
                "Click the 'Algo Trading' button (or Tools > Options > Expert Advisors "
                "> 'Allow algorithmic trading') and try again."
            )

        account = mt5.account_info()
        if account is not None and not account.trade_allowed:
            raise OrderException(
                "Trading is disabled for this account by the server "
                "or the account is read-only."
            )

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
        self._ensure_trade_allowed()

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
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": mt5.ORDER_TIME_GTC,
        }
        request.update(self._stops(symbol, stop_loss, take_profit))

        logger.info(f"Sending BUY order ({symbol}, vol={volume}, price={tick.ask})")
        result = self._place_order(request, symbol, "BUY")

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
        self._ensure_trade_allowed()

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
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": mt5.ORDER_TIME_GTC,
        }
        request.update(self._stops(symbol, stop_loss, take_profit))

        logger.info(f"Sending SELL order ({symbol}, vol={volume}, price={tick.bid})")
        result = self._place_order(request, symbol, "SELL")

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
        self._ensure_trade_allowed()

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
        }

        logger.info(f"Closing position ({ticket}, {position.symbol}, vol={position.volume})")
        result = self._place_order(request, position.symbol, "Close")

        logger.info(f"Position closed ({ticket})")
        return result

    def pending(
        self,
        symbol: str,
        order_name: str,
        volume: float,
        price: float,
        stop_loss: float = 0,
        take_profit: float = 0,
        expiration: int = 0,
        deviation: int | None = None,
    ):
        self._ensure_trade_allowed()

        order_type = ORDER_TYPES.get(order_name.lower())
        if order_type is None:
            logger.error(f"Unsupported pending order type: {order_name}")
            raise OrderException(f"Unsupported pending order type: {order_name}")

        type_time = mt5.ORDER_TIME_GTC
        request = {
            "action": mt5.TRADE_ACTION_PENDING,
            "symbol": symbol,
            "volume": volume,
            "type": order_type,
            "price": price,
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
            "type_time": type_time,
        }
        request.update(self._stops(symbol, stop_loss, take_profit))

        if expiration and expiration > 0:
            request["type_time"] = mt5.ORDER_TIME_SPECIFIED
            request["expiration"] = expiration

        logger.info(
            f"Sending {order_name} order "
            f"({symbol}, vol={volume}, price={price}, sl={stop_loss}, tp={take_profit})"
        )
        result = self._place_order(request, symbol, order_name.upper())

        logger.info(f"{order_name.upper()} order placed ({symbol}): ticket={result.order}")
        return result

    def modify_sltp(
        self,
        ticket: int,
        symbol: str = "",
        stop_loss: float = 0,
        take_profit: float = 0,
        deviation: int | None = None,
    ):
        self._ensure_trade_allowed()

        if not symbol:
            position = mt5.positions_get(ticket=ticket)
            if position:
                symbol = position[0].symbol
            else:
                order = mt5.orders_get(ticket=ticket)
                if order:
                    symbol = order[0].symbol
                else:
                    logger.error(f"Modify failed: position/order {ticket} not found")
                    raise OrderException(f"Position/order {ticket} not found")

        request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": symbol,
            "position": ticket,
            "sl": stop_loss,
            "tp": take_profit,
            "deviation": deviation if deviation is not None else settings.deviation,
            "magic": settings.magic_number,
        }

        logger.info(
            f"Modifying SL/TP (ticket={ticket}, symbol={symbol}, sl={stop_loss}, tp={take_profit})"
        )
        result = mt5.order_send(request)

        if result is None or result.retcode != mt5.TRADE_RETCODE_DONE:
            logger.error(f"Modify rejected (ticket={ticket}): {result}")
            raise OrderException(f"Modify rejected (ticket={ticket}): {result}")

        logger.info(f"Position modified (ticket={ticket}).")
        return result

    def pending_orders(self):
        self.connector.ensure_connected()

        orders = mt5.orders_get()
        if orders is None:
            logger.warning(f"Failed to load pending orders: {mt5.last_error()}")
            return []
        logger.info(f"Loaded {len(orders)} pending order(s).")
        return orders

    def cancel_pending(self, ticket: int):
        self._ensure_trade_allowed()

        request = {
            "action": mt5.TRADE_ACTION_REMOVE,
            "order": ticket,
        }

        logger.info(f"Cancelling pending order ({ticket})")
        result = mt5.order_send(request)

        if result is None or result.retcode != mt5.TRADE_RETCODE_DONE:
            logger.error(f"Cancel pending rejected ({ticket}): {result}")
            raise OrderException(f"Cancel pending rejected ({ticket}): {result}")

        logger.info(f"Pending order cancelled ({ticket})")
        return result

    def history(self, position_id: int = 0, from_time=0, to_time=0):
        self.connector.ensure_connected()

        if from_time and to_time:
            deals = mt5.history_deals_get(from_time, to_time)
        elif position_id:
            deals = mt5.history_deals_get(self_id=position_id)
        else:
            deals = mt5.history_deals_get()

        if deals is None:
            logger.warning(f"Failed to load history: {mt5.last_error()}")
            return []

        logger.info(f"Loaded {len(deals)} deal(s) from history.")
        return deals
