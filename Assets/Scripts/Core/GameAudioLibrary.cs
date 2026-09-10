using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Every sound file the project ships, in one place: the three scene tracks and the three
    /// effects from <c>Assets/Sounds</c>. Loaded by <see cref="GameAudio"/> from
    /// <c>Resources/GameAudio</c>, so no scene carries a reference that a lost inspector could
    /// drop (the project has lost its inspector values once — see CLAUDE.md).
    /// </summary>
    /// <remarks>
    /// The music clips are imported with <b>Preload Audio Data off</b> and <b>Load In Background
    /// on</b>: referenced from Resources, they would otherwise all be decoded at boot, before the
    /// first scene has even drawn. See asset-budget.md for the import settings.
    /// </remarks>
    [CreateAssetMenu(menuName = "Museum/Game Audio Library", fileName = "GameAudio")]
    public sealed class GameAudioLibrary : ScriptableObject
    {
        [Header("Scene music")]
        [Tooltip("MainMenu and the Museum; the Lobby too when it was not opened from a game's doorway.")]
        public AudioClip museumMusic;

        [Tooltip("The Dakon scene, and the Lobby on the way to it.")]
        public AudioClip dakonMusic;

        [Tooltip("The Egrang scene, and the Lobby on the way to it.")]
        public AudioClip egrangMusic;

        [Header("Effects")]
        public AudioClip buttonClick;
        public AudioClip buttonHover;

        [Tooltip("Played when the local player wins a game.")]
        public AudioClip win;

        [Header("Levels")]
        [Range(0f, 1f)] public float musicVolume = 0.45f;
        [Range(0f, 1f)] public float uiVolume = 0.7f;
        [Range(0f, 1f)] public float winVolume = 0.9f;

        /// <summary>The clip for <paramref name="track"/>; null for <see cref="MusicTrack.None"/>.</summary>
        public AudioClip ClipFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Museum: return museumMusic;
                case MusicTrack.Dakon: return dakonMusic;
                case MusicTrack.Egrang: return egrangMusic;
                default: return null;
            }
        }
    }
}
