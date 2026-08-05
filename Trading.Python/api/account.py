from fastapi import APIRouter, Depends

from loggers.logger import logger
from models.responses.api_response import ApiResponse
from services.account_service import AccountService
from dependencies.container import get_account_service

router = APIRouter(prefix="/api/account", tags=["account"])


@router.get("", response_model=ApiResponse)
def account_info(
    service: AccountService = Depends(get_account_service),
) -> ApiResponse:
    logger.info("GET /api/account")
    return ApiResponse(
        success=True,
        data=service.get_info()
    )