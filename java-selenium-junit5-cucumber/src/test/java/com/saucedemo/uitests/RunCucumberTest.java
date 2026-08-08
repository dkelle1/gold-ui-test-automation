package com.saucedemo.uitests;

import org.junit.platform.suite.api.IncludeEngines;
import org.junit.platform.suite.api.SelectPackages;
import org.junit.platform.suite.api.Suite;

/**
 * The single entry point Surefire runs; everything else about the run (glue package, parallelism,
 * plugins, tag filter) is configured in {@code src/test/resources/junit-platform.properties}.
 *
 * <p>The {@code features/} folder at the framework root is copied onto the test classpath by the
 * {@code <testResources>} block in pom.xml - see the comment there for why it lives at the root instead
 * of under {@code src/test/resources}. It is selected here with the package selector rather than
 * {@code @SelectClasspathResource}: the Cucumber engine emits a discovery warning for the latter,
 * recommending exactly this replacement.
 */
@Suite
@IncludeEngines("cucumber")
@SelectPackages("features")
public class RunCucumberTest {
}
