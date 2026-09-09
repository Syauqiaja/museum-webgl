using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Single source of the Colyseus server endpoint. Referenced by
    /// <see cref="ColyseusNetManager"/> — never hardcode the URL in scene objects.
    /// Create via: Assets → Create → Museum → Server Config.
    /// </summary>
    /// <remarks>
    /// Production must be <c>wss://</c> (secure): the WebGL client is served over
    /// <c>https://</c> and browsers block mixed-content WebSocket connections.
    /// See Assets/Docs/networking.md §2.
    /// </remarks>
    [CreateAssetMenu(menuName = "Museum/Server Config", fileName = "ServerConfig")]
    public class ServerConfig : ScriptableObject
    {
        [Tooltip("Dev endpoint — matches the server's `npm start` (ws://localhost:2567).")]
        public string devEndpoint = "ws://localhost:2567";

        [Tooltip("Production endpoint — must be wss:// (secure). Domain TBD.")]
        public string prodEndpoint = "wss://your-domain.example";

        [Tooltip("Use the dev endpoint. Turn off for production builds.")]
        public bool useDevEndpoint = true;

        /// <summary>Endpoint the client should connect to given the current toggle.</summary>
        public string Endpoint => useDevEndpoint ? devEndpoint : prodEndpoint;
    }
}
