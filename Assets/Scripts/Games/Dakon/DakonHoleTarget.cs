using UnityEngine;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// The clickable body of one hole. The hole anchors in the scene are bare, 0.05-scaled
    /// transforms that only place seeds; <see cref="DakonView"/> builds one of these per hole
    /// at runtime — an unscaled trigger sphere the size of the bowl — so a pointer ray can say
    /// *which* hole was tapped without the anchors having to change.
    ///
    /// A trigger so the seeds piling up in the bowl neither block the ray nor collide with it.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class DakonHoleTarget : MonoBehaviour
    {
        /// <summary>Ring index of the hole this body stands for.</summary>
        public int HoleIndex { get; private set; }

        public static DakonHoleTarget Create(Transform parent, Transform anchor, int holeIndex, float radius)
        {
            var go = new GameObject($"Hole Target {holeIndex}");
            go.transform.SetParent(parent, false);
            go.transform.position = anchor.position;

            var sphere = go.GetComponent<SphereCollider>();
            if (sphere == null) sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;

            var target = go.AddComponent<DakonHoleTarget>();
            target.HoleIndex = holeIndex;
            return target;
        }
    }
}
