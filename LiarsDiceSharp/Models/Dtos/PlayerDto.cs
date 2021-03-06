﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using LiarsDiceSharp.Models.Entities;

namespace LiarsDiceSharp.Models.Dtos
{
    public class PlayerDto
    {
        public string Name { get; set; }
        public string RoomCode { get; set; }
        public ICollection<short> Stake { get; set; }
        public int SeatNum { get; set; }
        public int Score { get; set; } = 0;
        public bool IsDiceRolled { get; set; }

        public PlayerDto(Player player)
        {
            Name = player.Name;
            RoomCode = player.GameState?.RoomCode;
            Stake = player.Stake != null ? JsonSerializer.Deserialize<ICollection<short>>(player.Stake) : null;
            SeatNum = player.SeatNum;
            Score = player.Score;
            IsDiceRolled = player.Dice != null;
        }
    }
}