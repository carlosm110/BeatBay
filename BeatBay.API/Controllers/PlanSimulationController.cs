using BeatBay.Data;
using BeatBay.DTOs;
using BeatBay.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlanSimulationController : ControllerBase
    {
        private readonly BeatBayDbContext _context;
        private readonly UserManager<User> _userManager;

        public PlanSimulationController(BeatBayDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Obtener estado del plan del usuario actual
        [HttpGet("my-plan-status")]
        public async Task<ActionResult<UserPlanStatusDto>> GetMyPlanStatus()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            // Verificar si puede tener plan
            var canPurchase = !userRoles.Contains("Admin") && !userRoles.Contains("Artist");
            var reasonCannotPurchase = "";

            if (!canPurchase)
            {
                reasonCannotPurchase = userRoles.Contains("Admin") ? "Los administradores no pueden comprar planes" : "Los artistas no pueden comprar planes";
            }

            // Buscar suscripción activa
            var activeSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.Plan)
                .Include(ps => ps.User)
                .Include(ps => ps.UserConnections)
                    .ThenInclude(uc => uc.ChildUser)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (activeSubscription != null && canPurchase)
            {
                canPurchase = false;
                reasonCannotPurchase = "Ya tienes una suscripción activa";
            }

            // Verificar si es hijo de alguna suscripción
            var isChild = await _context.UserConnections
                .AnyAsync(uc => uc.ChildUserId == userId && uc.IsActive);

            if (isChild && canPurchase)
            {
                canPurchase = false;
                reasonCannotPurchase = "Ya estás conectado a un plan familiar/empresarial";
            }

            var availablePlans = new List<PlanDto>();
            if (canPurchase)
            {
                // Excluir plan Free si ya tiene suscripción activa
                availablePlans = await _context.Plans
                    .Where(p => p.Name != "Free")
                    .Select(p => new PlanDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        PriceUSD = p.PriceUSD,
                        MaxConnections = p.MaxConnections,
                        UserCount = p.Users.Count
                    })
                    .ToListAsync();
            }

            var response = new UserPlanStatusDto
            {
                HasPlan = activeSubscription != null,
                CanPurchasePlan = canPurchase,
                ReasonCannotPurchase = reasonCannotPurchase,
                AvailablePlans = availablePlans
            };

            if (activeSubscription != null)
            {
                response.CurrentSubscription = new PlanSubscriptionDto
                {
                    Id = activeSubscription.Id,
                    UserId = activeSubscription.UserId,
                    UserName = activeSubscription.User.UserName,
                    PlanId = activeSubscription.PlanId,
                    PlanName = activeSubscription.Plan.Name,
                    PriceUSD = activeSubscription.Plan.PriceUSD,
                    MaxConnections = activeSubscription.Plan.MaxConnections,
                    UsedConnections = activeSubscription.UserConnections.Count(uc => uc.IsActive)+1,
                    StartDate = activeSubscription.StartDate,
                    EndDate = activeSubscription.EndDate,
                    IsActive = activeSubscription.IsActive,
                    ConnectedUsers = activeSubscription.UserConnections
                        .Where(uc => uc.IsActive)
                        .Select(uc => new UserDto
                        {
                            Id = uc.ChildUser.Id,
                            UserName = uc.ChildUser.UserName,
                            Email = uc.ChildUser.Email,
                            Name = uc.ChildUser.Name,
                            IsActive = uc.ChildUser.IsActive
                        })
                        .ToList()
                };
            }

            return Ok(response);
        }

        // Simular compra de plan
        [HttpPost("purchase")]
        public async Task<IActionResult> PurchasePlan(PurchasePlanDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            // Verificar restricciones
            if (userRoles.Contains("Admin") || userRoles.Contains("Artist"))
                return BadRequest(new { message = "Los administradores y artistas no pueden comprar planes" });

            // Verificar si ya tiene suscripción activa
            var hasActiveSubscription = await _context.PlanSubscriptions
                .AnyAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (hasActiveSubscription)
                return BadRequest(new { message = "Ya tienes una suscripción activa" });

            // Verificar si es hijo de alguna suscripción
            var isChild = await _context.UserConnections
                .AnyAsync(uc => uc.ChildUserId == userId && uc.IsActive);

            if (isChild)
                return BadRequest(new { message = "Ya estás conectado a un plan familiar/empresarial" });

            // Verificar que el plan existe y no es Free
            var plan = await _context.Plans.FindAsync(dto.PlanId);
            if (plan == null)
                return NotFound(new { message = "Plan no encontrado" });

            if (plan.Name == "Free")
                return BadRequest(new { message = "No puedes comprar el plan Free" });

            // Crear suscripción (simulación)
            var subscription = new PlanSubscription
            {
                UserId = userId,
                PlanId = dto.PlanId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1), // 1 mes de duración
                AmountPaid = plan.PriceUSD,
                IsActive = true
            };

            _context.PlanSubscriptions.Add(subscription);

            // Crear pago simulado
            var payment = new Payment
            {
                UserId = userId,
                PlanId = dto.PlanId,
                Status = PaymentStatus.Completed,
                PaymentDate = DateTime.UtcNow,
                Amount = plan.PriceUSD
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan comprado exitosamente (simulación)", subscriptionId = subscription.Id });
        }

        // Cambiar plan
        [HttpPost("change")]
        public async Task<IActionResult> ChangePlan(ChangePlanDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Buscar suscripción activa
            var activeSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.Plan)
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (activeSubscription == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            // Verificar el nuevo plan
            var newPlan = await _context.Plans.FindAsync(dto.NewPlanId);
            if (newPlan == null)
                return NotFound(new { message = "Plan no encontrado" });

            if (newPlan.Name == "Free")
                return BadRequest(new { message = "No puedes cambiar al plan Free" });

            if (activeSubscription.PlanId == dto.NewPlanId)
                return BadRequest(new { message = "Ya tienes este plan activo" });

            // Verificar si el cambio es válido (conexiones)
            var currentConnections = activeSubscription.UserConnections.Count(uc => uc.IsActive);
            if (currentConnections > newPlan.MaxConnections)
                return BadRequest(new { message = $"El nuevo plan solo permite {newPlan.MaxConnections} conexiones. Actualmente tienes {currentConnections} conexiones activas" });

            // Desactivar suscripción actual
            activeSubscription.IsActive = false;

            // Crear nueva suscripción
            var newSubscription = new PlanSubscription
            {
                UserId = userId,
                PlanId = dto.NewPlanId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                AmountPaid = newPlan.PriceUSD,
                IsActive = true
            };

            _context.PlanSubscriptions.Add(newSubscription);

            // Migrar conexiones si es necesario
            if (currentConnections > 0)
            {
                var connectionsToMigrate = activeSubscription.UserConnections
                    .Where(uc => uc.IsActive)
                    .Take(newPlan.MaxConnections)
                    .ToList();

                foreach (var oldConnection in connectionsToMigrate)
                {
                    var newConnection = new UserConnection
                    {
                        ParentSubscriptionId = newSubscription.Id,
                        ChildUserId = oldConnection.ChildUserId,
                        ConnectedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.UserConnections.Add(newConnection);
                    oldConnection.IsActive = false;
                }
            }

            // Crear pago simulado
            var payment = new Payment
            {
                UserId = userId,
                PlanId = dto.NewPlanId,
                Status = PaymentStatus.Completed,
                PaymentDate = DateTime.UtcNow,
                Amount = newPlan.PriceUSD
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan cambiado exitosamente", newSubscriptionId = newSubscription.Id });
        }

        // Agregar conexión (usuario hijo)
        [HttpPost("add-connection")]
        public async Task<IActionResult> AddConnection(AddConnectionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Buscar suscripción activa
            var activeSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.Plan)
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (activeSubscription == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            // Verificar límite de conexiones
            var currentConnections = activeSubscription.UserConnections.Count(uc => uc.IsActive);
            if (currentConnections >= activeSubscription.Plan.MaxConnections)
                return BadRequest(new { message = $"Has alcanzado el límite de conexiones ({activeSubscription.Plan.MaxConnections})" });

            // Verificar que el usuario hijo existe
            var childUser = await _userManager.FindByIdAsync(dto.ChildUserId.ToString());
            if (childUser == null)
                return NotFound(new { message = "Usuario no encontrado" });

            // Verificar que el usuario hijo no es Admin o Artist
            var childUserRoles = await _userManager.GetRolesAsync(childUser);
            if (childUserRoles.Contains("Admin") || childUserRoles.Contains("Artist"))
                return BadRequest(new { message = "No puedes agregar administradores o artistas como conexiones" });

            // Verificar que el usuario hijo no tiene plan propio
            var childHasSubscription = await _context.PlanSubscriptions
                .AnyAsync(ps => ps.UserId == dto.ChildUserId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (childHasSubscription)
                return BadRequest(new { message = "El usuario ya tiene su propio plan" });

            // Verificar que no está ya conectado
            var isAlreadyConnected = await _context.UserConnections
                .AnyAsync(uc => uc.ChildUserId == dto.ChildUserId && uc.IsActive);

            if (isAlreadyConnected)
                return BadRequest(new { message = "El usuario ya está conectado a un plan" });

            // Crear conexión
            var connection = new UserConnection
            {
                ParentSubscriptionId = activeSubscription.Id,
                ChildUserId = dto.ChildUserId,
                ConnectedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.UserConnections.Add(connection);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario agregado exitosamente al plan" });
        }

        // Remover conexión
        [HttpPost("remove-connection")]
        public async Task<IActionResult> RemoveConnection(RemoveConnectionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Buscar suscripción activa
            var activeSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (activeSubscription == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            // Buscar la conexión
            var connection = activeSubscription.UserConnections
                .FirstOrDefault(uc => uc.ChildUserId == dto.ChildUserId && uc.IsActive);

            if (connection == null)
                return NotFound(new { message = "Conexión no encontrada" });

            // Desactivar conexión
            connection.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario removido del plan exitosamente" });
        }

        // Cancelar suscripción
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Buscar suscripción activa
            var activeSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);

            if (activeSubscription == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            // Desactivar suscripción y todas sus conexiones
            activeSubscription.IsActive = false;
            foreach (var connection in activeSubscription.UserConnections)
            {
                connection.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Suscripción cancelada exitosamente" });
        }

        // Obtener historial de suscripciones
        [HttpGet("history")]
        public async Task<ActionResult<List<PlanSubscriptionDto>>> GetSubscriptionHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var subscriptions = await _context.PlanSubscriptions
                .Include(ps => ps.Plan)
                .Include(ps => ps.User)
                .Include(ps => ps.UserConnections)
                    .ThenInclude(uc => uc.ChildUser)
                .Where(ps => ps.UserId == userId)
                .OrderByDescending(ps => ps.CreatedAt)
                .Select(ps => new PlanSubscriptionDto
                {
                    Id = ps.Id,
                    UserId = ps.UserId,
                    UserName = ps.User.UserName,
                    PlanId = ps.PlanId,
                    PlanName = ps.Plan.Name,
                    PriceUSD = ps.Plan.PriceUSD,
                    MaxConnections = ps.Plan.MaxConnections,
                    UsedConnections = ps.UserConnections.Count(uc => uc.IsActive)+1,
                    StartDate = ps.StartDate,
                    EndDate = ps.EndDate,
                    IsActive = ps.IsActive,
                    ConnectedUsers = ps.UserConnections
                        .Where(uc => uc.IsActive)
                        .Select(uc => new UserDto
                        {
                            Id = uc.ChildUser.Id,
                            UserName = uc.ChildUser.UserName,
                            Email = uc.ChildUser.Email,
                            Name = uc.ChildUser.Name,
                            IsActive = uc.ChildUser.IsActive
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(subscriptions);
        }
    }
}