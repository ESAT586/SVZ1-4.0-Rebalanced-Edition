using UnityEngine;
using System.Collections.Generic;

public static class PCInputManager
{
    public enum Action
    {
        MoveLeft,
        MoveRight,
        UsePotion,
        UseAbility,
        UseAbility2,
        Pause,
        Helper1,
        Helper2,
        Helper3,
        Helper4,
        Helper5
    }

    private static Dictionary<Action, KeyCode> m_Bindings = new Dictionary<Action, KeyCode>();

    static PCInputManager()
    {
        LoadBindings();
    }

    public static void LoadBindings()
    {
        m_Bindings[Action.MoveLeft]   = (KeyCode)PlayerPrefs.GetInt("Key_MoveLeft",   (int)KeyCode.A);
        m_Bindings[Action.MoveRight]  = (KeyCode)PlayerPrefs.GetInt("Key_MoveRight",  (int)KeyCode.D);
        m_Bindings[Action.UsePotion]  = (KeyCode)PlayerPrefs.GetInt("Key_UsePotion",  (int)KeyCode.F);
        m_Bindings[Action.UseAbility] = (KeyCode)PlayerPrefs.GetInt("Key_UseAbility", (int)KeyCode.Q);
        m_Bindings[Action.UseAbility2]= (KeyCode)PlayerPrefs.GetInt("Key_UseAbility2", (int)KeyCode.E);
        m_Bindings[Action.Pause]      = (KeyCode)PlayerPrefs.GetInt("Key_Pause",      (int)KeyCode.Escape);
        
        m_Bindings[Action.Helper1]    = (KeyCode)PlayerPrefs.GetInt("Key_Helper1",    (int)KeyCode.Alpha1);
        m_Bindings[Action.Helper2]    = (KeyCode)PlayerPrefs.GetInt("Key_Helper2",    (int)KeyCode.Alpha2);
        m_Bindings[Action.Helper3]    = (KeyCode)PlayerPrefs.GetInt("Key_Helper3",    (int)KeyCode.Alpha3);
        m_Bindings[Action.Helper4]    = (KeyCode)PlayerPrefs.GetInt("Key_Helper4",    (int)KeyCode.Alpha4);
        m_Bindings[Action.Helper5]    = (KeyCode)PlayerPrefs.GetInt("Key_Helper5",    (int)KeyCode.Alpha5);
    }

    public static void SaveBindings()
    {
        foreach (var kvp in m_Bindings)
        {
            PlayerPrefs.SetInt("Key_" + kvp.Key.ToString(), (int)kvp.Value);
        }
        PlayerPrefs.Save();
    }

    public static void SetBinding(Action action, KeyCode key)
    {
        m_Bindings[action] = key;
    }

    public static KeyCode GetBinding(Action action)
    {
        KeyCode key;
        if (m_Bindings.TryGetValue(action, out key))
        {
            return key;
        }
        LoadBindings();
        return m_Bindings.TryGetValue(action, out key) ? key : KeyCode.None;
    }

    public static bool GetKey(Action action)
    {
        if (SVZMultiplayerUI.IsLobbyOpen) return false;
        KeyCode key;
        bool customPressed = m_Bindings.TryGetValue(action, out key) && Input.GetKey(key);

        switch (action)
        {
            case Action.MoveLeft:
                if (customPressed || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.JoystickButton4)) return true;
                try { if (Input.GetAxisRaw("Horizontal") < -0.35f) return true; } catch { }
                return false;

            case Action.MoveRight:
                if (customPressed || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.JoystickButton5)) return true;
                try { if (Input.GetAxisRaw("Horizontal") > 0.35f) return true; } catch { }
                return false;

            case Action.UsePotion:
                return customPressed || Input.GetKey(KeyCode.JoystickButton1); // B / Circle

            case Action.UseAbility:
                return customPressed || Input.GetKey(KeyCode.JoystickButton2); // X

            case Action.UseAbility2:
                return customPressed || Input.GetKey(KeyCode.JoystickButton3); // Y

            case Action.Pause:
                return customPressed || Input.GetKey(KeyCode.JoystickButton7) || Input.GetKey(KeyCode.Menu); // Start / Menu

            case Action.Helper1:
                return customPressed || Input.GetKey(KeyCode.Keypad1) || Input.GetKey(KeyCode.UpArrow);

            case Action.Helper2:
                return customPressed || Input.GetKey(KeyCode.Keypad2) || Input.GetKey(KeyCode.DownArrow);

            case Action.Helper3:
                return customPressed || Input.GetKey(KeyCode.Keypad3);

            case Action.Helper4:
                return customPressed || Input.GetKey(KeyCode.Keypad4);

            case Action.Helper5:
                return customPressed || Input.GetKey(KeyCode.Keypad5);

            default:
                return customPressed;
        }
    }

    public static bool GetKeyDown(Action action)
    {
        if (SVZMultiplayerUI.IsLobbyOpen) return false;
        KeyCode key;
        bool customPressed = m_Bindings.TryGetValue(action, out key) && Input.GetKeyDown(key);

        switch (action)
        {
            case Action.MoveLeft:
                return customPressed || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton4);

            case Action.MoveRight:
                return customPressed || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton5);

            case Action.UsePotion:
                return customPressed || Input.GetKeyDown(KeyCode.JoystickButton1);

            case Action.UseAbility:
                return customPressed || Input.GetKeyDown(KeyCode.JoystickButton2);

            case Action.UseAbility2:
                return customPressed || Input.GetKeyDown(KeyCode.JoystickButton3);

            case Action.Pause:
                return customPressed || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.Menu);

            case Action.Helper1:
                return customPressed || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.UpArrow);

            case Action.Helper2:
                return customPressed || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow);

            case Action.Helper3:
                return customPressed || Input.GetKeyDown(KeyCode.Keypad3);

            case Action.Helper4:
                return customPressed || Input.GetKeyDown(KeyCode.Keypad4);

            case Action.Helper5:
                return customPressed || Input.GetKeyDown(KeyCode.Keypad5);

            default:
                return customPressed;
        }
    }

    public static string GetBindingName(Action action)
    {
        KeyCode key;
        if (m_Bindings.TryGetValue(action, out key))
        {
            return key.ToString();
        }
        return "None";
    }

    public static void ResetToDefaults()
    {
        m_Bindings[Action.MoveLeft]   = KeyCode.A;
        m_Bindings[Action.MoveRight]  = KeyCode.D;
        m_Bindings[Action.UsePotion]  = KeyCode.F;
        m_Bindings[Action.UseAbility] = KeyCode.Q;
        m_Bindings[Action.UseAbility2]= KeyCode.E;
        m_Bindings[Action.Pause]      = KeyCode.Escape;
        m_Bindings[Action.Helper1]    = KeyCode.Alpha1;
        m_Bindings[Action.Helper2]    = KeyCode.Alpha2;
        m_Bindings[Action.Helper3]    = KeyCode.Alpha3;
        m_Bindings[Action.Helper4]    = KeyCode.Alpha4;
        m_Bindings[Action.Helper5]    = KeyCode.Alpha5;
        SaveBindings();
    }

    public static string GetActionName(Action action)
    {
        switch (action)
        {
            case Action.MoveLeft:   return "MOVE LEFT";
            case Action.MoveRight:  return "MOVE RIGHT";
            case Action.UsePotion:  return "USE POTION";
            case Action.UseAbility: return "ABILITY 1";
            case Action.UseAbility2:return "ABILITY 2";
            case Action.Pause:      return "PAUSE";
            case Action.Helper1:    return "ALLY 1";
            case Action.Helper2:    return "ALLY 2";
            case Action.Helper3:    return "ALLY 3";
            case Action.Helper4:    return "ALLY 4";
            case Action.Helper5:    return "ALLY 5";
            default: return action.ToString();
        }
    }
}
