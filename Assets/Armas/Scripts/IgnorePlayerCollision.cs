using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>Evita que as armas/carregadores no corpo do jogador choquem com o CharacterController (prendia o movimento).</summary>
    public class IgnorePlayerCollision : MonoBehaviour
    {
        void Start()
        {
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var cc in FindObjectsByType<CharacterController>())
                foreach (var c in mine)
                    if (c && !c.isTrigger) Physics.IgnoreCollision(cc, c, true);
        }
    }
}
