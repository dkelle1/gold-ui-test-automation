@e2e @checkout
Feature: Checkout
  As a saucedemo shopper
  I want to buy the products in my cart
  So that I receive an order confirmation

  Background:
    Given I log in with my assigned user
    And my cart is empty

  @smoke @allure.label.severity:critical
  Scenario: Complete a purchase of a single product
    When I add "Sauce Labs Backpack" to the cart
    And I go to the cart
    And I start checkout
    And I fill in my checkout information
    Then the order total should be a positive dollar amount
    And I finish the order
    Then I should see the order confirmation "Thank you for your order!"

  Scenario Outline: Complete a purchase of multiple products
    When I add the following products to the cart:
      | ProductName  |
      | <productA>   |
      | <productB>   |
    And I go to the cart
    And I start checkout
    And I fill in my checkout information
    Then the overview should list <count> items
    When I finish the order
    Then I should see the order confirmation "Thank you for your order!"

    Examples:
      | productA            | productB                 | count |
      | Sauce Labs Backpack  | Sauce Labs Bike Light    | 2     |
      | Sauce Labs Onesie    | Sauce Labs Fleece Jacket | 2     |

  @negative
  Scenario: Checkout requires a postal code
    When I add "Sauce Labs Backpack" to the cart
    And I go to the cart
    And I start checkout
    And I fill in my checkout information without a postal code
    Then I should see the checkout error "Error: Postal Code is required"

  @negative @user:problem_user @known-issue
  Scenario: problem_user cannot complete checkout because the last name field is broken
    When I add "Sauce Labs Backpack" to the cart
    And I go to the cart
    And I start checkout
    And I fill in my checkout information
    Then I should see the checkout error "Error: Last Name is required"
