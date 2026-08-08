package com.saucedemo.uitests.testdata;

import java.util.List;

/** Fixed saucedemo.com product names - reference data, never randomised, since it must match the real UI. */
public final class ProductCatalog {

    public static final String SAUCE_LABS_BACKPACK = "Sauce Labs Backpack";
    public static final String SAUCE_LABS_BIKE_LIGHT = "Sauce Labs Bike Light";
    public static final String SAUCE_LABS_BOLT_T_SHIRT = "Sauce Labs Bolt T-Shirt";
    public static final String SAUCE_LABS_FLEECE_JACKET = "Sauce Labs Fleece Jacket";
    public static final String SAUCE_LABS_ONESIE = "Sauce Labs Onesie";
    public static final String TEST_ALL_THE_THINGS_T_SHIRT = "Test.allTheThings() T-Shirt (Red)";

    public static final List<String> ALL = List.of(
            SAUCE_LABS_BACKPACK,
            SAUCE_LABS_BIKE_LIGHT,
            SAUCE_LABS_BOLT_T_SHIRT,
            SAUCE_LABS_FLEECE_JACKET,
            SAUCE_LABS_ONESIE,
            TEST_ALL_THE_THINGS_T_SHIRT);

    private ProductCatalog() {
    }
}
