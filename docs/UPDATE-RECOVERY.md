# Update recovery — proposed 0.3.0

Keep the existing 0.2.1 public API compatible on .NET 8 and .NET 10.

## R1: journal
One journal per product and user, under the application's private update directory. Persist transaction ID, previous version, target version and phase. Use atomic replacement and bounded reads. The application owns the single writer lock. Missing, invalid or corrupt journals are distinct outcomes. Journal contents are advisory, never authority to execute a package.

Phases: Prepared, HandoffStarted, AwaitingHealthCheck, Completed, RecoveryRequired. A process launch is not evidence of installation success. Only the new application can confirm its version and health. Interrupted transactions require explicit recovery assessment.

## R2: verified package retention
Retain the exact installer and original signed manifest bytes and signature. Revalidate with the pinned public key and product/channel/version policy before any recovery. Never trust a hash stored solely in the journal. No unsigned fallback. Keep previous known-good package until new version is confirmed healthy.

## R3: recovery
Offer explicit recovery choices; no automatic downgrade or execution on startup. Recheck package immediately before launch. Preserve user documents, settings and associations. Applications own data compatibility and backups; refuse rollback when data migration cannot safely be reversed. Missing verified previous package requires manual recovery.

## Acceptance
Test invalid and truncated journals, interruption at every phase, wrong product/key, altered package, missing package, concurrent writers, failed health checks and successful confirmation. Real Windows installer acceptance is required separately. No package publication until reviewed.
