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
    [Authorize] // Restaurar autorización global
    public class PlaylistsController : ControllerBase
    {
        private readonly BeatBayDbContext _context;
        private readonly UserManager<User> _userManager;

        public PlaylistsController(BeatBayDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper method para obtener el usuario actual
        private async Task<User?> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return null;

            return await _userManager.FindByIdAsync(userId);
        }

        // GET: api/playlists (Playlists públicas)
        [HttpGet]
        [AllowAnonymous] // Permitir ver playlists públicas sin autenticación
        public async Task<ActionResult<IEnumerable<PlaylistDto>>> GetPlaylists()
        {
            var playlists = await _context.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                        .ThenInclude(s => s.Artist)
                .Select(p => new PlaylistDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    UserId = p.UserId,
                    UserName = p.User.Name ?? p.User.UserName,
                    Songs = p.PlaylistSongs.Select(ps => new SongDto
                    {
                        Id = ps.Song.Id,
                        Title = ps.Song.Title,
                        Duration = ps.Song.Duration,
                        Genre = ps.Song.Genre,
                        StreamingUrl = ps.Song.StreamingUrl,
                        ArtistId = ps.Song.ArtistId,
                        ArtistName = ps.Song.Artist.Name ?? ps.Song.Artist.UserName,
                        IsActive = ps.Song.IsActive,
                        UploadedAt = ps.Song.UploadedAt,
                        PlayCount = ps.Song.PlaybackStatistics.Sum(stat => stat.PlayCount)
                    }).ToList()
                })
                .ToListAsync();

            return Ok(playlists);
        }

        // GET: api/playlists/5
        [HttpGet("{id}")]
        [AllowAnonymous] // Permitir ver detalles de playlist sin autenticación
        public async Task<ActionResult<PlaylistDto>> GetPlaylist(int id)
        {
            var playlist = await _context.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                        .ThenInclude(s => s.Artist)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return NotFound();

            var playlistDto = new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                UserId = playlist.UserId,
                UserName = playlist.User.Name ?? playlist.User.UserName,
                Songs = playlist.PlaylistSongs.Select(ps => new SongDto
                {
                    Id = ps.Song.Id,
                    Title = ps.Song.Title,
                    Duration = ps.Song.Duration,
                    Genre = ps.Song.Genre,
                    StreamingUrl = ps.Song.StreamingUrl,
                    ArtistId = ps.Song.ArtistId,
                    ArtistName = ps.Song.Artist.Name ?? ps.Song.Artist.UserName,
                    IsActive = ps.Song.IsActive,
                    UploadedAt = ps.Song.UploadedAt,
                    PlayCount = ps.Song.PlaybackStatistics.Sum(stat => stat.PlayCount)
                }).ToList()
            };

            return Ok(playlistDto);
        }

        // GET: api/playlists/my-playlists
        [HttpGet("my-playlists")]
        public async Task<ActionResult<IEnumerable<PlaylistDto>>> GetMyPlaylists()
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlists = await _context.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                        .ThenInclude(s => s.Artist)
                .Where(p => p.UserId == user.Id)
                .Select(p => new PlaylistDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    UserId = p.UserId,
                    UserName = p.User.Name ?? p.User.UserName,
                    Songs = p.PlaylistSongs.Select(ps => new SongDto
                    {
                        Id = ps.Song.Id,
                        Title = ps.Song.Title,
                        Duration = ps.Song.Duration,
                        Genre = ps.Song.Genre,
                        StreamingUrl = ps.Song.StreamingUrl,
                        ArtistId = ps.Song.ArtistId,
                        ArtistName = ps.Song.Artist.Name ?? ps.Song.Artist.UserName,
                        IsActive = ps.Song.IsActive,
                        UploadedAt = ps.Song.UploadedAt,
                        PlayCount = ps.Song.PlaybackStatistics.Sum(stat => stat.PlayCount)
                    }).ToList()
                })
                .ToListAsync();

            return Ok(playlists);
        }

        // POST: api/playlists
        [HttpPost]
        public async Task<ActionResult<PlaylistDto>> CreatePlaylist(CreatePlaylistDto dto)
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlist = new Playlist
            {
                Name = dto.Name,
                UserId = user.Id
            };

            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();

            var playlistDto = new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                UserId = playlist.UserId,
                UserName = user.Name ?? user.UserName,
                Songs = new List<SongDto>()
            };

            return CreatedAtAction(nameof(GetPlaylist), new { id = playlist.Id }, playlistDto);
        }
        // PUT: api/playlists/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlaylist(int id, CreatePlaylistDto dto)
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
                return NotFound();

            // Verificar que el usuario es el propietario de la playlist
            if (playlist.UserId != user.Id)
            {
                return StatusCode(403, new { message = "No tienes permisos para editar esta playlist." });
            }

            playlist.Name = dto.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Playlist actualizada correctamente" });
        }
        // DELETE: api/playlists/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlaylist(int id)
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlist = await _context.Playlists
                .Include(p => p.PlaylistSongs) // Incluir las canciones de la playlist
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return NotFound();

            // Verificar que el usuario es el propietario de la playlist
            if (playlist.UserId != user.Id)
            {
                return StatusCode(403, new { message = "No tienes permisos para eliminar esta playlist." });
            }

            // Eliminar primero todas las canciones de la playlist
            if (playlist.PlaylistSongs.Any())
            {
                _context.PlaylistSongs.RemoveRange(playlist.PlaylistSongs);
            }

            // Luego eliminar la playlist
            _context.Playlists.Remove(playlist);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Playlist eliminada correctamente" });
        }

        // POST: api/playlists/5/songs
        [HttpPost("{id}/songs")]
        public async Task<IActionResult> AddSongToPlaylist(int id, AddSongToPlaylistDto dto)
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
                return NotFound("Playlist no encontrada.");

            // Verificar que el usuario es el propietario de la playlist
            if (playlist.UserId != user.Id)
            {
                return StatusCode(403, new { message = "No tienes permisos para modificar esta playlist." });
            }

            var song = await _context.Songs.FindAsync(dto.SongId);
            if (song == null)
                return NotFound("Canción no encontrada.");

            // Verificar que la canción está activa
            if (!song.IsActive)
                return BadRequest("No se puede agregar una canción inactiva.");

            // Verificar si la canción ya está en la playlist
            var existingPlaylistSong = await _context.PlaylistSongs
                .FirstOrDefaultAsync(ps => ps.PlaylistId == id && ps.SongId == dto.SongId);

            if (existingPlaylistSong != null)
                return BadRequest("La canción ya está en la playlist.");

            var playlistSong = new PlaylistSong
            {
                PlaylistId = id,
                SongId = dto.SongId
            };

            _context.PlaylistSongs.Add(playlistSong);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Canción agregada a la playlist correctamente" });
        }

        // DELETE: api/playlists/5/songs/3
        [HttpDelete("{id}/songs/{songId}")]
        public async Task<IActionResult> RemoveSongFromPlaylist(int id, int songId)
        {
            var user = await GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
                return NotFound("Playlist no encontrada.");

            // Verificar que el usuario es el propietario de la playlist
            if (playlist.UserId != user.Id)
            {
                return StatusCode(403, new { message = "No tienes permisos para modificar esta playlist." });
            }

            var playlistSong = await _context.PlaylistSongs
                .FirstOrDefaultAsync(ps => ps.PlaylistId == id && ps.SongId == songId);

            if (playlistSong == null)
                return NotFound("La canción no está en la playlist.");

            _context.PlaylistSongs.Remove(playlistSong);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Canción removida de la playlist correctamente" });
        }
    }
}