using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
        
        [JsonIgnore]
        [Required]
        public virtual GameState GameState { get; set; }

        public Player(string id, string name, ICollection<Int32> dice, ICollection<Int16> stake, int score, int seatNum, GameState gameState)
        {
            Id = id;
            Name = name;
            Dice = JsonSerializer.Serialize(dice);
            Stake = JsonSerializer.Serialize(stake);
            Score = score;
            SeatNum = seatNum;
            GameState = gameState;
        }

        public Player()
        {
        }
    }
}