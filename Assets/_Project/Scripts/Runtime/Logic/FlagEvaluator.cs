namespace AyeAye
{

    public static class FlagEvaluator
    {

        public static bool IsMet(FlagCondition condition, GameState state, ShipState ship = null, OrderState order = null)
        {
            switch (condition)
            {
                case FlagCondition.CargoShiftedRequired:
                    return order != null
                        ? state.CargoShiftedOrderIds.Contains(order.Id)
                        : state.CargoShiftedOrderIds.Count > 0;
                case FlagCondition.CargoShiftedForbidden:
                    return order != null
                        ? !state.CargoShiftedOrderIds.Contains(order.Id)
                        : state.CargoShiftedOrderIds.Count == 0;
                case FlagCondition.TemporaryRepairRequired:
                    return ship != null
                        ? state.TemporaryRepairShipIds.Contains(ship.Id)
                        : state.TemporaryRepairShipIds.Count > 0;
                case FlagCondition.TemporaryRepairForbidden:
                    return ship != null
                        ? !state.TemporaryRepairShipIds.Contains(ship.Id)
                        : state.TemporaryRepairShipIds.Count == 0;
                default:
                    return true; 
            }
        }
    }
}

