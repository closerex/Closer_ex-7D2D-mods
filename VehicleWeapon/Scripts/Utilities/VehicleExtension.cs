public static class VehicleExtension
{
    public static int GetTotalSeats(this Vehicle self)
    {
        int seats = 0;
        while(seats < 99)
        {
            if (!self.Properties.Classes.TryGetValue("seat" + seats, out _))
                break;
            seats++;
        }
        return seats;
    }

    public static void TrySwitchSeatServer(this GameManager self, World _world, int entityId, int vehicleId, int seat)
    {
        if (!ConnectionManager.Instance.IsServer)
            return;
        Entity entity = _world.GetEntity(entityId);
        EntityVehicle vehicle = _world.GetEntity(vehicleId) as EntityVehicle;
        if (entity != null && vehicle != null && vehicle.GetAttached(seat) == null)
        {
            entity.SendDetach();
            entity.StartAttachToEntity(vehicle, seat);
        }
    }
}

