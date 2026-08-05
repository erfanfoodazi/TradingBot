from exceptions.base_exception import TradingException


class ValidationException(TradingException):

    def __init__(self, message):

        super().__init__(
            message,
            status_code=422
        )