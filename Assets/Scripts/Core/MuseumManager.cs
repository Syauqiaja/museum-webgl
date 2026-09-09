using UnityEngine;

namespace Museum.Core
{
    public class MuseumManager : MonoBehaviour
    {
        public void GoToScene(string reference)
        {
            SceneLoader.Instance.LoadScene(reference);
        }
    }
}
