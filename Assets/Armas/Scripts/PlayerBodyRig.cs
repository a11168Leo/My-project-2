using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Estrutura do corpo do jogador (cinto + colete). Segue a cabeça: fica à altura da cintura
    /// e roda com o corpo (com algum atraso, como o tronco real). Os coldres e bolsas são filhos deste objeto.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerBodyRig : MonoBehaviour
    {
        public Transform head;
        [Tooltip("Distância da cabeça (olhos) até à cintura, em metros")]
        public float waistDrop = 0.72f;
        [Tooltip("O pescoço fica um pouco à frente do centro do corpo")]
        public float backOffset = 0.06f;
        public float yawFollowSpeed = 5f;
        [Tooltip("Rodar a cabeça menos do que isto quase não roda o corpo")]
        public float yawDeadZone = 35f;

        float yaw;
        bool initialized;

        void Update()
        {
            if (!head)
            {
                var cam = Camera.main;
                if (!cam) return;
                head = cam.transform;
            }

            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(head.up, Vector3.up);
            float headYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            if (!initialized) { yaw = headYaw; initialized = true; }
            float diff = Mathf.Abs(Mathf.DeltaAngle(yaw, headYaw));
            float speed = diff > yawDeadZone ? yawFollowSpeed : yawFollowSpeed * 0.15f;
            yaw = Mathf.LerpAngle(yaw, headYaw, Mathf.Clamp01(Time.deltaTime * speed));

            var rot = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(head.position + Vector3.down * waistDrop - rot * Vector3.forward * backOffset, rot);
        }
    }
}
