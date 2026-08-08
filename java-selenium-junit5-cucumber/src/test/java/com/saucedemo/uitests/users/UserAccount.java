package com.saucedemo.uitests.users;

public record UserAccount(String username, String password, boolean canLogIn, boolean canCompleteCheckout) {

    @Override
    public String toString() {
        return username;
    }
}
