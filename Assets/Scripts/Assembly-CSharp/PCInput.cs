// ============================================================
// PCInput.cs  — YENİ DOSYA (Unity 6 Migration)
// Assets/Scripts/Assembly-CSharp/ klasörüne ekle
//
// Görev: Orijinal kodda PCInput.touchCount / PCInput.touches[i]
//        şeklinde kullanılan, artık kaynak kodu olmayan sınıfın
//        yerine geçer. Hem dokunmatik ekran hem de fare girişini
//        destekler (PC'de test için fare = parmak gibi davranır).
// ============================================================
using UnityEngine;
using System.Collections.Generic;

public class PCInput : MonoBehaviour
{
    // --- BURAYA EKLE ---
    public static bool GetButtonDown(string buttonName)
    {
        if (SVZMultiplayerUI.IsLobbyOpen) return false;
        switch (buttonName)
        {
            case "Skill1":    return PCInputManager.GetKeyDown(PCInputManager.Action.UseAbility) || Input.GetKeyDown(KeyCode.JoystickButton2); // Q / Gamepad X
            case "Ability1":  return PCInputManager.GetKeyDown(PCInputManager.Action.UseAbility2) || Input.GetKeyDown(KeyCode.JoystickButton3); // E / Gamepad Y
            case "Ability2":  return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton5); // R / Gamepad RB
            case "CycleItems": return PCInputManager.GetKeyDown(PCInputManager.Action.UsePotion) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton4); // F / Gamepad B / LB
            case "Helper1":   return PCInputManager.GetKeyDown(PCInputManager.Action.Helper1) || Input.GetKeyDown(KeyCode.UpArrow);
            case "Helper2":   return PCInputManager.GetKeyDown(PCInputManager.Action.Helper2) || Input.GetKeyDown(KeyCode.DownArrow);
            case "Helper3":   return PCInputManager.GetKeyDown(PCInputManager.Action.Helper3);
            case "Helper4":   return PCInputManager.GetKeyDown(PCInputManager.Action.Helper4);
            case "Helper5":   return PCInputManager.GetKeyDown(PCInputManager.Action.Helper5);
            default: 
                try { return Input.GetButtonDown(buttonName); } 
                catch { return false; }
        }
    }
    public static float mouseScrollDelta { get { try { return Input.GetAxis("Mouse ScrollWheel"); } catch { return 0f; } } }

    public static PCInput instance; 
    void Awake() { instance = this; }
    // -------------------
    // --------------------------------------------------------
    // Touch sayısı: gerçek dokunuş yoksa fareyi simüle et
    // --------------------------------------------------------
    public static int touchCount
    {
        get
        {
            if (SVZMultiplayerUI.IsLobbyOpen) return 0;
            int realTouches = Input.touchCount;
            if (realTouches > 0) return realTouches;

            // Fare ile simülasyon (Editor ve PC build için)
            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
                return 1;

            return 0;
        }
    }

    // --------------------------------------------------------
    // Touch dizisi: gerçek Touch nesneleri veya fare simülasyonu
    // --------------------------------------------------------
    public static IList<Touch> touches
    {
        get
        {
            if (Input.touchCount > 0)
                return Input.touches;

            // Fare simülasyonu
            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                TouchPhase phase = TouchPhase.Stationary;
                if (Input.GetMouseButtonDown(0))  phase = TouchPhase.Began;
                else if (Input.GetMouseButtonUp(0)) phase = TouchPhase.Ended;
                else if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
                    phase = TouchPhase.Moved;

                // Touch struct'ını oluşturmak doğrudan mümkün değil;
                // SUIInputManager'ın beklediği alanları sarıyoruz:
                return new SimulatedTouchList(Input.mousePosition, phase);
            }

            return new SimulatedTouchList(); // boş
        }
    }

    // --------------------------------------------------------
    // İç yardımcı sınıf: Touch benzeri nesne listesi
    // --------------------------------------------------------
    public class SimulatedTouch
    {
        public int fingerId = 0;
        public Vector2 position;
        public TouchPhase phase;

        public SimulatedTouch(Vector2 pos, TouchPhase ph)
        {
            position = pos;
            phase    = ph;
        }
    }

    public class SimulatedTouchList : IList<Touch>
    {
        private readonly SimulatedTouch _sim;
        private readonly bool _hasTouch;

        public SimulatedTouchList() { _hasTouch = false; }
        public SimulatedTouchList(Vector2 pos, TouchPhase phase)
        {
            _sim      = new SimulatedTouch(pos, phase);
            _hasTouch = true;
        }

        // IList<Touch> — SUIInputManager sadece [i].fingerId ve [i].position kullanıyor
        // Gerçek Touch struct kullanamayız (fingerId readonly), bu yüzden
        // SUIInputManager.cs de SimulatedTouch kullanacak şekilde güncellendi.
        public int Count { get { return _hasTouch ? 1 : 0; } }
        public bool IsReadOnly { get { return true; } }

        public Touch this[int index]
        {
            get { throw new System.NotSupportedException("Use PCInput.GetSimTouch()"); }
            set { throw new System.NotSupportedException(); }
        }

        // Gerçek IList boilerplate — kullanılmıyor ama interface gereği:
        public System.Collections.IEnumerator GetEnumerator() { throw new System.NotSupportedException(); }
        System.Collections.Generic.IEnumerator<Touch> System.Collections.Generic.IEnumerable<Touch>.GetEnumerator() { throw new System.NotSupportedException(); }
        public void Add(Touch item) { throw new System.NotSupportedException(); }
        public void Clear() { throw new System.NotSupportedException(); }
        public bool Contains(Touch item) { return false; }
        public void CopyTo(Touch[] array, int arrayIndex) {}
        public bool Remove(Touch item) { return false; }
        public int IndexOf(Touch item) { return -1; }
        public void Insert(int index, Touch item) { throw new System.NotSupportedException(); }
        public void RemoveAt(int index) { throw new System.NotSupportedException(); }
    }

    // --------------------------------------------------------
    // SUIInputManager.cs'nin doğrudan çağıracağı yardımcı
    // --------------------------------------------------------
    public static int GetFingerId(int index)
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(index).fingerId;
        return 0; // fare = parmak 0
    }

    public static Vector2 GetPosition(int index)
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(index).position;
        return Input.mousePosition; // fare pozisyonu
    }
}
