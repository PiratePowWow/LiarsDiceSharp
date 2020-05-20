using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiarsDiceSharp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiarsDiceSharp.Models.Contexts
{
    public class GameContext : DbContext
    {
        public GameContext(DbContextOptions<GameContext> options) : base(
            options)
        {
        }

        public DbSet<Player> Players { get; set; }
        public DbSet<GameState> GameStates { get; set; }

        public async Task<Player> FindPlayerByIdIncludeGameState(string id)
        {
            return await Players.Where(p => p.Id == id)
                .Include(p => p.GameState).FirstAsync();
        }

        public async Task<List<Player>> FindPlayersByGameStateOrderBySeatNumAsc(GameState gameState)
        {
            return await Players.Where(player => player.GameState == gameState)
                .OrderBy(player => player.SeatNum).ToListAsync();
        }
    }
}