from exceptions.base_exception import TradingException


class SymbolNotFoundException(TradingException):

    def __init__(self, symbol: str):

        super().__init__(
            f"Symbol '{symbol}' not found.",
            status_code=404
        )