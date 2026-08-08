package com.saucedemo.uitests.support;

import io.cucumber.java.Scenario;
import io.qameta.allure.Allure;
import io.qameta.allure.model.Parameter;
import java.util.ArrayList;
import java.util.List;

/**
 * Adds parameters to the Allure result for the current <em>scenario</em> from inside a Cucumber hook.
 *
 * <p>Two things make this less obvious than it looks, both found by running it rather than by reading:
 *
 * <ol>
 *   <li><strong>{@link Allure#parameter(String, Object)} does not work from a hook.</strong> It fails
 *       silently apart from a "{@code Could not update test case: test case with uuid ... not found}"
 *       line on stderr. {@code allure-cucumber7-jvm} classifies every {@code BEFORE}/{@code AFTER} hook
 *       as an Allure <em>fixture</em>, and {@code AllureLifecycle.startFixture} clears the thread context
 *       and makes the fixture's uuid its root - so the shortcut resolves the fixture and tries to update
 *       it as if it were a test case. Naming the test case explicitly avoids the thread context
 *       entirely: {@code Scenario.getId()} returns {@code testCase.getId().toString()}, exactly the uuid
 *       the adapter registered the result under.</li>
 *   <li><strong>The list returned by {@code getParameters()} may be immutable.</strong> The adapter only
 *       calls {@code setParameters(...)} for Scenario Outlines (to record the Examples row); a plain
 *       scenario keeps the model's default, and calling {@code add} on that throws
 *       {@link UnsupportedOperationException} - which, thrown from a {@code @Before} hook, fails the
 *       scenario outright. Copying into a new list and setting it back is safe either way.</li>
 * </ol>
 */
public final class AllureParameters {

    private AllureParameters() {
    }

    public static void addScenarioParameter(Scenario scenario, String name, String value) {
        Parameter parameter = new Parameter().setName(name).setValue(value);

        Allure.getLifecycle().updateTestCase(scenario.getId(), testResult -> {
            List<Parameter> merged = new ArrayList<>(testResult.getParameters());
            merged.add(parameter);
            testResult.setParameters(merged);
        });
    }
}
