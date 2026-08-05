from pydantic import BaseModel, Field


class CloseRequest(BaseModel):

    ticket: int = Field(..., gt=0, description="Position ticket to close")
