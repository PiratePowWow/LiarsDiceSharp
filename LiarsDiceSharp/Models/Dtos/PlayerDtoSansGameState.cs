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
        public ICollection<Int32> Dice { get; set; }
        public ICollection<Int16> Stake { get; set; }
        public int Score { get; set; }
        public int SeatNum { get; set; }
        
        public PlayerDtoSansGameState(Player player)
        {
            Id = player.Id;
            Name = player.Name;
            RoomCode = player.GameState?.RoomCode;
            Dice = player.Dice != null ? JsonSerializer.Deserialize<ICollection<Int32>>(player.Dice) : null;
            Stake = player.Stake != null ? JsonSerializer.Deserialize<ICollection<Int16>>(player.Stake) : null;
            Score = player.Score;
            SeatNum = player.SeatNum;
        }
    }
}