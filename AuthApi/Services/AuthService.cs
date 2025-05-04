using AuthApi.Data;
using BCrypt.Net;

namespace AuthApi.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public bool Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email);
            if (admin == null) return false;

            return BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
        }
    }
}