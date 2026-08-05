from fastapi import HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from exceptions.base_exception import TradingException
from loggers.logger import logger
from models.responses.api_response import ApiResponse


def _error_response(status_code: int, message: str, data=None) -> JSONResponse:
    return JSONResponse(
        status_code=status_code,
        content=ApiResponse(
            success=False,
            message=message,
            data=data
        ).model_dump()
    )


async def trading_exception_handler(
    request: Request,
    exc: TradingException
):

    logger.error(exc.message)

    return _error_response(
        status_code=exc.status_code,
        message=exc.message
    )


async def http_exception_handler(
    request: Request,
    exc: HTTPException
):

    logger.error(exc.detail)

    return _error_response(
        status_code=exc.status_code,
        message=str(exc.detail)
    )


def _serializable_errors(errors) -> list[dict]:
    result = []
    for error in errors:
        error = dict(error)
        ctx = error.get("ctx")
        if ctx:
            error["ctx"] = {
                key: str(value) for key, value in ctx.items()
            }
        result.append(error)
    return result


async def validation_exception_handler(
    request: Request,
    exc: RequestValidationError
):

    errors = _serializable_errors(exc.errors())
    logger.error(f"Validation error: {errors}")

    return _error_response(
        status_code=422,
        message="Validation error",
        data=errors
    )


async def generic_exception_handler(
    request: Request,
    exc: Exception
):

    logger.exception(exc)

    return _error_response(
        status_code=500,
        message="Internal Server Error"
    )