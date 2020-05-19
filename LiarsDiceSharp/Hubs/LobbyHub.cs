using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LiarsDiceSharp.LogicEngines;
using LiarsDiceSharp.Models.Contexts;
using LiarsDiceSharp.Models.Dtos;
using LiarsDiceSharp.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiarsDiceSharp.Hubs
{
    public class LobbyHub : Hub
    {
        private readonly GameContext gameContext;
        private readonly GameLogic _gameLogic;

        public LobbyHub(GameContext gameContext, GameLogic gameLogic)
        {
            this.gameContext = gameContext;
            _gameLogic = gameLogic;
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string connectionId = Context.ConnectionId;
            Player disconnectingPlayer = await gameContext.Players.Where(p => p.Id == connectionId).Include(p => p.GameState).FirstAsync();
            if (disconnectingPlayer != null) {
                GameState gameState = disconnectingPlayer.GameState;
                String roomCode = gameState.RoomCode;
                await _gameLogic.dropPlayer(disconnectingPlayer, connectionId);
                await gameContext.SaveChangesAsync();
                if (await gameContext.GameStates.FindAsync(roomCode) != null) {
                    PlayerDto disconnectingLoser = new PlayerDto(disconnectingPlayer);
                    await Clients.Group(roomCode).SendAsync("YouLost", disconnectingLoser);
                    await _gameLogic.resetGameState(roomCode);
                    await gameContext.SaveChangesAsync();
                    gameState = await gameContext.GameStates.FindAsync(roomCode);
                    PlayersDto playerDtos = new PlayersDto(gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToList());
                    Dictionary<string, object> playerListAndGameState = new Dictionary<string, object>();
                    playerListAndGameState.Add("playerList", playerDtos);
                    playerListAndGameState.Add("gameState", new GameStateDto(gameState, gameContext.Players));
                    await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                }
            }
            await Console.Out.WriteAsync("Disconnect event [ConnectionId:" + connectionId + "]");
        }

        public async Task MyPlayer()
        {
            string myPlayerId = Context.ConnectionId;
            Player myPlayer = await gameContext.Players.FindAsync(myPlayerId);
            if (myPlayer != null)
            {
                PlayerDtoSansGameState player = new PlayerDtoSansGameState(myPlayer);
                await Console.Out.WriteAsync("Returning Player Object");
                await Clients.Caller.SendAsync("GetDiceBack", player);
            }
            await Clients.Caller.SendAsync("ErrorFromServer", "Your Player Could Not Be Found - Likely Because You Did Not Enter A Valid Roomcode");
        }

        public async Task JoinGame(Dictionary<string, string> nameAndRoomCode)
        {
            string name = nameAndRoomCode["name"];
            string roomCode = nameAndRoomCode["roomCode"];
            string connectionId = Context.ConnectionId;
            if(roomCode.Equals("undefined")){
                roomCode = await _gameLogic.createNewGame(name, connectionId);
            }else
            {
                roomCode = roomCode.ToUpper();
                await _gameLogic.addPlayer(name, roomCode, connectionId);
            }

            await gameContext.SaveChangesAsync();
            await Groups.AddToGroupAsync(connectionId, roomCode);
        }

        public async Task GameState()
        {
            string id = Context.ConnectionId;
            Player playerJoiningGame = await gameContext.Players.Where(p => p.Id == id).Include(p => p.GameState).FirstAsync();
            if (playerJoiningGame == null)
            {
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                GameState gameState = playerJoiningGame.GameState;
                string roomCode = gameState.RoomCode;
                PlayersDto playerDtos = new PlayersDto(await gameContext.Players.Where(player => player.GameState == gameState)
                    .OrderBy(player => player.SeatNum).ToListAsync());
                Dictionary<string, Object> playerListAndGameState = new Dictionary<string, Object>();
                playerListAndGameState.Add("playerList", playerDtos);
                playerListAndGameState.Add("gameState",
                    new GameStateDto(await gameContext.GameStates.FindAsync(roomCode), gameContext.Players));
                await Console.Out.WriteAsync("Joining Game");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }

        public async Task CallBluff(string roomCode)
        {
            string id = Context.ConnectionId;
            Player playerCallingBluff = await gameContext.Players.Include(p => p.GameState).Where(p => p.Id == id).FirstAsync();
            if (playerCallingBluff == null)
            {
               await Clients.Caller.SendAsync("ErrorFromServer","Your player Id could Not be found");
            }
            else
            {
                GameState gameState = playerCallingBluff.GameState;
                if (gameState.ActivePlayerId != null)
                {
                    PlayerDto loserDto;
                    if (await _gameLogic.isActivePlayer(id))
                    {
                        Player loser = await _gameLogic.determineLoser(gameState);
                        gameState = gameContext.GameStates.Find(gameState.RoomCode);
                        gameState.LoserId = loser.Id;
                        gameContext.GameStates.Update(gameState);
                        loserDto = new PlayerDto(loser);
                        await _gameLogic.resetGameState(roomCode);
                        await gameContext.SaveChangesAsync();
                        await Console.Out.WriteAsync("Calling Bluff");
                        await Clients.Group(roomCode).SendAsync("YouLost", loserDto);
                    }

                    // gameState.LoserId = playerCallingBluff.Id;
                    // gameContext.GameStates.Update(gameState);
                    // loserDto = new PlayerDto(playerCallingBluff);
                    // await _gameLogic.resetGameState(roomCode);
                    // await gameContext.SaveChangesAsync();
                    // await Console.Out.WriteAsync("Calling Bluff");
                    // await Clients.Group(roomCode).SendAsync("YouLost", loserDto);
                }
                else
                {
                    await Clients.Caller.SendAsync("ErrorFromServer", "Waiting for gameContext.Players to roll dice");
                }
            }
        }

        public async Task ResetGame()
        {
            string id = Context.ConnectionId;
            Player playerRequestingReset = await gameContext.Players.FindAsync(id);
            if (playerRequestingReset == null)
            {
               await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                GameState gameState = playerRequestingReset.GameState;
                string roomCode = gameState.RoomCode;
                await _gameLogic.resetGameState(roomCode);
                GameState newGame = playerRequestingReset.GameState;
                PlayersDto playerDtos = new PlayersDto(await gameContext.Players.Where(player => player.GameState == gameState)
                    .OrderBy(player => player.SeatNum).ToListAsync());
                var playerListAndGameState = new Dictionary<string, Object>();
                playerListAndGameState.Add("playerList", playerDtos);
                playerListAndGameState.Add("gameState",
                    new GameStateDto(await gameContext.GameStates.FindAsync(roomCode), gameContext.Players));
                await gameContext.SaveChangesAsync();
                await Console.Out.WriteAsync("Resetting Game");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }

        public async Task SetStake(List<Int16> newStake)
        {
            string id = Context.ConnectionId;
            Player playerSettingStake = await gameContext.Players.Include(p => p.GameState).Where(p => p.Id == id).FirstAsync();
            if (newStake != null && newStake.Count == 2)
            {
                if (newStake.ElementAt(0) > 0 && newStake.ElementAt(1) > 0 && newStake.ElementAt(1) < 7 &&
                    newStake.Count == 2)
                {
                    if (playerSettingStake == null)
                    {
                        await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
                    }
                    else
                    {
                        GameState gameState = playerSettingStake.GameState;
                        string roomCode = gameState.RoomCode;
                        if (gameState.ActivePlayerId == null)
                        {
                            await Clients.Caller.SendAsync("ErrorFromServer", 
                                "No active player has been set because all gameContext.Players have not yet rolled their dice");
                        }
                        else
                        {
                            Dictionary<string, object> playerListAndGameState;
                            PlayersDto playerDtos;
                            if (playerSettingStake.Dice == null)
                            {
                                await _gameLogic.setNextActivePlayer(roomCode);
                                playerDtos = new PlayersDto(await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync());
                                playerListAndGameState = new Dictionary<string, Object>();
                                playerListAndGameState.Add("playerList", playerDtos);
                                playerListAndGameState.Add("gameState",
                                    new GameStateDto(await gameContext.GameStates.FindAsync(roomCode), gameContext.Players));
                                await Clients.Caller.SendAsync("ErrorFromServer", "You must have Dice in order to raise the stake, your turn is being skipped");
                                await Console.Out.WriteAsync("Setting Stake");
                                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                            }

                            if (await _gameLogic.isActivePlayer(id) && await _gameLogic.isValidRaise(gameState, newStake))
                            {
                                playerSettingStake.Stake = JsonSerializer.Serialize(newStake);
                                gameContext.Players.Update(playerSettingStake);
                                await _gameLogic.setNextActivePlayer(roomCode);
                            }
                            else if (await _gameLogic.isActivePlayer(id))
                            {
                                await Clients.Caller.SendAsync("ErrorFromServer", "You are the active player, but you did not submit a valid stake");
                            }
                            else if (await _gameLogic.isValidRaise(gameState, newStake))
                            {
                                await Clients.Caller.SendAsync("ErrorFromServer", "The stake is valid, but you are not the active player");
                            }
                            await gameContext.SaveChangesAsync();
                            playerDtos = new PlayersDto(await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync());
                            playerListAndGameState = new Dictionary<string, object>();
                            playerListAndGameState.Add("playerList", playerDtos);
                            playerListAndGameState.Add("gameState",
                                new GameStateDto(await gameContext.GameStates.FindAsync(roomCode), gameContext.Players));
                            await Clients.Caller.SendAsync("GetDiceBack", new PlayerDtoSansGameState(playerSettingStake));
                            await Console.Out.WriteAsync("Setting Stake");
                            await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                        }
                    }
                }
            }
        }

        public async Task RollDice()
        {
            Player playerRollingDice = await gameContext.Players.Include(p => p.GameState).Where(p => p.Id == Context.ConnectionId).FirstAsync();
            if (playerRollingDice == null){
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                GameState gameState = playerRollingDice.GameState;
                string roomCode = gameState.RoomCode;
                if (playerRollingDice.Dice == null)
                {
                    playerRollingDice.Dice = _gameLogic.rollDice();
                    gameContext.Players.Update(playerRollingDice);
                }
                else
                {
                    await Clients.Caller.SendAsync("ErrorFromServer", "You already have dice. Please reset the game if you wish to get new dice");
                }

                if (await _gameLogic.allDiceRolled(roomCode) && gameState.LoserId == null &&
                    gameState.ActivePlayerId == null)
                {
                    await _gameLogic.setNextActivePlayer(roomCode);
                }
                else if (await _gameLogic.allDiceRolled(roomCode) && gameState.ActivePlayerId == null)
                {
                    gameState.ActivePlayerId = gameState.LoserId;
                    gameContext.GameStates.Update(gameState);
                }
                await gameContext.SaveChangesAsync();
                PlayersDto playerDtos = new PlayersDto(await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync());
                Dictionary<string, object> playerListAndGameState = new Dictionary<string, object>();
                playerListAndGameState.Add("playerList", playerDtos);
                playerListAndGameState.Add("gameState", new GameStateDto(await gameContext.GameStates.FindAsync(roomCode), gameContext.Players));
                await Clients.Caller.SendAsync("GetDiceBack", new PlayerDtoSansGameState(playerRollingDice));
                await Console.Out.WriteAsync("Rolling Dice");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }
    }
}