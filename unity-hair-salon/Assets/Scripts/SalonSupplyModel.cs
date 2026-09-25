using System;

namespace HairSalon
{
    /// <summary>
    /// One shared wash-supply channel for the first running-and-restocking slice.
    /// Transfers are deliberately one item at a time so the scene can animate each step.
    /// </summary>
    public sealed class SalonSupplyModel
    {
        public int SourceWashKits { get; private set; }
        public int CarriedWashKits { get; private set; }
        public int WashRackWashKits { get; private set; }
        public int CarryCapacity { get; }
        public int WashRackCapacity { get; }
        public bool CanStartWash => WashRackWashKits > 0;

        public event Action Changed;

        public SalonSupplyModel(int sourceStock, int carryCapacity, int washRackCapacity)
        {
            if (sourceStock < 0) throw new ArgumentOutOfRangeException(nameof(sourceStock));
            if (carryCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(carryCapacity));
            if (washRackCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(washRackCapacity));
            CarryCapacity = carryCapacity;
            WashRackCapacity = washRackCapacity;
            SourceWashKits = sourceStock;
        }

        public bool TryPickUpWashKit()
        {
            if (SourceWashKits <= 0 || CarriedWashKits >= CarryCapacity) return false;
            SourceWashKits--;
            CarriedWashKits++;
            Changed?.Invoke();
            return true;
        }

        public bool TryDeliverWashKit()
        {
            if (CarriedWashKits <= 0 || WashRackWashKits >= WashRackCapacity) return false;
            CarriedWashKits--;
            WashRackWashKits++;
            Changed?.Invoke();
            return true;
        }

        public bool TryConsumeWashKit()
        {
            if (WashRackWashKits <= 0) return false;
            WashRackWashKits--;
            Changed?.Invoke();
            return true;
        }

        public void ResetDay(int sourceStock)
        {
            if (sourceStock < 0) throw new ArgumentOutOfRangeException(nameof(sourceStock));
            SourceWashKits = sourceStock;
            CarriedWashKits = 0;
            WashRackWashKits = 0;
            Changed?.Invoke();
        }
    }
}
