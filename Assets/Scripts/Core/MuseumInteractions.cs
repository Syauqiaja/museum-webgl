using System;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>An exhibit that can be set off by another visitor, as relayed by the museum room.</summary>
    public interface IRemoteInteractable
    {
        /// <summary>Plays what another visitor set off. <paramref name="index"/> is the petak for the engklek court, 0 otherwise.</summary>
        void PlayRemote(int index);
    }

    /// <summary>
    /// Where the museum's shared exhibits meet the network, without Core referencing it. A station
    /// reports what the local visitor set off (<see cref="ReportLocal"/>); <c>MuseumPresence</c>
    /// forwards that to the room as <c>interact</c>, and hands every <c>interacted</c> it receives
    /// back through <see cref="PlayRemote"/> to the station registered under that id.
    /// </summary>
    /// <remarks>
    /// Only the exhibits it is safe to share are listed, and the ids are the server's
    /// <c>MUSEUM_STATIONS</c> (src/rooms/museumStations.ts) — change both together. Video screens
    /// and lesson plaques are deliberately not here: someone walking up to a screen, or paging a
    /// plaque, would start, stop or turn it for everyone else in front of it.
    /// </remarks>
    public static class MuseumInteractions
    {
        public const string Gong = "gong";
        public const string Gasing = "gasing";
        public const string Tembang = "tembang";
        public const string Engklek = "engklek";

        private static readonly Dictionary<string, IRemoteInteractable> Stations = new Dictionary<string, IRemoteInteractable>();

        /// <summary>The local visitor set off <c>(station, index)</c>. Raised after the station has played it.</summary>
        public static event Action<string, int> LocalInteracted;

        public static void Register(string station, IRemoteInteractable target)
        {
            if (string.IsNullOrEmpty(station) || target == null) return;
            Stations[station] = target;
        }

        /// <summary>Removes <paramref name="target"/> — only if it is still the one registered, so a newer copy is not unhooked by an older one's teardown.</summary>
        public static void Unregister(string station, IRemoteInteractable target)
        {
            if (station != null && Stations.TryGetValue(station, out IRemoteInteractable current) && current == target)
            {
                Stations.Remove(station);
            }
        }

        public static void ReportLocal(string station, int index) => LocalInteracted?.Invoke(station, index);

        /// <summary>Plays what another visitor set off. False when no station has that id (another scene, or a newer server).</summary>
        public static bool PlayRemote(string station, int index)
        {
            if (station == null || !Stations.TryGetValue(station, out IRemoteInteractable target)) return false;

            // A destroyed MonoBehaviour compares equal to null through UnityEngine.Object only.
            if (target is UnityEngine.Object unityObject && unityObject == null)
            {
                Stations.Remove(station);
                return false;
            }

            target.PlayRemote(index);
            return true;
        }

        /// <summary>Play mode runs without a domain reload, so statics outlive a Play; start each one empty.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Stations.Clear();
            LocalInteracted = null;
        }
    }
}
