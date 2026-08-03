from dataclasses import dataclass


@dataclass(frozen=True)
class UserAccount:
    username: str
    password: str
    can_log_in: bool
    can_complete_checkout: bool

    def __str__(self) -> str:
        return self.username
