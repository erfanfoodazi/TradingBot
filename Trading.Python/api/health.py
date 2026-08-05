from fastapi import APIRouter

from loggers.logger import logger
from models.responses.api_response import ApiResponse

router = APIRouter()


@router.get("/health")
def health():
    logger.info("Health check requested.")
    return ApiResponse(
        success=True,
        data={"status": "OK"}
    )

# @router.post("/health")
# def health_post():
#     return {
#         "status": "Post OK"
#     }