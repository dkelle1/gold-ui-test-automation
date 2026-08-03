from dataclasses import dataclass


@dataclass(frozen=True)
class CheckoutInfo:
    first_name: str
    last_name: str
    postal_code: str
