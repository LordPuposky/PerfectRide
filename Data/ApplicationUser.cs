using Microsoft.AspNetCore.Identity;

namespace PerfectRide.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}

