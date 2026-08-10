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

    def pending(
        self,
        symbol,
        order_name,
        volume,
        price,
        stop_loss=0,
        take_profit=0,
        expiration=0,
    ):
        logger.info(f"TradeService.pending ({order_name}, {symbol}, vol={volume}, price={price})")
        return self.trade.pending(
            symbol,
            order_name,
            volume,
            price,
            stop_loss,
            take_profit,
            expiration,
        )

    def modify_sltp(self, ticket, symbol="", stop_loss=0, take_profit=0):
        logger.info(f"TradeService.modify_sltp (ticket={ticket}, sl={stop_loss}, tp={take_profit})")
        return self.trade.modify_sltp(ticket, symbol, stop_loss, take_profit)

    def pending_orders(self):
        return self.trade.pending_orders()

    def cancel_pending(self, ticket: int):
        logger.info(f"TradeService.cancel_pending ({ticket})")
        return self.trade.cancel_pending(ticket)

    def history(self, position_id=0, from_time=0, to_time=0):
        logger.info(f"TradeService.history (position_id={position_id})")
        return self.trade.history(position_id, from_time, to_time)