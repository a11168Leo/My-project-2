using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Diz de que material é um objeto, para a bala deixar o buraco e o efeito certos
    /// (papel = furo limpo, aço = faíscas + "ding", madeira = lascas, terra/betão = poeira).
    /// </summary>
    public class SurfaceImpact : MonoBehaviour
    {
        public enum Kind { Terra, Betao, Madeira, Metal, Papel }
        public Kind kind = Kind.Terra;
        public GameObject holePrefab;
        public GameObject fxPrefab;
        [Tooltip("Multiplica o tamanho do buraco")]
        public float holeScale = 1f;
    }
}
