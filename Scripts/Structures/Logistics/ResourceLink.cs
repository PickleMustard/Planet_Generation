using System;
using System.Collections.Generic;
using Constructables.Tick;
using Godot;
using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// Connects a source <see cref="ResourceNode"/> to a target <see cref="ResourceNode"/> and
/// manages the flow of <see cref="ResourcePackage"/> instances between them.
/// </summary>
public partial class ResourceLink : Resource, IManufactureTickable
{
    /// <summary>
    /// The node where packages originate.
    /// </summary>
    public ResourceNode? Source { get; set; }

    /// <summary>
    /// The node where packages are delivered.
    /// </summary>
    public ResourceNode? Target { get; set; }

    /// <summary>
    /// The profile governing speed, capacity, and bundling behaviour of this link.
    /// </summary>
    public LinkProfile? Profile { get; set; }

    /// <summary>
    /// ResourceLinks tick after Buildings (priority 1).
    /// </summary>
    public int TickPriority => 1;

    /// <summary>
    /// Packages currently in transit between source and target.
    /// </summary>
    public List<ResourcePackage> InFlight { get; } = new();

    /// <summary>
    /// Ticks remaining until the next dispatch/retry cycle.
    /// Decremented each <see cref="OnManufactureTick"/>; when zero (or below) the
    /// <see cref="ArrivalBuffer"/> is retried and the timer is reset to
    /// <see cref="LinkProfile.BundleTime"/>.
    /// </summary>
    public int BundleTimer { get; set; }

    /// <summary>
    /// Packages that arrived at the target but could not be deposited,
    /// awaiting retry on subsequent ticks when the bundle timer fires.
    /// </summary>
    public List<ResourcePackage> ArrivalBuffer { get; } = new();

    /// <summary>
    /// Cached great-circle hop estimate between the source and target buildings' primary cells.
    /// Computed on <see cref="ConnectNodes"/>. Defaults to 1 when either endpoint has no
    /// placement, so unplaced/test links behave like single-hop links.
    /// </summary>
    public float CellDistance { get; private set; } = 1f;

    /// <summary>
    /// Checks whether two nodes are allowed to be connected.
    /// Import↔Export is valid, Flex↔anything is valid, and same-type↔same-type is invalid.
    /// </summary>
    public static bool CanConnect(ResourceNode a, ResourceNode b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (a.Kind == ResourceNodeKind.Flex || b.Kind == ResourceNodeKind.Flex)
        {
            return true;
        }

        return a.Kind != b.Kind;
    }

    /// <summary>
    /// Connects the given source and target nodes through this link.
    /// Wires up the nodes' link references and auto-registers this link with
    /// the <see cref="ManufactureTickEngine"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the nodes cannot be connected per <see cref="CanConnect"/>.
    /// </exception>
    public void ConnectNodes(ResourceNode source, ResourceNode target)
    {
        if (!CanConnect(source, target))
        {
            throw new InvalidOperationException("Source and target nodes cannot be connected.");
        }

        Source = source;
        Target = target;

        if (source != null)
        {
            source.Link = this;
        }

        if (target != null)
        {
            target.Link = this;
        }

        RecomputeCellDistance();

        ManufactureTickEngine.Instance?.Register(this);
    }

    private void RecomputeCellDistance()
    {
        var sourceCell = Source?.Owner?.PrimaryCell;
        var targetCell = Target?.Owner?.PrimaryCell;

        if (sourceCell == null || targetCell == null)
        {
            CellDistance = 1f;
            return;
        }

        // Great-circle hop estimate: angular separation between cell centers (radians).
        // Cell centers live on a unit sphere; clamp the dot product to handle numeric drift.
        Vector3 a = sourceCell.Center.Normalized();
        Vector3 b = targetCell.Center.Normalized();
        float dot = Mathf.Clamp(a.Dot(b), -1f, 1f);
        float angle = Mathf.Acos(dot);

        // Convert radians to a hop count proxy. Tunable: 1 radian ~ 1 hop. The actual cell
        // adjacency BFS is more accurate but expensive; this great-circle approximation is
        // cheap and good enough for transport speed scaling.
        CellDistance = Mathf.Max(1f, angle);
    }

    /// <summary>
    /// Attempts to create one or more <see cref="ResourcePackage"/> instances and add them to
    /// <see cref="InFlight"/>.
    /// Fails if the link has no profile, the input is invalid, or in-flight capacity is reached.
    /// Amounts larger than <see cref="LinkProfile.PackageSize"/> are split across multiple
    /// packages up to the remaining slot capacity.
    /// </summary>
    public bool TryEnqueue(string resourceId, float amount)
    {
        return TryEnqueueAmount(resourceId, amount) > 0f;
    }

    /// <summary>
    /// Attempts to enqueue resources and returns the amount actually added to <see cref="InFlight"/>.
    /// Returns 0 if the link has no profile, the input is invalid, or in-flight capacity is reached.
    /// Amounts larger than <see cref="LinkProfile.PackageSize"/> are split across multiple
    /// packages up to the remaining slot capacity.
    /// </summary>
    public float TryEnqueueAmount(string resourceId, float amount)
    {
        if (Profile == null)
        {
            return 0f;
        }

        if (amount <= 0f || string.IsNullOrEmpty(resourceId))
        {
            return 0f;
        }

        float packageSize = Profile.PackageSize > 0 ? Profile.PackageSize : int.MaxValue;
        float remaining = amount;
        float enqueued = 0f;

        while (remaining > 0f && InFlight.Count < Profile.SlotCapacity)
        {
            float pkgAmount = Mathf.Min(remaining, packageSize);

            var package = new ResourcePackage
            {
                ResourceId = resourceId,
                Quantity = pkgAmount,
                Link = this,
                Progress = 0f
            };

            InFlight.Add(package);
            remaining -= pkgAmount;
            enqueued += pkgAmount;
        }

        return enqueued;
    }

    /// <summary>
    /// Advances in-flight packages, handles arrivals and deposit attempts,
    /// decrements the bundle timer, and retries any buffered packages when the timer fires.
    /// </summary>
    public void OnManufactureTick(float delta)
    {
        if (Profile == null)
        {
            return;
        }

        if (BundleTimer > 0)
        {
            BundleTimer--;
        }

        // Transport speed scales inversely with cell distance — far buildings ship slower.
        float speed = Profile.TransportSpeed / Mathf.Max(1f, CellDistance);

        // Advance in-flight packages
        foreach (var package in InFlight)
        {
            if (package.Stuck)
            {
                continue;
            }

            package.AdvanceProgress(speed * delta);
        }

        // Handle completed packages
        for (int i = InFlight.Count - 1; i >= 0; i--)
        {
            var package = InFlight[i];
            if (!package.IsComplete)
            {
                continue;
            }

            InFlight.RemoveAt(i);

            if (!package.TryDeposit())
            {
                ArrivalBuffer.Add(package);
            }
        }

        // Retry buffered packages when the bundle timer fires
        if (BundleTimer <= 0)
        {
            for (int i = ArrivalBuffer.Count - 1; i >= 0; i--)
            {
                if (ArrivalBuffer[i].TryDeposit())
                {
                    ArrivalBuffer.RemoveAt(i);
                }
            }

            BundleTimer = Profile.BundleTime;
        }
    }

    /// <summary>
    /// Severes Source and Target references and unregisters this link from
    /// the <see cref="ManufactureTickEngine"/>.
    /// </summary>
    public void Disconnect()
    {
        if (ManufactureTickEngine.Instance is { } engine)
        {
            engine.Unregister(this);
        }

        if (Source != null)
        {
            if (Source.Link == this)
            {
                Source.Link = null;
            }

            Source = null;
        }

        if (Target != null)
        {
            if (Target.Link == this)
            {
                Target.Link = null;
            }

            Target = null;
        }
    }
}
