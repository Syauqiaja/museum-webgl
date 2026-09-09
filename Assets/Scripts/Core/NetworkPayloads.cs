using System.Collections.Generic;

namespace Museum.Core
{
    /// <summary>
    /// Server → client message payloads shared across rooms. Field names/shapes must match
    /// the server contract exactly — see the server repo docs/protocol.md. Register with
    /// <c>room.OnMessage&lt;T&gt;("type", handler)</c>.
    /// </summary>
    public static class MessageType
    {
        public const string Error = "error";
        public const string PlayerJoined = "player_joined";
        public const string PlayerLeft = "player_left";
        public const string GameOver = "game_over"; // Dakon
    }

    /// <summary>Shared error codes (server protocol.md "shared conventions"). Not exhaustive — per-game codes exist too.</summary>
    public static class ErrorCode
    {
        public const string InvalidMove = "invalid_move";
        public const string NotYourTurn = "not_your_turn";
        public const string RoomFull = "room_full";
        public const string RoomNotFound = "room_not_found";
    }

    /// <summary><c>error</c> — { code, message }.</summary>
    public class ErrorPayload
    {
        public string code;
        public string message;
    }

    /// <summary><c>player_joined</c> / <c>player_left</c> — { sessionId }.</summary>
    public class PlayerPresencePayload
    {
        public string sessionId;
    }

    /// <summary><c>game_over</c> (Dakon) — { scores{sessionId:int}, winner:string|null } (null = tie).</summary>
    public class GameOverPayload
    {
        public Dictionary<string, int> scores;
        public string winner;
    }
}
