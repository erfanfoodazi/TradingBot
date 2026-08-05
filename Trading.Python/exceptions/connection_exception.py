from exceptions.base_exception import TradingException


class ConnectionException(TradingException):

    def __init__(
        self,
        message="MetaTrader connection failed."
    ):
        super().__init__(
            message,
            status_code=503
        )