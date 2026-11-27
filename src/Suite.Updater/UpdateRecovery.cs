namespace Suite.Updater;

public enum RecoveryStatus
{
    NoTransaction, InvalidJournal, Healthy, Interrupted,
    HealthCheckRequired, RecoveryRequired, UnexpectedVersion
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

    public UpdateTransaction RequireRecovery(Guid id)
    {
        var t = journal.Read().Transaction;
        if (t is null || t.Id != id) throw new InvalidOperationException("Unknown update transaction.");
        return t.Phase == UpdatePhase.RecoveryRequired ? t : journal.Advance(id, UpdatePhase.RecoveryRequired);
    }
}
