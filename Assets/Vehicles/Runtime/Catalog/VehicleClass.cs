namespace TycoWorld.Vehicles
{
    /// <summary>Handling and gameplay class of a vehicle (spec vehicles-3a §5).</summary>
    public enum VehicleClass
    {
        MotorMatic,
        MotorBebek,
        CarSmall,
        Mpv,
        Van,
        Angkot,
        Pickup,
        Truck,
        Police
    }

    public static class VehicleClasses
    {
        public static bool IsBike(VehicleClass c) => c == VehicleClass.MotorMatic || c == VehicleClass.MotorBebek;

        /// <summary>The word the HUD and toasts use for it.</summary>
        public static string Noun(VehicleClass c)
        {
            switch (c)
            {
                case VehicleClass.MotorMatic:
                case VehicleClass.MotorBebek:
                    return "motor";
                case VehicleClass.Angkot:
                    return "angkot";
                case VehicleClass.Pickup:
                    return "pikap";
                case VehicleClass.Truck:
                    return "truk";
                default:
                    return "mobil";
            }
        }
    }
}
