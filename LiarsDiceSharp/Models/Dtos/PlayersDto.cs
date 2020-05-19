using System.Collections;
using System.Collections.Generic;
using LiarsDiceSharp.Models.Entities;

namespace LiarsDiceSharp.Models.Dtos
{
    public class PlayersDto
    {
        public PlayersDto(List<Player> players)
        {
            PlayerDtos = players;
        }

        public List<Player> PlayerDtos { get; set; }
    }
}