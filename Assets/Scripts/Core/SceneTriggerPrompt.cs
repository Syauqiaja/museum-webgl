using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Museum.Core;

/// <summary>
/// A doorway inside the museum: walk into it, press Enter, and it loads what it points at.
///
/// Multiplayer games are not loaded directly. With <c>useLobby</c> on, the doorway opens the
/// Lobby scene first so the player can create or join a room, and hands it <c>sceneName</c> as
/// the destination to load once the host starts. The trigger therefore still names the game it
/// leads to — the lobby is a stop on the way, not the target.
/// </summary>
public class SceneTriggerPrompt : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject promptUI;
    [Tooltip("The label inside promptUI. Its text depends on the control scheme, so it is set at runtime.")]
    [SerializeField] private TMP_Text promptLabel;
    [Tooltip("The game scene this doorway leads to. With Use Lobby on, the lobby loads it after the host starts.")]
    [SerializeField] private string sceneName;

    [Header("Multiplayer")]
    [Tooltip("Go through the Lobby scene first so players can create or join a room.")]
    [SerializeField] private bool useLobby;
    [Tooltip("Server room name for this game — \"dakon\", \"egrang\". Must match the server's protocol.md.")]
    [SerializeField] private string roomName;
    [Tooltip("Seats in the room: Dakon 2, Egrang 3.")]
    [SerializeField] private int maxPlayers = 3;
    [Tooltip("The game's name as a player reads it on the lobby title — \"Dakon\", \"Egrang\". " +
             "Not the room name: that one is a wire identifier and never goes on screen.")]
    [SerializeField] private string displayName;

    private bool playerInside;

    /// <summary>True while the player stands in this doorway's trigger.</summary>
    public bool PlayerInside => playerInside;

    private void Awake()
    {
        if (promptUI != null) promptUI.SetActive(false);

        if (promptLabel != null)
        {
            bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
            promptLabel.text = touch ? "Ketuk Interaksi" : "Tekan Enter";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = true;
        if (promptUI != null) promptUI.SetActive(true);
        TouchInteractRouter.Register(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = false;
        if (promptUI != null) promptUI.SetActive(false);
        TouchInteractRouter.Unregister(this);
    }

    private void Update()
    {
        if (!playerInside) return;
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Enter();
        }
    }

    /// <summary>
    /// Walks through the doorway. Public because the touch overlay's Interaksi button reaches it
    /// through <see cref="TouchInteractRouter"/> — the keyboard is no longer the only way in.
    /// </summary>
    public void Enter()
    {
        if (promptUI != null) promptUI.SetActive(false);

        if (useLobby)
        {
            LobbyRequest.Pending = new LobbyRequest(roomName, maxPlayers, sceneName, displayName);
            SceneLoader.Instance.LoadScene(SceneReference.Lobby);
            return;
        }

        SceneLoader.Instance.LoadScene(sceneName);
    }
}
