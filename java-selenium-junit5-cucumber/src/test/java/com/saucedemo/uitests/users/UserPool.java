package com.saucedemo.uitests.users;

import com.saucedemo.uitests.config.ParallelSettings;
import java.time.Duration;
import java.util.LinkedList;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

/**
 * A thread-safe lease/release pool of login-capable accounts, one leased per running scenario, so
 * concurrently executing scenarios never race to use the same saucedemo account.
 *
 * <p>Why a pool here and not the fixed per-worker mapping the TypeScript and Python siblings use: those
 * two runners give each worker a stable identity for its whole lifetime (Cucumber.js sets
 * {@code CUCUMBER_WORKER_ID}; pytest-xdist sets {@code PYTEST_XDIST_WORKER}), so "worker N always gets
 * account N" is both possible and simpler there. Cucumber on the JUnit Platform instead dispatches
 * scenarios onto a shared thread pool with no stable per-worker index to key off - a thread is just
 * whichever pool thread happened to pick the scenario up. Leasing an account for the duration of a
 * scenario and returning it afterwards is therefore the model that actually fits, the same conclusion
 * the two C# NUnit siblings reach for the same reason.
 */
public class UserPool {

    /**
     * How long a scenario waits for a free account before failing. Generous, because it only ever
     * matters when more scenarios run concurrently than the pool has accounts - which the constructor
     * guard below is designed to prevent in the first place.
     */
    public static final Duration DEFAULT_ACQUIRE_TIMEOUT = Duration.ofMinutes(2);

    private final BlockingQueue<UserAccount> available;
    private final int size;

    public UserPool(List<UserAccount> users) {
        if (users.isEmpty()) {
            throw new IllegalArgumentException("User pool must contain at least one account.");
        }

        if (users.size() < ParallelSettings.MAX_PARALLEL_WORKERS) {
            throw new IllegalArgumentException("User pool has " + users.size()
                    + " account(s) but the configured parallelism is " + ParallelSettings.MAX_PARALLEL_WORKERS
                    + ". Every concurrently running scenario needs its own account, or scenarios would "
                    + "block waiting for one to free up.");
        }

        this.available = new LinkedBlockingQueue<>(new LinkedList<>(users));
        this.size = users.size();
    }

    /** Snapshot of how many accounts are currently free. For diagnostics and tests only. */
    public int availableCount() {
        return available.size();
    }

    public UserLease acquire() {
        return acquire(DEFAULT_ACQUIRE_TIMEOUT);
    }

    public UserLease acquire(Duration timeout) {
        UserAccount account;
        try {
            account = available.poll(timeout.toMillis(), TimeUnit.MILLISECONDS);
        } catch (InterruptedException e) {
            // Restore the flag so an interrupt initiated elsewhere (a cancelled run) is not swallowed.
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while waiting for a free saucedemo user.", e);
        }

        if (account == null) {
            throw new IllegalStateException("Timed out after " + timeout
                    + " waiting for a free saucedemo user (" + available.size() + " of " + size
                    + " currently available). This usually means the configured parallelism ("
                    + ParallelSettings.MAX_PARALLEL_WORKERS + ") exceeds the pool size, so more scenarios "
                    + "are running concurrently than there are distinct users to log in with.");
        }

        return new UserLease(account, this::release);
    }

    private void release(UserAccount account) {
        available.add(account);
    }
}
