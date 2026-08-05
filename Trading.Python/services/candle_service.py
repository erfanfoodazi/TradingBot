import pandas as pd
from exceptions.validation_exception import ValidationException
from loggers.logger import logger
from enums.timeframe import TimeFrame
from models.responses.candle_response import CandleResponse
class CandleService:

    def __init__(self, market):
        self.market = market

    def get_candles(
        self,
        symbol: str,
        timeframe: int,
        count: int
    ):
        try:
            tf = TimeFrame.to_mt5(timeframe)
        except ValueError as exc:
            logger.error(str(exc))
            raise ValidationException(str(exc))

        candles = self.market.get_candles(
        symbol,
        tf,
        0,
        count
        )

        result = []

        for row in candles:
        
            result.append(
            
                CandleResponse(
                
                    time=pd.to_datetime(
                        row["time"],
                        unit="s"
                    ),
        
                    open=row["open"],
        
                    high=row["high"],
        
                    low=row["low"],
        
                    close=row["close"],
        
                    tick_volume=row["tick_volume"],
        
                    spread=row["spread"],
        
                    real_volume=row["real_volume"]
        
                )
        
            )
        
        logger.info(f"Returned {len(result)} candles ({symbol}, tf={timeframe}).")
        return result