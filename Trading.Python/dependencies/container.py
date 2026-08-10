from config import settings
from loggers.logger import logger
from mt5.account import Account
from mt5.connector import MT5Connector
from mt5.market import Market
from mt5.trade import Trade

from services.account_service import AccountService
from services.account_stream import AccountStream
from services.broadcaster import Broadcaster
from services.candle_service import CandleService
from services.candle_stream import CandleStream
from services.connection_monitor import ConnectionMonitor
from services.position_stream import PositionStream
from services.price_feed import PriceFeed
from services.trade_service import TradeService


logger.info("Initializing dependencies...")

_connector = MT5Connector()
_connector.connect()

_market = Market(_connector)
_trade = Trade(_connector)
_account = Account(_connector)

_candle_service = CandleService(_market)
_trade_service = TradeService(_trade)
_account_service = AccountService(_account)
_price_feed = PriceFeed(_market)
_connection_monitor = ConnectionMonitor(
    _connector,
    check_interval=settings.mt5_monitor_interval,
)

_broadcaster = Broadcaster()
_candle_stream = CandleStream(_market, _broadcaster)
_position_stream = PositionStream(_market, _broadcaster, trade=_trade)
_account_stream = AccountStream(_account, _broadcaster)

logger.info("Dependencies initialized.")


def get_candle_service():

    return _candle_service


def get_trade_service():

    return _trade_service


def get_account_service():

    return _account_service


def get_market():

    return _market


def get_trade():

    return _trade


def get_connector():

    return _connector


def get_price_feed():

    return _price_feed


def get_connection_monitor():

    return _connection_monitor


def get_candle_stream():

    return _candle_stream


def get_position_stream():

    return _position_stream


def get_account_stream():

    return _account_stream