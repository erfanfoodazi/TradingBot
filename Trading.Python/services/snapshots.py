"""Shared serialization helpers for streaming snapshots and REST responses."""

import MetaTrader5 as mt5

from mt5.trade import ORDER_STATE_NAMES


def order_type_name(order_type: int) -> str:
    mapping = {
        mt5.ORDER_TYPE_BUY_LIMIT: "buy_limit",
        mt5.ORDER_TYPE_SELL_LIMIT: "sell_limit",
        mt5.ORDER_TYPE_BUY_STOP: "buy_stop",
        mt5.ORDER_TYPE_SELL_STOP: "sell_stop",
        mt5.ORDER_TYPE_BUY: "buy",
        mt5.ORDER_TYPE_SELL: "sell",
    }
    return mapping.get(order_type, "unknown")


def order_state_name(state: int) -> str:
    return ORDER_STATE_NAMES.get(state, "unknown")


def position_to_dict(position) -> dict:
    return {
        "ticket": int(position.ticket),
        "symbol": position.symbol,
        "volume": float(position.volume),
        "type": "buy" if position.type == mt5.POSITION_TYPE_BUY else "sell",
        "price_open": float(position.price_open),
        "sl": float(position.sl),
        "tp": float(position.tp),
        "profit": float(position.profit),
    }


def pending_order_to_dict(order) -> dict:
    return {
        "ticket": int(order.ticket),
        "symbol": order.symbol,
        "type": order_type_name(order.type),
        "volume": float(order.volume),
        "price": float(order.price_open),
        "sl": float(order.sl),
        "tp": float(order.tp),
        "state": order_state_name(order.state),
        "expiration": int(order.expiration),
    }


def deal_to_dict(deal) -> dict:
    return {
        "ticket": int(deal.ticket),
        "position_id": int(deal.position_id),
        "symbol": deal.symbol,
        "type": "buy" if deal.type == mt5.DEAL_TYPE_BUY else "sell",
        "volume": float(deal.volume),
        "price": float(deal.price),
        "profit": float(deal.profit),
        "commission": float(deal.commission),
        "swap": float(deal.swap),
        "fee": float(deal.fee),
        "time": int(deal.time),
        "comment": deal.comment or "",
    }