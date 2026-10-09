using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>Coldre / bolsa do equipamento: só aceita objetos com o HolsterItem certo. Larga a arma perto para guardar.</summary>
    public class FilteredSocket : XRSocketInteractor
    {
        [Header("Coldre")]
        public string acceptSlot = "Pistol";

        bool Accepts(IXRInteractable interactable)
        {
            var item = interactable.transform.GetComponent<HolsterItem>();
            return item != null && item.slot == acceptSlot;
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && Accepts(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && Accepts(interactable);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (Time.timeSinceLevelLoad > 1f)
                WeaponAudio.PlayAt(WeaponAudio.Holster(), transform.position, 0.5f);
        }
    }
}
