@sorting
Feature: Product sorting
  As a saucedemo shopper
  I want to sort the product listing by name
  So that I can find products more easily

  Background:
    Given I log in with my assigned user

  Scenario Outline: Sorting products by name orders them alphabetically
    When I sort products by "<sortOption>"
    Then the products should be listed in <direction> alphabetical order

    Examples:
      | sortOption | direction  |
      | az         | ascending  |
      | za         | descending |
