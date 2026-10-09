using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Carregador físico com balas contadas. Quando está metido na arma, a mão não o consegue
    /// arrancar: tens de carregar no botão de largar carregador (A/X), como numa arma real.
    /// </summary>
    public class Magazine : XRGrabInteractable
    {
        [Header("Carregador")]
        public string magazineType = "Glock17";
        public int capacity = 17;
        public int rounds = 17;
        [Tooltip("Bala de cima, visível quando há munição")]
        public GameObject topRoundVisual;

        public bool IsInserted
        {
            get
            {
                if (!isSelected) return false;
                foreach (var i in interactorsSelecting)
                    if (i is MagazineWell) return true;
                return false;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            RefreshVisual();
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (IsInserted && !(interactor is MagazineWell)) return false;
            return base.IsSelectableBy(interactor);
        }

        public bool TryTakeRound()
        {
            if (rounds <= 0) return false;
            rounds--;
            RefreshVisual();
            return true;
        }

        void RefreshVisual()
        {
            if (topRoundVisual) topRoundVisual.SetActive(rounds > 0);
        }
    }
}
