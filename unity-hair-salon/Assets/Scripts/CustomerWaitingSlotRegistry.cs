using System.Collections.Generic;
using HairSalon;

/// <summary>Legacy view helper that mirrors FIFO seat compaction.</summary>
public sealed class CustomerWaitingSlotRegistry
{
    private readonly List<CustomerModel> _queue = new List<CustomerModel>();

    public int Reserve(CustomerModel customer, int capacity)
    {
        if (customer == null || capacity <= 0) return -1;
        int existing = _queue.IndexOf(customer);
        if (existing >= 0) return existing < capacity ? existing : -1;
        if (_queue.Count >= capacity) return -1;
        _queue.Add(customer);
        return _queue.Count - 1;
    }

    public void Release(CustomerModel customer)
    {
        if (customer != null) _queue.Remove(customer);
    }
}
