using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Временный отладочный драйвер: каждый кадр «зажимает» W + Shift,
/// чтобы проверить позу спринта без ручного ввода. Удалить после теста.
/// </summary>
public sealed class TmpSprintDriver : MonoBehaviour
{
    private Keyboard _keyboard;

    private void Update()
    {
        if (_keyboard == null)
            _keyboard = InputSystem.AddDevice<Keyboard>("TmpSprintKeyboard");

        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.LeftShift));
    }
}
