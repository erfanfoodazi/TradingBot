from pydantic import BaseModel


class SymbolInfoResponse(BaseModel):
    """Explicit trade specs for a symbol, used to translate monetary risk /
    reward targets into SL/TP price levels.

    Maps from the raw MT5 ``mt5.symbol_info()`` namedtuple:
      - ``point`` ............ smallest point size in price units
      - ``tick_size`` ........ ``trade_tick_size``
      - ``tick_value`` ....... ``trade_tick_value`` (account currency, per
                               1.0 lot, per medium tick)
      - ``contract_size` ..... ``trade_contract_size`` (base units per lot)
    """

    symbol: str

    digits: int

    point: float

    tick_size: float

    tick_value: float

    contract_size: float

    currency: str = ""

    volume_min: float = 0

    volume_max: float = 0

    volume_step: float = 0