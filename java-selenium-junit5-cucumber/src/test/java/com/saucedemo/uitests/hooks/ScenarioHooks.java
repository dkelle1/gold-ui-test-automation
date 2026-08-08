package com.saucedemo.uitests.hooks;

import com.saucedemo.uitests.config.ConfigurationLoader;
import com.saucedemo.uitests.config.TestSettings;
import com.saucedemo.uitests.drivers.BrowserSession;
import com.saucedemo.uitests.drivers.WebDriverFactory;
import com.saucedemo.uitests.support.AllureAttachments;
import com.saucedemo.uitests.support.AllureParameters;
import com.saucedemo.uitests.support.EnvironmentWriter;
import com.saucedemo.uitests.users.SharedUserPool;
import com.saucedemo.uitests.users.TagHelpers;
import com.saucedemo.uitests.users.UserLease;
import io.cucumber.java.After;
import io.cucumber.java.Before;
import io.cucumber.java.BeforeAll;
import io.cucumber.java.Scenario;

/**
 * Per-scenario lifecycle: lease a distinct login user, open a browser for it, then tear both down in the
 * opposite order.
 *
 * <p><strong>Hook ordering runs opposite ways for the two annotations</strong>, per Cucumber's own
 * javadoc: {@code @Before} runs <em>lower</em> order values first, while {@code @After} runs
 * <em>higher</em> values first, so teardown naturally mirrors setup. That is the reverse of Reqnroll in
 * the C# siblings (where both run ascending), and getting it backwards here would quit the browser
 * before the failure screenshot was ever taken. The numbers below are chosen so the effective order is:
 *
 * <pre>
 *   setup:    lease user (10)  ->  open browser (20)
 *   teardown: capture evidence (30)  ->  quit browser (20)  ->  release user (10)
 * </pre>
 *
 * The user is released only after its browser is fully closed, so an account is never handed to another
 * scenario while a session for it is still alive.
 */
public class ScenarioHooks {

    private final ScenarioContext context;

    public ScenarioHooks(ScenarioContext context) {
        this.context = context;
    }

    /**
     * Runs once per JVM before any scenario. Touching the settings here means a misconfigured BaseUrl
     * fails the whole run up front instead of surfacing as a confusing per-scenario failure later, and
     * writing environment.properties now means it survives a crashed or cancelled run.
     */
    @BeforeAll
    public static void beforeAll() {
        ConfigurationLoader.settings();
        EnvironmentWriter.write();
    }

    @Before(order = 10)
    public void leaseUser(Scenario scenario) {
        UserLease lease = TagHelpers.taggedUser(scenario)
                .map(UserLease::new)
                .orElseGet(() -> SharedUserPool.get().acquire());
        context.setUserLease(lease);

        // Surfacing both in the report is what makes the parallel behaviour visible: the Allure result
        // for every scenario names the account and the thread that actually ran it. Note this goes
        // through AllureParameters rather than Allure.parameter(...) - see that class for why the plain
        // shortcut silently does nothing when called from a hook.
        AllureParameters.addScenarioParameter(scenario, "user", lease.account().username());
        AllureParameters.addScenarioParameter(scenario, "worker", Thread.currentThread().getName());
    }

    @Before(order = 20)
    public void openBrowser() {
        TestSettings settings = ConfigurationLoader.settings();
        BrowserSession session = WebDriverFactory.create(settings);
        context.setBrowserSession(session);

        try {
            session.driver().get(settings.baseUrl());
        } catch (RuntimeException e) {
            // Navigation failed after the browser was already launched. The @After hooks do run for a
            // scenario whose @Before threw, so the session would still be closed - but closing it here
            // keeps the failure self-contained rather than depending on that.
            session.close();
            context.setBrowserSession(null);
            throw e;
        }
    }

    @After(order = 30)
    public void captureFailureEvidence(Scenario scenario) {
        if (scenario.isFailed() && context.hasBrowserSession()) {
            AllureAttachments.attachFailureEvidence(context.driver());
        }
    }

    @After(order = 20)
    public void quitBrowser() {
        if (context.hasBrowserSession()) {
            context.browserSession().close();
        }
    }

    @After(order = 10)
    public void releaseUser() {
        UserLease lease = context.userLease();
        if (lease != null) {
            lease.close();
        }
    }
}
