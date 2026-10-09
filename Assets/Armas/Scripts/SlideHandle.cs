using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Slide da pistola ou alavanca de carregar da espingarda.
    /// Agarra com a outra mão e puxa para trás até ao fim: ejeta a bala da câmara;
    /// larga (ou empurra para a frente): mete uma bala nova do carregador.
    /// Puxar só um bocadinho e largar NÃO mete bala (como na vida real).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SlideHandle : MonoBehaviour
    {
        public Firearm firearm;
        [Tooltip("Peça que se mexe (por defeito este objeto)")]
        public Transform movingPart;
        [Tooltip("Direção de puxar, no espaço do objeto pai")]
        public Vector3 pullDirection = Vector3.back;
        public float travel = 0.05f;
        [Tooltip("Velocidade da mola a voltar (m/s)")]
        public float returnSpeed = 3.5f;
        [Tooltip("Pistola: a slide anda para trás a cada tiro. Espingarda: a alavanca fica parada")]
        public bool reciprocatesOnFire = true;
        public Transform boltVisual;
        public float boltTravel = 0.08f;
        public float cycleDuration = 0.05f;

        XRSimpleInteractable interactable;
        Transform hand;
        XRNode? handNode;
        bool held, reachedBack;
        float manual, grabStartOffset, cycleT = -1f;
        Vector3 grabStartLocal, restPos, boltRest;

        public float ManualOffset => manual;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (!movingPart) movingPart = transform;
            restPos = movingPart.localPosition;
            if (boltVisual) boltRest = boltVisual.localPosition;
        }

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject.transform;
            handNode = WeaponUtils.HandOf(hand);
            held = true;
            grabStartLocal = movingPart.parent.InverseTransformPoint(hand.position);
            grabStartOffset = VisualOffset();
            manual = grabStartOffset;
            WeaponUtils.Haptic(handNode, 0.2f, 0.03f);
        }

        void OnRelease(SelectExitEventArgs args)
        {
            held = false;
            hand = null;
            if (reachedBack)
            {
                reachedBack = false;
                if (firearm) firearm.OnHandleReturned();
            }
        }

        public void PlayCycle() => cycleT = 0f;

        float CycleOffset()
        {
            if (cycleT < 0f) return 0f;
            float p = cycleT / cycleDuration;
            return travel * (p < 0.5f ? p * 2f : (1f - p) * 2f);
        }

        float VisualOffset()
        {
            float v = manual;
            if (reciprocatesOnFire)
            {
                v = Mathf.Max(v, CycleOffset());
                if (firearm && firearm.boltLocked) v = Mathf.Max(v, travel);
            }
            return v;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (cycleT >= 0f)
            {
                cycleT += dt;
                if (cycleT >= cycleDuration) cycleT = -1f;
            }

            if (held && hand)
            {
                Vector3 local = movingPart.parent.InverseTransformPoint(hand.position);
                float delta = Vector3.Dot(local - grabStartLocal, pullDirection.normalized);
                manual = Mathf.Clamp(grabStartOffset + delta, 0f, travel);
                if (!reachedBack && manual >= travel * 0.95f)
                {
                    reachedBack = true;
                    if (firearm) firearm.OnHandlePulledBack();
                    WeaponUtils.Haptic(handNode, 0.5f, 0.04f);
                }
                else if (reachedBack && manual <= travel * 0.2f)
                {
                    reachedBack = false;
                    if (firearm) firearm.OnHandleReturned();
                    WeaponUtils.Haptic(handNode, 0.4f, 0.04f);
                }
            }
            else if (manual > 0f)
            {
                manual = Mathf.MoveTowards(manual, 0f, returnSpeed * dt);
            }

            movingPart.localPosition = restPos + pullDirection.normalized * VisualOffset();

            if (boltVisual)
            {
                float b = Mathf.Max(manual, CycleOffset()) / Mathf.Max(travel, 1e-4f);
                if (firearm && firearm.boltLocked) b = 1f;
                boltVisual.localPosition = boltRest + pullDirection.normalized * (Mathf.Clamp01(b) * boltTravel);
            }
        }
    }
}
