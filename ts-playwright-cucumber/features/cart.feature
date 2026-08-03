@cart
Feature: Cart
  As a saucedemo shopper
  I want the cart to accurately reflect what I've added or removed
  So that I don't get billed for the wrong items

  Background:
    Given I log in with my assigned user
    And my cart is empty

  Scenario: Adding and removing items updates the cart badge
    When I add "Sauce Labs Backpack" to the cart
    And I add "Sauce Labs Bike Light" to the cart
    Then the cart badge should show 2
    When I remove "Sauce Labs Backpack" from the cart
    Then the cart badge should show 1

  Scenario: Items added to the cart appear on the checkout overview
    When I add "Sauce Labs Backpack" to the cart
    And I add "Sauce Labs Bike Light" to the cart
    And I go to the cart
    Then the cart should list the following products:
      | ProductName            |
      | Sauce Labs Backpack    |
      | Sauce Labs Bike Light  |
