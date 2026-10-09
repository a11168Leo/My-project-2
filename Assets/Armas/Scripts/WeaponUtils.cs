using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem.Controls;
using ISXRController = UnityEngine.InputSystem.XR.XRController;
using ISXRControllerWithRumble = UnityEngine.InputSystem.XR.XRControllerWithRumble;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Utilitários partilhados: mão (esquerda/direita), gatilho analógico, botões e vibração.
    /// Lê primeiro pelo Input System novo (o que o projeto usa com OpenXR) e só depois pela API XR antiga.
    /// </summary>
    public static class WeaponUtils
    {
        public const string BtnPrimary = "primaryButton";       // A / X
        public const string BtnSecondary = "secondaryButton";   // B / Y
        public const string BtnStick = "thumbstickClicked";     // carregar no analógico

        /// <summary>Descobre a mão a partir do nome dos objetos pai ("Left Controller" / "Right Controller").</summary>
        public static XRNode? HandOf(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var n = p.name.ToLowerInvariant();
                if (n.Contains("left")) return XRNode.LeftHand;
                if (n.Contains("right")) return XRNode.RightHand;
            }
            return null;
        }

        static ISXRController Ctrl(XRNode node)
        {
            return node == XRNode.LeftHand ? ISXRController.leftHand : ISXRController.rightHand;
        }

        public static float Trigger(XRNode? node)
        {
            if (node == null) return 0f;
            var c = Ctrl(node.Value);
            if (c != null)
            {
                var a = c.TryGetChildControl<AxisControl>("trigger");
                if (a != null) return a.ReadValue();
            }
            var d = InputDevices.GetDeviceAtXRNode(node.Value);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.trigger, out float v) ? v : 0f;
        }

        public static bool Button(XRNode? node, string controlName)
        {
            if (node == null) return false;
            var c = Ctrl(node.Value);
            if (c != null)
            {
                var b = c.TryGetChildControl<ButtonControl>(controlName);
                if (b == null && controlName == BtnStick) b = c.TryGetChildControl<ButtonControl>("primary2DAxisClick");
                if (b != null) return b.isPressed;
            }
            var usage = controlName == BtnPrimary ? CommonUsages.primaryButton
                      : controlName == BtnSecondary ? CommonUsages.secondaryButton
                      : CommonUsages.primary2DAxisClick;
            var d = InputDevices.GetDeviceAtXRNode(node.Value);
            return d.isValid && d.TryGetFeatureValue(usage, out bool v) && v;
        }

        public static void Haptic(XRNode? node, float amplitude, float duration)
        {
            if (node == null) return;
            amplitude = Mathf.Clamp01(amplitude);
            if (Ctrl(node.Value) is ISXRControllerWithRumble r)
            {
                r.SendImpulse(amplitude, duration);
                return;
            }
            var d = InputDevices.GetDeviceAtXRNode(node.Value);
            if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0, amplitude, duration);
        }

        /// <summary>Encontra quem recebe dano (DamageZone na peça ou Health no objeto pai).</summary>
        public static IDamageable FindDamageable(Collider c)
        {
            if (c == null) return null;
            var d = c.GetComponent<IDamageable>();
            if (d != null && (d as Object) != null) return d;
            d = c.GetComponentInParent<IDamageable>();
            if (d != null && (d as Object) != null) return d;
            return null;
        }
    }
}
