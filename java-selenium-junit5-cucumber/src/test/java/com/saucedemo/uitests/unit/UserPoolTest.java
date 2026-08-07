package com.saucedemo.uitests.unit;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.saucedemo.uitests.config.ParallelSettings;
import com.saucedemo.uitests.users.UserAccount;
import com.saucedemo.uitests.users.UserCatalog;
import com.saucedemo.uitests.users.UserLease;
import com.saucedemo.uitests.users.UserPool;
import java.time.Duration;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

/**
 * Plain JUnit tests (no browser, no Gherkin) for the one genuinely novel piece of this framework: that
 * {@link UserPool} never hands the same account to two concurrent leases, and fails diagnosably rather
 * than deadlocking when it is misconfigured.
 *
 * <p>These run in the same {@code mvn test} invocation as the Cucumber suite - Surefire picks them up by
 * the {@code *Test.java} naming convention, and they execute on the Jupiter engine rather than the
 * Cucumber one.
 */
class UserPoolTest {

    private static List<UserAccount> threeTestUsers() {
        return List.of(
                new UserAccount("user-a", "pw", true, true),
                new UserAccount("user-b", "pw", true, true),
                new UserAccount("user-c", "pw", true, true));
    }

    @Test
    @DisplayName("concurrently held leases always hold different accounts")
    void acquireReturnsDistinctAccountsForConcurrentLeases() {
        UserPool pool = new UserPool(threeTestUsers());

        try (UserLease first = pool.acquire(Duration.ofSeconds(1));
                UserLease second = pool.acquire(Duration.ofSeconds(1));
                UserLease third = pool.acquire(Duration.ofSeconds(1))) {

            assertThat(List.of(first.account(), second.account(), third.account()))
                    .doesNotHaveDuplicates();
        }
    }

    @Test
    @DisplayName("an exhausted pool times out with a diagnosable message instead of hanging")
    void acquireTimesOutWhenPoolIsExhausted() {
        UserPool pool = new UserPool(threeTestUsers());

        try (UserLease first = pool.acquire(Duration.ofSeconds(1));
                UserLease second = pool.acquire(Duration.ofSeconds(1));
                UserLease third = pool.acquire(Duration.ofSeconds(1))) {

            assertThatThrownBy(() -> pool.acquire(Duration.ofMillis(200)))
                    .isInstanceOf(IllegalStateException.class)
                    .hasMessageContaining("Timed out")
                    .hasMessageContaining("currently available");
        }
    }

    @Test
    @DisplayName("closing a lease returns its account for reuse")
    void closingALeaseReturnsTheAccountToThePool() {
        UserPool pool = new UserPool(threeTestUsers());

        UserLease first = pool.acquire(Duration.ofSeconds(1));
        UserAccount released = first.account();
        first.close();

        assertThat(pool.availableCount()).isEqualTo(3);

        // Drain the pool; the released account must be among what comes back out.
        List<UserAccount> drained = new ArrayList<>();
        try (UserLease a = pool.acquire(Duration.ofSeconds(1));
                UserLease b = pool.acquire(Duration.ofSeconds(1));
                UserLease c = pool.acquire(Duration.ofSeconds(1))) {
            Collections.addAll(drained, a.account(), b.account(), c.account());
        }

        assertThat(drained).contains(released);
    }

    @Test
    @DisplayName("closing a lease twice does not duplicate its account in the pool")
    void closeIsIdempotent() {
        UserPool pool = new UserPool(threeTestUsers());
        UserLease lease = pool.acquire(Duration.ofSeconds(1));

        lease.close();
        lease.close();

        assertThat(pool.availableCount()).isEqualTo(3);
    }

    @Test
    @DisplayName("a tag-override lease has no pool to return to, so closing it is a no-op")
    void taggedUserLeaseCloseIsANoOp() {
        UserLease lease = new UserLease(UserCatalog.LOCKED_OUT_USER);

        assertThat(lease.account()).isEqualTo(UserCatalog.LOCKED_OUT_USER);
        lease.close();
        lease.close();
    }

    @Test
    @DisplayName("a pool smaller than the configured parallelism is rejected at construction")
    void constructorRejectsPoolSmallerThanParallelism() {
        List<UserAccount> tooFew = List.of(new UserAccount("solo", "pw", true, true));

        assertThatThrownBy(() -> new UserPool(tooFew))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("configured parallelism");
    }

    @Test
    @DisplayName("an empty pool is rejected at construction")
    void constructorRejectsEmptyPool() {
        assertThatThrownBy(() -> new UserPool(List.of()))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("at least one account");
    }

    @Test
    @DisplayName("the real catalog's pool is big enough for the configured parallelism")
    void realCatalogSatisfiesConfiguredParallelism() {
        assertThat(UserCatalog.POOL_USERS.size())
                .as("every concurrently running scenario needs its own checkout-capable account")
                .isGreaterThanOrEqualTo(ParallelSettings.MAX_PARALLEL_WORKERS);

        assertThat(UserCatalog.POOL_USERS).allMatch(UserAccount::canCompleteCheckout);
    }

    /**
     * The property that actually matters under parallel execution, exercised the way it is really used:
     * many threads leasing and releasing in a loop. Any window where two threads could hold the same
     * account at once trips the overlap detector below.
     */
    @Test
    @DisplayName("no two threads ever hold the same account at the same time")
    void concurrentLeasesNeverOverlap() throws InterruptedException {
        UserPool pool = new UserPool(threeTestUsers());
        int threads = 8;
        int iterationsPerThread = 50;

        Set<String> currentlyHeld = ConcurrentHashMap.newKeySet();
        AtomicBoolean sawOverlap = new AtomicBoolean(false);
        CountDownLatch startLine = new CountDownLatch(1);
        CountDownLatch finished = new CountDownLatch(threads);

        ExecutorService executor = Executors.newFixedThreadPool(threads);
        try {
            for (int i = 0; i < threads; i++) {
                executor.submit(() -> {
                    try {
                        startLine.await();
                        for (int n = 0; n < iterationsPerThread; n++) {
                            try (UserLease lease = pool.acquire(Duration.ofSeconds(10))) {
                                String username = lease.account().username();
                                if (!currentlyHeld.add(username)) {
                                    sawOverlap.set(true);
                                }
                                Thread.yield();
                                currentlyHeld.remove(username);
                            }
                        }
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                    } finally {
                        finished.countDown();
                    }
                });
            }

            startLine.countDown();
            assertThat(finished.await(60, TimeUnit.SECONDS))
                    .as("all lease/release threads should finish well within the timeout")
                    .isTrue();
        } finally {
            executor.shutdownNow();
        }

        assertThat(sawOverlap).isFalse();
        assertThat(pool.availableCount())
                .as("every lease was closed, so the pool should be back to full")
                .isEqualTo(3);
    }
}
