using System;
using System.Collections.Generic;
using System.Text.Json;
using LiarsDiceSharp.Models.Entities;

namespace LiarsDiceSharp.Models.Dtos
{
    public class PlayerDtoSansGameState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RoomCode { get; set; }
        public ICollection<int> Dice { get; set; }
        public ICollection<short> Stake { get; set; }
        public int Score { get; set; }
        public int SeatNum { get; set; }
        
        public PlayerDtoSansGameState(Player player)
        {
            Id = player.Id;
            Name = player.Name;
            RoomCode = player.GameState?.RoomCode;
            Dice = player.Dice != null ? JsonSerializer.Deserialize<ICollection<int>>(player.Dice) : null;
            Stake = player.Stake != null ? JsonSerializer.Deserialize<ICollection<short>>(player.Stake) : null;
            Score = player.Score;
            SeatNum = player.SeatNum;
        }
    }
}