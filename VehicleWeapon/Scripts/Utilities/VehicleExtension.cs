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
}

