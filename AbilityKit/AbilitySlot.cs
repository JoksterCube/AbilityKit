using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoksterCube.AbilityKit;

// An ability's slot on the HUD: a copy of the Guardian Power slot (icon, name, and "Ready" or a countdown under it),
// shown right of the Guardian Power, or in its place while no power is chosen.
//
// Every mod built on AbilityKit has its own slots, in one row after the Guardian Power, left to right by priority
// (lower priority further left; the same priority by id). Only the slots showing take up room, and all of them are
// spaced evenly, one step apart. Each name (the Guardian Power's too) sits in a field a little wider than an icon and
// shrinks to fit it on one line, so long names don't push the slots apart. The mods each carry their own copy of AbilityKit, so
// they agree through the slots themselves: each slot's object is named "AbilityKit_Slot|priority|id", and each slot
// places itself among the showing slots it finds next to it. Any mod can hide all the names (see SetNamesHidden).
public sealed class AbilitySlot
{
    public const string NamePrefix = "AbilityKit_Slot|";
    // An empty, inactive sibling of the Guardian Power slot, "AbilityKit_HideNames|owner". While any is there, the
    // names above the power and every slot are hidden and the slots move closer.
    public const string HideNamesPrefix = "AbilityKit_HideNames|";
    private const char NameSeparator = '|';
    // Vanilla's look for a power on cooldown.
    private static readonly Color CooldownColor = new(1f, 0f, 1f, 0f);
    // Space between two icons.
    private const float IconGap = 8f;
    // A name's field is this much wider than an icon, and two fields keep NameGap apart. A name keeps NamePadding free
    // at each side of its field, and a longer one shrinks to fit, down to MinNameScale of its normal size.
    private const float NameExtra = 18f;
    private const float NameGap = 4f;
    private const float NamePadding = 3f;
    private const float MinNameScale = 0.55f;
    // The red cross over a switched-off ability, the same as ChestKit's over a switched-off flap button.
    private const float CrossThickness = 4f;
    private const float CrossInsetPart = 0.25f;
    private static readonly Color CrossColor = new(0.9f, 0.15f, 0.1f, 1f);
    private static readonly Color CrossEdge = new(0.1f, 0.05f, 0.03f, 0.85f);

    private readonly string _id;
    private readonly int _priority;
    private RectTransform? _root;
    private TMP_Text? _name;
    private TMP_Text? _text;
    private Image? _icon;
    private GameObject? _cross;

    private static readonly List<(int Priority, string Id, RectTransform Rect)> Row = [];

    // id: unique among all mods, e.g. "PingMark_Perception". priority: its place in the row (lower further left).
    public AbilitySlot(string id, int priority)
    {
        _id = id.Replace(NameSeparator, '_');
        _priority = priority;
    }

    // The same, with the slot's claimed place from SlotPriority.
    public AbilitySlot(string id, SlotPriority priority) : this(id, (int)priority)
    {
    }

    public bool IsShown => _root && _root!.gameObject.activeSelf;

    // Every frame, from a Hud.UpdateGuardianPower postfix. show: whether the slot shows at all. text: under the name,
    // e.g. "Ready" or a time left. ready: a ready slot's icon is lit, otherwise faded like a power on cooldown.
    // crossed: a red cross over the icon, for an ability that is a switch and is off.
    public void Update(Hud hud, bool show, Sprite? icon, string name, string text, bool ready, bool crossed = false)
    {
        // The copy is destroyed with the Hud on logout and made again for the next one.
        if (!_root)
        {
            if (!show) return;
            Create(hud);
        }
        if (_root!.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show) return;

        if (_icon!.sprite != icon) _icon.sprite = icon;
        _icon.color = ready ? Color.white : CooldownColor;
        if (_cross!.activeSelf != crossed) _cross.SetActive(crossed);
        if (_name!.text != name) _name.text = name;
        if (_text!.text != text) _text.text = text;
        Place(hud);
    }

    // Every frame, from a Hud.UpdateGuardianPower postfix, by a mod that offers this as a setting. owner: unique per
    // mod, e.g. "PingMark". While any mod hides them, the names above the Guardian Power and all slots are hidden.
    public static void SetNamesHidden(Hud hud, string owner, bool hidden)
    {
        Transform parent = hud.m_gpRoot.parent;
        string markerName = HideNamesPrefix + owner.Replace(NameSeparator, '_');
        Transform? marker = parent.Find(markerName);
        if (hidden && !marker)
        {
            GameObject created = new(markerName, typeof(RectTransform));
            created.SetActive(false);
            created.transform.SetParent(parent, false);
        }
        // At once, so the names come back this frame.
        else if (!hidden && marker)
            UnityEngine.Object.DestroyImmediate(marker!.gameObject);
        ShowPowerName(hud, !NamesHidden(parent));
    }

    // "Ready", in the game's language.
    public static string ReadyText => Localization.instance.Localize("$hud_ready");

    // A time left as the game shows a power's cooldown: "45s", "2m 10s".
    public static string TimeText(float seconds) => StatusEffect.GetTimeString(seconds);

    // In the row: the Guardian Power first while it shows, then the showing slots by priority, all one step apart.
    private void Place(Hud hud)
    {
        RectTransform power = hud.m_gpRoot;
        Transform parent = power.parent;
        Row.Clear();
        foreach (Transform sibling in parent)
        {
            if (!sibling.gameObject.activeSelf || sibling is not RectTransform rect) continue;
            if (TryParse(sibling.name, out int priority, out string id)) Row.Add((priority, id, rect));
        }
        Row.Sort((a, b) => Compare(a.Priority, a.Id, b.Priority, b.Id));

        int index = power.gameObject.activeSelf ? 1 : 0;
        int mine = index;
        foreach ((int _, string _, RectTransform rect) in Row)
        {
            if (rect == _root) mine = index;
            index++;
        }
        Row.Clear();

        bool namesHidden = NamesHidden(parent);
        ShowPowerName(hud, !namesHidden);
        if (_name!.gameObject.activeSelf == namesHidden) _name.gameObject.SetActive(!namesHidden);

        float icon = Width(hud.m_gpIcon.rectTransform, parent);
        float field = icon + NameExtra;
        if (!namesHidden)
        {
            FitName(hud.m_gpName, field, parent);
            FitName(_name, field, parent);
        }
        float step = namesHidden ? icon + IconGap : Mathf.Max(icon + IconGap, field + NameGap);
        _root!.anchoredPosition = power.anchoredPosition + new Vector2(mine * step, 0f);
    }

    private static bool NamesHidden(Transform parent)
    {
        foreach (Transform child in parent)
            if (child.name.StartsWith(HideNamesPrefix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static void ShowPowerName(Hud hud, bool show)
    {
        GameObject name = hud.m_gpName.gameObject;
        // Never the whole power slot, should the name ever be its root.
        if (name == hud.m_gpRoot.gameObject) return;
        if (name.activeSelf != show) name.SetActive(show);
    }

    // A name on one line in a field `width` wide (as the slots' parent measures it), with NamePadding free at each
    // side; a name too wide shrinks, down to MinNameScale. The Guardian Power's name gets the same, so all the names
    // match and sit on one bottom line.
    //
    // Every mod's copy of AbilityKit fits the Guardian Power's name, so the state lives on the text itself: its normal
    // size in fontSizeMax, and the line's distance from the bottom of the field in its bottom margin.
    private static void FitName(TMP_Text name, float width, Transform parent)
    {
        RectTransform rect = name.rectTransform;
        float local = width * Mathf.Max(parent.lossyScale.x, 0.0001f) / Mathf.Max(rect.lossyScale.x, 0.0001f);
        if (Mathf.Abs(rect.rect.width - local) > 0.5f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, local);

        if (!IsFitted(name)) Prepare(name);
        if (Fitted.TryGetValue(name, out (string Text, float Width) last) && last.Text == name.text &&
            Mathf.Abs(last.Width - local) < 0.5f) return;
        // Names of HUDs gone since logout.
        if (Fitted.Count > 32)
            foreach (TMP_Text gone in new List<TMP_Text>(Fitted.Keys))
                if (!gone) Fitted.Remove(gone!);
        Fitted[name] = (name.text, local);

        float max = name.fontSizeMax;
        name.fontSize = max;
        float room = local * (1f - 2f * NamePadding / width) - name.margin.x - name.margin.z;
        float wide = name.GetPreferredValues(name.text).x;
        float size = wide > room && wide > 0f ? max * room / wide : max;
        name.fontSize = Mathf.Max(size, max * MinNameScale);
    }

    // Names fitted already, with the text and width they were fitted for (this copy of AbilityKit only).
    private static readonly Dictionary<TMP_Text, (string Text, float Width)> Fitted = [];

    private static bool IsFitted(TMP_Text name) =>
        !name.enableAutoSizing && name.textWrappingMode == TextWrappingModes.NoWrap &&
        name.verticalAlignment == VerticalAlignmentOptions.Bottom &&
        Mathf.Approximately(name.fontSizeMin, name.fontSizeMax * MinNameScale);

    // Wrapping, bottom aligned, the bottom margin lifting the last line to where the name's single line was.
    private static void Prepare(TMP_Text name)
    {
        float max = name.enableAutoSizing ? name.fontSizeMax : name.fontSize;
        name.enableAutoSizing = false;
        name.fontSize = max;
        float line = name.GetPreferredValues("A").y;
        float free = Mathf.Max(0f, name.rectTransform.rect.height - name.margin.y - name.margin.w - line);
        float lift = name.verticalAlignment switch
        {
            VerticalAlignmentOptions.Top => free,
            VerticalAlignmentOptions.Bottom => 0f,
            _ => free / 2f,
        };
        Vector4 margin = name.margin;
        margin.w += lift;
        name.margin = margin;
        name.verticalAlignment = VerticalAlignmentOptions.Bottom;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Overflow;
        name.fontSizeMax = max;
        name.fontSizeMin = max * MinNameScale;
    }

    // A width as the slots' parent measures it.
    private static float Width(RectTransform rect, Transform parent) =>
        rect.rect.width * rect.lossyScale.x / Mathf.Max(parent.lossyScale.x, 0.0001f);

    // Lower priority first; the same priority by id, so every mod agrees on the order.
    private static int Compare(int priorityA, string idA, int priorityB, string idB) =>
        priorityA != priorityB ? priorityA.CompareTo(priorityB) : string.CompareOrdinal(idA, idB);

    private static bool TryParse(string name, out int priority, out string id)
    {
        priority = 0;
        id = "";
        if (!name.StartsWith(NamePrefix, StringComparison.Ordinal)) return false;
        string[] parts = name.Split(NameSeparator);
        if (parts.Length < 3 || !int.TryParse(parts[1], out priority)) return false;
        id = parts[2];
        return true;
    }

    private void Create(Hud hud)
    {
        RectTransform power = hud.m_gpRoot;
        GameObject copy = UnityEngine.Object.Instantiate(power.gameObject, power.parent);
        copy.name = $"{NamePrefix}{_priority}{NameSeparator}{_id}";
        _root = (RectTransform)copy.transform;
        _name = Find(power, hud.m_gpName, _root);
        _text = Find(power, hud.m_gpCooldown, _root);
        _icon = Find(power, hud.m_gpIcon, _root);
        // The power's touch button would start the Guardian Power; each ability has its own shortcut.
        foreach (UIInputHandler handler in copy.GetComponentsInChildren<UIInputHandler>(true))
            handler.enabled = false;
        _cross = CreateCross(_icon.rectTransform);
        _cross.SetActive(false);
    }

    // A red X over the icon, thin enough to keep it readable, with a dark edge so it shows on any icon.
    private static GameObject CreateCross(RectTransform icon)
    {
        float size = Mathf.Min(icon.rect.width, icon.rect.height);
        float inset = size * CrossInsetPart;
        GameObject cross = new("Cross", typeof(RectTransform));
        cross.transform.SetParent(icon, false);
        RectTransform rect = (RectTransform)cross.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        float length = (size - inset * 2f) * 1.41f;
        foreach (float angle in new[] { 45f, -45f }) Bar(rect, angle, length + 3f, CrossThickness + 3f, CrossEdge);
        foreach (float angle in new[] { 45f, -45f }) Bar(rect, angle, length, CrossThickness, CrossColor);
        return cross;
    }

    private static void Bar(RectTransform parent, float angle, float length, float thickness, Color color)
    {
        GameObject bar = new("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)bar.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(length, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = bar.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    // The same child in the copy, found by its path under the original root.
    private static T Find<T>(Transform root, T original, Transform copy) where T : Component
    {
        string path = PathOf(root, original);
        return path.Length == 0 ? copy.GetComponent<T>() : copy.Find(path).GetComponent<T>();
    }

    private static string PathOf(Transform root, Component child)
    {
        string path = "";
        for (Transform current = child.transform; current != root; current = current.parent)
            path = path.Length == 0 ? current.name : current.name + "/" + path;
        return path;
    }
}
