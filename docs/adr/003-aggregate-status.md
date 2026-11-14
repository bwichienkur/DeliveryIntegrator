# ADR 003: Aggregate delivery status and primary endpoint

## Status

Accepted

## Rules

1. **Failed** if any **Primary** endpoint fails (regardless of secondary outcomes).
2. **PartialSuccess** if Primary succeeds and at least one **Secondary** fails (and no Primary failure).
3. **Success** if Primary succeeds and all **Secondary** and **Optional** endpoints that ran also succeeded.
4. Config flag **`ContinueOnPrimaryFailure`**: when `false` (default), do not execute secondaries if primary fails.

## Optional endpoints

Failures on Optional endpoints alone do not change Success to Partial unless a Secondary also failed.