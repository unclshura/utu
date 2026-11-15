namespace Utu.Core;

/// <summary>
/// Receives the progress of an upload run. Every method may be called from
/// several threads at once.
/// </summary>
public interface IUploadObserver
{
    /// <summary>The plan is complete and work is about to start.</summary>
    void PlanReady(UploadPlan plan);

    /// <summary>An item starts.</summary>
    void ItemStarted(UploadPlanItem item);

    /// <summary>Bytes of an item that YouTube has received.</summary>
    void ItemProgress(UploadPlanItem item, long bytesSent);

    /// <summary>An item finished.</summary>
    void ItemFinished(UploadOutcome outcome);
}