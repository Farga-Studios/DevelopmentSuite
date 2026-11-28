namespace Suite.Updater;

public enum RecoveryStatus
{
    NoTransaction, InvalidJournal, Healthy, Interrupted,
    HealthCheckRequired, RecoveryRequired, UnexpectedVersion, RollbackHealthCheckRequired, RollbackInterrupted, RolledBack, Aborted
}

public sealed record RecoveryAssessment(RecoveryStatus Status, UpdateTransaction? Transaction);

/// <summary>Assess startup state after installer exit, under the host's single-writer lock.
/// Never executes installers or treats journal contents as package authorization.</summary>
public sealed class UpdateRecovery(UpdateJournal journal)
{
    public RecoveryAssessment Assess(Version runningVersion)
    {
        ArgumentNullException.ThrowIfNull(runningVersion);
        var read = journal.Read();
        if (read.Status == JournalStatus.Missing) return new(RecoveryStatus.NoTransaction, null);
        if (read.Status != JournalStatus.Valid || read.Transaction is not { } t)
            return new(RecoveryStatus.InvalidJournal, null);
        if (runningVersion.Revision > 0) return new(RecoveryStatus.UnexpectedVersion, t);
        var running = new Version(runningVersion.Major, runningVersion.Minor, Math.Max(0,runningVersion.Build));
        var previous = Version.Parse(t.PreviousVersion);
        var target = Version.Parse(t.TargetVersion);
        if (running != previous && running != target)
            return new(RecoveryStatus.UnexpectedVersion, t);
        var status = t.Phase switch
        {
            UpdatePhase.RollbackStarted when running == previous => RecoveryStatus.RollbackHealthCheckRequired,
            UpdatePhase.RollbackStarted => RecoveryStatus.RollbackInterrupted,
            UpdatePhase.RolledBack when running == previous => RecoveryStatus.RolledBack,
            UpdatePhase.RolledBack => RecoveryStatus.UnexpectedVersion,
            UpdatePhase.Aborted when running == previous => RecoveryStatus.Aborted,
            UpdatePhase.Aborted => RecoveryStatus.UnexpectedVersion,
            UpdatePhase.Completed when running == target => RecoveryStatus.Healthy,
            UpdatePhase.Completed => RecoveryStatus.UnexpectedVersion,
            UpdatePhase.RecoveryRequired => RecoveryStatus.RecoveryRequired,
            UpdatePhase.Prepared when running == previous => RecoveryStatus.Interrupted,
            UpdatePhase.Prepared => RecoveryStatus.UnexpectedVersion,
            UpdatePhase.HandoffStarted or UpdatePhase.AwaitingHealthCheck when running == target => RecoveryStatus.HealthCheckRequired,
            UpdatePhase.HandoffStarted or UpdatePhase.AwaitingHealthCheck => RecoveryStatus.Interrupted,
            _ => RecoveryStatus.UnexpectedVersion
        };
        return new(status,t);
    }

    /// <summary>Call only after application-specific health checks succeed.</summary>
    public UpdateTransaction ConfirmHealthy(Guid id, Version runningVersion)
    {
        var assessment = Assess(runningVersion);
        if (assessment.Status != RecoveryStatus.HealthCheckRequired || assessment.Transaction is not { } t || t.Id != id)
            throw new InvalidOperationException("Cannot confirm this update.");
        if (t.Phase == UpdatePhase.HandoffStarted)
            journal.Advance(id, UpdatePhase.AwaitingHealthCheck);
        return journal.Advance(id, UpdatePhase.Completed);
    }

    public UpdateTransaction CancelPrepared(Guid id, Version runningVersion)
    {
        var assessment = Assess(runningVersion);
        if (assessment.Status != RecoveryStatus.Interrupted || assessment.Transaction is not { } t ||
            t.Id != id || t.Phase != UpdatePhase.Prepared)
            throw new InvalidOperationException("Only a prepared update can be cancelled before handoff.");
        return journal.Advance(id, UpdatePhase.Aborted);
    }

    /// <summary>Host must verify previous installation health, installer exit and user consent under the exclusive lock.</summary>
    public UpdateTransaction ResolveInterrupted(Guid id, Version runningVersion, bool installerExited, bool previousHealthy, bool userApproved)
    {
        var assessment = Assess(runningVersion);
        if (!installerExited || !previousHealthy || !userApproved ||
            assessment.Status != RecoveryStatus.Interrupted || assessment.Transaction is not { } t ||
            t.Id != id || t.Phase is not (UpdatePhase.HandoffStarted or UpdatePhase.AwaitingHealthCheck))
            throw new InvalidOperationException("Interrupted update cannot be resolved.");
        return journal.Advance(id, UpdatePhase.Aborted);
    }

    public UpdateTransaction MarkRollbackStarted(Guid id, Version runningVersion)
    {
        var assessment = Assess(runningVersion);
        if (assessment.Status != RecoveryStatus.RecoveryRequired || assessment.Transaction is not { } t ||
            t.Id != id || runningVersion.Revision > 0 ||
            new Version(runningVersion.Major, runningVersion.Minor, Math.Max(0,runningVersion.Build)) != Version.Parse(t.TargetVersion))
            throw new InvalidOperationException("Cannot start rollback.");
        return journal.Advance(id, UpdatePhase.RollbackStarted);
    }

    public UpdateTransaction ConfirmRollbackHealthy(Guid id, Version runningVersion)
    {
        var assessment = Assess(runningVersion);
        if (assessment.Status != RecoveryStatus.RollbackHealthCheckRequired || assessment.Transaction?.Id != id)
            throw new InvalidOperationException("Cannot confirm rollback.");
        return journal.Advance(id, UpdatePhase.RolledBack);
    }

    public UpdateTransaction RequireRecovery(Guid id)
    {
        var t = journal.Read().Transaction;
        if (t is null || t.Id != id) throw new InvalidOperationException("Unknown update transaction.");
        return t.Phase == UpdatePhase.RecoveryRequired ? t : journal.Advance(id, UpdatePhase.RecoveryRequired);
    }
}
