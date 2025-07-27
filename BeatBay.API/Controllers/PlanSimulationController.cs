using BeatBay.Data;
using BeatBay.DTOs;
using BeatBay.Model;
using BeatBay.Services;
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
        private readonly PayPalService _payPalService;

        public PlanSimulationController(
            BeatBayDbContext context,
            UserManager<User> userManager,
            PayPalService payPalService)
        {
            _context = context;
            _userManager = userManager;
            _payPalService = payPalService;
        }

        // 1. Obtener estado del plan del usuario actual
        [HttpGet("my-plan-status")]
        public async Task<ActionResult<UserPlanStatusDto>> GetMyPlanStatus()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                {
                    return BadRequest(new { message = "Usuario no válido" });
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return NotFound(new { message = "Usuario no encontrado" });
                }

                // ✅ VERIFICAR Y EXPIRAR SUSCRIPCIONES VENCIDAS AUTOMÁTICAMENTE
                await CheckAndExpireSubscriptions(userId);

                var roles = await _userManager.GetRolesAsync(user);
                var canPurchase = !roles.Contains("Admin") && !roles.Contains("Artist");
                string reasonCannotPurchase = "";

                if (!canPurchase)
                    reasonCannotPurchase = roles.Contains("Admin")
                        ? "Los administradores no pueden comprar planes"
                        : "Los artistas no pueden comprar planes";

                // Suscripción activa - DESPUÉS de verificar expiraciones
                var activeSub = await _context.PlanSubscriptions
                    .Include(ps => ps.Plan)
                    .Include(ps => ps.User)
                    .Include(ps => ps.UserConnections)
                        .ThenInclude(uc => uc.ChildUser)
                    .FirstOrDefaultAsync(ps =>
                        ps.UserId == userId &&
                        ps.IsActive &&
                        ps.EndDate > DateTime.UtcNow);

                if (activeSub != null && canPurchase)
                {
                    canPurchase = false;
                    reasonCannotPurchase = "Ya tienes una suscripción activa";
                }

                // ¿Es hijo de otro plan?
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
                    availablePlans = await _context.Plans
                        .Where(p => p.Name != "Free")
                        .Select(p => new PlanDto
                        {
                            Id = p.Id,
                            Name = p.Name,
                            PriceUSD = p.PriceUSD,
                            MaxConnections = p.MaxConnections,
                            UserCount = 0
                        })
                        .ToListAsync();
                }

                var dto = new UserPlanStatusDto
                {
                    HasPlan = activeSub != null,
                    CanPurchasePlan = canPurchase,
                    ReasonCannotPurchase = reasonCannotPurchase,
                    AvailablePlans = availablePlans
                };

                if (activeSub != null)
                {
                    dto.CurrentSubscription = new PlanSubscriptionDto
                    {
                        Id = activeSub.Id,
                        UserId = activeSub.UserId,
                        UserName = activeSub.User?.UserName ?? "Unknown",
                        PlanId = activeSub.PlanId,
                        PlanName = activeSub.Plan.Name,
                        PriceUSD = activeSub.Plan.PriceUSD,
                        MaxConnections = activeSub.Plan.MaxConnections,
                        UsedConnections = activeSub.UserConnections.Count(uc => uc.IsActive) + 1,
                        StartDate = activeSub.StartDate,
                        EndDate = activeSub.EndDate,
                        IsActive = activeSub.IsActive,
                        ConnectedUsers = activeSub.UserConnections
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

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Error al obtener el estado del plan",
                    error = ex.Message
                });
            }
        }

        //  MÉTODO PRIVADO PARA VERIFICAR Y EXPIRAR SUSCRIPCIONES
        private async Task CheckAndExpireSubscriptions(int userId)
        {
            // Buscar suscripción del usuario que esté marcada como activa pero ya expiró
            var expiredSubscription = await _context.PlanSubscriptions
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps =>
                    ps.UserId == userId &&
                    ps.IsActive &&
                    ps.EndDate <= DateTime.UtcNow);

            if (expiredSubscription != null)
            {
                // Marcar suscripción como inactiva
                expiredSubscription.IsActive = false;

                // Obtener plan Free para resetear usuarios
                var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.Name == "Free");

                // Desactivar todas las conexiones y resetear PlanId de usuarios hijos
                foreach (var connection in expiredSubscription.UserConnections.Where(uc => uc.IsActive))
                {
                    connection.IsActive = false;

                    // Resetear PlanId del usuario hijo al plan Free
                    var childUser = await _userManager.FindByIdAsync(connection.ChildUserId.ToString());
                    if (childUser != null)
                    {
                        childUser.PlanId = freePlan?.Id;
                        await _userManager.UpdateAsync(childUser);
                    }
                }

                // Resetear PlanId del usuario principal al plan Free
                var mainUser = await _userManager.FindByIdAsync(userId.ToString());
                if (mainUser != null)
                {
                    mainUser.PlanId = freePlan?.Id;
                    await _userManager.UpdateAsync(mainUser);
                }

                // Guardar cambios en la base de datos
                await _context.SaveChangesAsync();
            }
        }

        // 2. Crear pago y redirigir a PayPal
        [HttpPost("purchase")]
        public async Task<IActionResult> PurchasePlan(PurchasePlanDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin") || roles.Contains("Artist"))
                return BadRequest(new { message = "Los administradores y artistas no pueden comprar planes" });

            var hasActive = await _context.PlanSubscriptions
                .AnyAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);
            if (hasActive)
                return BadRequest(new { message = "Ya tienes una suscripción activa" });

            var isChildPlan = await _context.UserConnections
                .AnyAsync(uc => uc.ChildUserId == userId && uc.IsActive);
            if (isChildPlan)
                return BadRequest(new { message = "Ya estás conectado a un plan familiar/empresarial" });

            var plan = await _context.Plans.FindAsync(dto.PlanId);
            if (plan == null) return NotFound(new { message = "Plan no encontrado" });
            if (plan.Name == "Free")
                return BadRequest(new { message = "No puedes comprar el plan Free" });

            // Crear pago en PayPal
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var payment = _payPalService.CreatePayment(
                plan.Name,
                plan.PriceUSD,
                plan.Id,
                userId,
                baseUrl
            );

            // Guardar registro local pendiente
            var paymentRecord = new Payment
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = PaymentStatus.Pending,
                PaymentDate = DateTime.UtcNow,
                Amount = plan.PriceUSD
            };
            _context.Payments.Add(paymentRecord);
            await _context.SaveChangesAsync();

            var approvalUrl = payment.links
                .First(l => l.rel.Equals("approval_url", StringComparison.OrdinalIgnoreCase))
                .href;

            return Ok(new
            {
                paymentId = payment.id,
                localPaymentId = paymentRecord.Id,
                approvalUrl
            });
        }

        // 3. Ejecutar pago tras aprobación en PayPal
        [HttpGet("execute-payment")]
        [AllowAnonymous]
        public async Task<IActionResult> ExecutePayment([FromQuery] string paymentId, [FromQuery] string PayerID, [FromQuery] string token)
        {
            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(PayerID))
                return BadRequest(new { message = "Parámetros de pago inválidos" });

            var executed = _payPalService.ExecutePayment(paymentId, PayerID);
            if (executed.state.ToLower() != "approved")
                return BadRequest(new { message = "El pago no fue aprobado" });

            // Leer custom = "planId|userId"
            var parts = executed.transactions[0].custom?.Split('|');
            if (parts == null || parts.Length != 2)
                return BadRequest(new { message = "Datos de pago corruptos" });

            int planId = int.Parse(parts[0]), userId = int.Parse(parts[1]);

            var record = await _context.Payments
                .Where(p => p.UserId == userId
                         && p.PlanId == planId
                         && p.Status == PaymentStatus.Pending)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
            if (record == null)
                return BadRequest(new { message = "No se encontró el pago pendiente" });

            record.Status = PaymentStatus.Completed;

            // ✅ ACTUALIZAR EL PLANID DEL USUARIO
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                user.PlanId = planId;
                await _userManager.UpdateAsync(user);
            }

            var subscription = new PlanSubscription
            {
                UserId = userId,
                PlanId = planId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                AmountPaid = record.Amount,
                IsActive = true
            };
            _context.PlanSubscriptions.Add(subscription);

            await _context.SaveChangesAsync();

            // Redirigir al cliente a Plans/Index
            return Redirect("https://localhost:7194/Plans/Index");
        }

        // 4. Cancelar pago (usuario abandona PayPal)
        [HttpGet("cancel-payment")]
        [AllowAnonymous]
        public IActionResult CancelPayment([FromQuery] string token)
        {
            // (Opcional) marcar el pago como cancelado en BD
            return Ok(new { message = "Pago cancelado por el usuario" });
        }

        // 5. Consultar estado del pago en PayPal
        [HttpGet("payment-status/{paymentId}")]
        public IActionResult GetPaymentStatus(string paymentId)
        {
            try
            {
                var payment = _payPalService.GetPayment(paymentId);
                return Ok(new
                {
                    id = payment.id,
                    state = payment.state,
                    create_time = payment.create_time,
                    update_time = payment.update_time
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error al obtener el estado del pago", error = ex.Message });
            }
        }

        // 6. Agregar conexión de usuario hijo (VERSIÓN MEJORADA)
        [HttpPost("add-connection")]
        public async Task<IActionResult> AddConnection(AddConnectionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var active = await _context.PlanSubscriptions
                .Include(ps => ps.Plan)
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);
            if (active == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            var used = active.UserConnections.Count(uc => uc.IsActive);
            if (used >= active.Plan.MaxConnections)
                return BadRequest(new { message = $"Has alcanzado el límite de conexiones ({active.Plan.MaxConnections})" });

            var child = await _userManager.FindByIdAsync(dto.ChildUserId.ToString());
            if (child == null) return NotFound(new { message = "Usuario no encontrado" });

            var childRoles = await _userManager.GetRolesAsync(child);
            if (childRoles.Contains("Admin") || childRoles.Contains("Artist"))
                return BadRequest(new { message = "No puedes agregar administradores o artistas como conexiones" });

            var childHasSub = await _context.PlanSubscriptions
                .AnyAsync(ps => ps.UserId == dto.ChildUserId && ps.IsActive && ps.EndDate > DateTime.UtcNow);
            if (childHasSub)
                return BadRequest(new { message = "El usuario ya tiene su propio plan" });

            // VERIFICAR SI YA ESTÁ CONECTADO ACTIVAMENTE EN CUALQUIER PLAN
            var activeConnection = await _context.UserConnections
                .AnyAsync(uc => uc.ChildUserId == dto.ChildUserId && uc.IsActive);
            if (activeConnection)
                return BadRequest(new { message = "El usuario ya está conectado a un plan" });

            // BUSCAR UNA CONEXIÓN INACTIVA EXISTENTE PARA ESTE USUARIO Y SUSCRIPCIÓN
            var existingConnection = await _context.UserConnections
                .FirstOrDefaultAsync(uc =>
                    uc.ParentSubscriptionId == active.Id &&
                    uc.ChildUserId == dto.ChildUserId &&
                    !uc.IsActive);

            if (existingConnection != null)
            {
                // REACTIVAR LA CONEXIÓN EXISTENTE
                existingConnection.IsActive = true;
                existingConnection.ConnectedAt = DateTime.UtcNow; // Actualizar fecha de reconexión
            }
            else
            {
                // CREAR NUEVA CONEXIÓN SI NO EXISTE UNA PREVIA
                _context.UserConnections.Add(new UserConnection
                {
                    ParentSubscriptionId = active.Id,
                    ChildUserId = dto.ChildUserId,
                    ConnectedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            // ACTUALIZAR EL PLAN DEL USUARIO HIJO
            child.PlanId = active.PlanId;
            await _userManager.UpdateAsync(child);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario agregado exitosamente al plan" });
        }

        // 7. Remover conexión
        [HttpPost("remove-connection")]
        public async Task<IActionResult> RemoveConnection(RemoveConnectionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var active = await _context.PlanSubscriptions
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);
            if (active == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            var conn = active.UserConnections
                .FirstOrDefault(uc => uc.ChildUserId == dto.ChildUserId && uc.IsActive);
            if (conn == null)
                return NotFound(new { message = "Conexión no encontrada" });

            conn.IsActive = false;

            var childUser = await _userManager.FindByIdAsync(dto.ChildUserId.ToString());
            if (childUser != null)
            {
                var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.Name == "Free");
                childUser.PlanId = freePlan?.Id; // O null si no tienes plan Free
                await _userManager.UpdateAsync(childUser);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuario removido del plan exitosamente" });
        }

        // 8. Cancelar suscripción
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var active = await _context.PlanSubscriptions
                .Include(ps => ps.UserConnections)
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.IsActive && ps.EndDate > DateTime.UtcNow);
            if (active == null)
                return BadRequest(new { message = "No tienes una suscripción activa" });

            active.IsActive = false;
            foreach (var c in active.UserConnections)
                c.IsActive = false;

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.Name == "Free");
                user.PlanId = freePlan?.Id; // O null si no tienes plan Free
                await _userManager.UpdateAsync(user);
            }

            // ✅ RESETEAR EL PLANID DE TODOS LOS USUARIOS CONECTADOS
            foreach (var conn in active.UserConnections)
            {
                var childUser = await _userManager.FindByIdAsync(conn.ChildUserId.ToString());
                if (childUser != null)
                {
                    var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.Name == "Free");
                    childUser.PlanId = freePlan?.Id;
                    await _userManager.UpdateAsync(childUser);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción cancelada exitosamente" });
        }

        // 9. Historial de suscripciones
        [HttpGet("history")]
        public async Task<ActionResult<List<PlanSubscriptionDto>>> GetSubscriptionHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var list = await _context.PlanSubscriptions
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
                    UsedConnections = ps.UserConnections.Count(uc => uc.IsActive) + 1,
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

            return Ok(list);
        }

        // 10. Buscar usuarios por nombre de usuario
        [HttpGet("search-users")]
        public async Task<ActionResult<List<UserDto>>> SearchUsers([FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username))
                return BadRequest(new { message = "El nombre de usuario es requerido" });

            var users = await _userManager.Users
                .Where(u => u.UserName.Contains(username) && u.IsActive)
                .Take(5) // Limit results
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Name = u.Name,
                    Email = u.Email,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            // Filter out admins and artists
            var filteredUsers = new List<UserDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(user.Id.ToString()));
                if (!roles.Contains("Admin") && !roles.Contains("Artist"))
                {
                    filteredUsers.Add(user);
                }
            }

            return Ok(filteredUsers);
        }
    }
}