using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>Bolsa de carregadores no colete: tira um carregador e aparece outro (até acabarem os de reserva).</summary>
    [RequireComponent(typeof(FilteredSocket))]
    public class AmmoPouch : MonoBehaviour
    {
        public Magazine magazinePrefab;
        public int spareMagazines = 4;
        public float refillDelay = 0.6f;

        FilteredSocket socket;
        float nextTime;
        Magazine pending;
        float pendingTime;

        void Awake() => socket = GetComponent<FilteredSocket>();

        void Start() => nextTime = Time.time + 0.3f;

        void Update()
        {
            if (magazinePrefab == null || spareMagazines <= 0) return;
            if (socket.hasSelection)
            {
                pending = null;
                nextTime = Time.time + refillDelay;
                return;
            }
            if (pending != null && Time.time < pendingTime + 1f) return;
            if (Time.time < nextTime) return;

            var at = socket.attachTransform ? socket.attachTransform : transform;
            pending = Instantiate(magazinePrefab, at.position, at.rotation);
            pendingTime = Time.time;
            spareMagazines--;
        }
    }
}
