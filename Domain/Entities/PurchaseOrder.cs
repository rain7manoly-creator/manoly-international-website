using ManolyWarehouse.Domain.Common;
using ManolyWarehouse.Domain.Exceptions;

namespace ManolyWarehouse.Domain.Entities;

public enum OrderStatus
{
    Ordered,
    Shipped,
    ArrivingSoon,
    Received,
    Cancelled
}

public class PurchaseOrder : AuditableEntity
{
    public int Id { get; private set; }
    public string Supplier { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public DateTime OrderedAt { get; private set; }
    public DateOnly? ExpectedArrivalDate { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? LastUpdatedAt { get; private set; }
    public string? LastUpdatedBy { get; private set; }

    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        string supplier, DateOnly? expectedArrivalDate,
        string? notes, string createdBy)
    {
        var order = new PurchaseOrder
        {
            Supplier = supplier,
            Status = OrderStatus.Ordered,
            OrderedAt = DateTime.UtcNow,
            ExpectedArrivalDate = expectedArrivalDate,
            Notes = notes
        };
        order.SetCreated(createdBy);
        return order;
    }

    public void Update(string supplier, DateOnly? expectedArrivalDate, string? notes, string updatedBy)
    {
        if (IsTerminal) throw new TerminalStateException();
        Supplier = supplier;
        ExpectedArrivalDate = expectedArrivalDate;
        Notes = notes;
        SetUpdated(updatedBy);
    }

    public void AddItem(PurchaseOrderItem item)
    {
        if (IsTerminal) throw new TerminalStateException();
        _items.Add(item);
    }

    public void AdvanceStatus(string updatedBy)
    {
        if (Status == OrderStatus.Ordered && !HasItems)
            throw new DomainException("لا يمكن تقديم طلبية فارغة. أضف عناصر أولاً.");

        var next = Status switch
        {
            OrderStatus.Ordered      => OrderStatus.Shipped,
            OrderStatus.Shipped      => OrderStatus.ArrivingSoon,
            OrderStatus.ArrivingSoon => OrderStatus.Received,
            _ => throw new InvalidStatusTransitionException(Status.ToString(), "next")
        };

        Status = next;
        SetStatusChanged(updatedBy);
    }

    public void Cancel(string updatedBy)
    {
        if (IsTerminal) throw new TerminalStateException();
        Status = OrderStatus.Cancelled;
        SetStatusChanged(updatedBy);
    }

    public bool IsTerminal => Status is OrderStatus.Received or OrderStatus.Cancelled;
    public bool HasItems => _items.Count > 0;
    public bool CanAdvance => !IsTerminal && (Status != OrderStatus.Ordered || HasItems);

    public OrderStatus? NextStatus => Status switch
    {
        OrderStatus.Ordered      => OrderStatus.Shipped,
        OrderStatus.Shipped      => OrderStatus.ArrivingSoon,
        OrderStatus.ArrivingSoon => OrderStatus.Received,
        _ => null
    };

    private void SetStatusChanged(string updatedBy)
    {
        LastUpdatedAt = DateTime.UtcNow;
        LastUpdatedBy = updatedBy;
        SetUpdated(updatedBy);
    }
}
