from exceptions.base_exception import TradingException


class OrderException(TradingException):

    def __init__(
        self,
        message="Order execution failed."
    ):

        super().__init__(
            message,
            status_code=400
        )