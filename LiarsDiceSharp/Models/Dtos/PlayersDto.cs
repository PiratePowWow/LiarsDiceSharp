using System.Collections.Generic;
using LiarsDiceSharp.Models.Entities;

namespace LiarsDiceSharp.Models.Dtos
{
    public class PlayersDto
    {
        public List<Player> PlayerDtos { get; set; }

        public PlayersDto(List<Player> players)
        {
            PlayerDtos = players;
        }
    }
}