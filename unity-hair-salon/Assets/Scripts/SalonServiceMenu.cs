using System;
using System.Collections.Generic;

namespace HairSalon
{
    /// <summary>Services currently exposed by the built equipment.</summary>
    public sealed class SalonServiceMenu
    {
        public bool HasBlowDryer { get; }
        public bool HasWashStation { get; }

        public SalonServiceMenu(bool hasBlowDryer, bool hasWashStation)
        {
            HasBlowDryer = hasBlowDryer;
            HasWashStation = hasWashStation;
        }

        public bool IsServiceAvailable(ServiceType service)
            => service == ServiceType.Cut ||
               (service == ServiceType.Dry && HasBlowDryer) ||
               (service == ServiceType.Wash && HasWashStation);

        public bool IsOrderAvailable(string orderId)
        {
            IReadOnlyList<ServiceType> order = SalonOrderCatalog.Get(orderId);
            for (int i = 0; i < order.Count; i++)
                if (!IsServiceAvailable(order[i])) return false;
            return true;
        }

        public static bool IsOrderAvailable(string orderId, SalonServiceMenu menu)
            => menu != null && menu.IsOrderAvailable(orderId);

        public static string FilterOrder(string pickedOrderId, SalonServiceMenu menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (menu.IsOrderAvailable(pickedOrderId)) return pickedOrderId;

            switch (pickedOrderId)
            {
                case "O005": return menu.HasWashStation
                    ? (menu.HasBlowDryer ? "O005" : "O003")
                    : (menu.HasBlowDryer ? "O004" : "O001");
                case "O003": return menu.HasWashStation ? "O003" : "O001";
                case "O002": return menu.HasWashStation
                    ? (menu.HasBlowDryer ? "O002" : "O003")
                    : (menu.HasBlowDryer ? "O004" : "O001");
                case "O004": return menu.HasBlowDryer ? "O004" : "O001";
                case "O001": return "O001";
                default: throw new ArgumentException("Unknown order.", nameof(pickedOrderId));
            }
        }

        public static string TeachingOrderAfter(SalonUnlockId unlockId)
            => SalonUnlockRoute.TeachingOrderAfter(unlockId);
    }
}
