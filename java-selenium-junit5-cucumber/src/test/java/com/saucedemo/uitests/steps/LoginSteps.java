package com.saucedemo.uitests.steps;

import static org.assertj.core.api.Assertions.assertThat;

import com.saucedemo.uitests.hooks.ScenarioContext;
import com.saucedemo.uitests.pages.InventoryPage;
import com.saucedemo.uitests.pages.LoginPage;
import com.saucedemo.uitests.testdata.InvalidCredentialsFactory;
import com.saucedemo.uitests.testdata.ProductCatalog;
import io.cucumber.java.en.Then;
import io.cucumber.java.en.When;
import java.util.List;

public class LoginSteps {

    /**
     * Lets an Examples row ask for fresh, guaranteed-not-real credentials instead of a fixed literal.
     * Gherkin only substitutes {@code <...>} placeholders into the step text, so an Examples *value* of
     * "&lt;generated&gt;" reaches the step definition verbatim - which is exactly what makes it usable as
     * a marker.
     */
    private static final String GENERATED_MARKER = "<generated>";

    private final ScenarioContext context;

    public LoginSteps(ScenarioContext context) {
        this.context = context;
    }

    // Cucumber-JVM does not bind step definitions to a keyword - a @When definition matches the same text
    // written as Given/When/And - so this one definition serves both the "When" usage in login.feature and
    // the "Given" usage in the other two features' Background.
    @When("I log in with my assigned user")
    public void iLogInWithMyAssignedUser() {
        new LoginPage(context.driver())
                .submitLogin(context.user().username(), context.user().password());
    }

    @When("I log in with username {string} and password {string}")
    public void iLogInWithUsernameAndPassword(String username, String password) {
        String effectiveUsername = username;
        String effectivePassword = password;

        if (GENERATED_MARKER.equals(username) || GENERATED_MARKER.equals(password)) {
            InvalidCredentialsFactory.Credentials generated = InvalidCredentialsFactory.create();
            effectiveUsername = GENERATED_MARKER.equals(username) ? generated.username() : username;
            effectivePassword = GENERATED_MARKER.equals(password) ? generated.password() : password;
        }

        new LoginPage(context.driver()).submitLogin(effectiveUsername, effectivePassword);
    }

    @Then("I should land on the inventory page")
    public void iShouldLandOnTheInventoryPage() {
        assertThat(new InventoryPage(context.driver()).isDisplayed())
                .as("the inventory page should be displayed after login")
                .isTrue();
    }

    @Then("{int} products should be listed")
    public void productsShouldBeListed(int expectedCount) {
        List<String> actual = new InventoryPage(context.driver()).listProductNames();

        assertThat(actual).hasSize(expectedCount);
        assertThat(ProductCatalog.ALL)
                .as("every listed product name should be a known saucedemo product")
                .containsAll(actual);
    }

    @Then("I should see the login error {string}")
    public void iShouldSeeTheLoginError(String expectedMessage) {
        assertThat(new LoginPage(context.driver()).errorMessage()).isEqualTo(expectedMessage);
    }

    @Then("I should see a login error")
    public void iShouldSeeALoginError() {
        assertThat(new LoginPage(context.driver()).hasError()).isTrue();
    }
}
