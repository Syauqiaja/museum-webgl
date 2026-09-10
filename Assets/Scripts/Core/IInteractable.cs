namespace Museum.Core
{
    /// <summary>
    /// Something the visitor can stand at and trigger with the Interaksi button or the Enter
    /// key: the lobby's gong, the gasing, the song stage. Doorways are not this — they have
    /// their own <see cref="SceneTriggerPrompt"/> and take precedence in the router.
    /// </summary>
    public interface IInteractable
    {
        void Interact();
    }
}
