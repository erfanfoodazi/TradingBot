from loggers.logger import logger


class TradeService:

    def __init__(self, trade):
        self.trade = trade

    def buy(
        self,
        symbol,
        volume,
        stop_loss,
        take_profit
    ):

        logger.info(f"TradeService.buy ({symbol}, vol={volume})")
        return self.trade.buy(
            symbol,
            volume,
            stop_loss,
            take_profit
        )

    def sell(
        self,
        symbol,
        volume,
        stop_loss,
        take_profit
    ):

        logger.info(f"TradeService.sell ({symbol}, vol={volume})")
        return self.trade.sell(
            symbol,
            volume,
            stop_loss,
            take_profit
        )

    def positions(self):
        return self.trade.positions()

    def close(self, ticket: int):
        logger.info(f"TradeService.close ({ticket})")
        return self.trade.close(ticket)