

using LiarsDiceSharp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiarsDiceSharp.Models.Dtos
{
    public class GameStateDto
    {
        public GameStateDto(GameState gameState, DbSet<Player> players)
        {
            RoomCode = gameState.RoomCode;
            ActivePlayerSeatNum = gameState.ActivePlayerId == null ? null : players.Find(gameState.ActivePlayerId)?.SeatNum;
            LastPlayerSeatNum = gameState.LastPlayerId == null ? null : players.Find(gameState.LastPlayerId)?.SeatNum;
        }

        public string RoomCode { get; set; }
        public int? ActivePlayerSeatNum { get; set; }
        public int? LastPlayerSeatNum { get; set; }
    }
}