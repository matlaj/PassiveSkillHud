using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PassiveSkillHud;

/// <summary>
/// Sits on a HUD and shows the watched body's passive skill, plus passives chosen in the loadout (extra loadout rows),
/// as a row of round icons above the skill bar. Each icon is a copy of the HUD's primary skill icon with the cooldown,
/// stock and key binding parts hidden. Its tooltip works like the skill bar's: hover it while the scoreboard is open.
/// </summary>
public class PassiveSkillDisplay : MonoBehaviour
{
    private HUD hud;
    private SkillLocator shownSkillLocator;
    private int shownSettingsVersion = -1;
    private readonly List<Icon> icons = new();

    private void Awake() => hud = GetComponent<HUD>();

    private void OnDestroy() => ClearIcons();

    private void LateUpdate()
    {
        var body = hud.targetBodyObject ? hud.targetBodyObject.GetComponent<CharacterBody>() : null;
        var skillLocator = body ? body.skillLocator : null;
        // ReferenceEquals, because a destroyed SkillLocator compares equal to null.
        if (!ReferenceEquals(skillLocator, shownSkillLocator) || shownSettingsVersion != PassiveSkillHudPlugin.SettingsVersion)
        {
            Rebuild(body, skillLocator);
        }

        if (icons.Count == 0 || !PrimaryIcon) return;
        foreach (var icon in icons) icon.Refresh();
        PlaceIcons();
    }

    private SkillIcon PrimaryIcon => hud.skillIcons is { Length: > 0 } ? hud.skillIcons[0] : null;

    private void Rebuild(CharacterBody body, SkillLocator skillLocator)
    {
        ClearIcons();
        shownSkillLocator = skillLocator;
        shownSettingsVersion = PassiveSkillHudPlugin.SettingsVersion;
        if (!skillLocator || !PrimaryIcon || !PassiveSkillHudPlugin.ShowPassives.Value) return;

        var titleColor = GetTooltipTitleColor(body);
        if (skillLocator.passiveSkill.enabled)
        {
            AddIcon(new Icon(skillLocator.passiveSkill), titleColor);
        }

        // Passives chosen in the loadout, like Acrid's Poison or Blight, are extra loadout rows rather than the passive
        // above. Same rows, in the same order, as the skill list on the character select screen.
        foreach (var skill in body.GetComponents<GenericSkill>())
        {
            if (skill.hideInCharacterSelect || skillLocator.FindSkillSlot(skill) != SkillSlot.None) continue;
            if (SharesSkillsWithSlot(skill, skillLocator)) continue;
            AddIcon(new Icon(skill), titleColor);
        }
    }

    /// <summary>
    /// Whether a loadout row offers any of the same skills as one of the four skill slots. Such a row is a second
    /// weapon, like MUL-T's second primary, rather than a passive: a passive row's skills are its own.
    /// </summary>
    private static bool SharesSkillsWithSlot(GenericSkill row, SkillLocator skillLocator)
    {
        if (!row.skillFamily) return false;
        foreach (var slot in new[] { skillLocator.primary, skillLocator.secondary, skillLocator.utility, skillLocator.special })
        {
            if (!slot || !slot.skillFamily) continue;
            foreach (var slotVariant in slot.skillFamily.variants)
            {
                foreach (var rowVariant in row.skillFamily.variants)
                {
                    if (rowVariant.skillDef == slotVariant.skillDef) return true;
                }
            }
        }
        return false;
    }

    private void AddIcon(Icon icon, Color titleColor)
    {
        if (!icon.TryCreate(PrimaryIcon, titleColor)) return;
        icons.Add(icon);
    }

    private void ClearIcons()
    {
        foreach (var icon in icons) icon.Destroy();
        icons.Clear();
    }

    private readonly List<Rect> slots = new();

    /// <summary>
    /// Lines the shown icons up in a row above the skill bar, right-aligned: the first above the bar's rightmost slot
    /// (the equipment slot), the next above the slot left of that, and so on. Done every frame, since the skill bar
    /// moves when the resolution or HUD scale changes.
    /// </summary>
    private void PlaceIcons()
    {
        // Worked out in the skill bar's own space, where its slots sit side by side.
        var primary = (RectTransform)PrimaryIcon.transform;
        var bar = primary.parent;
        var primaryBox = GetBox(primary, bar);
        var secondary = hud.skillIcons.Length > 1 ? hud.skillIcons[1] : null;
        var step = secondary
            ? Mathf.Abs(primary.localPosition.x - secondary.transform.localPosition.x)
            : primaryBox.width * 1.1f;
        var gap = step - primaryBox.width;

        FindSlots(bar, primaryBox);
        if (slots.Count == 0) slots.Add(primaryBox);
        var top = primaryBox.yMax;
        foreach (var slot in slots) top = Mathf.Max(top, slot.yMax);

        var shownCount = 0;
        foreach (var icon in icons) if (icon.IsShown) shownCount++;
        if (shownCount == 0) return;

        // Columns over the slots, then on to the left at the skill bar's spacing once there are more icons than slots.
        var lastSlot = slots[slots.Count - 1];
        float ColumnCenter(int column) => column < slots.Count
            ? slots[column].center.x
            : lastSlot.center.x - step * (column - slots.Count + 1);

        var left = ColumnCenter(shownCount - 1) - primaryBox.width / 2f;
        var right = ColumnCenter(0) + primaryBox.width / 2f;
        // A slot's own parts that stick out a little, like its flash when a skill comes off cooldown, sit below this
        // line and don't count as being in the way; the row keeps a gap above the slots anyway.
        var popupLine = top + primaryBox.height * 0.5f;
        var bottom = FindFreeBottom(bar, left, right, top + gap, primaryBox.height, gap, popupLine);

        var column = 0;
        foreach (var icon in icons)
        {
            if (!icon.IsShown) continue;
            // Where the primary skill icon would be if its box were in this column of the row.
            var offset = new Vector2(ColumnCenter(column) - primaryBox.center.x, bottom - primaryBox.yMin);
            icon.SetLocalPosition(primary.localPosition + (Vector3)offset);
            column++;
        }
    }

    /// <summary>
    /// Collects the boxes of the skill bar's slots, rightmost first: the skills, plus the equipment slots level with
    /// them. Equipment slots set higher or lower, such as the second one for multiple equipment, aren't part of the bar.
    /// </summary>
    private void FindSlots(Transform bar, Rect primaryBox)
    {
        slots.Clear();
        foreach (var skillIcon in hud.skillIcons) AddSlot(skillIcon, bar, primaryBox);
        foreach (var equipmentIcon in hud.equipmentIcons) AddSlot(equipmentIcon, bar, primaryBox);
        slots.Sort((a, b) => b.center.x.CompareTo(a.center.x));
    }

    private void AddSlot(Component slot, Transform bar, Rect primaryBox)
    {
        if (!slot || !slot.gameObject.activeInHierarchy) return;
        var box = GetBox((RectTransform)slot.transform, bar);
        if (Mathf.Abs(box.center.y - primaryBox.center.y) < primaryBox.height * 0.3f) slots.Add(box);
    }

    private readonly List<Graphic> graphics = new();

    /// <summary>
    /// The lowest the row can sit without covering anything. Normally that's where it was planned, one gap above the
    /// skill bar. This catches things this mod doesn't know about, such as a HUD element from a future update or another
    /// mod, or a hint popping up over a slot: the row moves up past anything it would cover.
    /// </summary>
    private float FindFreeBottom(Transform bar, float left, float right, float bottom, float height, float gap, float popupLine)
    {
        bar.GetComponentsInChildren(false, graphics);
        // Each pass moves the row past one obstacle; the cap only guards against a pathological layout.
        for (var pass = 0; pass < 8; pass++)
        {
            var obstacleTop = float.NegativeInfinity;
            foreach (var graphic in graphics)
            {
                if (!IsObstacle(graphic)) continue;
                var box = GetBox(graphic.rectTransform, bar);
                if (box.yMax <= popupLine && IsPartOfSlot(graphic.transform)) continue;
                if (box.xMax <= left || box.xMin >= right || box.yMax <= bottom || box.yMin >= bottom + height) continue;
                obstacleTop = Mathf.Max(obstacleTop, box.yMax);
            }
            if (float.IsNegativeInfinity(obstacleTop)) break;
            bottom = obstacleTop + gap;
        }
        return bottom;
    }

    private bool IsObstacle(Graphic graphic)
    {
        if (!graphic.enabled || graphic.color.a <= 0.01f) return false;
        if (graphic is TMP_Text text && string.IsNullOrEmpty(text.text)) return false;
        foreach (var icon in icons) if (icon.Contains(graphic.transform)) return false;
        return true;
    }

    private bool IsPartOfSlot(Transform t)
    {
        foreach (var skillIcon in hud.skillIcons) if (skillIcon && t.IsChildOf(skillIcon.transform)) return true;
        foreach (var equipmentIcon in hud.equipmentIcons) if (equipmentIcon && t.IsChildOf(equipmentIcon.transform)) return true;
        return false;
    }

    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>The rectangle a UI element covers, in the given transform's space.</summary>
    private static Rect GetBox(RectTransform rectTransform, Transform space)
    {
        rectTransform.GetWorldCorners(corners);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 local = space.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>The same title color the skill bar's tooltips use: the survivor's color, darkened a bit.</summary>
    private static Color GetTooltipTitleColor(CharacterBody body)
    {
        Color.RGBToHSV(body.bodyColor, out var h, out var s, out var v);
        return Color.HSVToRGB(h, s, Mathf.Min(v, 0.7f));
    }

    private sealed class Icon
    {
        // Set for a skill from an extra loadout row, which can change mid-run; otherwise this is the passive.
        private readonly GenericSkill skill;
        private readonly SkillLocator.PassiveSkill passive;

        private GameObject root;
        private Image image;
        private TooltipProvider tooltip;
        private bool shown;
        private Object shownSkillDef;
        private string shownLanguage;

        public Icon(SkillLocator.PassiveSkill passive) => this.passive = passive;
        public Icon(GenericSkill skill) => this.skill = skill;

        /// <summary>
        /// Copies the HUD's primary skill icon and strips it down to the picture and the tooltip.
        /// </summary>
        public bool TryCreate(SkillIcon template, Color titleColor)
        {
            root = Object.Instantiate(template.gameObject, template.transform.parent);
            root.name = "PassiveSkillIcon";
            var copy = root.GetComponent<SkillIcon>();

            // The copy's references only point into the copy for parts inside the icon's own hierarchy.
            // Anything else still belongs to the real primary icon and must be left alone.
            image = Owns(copy.iconImage) ? copy.iconImage : null;
            if (!image)
            {
                PassiveSkillHudPlugin.Log.LogWarning("The HUD's skill icon has an unexpected layout; not showing passive skills.");
                Destroy();
                return false;
            }
            tooltip = Owns(copy.tooltipProvider) ? copy.tooltipProvider : root.GetComponentInChildren<TooltipProvider>();
            if (!tooltip)
            {
                tooltip = image.gameObject.AddComponent<TooltipProvider>();
                image.raycastTarget = true;
            }
            tooltip.titleColor = titleColor;

            if (Owns(copy.cooldownText)) copy.cooldownText.gameObject.SetActive(false);
            if (Owns(copy.stockText)) copy.stockText.gameObject.SetActive(false);
            if (Owns(copy.flashPanelObject)) copy.flashPanelObject.SetActive(false);
            if (Owns(copy.cooldownRemapPanel)) copy.cooldownRemapPanel.enabled = false;
            if (Owns(copy.animator)) copy.animator.enabled = false;
            foreach (var binding in root.GetComponentsInChildren<InputBindingDisplayController>(true))
            {
                // The key binding sits on its own background panel; hide that too unless it also holds the picture.
                var panel = binding.transform.parent;
                var hide = panel != root.transform && !image.transform.IsChildOf(panel) ? panel : binding.transform;
                if (hide != root.transform) hide.gameObject.SetActive(false);
            }
            var readyPanel = Owns(copy.isReadyPanelObject) ? copy.isReadyPanelObject : null;
            if (readyPanel) readyPanel.SetActive(true);
            Object.DestroyImmediate(copy);

            MakeRound(readyPanel);

            // Placed by hand, so a layout group on the skill bar mustn't move it.
            var layoutElement = root.GetComponent<LayoutElement>();
            if (!layoutElement) layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            Refresh();
            return true;
        }

        /// <summary>
        /// Turns the square icon into a round one, the usual look for a passive: the background becomes a disc,
        /// the picture is clipped to a circle, and the "ready" outline becomes a ring drawn over the clipped edge.
        /// </summary>
        private void MakeRound(GameObject readyPanel)
        {
            var background = root.GetComponent<Image>();
            if (background) UseSprite(background, RoundSprites.Disc);

            var mask = new GameObject("RoundMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            mask.layer = image.gameObject.layer;
            var maskTransform = (RectTransform)mask.transform;
            var imageTransform = image.rectTransform;
            maskTransform.SetParent(imageTransform.parent, false);
            maskTransform.SetSiblingIndex(imageTransform.GetSiblingIndex());
            maskTransform.anchorMin = imageTransform.anchorMin;
            maskTransform.anchorMax = imageTransform.anchorMax;
            maskTransform.pivot = imageTransform.pivot;
            maskTransform.offsetMin = imageTransform.offsetMin;
            maskTransform.offsetMax = imageTransform.offsetMax;
            var maskImage = mask.GetComponent<Image>();
            UseSprite(maskImage, RoundSprites.Disc);
            maskImage.raycastTarget = false;
            mask.GetComponent<Mask>().showMaskGraphic = false;

            imageTransform.SetParent(maskTransform, false);
            imageTransform.anchorMin = Vector2.zero;
            imageTransform.anchorMax = Vector2.one;
            imageTransform.offsetMin = Vector2.zero;
            imageTransform.offsetMax = Vector2.zero;

            // A mask's edge is hard, so the ring goes on top to cover it. It's sized to the picture plus a sliver,
            // not to the square outline's frame, so it's about as thick as the skill bar's outlines.
            var outline = readyPanel ? readyPanel.GetComponent<Image>() : null;
            if (!outline) return;
            UseSprite(outline, RoundSprites.Ring);
            var outlineTransform = outline.rectTransform;
            outlineTransform.SetParent(maskTransform.parent, false);
            outlineTransform.SetAsLastSibling();
            outlineTransform.anchorMin = maskTransform.anchorMin;
            outlineTransform.anchorMax = maskTransform.anchorMax;
            outlineTransform.pivot = maskTransform.pivot;
            outlineTransform.offsetMin = maskTransform.offsetMin - Vector2.one * RingMargin;
            outlineTransform.offsetMax = maskTransform.offsetMax + Vector2.one * RingMargin;
        }

        // How far the ring reaches past the round picture, in the skill icon's units (it's 36 across).
        private const float RingMargin = 1f;

        private static void UseSprite(Image target, Sprite sprite)
        {
            target.sprite = sprite;
            target.type = Image.Type.Simple;
            target.preserveAspect = false;
        }

        private bool Owns(Component component) => component && component.transform.IsChildOf(root.transform);
        private bool Owns(GameObject obj) => obj && obj.transform.IsChildOf(root.transform);

        public bool IsShown => root && root.activeSelf;

        public bool Contains(Transform transform) => root && transform.IsChildOf(root.transform);

        public void SetLocalPosition(Vector3 position)
        {
            // Moving a UI element makes its canvas redraw, so only move it when the skill bar has moved.
            if (root && root.transform.localPosition != position) root.transform.localPosition = position;
        }

        public void Refresh()
        {
            if (!root) return;

            var skillDef = skill ? skill.skillDef : null;
            var language = Language.currentLanguageName;
            if (shown && skillDef == shownSkillDef && language == shownLanguage) return;
            shown = true;
            shownSkillDef = skillDef;
            shownLanguage = language;

            if (skill)
            {
                root.SetActive(skillDef);
                if (!skillDef) return;
                Show(skill.icon, skill.skillNameToken, skill.skillDescriptionToken, skillDef.keywordTokens);
            }
            else
            {
                var keywords = string.IsNullOrEmpty(passive.keywordToken) ? null : new[] { passive.keywordToken };
                Show(passive.icon, passive.skillNameToken, passive.skillDescriptionToken, keywords);
            }
        }

        private void Show(Sprite sprite, string nameToken, string descriptionToken, string[] keywordTokens)
        {
            image.sprite = sprite;
            image.color = Color.white;
            tooltip.titleToken = nameToken;
            tooltip.bodyToken = descriptionToken;
            // Like the character select screen, explain the keywords (e.g. Poison) under the description.
            tooltip.overrideBodyText = BuildBodyText(descriptionToken, keywordTokens);
        }

        private static string BuildBodyText(string descriptionToken, string[] keywordTokens)
        {
            if (keywordTokens is not { Length: > 0 }) return null;
            var text = new StringBuilder(Language.GetString(descriptionToken));
            foreach (var keywordToken in keywordTokens)
            {
                if (!string.IsNullOrEmpty(keywordToken)) text.Append("\n\n").Append(Language.GetString(keywordToken));
            }
            return text.ToString();
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root);
            root = null;
        }
    }
}
