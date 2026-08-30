using HairSalon;
using UnityEngine;

/// <summary>
/// Owns one customer's world UI while allowing its display position to come from a fixed seat anchor.
/// The component stays on the customer instance; only the visual root is reparented.
/// </summary>
public sealed class CustomerUIRootView : MonoBehaviour
{
    private CustomerModel _customer;
    private Transform _visualRoot;
    private Transform _currentAnchor;
    private Transform _mobileAnchor;
    private Transform _requirementContainer;
    private Transform _emotionContainer;
    private bool _isSelected;

    public CustomerModel Customer => _customer;
    public Transform VisualRoot => _visualRoot;
    public Transform CurrentAnchor => _currentAnchor;
    public Transform RequirementContainer => _requirementContainer;
    public Transform EmotionContainer => _emotionContainer;
    public bool IsVisible => _visualRoot != null && _visualRoot.gameObject.activeSelf;

    public void Initialize(CustomerModel customer)
    {
        if (_customer != null || customer == null) return;
        _customer = customer;

        _visualRoot = new GameObject("Customer UI Root").transform;
        _visualRoot.SetParent(transform, false);
        _requirementContainer = new GameObject("RequirementContainer").transform;
        _requirementContainer.SetParent(_visualRoot, false);
        _emotionContainer = new GameObject("EmotionSlot").transform;
        _emotionContainer.SetParent(_visualRoot, false);
        _visualRoot.gameObject.SetActive(false);
    }

    public void SetAnchor(Transform anchor)
    {
        if (_visualRoot == null) return;
        if (anchor == null)
        {
            _currentAnchor = null;
            _visualRoot.gameObject.SetActive(false);
            return;
        }
        _visualRoot.gameObject.SetActive(_isSelected);
        if (_currentAnchor == anchor) return;
        _currentAnchor = anchor;
        _visualRoot.SetParent(anchor, false);
        _visualRoot.localPosition = Vector3.zero;
        _visualRoot.localRotation = Quaternion.identity;
        _visualRoot.localScale = Vector3.one;
    }

    public void SetVisible(bool visible)
    {
        if (_visualRoot != null) _visualRoot.gameObject.SetActive(visible);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(selected && _currentAnchor != null);
    }

    public void SetMobileAnchor(Vector3 worldPosition)
    {
        if (_visualRoot == null) return;
        if (_mobileAnchor == null)
        {
            _mobileAnchor = new GameObject("Customer Mobile UI Anchor").transform;
            _mobileAnchor.SetParent(transform.parent, false);
        }
        _mobileAnchor.position = worldPosition;
        SetAnchor(_mobileAnchor);
    }

    private void OnDestroy()
    {
        if (_visualRoot != null)
        {
            if (Application.isPlaying) Destroy(_visualRoot.gameObject);
            else DestroyImmediate(_visualRoot.gameObject);
            _visualRoot = null;
        }
        if (_mobileAnchor != null)
        {
            if (Application.isPlaying) Destroy(_mobileAnchor.gameObject);
            else DestroyImmediate(_mobileAnchor.gameObject);
            _mobileAnchor = null;
        }
    }
}
