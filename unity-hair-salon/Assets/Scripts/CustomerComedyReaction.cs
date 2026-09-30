using System.Collections.Generic;
using HairSalon;
using UnityEngine;
using UnityEngine.UI;

public enum CustomerComedyReactionKind
{
    None,
    Overcut,
    FoamTooLong,
    BlowTooLong,
    CheckoutTooLong,
    Satisfied
}

public sealed class CustomerComedyReaction : MonoBehaviour
{
    private struct Pose
    {
        public Transform Transform;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private readonly List<Pose> _poses = new List<Pose>();
    private readonly List<Transform> _foam = new List<Transform>();
    private readonly List<Transform> _smoke = new List<Transform>();
    private Transform _root;
    private Text _bubble;
    private GameObject _bubbleRoot;
    private CustomerMoodStage _mood;
    private CustomerComedyReactionKind _kind;
    private float _elapsed;
    private float _duration;
    private bool _playing;
    private Font _font;

    public bool IsPlaying => _playing;
    public CustomerMoodStage Mood => _mood;
    public CustomerComedyReactionKind ReactionKind => _kind;
    public float StormOutSpeedMultiplier => CustomerMoodModel.StormOutSpeedMultiplier(_mood);
    public string BubbleText => _bubble == null ? string.Empty : _bubble.text;

    public static CustomerComedyReaction Attach(Transform customerRoot, Font font)
    {
        if (customerRoot == null) return null;
        var reaction = customerRoot.GetComponent<CustomerComedyReaction>();
        if (reaction == null) reaction = customerRoot.gameObject.AddComponent<CustomerComedyReaction>();
        reaction.Configure(customerRoot, font);
        return reaction;
    }

    public void Configure(Transform customerRoot, Font font)
    {
        _root = customerRoot == null ? transform : customerRoot;
        _font = font == null ? Resources.Load<Font>("Fonts/NotoSansSC-UI") : font;
        if (_bubbleRoot == null) BuildVisuals();
    }

    public void SetMood(CustomerMoodStage stage)
    {
        _mood = stage;
        if (_playing && _kind != CustomerComedyReactionKind.None) return;

    }

    public void Play(CustomerComedyReactionKind kind)
    {
        if (kind == CustomerComedyReactionKind.None) return;
        if (_playing && Priority(kind) < Priority(_kind)) return;
        _kind = kind;
        _elapsed = 0f;
        _duration = kind == CustomerComedyReactionKind.Satisfied ? 1f : 2.4f;
        _playing = true;
        _bubble.text = BubbleFor(kind);
        _bubbleRoot.SetActive(true);
    }

    public void Play(CustomerReactionKind kind)
    {
        if (kind == CustomerReactionKind.None) return;
        // Generic confusion is a wrong-station hint, never a foam accident.
        if (kind == CustomerReactionKind.Angry) SetMood(CustomerMoodStage.Angry);
    }

    public void Stop()
    {
        _playing = false;
        _kind = CustomerComedyReactionKind.None;
        _bubbleRoot?.SetActive(false);
        ResetPoses();
        ClearEffects();
        ApplyMood(_mood, 0f);
    }

    public void RestoreFramePose() => ResetPoses();

    public void RenderFrame(Camera camera)
    {
        CapturePoses();
        ClearEffects();
        _bubbleRoot.transform.localPosition = new Vector3(0f, 3.65f, 0f);
        if (camera != null)
        {
            _bubbleRoot.transform.rotation = camera.transform.rotation;
            // Keep the punchline below the fixed top HUD at the landscape viewport.
            Vector3 screen = camera.WorldToScreenPoint(_bubbleRoot.transform.position);
            screen.y = Mathf.Min(screen.y, Screen.height * .66f);
            // The reclining wash pose puts this bubble over the rinse timer.
            // Keep both the punchline and the required next action readable.
            if (_kind == CustomerComedyReactionKind.FoamTooLong)
                screen.x += Screen.width * .19f;
            screen.x = Mathf.Clamp(screen.x, Screen.width * .08f, Screen.width * .92f);
            _bubbleRoot.transform.position = camera.ScreenToWorldPoint(screen);
        }
        if (_root == null) return;
        if (_playing)
        {
            _elapsed += Time.unscaledDeltaTime;
            AnimateReaction(Mathf.Clamp01(_elapsed / _duration));
            if (_elapsed >= _duration)
            {
                bool storm = _kind != CustomerComedyReactionKind.Satisfied;
                _playing = false;
                _bubbleRoot.SetActive(false);
                ResetPoses();
                ClearEffects();
                if (storm && _mood != CustomerMoodStage.Leaving) _mood = CustomerMoodStage.Angry;
                ApplyMood(_mood, 0f);
                if (_kind == CustomerComedyReactionKind.Satisfied) _kind = CustomerComedyReactionKind.None;
            }
        }
        else
        {
            ApplyMood(_mood, Time.unscaledTime);
        }
    }

    private void BuildVisuals()
    {
        _bubbleRoot = new GameObject("Customer Comedy Bubble", typeof(Canvas), typeof(CanvasScaler));
        _bubbleRoot.transform.SetParent(_root, false);
        _bubbleRoot.transform.localPosition = new Vector3(0f, 3.65f, 0f);
        _bubbleRoot.transform.localScale = Vector3.one * .011f;
        var canvas = _bubbleRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;
        _bubbleRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 74f);
        var panel = new GameObject("Comedy Bubble Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_bubbleRoot.transform, false);
        var image = panel.GetComponent<Image>();
        image.sprite = SalonUiFactory.GetRoundedPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(.98f, .88f, .55f, .98f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 62f);
        var textObject = new GameObject("Comedy Bubble Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(_bubbleRoot.transform, false);
        _bubble = textObject.GetComponent<Text>();
        _bubble.font = _font == null ? SalonUiFactory.GetPackagedUiFont() : _font;
        _bubble.fontSize = 31;
        _bubble.fontStyle = FontStyle.Bold;
        _bubble.alignment = TextAnchor.MiddleCenter;
        _bubble.color = SalonPalette.Ink;
        _bubble.raycastTarget = false;
        textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 62f);
        _bubbleRoot.SetActive(false);
        for (int i = 0; i < 4; i++)
        {
            _foam.Add(CreateEffect("泡沫球 " + i, new Vector3((i - 1.5f) * .16f, 2.72f, -.1f), .1f, Color.white));
            _smoke.Add(CreateEffect("黑烟 " + i, new Vector3((i - 1.5f) * .16f, 2.9f, .05f), .11f, new Color(.18f, .17f, .2f, .9f)));
        }
        ClearEffects();
    }

    private Transform CreateEffect(string name, Vector3 position, float size, Color color)
    {
        var go = SalonPrimitiveFactory.CreateSphere(_root, position, Vector3.one * size, color);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        return go.transform;
    }

    private void CapturePoses()
    {
        _poses.Clear();
        string[] names = { "Body", "Apron", "Head", "Eye L", "Eye R", "Leg L", "Leg R" };
        for (int i = 0; i < names.Length; i++)
        {
            var child = FindChild(names[i]);
            if (child != null) _poses.Add(new Pose { Transform = child, Position = child.localPosition, Rotation = child.localRotation, Scale = child.localScale });
        }
        foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("Faceted Hair"))
                _poses.Add(new Pose { Transform = child, Position = child.localPosition, Rotation = child.localRotation, Scale = child.localScale });
    }

    private Transform FindChild(string name)
    {
        foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private void ResetPoses()
    {
        for (int i = 0; i < _poses.Count; i++)
        {
            var pose = _poses[i];
            if (pose.Transform == null) continue;
            pose.Transform.localPosition = pose.Position;
            pose.Transform.localRotation = pose.Rotation;
            pose.Transform.localScale = pose.Scale;
        }
    }

    private void AnimateReaction(float t)
    {
        float pulse = Mathf.Sin(t * Mathf.PI);
        if (_kind == CustomerComedyReactionKind.Overcut)
        {
            OffsetAll(new Vector3(0f, Mathf.Sin(t * Mathf.PI * 5f) * .14f * (1f - t), 0f));
            ScaleNamed("Head", 1f + .06f * pulse);
            ShowEffects(_smoke, pulse);
        }
        else if (_kind == CustomerComedyReactionKind.FoamTooLong)
        {
            ShowEffects(_foam, Mathf.Clamp01(t * 2f));
            ScaleNamed("Eye L", 1f - .28f * pulse);
            ScaleNamed("Eye R", 1f - .28f * pulse);
        }
        else if (_kind == CustomerComedyReactionKind.BlowTooLong)
        {
            ScaleHair(1f + .5f * pulse);
            ShowEffects(_smoke, pulse);
        }
        else
        {
            OffsetAll(new Vector3(0f, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * .12f, 0f));
        }
    }

    private void ApplyMood(CustomerMoodStage stage, float time)
    {
        if (stage == CustomerMoodStage.Calm) { _bubbleRoot.SetActive(false); return; }
        float shake = stage == CustomerMoodStage.Impatient ? .025f : .07f;
        if (stage == CustomerMoodStage.Leaving) shake = .11f;
        OffsetAll(new Vector3(Mathf.Sin(time * 42f) * shake, Mathf.Sin(time * 36f) * shake, 0f));
        if (stage == CustomerMoodStage.Angry || stage == CustomerMoodStage.Leaving)
        {
            ShowEffects(_smoke, .7f);
        }
        if (stage == CustomerMoodStage.Leaving && _bubble != null && !_playing)
        {
            _bubble.text = _kind == CustomerComedyReactionKind.CheckoutTooLong ? "不给钱了！" : "不等了！";
            _bubbleRoot.SetActive(true);
        }
    }

    private void OffsetAll(Vector3 offset)
    {
        for (int i = 0; i < _poses.Count; i++)
            if (_poses[i].Transform != null) _poses[i].Transform.localPosition = _poses[i].Position + offset;
    }

    private void ScaleNamed(string name, float factor)
    {
        var child = FindChild(name);
        if (child == null) return;
        for (int i = 0; i < _poses.Count; i++)
            if (_poses[i].Transform == child) child.localScale = _poses[i].Scale * factor;
    }

    private void ScaleHair(float factor)
    {
        for (int i = 0; i < _poses.Count; i++)
            if (_poses[i].Transform != null && _poses[i].Transform.name.StartsWith("Faceted Hair"))
                _poses[i].Transform.localScale = _poses[i].Scale * factor;
    }

    private void TintEyes(bool red)
    {
        Color color = red ? new Color(1f, .08f, .04f) : Color.white;
        var left = FindChild("Eye L");
        var right = FindChild("Eye R");
        if (left != null) SetColor(left, color);
        if (right != null) SetColor(right, color);
    }

    private static void SetColor(Transform target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null) renderer.material.color = color;
    }

    private void ShowEffects(List<Transform> effects, float amount)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            effects[i].gameObject.SetActive(i < Mathf.CeilToInt(amount * effects.Count));
            effects[i].localScale = Vector3.one * (.06f + amount * .1f);
        }
    }

    private void ClearEffects()
    {
        for (int i = 0; i < _foam.Count; i++) _foam[i].gameObject.SetActive(false);
        for (int i = 0; i < _smoke.Count; i++) _smoke[i].gameObject.SetActive(false);
    }

    private static int Priority(CustomerComedyReactionKind kind)
    {
        return kind == CustomerComedyReactionKind.Satisfied ? 1 :
            kind == CustomerComedyReactionKind.None ? 0 : 2;
    }

    private static string BubbleFor(CustomerComedyReactionKind kind)
    {
        switch (kind)
        {
            case CustomerComedyReactionKind.Overcut: return "我的头发！！";
            case CustomerComedyReactionKind.FoamTooLong: return "辣眼睛！";
            case CustomerComedyReactionKind.BlowTooLong: return "头发炸了！";
            case CustomerComedyReactionKind.CheckoutTooLong: return "不给钱了！";
            case CustomerComedyReactionKind.Satisfied: return "好舒服！";
            default: return string.Empty;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        if (_bubbleRoot != null) Destroy(_bubbleRoot);
        for (int i = 0; i < _foam.Count; i++) if (_foam[i] != null) Destroy(_foam[i].gameObject);
        for (int i = 0; i < _smoke.Count; i++) if (_smoke[i] != null) Destroy(_smoke[i].gameObject);
    }
}
