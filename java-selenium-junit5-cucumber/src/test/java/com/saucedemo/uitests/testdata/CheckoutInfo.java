package com.saucedemo.uitests.testdata;

public record CheckoutInfo(String firstName, String lastName, String postalCode) {

    /** The "postal code is required" scenario needs the same generated name data with a blank postcode. */
    public CheckoutInfo withoutPostalCode() {
        return new CheckoutInfo(firstName, lastName, "");
    }
}
