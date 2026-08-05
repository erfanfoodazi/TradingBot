import MetaTrader5 as mt5

from exceptions.connection_exception import ConnectionException
from loggers.logger import logger

TRADE_MODE_NAMES = {
    0: "demonstration",
    1: "contest",
    2: "real",
}

MARGIN_SO_MODE_NAMES = {
    0: "cfd_without_leverage",
    1: "cfd_leverage",
    2: "futures",
    3: "forex",
}


class Account:

    def __init__(self, connector):
        self.connector = connector

    def get_info(self):
        self.connector.ensure_connected()

        info = mt5.account_info()

        if info is None:
            logger.error(f"Failed to load account info: {mt5.last_error()}")
            raise ConnectionException("Failed to load account info")

        logger.info(f"Loaded account info (login={info.login}).")
        return info

    def as_dict(self, info):
        return {
            "login": info.login,
            "trade_mode": TRADE_MODE_NAMES.get(
                info.trade_mode,
                info.trade_mode
            ),
            "leverage": info.leverage,
            "trade_allowed": bool(info.trade_allowed),
            "margin_mode": MARGIN_SO_MODE_NAMES.get(
                info.margin_mode,
                info.margin_mode
            ),
            "currency": info.currency,
            "balance": info.balance,
            "equity": info.equity,
            "margin": info.margin,
            "margin_free": info.margin_free,
            "margin_level": info.margin_level,
            "profit": info.profit,
            "credit": info.credit,
        }