using UnityEngine;
using UnityEngine.InputSystem;

namespace BobosJourney
{
    /// <summary>Keyboard, mouse and gamepad input, read straight from the Input System devices.</summary>
    public static class GameInput
    {
        public static float MouseSensitivity = 0.09f;
        public static float StickSensitivity = 160f;

        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                var k = K;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                }
                var g = G;
                if (g != null)
                {
                    var stick = g.leftStick.ReadValue();
                    if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool JumpDown => (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool JumpHeld => (K != null && K.spaceKey.isPressed) || (G != null && G.buttonSouth.isPressed);

        /// <summary>Camera look in degrees for this frame.</summary>
        public static Vector2 Look
        {
            get
            {
                var v = Vector2.zero;
                if (M != null && Cursor.lockState == CursorLockMode.Locked) v += M.delta.ReadValue() * MouseSensitivity;
                if (G != null) v += G.rightStick.ReadValue() * StickSensitivity * Time.unscaledDeltaTime;
                return v;
            }
        }

        public static float Zoom
        {
            get
            {
                float z = 0;
                if (M != null) z -= Mathf.Sign(M.scroll.ReadValue().y) * (Mathf.Abs(M.scroll.ReadValue().y) > 0.01f ? 1f : 0f);
                if (G != null)
                {
                    if (G.rightShoulder.isPressed) z -= 4f * Time.unscaledDeltaTime;
                    if (G.leftShoulder.isPressed) z += 4f * Time.unscaledDeltaTime;
                }
                return z;
            }
        }

        public static bool PauseDown => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool ResetDown => (K != null && K.rKey.wasPressedThisFrame) || (G != null && G.selectButton.wasPressedThisFrame);
        public static bool ClickDown => M != null && M.leftButton.wasPressedThisFrame;

        public static bool AdvanceDown =>
            (K != null && (K.spaceKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame || K.rightArrowKey.wasPressedThisFrame)) ||
            (M != null && M.leftButton.wasPressedThisFrame) ||
            (G != null && G.buttonSouth.wasPressedThisFrame);

        public static bool SkipDown => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
    }
}
