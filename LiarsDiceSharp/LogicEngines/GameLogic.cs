using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LiarsDiceSharp.Models.Contexts;
using LiarsDiceSharp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace LiarsDiceSharp.LogicEngines
{
    public class GameLogic
    {
        private readonly GameContext gameContext;

        public GameLogic(GameContext gameContext)
        {
            this.gameContext = gameContext;
            gameContext.GameStates = gameContext.GameStates;
        }

        public string rollDice()
        {
            List<Int32> dice = new List<Int32>();
            int i;
            for (i = 0; i < 5; i++)
            {
                Random r = new Random();
                dice.Add(r.Next(6) + 1);
            }
            
            return JsonSerializer.Serialize(dice);
    }

    public async Task<Player> determineLoser(GameState gameState) {
        List<Int32> allDice = new List<Int32>();
        ICollection<Player> playersInGame = await gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
        Player loser;
        foreach (Player player in playersInGame)
        {
            var playerDice = JsonSerializer.Deserialize<ICollection<int>>(player.Dice);
            allDice.AddRange(playerDice);
        }
        var stake = JsonSerializer.Deserialize<ICollection<Int16>>((await gameContext.Players.FindAsync(gameState.LastPlayerId)).Stake);
        int count = 0;
        foreach (int die in allDice){
           if (die == stake.ElementAt(1) || die.Equals(1)){
               count += 1;
           }
        }
        if (count < stake.ElementAt(0)){
            loser = await gameContext.Players.FindAsync(gameState.LastPlayerId);
            loser.Score += 1;
            return gameContext.Players.Update(loser).Entity;
        }
        loser = await gameContext.Players.FindAsync(gameState.ActivePlayerId);
        loser.Score += 1;
        return gameContext.Players.Update(loser).Entity;
    }

    public async Task<bool> isValidRaise(GameState gameState, ICollection<Int16> newStake){
        if (gameState.LastPlayerId == null){
            return true;
        }
        ICollection<Int16> oldStake = JsonSerializer.Deserialize<ICollection<Int16>>((await gameContext.Players.FindAsync(gameState.LastPlayerId)).Stake);
        if (newStake != null && newStake.ElementAt(0) > 0 && newStake.ElementAt(1) > 0 && newStake.ElementAt(1) < 7 && newStake.Count == 2) {
            if (oldStake == null) {
                return true;
            } else if (newStake.ElementAt(0) > oldStake.ElementAt(0)) {
                return true;
            } else if (newStake.ElementAt(0).Equals(oldStake.ElementAt(0))  && newStake.ElementAt(1) > oldStake.ElementAt(1)) {
                return true;
            }else{
                return false;
            }
        }
        return false;
    }

    public async Task setNextActivePlayer(string roomCode) {
        GameState gameState = await gameContext.GameStates.FindAsync(roomCode);
        string activePlayer = gameState.ActivePlayerId;
        
        ICollection<Player> playersInGame = await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync();
        if (activePlayer == null) {
            gameState.ActivePlayerId = playersInGame.ElementAt(0).Id;
            gameContext.GameStates.Update(gameState);
        }
        else{
            gameState.LastPlayerId = gameState.ActivePlayerId;
            int nextIndex = playersInGame.IndexOf(await gameContext.Players.FindAsync(gameState.LastPlayerId));
            ICollection<Player> playersInGameOrderedBySeatNum = await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync();
            if (nextIndex + 1 >= playersInGameOrderedBySeatNum.Count){
                nextIndex = -1;
            }
            while(playersInGameOrderedBySeatNum.ElementAt(nextIndex + 1).Dice == null){
                nextIndex++;
                if (nextIndex + 1 >= playersInGameOrderedBySeatNum.Count){
                    nextIndex = 0;
                }
            }

            gameState.ActivePlayerId = playersInGameOrderedBySeatNum.ElementAt(nextIndex + 1).Id;
            gameContext.GameStates.Update(gameState);
        }
        await Console.Out.WriteAsync("Setting Active Player");
    }

    public async Task<string> makeRoomCode(){
        bool isPreExistingCode = true;
        string roomCode = "";
        while (isPreExistingCode) {
            string roomCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            StringBuilder code = new StringBuilder();
            Random rnd = new Random();
            while (code.Length < 4) {
                int index = (rnd.Next(0, roomCodeChars.Length));
                code.Append(roomCodeChars[index]);
            }
            roomCode = code.ToString();
            if (await gameContext.GameStates.FindAsync(roomCode) == null) {
                isPreExistingCode = false;
            }
        }
        return roomCode;
    }

    public async Task resetGameState(string roomCode) {
        GameState gameState = await gameContext.GameStates.FindAsync(roomCode);
        gameState.ActivePlayerId = null;
        gameState.LastPlayerId = null;
        gameContext.GameStates.Update(gameState);
        ICollection<Player> playersInGame = await gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
        foreach (Player player in playersInGame) {
            player.Dice = null;
            player.Stake = null;
            gameContext.Players.Update(player);
        }
    }
 
    public async Task<string> createNewGame(string name, string id){
        string roomCode = await makeRoomCode();
        GameState gameState = new GameState(roomCode);
        gameState.Players.Add(new Player
        {
            Id = id,
            Name = name,
            SeatNum = 1
        });
        await gameContext.AddAsync(gameState);
        return roomCode;
    }

    public async Task addPlayer(string name, string roomCode, string id) {
        GameState gameState = await gameContext.GameStates.FindAsync(roomCode);
        ICollection<Player> playersInGame = await gameContext.Players.Where(player => player.GameState == gameState).OrderBy(player => player.SeatNum).ToListAsync();
        gameState.Players.Add(new Player
        {
            Id = id,
            Name = name,
            SeatNum = determineSeatNum(playersInGame)
        });
        gameContext.Update(gameState);
    }

    public int determineSeatNum(ICollection<Player> playersInGame){
        int seatNum = 0;
        int i = 1;
        foreach (Player player in playersInGame){
            if (player.SeatNum != i){
                break;
            }
            i++;
        }
        seatNum = i;
        return seatNum;
    }

    public async Task dropPlayer(Player disconnectingPlayer, string id){
        GameState gameState = (await gameContext.Players.FindAsync(id)).GameState;
        if(await gameContext.Players.CountAsync(player => player.GameState == gameState) == 1){
            gameContext.GameStates.Remove(gameState);
        }else
        {
            gameContext.Players.Remove(disconnectingPlayer);
        }
    }

    public async Task setStake(string id, ICollection<Int16> newStake) {
        Player player = await gameContext.Players.FindAsync(id);
        player.Stake = JsonSerializer.Serialize(newStake);
        gameContext.Players.Update(player);
    }

    public async Task<bool> isActivePlayer(string id){
        Player player = await gameContext.Players.FindAsync(id);
        GameState gameState = player.GameState;
        string activePlayer = gameState.ActivePlayerId;
        if (activePlayer == null){
            return false;
        }
        return id.Equals(activePlayer);
    }

    public async Task<bool> allDiceRolled(string roomCode){
        GameState gameState = await gameContext.GameStates.FindAsync(roomCode);
        ICollection<Player> playersInGame = await gameContext.Players.Where(player => player.GameState == gameState).ToListAsync();
        return playersInGame.All(p => p.Dice != null);
    }

    public async Task setDice(string id){
        Player player = await gameContext.Players.FindAsync(id);
        player.Dice = rollDice();
        gameContext.Players.Update(player);
    }
    }
}