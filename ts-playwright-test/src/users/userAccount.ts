export interface UserAccount {
  readonly username: string;
  readonly password: string;
  readonly canLogIn: boolean;
  readonly canCompleteCheckout: boolean;
}
