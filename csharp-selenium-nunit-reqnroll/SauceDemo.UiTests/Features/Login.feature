@login
Feature: Login
  As a saucedemo shopper
  I want to log in with my account
  So that I can browse and purchase products

  @smoke
  Scenario: Successful login with a pooled user
    When I log in with my assigned user
    Then I should land on the inventory page
    And 6 products should be listed

  @negative @user:locked_out_user
  Scenario: A locked-out user is rejected at login
    When I log in with my assigned user
    Then I should see the login error "Epic sadface: Sorry, this user has been locked out."

  @negative
  Scenario Outline: Login is rejected for invalid credentials
    When I log in with username "<username>" and password "<password>"
    Then I should see a login error

    Examples:
      | username      | password     |
      |               | secret_sauce |
      | standard_user |              |
      | <generated>   | <generated>  |
