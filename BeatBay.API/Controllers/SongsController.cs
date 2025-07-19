using Azure.Storage.Blobs;
using BeatBay.DTOs;
using BeatBay.Data;
using BeatBay.Model;
using BeatBay.API.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongsController : ControllerBase
    {
        private readonly BeatBayDbContext _context;
        private readonly BlobContainerClient _blobContainerClient;
        private readonly AzureBlobStorageSettings _blobSettings;
        private readonly UserManager<User> _userManager;

        public SongsController(BeatBayDbContext context, IOptions<AzureBlobStorageSettings> blobSettings, UserManager<User> userManager)
        {
            _context = context;
            _blobSettings = blobSettings.Value;
            _userManager = userManager;
            _blobContainerClient = new BlobContainerClient(new Uri(_blobSettings.ContainerUrl));
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return null;

            return await _userManager.FindByIdAsync(userId.ToString());
        }

        private async Task<bool> IsUserInRoleAsync(int userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                return false;

            return await _userManager.IsInRoleAsync(user, role);
        }

        // GET: api/songs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SongDto>>> GetSongs()
        {
            var songs = await _context.Songs
                .Include(s => s.Artist)
                .Where(s => s.IsActive)
                .Select(s => new SongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Duration = s.Duration,
                    Genre = s.Genre,
                    StreamingUrl = s.StreamingUrl,
                    ArtistId = s.ArtistId,
                    ArtistName = s.Artist.Name ?? s.Artist.UserName,
                    IsActive = s.IsActive,
                    UploadedAt = s.UploadedAt,
                    PlayCount = _context.PlaybackStatistics
                        .Where(ps => ps.EntityType == EntityType.Song && ps.EntityId == s.Id)
                        .Sum(ps => ps.PlayCount)
                })
                .ToListAsync();

            return Ok(songs);
        }

        // GET: api/songs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SongDto>> GetSong(int id)
        {
            var song = await _context.Songs
                .Include(s => s.Artist)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (song == null)
                return NotFound();

            var songDto = new SongDto
            {
                Id = song.Id,
                Title = song.Title,
                Duration = song.Duration,
                Genre = song.Genre,
                StreamingUrl = song.StreamingUrl,
                ArtistId = song.ArtistId,
                ArtistName = song.Artist.Name ?? song.Artist.UserName,
                IsActive = song.IsActive,
                UploadedAt = song.UploadedAt,
                PlayCount = _context.PlaybackStatistics
                    .Where(ps => ps.EntityType == EntityType.Song && ps.EntityId == id)
                    .Sum(ps => ps.PlayCount)
            };

            return Ok(songDto);
        }

        // POST: api/songs
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<SongDto>> CreateSong([FromForm] CreateSongDto dto, IFormFile audioFile)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized();

            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid();

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("Archivo de audio requerido");

            var allowedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a" };
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("Formato no soportado");

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(uniqueFileName);
                using var stream = audioFile.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);

                var song = new Song
                {
                    Title = dto.Title,
                    Duration = dto.Duration,
                    Genre = dto.Genre,
                    StreamingUrl = blobClient.Uri.ToString(),
                    ArtistId = currentUser.Id,
                    IsActive = true,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Songs.Add(song);
                await _context.SaveChangesAsync();

                var songDto = new SongDto
                {
                    Id = song.Id,
                    Title = song.Title,
                    Duration = song.Duration,
                    Genre = song.Genre,
                    StreamingUrl = song.StreamingUrl,
                    ArtistId = song.ArtistId,
                    ArtistName = currentUser.Name ?? currentUser.UserName,
                    IsActive = song.IsActive,
                    UploadedAt = song.UploadedAt,
                    PlayCount = 0
                };

                return CreatedAtAction(nameof(GetSong), new { id = song.Id }, songDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // PUT: api/songs/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateSong(int id, UpdateSongDto dto)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
                return NotFound();

            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized();

            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (song.ArtistId != currentUser.Id && !isAdmin)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(dto.Title))
                song.Title = dto.Title;

            if (dto.Duration.HasValue)
                song.Duration = dto.Duration.Value;

            if (!string.IsNullOrWhiteSpace(dto.Genre))
                song.Genre = dto.Genre;

            if (dto.IsActive.HasValue)
                song.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE: api/songs/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteSong(int id)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
                return NotFound();

            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized();

            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (song.ArtistId != currentUser.Id && !isAdmin)
                return Forbid();

            song.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET: api/songs/my-songs
        [HttpGet("my-songs")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SongDto>>> GetMySongs()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized();

            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid();

            var songs = await _context.Songs
                .Include(s => s.Artist)
                .Where(s => s.ArtistId == currentUser.Id)
                .Select(s => new SongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Duration = s.Duration,
                    Genre = s.Genre,
                    StreamingUrl = s.StreamingUrl,
                    ArtistId = s.ArtistId,
                    ArtistName = s.Artist.Name ?? s.Artist.UserName,
                    IsActive = s.IsActive,
                    UploadedAt = s.UploadedAt,
                    PlayCount = _context.PlaybackStatistics
                        .Where(ps => ps.EntityType == EntityType.Song && ps.EntityId == s.Id)
                        .Sum(ps => ps.PlayCount)
                })
                .ToListAsync();

            return Ok(songs);
        }

        // POST: api/songs/5/play
        [HttpPost("{id}/play")]
        [Authorize]
        public async Task<IActionResult> RecordPlay(int id, [FromBody] RecordPlayRequest request)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
                return NotFound();

            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized();

            if (request.DurationPlayedSeconds <= 0)
                return BadRequest("La duración reproducida debe ser mayor a 0");

            var today = DateTime.UtcNow.Date;
            var stat = await _context.PlaybackStatistics
                .FirstOrDefaultAsync(ps =>
                    ps.EntityType == EntityType.Song &&
                    ps.EntityId == id &&
                    ps.UserId == currentUser.Id &&
                    ps.PlaybackDate.Date == today);

            if (stat == null)
            {
                // Nuevo registro
                int playCount = request.DurationPlayedSeconds >= 30 ? request.DurationPlayedSeconds / 30 : 0;
                stat = new PlaybackStatistic
                {
                    EntityType = EntityType.Song,
                    EntityId = id,
                    SongId = id,
                    UserId = currentUser.Id,
                    PlaybackDate = DateTime.UtcNow,
                    DurationPlayedSeconds = request.DurationPlayedSeconds,
                    PlayCount = playCount
                };
                _context.PlaybackStatistics.Add(stat);
            }
            else
            {
                // Actualizar registro existente
                int prevDuration = stat.DurationPlayedSeconds;
                stat.DurationPlayedSeconds += request.DurationPlayedSeconds;
                stat.PlaybackDate = DateTime.UtcNow;

                // Calcular cuántos múltiplos de 30 segundos se cruzaron en total
                int prevPlays = prevDuration / 30;
                int newPlays = stat.DurationPlayedSeconds / 30;
                stat.PlayCount += (newPlays - prevPlays);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Reproducción registrada exitosamente" });
        }

        // Clase para el request
        public class RecordPlayRequest
        {
            public int DurationPlayedSeconds { get; set; }
        }
    } 
}