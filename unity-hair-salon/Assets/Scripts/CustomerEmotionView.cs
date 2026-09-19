using System.Collections;
using HairSalon;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A replaceable geometric emotion slot. It deliberately contains no development words.</summary>
public sealed class CustomerEmotionView : MonoBehaviour
{
    private CustomerModel _customer;
    private GameObject _badge;
    private Image _background;
    private RectTransform _leftEye;
    private RectTransform _rightEye;
    private RectTransform _leftMouth;
    private RectTransform _rightMouth;
    private Image _sweatTip;
    private Text _reactionSymbol;
    private Coroutine _wrongServicePulse;

    public bool IsVisible => _badge != null && _badge.activeSelf;
    public string Symbol => string.Empty;
    public string VisualKind => _customer == null ? "None" :
        _customer.WrongServiceKind == CustomerWrongServiceKind.Wash ? "WrongWash" :
        _customer.WrongServiceKind == CustomerWrongServiceKind.Dry ? "WrongDry" :
        _customer.WrongServiceKind == CustomerWrongServiceKind.Haircut ? "WrongHaircut" :
        _customer.ReactionKind == CustomerReactionKind.Confused ? "Confused" :
        _customer.ReactionKind == CustomerReactionKind.Protest ? "Protest" :
        _customer.ReactionKind == CustomerReactionKind.Resistance ? "Resistance" :
        _customer.Emotion == CustomerEmotion.Impatient ? "BlueSweat" :
        _customer.Emotion == CustomerEmotion.Angry ? "RedAnger" :
        _customer.Emotion == CustomerEmotion.Happy ? "GreenHappy" : "None";

    public void Initialize(CustomerModel customer, Camera sceneCamera)
    {
        if (_customer != null || customer == null) return;
        _customer = customer;
        _badge = new GameObject("Low-Poly Emotion Placeholder Slot", typeof(Canvas), typeof(CanvasScaler));
        _badge.transform.SetParent(transform, false);
        _badge.transform.localPosition = new Vector3(1.62f, 3.18f, 0f);
        if (sceneCamera != null) _badge.transform.rotation = sceneCamera.transform.rotation;
        _badge.transform.localScale = Vector3.one * .009f;

        Canvas canvas = _badge.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 24;
        _badge.GetComponent<RectTransform>().sizeDelta = new Vector2(62f, 62f);

        _background = Panel("Faceted Face", _badge.transform, Vector2.zero, new Vector2(58f, 58f), Color.white);
        _background.sprite = SalonUiFactory.GetCircleSprite();
        _leftEye = Panel("Left Eye", _badge.transform, new Vector2(-13f, 9f), new Vector2(9f, 7f), Color.white).rectTransform;
        _rightEye = Panel("Right Eye", _badge.transform, new Vector2(13f, 9f), new Vector2(9f, 7f), Color.white).rectTransform;
        _leftMouth = Panel("Mouth Left", _badge.transform, new Vector2(-6f, -11f), new Vector2(17f, 5f), Color.white).rectTransform;
        _rightMouth = Panel("Mouth Right", _badge.transform, new Vector2(6f, -11f), new Vector2(17f, 5f), Color.white).rectTransform;
        _sweatTip = Panel("Low-Poly Sweat Tip", _badge.transform, new Vector2(-11f, 17f), new Vector2(18f, 18f), Color.white);
        _sweatTip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        var symbolObject = new GameObject("Reaction Symbol", typeof(RectTransform), typeof(Text));
        symbolObject.transform.SetParent(_badge.transform, false);
        _reactionSymbol = symbolObject.GetComponent<Text>();
        _reactionSymbol.rectTransform.sizeDelta = new Vector2(58f, 58f);
        _reactionSymbol.font = SalonUiFactory.GetPackagedUiFont();
        _reactionSymbol.fontSize = 34;
        _reactionSymbol.fontStyle = FontStyle.Bold;
        _reactionSymbol.alignment = TextAnchor.MiddleCenter;
        _reactionSymbol.color = SalonPalette.Ink;
        _reactionSymbol.raycastTarget = false;
        SalonUiFactory.MakeClickThrough(_badge);
        Refresh();
    }

    public void Refresh()
    {
        if (_customer == null || _badge == null) return;
        if (!isActiveAndEnabled)
            StopWrongServicePulse();
        bool hasFoamBurst = _customer.HasUnresolvedFoamBurstEvent;
        bool hasReaction = _customer.ReactionKind != CustomerReactionKind.None
            && _customer.ReactionRemaining > 0f;
        hasReaction = hasReaction || hasFoamBurst;
        bool visible = hasReaction || _customer.Emotion != CustomerEmotion.Calm;
        _badge.SetActive(visible);
        if (!visible)
        {
            StopWrongServicePulse();
            return;
        }
        _reactionSymbol.gameObject.SetActive(hasReaction);
        if (hasFoamBurst)
        {
            SetFaceVisible(false);
            ResetFaceBackground();
            _sweatTip.gameObject.SetActive(false);
            _background.color = new Color(.96f, .8f, .16f, .97f);
            _reactionSymbol.text = "🫧";
            return;
        }
        if (hasReaction)
        {
            SetFaceVisible(false);
            ResetFaceBackground();
            _sweatTip.gameObject.SetActive(false);
            bool wrongWash = _customer.WrongServiceKind == CustomerWrongServiceKind.Wash;
            bool wrongDry = _customer.WrongServiceKind == CustomerWrongServiceKind.Dry;
            bool wrongHaircut = _customer.WrongServiceKind == CustomerWrongServiceKind.Haircut;
            if (wrongWash)
            {
                _background.color = new Color(.96f, .79f, .31f, .97f);
                _reactionSymbol.text = "♨";
            }
            else if (wrongHaircut)
            {
                _background.color = new Color(.95f, .48f, .23f, .97f);
                _reactionSymbol.text = "✂";
            }
            else if (wrongDry)
            {
                _background.color = new Color(.95f, .48f, .23f, .97f);
                _reactionSymbol.text = "≋";
            }
            else
            {
                _background.color = _customer.ReactionKind == CustomerReactionKind.Confused
                    ? new Color(.96f, .79f, .31f, .97f)
                    : new Color(.95f, .48f, .23f, .97f);
                _reactionSymbol.text = _customer.ReactionKind == CustomerReactionKind.Confused ? "?" : "!";
            }

            if (wrongWash || wrongDry || wrongHaircut)
                StartWrongServicePulse();
            else
                StopWrongServicePulse();
            return;
        }

        switch (_customer.Emotion)
        {
            case CustomerEmotion.Impatient:
                SetFaceVisible(false);
                _background.color = new Color(.16f, .58f, .92f, .97f);
                _background.rectTransform.sizeDelta = new Vector2(34f, 46f);
                _background.rectTransform.anchoredPosition = new Vector2(5f, -4f);
                _background.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
                _sweatTip.color = _background.color;
                _sweatTip.gameObject.SetActive(true);
                break;
            case CustomerEmotion.Happy:
                SetFaceVisible(true);
                ResetFaceBackground();
                _background.color = new Color(.22f, .72f, .34f, .96f);
                SetFace(0f, 0f, 18f, -18f);
                break;
            default:
                SetFaceVisible(true);
                ResetFaceBackground();
                _background.color = new Color(.91f, .22f, .17f, .96f);
                SetFace(18f, -18f, -18f, 18f);
                break;
        }
    }

    private void ResetFaceBackground()
    {
        _background.rectTransform.sizeDelta = new Vector2(58f, 58f);
        _background.rectTransform.anchoredPosition = Vector2.zero;
        _background.rectTransform.localEulerAngles = Vector3.zero;
        _sweatTip.gameObject.SetActive(false);
    }

    private void SetFaceVisible(bool visible)
    {
        _leftEye.gameObject.SetActive(visible);
        _rightEye.gameObject.SetActive(visible);
        _leftMouth.gameObject.SetActive(visible);
        _rightMouth.gameObject.SetActive(visible);
    }

    private void SetFace(float leftEyeAngle, float rightEyeAngle, float leftMouthAngle, float rightMouthAngle)
    {
        _leftEye.localEulerAngles = new Vector3(0f, 0f, leftEyeAngle);
        _rightEye.localEulerAngles = new Vector3(0f, 0f, rightEyeAngle);
        _leftMouth.localEulerAngles = new Vector3(0f, 0f, leftMouthAngle);
        _rightMouth.localEulerAngles = new Vector3(0f, 0f, rightMouthAngle);
    }

    private void StartWrongServicePulse()
    {
        // Refresh can arrive while the owning customer UI is hidden by a seat/selection
        // transition. Unity rejects StartCoroutine on an inactive hierarchy; leave the
        // reaction visible and let the next active refresh start the pulse instead.
        if (_badge == null || !isActiveAndEnabled || !_badge.activeInHierarchy) return;
        if (_wrongServicePulse != null) return;
        _wrongServicePulse = StartCoroutine(WrongServicePulse());
    }

    private void StopWrongServicePulse()
    {
        if (_wrongServicePulse == null) return;
        StopCoroutine(_wrongServicePulse);
        _wrongServicePulse = null;
    }

    private IEnumerator WrongServicePulse()
    {
        if (_badge == null) { _wrongServicePulse = null; yield break; }
        Vector3 originPosition = _badge.transform.localPosition;
        const float duration = .45f;
        float elapsed = 0f;
        while (elapsed < duration && _badge != null)
        {
            elapsed += Time.deltaTime;
            float strength = 1f - Mathf.Clamp01(elapsed / duration);
            float offset = Mathf.Sin(elapsed * 70f) * .2f * strength;
            _badge.transform.localPosition = originPosition + new Vector3(offset, 0f, 0f);
            yield return null;
        }

        if (_badge != null) _badge.transform.localPosition = originPosition;
        _wrongServicePulse = null;
    }

    private void OnDisable()
    {
        StopWrongServicePulse();
    }

    private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
