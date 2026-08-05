from loggers.logger import logger


class AccountService:

    def __init__(self, account):
        self.account = account

    def get_info(self):
        info = self.account.get_info()
        logger.info("AccountService.get_info")
        return self.account.as_dict(info)