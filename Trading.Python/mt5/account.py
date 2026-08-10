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
        server = ""
        terminal = mt5.terminal_info()
        if terminal is not None:
            parts = [terminal.company or "", terminal.name or ""]
            server = " | ".join([p for p in parts if p])

        return {
            "login": int(info.login),
            "server": server,
            "trade_mode": TRADE_MODE_NAMES.get(
                info.trade_mode,
                info.trade_mode
            ),
            "leverage": int(info.leverage),
            "trade_allowed": bool(info.trade_allowed),
            "margin_mode": MARGIN_SO_MODE_NAMES.get(
                info.margin_mode,
                info.margin_mode
            ),
            "currency": info.currency,
            "balance": float(info.balance),
            "equity": float(info.equity),
            "margin": float(info.margin),
            "margin_free": float(info.margin_free),
            "margin_level": float(info.margin_level),
            "profit": float(info.profit),
            "credit": float(info.credit),
        }