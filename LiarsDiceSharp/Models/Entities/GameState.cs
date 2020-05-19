using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LiarsDiceSharp.Models.Entities
{
    public class GameState
    {
        [Key] public string RoomCode { get; set; }
        public string ActivePlayerId { get; set; }
        public string LastPlayerId { get; set; }
        public ICollection<Player> Players { get; set; } = new List<Player>();
        public string LoserId { get; set; }

        public GameState(string roomCode)
        {
            RoomCode = roomCode;
        }
    }
}