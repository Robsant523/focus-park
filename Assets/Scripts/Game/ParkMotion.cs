using UnityEngine;

namespace FocusPark.Game
{
    public sealed class ParkMotion : MonoBehaviour
    {
        public Vector3 axis=Vector3.up; public float speed=10;
        void Update() { transform.Rotate(axis,speed*Time.deltaTime,Space.Self); }
    }
}
