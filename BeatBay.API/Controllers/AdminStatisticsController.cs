using BeatBay.Data;
using BeatBay.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminStatisticsController : ControllerBase
    {
        private readonly BeatBayDbContext _context;

        public AdminStatisticsController(BeatBayDbContext context)
        {
            _context = context;
        }

        [HttpGet("general")]
        public async Task<IActionResult> GetGeneralStatistics()
        {
            try
            {
                var stats = new
                {
                    TotalSongs = await _context.Songs.CountAsync(s => s.IsActive),
                    TotalUsers = await _context.Users.CountAsync(u => u.IsActive),
                    TotalArtists = await _context.Users
                        .Where(u => u.IsActive)
                        .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                        .Join(_context.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r })
                        .Where(x => x.r.Name == "Artist")
                        .CountAsync(),
                    UsersByPlan = await _context.Users
                        .Where(u => u.IsActive && u.PlanId.HasValue)
                        .Include(u => u.Plan)
                        .GroupBy(u => new { u.Plan.Id, u.Plan.Name })
                        .Select(g => new
                        {
                            PlanId = g.Key.Id,
                            PlanName = g.Key.Name,
                            Count = g.Count()
                        })
                        .ToListAsync(),
                    FreeUsers = await _context.Users.CountAsync(u => u.IsActive && !u.PlanId.HasValue)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving statistics", error = ex.Message });
            }
        }

        [HttpGet("songs")]
        public async Task<IActionResult> GetSongStatistics()
        {
            try
            {
                var songStats = new
                {
                    TotalSongs = await _context.Songs.CountAsync(s => s.IsActive),
                    SongsByGenre = await _context.Songs
                        .Where(s => s.IsActive && !string.IsNullOrEmpty(s.Genre))
                        .GroupBy(s => s.Genre)
                        .Select(g => new
                        {
                            Genre = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToListAsync(),
                    RecentUploads = await _context.Songs
                        .Where(s => s.IsActive)
                        .OrderByDescending(s => s.UploadedAt)
                        .Take(10)
                        .Select(s => new
                        {
                            s.Id,
                            s.Title,
                            s.Genre,
                            s.UploadedAt,
                            Artist = s.Artist.Name ?? s.Artist.UserName
                        })
                        .ToListAsync()
                };

                return Ok(songStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving song statistics", error = ex.Message });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUserStatistics()
        {
            try
            {
                var userStats = new
                {
                    TotalUsers = await _context.Users.CountAsync(u => u.IsActive),
                    TotalArtists = await _context.Users
                        .Where(u => u.IsActive)
                        .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                        .Join(_context.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r })
                        .Where(x => x.r.Name == "Artist")
                        .CountAsync(),
                    FreeUsers = await _context.Users.CountAsync(u => u.IsActive && !u.PlanId.HasValue),
                    PremiumUsers = await _context.Users.CountAsync(u => u.IsActive && u.PlanId.HasValue),
                    UsersByPlan = await _context.Users
                        .Where(u => u.IsActive && u.PlanId.HasValue)
                        .Include(u => u.Plan)
                        .GroupBy(u => new { u.Plan.Id, u.Plan.Name })
                        .Select(g => new
                        {
                            PlanId = g.Key.Id,
                            PlanName = g.Key.Name,
                            Count = g.Count()
                        })
                        .ToListAsync(),
                    RecentRegistrations = await _context.Users
                        .Where(u => u.IsActive)
                        .OrderByDescending(u => u.CreatedAt)
                        .Take(10)
                        .Select(u => new
                        {
                            u.Id,
                            u.UserName,
                            u.Name,
                            u.CreatedAt,
                            Plan = u.Plan != null ? u.Plan.Name : "Free"
                        })
                        .ToListAsync()
                };

                return Ok(userStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving user statistics", error = ex.Message });
            }
        }

        [HttpGet("playback")]
        public async Task<IActionResult> GetPlaybackStatistics()
        {
            try
            {
                var playbackStats = new
                {
                    TotalPlays = await _context.PlaybackStatistics.SumAsync(ps => ps.PlayCount),
                    TotalDurationPlayed = await _context.PlaybackStatistics.SumAsync(ps => ps.DurationPlayedSeconds),
                    TopSongs = await _context.PlaybackStatistics
                        .Where(ps => ps.EntityType == EntityType.Song && ps.SongId.HasValue)
                        .Include(ps => ps.Song)
                        .ThenInclude(s => s.Artist)
                        .GroupBy(ps => new { ps.Song.Id, ps.Song.Title, ps.Song.Artist.Name })
                        .Select(g => new
                        {
                            SongId = g.Key.Id,
                            Title = g.Key.Title,
                            Artist = g.Key.Name,
                            TotalPlays = g.Sum(ps => ps.PlayCount),
                            TotalDuration = g.Sum(ps => ps.DurationPlayedSeconds)
                        })
                        .OrderByDescending(x => x.TotalPlays)
                        .Take(10)
                        .ToListAsync(),
                    PlaysByDate = await _context.PlaybackStatistics
                        .GroupBy(ps => ps.PlaybackDate.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            TotalPlays = g.Sum(ps => ps.PlayCount)
                        })
                        .OrderByDescending(x => x.Date)
                        .Take(30)
                        .ToListAsync()
                };

                return Ok(playbackStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving playback statistics", error = ex.Message });
            }
        }

        [HttpGet("payments")]
        public async Task<IActionResult> GetPaymentStatistics()
        {
            try
            {
                var paymentStats = new
                {
                    TotalRevenue = await _context.Payments
                        .Where(p => p.Status == PaymentStatus.Completed)
                        .SumAsync(p => p.Amount),
                    TotalPayments = await _context.Payments.CountAsync(),
                    CompletedPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Completed),
                    PendingPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending),
                    FailedPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed),
                    RevenueByPlan = await _context.Payments
                        .Where(p => p.Status == PaymentStatus.Completed)
                        .Include(p => p.Plan)
                        .GroupBy(p => new { p.Plan.Id, p.Plan.Name })
                        .Select(g => new
                        {
                            PlanId = g.Key.Id,
                            PlanName = g.Key.Name,
                            Revenue = g.Sum(p => p.Amount),
                            Count = g.Count()
                        })
                        .ToListAsync(),
                    RecentPayments = await _context.Payments
                        .Include(p => p.User)
                        .Include(p => p.Plan)
                        .OrderByDescending(p => p.PaymentDate)
                        .Take(10)
                        .Select(p => new
                        {
                            p.Id,
                            User = p.User.Name ?? p.User.UserName,
                            Plan = p.Plan.Name,
                            p.Amount,
                            p.Status,
                            p.PaymentDate
                        })
                        .ToListAsync()
                };

                return Ok(paymentStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving payment statistics", error = ex.Message });
            }
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStatistics()
        {
            try
            {
                var freePlanId = await _context.Plans.Where(p => p.Name == "Free").Select(p => p.Id).FirstOrDefaultAsync();
                var personalPlanId = await _context.Plans.Where(p => p.Name == "Personal").Select(p => p.Id).FirstOrDefaultAsync();
                var familiarPlanId = await _context.Plans.Where(p => p.Name == "Familiar").Select(p => p.Id).FirstOrDefaultAsync();
                var businessPlanId = await _context.Plans.Where(p => p.Name == "Empresarial" || p.Name == "Business").Select(p => p.Id).FirstOrDefaultAsync();

                var dashboardStats = new
                {
                    Overview = new
                    {
                        TotalSongs = await _context.Songs.CountAsync(s => s.IsActive),
                        TotalUsers = await _context.Users.CountAsync(u => u.IsActive),
                        TotalArtists = await _context.Users
                            .Where(u => u.IsActive)
                            .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                            .Join(_context.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r })
                            .Where(x => x.r.Name == "Artist")
                            .CountAsync(),
                        TotalRevenue = await _context.Payments
                            .Where(p => p.Status == PaymentStatus.Completed)
                            .SumAsync(p => p.Amount)
                    },
                    UserBreakdown = new
                    {
                        FreeUsers = freePlanId == 0 ? 0 : await _context.Users.CountAsync(u => u.IsActive && u.PlanId == freePlanId),
                        PersonalUsers = personalPlanId == 0 ? 0 : await _context.Users.CountAsync(u => u.IsActive && u.PlanId == personalPlanId),
                        FamiliarUsers = familiarPlanId == 0 ? 0 : await _context.Users.CountAsync(u => u.IsActive && u.PlanId == familiarPlanId),
                        BusinessUsers = businessPlanId == 0 ? 0 : await _context.Users.CountAsync(u => u.IsActive && u.PlanId == businessPlanId)
                    }
                };

                return Ok(dashboardStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving dashboard statistics", error = ex.Message });
            }
        }
    }
}