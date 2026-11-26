using UnityEngine;

public static class PrisonInput
{
    public static bool Held(KeyCode key) => RoundPlaytest.Active ? RoundPlaytest.Held(key) : !PrisonPresentation.Blocked && Input.GetKey(key);
    public static bool Down(KeyCode key) => RoundPlaytest.Active ? RoundPlaytest.Down(key) : !PrisonPresentation.Blocked && Input.GetKeyDown(key);
}
