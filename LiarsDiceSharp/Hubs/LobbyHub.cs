using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LiarsDiceSharp.LogicEngines;
using LiarsDiceSharp.Models.Contexts;
using LiarsDiceSharp.Models.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace LiarsDiceSharp.Hubs
{
    public class LobbyHub : Hub
    {
        private readonly GameContext _gameContext;
        private readonly GameLogic _gameLogic;

        public LobbyHub(GameContext gameContext, GameLogic gameLogic)
        {
            _gameContext = gameContext;
            _gameLogic = gameLogic;
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;
            var disconnectingPlayer = await _gameContext.FindPlayerByIdIncludeGameState(connectionId);
            if (disconnectingPlayer != null)
            {
                var gameState = disconnectingPlayer.GameState;
                var roomCode = gameState.RoomCode;
                await _gameLogic.DropPlayer(disconnectingPlayer, connectionId);
                await _gameContext.SaveChangesAsync();
                if (await _gameContext.GameStates.FindAsync(roomCode) != null)
                {
                    var disconnectingLoser = new PlayerDto(disconnectingPlayer);
                    await Clients.Group(roomCode).SendAsync("YouLost", disconnectingLoser);
                    await _gameLogic.ResetGameState(roomCode);
                    await _gameContext.SaveChangesAsync();
                    gameState = await _gameContext.GameStates.FindAsync(roomCode);
                    var playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                    var playerListAndGameState = new Dictionary<string, object>
                    {
                        {"playerList", playerDtos}, {"gameState", new GameStateDto(gameState, _gameContext.Players)}
                    };
                    await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                }
            }

            await Console.Out.WriteLineAsync("Disconnect event [ConnectionId:" + connectionId + "]");
        }

        public async Task MyPlayer()
        {
            var myPlayerId = Context.ConnectionId;
            var myPlayer = await _gameContext.Players.FindAsync(myPlayerId);
            if (myPlayer != null)
            {
                var player = new PlayerDtoSansGameState(myPlayer);
                await Console.Out.WriteLineAsync("Returning Player Object");
                await Clients.Caller.SendAsync("GetDiceBack", player);
            }

            await Clients.Caller.SendAsync("ErrorFromServer",
                "Your Player Could Not Be Found - Likely Because You Did Not Enter A Valid Roomcode");
        }

        public async Task JoinGame(Dictionary<string, string> nameAndRoomCode)
        {
            var name = nameAndRoomCode["name"];
            var roomCode = nameAndRoomCode["roomCode"];
            var connectionId = Context.ConnectionId;
            if (roomCode.Equals("undefined"))
            {
                roomCode = await _gameLogic.CreateNewGame(name, connectionId);
            }
            else
            {
                roomCode = roomCode.ToUpper();
                await _gameLogic.AddPlayer(name, roomCode, connectionId);
            }

            await _gameContext.SaveChangesAsync();
            await Groups.AddToGroupAsync(connectionId, roomCode);
        }

        public async Task GameState()
        {
            var id = Context.ConnectionId;
            var playerJoiningGame =
                await _gameContext.FindPlayerByIdIncludeGameState(id);
            if (playerJoiningGame == null)
            {
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                var gameState = playerJoiningGame.GameState;
                var roomCode = gameState.RoomCode;
                var playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                var playerListAndGameState = new Dictionary<string, object>
                {
                    {"playerList", playerDtos},
                    {
                        "gameState",
                        new GameStateDto(await _gameContext.GameStates.FindAsync(roomCode), _gameContext.Players)
                    }
                };
                await Console.Out.WriteLineAsync("Joining Game");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }

        public async Task CallBluff(string roomCode)
        {
            var id = Context.ConnectionId;
            var playerCallingBluff =
                await _gameContext.FindPlayerByIdIncludeGameState(id);
            if (playerCallingBluff == null)
            {
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                var gameState = playerCallingBluff.GameState;
                if (gameState.ActivePlayerId != null)
                {
                    if (await _gameLogic.IsActivePlayer(id))
                    {
                        var loser = await _gameLogic.DetermineLoser(gameState);
                        gameState = await _gameContext.GameStates.FindAsync(gameState.RoomCode);
                        gameState.LoserId = loser.Id;
                        _gameContext.GameStates.Update(gameState);
                        var loserDto = new PlayerDto(loser);
                        await _gameLogic.ResetGameState(roomCode);
                        await _gameContext.SaveChangesAsync();
                        await Console.Out.WriteLineAsync("Calling Bluff");
                        await Clients.Group(roomCode).SendAsync("YouLost", loserDto);
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ErrorFromServer", "Waiting for gameContext.Players to roll dice");
                }
            }
        }

        public async Task ResetGame()
        {
            var id = Context.ConnectionId;
            var playerRequestingReset = await _gameContext.Players.FindAsync(id);
            if (playerRequestingReset == null)
            {
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                var gameState = playerRequestingReset.GameState;
                var roomCode = gameState.RoomCode;
                await _gameLogic.ResetGameState(roomCode);
                var playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                var playerListAndGameState = new Dictionary<string, object>
                {
                    {"playerList", playerDtos},
                    {
                        "gameState",
                        new GameStateDto(await _gameContext.GameStates.FindAsync(roomCode), _gameContext.Players)
                    }
                };
                await _gameContext.SaveChangesAsync();
                await Console.Out.WriteLineAsync("Resetting Game");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }

        public async Task SetStake(List<short> newStake)
        {
            var id = Context.ConnectionId;
            var playerSettingStake =
                await _gameContext.FindPlayerByIdIncludeGameState(id);
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
                        var gameState = playerSettingStake.GameState;
                        var roomCode = gameState.RoomCode;
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
                                await _gameLogic.SetNextActivePlayer(roomCode);
                                playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                                playerListAndGameState = new Dictionary<string, Object>();
                                playerListAndGameState.Add("playerList", playerDtos);
                                playerListAndGameState.Add("gameState",
                                    new GameStateDto(await _gameContext.GameStates.FindAsync(roomCode),
                                        _gameContext.Players));
                                await Clients.Caller.SendAsync("ErrorFromServer",
                                    "You must have Dice in order to raise the stake, your turn is being skipped");
                                await Console.Out.WriteLineAsync("Setting Stake");
                                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                            }

                            if (await _gameLogic.IsActivePlayer(id) &&
                                await _gameLogic.IsValidRaise(gameState, newStake))
                            {
                                playerSettingStake.Stake = JsonSerializer.Serialize(newStake);
                                _gameContext.Players.Update(playerSettingStake);
                                await _gameLogic.SetNextActivePlayer(roomCode);
                            }
                            else if (await _gameLogic.IsActivePlayer(id))
                            {
                                await Clients.Caller.SendAsync("ErrorFromServer",
                                    "You are the active player, but you did not submit a valid stake");
                            }
                            else if (await _gameLogic.IsValidRaise(gameState, newStake))
                            {
                                await Clients.Caller.SendAsync("ErrorFromServer",
                                    "The stake is valid, but you are not the active player");
                            }

                            await _gameContext.SaveChangesAsync();
                            playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                            playerListAndGameState = new Dictionary<string, object>
                            {
                                {"playerList", playerDtos},
                                {
                                    "gameState", new GameStateDto(await _gameContext.GameStates.FindAsync(roomCode),
                                        _gameContext.Players)
                                }
                            };
                            await Clients.Caller.SendAsync("GetDiceBack",
                                new PlayerDtoSansGameState(playerSettingStake));
                            await Console.Out.WriteLineAsync("Setting Stake");
                            await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
                        }
                    }
                }
            }
        }

        public async Task RollDice()
        {
            var playerRollingDice = await _gameContext.FindPlayerByIdIncludeGameState(Context.ConnectionId);
            if (playerRollingDice == null)
            {
                await Clients.Caller.SendAsync("ErrorFromServer", "Your player Id could Not be found");
            }
            else
            {
                var gameState = playerRollingDice.GameState;
                var roomCode = gameState.RoomCode;
                if (playerRollingDice.Dice == null)
                {
                    playerRollingDice.Dice = GameLogic.RollDice();
                    _gameContext.Players.Update(playerRollingDice);
                }
                else
                {
                    await Clients.Caller.SendAsync("ErrorFromServer",
                        "You already have dice. Please reset the game if you wish to get new dice");
                }

                if (await _gameLogic.AllDiceRolled(roomCode) && gameState.LoserId == null &&
                    gameState.ActivePlayerId == null)
                {
                    await _gameLogic.SetNextActivePlayer(roomCode);
                }
                else if (await _gameLogic.AllDiceRolled(roomCode) && gameState.ActivePlayerId == null)
                {
                    gameState.ActivePlayerId = gameState.LoserId;
                    _gameContext.GameStates.Update(gameState);
                }

                await _gameContext.SaveChangesAsync();
                var playerDtos = new PlayersDto(await _gameContext.FindPlayersByGameStateOrderBySeatNumAsc(gameState));
                var playerListAndGameState = new Dictionary<string, object>
                {
                    {"playerList", playerDtos},
                    {
                        "gameState",
                        new GameStateDto(await _gameContext.GameStates.FindAsync(roomCode), _gameContext.Players)
                    }
                };
                await Clients.Caller.SendAsync("GetDiceBack", new PlayerDtoSansGameState(playerRollingDice));
                await Console.Out.WriteLineAsync("Rolling Dice");
                await Clients.Group(roomCode).SendAsync("PlayerList", playerListAndGameState);
            }
        }
    }
}