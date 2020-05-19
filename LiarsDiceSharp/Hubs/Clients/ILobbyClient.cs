using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiarsDiceSharp.Models.Dtos;

namespace LiarsDiceSharp.Hubs.Clients
{
    public interface ILobbyClient
    {
        Task GetDiceBack( PlayerDtoSansGameState playerDtoSansGameState);
        Task PlayerList( Dictionary<string, Object> playerListAndGameState);
        Task YouLost( PlayerDto playerDto);
        Task ErrorFromServer( string errorMsg);
    }
}