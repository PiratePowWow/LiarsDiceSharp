using System.Collections.Generic;

namespace LiarsDiceSharp.Models.Dtos
{
    public class Stake
    {
        private string PlayerId { get; set; }
        private ICollection<int> NewStake { get; set; }
    }
}