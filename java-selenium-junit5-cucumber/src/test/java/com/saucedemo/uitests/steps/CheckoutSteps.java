package com.saucedemo.uitests.steps;

import static org.assertj.core.api.Assertions.assertThat;

import com.saucedemo.uitests.hooks.ScenarioContext;
import com.saucedemo.uitests.pages.CheckoutCompletePage;
import com.saucedemo.uitests.pages.CheckoutInformationPage;
import com.saucedemo.uitests.pages.CheckoutOverviewPage;
import com.saucedemo.uitests.support.Polling;
import com.saucedemo.uitests.testdata.CheckoutDataFactory;
import com.saucedemo.uitests.testdata.CheckoutInfo;
import io.cucumber.java.en.Then;
import io.cucumber.java.en.When;
import java.math.BigDecimal;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CheckoutSteps {

    private static final Pattern TOTAL_LABEL_PATTERN = Pattern.compile("^Total: \\$(\\d+\\.\\d{2})$");

    private final ScenarioContext context;

    public CheckoutSteps(ScenarioContext context) {
        this.context = context;
    }

    @When("I fill in my checkout information")
    public void iFillInMyCheckoutInformation() {
        new CheckoutInformationPage(context.driver()).fillAndContinue(CheckoutDataFactory.create());
    }

    @When("I fill in my checkout information without a postal code")
    public void iFillInMyCheckoutInformationWithoutAPostalCode() {
        CheckoutInfo info = CheckoutDataFactory.create().withoutPostalCode();
        new CheckoutInformationPage(context.driver()).submitWithoutPostalCode(info);
    }

    @Then("the overview should list {int} items")
    public void theOverviewShouldListItems(int expectedCount) {
        Polling.assertEventually(
                () -> new CheckoutOverviewPage(context.driver()).listItemNames().size(),
                expectedCount,
                "checkout overview item count");
    }

    @Then("the order total should be a positive dollar amount")
    public void theOrderTotalShouldBeAPositiveDollarAmount() {
        String totalLabel = new CheckoutOverviewPage(context.driver()).totalLabel();
        Matcher matcher = TOTAL_LABEL_PATTERN.matcher(totalLabel);

        assertThat(matcher.matches())
                .as("'%s' should match the expected 'Total: $<amount>' format", totalLabel)
                .isTrue();
        assertThat(new BigDecimal(matcher.group(1))).isGreaterThan(BigDecimal.ZERO);
    }

    @When("I finish the order")
    public void iFinishTheOrder() {
        new CheckoutOverviewPage(context.driver()).finish();
    }

    @Then("I should see the order confirmation {string}")
    public void iShouldSeeTheOrderConfirmation(String expectedMessage) {
        assertThat(new CheckoutCompletePage(context.driver()).confirmationMessage()).isEqualTo(expectedMessage);
    }

    @Then("I should see the checkout error {string}")
    public void iShouldSeeTheCheckoutError(String expectedMessage) {
        assertThat(new CheckoutInformationPage(context.driver()).errorMessage()).isEqualTo(expectedMessage);
    }
}
