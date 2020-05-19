using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LiarsDiceSharp.Models.Contexts;
using LiarsDiceSharp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiarsDiceSharp.LogicEngines
{
    public class GameLogic
    {
        private readonly GameContext _gameContext;

        public GameLogic(GameContext gameContext)
        {
            this._gameContext = gameContext;
        }

        public static string RollDice()
        {
            var dice = new List<int>();
            int i;
            for (i = 0; i < 5; i++)
            {
                var r = new Random();
                dice.Add(r.Next(6) + 1);
            }

            return JsonSerializer.Serialize(dice);
        }

        public async Task<Player> DetermineLoser(GameState gameState)
        {
            var allDice = new List<int>();
            ICollection<Player> playersInGame =
                await _gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
            Player loser;
            foreach (var player in playersInGame)
            {
                var playerDice = JsonSerializer.Deserialize<ICollection<int>>(player.Dice);
                allDice.AddRange(playerDice);
            }

            var stake = JsonSerializer.Deserialize<ICollection<Int16>>(
                (await _gameContext.Players.FindAsync(gameState.LastPlayerId)).Stake);
            var count = allDice.Count(die => die == stake.ElementAt(1) || die.Equals(1));

            if (count < stake.ElementAt(0))
            {
                loser = await _gameContext.Players.FindAsync(gameState.LastPlayerId);
                loser.Score += 1;
                return _gameContext.Players.Update(loser).Entity;
            }

            loser = await _gameContext.Players.FindAsync(gameState.ActivePlayerId);
            loser.Score += 1;
            return _gameContext.Players.Update(loser).Entity;
        }

        public async Task<bool> IsValidRaise(GameState gameState, ICollection<Int16> newStake)
        {
            if (gameState.LastPlayerId == null)
            {
                return true;
            }

            var oldStake =
                JsonSerializer.Deserialize<ICollection<short>>(
                    (await _gameContext.Players.FindAsync(gameState.LastPlayerId)).Stake);
            if (newStake == null || newStake.ElementAt(0) <= 0 || newStake.ElementAt(1) <= 0 ||
                newStake.ElementAt(1) >= 7 || newStake.Count != 2) return false;
            if (oldStake == null)
            {
                return true;
            }

            if (newStake.ElementAt(0) > oldStake.ElementAt(0))
            {
                return true;
            }

            return newStake.ElementAt(0).Equals(oldStake.ElementAt(0)) &&
                   newStake.ElementAt(1) > oldStake.ElementAt(1);
        }

        public async Task SetNextActivePlayer(string roomCode)
        {
            var gameState = await _gameContext.GameStates.FindAsync(roomCode);
            var activePlayer = gameState.ActivePlayerId;

            ICollection<Player> playersInGame = await _gameContext.Players
                .Where(player => player.GameState == gameState)
                .OrderBy(player => player.SeatNum).ToListAsync();
            if (activePlayer == null)
            {
                gameState.ActivePlayerId = playersInGame.ElementAt(0).Id;
                _gameContext.GameStates.Update(gameState);
            }
            else
            {
                gameState.LastPlayerId = gameState.ActivePlayerId;
                var nextIndex = playersInGame.ToList()
                    .IndexOf(await _gameContext.Players.FindAsync(gameState.LastPlayerId));
                ICollection<Player> playersInGameOrderedBySeatNum = await _gameContext.Players
                    .Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync();
                if (nextIndex + 1 >= playersInGameOrderedBySeatNum.Count)
                {
                    nextIndex = -1;
                }

                while (playersInGameOrderedBySeatNum.ElementAt(nextIndex + 1).Dice == null)
                {
                    nextIndex++;
                    if (nextIndex + 1 >= playersInGameOrderedBySeatNum.Count)
                    {
                        nextIndex = 0;
                    }
                }

                gameState.ActivePlayerId = playersInGameOrderedBySeatNum.ElementAt(nextIndex + 1).Id;
                _gameContext.GameStates.Update(gameState);
            }

            await Console.Out.WriteLineAsync("Setting Active Player");
        }

        private async Task<string> MakeRoomCode()
        {
            var isPreExistingCode = true;
            var roomCode = "";
            while (isPreExistingCode)
            {
                const string roomCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
                var code = new StringBuilder();
                var rnd = new Random();
                while (code.Length < 4)
                {
                    var index = (rnd.Next(0, roomCodeChars.Length));
                    code.Append(roomCodeChars[index]);
                }

                roomCode = code.ToString();
                if (await _gameContext.GameStates.FindAsync(roomCode) == null)
                {
                    isPreExistingCode = false;
                }
            }

            return roomCode;
        }

        public async Task ResetGameState(string roomCode)
        {
            var gameState = await _gameContext.GameStates.FindAsync(roomCode);
            gameState.ActivePlayerId = null;
            gameState.LastPlayerId = null;
            _gameContext.GameStates.Update(gameState);
            ICollection<Player> playersInGame =
                await _gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
            foreach (var player in playersInGame)
            {
                player.Dice = null;
                player.Stake = null;
                _gameContext.Players.Update(player);
            }
        }

        public async Task<string> CreateNewGame(string name, string id)
        {
            var roomCode = await MakeRoomCode();
            var gameState = new GameState(roomCode);
            gameState.Players.Add(new Player
            {
                Id = id,
                Name = name,
                SeatNum = 1
            });
            await _gameContext.AddAsync(gameState);
            return roomCode;
        }

        public async Task AddPlayer(string name, string roomCode, string id)
        {
            var gameState = await _gameContext.GameStates.FindAsync(roomCode);
            ICollection<Player> playersInGame = await _gameContext.Players
                .Where(player => player.GameState == gameState)
                .OrderBy(player => player.SeatNum).ToListAsync();
            gameState.Players.Add(new Player
            {
                Id = id,
                Name = name,
                SeatNum = DetermineSeatNum(playersInGame)
            });
            _gameContext.Update(gameState);
        }

        private static int DetermineSeatNum(IEnumerable<Player> playersInGame)
        {
            var i = 1;
            foreach (var player in playersInGame)
            {
                if (player.SeatNum != i)
                {
                    break;
                }

                i++;
            }

            return i;
        }

        public async Task DropPlayer(Player disconnectingPlayer, string id)
        {
            var gameState = (await _gameContext.Players.FindAsync(id)).GameState;
            if (await _gameContext.Players.CountAsync(player => player.GameState == gameState) == 1)
            {
                _gameContext.GameStates.Remove(gameState);
            }
            else
            {
                _gameContext.Players.Remove(disconnectingPlayer);
            }
        }

        public async Task<bool> IsActivePlayer(string id)
        {
            var player = await _gameContext.Players.FindAsync(id);
            var gameState = player.GameState;
            var activePlayer = gameState.ActivePlayerId;
            return activePlayer != null && id.Equals(activePlayer);
        }

        public async Task<bool> AllDiceRolled(string roomCode)
        {
            var gameState = await _gameContext.GameStates.FindAsync(roomCode);
            ICollection<Player> playersInGame =
                await _gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
            return playersInGame.All(p => p.Dice != null);
        }
    }
}