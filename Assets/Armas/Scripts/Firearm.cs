using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Arma de fogo realista:
    ///  - só dispara se houver bala na câmara (precisa de puxar a slide / alavanca de carregar)
    ///  - gatilho analógico com ponto de quebra e reset (como uma arma real)
    ///  - carregador físico, slide/ferrolho trava atrás quando o carregador fica vazio
    ///  - recuo que sobe a arma (menor com as duas mãos), cápsulas ejetadas, vibração
    /// Controlos na mão que segura o punho:
    ///  Gatilho = disparar | A/X = largar carregador | B/Y = soltar slide/ferrolho | clicar analógico = modo de tiro
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Firearm : MonoBehaviour
    {
        public enum FireMode { Safe, Semi, Auto }

        [Header("Identidade")]
        public string weaponName = "Glock 17";

        [Header("Balística")]
        public float damage = 25f;
        [Tooltip("Velocidade da bala à saída do cano (m/s). 9mm ≈ 375, 5.56 ≈ 880")]
        public float muzzleVelocity = 375f;
        public float roundsPerMinute = 1100f;
        [Tooltip("Imprecisão mecânica da arma em graus")]
        public float spreadDegrees = 0.12f;
        public float bulletHoleSize = 0.012f;

        [Header("Modos de tiro")]
        public FireMode[] fireModes = { FireMode.Semi };
        public int fireModeIndex = 0;
        public Transform selectorVisual;
        public float[] selectorAngles = { 0f, 90f, 180f };

        [Header("Gatilho")]
        [Range(0, 1)] public float triggerBreak = 0.85f;
        [Range(0, 1)] public float triggerReset = 0.45f;
        public Transform triggerVisual;
        public float triggerTravelDegrees = 14f;

        [Header("Peças")]
        public Transform muzzle;
        public Transform ejectionPort;
        public Transform recoilPivot;
        public MagazineWell magazineWell;
        public SlideHandle slide;
        [Tooltip("A slide/ferrolho fica travada atrás quando o carregador fica vazio")]
        public bool lockOpenOnEmpty = true;

        [Header("Recuo")]
        public float recoilBack = 0.025f;
        public float recoilRise = 7f;
        public float recoilYaw = 1.5f;
        public float recoilRoll = 2f;
        public float twoHandRecoilMultiplier = 0.4f;
        public float recoverTime = 0.09f;

        [Header("Efeitos")]
        public GameObject muzzleFlash;
        public Light muzzleLight;
        public GameObject casingPrefab;
        public GameObject liveRoundPrefab;
        public GameObject bulletHolePrefab;
        public GameObject impactFxPrefab;
        public float shotPitch = 1f;
        public float shotBody = 0.8f;
        public float shotVolume = 1f;

        [Header("Estado")]
        public bool roundChambered;
        public bool boltLocked;

        XRGrabInteractable grab;
        XRNode? primary, secondary;
        IXRSelectInteractor primaryInteractor;
        bool trigReady = true, dryClicked, prevA, prevB, prevStick;
        float nextShot, flashTimer;
        Vector3 rPos, rRot, rPosVel, rRotVel, pivotPos, lastPos, velocity;
        Quaternion pivotRot = Quaternion.identity, triggerBaseRot = Quaternion.identity, selectorBaseRot = Quaternion.identity;
        Collider[] ownColliders;

        public XRNode? PrimaryHand => primary;
        public bool IsHeld => primary.HasValue;
        public Magazine CurrentMagazine => magazineWell != null ? magazineWell.CurrentMagazine : null;
        public FireMode Mode => fireModes.Length > 0 ? fireModes[Mathf.Clamp(fireModeIndex, 0, fireModes.Length - 1)] : FireMode.Semi;
        public Collider[] OwnColliders => ownColliders ??= GetComponentsInChildren<Collider>(true);
        public Vector3 Velocity => velocity;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (recoilPivot) { pivotPos = recoilPivot.localPosition; pivotRot = recoilPivot.localRotation; }
            if (triggerVisual) triggerBaseRot = triggerVisual.localRotation;
            if (selectorVisual) selectorBaseRot = selectorVisual.localRotation;
            if (muzzleFlash) muzzleFlash.SetActive(false);
            if (muzzleLight) muzzleLight.enabled = false;
            lastPos = transform.position;
            UpdateSelectorVisual();
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            velocity = (transform.position - lastPos) / dt;
            lastPos = transform.position;

            ResolveHands();
            UpdateRecoil();
            UpdateFlash();

            if (!primary.HasValue)
            {
                trigReady = true;
                if (triggerVisual) triggerVisual.localRotation = triggerBaseRot;
                return;
            }

            float trig = Mode == FireMode.Safe ? 0f : ReadTrigger();
            if (triggerVisual) triggerVisual.localRotation = triggerBaseRot * Quaternion.Euler(trig * triggerTravelDegrees, 0, 0);

            if (trig <= triggerReset) { trigReady = true; dryClicked = false; }
            if (trig >= triggerBreak && Mode != FireMode.Safe)
            {
                if (Mode == FireMode.Semi && trigReady) { trigReady = false; TryFire(); }
                else if (Mode == FireMode.Auto && Time.time >= nextShot) { trigReady = false; TryFire(); }
            }

            bool a = WeaponUtils.Button(primary, WeaponUtils.BtnPrimary);
            bool b = WeaponUtils.Button(primary, WeaponUtils.BtnSecondary);
            bool s = WeaponUtils.Button(primary, WeaponUtils.BtnStick);
            if (a && !prevA) DropMagazine();
            if (b && !prevB) ReleaseBolt();
            if (s && !prevStick) CycleFireMode();
            prevA = a; prevB = b; prevStick = s;
        }

        /// <summary>Gatilho: Input System; se der 0, usa o "Activate" do próprio XR Interaction Toolkit.</summary>
        float ReadTrigger()
        {
            float t = WeaponUtils.Trigger(primary);
            if (t <= 0.001f && primaryInteractor is XRBaseInputInteractor bi)
            {
                var r = bi.activateInput;
                if (r != null) t = Mathf.Max(t, r.ReadValue());
            }
            return t;
        }

        void ResolveHands()
        {
            primary = null;
            secondary = null;
            primaryInteractor = null;
            if (!grab.isSelected) return;
            var grip = grab.attachTransform ? grab.attachTransform : transform;
            IXRSelectInteractor best = null;
            float bestD = float.MaxValue;
            foreach (var i in grab.interactorsSelecting)
            {
                if (i is XRSocketInteractor) continue;
                float d = (i.transform.position - grip.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best == null) return;
            primaryInteractor = best;
            primary = WeaponUtils.HandOf(best.transform) ?? XRNode.RightHand;
            foreach (var i in grab.interactorsSelecting)
            {
                if (i == best || i is XRSocketInteractor) continue;
                secondary = WeaponUtils.HandOf(i.transform);
            }
        }

        bool OutOfBattery => slide != null && slide.ManualOffset > 0.004f;

        void TryFire()
        {
            if (!roundChambered || boltLocked || OutOfBattery)
            {
                if (!dryClicked)
                {
                    dryClicked = true;
                    WeaponAudio.PlayAt(WeaponAudio.DryClick(), transform.position, 0.6f);
                    WeaponUtils.Haptic(primary, 0.15f, 0.02f);
                }
                return;
            }
            Fire();
        }

        void Fire()
        {
            roundChambered = false;
            nextShot = Time.time + 60f / Mathf.Max(1f, roundsPerMinute);

            Vector3 dir = muzzle.forward;
            dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * (Quaternion.AngleAxis(Random.Range(0f, spreadDegrees), muzzle.up) * dir);
            Ballistics.Fire(muzzle.position, dir * muzzleVelocity, damage, bulletHoleSize, bulletHolePrefab, impactFxPrefab);

            WeaponAudio.PlayAt(WeaponAudio.Gunshot(shotPitch, shotBody), muzzle.position, shotVolume, 0.05f, 400f);
            WeaponUtils.Haptic(primary, 1f, 0.07f);
            WeaponUtils.Haptic(secondary, 0.6f, 0.06f);

            if (muzzleFlash)
            {
                muzzleFlash.SetActive(true);
                muzzleFlash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            }
            if (muzzleLight) muzzleLight.enabled = true;
            flashTimer = 0.035f;

            AddRecoil();
            Eject(casingPrefab);

            var mag = CurrentMagazine;
            if (mag != null && mag.TryTakeRound()) roundChambered = true;
            else if (lockOpenOnEmpty && mag != null) boltLocked = true;
            if (slide) slide.PlayCycle();
        }

        void AddRecoil()
        {
            float m = secondary.HasValue ? twoHandRecoilMultiplier : 1f;
            rPos += new Vector3(0f, recoilBack * 0.15f, -recoilBack) * m;
            rRot += new Vector3(-recoilRise * Random.Range(0.85f, 1.15f), Random.Range(-recoilYaw, recoilYaw), Random.Range(-recoilRoll, recoilRoll)) * m;
            rRot.x = Mathf.Max(rRot.x, -35f);
            rPos.z = Mathf.Max(rPos.z, -0.08f);
        }

        void UpdateRecoil()
        {
            rPos = Vector3.SmoothDamp(rPos, Vector3.zero, ref rPosVel, recoverTime * 0.7f);
            rRot = Vector3.SmoothDamp(rRot, Vector3.zero, ref rRotVel, recoverTime);
            if (recoilPivot)
            {
                recoilPivot.localPosition = pivotPos + rPos;
                recoilPivot.localRotation = pivotRot * Quaternion.Euler(rRot);
            }
        }

        void UpdateFlash()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer > 0f) return;
            if (muzzleFlash) muzzleFlash.SetActive(false);
            if (muzzleLight) muzzleLight.enabled = false;
        }

        void Eject(GameObject prefab)
        {
            if (prefab == null || ejectionPort == null) return;
            var c = Instantiate(prefab, ejectionPort.position, ejectionPort.rotation * Quaternion.Euler(0f, 0f, 90f));
            foreach (var cc in c.GetComponentsInChildren<Collider>())
                foreach (var own in OwnColliders)
                    if (own && !own.isTrigger) Physics.IgnoreCollision(cc, own, true);
            var rb = c.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = velocity
                    + ejectionPort.right * Random.Range(1.8f, 2.8f)
                    + ejectionPort.up * Random.Range(1.0f, 1.7f)
                    - ejectionPort.forward * Random.Range(0.1f, 0.5f);
                rb.angularVelocity = Random.insideUnitSphere * 25f;
            }
            Destroy(c, 20f);
        }

        /// <summary>Disparo de teste (menu Ferramentas > Teste > Disparar arma selecionada, em Play).</summary>
        public void TestFire()
        {
            boltLocked = false;
            if (!roundChambered)
            {
                var mag = CurrentMagazine;
                if (mag == null || !mag.TryTakeRound()) { }
                roundChambered = true;
            }
            Fire();
        }

        // ---------- Ações dos botões ----------
        public void DropMagazine()
        {
            if (magazineWell == null || !magazineWell.hasSelection) return;
            magazineWell.Eject(velocity);
            WeaponUtils.Haptic(primary, 0.3f, 0.03f);
        }

        public void ReleaseBolt()
        {
            if (!boltLocked) return;
            boltLocked = false;
            var mag = CurrentMagazine;
            if (!roundChambered && mag != null && mag.TryTakeRound()) roundChambered = true;
            WeaponAudio.PlayAt(WeaponAudio.RackForward(), transform.position, 0.8f);
            WeaponUtils.Haptic(primary, 0.4f, 0.04f);
        }

        public void CycleFireMode()
        {
            if (fireModes.Length <= 1) return;
            fireModeIndex = (fireModeIndex + 1) % fireModes.Length;
            UpdateSelectorVisual();
            WeaponAudio.PlayAt(WeaponAudio.Selector(), transform.position, 0.5f);
            WeaponUtils.Haptic(primary, 0.2f, 0.02f);
        }

        void UpdateSelectorVisual()
        {
            if (!selectorVisual || selectorAngles == null || selectorAngles.Length == 0) return;
            float ang = selectorAngles[Mathf.Clamp(fireModeIndex, 0, selectorAngles.Length - 1)];
            selectorVisual.localRotation = selectorBaseRot * Quaternion.Euler(-ang, 0f, 0f);
        }

        // ---------- Chamadas da slide / alavanca de carregar ----------
        public void OnHandlePulledBack()
        {
            boltLocked = false;
            if (roundChambered)
            {
                roundChambered = false;
                Eject(liveRoundPrefab != null ? liveRoundPrefab : casingPrefab);
            }
            WeaponAudio.PlayAt(WeaponAudio.RackBack(), transform.position, 0.7f);
        }

        public void OnHandleReturned()
        {
            var mag = CurrentMagazine;
            if (!roundChambered && mag != null && mag.TryTakeRound()) roundChambered = true;
            else if (!roundChambered && mag != null && lockOpenOnEmpty) boltLocked = true;
            WeaponAudio.PlayAt(WeaponAudio.RackForward(), transform.position, 0.8f);
        }
    }
}
