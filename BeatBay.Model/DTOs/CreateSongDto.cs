using System;
using System.ComponentModel.DataAnnotations;

namespace BeatBay.DTOs
{
    public class CreateSongDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public TimeSpan Duration { get; set; }

        [MaxLength(100)]
        public string? Genre { get; set; }

    }
}