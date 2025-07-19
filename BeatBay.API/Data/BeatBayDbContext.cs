using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BeatBay.Model;

namespace BeatBay.Data
{
    public class BeatBayDbContext : IdentityDbContext<User, Role, int,
        Microsoft.AspNetCore.Identity.IdentityUserClaim<int>,
        UserRole,
        Microsoft.AspNetCore.Identity.IdentityUserLogin<int>,
        Microsoft.AspNetCore.Identity.IdentityRoleClaim<int>,
        Microsoft.AspNetCore.Identity.IdentityUserToken<int>>
    {
        public BeatBayDbContext(DbContextOptions<BeatBayDbContext> options) : base(options)
        {
        }

        public DbSet<Plan> Plans { get; set; }
        public DbSet<Song> Songs { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<PlaylistSong> PlaylistSongs { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PlaybackStatistic> PlaybackStatistics { get; set; }
        public DbSet<AdminActionLog> AdminActionLogs { get; set; }
        public DbSet<PlanSubscription> PlanSubscriptions { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configuración de PlaylistSong (muchos a muchos)
            builder.Entity<PlaylistSong>()
                .HasKey(ps => new { ps.PlaylistId, ps.SongId });

            builder.Entity<PlaylistSong>()
                .HasOne(ps => ps.Playlist)
                .WithMany(p => p.PlaylistSongs)
                .HasForeignKey(ps => ps.PlaylistId);

            builder.Entity<PlaylistSong>()
                .HasOne(ps => ps.Song)
                .WithMany()
                .HasForeignKey(ps => ps.SongId);

            // Configuración de User con Plan (opcional)
            builder.Entity<User>()
                .HasOne(u => u.Plan)
                .WithMany(p => p.Users)
                .HasForeignKey(u => u.PlanId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configuración de Song con Artist
            builder.Entity<Song>()
                .HasOne(s => s.Artist)
                .WithMany(u => u.UploadedSongs)
                .HasForeignKey(s => s.ArtistId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de Playlist con User
            builder.Entity<Playlist>()
                .HasOne(p => p.User)
                .WithMany(u => u.Playlists)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de Payment
            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Payment>()
                .HasOne(p => p.Plan)
                .WithMany(pl => pl.Payments)
                .HasForeignKey(p => p.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de PlaybackStatistic
            builder.Entity<PlaybackStatistic>()
                .HasOne(ps => ps.Song)
                .WithMany(s => s.PlaybackStatistics)
                .HasForeignKey(ps => ps.SongId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PlaybackStatistic>()
                .HasOne(ps => ps.User)
                .WithMany()
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configuración de AdminActionLog
            builder.Entity<AdminActionLog>()
                .HasOne(aal => aal.AdminUser)
                .WithMany()
                .HasForeignKey(aal => aal.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar Identity relations
            builder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany()
                .HasForeignKey(ur => ur.UserId);

            builder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // Configuración de PlanSubscription
            builder.Entity<PlanSubscription>()
                .HasOne(ps => ps.User)
                .WithMany()
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PlanSubscription>()
                .HasOne(ps => ps.Plan)
                .WithMany()
                .HasForeignKey(ps => ps.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración de UserConnection
            builder.Entity<UserConnection>()
                .HasOne(uc => uc.ParentSubscription)
                .WithMany(ps => ps.UserConnections)
                .HasForeignKey(uc => uc.ParentSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserConnection>()
                .HasOne(uc => uc.ChildUser)
                .WithMany()
                .HasForeignKey(uc => uc.ChildUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índice único para evitar duplicados
            builder.Entity<UserConnection>()
                .HasIndex(uc => new { uc.ParentSubscriptionId, uc.ChildUserId })
                .IsUnique();

            // Índice para buscar suscripciones activas
            builder.Entity<PlanSubscription>()
                .HasIndex(ps => new { ps.UserId, ps.IsActive });

            // Datos iniciales
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed Plans
            builder.Entity<Plan>().HasData(
                new Plan { Id = 1, Name = "Free", PriceUSD = 0, MaxConnections = 1 },
                new Plan { Id = 2, Name = "Personal", PriceUSD = 1m, MaxConnections = 1 },
                new Plan { Id = 3, Name = "Familiar", PriceUSD = 3.50m, MaxConnections = 4 },
                new Plan { Id = 4, Name = "Empresarial", PriceUSD = 35m, MaxConnections = 50 }
            );

            // Seed Roles
            builder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin", NormalizedName = "ADMIN" },
                new Role { Id = 2, Name = "Artist", NormalizedName = "ARTIST" },
                new Role { Id = 3, Name = "User", NormalizedName = "USER" }
            );
        }
    }
} 