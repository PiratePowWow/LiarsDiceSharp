using LiarsDiceSharp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiarsDiceSharp.Models.Contexts
{
    public class GameContext : DbContext
    {
        public GameContext( DbContextOptions<GameContext> options) : base(
            options)
        {
        }
        
        public DbSet<Player> Players { get; set; }
        public DbSet<GameState> GameStates { get; set; }

    }
    
}