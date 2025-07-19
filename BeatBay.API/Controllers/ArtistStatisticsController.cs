using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BeatBay.Data;
using BeatBay.Model;
using BeatBay.DTOs;
using BeatBay.API.Services;


namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ArtistStatisticsController : ControllerBase
    {
        private readonly PdfReportService _pdfReportService;
        private readonly BeatBayDbContext _context;
        private readonly UserManager<User> _userManager;

        public ArtistStatisticsController(BeatBayDbContext context, UserManager<User> userManager, PdfReportService pdfReportService)
        {
            _context = context;
            _userManager = userManager;
            _pdfReportService = pdfReportService;
        }

        // Método helper para verificar roles usando Identity
        private async Task<bool> IsUserInRoleAsync(int userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                return false;

            return await _userManager.IsInRoleAsync(user, role);
        }

        // Método helper para obtener el usuario actual
        private async Task<User?> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return null;

            return await _userManager.FindByIdAsync(userId.ToString());
        }

        // GET: api/artiststatistics/my-songs-stats
        [HttpGet("my-songs-stats")]
        public async Task<ActionResult<List<SongStatsDto>>> GetMySongsStats()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para ver estadísticas.");

            try
            {
                var songsStats = await _context.Songs
                    .Where(s => s.ArtistId == currentUser.Id)
                    .Select(s => new SongStatsDto
                    {
                        SongId = s.Id,
                        Title = s.Title,
                        ArtistName = s.Artist.Name ?? s.Artist.UserName,
                        PlayCount = s.PlaybackStatistics.Sum(ps => ps.PlayCount),
                        TotalDurationPlayed = s.PlaybackStatistics.Sum(ps => ps.DurationPlayedSeconds)
                    })
                    .OrderByDescending(s => s.PlayCount)
                    .ToListAsync();

                return Ok(songsStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener estadísticas: {ex.Message}");
            }
        }

        // GET: api/artiststatistics/song-details/{songId}
        [HttpGet("song-details/{songId}")]
        public async Task<ActionResult<List<PlaybackStatisticDto>>> GetSongDetails(int songId)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para ver estadísticas.");

            // Verificar que la canción pertenezca al artista actual
            var song = await _context.Songs.FirstOrDefaultAsync(s => s.Id == songId);
            if (song == null)
                return NotFound("Canción no encontrada.");

            if (song.ArtistId != currentUser.Id && !isAdmin)
                return Forbid("No tienes permisos para ver estadísticas de esta canción.");

            try
            {
                var playbackStats = await _context.PlaybackStatistics
                    .Where(ps => ps.SongId == songId)
                    .Include(ps => ps.Song)
                    .Include(ps => ps.User)
                    .Select(ps => new PlaybackStatisticDto
                    {
                        Id = ps.Id,
                        EntityType = ps.EntityType,
                        EntityId = ps.EntityId,
                        SongId = ps.SongId,
                        SongTitle = ps.Song != null ? ps.Song.Title : null,
                        UserId = ps.UserId,
                        UserName = ps.User != null ? (ps.User.Name ?? ps.User.UserName) : "Usuario desconocido",
                        PlaybackDate = ps.PlaybackDate,
                        DurationPlayedSeconds = ps.DurationPlayedSeconds,
                        PlayCount = ps.PlayCount
                    })
                    .OrderByDescending(ps => ps.PlaybackDate)
                    .ToListAsync();

                return Ok(playbackStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener detalles de la canción: {ex.Message}");
            }
        }

        // GET: api/artiststatistics/summary
        [HttpGet("summary")]
        public async Task<ActionResult<object>> GetArtistSummary()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para ver estadísticas.");

            try
            {
                // Obtener todas las canciones del artista
                var artistSongs = await _context.Songs
                    .Where(s => s.ArtistId == currentUser.Id)
                    .Include(s => s.PlaybackStatistics)
                    .ToListAsync();


                // Calcular estadísticas manualmente para evitar problemas con nulls
                var totalSongs = artistSongs.Count;
                var totalPlays = 0;
                var totalDurationPlayed = 0;

                foreach (var song in artistSongs)
                {
                    if (song.PlaybackStatistics != null)
                    {
                        totalPlays += song.PlaybackStatistics.Sum(ps => ps.PlayCount);
                        totalDurationPlayed += song.PlaybackStatistics.Sum(ps => ps.DurationPlayedSeconds);
                    }
                }

                var activeSongs = artistSongs.Count(s => s.IsActive);
                var inactiveSongs = artistSongs.Count(s => !s.IsActive);

                var summary = new
                {
                    TotalSongs = totalSongs,
                    TotalPlays = totalPlays,
                    TotalDurationPlayed = totalDurationPlayed,
                    ActiveSongs = activeSongs,
                    InactiveSongs = inactiveSongs
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener resumen: {ex.Message}");
            }
        }



        // GET: api/artiststatistics/top-songs
        [HttpGet("top-songs")]
        public async Task<ActionResult<TopSongsDto>> GetTopSongs([FromQuery] int limit = 10)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para ver estadísticas.");

            try
            {
                var topSongs = await _context.Songs
                    .Where(s => s.ArtistId == currentUser.Id)
                    .Select(s => new SongStatsDto
                    {
                        SongId = s.Id,
                        Title = s.Title,
                        ArtistName = s.Artist.Name ?? s.Artist.UserName,
                        PlayCount = s.PlaybackStatistics.Sum(ps => ps.PlayCount),
                        TotalDurationPlayed = s.PlaybackStatistics.Sum(ps => ps.DurationPlayedSeconds)
                    })
                    .OrderByDescending(s => s.PlayCount)
                    .Take(limit)
                    .ToListAsync();

                return Ok(new TopSongsDto { Songs = topSongs });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener top canciones: {ex.Message}");
            }
        }

        // GET: api/artiststatistics/download-report
        [HttpGet("download-report")]
        public async Task<ActionResult> DownloadReport()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para descargar el reporte.");

            try
            {
                // Obtener resumen
                var artistSongs = await _context.Songs
                    .Where(s => s.ArtistId == currentUser.Id)
                    .Include(s => s.PlaybackStatistics)
                    .ToListAsync();

                var totalSongs = artistSongs.Count;
                var totalPlays = 0;
                var totalDurationPlayed = 0;

                foreach (var song in artistSongs)
                {
                    if (song.PlaybackStatistics != null)
                    {
                        totalPlays += song.PlaybackStatistics.Sum(ps => ps.PlayCount);
                        totalDurationPlayed += song.PlaybackStatistics.Sum(ps => ps.DurationPlayedSeconds);
                    }
                }

                var activeSongs = artistSongs.Count(s => s.IsActive);
                var inactiveSongs = artistSongs.Count(s => !s.IsActive);

                var summary = new
                {
                    TotalSongs = totalSongs,
                    TotalPlays = totalPlays,
                    TotalDurationPlayed = totalDurationPlayed,
                    ActiveSongs = activeSongs,
                    InactiveSongs = inactiveSongs
                };

                // Obtener todas las canciones
                var songs = await _context.Songs
                    .Where(s => s.ArtistId == currentUser.Id)
                    .Select(s => new SongStatsDto
                    {
                        SongId = s.Id,
                        Title = s.Title,
                        ArtistName = s.Artist.Name ?? s.Artist.UserName,
                        PlayCount = s.PlaybackStatistics.Sum(ps => ps.PlayCount),
                        TotalDurationPlayed = s.PlaybackStatistics.Sum(ps => ps.DurationPlayedSeconds)
                    })
                    .OrderByDescending(s => s.PlayCount)
                    .ToListAsync();

                // Obtener top 10 canciones
                var topSongs = songs.Take(10).ToList();

                // Generar PDF
                var pdfBytes = _pdfReportService.GenerateArtistStatisticsReport(
                    currentUser.Name ?? currentUser.UserName ?? "Artista",
                    summary,
                    songs,
                    topSongs
                );

                // Configurar nombre del archivo
                var fileName = $"Reporte_Estadisticas_{currentUser.Name ?? currentUser.UserName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al generar el reporte: {ex.Message}");
            }
        }
    }
}