using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Mexe os dedos da luva:
    ///  - botão do lado (grip)  -> fecha médio, anelar, mindinho e polegar  (forma "Punho")
    ///  - gatilho               -> dobra o indicador                       (forma "Indicador")
    ///  - a segurar uma arma/objeto: a mão fica fechada e o indicador pousa no gatilho.
    /// Fica no objeto da luva (filho do "Left/Right Controller").
    /// </summary>
    public class HandPoseDriver : MonoBehaviour
    {
        public XRNode hand = XRNode.RightHand;
        [Tooltip("Velocidade com que os dedos acompanham os botões")]
        public float speed = 18f;
        [Range(0, 1)] public float indexRestOnTrigger = 0.3f;

        SkinnedMeshRenderer[] renderers;
        int[] fistIdx, indexIdx;
        XRBaseInteractor[] interactors;
        float fist, index;

        void Awake()
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            fistIdx = new int[renderers.Length];
            indexIdx = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMesh;
                fistIdx[i] = m ? m.GetBlendShapeIndex("Punho") : -1;
                indexIdx[i] = m ? m.GetBlendShapeIndex("Indicador") : -1;
            }
            var ctrl = transform.parent != null ? transform.parent : transform;
            interactors = ctrl.GetComponentsInChildren<XRBaseInteractor>(true);
            var auto = WeaponUtils.HandOf(transform);
            if (auto.HasValue) hand = auto.Value;
        }

        bool Holding()
        {
            foreach (var i in interactors)
            {
                if (i == null || !i.isActiveAndEnabled || !i.hasSelection) continue;
                if (i is XRSocketInteractor) continue;
                var n = i.GetType().Name;
                if (n.Contains("Teleport") || n.Contains("Gaze")) continue;
                return true;
            }
            return false;
        }

        void Update()
        {
            float grip = WeaponUtils.Grip(hand);
            float trig = WeaponUtils.Trigger(hand);
            float targetFist = grip, targetIndex = trig;
            if (Holding())
            {
                targetFist = 1f;
                targetIndex = Mathf.Lerp(indexRestOnTrigger, 1f, trig);
            }
            float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
            fist = Mathf.Lerp(fist, targetFist, k);
            index = Mathf.Lerp(index, targetIndex, k);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (fistIdx[i] >= 0) renderers[i].SetBlendShapeWeight(fistIdx[i], fist * 100f);
                if (indexIdx[i] >= 0) renderers[i].SetBlendShapeWeight(indexIdx[i], index * 100f);
            }
        }
    }
}
