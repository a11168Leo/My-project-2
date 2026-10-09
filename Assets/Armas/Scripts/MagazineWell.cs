using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Encaixe do carregador. Só aceita o carregador certo e mais ou menos alinhado
    /// (não entra de lado). Basta empurrar o carregador para dentro: ele "tranca" e sai da mão.
    /// </summary>
    public class MagazineWell : XRSocketInteractor
    {
        [Header("Carregador")]
        public string magazineType = "Glock17";
        public Firearm firearm;
        [Tooltip("Ângulo máximo entre o carregador e o encaixe para entrar")]
        public float maxInsertAngle = 40f;

        float blockedUntil;

        public Magazine CurrentMagazine
        {
            get
            {
                if (!hasSelection) return null;
                return interactablesSelected[0].transform.GetComponent<Magazine>();
            }
        }

        bool Accepts(IXRInteractable interactable)
        {
            if (Time.time < blockedUntil) return false;
            var m = interactable.transform.GetComponent<Magazine>();
            if (m == null || m.magazineType != magazineType) return false;
            // No arranque do nível (carregador inicial) não verifica o ângulo
            if (Time.timeSinceLevelLoad < 1f || IsSelecting(interactable as IXRSelectInteractable)) return true;
            var at = attachTransform ? attachTransform : transform;
            return Vector3.Angle(m.transform.up, at.up) <= maxInsertAngle;
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && Accepts(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            if (!Accepts(interactable)) return false;
            if (hasSelection) return IsSelecting(interactable);
            // Ao contrário de um socket normal, aceita o carregador mesmo que a mão ainda o esteja a segurar
            return socketActive && isActiveAndEnabled && interactable.IsSelectableBy(this);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            SetIgnore(args.interactableObject.transform, true);
            WeaponAudio.PlayAt(WeaponAudio.MagIn(), transform.position, 0.7f);
            if (firearm) WeaponUtils.Haptic(firearm.PrimaryHand, 0.35f, 0.04f);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (isActiveAndEnabled) StartCoroutine(RestoreCollisions(args.interactableObject.transform));
        }

        IEnumerator RestoreCollisions(Transform mag)
        {
            yield return new WaitForSeconds(0.6f);
            if (mag) SetIgnore(mag, false);
        }

        void SetIgnore(Transform mag, bool ignore)
        {
            if (!firearm || !mag) return;
            var magCols = mag.GetComponentsInChildren<Collider>();
            foreach (var a in firearm.OwnColliders)
            {
                if (!a || a.isTrigger) continue;
                foreach (var b in magCols)
                    if (b && !b.isTrigger) Physics.IgnoreCollision(a, b, ignore);
            }
        }

        /// <summary>Larga o carregador (botão A/X).</summary>
        public void Eject(Vector3 gunVelocity)
        {
            if (!hasSelection) return;
            var sel = interactablesSelected[0];
            blockedUntil = Time.time + 0.7f;
            interactionManager.SelectExit(this, sel);
            var rb = sel.transform.GetComponent<Rigidbody>();
            if (rb && !rb.isKinematic)
            {
                var at = attachTransform ? attachTransform : transform;
                rb.linearVelocity = gunVelocity - at.up * 1.2f;
            }
            WeaponAudio.PlayAt(WeaponAudio.MagOut(), transform.position, 0.6f);
        }
    }
}
