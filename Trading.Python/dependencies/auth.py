from fastapi import Header, HTTPException, status

from config import settings


def verify_api_key(x_api_key: str = Header(default="")):
    """
    Simple shared-secret check between the WPF client and this connector.
    Set API_KEY in the environment; if it's left empty, auth is disabled
    (useful for local dev, but NOT recommended once exposed to a network).
    """
    if not settings.api_key:
        return

    if x_api_key != settings.api_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing API key."
        )