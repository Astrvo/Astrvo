using UnityEngine;

namespace Astrvo.Space
{
    public class LookAtCamera : MonoBehaviour
    {
        [SerializeField] private GameObject cam;

        private void Update()
        {
            transform.LookAt(cam.transform);
        }
    }
}
