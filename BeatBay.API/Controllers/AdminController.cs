using BeatBay.DTOs;
using BeatBay.Data;
using BeatBay.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly BeatBayDbContext _context;

        public AdminController(BeatBayDbContext context)
        {
            _context = context;
        }

        // **Método auxiliar mejorado para verificar si un usuario es Admin**
        private async Task<bool> IsUserAdminAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Plan)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return false;

            // Verificar por Plan Admin (más confiable)
            if (user.Plan != null && user.Plan.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            // Verificar por username que contenga "admin" (fallback)
            if (!string.IsNullOrEmpty(user.UserName) &&
                user.UserName.ToLower().Contains("admin"))
                return true;

            // Verificar por email que contenga "admin" (opcional, más seguridad)
            if (!string.IsNullOrEmpty(user.Email) &&
                user.Email.ToLower().Contains("admin"))
                return true;

            return false;
        }

        // **Obtener el ID del usuario actual desde el token**
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // **Activar Usuario**
        [HttpPut("activate-user/{userId}")]
        public async Task<IActionResult> ActivateUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            if (user.IsActive)
                return BadRequest(new { message = "El usuario ya está activo" });

            user.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Usuario activado exitosamente",
                userId = user.Id,
                userName = user.UserName,
                isActive = user.IsActive
            });
        }

        // **Desactivar Usuario**
        [HttpPut("deactivate-user/{userId}")]
        public async Task<IActionResult> DeactivateUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            if (!user.IsActive)
                return BadRequest(new { message = "El usuario ya está desactivado" });

            // NUEVO: Verificar si el usuario a desactivar es Admin
            if (await IsUserAdminAsync(userId))
                return BadRequest(new { message = "No se puede desactivar a un administrador" });

            // NUEVO: Verificar si está intentando desactivarse a sí mismo
            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId)
                return BadRequest(new { message = "No puedes desactivarte a ti mismo" });

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Usuario desactivado exitosamente",
                userId = user.Id,
                userName = user.UserName,
                isActive = user.IsActive
            });
        }

        // **Cambiar Estado de Usuario (Toggle)**
        [HttpPut("toggle-user-status/{userId}")]
        public async Task<IActionResult> ToggleUserStatus(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            // NUEVO: Si el usuario está activo y es Admin, no permitir desactivación
            if (user.IsActive && await IsUserAdminAsync(userId))
                return BadRequest(new { message = "No se puede desactivar a un administrador" });

            // NUEVO: Verificar si está intentando desactivarse a sí mismo
            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId && user.IsActive)
                return BadRequest(new { message = "No puedes desactivarte a ti mismo" });

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            var status = user.IsActive ? "activado" : "desactivado";

            return Ok(new
            {
                message = $"Usuario {status} exitosamente",
                userId = user.Id,
                userName = user.UserName,
                isActive = user.IsActive
            });
        }

        // **Obtener Estado de Usuario**
        [HttpGet("user-status/{userId}")]
        public async Task<IActionResult> GetUserStatus(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Plan)
                .Select(u => new {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Name,
                    u.IsActive,
                    u.CreatedAt,
                    PlanName = u.Plan.Name
                })
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            return Ok(user);
        }

        // **Listar Todos los Usuarios para Admin - CON BÚSQUEDA CORREGIDA**
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] bool? isActive = null, [FromQuery] string search = "")
        {
            var query = _context.Users.Include(u => u.Plan).AsQueryable();

            // NUEVO: Filtrar por búsqueda si se especifica
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.UserName.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search) ||
                    u.Name.ToLower().Contains(search)
                );
            }

            // Filtrar por estado si se especifica
            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var totalUsers = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(u => new {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Name,
                    u.IsActive,
                    u.CreatedAt,
                    PlanName = u.Plan.Name,
                    u.PlanId
                })
                .ToListAsync();

            return Ok(new
            {
                users,
                pagination = new
                {
                    currentPage = page,
                    pageSize = size,
                    totalUsers,
                    totalPages = (int)Math.Ceiling((double)totalUsers / size)
                }
            });
        }
    }
}