using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LiarsDiceSharp.Models.Entities
{
    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Dice { get; set; }
        public string Stake { get; set; }
        public int Score { get; set; } = 0;
        public int SeatNum { get; set; }

        [JsonIgnore] [Required] public virtual GameState GameState { get; set; }
    }
}