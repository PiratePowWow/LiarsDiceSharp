using System.Collections;
using System.Collections.Generic;

namespace LiarsDiceSharp.Models.Dtos
{
    public class GameDto
    {
        public ICollection<PlayerDto> PlayerDtos { get; set; }
        public GameStateDto GameState { get; set; }
    }
}